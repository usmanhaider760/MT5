//+------------------------------------------------------------------+
//|  TradingBotEA.mq5  — Production v3.0                            |
//|  C# ↔ MT5 bridge via Named Pipe (length-prefixed binary frames)  |
//|  Matches C# MT5Bridge.cs framing exactly.                        |
//+------------------------------------------------------------------+
#property copyright "MT5TradingBot"
#property version   "3.00"
#property strict

#include <Trade\Trade.mqh>
#include <Trade\PositionInfo.mqh>
#include <Trade\OrderInfo.mqh>

//── Inputs ────────────────────────────────────────────────────────
input string InpPipeName    = "MT5TradingBotPipe"; // Named pipe name
input int    InpBufSize     = 1048576;               // Buffer size (128 KB)
input bool   InpEnableLog   = true;                 // Verbose logging
input int    InpMagic       = 999001;               // Default magic number
input int    InpSlippage    = 10;                   // Max slippage points
input bool   InpFillIOC     = true;                 // Use IOC fill policy

//── Globals ───────────────────────────────────────────────────────
CTrade       Trade;
CPositionInfo PosInfo;

int  g_pipe      = INVALID_HANDLE;
bool g_connected = false;
int  g_totalServed = 0;

double g_AsianHigh = 0;
double g_AsianLow  = 0;

double g_VwapNumerator   = 0;
double g_VwapDenominator = 0;
double g_Vwap            = 0;

//+------------------------------------------------------------------+
int OnInit()
{
   Trade.SetExpertMagicNumber(InpMagic);
   Trade.SetDeviationInPoints(InpSlippage);
   Trade.SetTypeFilling(InpFillIOC ? ORDER_FILLING_IOC : ORDER_FILLING_FOK);
   Trade.SetAsyncMode(false); // synchronous — wait for fill confirmation

   EventSetMillisecondTimer(100); // poll 10×/sec
   EA_Log(StringFormat("EA v3.0 started | Pipe: %s | Magic: %d", InpPipeName, InpMagic));
   return INIT_SUCCEEDED;
}

//+------------------------------------------------------------------+
void OnDeinit(const int reason)
{
   EventKillTimer();
   ClosePipe();
   EA_Log(StringFormat("EA stopped. Served %d requests.", g_totalServed));
}

//+------------------------------------------------------------------+
void OnTimer()
{
   ServeOnePipeRequest();
}

// Expose hook on tick too for lower latency
void OnTick()
{
   MqlDateTime dt;
   TimeToStruct(TimeGMT(), dt);
   int hour   = dt.hour;
   int minute = dt.min;
   double bid = SymbolInfoDouble(_Symbol, SYMBOL_BID);

   if (hour == 0 && minute == 0)
   {
      g_AsianHigh      = 0;
      g_AsianLow       = 0;
      g_VwapNumerator  = 0;
      g_VwapDenominator = 0;
      g_Vwap           = 0;
   }
   if (hour >= 0 && hour < 7)
   {
      if (g_AsianHigh == 0 || bid > g_AsianHigh) g_AsianHigh = bid;
      if (g_AsianLow  == 0 || bid < g_AsianLow)  g_AsianLow  = bid;
   }

   double ask = SymbolInfoDouble(_Symbol, SYMBOL_ASK);
   double typicalPrice = (bid + ask + iClose(_Symbol, PERIOD_M1, 0)) / 3.0;
   double tickVol = (double)iVolume(_Symbol, PERIOD_M1, 0);
   if (tickVol > 0)
   {
      g_VwapNumerator   += typicalPrice * tickVol;
      g_VwapDenominator += tickVol;
      g_Vwap = g_VwapDenominator > 0 ? g_VwapNumerator / g_VwapDenominator : typicalPrice;
   }

   ServeOnePipeRequest();
}

//+------------------------------------------------------------------+
//| One iteration: accept → read → process → respond → close        |
//+------------------------------------------------------------------+
void ServeOnePipeRequest()
{
   //── 1. Open pipe server if not open ──────────────────────────────
   if(g_pipe == INVALID_HANDLE)
   {
      // Windows named pipe path — MQL5 FileOpen uses the real pipe namespace
      string pipePath = "\\\\.\\pipe\\" + InpPipeName;
      // FILE_READ|FILE_WRITE|FILE_BIN: raw binary bidirectional
      g_pipe = FileOpen(pipePath, FILE_READ | FILE_WRITE | FILE_BIN);
      if(g_pipe == INVALID_HANDLE)
         return; // client not yet connected — normal
      g_connected = true;
      EA_Log("Client connected");
   }

   //── 2. Check if client wrote data ────────────────────────────────
   if(FileSize(g_pipe) < 4) return; // need at least 4 bytes for length prefix

   //── 3. Read 4-byte length prefix ─────────────────────────────────
   uint msgLen = 0;
   uchar lenBuf[];
   ArrayResize(lenBuf, 4);
   if(FileReadArray(g_pipe, lenBuf, 0, 4) != 4)
   {
      EA_Log("Failed to read length prefix — closing pipe");
      ClosePipe();
      return;
   }
   msgLen = (uint)lenBuf[0]
          | ((uint)lenBuf[1] << 8)
          | ((uint)lenBuf[2] << 16)
          | ((uint)lenBuf[3] << 24);

   if(msgLen == 0 || msgLen > (uint)InpBufSize)
   {
      EA_Log(StringFormat("Invalid message length %d — closing", msgLen));
      ClosePipe();
      return;
   }

   //── 4. Read payload ───────────────────────────────────────────────
   uchar payload[];
   ArrayResize(payload, (int)msgLen);
   uint readBytes = FileReadArray(g_pipe, payload, 0, (int)msgLen);
   if(readBytes != msgLen)
   {
      EA_Log("Short read — closing pipe");
      ClosePipe();
      return;
   }

   string requestJson = CharArrayToString(payload, 0, (int)msgLen, CP_UTF8);
   if(InpEnableLog) EA_Log("← " + StringSubstr(requestJson, 0, 200));

   //── 5. Process command ────────────────────────────────────────────
   string responseJson = ProcessRequest(requestJson);
   if(InpEnableLog) EA_Log("→ " + StringSubstr(responseJson, 0, 200));
   g_totalServed++;

   //── 6. Write 4-byte length prefix + response ─────────────────────
   uchar respBytes[];
   StringToCharArray(responseJson, respBytes, 0, StringLen(responseJson), CP_UTF8);
   int respLen = ArraySize(respBytes);
   // Strip the null terminator StringToCharArray appends
   if(respLen > 0 && respBytes[respLen-1] == 0) respLen--;

   uchar respLenBuf[4];
   respLenBuf[0] = (uchar)(respLen & 0xFF);
   respLenBuf[1] = (uchar)((respLen >> 8) & 0xFF);
   respLenBuf[2] = (uchar)((respLen >> 16) & 0xFF);
   respLenBuf[3] = (uchar)((respLen >> 24) & 0xFF);

   FileWriteArray(g_pipe, respLenBuf, 0, 4);
   FileWriteArray(g_pipe, respBytes, 0, respLen);
   FileFlush(g_pipe);

   //── 7. Close after each request (stateless, per C# design) ───────
   ClosePipe();
}

//+------------------------------------------------------------------+
//| Dispatch command to handler                                       |
//+------------------------------------------------------------------+
string ProcessRequest(string json)
{
   string cmd   = JsonStr(json, "cmd");
   string reqId = JsonStr(json, "req_id");

   if(cmd == "PING")           return Ok(reqId, "{\"pong\":true,\"mt5_time\":\"" + TimeToString(TimeCurrent()) + "\"}");
   if(cmd == "GET_EA_HEALTH")  return CmdGetEaHealth(reqId);
   if(cmd == "GET_ACCOUNT")    return CmdGetAccount(reqId);
   if(cmd == "GET_POSITIONS")  return CmdGetPositions(reqId);
   if(cmd == "GET_TRADE_HISTORY")return CmdGetTradeHistory(reqId, json);
   if(cmd == "OPEN_TRADE")     return CmdOpenTrade(reqId, json);
   if(cmd == "CHECK_ORDER")    return CmdCheckOrder(reqId, json);
   if(cmd == "CLOSE_TRADE")    return CmdCloseTrade(reqId, json);
   if(cmd == "MODIFY_POSITION")return CmdModifyPosition(reqId, json);
   if(cmd == "GET_SYMBOL_INFO")return CmdGetSymbolInfo(reqId, json);
   if(cmd == "GET_MARKET_SNAPSHOT")return CmdGetMarketSnapshot(reqId, json);
   if(cmd == "GET_TICKS")      return CmdGetTicks(reqId, json);
   if(cmd == "GET_RATES")      return CmdGetRates(reqId, json);
   if(cmd == "CLOSE_ALL")      return CmdCloseAll(reqId);

   return Err(reqId, "UNKNOWN_CMD", "Unknown command: " + cmd);
}

long JsonLong(string json, string key)
{
   string v = JsonStr(json, key);
   if(StringLen(v) == 0) return 0;
   return (long)StringToInteger(v);
}

datetime UnixMsToTime(long unixMs)
{
   return (datetime)(unixMs / 1000);
}

string TimeToIsoUtc(datetime t)
{
   MqlDateTime dt;
   TimeToStruct(t, dt);
   return StringFormat("%04d-%02d-%02dT%02d:%02d:%02dZ",
                       dt.year, dt.mon, dt.day, dt.hour, dt.min, dt.sec);
}

string CmdGetTicks(string reqId, string json)
{
   string data = JsonStr(json, "data");
   if(StringLen(data) == 0 || JsonLong(data, "from_unix_ms") <= 0 || JsonLong(data, "to_unix_ms") <= 0)
      data = json;

   string sym = JsonStr(data, "symbol");
   StringReplace(sym, "/", "");

   string brokerSymbol = "";
   if(!ResolveBrokerSymbol(sym, brokerSymbol))
      return Err(reqId, "INVALID_SYMBOL", "Symbol not found: " + sym);

   sym = brokerSymbol;

   long fromMs = JsonLong(data, "from_unix_ms");
   long toMs   = JsonLong(data, "to_unix_ms");
   int maxRows = (int)JsonDbl(data, "max_rows");

   if(maxRows <= 0) maxRows = 5000;
   if(maxRows > 50000) maxRows = 50000;

   datetime fromTime = UnixMsToTime(fromMs);
   datetime toTime   = UnixMsToTime(toMs);

   if(fromTime <= 0 || toTime <= 0 || fromTime >= toTime)
      return Err(reqId, "INVALID_RANGE", "Invalid tick date range");

   MqlTick ticks[];
   ulong fromUs = (ulong)fromMs * 1000;
   ulong toUs   = (ulong)toMs * 1000;

   int copied = CopyTicksRange(sym, ticks, COPY_TICKS_ALL, fromUs, toUs);

   if(copied <= 0)
      return Ok(reqId, "[]");

   string arr = "[";
   int added = 0;

   for(int i = 0; i < copied && added < maxRows; i++)
   {
      if(added > 0) arr += ",";

      arr += "{";
      arr += "\"TimestampUtc\":\"" + TimeToIsoUtc((datetime)ticks[i].time) + "\",";
      arr += "\"Symbol\":\"" + Esc(sym) + "\",";
      arr += "\"Bid\":" + DoubleToString(ticks[i].bid, (int)SymbolInfoInteger(sym, SYMBOL_DIGITS)) + ",";
      arr += "\"Ask\":" + DoubleToString(ticks[i].ask, (int)SymbolInfoInteger(sym, SYMBOL_DIGITS)) + ",";
      arr += "\"Volume\":" + DoubleToString((double)ticks[i].volume, 2);
      arr += "}";

      added++;
   }

   arr += "]";
   return Ok(reqId, arr);
}

string CmdGetRates(string reqId, string json)
{
   string data = JsonStr(json, "data");
   if(StringLen(data) == 0 || JsonLong(data, "from_unix_ms") <= 0 || JsonLong(data, "to_unix_ms") <= 0)
      data = json;

   string sym = JsonStr(data, "symbol");
   StringReplace(sym, "/", "");

   string brokerSymbol = "";
   if(!ResolveBrokerSymbol(sym, brokerSymbol))
      return Err(reqId, "INVALID_SYMBOL", "Symbol not found: " + sym);

   sym = brokerSymbol;

   string tfText = JsonStr(data, "timeframe");
   string tfOut = tfText;
   if(StringLen(tfOut) == 0)
      tfOut = "M1";
   ENUM_TIMEFRAMES tf = PERIOD_M1;

   if(tfText == "M5") tf = PERIOD_M5;
   else if(tfText == "M15") tf = PERIOD_M15;
   else if(tfText == "M30") tf = PERIOD_M30;
   else if(tfText == "H1") tf = PERIOD_H1;
   else tf = PERIOD_M1;

   long fromMs = JsonLong(data, "from_unix_ms");
   long toMs   = JsonLong(data, "to_unix_ms");
   int maxRows = (int)JsonDbl(data, "max_rows");

   if(maxRows <= 0) maxRows = 5000;
   if(maxRows > 50000) maxRows = 50000;

   datetime fromTime = UnixMsToTime(fromMs);
   datetime toTime   = UnixMsToTime(toMs);

   if(fromTime <= 0 || toTime <= 0 || fromTime >= toTime)
      return Err(reqId, "INVALID_RANGE", "Invalid OHLC date range");

   MqlRates rates[];
   ArraySetAsSeries(rates, false);

   int copied = CopyRates(sym, tf, fromTime, toTime, rates);

   if(copied <= 0)
      return Ok(reqId, "[]");

   int digits = (int)SymbolInfoInteger(sym, SYMBOL_DIGITS);
   double pip = SnapshotPipSize(sym);

   string arr = "[";
   int added = 0;

   for(int i = 0; i < copied && added < maxRows; i++)
   {
      if(added > 0) arr += ",";

      double spreadPips = pip > 0 ? rates[i].spread * SymbolInfoDouble(sym, SYMBOL_POINT) / pip : 0.0;

      arr += "{";
      arr += "\"TimestampUtc\":\"" + TimeToIsoUtc(rates[i].time) + "\",";
      arr += "\"Symbol\":\"" + Esc(sym) + "\",";
      arr += "\"Open\":" + DoubleToString(rates[i].open, digits) + ",";
      arr += "\"High\":" + DoubleToString(rates[i].high, digits) + ",";
      arr += "\"Low\":" + DoubleToString(rates[i].low, digits) + ",";
      arr += "\"Close\":" + DoubleToString(rates[i].close, digits) + ",";
      arr += "\"Timeframe\":\"" + Esc(tfOut) + "\",";
      arr += "\"SpreadPips\":" + DoubleToString(spreadPips, 2);
      arr += "}";

      added++;
   }

   arr += "]";
   return Ok(reqId, arr);
}

//+------------------------------------------------------------------+
//| GET_EA_HEALTH                                                     |
//+------------------------------------------------------------------+
string CmdGetEaHealth(string reqId)
{
   string d = "{";
   d += "\"IsAlive\":true,";
   d += "\"Version\":\"3.00\",";
   d += "\"BuildIdentifier\":\"TradingBotEA-3.00\",";
   d += "\"TerminalName\":\"" + Esc(TerminalInfoString(TERMINAL_NAME)) + "\",";
   d += "\"Server\":\"" + Esc(AccountInfoString(ACCOUNT_SERVER)) + "\",";
   d += "\"CheckedAtUtc\":\"" + TimeToString(TimeGMT(), TIME_DATE|TIME_SECONDS) + "\"";
   d += "}";
   return Ok(reqId, d);
}

//+------------------------------------------------------------------+
//| GET_ACCOUNT                                                       |
//+------------------------------------------------------------------+
string CmdGetAccount(string reqId)
{
   string d = "{";
   d += "\"AccountNumber\":"  + IntegerToString(AccountInfoInteger(ACCOUNT_LOGIN)) + ",";
   d += "\"Name\":\""         + Esc(AccountInfoString(ACCOUNT_NAME)) + "\",";
   d += "\"Server\":\""       + Esc(AccountInfoString(ACCOUNT_SERVER)) + "\",";
   d += "\"Currency\":\""     + Esc(AccountInfoString(ACCOUNT_CURRENCY)) + "\",";
   d += "\"Balance\":"        + DoubleToString(AccountInfoDouble(ACCOUNT_BALANCE),    2) + ",";
   d += "\"Equity\":"         + DoubleToString(AccountInfoDouble(ACCOUNT_EQUITY),     2) + ",";
   d += "\"Margin\":"         + DoubleToString(AccountInfoDouble(ACCOUNT_MARGIN),     2) + ",";
   d += "\"FreeMargin\":"     + DoubleToString(AccountInfoDouble(ACCOUNT_MARGIN_FREE), 2) + ",";
   d += "\"MarginLevel\":"    + DoubleToString(AccountInfoDouble(ACCOUNT_MARGIN_LEVEL),2) + ",";
   d += "\"Profit\":"         + DoubleToString(AccountInfoDouble(ACCOUNT_PROFIT),     2) + ",";
   d += "\"Leverage\":"       + IntegerToString(AccountInfoInteger(ACCOUNT_LEVERAGE)) + ",";
   d += "\"IsConnected\":true";
   d += "}";
   return Ok(reqId, d);
}

//+------------------------------------------------------------------+
//| Broker symbol resolver                                            |
//+------------------------------------------------------------------+
bool ResolveBrokerSymbol(string requested, string &resolved)
{
   resolved = "";
   StringTrimLeft(requested);
   StringTrimRight(requested);
   StringReplace(requested, "/", "");

   if(StringLen(requested) == 0)
      return false;

   if(SymbolSelect(requested, true))
   {
      resolved = requested;
      return true;
   }

   int total = SymbolsTotal(false);
   for(int i = 0; i < total; i++)
   {
      string candidate = SymbolName(i, false);
      if(StringFind(candidate, requested) == 0)
      {
         if(SymbolSelect(candidate, true))
         {
            resolved = candidate;
            return true;
         }
      }
   }

   total = SymbolsTotal(true);
   for(int i = 0; i < total; i++)
   {
      string candidate = SymbolName(i, true);
      if(StringFind(candidate, requested) == 0)
      {
         resolved = candidate;
         return true;
      }
   }

   return false;
}

//+------------------------------------------------------------------+
//| GET_POSITIONS                                                     |
//+------------------------------------------------------------------+
string CmdGetPositions(string reqId)
{
   string arr = "[";
   int total = PositionsTotal();
   bool first = true;
   for(int i = 0; i < total; i++)
   {
      if(!PosInfo.SelectByIndex(i)) continue;
      if(!first) arr += ",";
      first = false;
      string typeStr = PosInfo.PositionType() == POSITION_TYPE_BUY ? "BUY" : "SELL";
      arr += "{";
      arr += "\"Ticket\":"       + IntegerToString(PosInfo.Ticket())           + ",";
      arr += "\"Symbol\":\""     + Esc(PosInfo.Symbol())                       + "\",";
      arr += "\"Type\":\""       + typeStr                                     + "\",";
      arr += "\"Lots\":"         + DoubleToString(PosInfo.Volume(),           2) + ",";
      arr += "\"OpenPrice\":"    + DoubleToString(PosInfo.PriceOpen(),        5) + ",";
      arr += "\"CurrentPrice\":" + DoubleToString(PosInfo.PriceCurrent(),     5) + ",";
      arr += "\"StopLoss\":"     + DoubleToString(PosInfo.StopLoss(),         5) + ",";
      arr += "\"TakeProfit\":"   + DoubleToString(PosInfo.TakeProfit(),       5) + ",";
      arr += "\"Profit\":"       + DoubleToString(PosInfo.Profit(),           2) + ",";
      arr += "\"MagicNumber\":"  + IntegerToString(PosInfo.Magic())              + ",";
      arr += "\"Comment\":\""    + Esc(PosInfo.Comment())                      + "\",";
      arr += "\"OpenTime\":\""   + TimeToIsoUtc(PosInfo.Time()) + "\",";
      arr += "\"SlMovedToBreakeven\":false";
      arr += "}";
   }
   arr += "]";
   return Ok(reqId, arr);
}

string DealEntryName(long entry)
{
   if(entry == DEAL_ENTRY_IN) return "IN";
   if(entry == DEAL_ENTRY_OUT) return "OUT";
   if(entry == DEAL_ENTRY_INOUT) return "INOUT";
   if(entry == DEAL_ENTRY_OUT_BY) return "OUT_BY";
   return "UNKNOWN";
}

string DealDirectionName(ulong dealTicket)
{
   long type = HistoryDealGetInteger(dealTicket, DEAL_TYPE);
   long entry = HistoryDealGetInteger(dealTicket, DEAL_ENTRY);

   if(entry == DEAL_ENTRY_OUT || entry == DEAL_ENTRY_OUT_BY)
      return type == DEAL_TYPE_BUY ? "SELL" : "BUY";

   if(type == DEAL_TYPE_BUY) return "BUY";
   if(type == DEAL_TYPE_SELL) return "SELL";
   return "UNKNOWN";
}

string DealReasonName(long reason)
{
   if(reason == DEAL_REASON_CLIENT) return "CLIENT";
   if(reason == DEAL_REASON_MOBILE) return "MOBILE";
   if(reason == DEAL_REASON_WEB) return "WEB";
   if(reason == DEAL_REASON_EXPERT) return "EXPERT";
   if(reason == DEAL_REASON_SL) return "STOP_LOSS";
   if(reason == DEAL_REASON_TP) return "TAKE_PROFIT";
   if(reason == DEAL_REASON_SO) return "STOP_OUT";
   return "UNKNOWN";
}

ulong FindEntryDealForPosition(ulong exitTicket)
{
   long positionId = HistoryDealGetInteger(exitTicket, DEAL_POSITION_ID);
   datetime exitTime = (datetime)HistoryDealGetInteger(exitTicket, DEAL_TIME);
   ulong best = 0;
   datetime bestTime = 0;

   for(int i = 0; i < HistoryDealsTotal(); i++)
   {
      ulong ticket = HistoryDealGetTicket(i);
      if(ticket == 0 || ticket == exitTicket) continue;
      if(HistoryDealGetInteger(ticket, DEAL_POSITION_ID) != positionId) continue;

      long entry = HistoryDealGetInteger(ticket, DEAL_ENTRY);
      if(entry != DEAL_ENTRY_IN && entry != DEAL_ENTRY_INOUT) continue;

      datetime entryTime = (datetime)HistoryDealGetInteger(ticket, DEAL_TIME);
      if(entryTime <= exitTime && entryTime >= bestTime)
      {
         best = ticket;
         bestTime = entryTime;
      }
   }

   return best;
}

void GetTradeExtremes(string symbol, datetime fromTime, datetime toTime, double entryPrice, double exitPrice, double &highest, double &lowest)
{
   highest = MathMax(entryPrice, exitPrice);
   lowest = MathMin(entryPrice, exitPrice);
   if(fromTime <= 0 || toTime <= fromTime) return;

   MqlRates rates[];
   ArraySetAsSeries(rates, false);
   int copied = CopyRates(symbol, PERIOD_M1, fromTime, toTime, rates);
   for(int i = 0; i < copied; i++)
   {
      if(rates[i].high > highest) highest = rates[i].high;
      if(rates[i].low < lowest) lowest = rates[i].low;
   }
}

string CmdGetTradeHistory(string reqId, string json)
{
   string data = JsonStr(json, "data");
   if(StringLen(data) == 0 || JsonLong(data, "from_unix_ms") <= 0 || JsonLong(data, "to_unix_ms") <= 0)
      data = json;

   long fromMs = JsonLong(data, "from_unix_ms");
   long toMs   = JsonLong(data, "to_unix_ms");
   int maxRows = (int)JsonDbl(data, "max_rows");

   if(maxRows <= 0) maxRows = 200;
   if(maxRows > 1000) maxRows = 1000;

   datetime fromTime = UnixMsToTime(fromMs);
   datetime toTime   = UnixMsToTime(toMs);

   if(fromTime <= 0 || toTime <= 0 || fromTime >= toTime)
      return Err(reqId, "INVALID_RANGE", "Invalid trade history date range");

   if(!HistorySelect(fromTime, toTime))
      return Ok(reqId, "[]");

   string arr = "[";
   int added = 0;

   for(int i = HistoryDealsTotal() - 1; i >= 0 && added < maxRows; i--)
   {
      ulong dealTicket = HistoryDealGetTicket(i);
      if(dealTicket == 0) continue;

      long dealType = HistoryDealGetInteger(dealTicket, DEAL_TYPE);
      if(dealType != DEAL_TYPE_BUY && dealType != DEAL_TYPE_SELL) continue;

      ulong orderTicket = (ulong)HistoryDealGetInteger(dealTicket, DEAL_ORDER);
      double sl = 0.0;
      double tp = 0.0;
      if(orderTicket > 0 && HistoryOrderSelect(orderTicket))
      {
         sl = HistoryOrderGetDouble(orderTicket, ORDER_SL);
         tp = HistoryOrderGetDouble(orderTicket, ORDER_TP);
      }

      double profit = HistoryDealGetDouble(dealTicket, DEAL_PROFIT)
                    + HistoryDealGetDouble(dealTicket, DEAL_SWAP)
                    + HistoryDealGetDouble(dealTicket, DEAL_COMMISSION);
      string symbol = HistoryDealGetString(dealTicket, DEAL_SYMBOL);
      int digits = (int)SymbolInfoInteger(symbol, SYMBOL_DIGITS);
      if(digits <= 0) digits = 5;
      double pip = SnapshotPipSize(symbol);
      if(pip <= 0.0) pip = SymbolInfoDouble(symbol, SYMBOL_POINT);

      long entryType = HistoryDealGetInteger(dealTicket, DEAL_ENTRY);
      ulong entryTicket = (entryType == DEAL_ENTRY_OUT || entryType == DEAL_ENTRY_OUT_BY)
         ? FindEntryDealForPosition(dealTicket)
         : dealTicket;

      datetime entryTime = entryTicket > 0
         ? (datetime)HistoryDealGetInteger(entryTicket, DEAL_TIME)
         : (datetime)HistoryDealGetInteger(dealTicket, DEAL_TIME);
      datetime exitTime = (entryType == DEAL_ENTRY_OUT || entryType == DEAL_ENTRY_INOUT || entryType == DEAL_ENTRY_OUT_BY)
         ? (datetime)HistoryDealGetInteger(dealTicket, DEAL_TIME)
         : 0;
      double entryPrice = entryTicket > 0 ? HistoryDealGetDouble(entryTicket, DEAL_PRICE) : HistoryDealGetDouble(dealTicket, DEAL_PRICE);
      double exitPrice = exitTime > 0 ? HistoryDealGetDouble(dealTicket, DEAL_PRICE) : 0.0;
      string direction = DealDirectionName(entryTicket > 0 ? entryTicket : dealTicket);
      double highest = entryPrice;
      double lowest = entryPrice;
      double closePips = 0.0;
      double maxProfitPips = 0.0;
      double maxLossPips = 0.0;
      int durationMinutes = 0;

      if(exitTime > 0)
      {
         GetTradeExtremes(symbol, entryTime, exitTime, entryPrice, exitPrice, highest, lowest);
         durationMinutes = (int)MathMax(0, (exitTime - entryTime) / 60);
         if(direction == "BUY")
         {
            closePips = (exitPrice - entryPrice) / pip;
            maxProfitPips = MathMax(0.0, (highest - entryPrice) / pip);
            maxLossPips = MathMax(0.0, (entryPrice - lowest) / pip);
         }
         else if(direction == "SELL")
         {
            closePips = (entryPrice - exitPrice) / pip;
            maxProfitPips = MathMax(0.0, (entryPrice - lowest) / pip);
            maxLossPips = MathMax(0.0, (highest - entryPrice) / pip);
         }
      }

      if(added > 0) arr += ",";
      arr += "{";
      arr += "\"DealTicket\":" + IntegerToString((long)dealTicket) + ",";
      arr += "\"OrderTicket\":" + IntegerToString((long)orderTicket) + ",";
      arr += "\"PositionId\":" + IntegerToString(HistoryDealGetInteger(dealTicket, DEAL_POSITION_ID)) + ",";
      arr += "\"TimeUtc\":\"" + TimeToIsoUtc((datetime)HistoryDealGetInteger(dealTicket, DEAL_TIME)) + "\",";
      arr += "\"EntryTimeUtc\":" + (entryTime > 0 ? "\"" + TimeToIsoUtc(entryTime) + "\"" : "null") + ",";
      arr += "\"ExitTimeUtc\":" + (exitTime > 0 ? "\"" + TimeToIsoUtc(exitTime) + "\"" : "null") + ",";
      arr += "\"Symbol\":\"" + Esc(symbol) + "\",";
      arr += "\"Direction\":\"" + direction + "\",";
      arr += "\"EntryType\":\"" + DealEntryName(HistoryDealGetInteger(dealTicket, DEAL_ENTRY)) + "\",";
      arr += "\"Lots\":" + DoubleToString(HistoryDealGetDouble(dealTicket, DEAL_VOLUME), 2) + ",";
      arr += "\"Price\":" + DoubleToString(HistoryDealGetDouble(dealTicket, DEAL_PRICE), digits) + ",";
      arr += "\"EntryPrice\":" + DoubleToString(entryPrice, digits) + ",";
      arr += "\"ExitPrice\":" + DoubleToString(exitPrice, digits) + ",";
      arr += "\"Profit\":" + DoubleToString(profit, 2) + ",";
      arr += "\"ClosePips\":" + DoubleToString(closePips, 1) + ",";
      arr += "\"MaxProfitPips\":" + DoubleToString(maxProfitPips, 1) + ",";
      arr += "\"MaxLossPips\":" + DoubleToString(maxLossPips, 1) + ",";
      arr += "\"HighestPrice\":" + DoubleToString(highest, digits) + ",";
      arr += "\"LowestPrice\":" + DoubleToString(lowest, digits) + ",";
      arr += "\"DurationMinutes\":" + IntegerToString(durationMinutes) + ",";
      arr += "\"StopLoss\":" + DoubleToString(sl, digits) + ",";
      arr += "\"TakeProfit\":" + DoubleToString(tp, digits) + ",";
      arr += "\"MagicNumber\":" + IntegerToString((int)HistoryDealGetInteger(dealTicket, DEAL_MAGIC)) + ",";
      arr += "\"Comment\":\"" + Esc(HistoryDealGetString(dealTicket, DEAL_COMMENT)) + "\",";
      arr += "\"CloseReason\":\"" + DealReasonName(HistoryDealGetInteger(dealTicket, DEAL_REASON)) + "\"";
      arr += "}";
      added++;
   }

   arr += "]";
   return Ok(reqId, arr);
}

//+------------------------------------------------------------------+
//| OPEN_TRADE                                                        |
//+------------------------------------------------------------------+
string CmdOpenTrade(string reqId, string json)
{
   // The C# app serializes TradeRequest as the "data" field
   string data = JsonStr(json, "data");
   if(StringLen(data) == 0) data = json;

   string symbol    = JsonStr(data, "Pair");
   string typeStr   = JsonStr(data, "TradeType");
   string orderStr  = JsonStr(data, "OrderType");
   double entry     = JsonDbl(data, "EntryPrice");
   double sl        = JsonDbl(data, "StopLoss");
   double tp        = JsonDbl(data, "TakeProfit");
   double lots      = JsonDbl(data, "LotSize");
   int    magic     = (int)JsonDbl(data, "MagicNumber");
   string comment   = JsonStr(data, "Comment");
   int    expiryMin = (int)JsonDbl(data, "ExpiryMinutes");

   // Defaults
   if(lots    < 0.01) lots   = 0.01;
   if(magic   <= 0)   magic  = InpMagic;
   if(expiryMin <= 0) expiryMin = 60;
   if(StringLen(comment) == 0) comment = "MT5Bot";

   bool isBuy    = (typeStr == "BUY");
   bool isMarket = (orderStr == "MARKET" || StringLen(orderStr) == 0);
   bool isLimit  = (orderStr == "LIMIT");
   bool isStop   = (orderStr == "STOP");

   string requestedSymbol = symbol;

   // Normalize symbol (remove / if present)
   StringReplace(symbol, "/", "");
   if(StringLen(symbol) == 0)
      return Err(reqId, "INVALID_PAIR", "Pair is empty");

   // Resolve broker suffixes/prefixes such as GBPUSDm, GBPUSDc, GBPUSD.pro.
   string brokerSymbol = "";
   if(!ResolveBrokerSymbol(symbol, brokerSymbol))
      return Err(reqId, "INVALID_PAIR", "Symbol not found: " + symbol + ". Check broker suffix in MT5 Market Watch.");

   if(brokerSymbol != symbol)
      EA_Log("Resolved symbol " + requestedSymbol + " -> " + brokerSymbol);
   symbol = brokerSymbol;

   // Validate SL/TP direction
   double ask = SymbolInfoDouble(symbol, SYMBOL_ASK);
   double bid = SymbolInfoDouble(symbol, SYMBOL_BID);
   double refPrice = isMarket ? (isBuy ? ask : bid) : entry;

   if(sl == 0) return Err(reqId, "INVALID_SL", "StopLoss cannot be 0");
   if(tp == 0) return Err(reqId, "INVALID_TP", "TakeProfit cannot be 0");

   Trade.SetExpertMagicNumber(magic);

   bool   success = false;
   ulong  ticket  = 0;
   double execPrice = 0;

   if(isMarket)
   {
      if(isBuy)  success = Trade.Buy( lots, symbol, 0,  sl, tp, comment);
      else       success = Trade.Sell(lots, symbol, 0,  sl, tp, comment);
      ticket    = Trade.ResultOrder();
      execPrice = Trade.ResultPrice();
   }
   else
   {
      datetime expiry = TimeCurrent() + (datetime)(expiryMin * 60);
      if(isBuy  && isLimit) success = Trade.BuyLimit( lots, entry, symbol, sl, tp, ORDER_TIME_SPECIFIED, expiry, comment);
      if(!isBuy && isLimit) success = Trade.SellLimit(lots, entry, symbol, sl, tp, ORDER_TIME_SPECIFIED, expiry, comment);
      if(isBuy  && isStop)  success = Trade.BuyStop(  lots, entry, symbol, sl, tp, ORDER_TIME_SPECIFIED, expiry, comment);
      if(!isBuy && isStop)  success = Trade.SellStop( lots, entry, symbol, sl, tp, ORDER_TIME_SPECIFIED, expiry, comment);
      ticket    = Trade.ResultOrder();
      execPrice = entry;
   }

   if(success)
   {
      ulong positionTicket = ticket;
      for(int i = PositionsTotal() - 1; i >= 0; i--)
      {
         if(!PosInfo.SelectByIndex(i)) continue;
         if(PosInfo.Symbol() != symbol) continue;
         if(PosInfo.Magic() != magic) continue;
         if(isBuy && PosInfo.PositionType() != POSITION_TYPE_BUY) continue;
         if(!isBuy && PosInfo.PositionType() != POSITION_TYPE_SELL) continue;
         if(MathAbs(PosInfo.Volume() - lots) > 0.0000001) continue;

         positionTicket = PosInfo.Ticket();
         execPrice = PosInfo.PriceOpen();
         break;
      }

      EA_Log(StringFormat("Opened %s position #%d order #%d %s @ %.5f lots %.2f", typeStr, positionTicket, ticket, symbol, execPrice, lots));
      string d = "{";
      d += "\"Ticket\":"        + IntegerToString((long)positionTicket) + ",";
      d += "\"Status\":\"Filled\",";
      d += "\"ExecutedPrice\":" + DoubleToString(execPrice, 5) + ",";
      d += "\"ExecutedLots\":"  + DoubleToString(lots,      2) + ",";
      d += "\"OrderTicket\":"   + IntegerToString((long)ticket) + ",";
      d += "\"DealTicket\":"    + IntegerToString((long)Trade.ResultDeal());
      d += "}";
      return Ok(reqId, d);
   }
   else
   {
      uint   retcode = Trade.ResultRetcode();
      string retDesc = Trade.ResultRetcodeDescription();
      EA_Log(StringFormat("Open failed [%d] %s", retcode, retDesc));
      return Err(reqId, "MT5_" + IntegerToString(retcode), retDesc);
   }
}

//+------------------------------------------------------------------+
//| CHECK_ORDER                                                       |
//+------------------------------------------------------------------+
string CmdCheckOrder(string reqId, string json)
{
   string data = JsonStr(json, "data");
   if(StringLen(data) == 0) data = json;

   string symbol   = JsonStr(data, "symbol");
   if(StringLen(symbol) == 0) symbol = JsonStr(data, "Pair");
   string typeStr  = JsonStr(data, "trade_type");
   if(StringLen(typeStr) == 0) typeStr = JsonStr(data, "TradeType");
   string orderStr = JsonStr(data, "order_type");
   if(StringLen(orderStr) == 0) orderStr = JsonStr(data, "OrderType");
   double lots     = JsonDbl(data, "lots");
   if(lots <= 0) lots = JsonDbl(data, "LotSize");
   double price    = JsonDbl(data, "price");
   if(price <= 0) price = JsonDbl(data, "EntryPrice");
   double sl       = JsonDbl(data, "stop_loss");
   if(sl <= 0) sl = JsonDbl(data, "StopLoss");
   double tp       = JsonDbl(data, "take_profit");
   if(tp <= 0) tp = JsonDbl(data, "TakeProfit");
   int magic       = (int)JsonDbl(data, "magic_number");
   if(magic <= 0) magic = (int)JsonDbl(data, "MagicNumber");
   if(magic <= 0) magic = InpMagic;

   StringReplace(symbol, "/", "");
   if(StringLen(symbol) == 0)
      return Err(reqId, "INVALID_PAIR", "Pair is empty");

   string brokerSymbol = "";
   if(!ResolveBrokerSymbol(symbol, brokerSymbol))
      return Err(reqId, "INVALID_PAIR", "Symbol not found: " + symbol + ". Check broker suffix in MT5 Market Watch.");
   symbol = brokerSymbol;

   if(lots <= 0) lots = SymbolInfoDouble(symbol, SYMBOL_VOLUME_MIN);
   bool isBuy = (typeStr == "BUY");
   bool isMarket = (orderStr == "MARKET" || StringLen(orderStr) == 0);
   if(price <= 0)
      price = isBuy ? SymbolInfoDouble(symbol, SYMBOL_ASK) : SymbolInfoDouble(symbol, SYMBOL_BID);

   MqlTradeRequest request;
   MqlTradeCheckResult check;
   ZeroMemory(request);
   ZeroMemory(check);

   request.symbol = symbol;
   request.volume = lots;
   request.price = price;
   request.sl = sl;
   request.tp = tp;
   request.magic = magic;
   request.deviation = InpSlippage;
   request.type_time = ORDER_TIME_GTC;
   request.type_filling = InpFillIOC ? ORDER_FILLING_IOC : ORDER_FILLING_FOK;

   if(isMarket)
   {
      request.action = TRADE_ACTION_DEAL;
      request.type = isBuy ? ORDER_TYPE_BUY : ORDER_TYPE_SELL;
   }
   else if(orderStr == "LIMIT")
   {
      request.action = TRADE_ACTION_PENDING;
      request.type = isBuy ? ORDER_TYPE_BUY_LIMIT : ORDER_TYPE_SELL_LIMIT;
   }
   else if(orderStr == "STOP")
   {
      request.action = TRADE_ACTION_PENDING;
      request.type = isBuy ? ORDER_TYPE_BUY_STOP : ORDER_TYPE_SELL_STOP;
   }
   else
   {
      return Err(reqId, "INVALID_ORDER_TYPE", "Unsupported order type for OrderCheck: " + orderStr);
   }

   bool ok = OrderCheck(request, check);
   string comment = check.comment;
   if(StringLen(comment) == 0)
      comment = ok ? "OrderCheck accepted" : "OrderCheck rejected";

   bool accepted = ok && (check.retcode == 0 || check.retcode == TRADE_RETCODE_DONE || check.retcode == TRADE_RETCODE_PLACED);

   string d = "{";
   d += "\"IsAccepted\":" + BoolJson(accepted) + ",";
   d += "\"Retcode\":" + IntegerToString((int)check.retcode) + ",";
   d += "\"Comment\":\"" + Esc(comment) + "\",";
   d += "\"Margin\":" + DoubleToString(check.margin, 2) + ",";
   d += "\"MarginFree\":" + DoubleToString(check.margin_free, 2) + ",";
   d += "\"MarginLevel\":" + DoubleToString(check.margin_level, 2) + ",";
   d += "\"Volume\":" + DoubleToString(lots, 2) + ",";
   d += "\"Price\":" + DoubleToString(price, (int)SymbolInfoInteger(symbol, SYMBOL_DIGITS)) + ",";
   d += "\"StopLoss\":" + DoubleToString(sl, (int)SymbolInfoInteger(symbol, SYMBOL_DIGITS)) + ",";
   d += "\"TakeProfit\":" + DoubleToString(tp, (int)SymbolInfoInteger(symbol, SYMBOL_DIGITS)) + ",";
   d += "\"Symbol\":\"" + Esc(symbol) + "\",";
   d += "\"TradeType\":\"" + (isBuy ? "BUY" : "SELL") + "\"";
   d += "}";

   return Ok(reqId, d);
}

//+------------------------------------------------------------------+
//| CLOSE_TRADE                                                       |
//+------------------------------------------------------------------+
string CmdCloseTrade(string reqId, string json)
{
   string data = JsonStr(json, "data");
   ulong ticket = (ulong)JsonDbl(data, "ticket");

   // Try position close first, then order delete (for pending)
   bool success = Trade.PositionClose(ticket);
   if(!success)
   {
      // Maybe it's a pending order
      success = Trade.OrderDelete(ticket);
   }

   if(success)
   {
      EA_Log("Closed #" + IntegerToString((int)ticket));
      return Ok(reqId, "{\"closed\":true,\"ticket\":" + IntegerToString((int)ticket) + "}");
   }
   return Err(reqId, "CLOSE_FAILED",
      "Cannot close #" + IntegerToString((int)ticket) + ": " + Trade.ResultRetcodeDescription());
}

//+------------------------------------------------------------------+
//| MODIFY_POSITION                                                   |
//+------------------------------------------------------------------+
string CmdModifyPosition(string reqId, string json)
{
   string data = JsonStr(json, "data");
   ulong  ticket = (ulong)JsonDbl(data, "ticket");
   double newSL  = JsonDbl(data, "stop_loss");
   double newTP  = JsonDbl(data, "take_profit");

   bool success = Trade.PositionModify(ticket, newSL, newTP);
   if(success)
   {
      EA_Log(StringFormat("Modified #%d SL=%.5f TP=%.5f", ticket, newSL, newTP));
      return Ok(reqId, "{\"modified\":true}");
   }
   return Err(reqId, "MODIFY_FAILED", Trade.ResultRetcodeDescription());
}

//+------------------------------------------------------------------+
//| CLOSE_ALL positions managed by this EA                           |
//+------------------------------------------------------------------+
string CmdCloseAll(string reqId)
{
   int closed = 0, failed = 0;
   for(int i = PositionsTotal() - 1; i >= 0; i--)
   {
      if(!PosInfo.SelectByIndex(i)) continue;
      if(PosInfo.Magic() != InpMagic) continue;
      if(Trade.PositionClose(PosInfo.Ticket())) closed++;
      else failed++;
   }
   EA_Log(StringFormat("CloseAll: closed=%d failed=%d", closed, failed));
   return Ok(reqId, StringFormat("{\"closed\":%d,\"failed\":%d}", closed, failed));
}

//+------------------------------------------------------------------+
//| GET_SYMBOL_INFO                                                   |
//+------------------------------------------------------------------+
string CmdGetSymbolInfo(string reqId, string json)
{
   string data = JsonStr(json, "data");
   string sym  = JsonStr(data, "symbol");
   StringReplace(sym, "/", "");

   string brokerSymbol = "";
   if(!ResolveBrokerSymbol(sym, brokerSymbol))
      return Err(reqId, "INVALID_SYMBOL", "Symbol not found: " + sym + ". Check broker suffix in MT5 Market Watch.");
   sym = brokerSymbol;

   double ask   = SymbolInfoDouble(sym, SYMBOL_ASK);
   double bid   = SymbolInfoDouble(sym, SYMBOL_BID);
   double spread= (ask - bid) / SymbolInfoDouble(sym, SYMBOL_POINT);
   double minLot= SymbolInfoDouble(sym, SYMBOL_VOLUME_MIN);
   double maxLot= SymbolInfoDouble(sym, SYMBOL_VOLUME_MAX);
   double lotStep = SymbolInfoDouble(sym, SYMBOL_VOLUME_STEP);
   double volumeLimit = SymbolInfoDouble(sym, SYMBOL_VOLUME_LIMIT);
   double point = SymbolInfoDouble(sym, SYMBOL_POINT);
   double tickSize = SymbolInfoDouble(sym, SYMBOL_TRADE_TICK_SIZE);
   double tickValue = SymbolInfoDouble(sym, SYMBOL_TRADE_TICK_VALUE);
   double contractSize = SymbolInfoDouble(sym, SYMBOL_TRADE_CONTRACT_SIZE);
   long stopLevel = SymbolInfoInteger(sym, SYMBOL_TRADE_STOPS_LEVEL);
   long freezeLevel = SymbolInfoInteger(sym, SYMBOL_TRADE_FREEZE_LEVEL);
   int digits = (int)SymbolInfoInteger(sym, SYMBOL_DIGITS);

   string d = "{";
   d += "\"Symbol\":\""   + Esc(sym) + "\",";
   d += "\"Ask\":"        + DoubleToString(ask,    digits) + ",";
   d += "\"Bid\":"        + DoubleToString(bid,    digits) + ",";
   d += "\"Spread\":"     + DoubleToString(spread, 1) + ",";
   d += "\"MinLot\":"     + DoubleToString(minLot, 2) + ",";
   d += "\"MaxLot\":"     + DoubleToString(maxLot, 2) + ",";
   d += "\"LotStep\":"    + DoubleToString(lotStep, 2) + ",";
   d += "\"VolumeLimit\":" + DoubleToString(volumeLimit, 2) + ",";
   d += "\"PointSize\":"  + DoubleToString(point, digits + 2) + ",";
   d += "\"TickSize\":"   + DoubleToString(tickSize, digits + 2) + ",";
   d += "\"TickValue\":"  + DoubleToString(tickValue, 2) + ",";
   d += "\"ContractSize\":" + DoubleToString(contractSize, 2) + ",";
   d += "\"StopLevelPoints\":" + IntegerToString(stopLevel) + ",";
   d += "\"FreezeLevelPoints\":" + IntegerToString(freezeLevel) + ",";
   d += "\"Digits\":"     + IntegerToString(digits);
   d += "}";
   return Ok(reqId, d);
}

//+------------------------------------------------------------------+
//| JSON helpers — minimal, no deps                                   |
//+------------------------------------------------------------------+

//+------------------------------------------------------------------+
//| GET_MARKET_SNAPSHOT                                               |
//+------------------------------------------------------------------+
string CmdGetMarketSnapshot(string reqId, string json)
{
   string data = JsonStr(json, "data");
   if(StringLen(data) == 0) data = json;

   string sym = JsonStr(data, "symbol");
   if(StringLen(sym) == 0) sym = JsonStr(data, "Pair");
   StringReplace(sym, "/", "");

   string brokerSymbol = "";
   if(!ResolveBrokerSymbol(sym, brokerSymbol))
      return Err(reqId, "INVALID_SYMBOL", "Symbol not found: " + sym + ". Check broker suffix in MT5 Market Watch.");
   sym = brokerSymbol;

   string tradeType = JsonStr(data, "trade_type");
   if(StringLen(tradeType) == 0) tradeType = JsonStr(data, "TradeType");
   if(StringLen(tradeType) == 0) tradeType = "BUY";

   double entry = JsonDbl(data, "entry_price");
   if(entry <= 0) entry = JsonDbl(data, "EntryPrice");
   double sl = JsonDbl(data, "stop_loss");
   if(sl <= 0) sl = JsonDbl(data, "StopLoss");
   double tp1 = JsonDbl(data, "take_profit");
   if(tp1 <= 0) tp1 = JsonDbl(data, "TakeProfit");
   double tp2 = JsonDbl(data, "take_profit_2");
   if(tp2 <= 0) tp2 = JsonDbl(data, "TakeProfit2");
   double lots = JsonDbl(data, "lot_size");
   if(lots <= 0) lots = JsonDbl(data, "LotSize");
   if(lots <= 0) lots = SymbolInfoDouble(sym, SYMBOL_VOLUME_MIN);

   double maxRiskPct = JsonDbl(data, "max_risk_pct");
   double dailyLossPct = JsonDbl(data, "daily_loss_limit_pct");
   double maxSpreadPips = JsonDbl(data, "max_spread_pips");

   datetime utc = TimeGMT();
   MqlDateTime utcDt;
   TimeToStruct(utc, utcDt);
   bool isWeekend = utcDt.day_of_week == 0 || utcDt.day_of_week == 6;
   bool londonOpen = utcDt.hour >= 7 && utcDt.hour < 16;
   bool newYorkOpen = utcDt.hour >= 12 && utcDt.hour < 21;
   bool overlap = utcDt.hour >= 12 && utcDt.hour < 16;

   string d = "{";
   d += "\"collected_at_utc\":\"" + TimeToString(utc, TIME_DATE|TIME_SECONDS) + "\",";
   d += "\"collected_at_pkt\":\"" + TimeToString(TimeLocal(), TIME_DATE|TIME_SECONDS) + "\",";
   d += "\"account\":" + SnapshotAccountJson(dailyLossPct) + ",";
   d += "\"session\":{";
   d += "\"broker_time\":\"" + TimeToString(TimeCurrent(), TIME_DATE|TIME_SECONDS) + "\",";
   d += "\"terminal_time\":\"" + TimeToString(TimeLocal(), TIME_DATE|TIME_SECONDS) + "\",";
   d += "\"current_hour_utc\":" + IntegerToString(utcDt.hour) + ",";
   d += "\"terminal_connected\":" + BoolJson(TerminalInfoInteger(TERMINAL_CONNECTED) != 0) + ",";
   d += "\"market_open\":" + BoolJson(!isWeekend) + ",";
   d += "\"london_open\":" + BoolJson(londonOpen) + ",";
   d += "\"newyork_open\":" + BoolJson(newYorkOpen) + ",";
   d += "\"overlap_active\":" + BoolJson(overlap) + ",";
   d += "\"session_name\":\"" + (overlap ? "London+NY Overlap" : (londonOpen ? "London" : (newYorkOpen ? "New York" : "Off Session"))) + "\",";
   d += "\"is_weekend\":" + BoolJson(isWeekend) + ",";
   d += "\"vwap\":" + DoubleToString(g_Vwap, (int)SymbolInfoInteger(sym, SYMBOL_DIGITS));
   d += "},";
   d += "\"symbol\":" + SnapshotSymbolJson(sym) + ",";
   d += "\"price\":" + SnapshotPriceJson(sym, maxSpreadPips) + ",";
   d += "\"candles\":{";
   d += "\"h4_last\":" + SnapshotCandleJson(sym, PERIOD_H4) + ",";
   d += "\"h1_last\":" + SnapshotCandleJson(sym, PERIOD_H1) + ",";
   d += "\"m15_last\":" + SnapshotCandleJson(sym, PERIOD_M15) + ",";
   d += "\"m5_last\":" + SnapshotCandleJson(sym, PERIOD_M5);
   d += "},";
   d += "\"indicators\":{";
   d += "\"h1\":" + SnapshotIndicatorsJson(sym, PERIOD_H1, true, true) + ",";
   d += "\"m15\":" + SnapshotIndicatorsJson(sym, PERIOD_M15, false, false) + ",";
   d += "\"m5\":" + SnapshotIndicatorsJson(sym, PERIOD_M5, false, false);
   d += "},";
   d += "\"structure\":" + SnapshotStructureJson(sym) + ",";
   d += "\"levels\":" + SnapshotLevelsJson(sym) + ",";
   d += "\"positions\":" + SnapshotPositionsJson(sym, tradeType) + ",";
   d += "\"last_order\":{\"ticket\":0,\"execution_result\":\"NONE\",\"fill_price\":0.00000,\"slippage\":0,\"error_code\":0,\"requote\":false},";
   d += "\"history\":" + SnapshotHistoryJson() + ",";
   d += "\"risk\":" + SnapshotRiskJson(sym, tradeType, entry, sl, tp1, tp2, lots, maxRiskPct, dailyLossPct) + ",";
   d += "\"news\":{\"news_risk_level\":\"UNAVAILABLE\",\"high_impact_next_60_min\":false,\"events_last_2_hours\":[],\"events_next_60_min\":[],\"source\":\"MT5 EA has no configured news/calendar feed\"}";
   d += "}";

   return Ok(reqId, d);
}

// Extract a string or number value for a given key
string JsonStr(string json, string key)
{
   string search = "\"" + key + "\"";
   int p = StringFind(json, search);
   if(p < 0) return "";
   p += StringLen(search);
   // skip : and whitespace
   while(p < StringLen(json) && (StringGetCharacter(json, p) == ' ' ||
                                  StringGetCharacter(json, p) == ':' ||
                                  StringGetCharacter(json, p) == '\t')) p++;
   if(p >= StringLen(json)) return "";

   ushort ch = StringGetCharacter(json, p);
   if(ch == '"')
   {
      p++;
      string result = "";
      while(p < StringLen(json))
      {
         ushort c = StringGetCharacter(json, p);
         if(c == '\\') { p++; result += ShortToString(StringGetCharacter(json, p)); p++; continue; }
         if(c == '"') break;
         result += ShortToString(c);
         p++;
      }
      return result;
   }
   // non-string value: read until , or } or ]
   string result = "";
   while(p < StringLen(json))
   {
      ushort c = StringGetCharacter(json, p);
      if(c == ',' || c == '}' || c == ']') break;
      result += ShortToString(c);
      p++;
   }
   StringTrimLeft(result);
StringTrimRight(result);
return result;
}

double JsonDbl(string json, string key)
{
   string v = JsonStr(json, key);
   return StringLen(v) == 0 ? 0.0 : StringToDouble(v);
}

string BoolJson(bool value)
{
   return value ? "true" : "false";
}

double SnapshotPipSize(string sym)
{
   int digits = (int)SymbolInfoInteger(sym, SYMBOL_DIGITS);
   double point = SymbolInfoDouble(sym, SYMBOL_POINT);
   if(digits == 3 || digits == 5) return point * 10.0;
   return point;
}

double SnapshotPipValuePerLot(string sym)
{
   double pip = SnapshotPipSize(sym);
   double tickSize = SymbolInfoDouble(sym, SYMBOL_TRADE_TICK_SIZE);
   double tickValue = SymbolInfoDouble(sym, SYMBOL_TRADE_TICK_VALUE);
   if(tickSize <= 0.0) return 0.0;
   return tickValue * (pip / tickSize);
}

double SnapshotBufferValue(int handle, int buffer, int shift)
{
   if(handle == INVALID_HANDLE) return 0.0;
   double values[];
   ArraySetAsSeries(values, true);
   int copied = CopyBuffer(handle, buffer, shift, 1, values);
   IndicatorRelease(handle);
   if(copied < 1) return 0.0;
   if(values[0] == EMPTY_VALUE) return 0.0;
   return values[0];
}

double SnapshotClose(string sym, ENUM_TIMEFRAMES tf, int shift)
{
   double values[];
   ArraySetAsSeries(values, true);
   if(CopyClose(sym, tf, shift, 1, values) < 1) return 0.0;
   return values[0];
}

double SnapshotHigh(string sym, ENUM_TIMEFRAMES tf, int shift, int count)
{
   double values[];
   ArraySetAsSeries(values, true);
   if(CopyHigh(sym, tf, shift, count, values) < 1) return 0.0;
   int index = ArrayMaximum(values, 0, ArraySize(values));
   return index >= 0 ? values[index] : 0.0;
}

double SnapshotLow(string sym, ENUM_TIMEFRAMES tf, int shift, int count)
{
   double values[];
   ArraySetAsSeries(values, true);
   if(CopyLow(sym, tf, shift, count, values) < 1) return 0.0;
   int index = ArrayMinimum(values, 0, ArraySize(values));
   return index >= 0 ? values[index] : 0.0;
}

double SnapshotLastFractal(string sym, ENUM_TIMEFRAMES tf, int buffer)
{
   int handle = iFractals(sym, tf);
   if(handle == INVALID_HANDLE) return 0.0;
   double values[];
   ArraySetAsSeries(values, true);
   int copied = CopyBuffer(handle, buffer, 2, 80, values);
   IndicatorRelease(handle);
   for(int i = 0; i < copied; i++)
   {
      if(values[i] != EMPTY_VALUE && values[i] != 0.0)
         return values[i];
   }
   return 0.0;
}

string SnapshotTrend(string sym, ENUM_TIMEFRAMES tf)
{
   double close = SnapshotClose(sym, tf, 1);
   double ema20 = SnapshotBufferValue(iMA(sym, tf, 20, 0, MODE_EMA, PRICE_CLOSE), 0, 1);
   double ema50 = SnapshotBufferValue(iMA(sym, tf, 50, 0, MODE_EMA, PRICE_CLOSE), 0, 1);
   if(close >= ema20 && ema20 >= ema50) return "BULLISH";
   if(close <= ema20 && ema20 <= ema50) return "BEARISH";
   return "RANGING";
}

string SnapshotAccountJson(double dailyLossPct)
{
   double balance = AccountInfoDouble(ACCOUNT_BALANCE);
   double equity = AccountInfoDouble(ACCOUNT_EQUITY);
   double dailyPnl = SnapshotTodayPnl();
   int tradesToday = SnapshotTradesToday();
   double dailyLossLimit = dailyLossPct > 0 ? equity * dailyLossPct / 100.0 : 0.0;
   string d = "{";
   d += "\"balance\":" + DoubleToString(balance, 2) + ",";
   d += "\"equity\":" + DoubleToString(equity, 2) + ",";
   d += "\"free_margin\":" + DoubleToString(AccountInfoDouble(ACCOUNT_MARGIN_FREE), 2) + ",";
   d += "\"margin_used\":" + DoubleToString(AccountInfoDouble(ACCOUNT_MARGIN), 2) + ",";
   d += "\"margin_level\":" + DoubleToString(AccountInfoDouble(ACCOUNT_MARGIN_LEVEL), 2) + ",";
   d += "\"currency\":\"" + Esc(AccountInfoString(ACCOUNT_CURRENCY)) + "\",";
   d += "\"leverage\":" + IntegerToString(AccountInfoInteger(ACCOUNT_LEVERAGE)) + ",";
   d += "\"floating_pnl\":" + DoubleToString(AccountInfoDouble(ACCOUNT_PROFIT), 2) + ",";
   d += "\"daily_pnl\":" + DoubleToString(dailyPnl, 2) + ",";
   d += "\"daily_trades_taken\":" + IntegerToString(tradesToday) + ",";
   d += "\"consecutive_losses\":" + IntegerToString(SnapshotConsecutiveLosses()) + ",";
   d += "\"win_rate_today_pct\":" + DoubleToString(SnapshotWinRateToday(), 1) + ",";
   d += "\"daily_loss_limit_reached\":" + BoolJson(dailyLossLimit > 0 && dailyPnl <= -dailyLossLimit);
   d += "}";
   return d;
}

string SnapshotSymbolJson(string sym)
{
   int digits = (int)SymbolInfoInteger(sym, SYMBOL_DIGITS);
   double point = SymbolInfoDouble(sym, SYMBOL_POINT);
   long tradeMode = SymbolInfoInteger(sym, SYMBOL_TRADE_MODE);
   string d = "{";
   d += "\"name\":\"" + Esc(sym) + "\",";
   d += "\"digits\":" + IntegerToString(digits) + ",";
   d += "\"point_size\":" + DoubleToString(point, digits) + ",";
   d += "\"pip_size\":" + DoubleToString(SnapshotPipSize(sym), digits) + ",";
   d += "\"tick_size\":" + DoubleToString(SymbolInfoDouble(sym, SYMBOL_TRADE_TICK_SIZE), digits) + ",";
   d += "\"tick_value\":" + DoubleToString(SymbolInfoDouble(sym, SYMBOL_TRADE_TICK_VALUE), 2) + ",";
   d += "\"contract_size\":" + DoubleToString(SymbolInfoDouble(sym, SYMBOL_TRADE_CONTRACT_SIZE), 0) + ",";
   d += "\"min_lot\":" + DoubleToString(SymbolInfoDouble(sym, SYMBOL_VOLUME_MIN), 2) + ",";
   d += "\"max_lot\":" + DoubleToString(SymbolInfoDouble(sym, SYMBOL_VOLUME_MAX), 2) + ",";
   d += "\"lot_step\":" + DoubleToString(SymbolInfoDouble(sym, SYMBOL_VOLUME_STEP), 2) + ",";
   d += "\"stop_level\":" + IntegerToString(SymbolInfoInteger(sym, SYMBOL_TRADE_STOPS_LEVEL)) + ",";
   d += "\"freeze_level\":" + IntegerToString(SymbolInfoInteger(sym, SYMBOL_TRADE_FREEZE_LEVEL)) + ",";
   d += "\"swap_long\":" + DoubleToString(SymbolInfoDouble(sym, SYMBOL_SWAP_LONG), 2) + ",";
   d += "\"swap_short\":" + DoubleToString(SymbolInfoDouble(sym, SYMBOL_SWAP_SHORT), 2) + ",";
   d += "\"commission\":0.00,";
   d += "\"trade_allowed\":" + BoolJson(TerminalInfoInteger(TERMINAL_TRADE_ALLOWED) != 0 && MQLInfoInteger(MQL_TRADE_ALLOWED) != 0) + ",";
   d += "\"symbol_trade_allowed\":" + BoolJson(tradeMode != SYMBOL_TRADE_MODE_DISABLED) + ",";
   d += "\"execution_mode\":\"" + SnapshotExecutionMode(sym) + "\",";
   d += "\"filling_mode\":\"" + SnapshotFillingMode(sym) + "\",";
   d += "\"expiry_mode\":\"" + SnapshotExpiryMode(sym) + "\"";
   d += "}";
   return d;
}

string SnapshotPriceJson(string sym, double maxSpreadPips)
{
   int digits = (int)SymbolInfoInteger(sym, SYMBOL_DIGITS);
   double pip = SnapshotPipSize(sym);
   double bid = SymbolInfoDouble(sym, SYMBOL_BID);
   double ask = SymbolInfoDouble(sym, SYMBOL_ASK);
   double spreadPips = pip > 0 ? (ask - bid) / pip : 0.0;
   MqlRates d1[];
   MqlRates w1[];
   ArraySetAsSeries(d1, true);
   ArraySetAsSeries(w1, true);
   CopyRates(sym, PERIOD_D1, 0, 2, d1);
   CopyRates(sym, PERIOD_W1, 0, 1, w1);
   double dailyOpen = ArraySize(d1) > 0 ? d1[0].open : 0.0;
   double dailyHigh = ArraySize(d1) > 0 ? d1[0].high : 0.0;
   double dailyLow = ArraySize(d1) > 0 ? d1[0].low : 0.0;
   double prevHigh = ArraySize(d1) > 1 ? d1[1].high : 0.0;
   double prevLow = ArraySize(d1) > 1 ? d1[1].low : 0.0;
   double weeklyHigh = ArraySize(w1) > 0 ? w1[0].high : 0.0;
   double weeklyLow = ArraySize(w1) > 0 ? w1[0].low : 0.0;
   string d = "{";
   d += "\"bid\":" + DoubleToString(bid, digits) + ",";
   d += "\"ask\":" + DoubleToString(ask, digits) + ",";
   d += "\"spread_pips\":" + DoubleToString(spreadPips, 1) + ",";
   d += "\"spread_normal\":" + BoolJson(maxSpreadPips <= 0 || spreadPips <= maxSpreadPips) + ",";
   d += "\"daily_open\":" + DoubleToString(dailyOpen, digits) + ",";
   d += "\"daily_high\":" + DoubleToString(dailyHigh, digits) + ",";
   d += "\"daily_low\":" + DoubleToString(dailyLow, digits) + ",";
   d += "\"daily_range_pips\":" + DoubleToString(pip > 0 ? (dailyHigh - dailyLow) / pip : 0.0, 1) + ",";
   d += "\"weekly_high\":" + DoubleToString(weeklyHigh, digits) + ",";
   d += "\"weekly_low\":" + DoubleToString(weeklyLow, digits) + ",";
   d += "\"prev_day_high\":" + DoubleToString(prevHigh, digits) + ",";
   d += "\"prev_day_low\":" + DoubleToString(prevLow, digits) + ",";
   d += "\"distance_to_daily_high_pips\":" + DoubleToString(pip > 0 ? MathAbs(dailyHigh - bid) / pip : 0.0, 1) + ",";
   d += "\"distance_to_daily_low_pips\":" + DoubleToString(pip > 0 ? MathAbs(bid - dailyLow) / pip : 0.0, 1);
   d += "}";
   return d;
}

string SnapshotCandleJson(string sym, ENUM_TIMEFRAMES tf)
{
   int digits = (int)SymbolInfoInteger(sym, SYMBOL_DIGITS);
   double pip = SnapshotPipSize(sym);
   MqlRates rates[];
   ArraySetAsSeries(rates, true);
   int copied = CopyRates(sym, tf, 1, 3, rates);
   if(copied < 1) return "{}";
   MqlRates c = rates[0];
   MqlRates p = copied > 1 ? rates[1] : c;
   double body = MathAbs(c.close - c.open);
   double upper = c.high - MathMax(c.open, c.close);
   double lower = MathMin(c.open, c.close) - c.low;
   bool isDoji = pip > 0 && body / pip <= 1.5;
   bool isPin = body > 0 && (upper >= body * 2.0 || lower >= body * 2.0);
   bool inside = c.high < p.high && c.low > p.low;
   bool engulf = (c.close > c.open && p.close < p.open && c.close >= p.open && c.open <= p.close)
              || (c.close < c.open && p.close > p.open && c.open >= p.close && c.close <= p.open);
   string dir = c.close > c.open ? "BULLISH" : (c.close < c.open ? "BEARISH" : "DOJI");
   string d = "{";
   d += "\"time\":\"" + TimeToString(c.time, TIME_DATE|TIME_MINUTES) + "\",";
   d += "\"open\":" + DoubleToString(c.open, digits) + ",";
   d += "\"high\":" + DoubleToString(c.high, digits) + ",";
   d += "\"low\":" + DoubleToString(c.low, digits) + ",";
   d += "\"close\":" + DoubleToString(c.close, digits) + ",";
   d += "\"volume\":" + IntegerToString((int)c.tick_volume) + ",";
   d += "\"body_pips\":" + DoubleToString(pip > 0 ? body / pip : 0.0, 1) + ",";
   d += "\"direction\":\"" + dir + "\",";
   d += "\"is_engulfing\":" + BoolJson(engulf) + ",";
   d += "\"is_pin_bar\":" + BoolJson(isPin) + ",";
   d += "\"is_inside_bar\":" + BoolJson(inside) + ",";
   d += "\"is_doji\":" + BoolJson(isDoji);
   d += "}";
   return d;
}

string SnapshotIndicatorsJson(string sym, ENUM_TIMEFRAMES tf, bool includeEma200, bool includeBands)
{
   int digits = (int)SymbolInfoInteger(sym, SYMBOL_DIGITS);
   double pip = SnapshotPipSize(sym);
   double close = SnapshotClose(sym, tf, 1);
   double rsi = SnapshotBufferValue(iRSI(sym, tf, 14, PRICE_CLOSE), 0, 1);
   int macdHandle = iMACD(sym, tf, 12, 26, 9, PRICE_CLOSE);
   double macdMain = 0.0, macdSignal = 0.0;
   if(macdHandle != INVALID_HANDLE)
   {
      double mainValues[];
      double signalValues[];
      ArraySetAsSeries(mainValues, true);
      ArraySetAsSeries(signalValues, true);
      if(CopyBuffer(macdHandle, 0, 1, 1, mainValues) > 0) macdMain = mainValues[0];
      if(CopyBuffer(macdHandle, 1, 1, 1, signalValues) > 0) macdSignal = signalValues[0];
      IndicatorRelease(macdHandle);
   }
   double ema20 = SnapshotBufferValue(iMA(sym, tf, 20, 0, MODE_EMA, PRICE_CLOSE), 0, 1);
   double ema50 = SnapshotBufferValue(iMA(sym, tf, 50, 0, MODE_EMA, PRICE_CLOSE), 0, 1);
   double ema200 = includeEma200 ? SnapshotBufferValue(iMA(sym, tf, 200, 0, MODE_EMA, PRICE_CLOSE), 0, 1) : 0.0;
   double adx = SnapshotBufferValue(iADX(sym, tf, 14), 0, 1);
   double atr = SnapshotBufferValue(iATR(sym, tf, 14), 0, 1);
   int stochHandle = iStochastic(sym, tf, 5, 3, 3, MODE_SMA, STO_LOWHIGH);
   double stochK = 0.0, stochD = 0.0;
   if(stochHandle != INVALID_HANDLE)
   {
      double kValues[];
      double dValues[];
      ArraySetAsSeries(kValues, true);
      ArraySetAsSeries(dValues, true);
      if(CopyBuffer(stochHandle, 0, 1, 1, kValues) > 0) stochK = kValues[0];
      if(CopyBuffer(stochHandle, 1, 1, 1, dValues) > 0) stochD = dValues[0];
      IndicatorRelease(stochHandle);
   }
   double bandsUpper = 0.0, bandsMid = 0.0, bandsLower = 0.0;
   if(includeBands)
   {
      int bandsHandle = iBands(sym, tf, 20, 0, 2.0, PRICE_CLOSE);
      if(bandsHandle != INVALID_HANDLE)
      {
         double mid[], up[], low[];
         ArraySetAsSeries(mid, true);
         ArraySetAsSeries(up, true);
         ArraySetAsSeries(low, true);
         // MQL5 iBands buffers: 0=middle/base, 1=upper, 2=lower.
         if(CopyBuffer(bandsHandle, 0, 1, 1, mid) > 0) bandsMid = mid[0];
         if(CopyBuffer(bandsHandle, 1, 1, 1, up) > 0) bandsUpper = up[0];
         if(CopyBuffer(bandsHandle, 2, 1, 1, low) > 0) bandsLower = low[0];
         IndicatorRelease(bandsHandle);
      }
   }
   string d = "{";
   d += "\"rsi\":" + DoubleToString(rsi, 1) + ",";
   d += "\"rsi_signal\":\"" + (rsi >= 70 ? "OVERBOUGHT" : (rsi <= 30 ? "OVERSOLD" : "NEUTRAL")) + "\",";
   d += "\"macd_value\":" + DoubleToString(macdMain, digits) + ",";
   d += "\"macd_signal_line\":" + DoubleToString(macdSignal, digits) + ",";
   d += "\"macd_histogram\":" + DoubleToString(macdMain - macdSignal, digits) + ",";
   d += "\"macd_bias\":\"" + (macdMain >= macdSignal ? "BULLISH" : "BEARISH") + "\",";
   d += "\"ema20\":" + DoubleToString(ema20, digits) + ",";
   d += "\"ema50\":" + DoubleToString(ema50, digits) + ",";
   if(includeEma200) d += "\"ema200\":" + DoubleToString(ema200, digits) + ",";
   d += "\"price_vs_ema20\":\"" + (close >= ema20 ? "ABOVE" : "BELOW") + "\",";
   d += "\"price_vs_ema50\":\"" + (close >= ema50 ? "ABOVE" : "BELOW") + "\",";
   if(includeEma200) d += "\"price_vs_ema200\":\"" + (close >= ema200 ? "ABOVE" : "BELOW") + "\",";
   d += "\"adx\":" + DoubleToString(adx, 1) + ",";
   d += "\"adx_signal\":\"" + (adx >= 25 ? "STRONG_TREND" : (adx >= 18 ? "WEAK_TREND" : "NO_TREND")) + "\",";
   if(includeBands)
   {
      d += "\"bollinger_upper\":" + DoubleToString(bandsUpper, digits) + ",";
      d += "\"bollinger_mid\":" + DoubleToString(bandsMid, digits) + ",";
      d += "\"bollinger_lower\":" + DoubleToString(bandsLower, digits) + ",";
      d += "\"bollinger_position\":\"" + (close >= bandsUpper ? "UPPER" : (close <= bandsLower ? "LOWER" : "MIDDLE")) + "\",";
   }
   d += "\"atr\":" + DoubleToString(atr, digits) + ",";
   d += "\"atr_pips\":" + DoubleToString(pip > 0.0 ? atr / pip : 0.0, 1) + ",";
   d += "\"stoch_k\":" + DoubleToString(stochK, 1) + ",";
   d += "\"stoch_d\":" + DoubleToString(stochD, 1) + ",";
   d += "\"stoch_signal\":\"" + (stochK >= 80 ? "OVERBOUGHT" : (stochK <= 20 ? "OVERSOLD" : "NEUTRAL")) + "\",";
   d += "\"fractal_up\":" + DoubleToString(SnapshotLastFractal(sym, tf, 0), digits) + ",";
   d += "\"fractal_down\":" + DoubleToString(SnapshotLastFractal(sym, tf, 1), digits);
   d += "}";
   return d;
}

string SnapshotStructureJson(string sym)
{
   int digits = (int)SymbolInfoInteger(sym, SYMBOL_DIGITS);
   double pip = SnapshotPipSize(sym);
   string h4 = SnapshotTrend(sym, PERIOD_H4);
   string h1 = SnapshotTrend(sym, PERIOD_H1);
   string m15 = SnapshotTrend(sym, PERIOD_M15);
   string m5 = SnapshotTrend(sym, PERIOD_M5);
   double swingHigh = SnapshotHigh(sym, PERIOD_H1, 1, 20);
   double swingLow = SnapshotLow(sym, PERIOD_H1, 1, 20);
   double recentHigh = SnapshotHigh(sym, PERIOD_H1, 1, 5);
   double priorHigh = SnapshotHigh(sym, PERIOD_H1, 6, 5);
   double recentLow = SnapshotLow(sym, PERIOD_H1, 1, 5);
   double priorLow = SnapshotLow(sym, PERIOD_H1, 6, 5);
   double close = SnapshotClose(sym, PERIOD_M15, 1);
   string d = "{";
   d += "\"trend_h4\":\"" + h4 + "\",";
   d += "\"trend_h1\":\"" + h1 + "\",";
   d += "\"trend_m15\":\"" + m15 + "\",";
   d += "\"trend_m5\":\"" + m5 + "\",";
   d += "\"all_timeframes_aligned\":" + BoolJson(h4 == h1 && h1 == m15 && m15 == m5) + ",";
   d += "\"market_regime\":\"" + (SnapshotBufferValue(iADX(sym, PERIOD_H1, 14), 0, 1) >= 25 ? "TRENDING" : "RANGING") + "\",";
   d += "\"higher_high\":" + BoolJson(recentHigh > priorHigh) + ",";
   d += "\"higher_low\":" + BoolJson(recentLow > priorLow) + ",";
   d += "\"lower_high\":" + BoolJson(recentHigh < priorHigh) + ",";
   d += "\"lower_low\":" + BoolJson(recentLow < priorLow) + ",";
   d += "\"swing_high\":" + DoubleToString(swingHigh, digits) + ",";
   d += "\"swing_low\":" + DoubleToString(swingLow, digits) + ",";
   d += "\"bos_detected\":" + BoolJson(close > swingHigh || close < swingLow) + ",";
   d += "\"choch_detected\":false,";
   d += "\"liquidity_sweep\":false,";
   d += "\"pullback_detected\":" + BoolJson(pip > 0 && MathAbs(close - SnapshotBufferValue(iMA(sym, PERIOD_M15, 20, 0, MODE_EMA, PRICE_CLOSE), 0, 1)) / pip <= 10.0) + ",";
   d += "\"pullback_to_level\":" + DoubleToString(close, digits) + ",";
   d += "\"entry_confirmed\":false";
   d += "}";
   return d;
}

string SnapshotLevelsJson(string sym)
{
   int digits = (int)SymbolInfoInteger(sym, SYMBOL_DIGITS);
   double pip = SnapshotPipSize(sym);
   double bid = SymbolInfoDouble(sym, SYMBOL_BID);
   MqlRates d1[];
   ArraySetAsSeries(d1, true);
   CopyRates(sym, PERIOD_D1, 0, 2, d1);
   double prevHigh = ArraySize(d1) > 1 ? d1[1].high : 0.0;
   double prevLow = ArraySize(d1) > 1 ? d1[1].low : 0.0;
   double prevClose = ArraySize(d1) > 1 ? d1[1].close : 0.0;
   double pivot = (prevHigh + prevLow + prevClose) / 3.0;
   double s1 = pivot * 2.0 - prevHigh;
   double r1 = pivot * 2.0 - prevLow;
   double s2 = pivot - (prevHigh - prevLow);
   double r2 = pivot + (prevHigh - prevLow);
   MqlRates w1[];
   ArraySetAsSeries(w1, true);
   CopyRates(sym, PERIOD_W1, 0, 2, w1);
   double weeklyHigh = ArraySize(w1) > 1 ? w1[1].high : 0.0;
   double weeklyLow = ArraySize(w1) > 1 ? w1[1].low : 0.0;
   double weeklyClose = ArraySize(w1) > 1 ? w1[1].close : 0.0;
   double weeklyPivot = weeklyHigh > 0 && weeklyLow > 0 ? (weeklyHigh + weeklyLow + weeklyClose) / 3.0 : 0.0;
   double weeklyS1 = weeklyPivot > 0 ? weeklyPivot * 2.0 - weeklyHigh : 0.0;
   double weeklyR1 = weeklyPivot > 0 ? weeklyPivot * 2.0 - weeklyLow : 0.0;
   double asianHigh = g_AsianHigh;
   double asianLow = g_AsianLow;
   SnapshotAsianRange(sym, asianHigh, asianLow);
   double support = bid >= s1 ? s1 : s2;
   double resistance = bid <= r1 ? r1 : r2;
   string d = "{";
   d += "\"nearest_support_1\":" + DoubleToString(support, digits) + ",";
   d += "\"nearest_support_2\":" + DoubleToString(s2, digits) + ",";
   d += "\"nearest_resistance_1\":" + DoubleToString(resistance, digits) + ",";
   d += "\"nearest_resistance_2\":" + DoubleToString(r2, digits) + ",";
   d += "\"distance_to_support_pips\":" + DoubleToString(pip > 0 ? MathAbs(bid - support) / pip : 0.0, 1) + ",";
   d += "\"distance_to_resistance_pips\":" + DoubleToString(pip > 0 ? MathAbs(resistance - bid) / pip : 0.0, 1) + ",";
   d += "\"price_at_key_level\":" + BoolJson(pip > 0 && MathMin(MathAbs(bid - support), MathAbs(resistance - bid)) / pip <= 5.0) + ",";
   d += "\"key_level_type\":\"" + (MathAbs(bid - support) <= MathAbs(resistance - bid) ? "SUPPORT" : "RESISTANCE") + "\",";
   d += "\"prev_day_high\":" + DoubleToString(prevHigh, digits) + ",";
   d += "\"prev_day_low\":" + DoubleToString(prevLow, digits) + ",";
   d += "\"asian_high\":" + DoubleToString(asianHigh, digits) + ",\"asian_low\":" + DoubleToString(asianLow, digits) + ",";
   d += "\"daily_pivot\":" + DoubleToString(pivot, digits) + ",";
   d += "\"daily_s1\":" + DoubleToString(s1, digits) + ",";
   d += "\"daily_s2\":" + DoubleToString(s2, digits) + ",";
   d += "\"daily_r1\":" + DoubleToString(r1, digits) + ",";
   d += "\"daily_r2\":" + DoubleToString(r2, digits) + ",";
   d += "\"weekly_pivot\":" + DoubleToString(weeklyPivot, digits) + ",";
   d += "\"weekly_s1\":" + DoubleToString(weeklyS1, digits) + ",";
   d += "\"weekly_r1\":" + DoubleToString(weeklyR1, digits);
   d += "}";
   return d;
}

void SnapshotAsianRange(string sym, double &asianHigh, double &asianLow)
{
   if(asianHigh > 0.0 && asianLow > 0.0) return;

   MqlDateTime dt;
   TimeToStruct(TimeCurrent(), dt);
   dt.hour = 0;
   dt.min = 0;
   dt.sec = 0;
   datetime from = StructToTime(dt);
   datetime to = from + 7 * 60 * 60;

   MqlRates rates[];
   ArraySetAsSeries(rates, true);
   int copied = CopyRates(sym, PERIOD_M15, from, to, rates);
   for(int i = 0; i < copied; i++)
   {
      if(asianHigh == 0.0 || rates[i].high > asianHigh) asianHigh = rates[i].high;
      if(asianLow == 0.0 || rates[i].low < asianLow) asianLow = rates[i].low;
   }
}

string SnapshotPositionsJson(string sym, string tradeType)
{
   string arr = "[";
   bool first = true;
   int samePair = 0;
   bool sameDir = false;
   bool opposite = false;
   string samePairDirection = "NONE";
   for(int i = 0; i < PositionsTotal(); i++)
   {
      if(!PosInfo.SelectByIndex(i)) continue;
      string typeStr = PosInfo.PositionType() == POSITION_TYPE_BUY ? "BUY" : "SELL";
      if(PosInfo.Symbol() == sym)
      {
         samePair++;
         samePairDirection = typeStr;
         if(typeStr == tradeType) sameDir = true;
         else opposite = true;
      }
      if(!first) arr += ",";
      first = false;
      arr += "{";
      arr += "\"ticket\":" + IntegerToString(PosInfo.Ticket()) + ",";
      arr += "\"pair\":\"" + Esc(PosInfo.Symbol()) + "\",";
      arr += "\"direction\":\"" + typeStr + "\",";
      arr += "\"lots\":" + DoubleToString(PosInfo.Volume(), 2) + ",";
      arr += "\"open_price\":" + DoubleToString(PosInfo.PriceOpen(), 5) + ",";
      arr += "\"current_price\":" + DoubleToString(PosInfo.PriceCurrent(), 5) + ",";
      double pip = SnapshotPipSize(PosInfo.Symbol());
      double openPips = pip > 0.0
         ? (PosInfo.PositionType() == POSITION_TYPE_BUY
            ? (PosInfo.PriceCurrent() - PosInfo.PriceOpen()) / pip
            : (PosInfo.PriceOpen() - PosInfo.PriceCurrent()) / pip)
         : 0.0;
      arr += "\"pnl\":" + DoubleToString(PosInfo.Profit(), 2) + ",";
      arr += "\"pips\":" + DoubleToString(openPips, 1);
      arr += "}";
   }
   arr += "]";
   string d = "{";
   d += "\"total_open\":" + IntegerToString(PositionsTotal()) + ",";
   d += "\"same_pair_open\":" + BoolJson(samePair > 0) + ",";
   d += "\"same_pair_direction\":\"" + samePairDirection + "\",";
   d += "\"duplicate_trade_exists\":" + BoolJson(sameDir) + ",";
   d += "\"opposite_trade_exists\":" + BoolJson(opposite) + ",";
   d += "\"pending_orders\":[],";
   d += "\"open_list\":" + arr;
   d += "}";
   return d;
}

string SnapshotRiskJson(string sym, string tradeType, double entry, double sl, double tp1, double tp2, double lots, double maxRiskPct, double dailyLossPct)
{
   int digits = (int)SymbolInfoInteger(sym, SYMBOL_DIGITS);
   double bid = SymbolInfoDouble(sym, SYMBOL_BID);
   double ask = SymbolInfoDouble(sym, SYMBOL_ASK);
   if(entry <= 0) entry = tradeType == "SELL" ? bid : ask;
   double pip = SnapshotPipSize(sym);
   double pipValue = SnapshotPipValuePerLot(sym);
   bool isSell = SnapshotIsSell(tradeType);
   bool slValid = sl > 0.0 && (isSell ? sl > entry : sl < entry);
   bool tp1Valid = tp1 > 0.0 && (isSell ? tp1 < entry : tp1 > entry);
   bool tp2Valid = tp2 > 0.0 && (isSell ? tp2 < entry : tp2 > entry);
   double slPips = pip > 0 && slValid ? MathAbs(entry - sl) / pip : 0.0;
   double tp1Pips = pip > 0 && tp1Valid ? MathAbs(tp1 - entry) / pip : 0.0;
   double tp2Pips = pip > 0 && tp2Valid ? MathAbs(tp2 - entry) / pip : 0.0;
   double rr = slPips > 0 ? tp1Pips / slPips : 0.0;
   double equity = AccountInfoDouble(ACCOUNT_EQUITY);
   double dailyLimit = dailyLossPct > 0 ? equity * dailyLossPct / 100.0 : 0.0;
   double marginRequired = SnapshotMarginRequired(sym, tradeType, lots, entry);
   string d = "{";
   d += "\"max_risk_pct\":" + DoubleToString(maxRiskPct, 2) + ",";
   d += "\"max_risk_dollar\":" + DoubleToString(maxRiskPct > 0 ? equity * maxRiskPct / 100.0 : 0.0, 2) + ",";
   d += "\"suggested_sl\":" + DoubleToString(sl, digits) + ",";
   d += "\"suggested_tp1\":" + DoubleToString(tp1, digits) + ",";
   d += "\"suggested_tp2\":" + DoubleToString(tp2, digits) + ",";
   d += "\"sl_distance_pips\":" + DoubleToString(slPips, 1) + ",";
   d += "\"tp1_distance_pips\":" + DoubleToString(tp1Pips, 1) + ",";
   d += "\"rr_ratio\":" + DoubleToString(rr, 2) + ",";
   d += "\"calculated_lot\":" + DoubleToString(lots, 2) + ",";
   d += "\"dollar_risk\":" + DoubleToString(slPips * pipValue * lots, 2) + ",";
   d += "\"dollar_profit_tp1\":" + DoubleToString(tp1Pips * pipValue * lots, 2) + ",";
   d += "\"dollar_profit_tp2\":" + DoubleToString(tp2Pips * pipValue * lots, 2) + ",";
   d += "\"atr_based_sl\":" + DoubleToString(SnapshotBufferValue(iATR(sym, PERIOD_H1, 14), 0, 1), digits) + ",";
   d += "\"margin_required\":" + DoubleToString(marginRequired, 2) + ",";
   d += "\"daily_loss_limit_dollar\":" + DoubleToString(dailyLimit, 2) + ",";
   d += "\"daily_loss_remaining\":" + DoubleToString(dailyLimit > 0 ? dailyLimit + SnapshotTodayPnl() : 0.0, 2);
   d += "}";
   return d;
}

bool SnapshotIsSell(string tradeType)
{
   return StringCompare(tradeType, "SELL", false) == 0;
}

double SnapshotMarginRequired(string sym, string tradeType, double lots, double price)
{
   if(lots <= 0.0 || price <= 0.0) return 0.0;

   double margin = 0.0;
   ENUM_ORDER_TYPE orderType = SnapshotIsSell(tradeType) ? ORDER_TYPE_SELL : ORDER_TYPE_BUY;
   if(OrderCalcMargin(orderType, sym, lots, price, margin))
      return margin;

   return 0.0;
}

string SnapshotHistoryJson()
{
   double pnl = SnapshotTodayPnl();
   int total = SnapshotTradesToday();
   string d = "{";
   d += "\"total_trades_today\":" + IntegerToString(total) + ",";
   d += "\"consecutive_losses\":" + IntegerToString(SnapshotConsecutiveLosses()) + ",";
   d += "\"win_rate_today_pct\":" + DoubleToString(SnapshotWinRateToday(), 1) + ",";
   d += "\"total_pnl_today\":" + DoubleToString(pnl, 2) + ",";
   d += "\"last_5_trades\":" + SnapshotLastTradesJson(5);
   d += "}";
   return d;
}

datetime SnapshotDayStart()
{
   MqlDateTime dt;
   TimeToStruct(TimeCurrent(), dt);
   dt.hour = 0;
   dt.min = 0;
   dt.sec = 0;
   return StructToTime(dt);
}

double SnapshotTodayPnl()
{
   double pnl = 0.0;
   if(!HistorySelect(SnapshotDayStart(), TimeCurrent())) return 0.0;
   int total = HistoryDealsTotal();
   for(int i = 0; i < total; i++)
   {
      ulong ticket = HistoryDealGetTicket(i);
      if(ticket == 0) continue;
      long entry = HistoryDealGetInteger(ticket, DEAL_ENTRY);
      if(entry != DEAL_ENTRY_OUT && entry != DEAL_ENTRY_INOUT) continue;
      pnl += HistoryDealGetDouble(ticket, DEAL_PROFIT)
           + HistoryDealGetDouble(ticket, DEAL_SWAP)
           + HistoryDealGetDouble(ticket, DEAL_COMMISSION);
   }
   return pnl;
}

int SnapshotTradesToday()
{
   int count = 0;
   if(!HistorySelect(SnapshotDayStart(), TimeCurrent())) return 0;
   int total = HistoryDealsTotal();
   for(int i = 0; i < total; i++)
   {
      ulong ticket = HistoryDealGetTicket(i);
      long entry = HistoryDealGetInteger(ticket, DEAL_ENTRY);
      if(entry == DEAL_ENTRY_OUT || entry == DEAL_ENTRY_INOUT) count++;
   }
   return count;
}

double SnapshotWinRateToday()
{
   int wins = 0;
   int totalClosed = 0;
   if(!HistorySelect(SnapshotDayStart(), TimeCurrent())) return 0.0;
   int total = HistoryDealsTotal();
   for(int i = 0; i < total; i++)
   {
      ulong ticket = HistoryDealGetTicket(i);
      long entry = HistoryDealGetInteger(ticket, DEAL_ENTRY);
      if(entry != DEAL_ENTRY_OUT && entry != DEAL_ENTRY_INOUT) continue;
      totalClosed++;
      if(HistoryDealGetDouble(ticket, DEAL_PROFIT) > 0) wins++;
   }
   return totalClosed > 0 ? (100.0 * wins / totalClosed) : 0.0;
}

int SnapshotConsecutiveLosses()
{
   int losses = 0;
   if(!HistorySelect(SnapshotDayStart(), TimeCurrent())) return 0;
   for(int i = HistoryDealsTotal() - 1; i >= 0; i--)
   {
      ulong ticket = HistoryDealGetTicket(i);
      long entry = HistoryDealGetInteger(ticket, DEAL_ENTRY);
      if(entry != DEAL_ENTRY_OUT && entry != DEAL_ENTRY_INOUT) continue;
      double profit = HistoryDealGetDouble(ticket, DEAL_PROFIT);
      if(profit < 0) losses++;
      else break;
   }
   return losses;
}

string SnapshotLastTradesJson(int maxItems)
{
   string arr = "[";
   int added = 0;
   if(HistorySelect(SnapshotDayStart(), TimeCurrent()))
   {
      for(int i = HistoryDealsTotal() - 1; i >= 0 && added < maxItems; i--)
      {
         ulong ticket = HistoryDealGetTicket(i);
         long entry = HistoryDealGetInteger(ticket, DEAL_ENTRY);
         if(entry != DEAL_ENTRY_OUT && entry != DEAL_ENTRY_INOUT) continue;
         double profit = HistoryDealGetDouble(ticket, DEAL_PROFIT)
                       + HistoryDealGetDouble(ticket, DEAL_SWAP)
                       + HistoryDealGetDouble(ticket, DEAL_COMMISSION);
         string symbol = HistoryDealGetString(ticket, DEAL_SYMBOL);
         string direction = "";
         double pips = SnapshotClosedDealPips(ticket, symbol, direction);
         if(added > 0) arr += ",";
         arr += "{";
         arr += "\"pair\":\"" + Esc(symbol) + "\",";
         arr += "\"direction\":\"" + direction + "\",";
         arr += "\"result\":\"" + (profit >= 0 ? "WIN" : "LOSS") + "\",";
         arr += "\"pips\":" + DoubleToString(pips, 1) + ",";
         arr += "\"pnl\":" + DoubleToString(profit, 2);
         arr += "}";
         added++;
      }
   }
   arr += "]";
   return arr;
}

double SnapshotClosedDealPips(ulong exitTicket, string sym, string &direction)
{
   direction = "UNKNOWN";
   double pip = SnapshotPipSize(sym);
   if(pip <= 0.0) return 0.0;

   long positionId = HistoryDealGetInteger(exitTicket, DEAL_POSITION_ID);
   double exitPrice = HistoryDealGetDouble(exitTicket, DEAL_PRICE);
   for(int i = HistoryDealsTotal() - 1; i >= 0; i--)
   {
      ulong ticket = HistoryDealGetTicket(i);
      if(ticket == 0 || ticket == exitTicket) continue;
      if(HistoryDealGetInteger(ticket, DEAL_POSITION_ID) != positionId) continue;

      long entry = HistoryDealGetInteger(ticket, DEAL_ENTRY);
      if(entry != DEAL_ENTRY_IN && entry != DEAL_ENTRY_INOUT) continue;

      long type = HistoryDealGetInteger(ticket, DEAL_TYPE);
      double entryPrice = HistoryDealGetDouble(ticket, DEAL_PRICE);
      direction = type == DEAL_TYPE_BUY ? "BUY" : "SELL";
      return type == DEAL_TYPE_BUY
         ? (exitPrice - entryPrice) / pip
         : (entryPrice - exitPrice) / pip;
   }

   long exitType = HistoryDealGetInteger(exitTicket, DEAL_TYPE);
   direction = exitType == DEAL_TYPE_BUY ? "SELL" : "BUY";
   return 0.0;
}

string SnapshotExecutionMode(string sym)
{
   long mode = SymbolInfoInteger(sym, SYMBOL_TRADE_EXEMODE);
   if(mode == SYMBOL_TRADE_EXECUTION_REQUEST) return "REQUEST";
   if(mode == SYMBOL_TRADE_EXECUTION_INSTANT) return "INSTANT";
   if(mode == SYMBOL_TRADE_EXECUTION_MARKET) return "MARKET";
   if(mode == SYMBOL_TRADE_EXECUTION_EXCHANGE) return "EXCHANGE";
   return "UNKNOWN";
}

string SnapshotFillingMode(string sym)
{
   long mode = SymbolInfoInteger(sym, SYMBOL_FILLING_MODE);
   if((mode & SYMBOL_FILLING_IOC) == SYMBOL_FILLING_IOC) return "IOC";
   if((mode & SYMBOL_FILLING_FOK) == SYMBOL_FILLING_FOK) return "FOK";
   if((mode & SYMBOL_FILLING_BOC) == SYMBOL_FILLING_BOC) return "BOC";
   return "UNKNOWN";
}

string SnapshotExpiryMode(string sym)
{
   long mode = SymbolInfoInteger(sym, SYMBOL_EXPIRATION_MODE);
   if((mode & SYMBOL_EXPIRATION_DAY) == SYMBOL_EXPIRATION_DAY) return "DAY";
   if((mode & SYMBOL_EXPIRATION_GTC) == SYMBOL_EXPIRATION_GTC) return "GTC";
   if((mode & SYMBOL_EXPIRATION_SPECIFIED) == SYMBOL_EXPIRATION_SPECIFIED) return "SPECIFIED";
   return "UNKNOWN";
}

// Escape JSON string
string Esc(string s)
{
   StringReplace(s, "\\", "\\\\");
   StringReplace(s, "\"", "\\\"");
   StringReplace(s, "\n", "\\n");
   StringReplace(s, "\r", "\\r");
   return s;
}

//+------------------------------------------------------------------+
//| Response builders                                                 |
//+------------------------------------------------------------------+
string Ok(string reqId, string data)
{
   return "{\"req_id\":\"" + reqId + "\",\"success\":true,\"data\":" + data + ",\"error\":\"\"}";
}

string Err(string reqId, string code, string msg)
{
   EA_Log("ERR [" + code + "] " + msg);
   return "{\"req_id\":\"" + reqId + "\",\"success\":false,\"data\":null,"
        + "\"error\":\"" + Esc(code + ": " + msg) + "\"}";
}

//+------------------------------------------------------------------+
//| Pipe helpers                                                      |
//+------------------------------------------------------------------+
void ClosePipe()
{
   if(g_pipe != INVALID_HANDLE)
   {
      FileClose(g_pipe);
      g_pipe = INVALID_HANDLE;
      g_connected = false;
   }
}

void EA_Log(string msg)
{
   if(InpEnableLog)
      Print("[TradingBotEA] ", msg);
}
