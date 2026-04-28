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
input int    InpBufSize     = 131072;               // Buffer size (128 KB)
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
void OnTick() { ServeOnePipeRequest(); }

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
   if(cmd == "GET_ACCOUNT")    return CmdGetAccount(reqId);
   if(cmd == "GET_POSITIONS")  return CmdGetPositions(reqId);
   if(cmd == "OPEN_TRADE")     return CmdOpenTrade(reqId, json);
   if(cmd == "CLOSE_TRADE")    return CmdCloseTrade(reqId, json);
   if(cmd == "MODIFY_POSITION")return CmdModifyPosition(reqId, json);
   if(cmd == "GET_SYMBOL_INFO")return CmdGetSymbolInfo(reqId, json);
   if(cmd == "CLOSE_ALL")      return CmdCloseAll(reqId);

   return Err(reqId, "UNKNOWN_CMD", "Unknown command: " + cmd);
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
      arr += "\"OpenTime\":\""   + TimeToString(PosInfo.Time(), TIME_DATE|TIME_MINUTES) + "\",";
      arr += "\"SlMovedToBreakeven\":false";
      arr += "}";
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
      EA_Log(StringFormat("Opened %s #%d %s @ %.5f lots %.2f", typeStr, ticket, symbol, execPrice, lots));
      string d = "{";
      d += "\"Ticket\":"        + IntegerToString((int)ticket) + ",";
      d += "\"Status\":\"Filled\",";
      d += "\"ExecutedPrice\":" + DoubleToString(execPrice, 5) + ",";
      d += "\"ExecutedLots\":"  + DoubleToString(lots,      2);
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

   string d = "{";
   d += "\"Symbol\":\""   + Esc(sym) + "\",";
   d += "\"Ask\":"        + DoubleToString(ask,    5) + ",";
   d += "\"Bid\":"        + DoubleToString(bid,    5) + ",";
   d += "\"Spread\":"     + DoubleToString(spread, 1) + ",";
   d += "\"MinLot\":"     + DoubleToString(minLot, 2) + ",";
   d += "\"MaxLot\":"     + DoubleToString(maxLot, 2) + ",";
   d += "\"Digits\":"     + IntegerToString(SymbolInfoInteger(sym, SYMBOL_DIGITS));
   d += "}";
   return Ok(reqId, d);
}

//+------------------------------------------------------------------+
//| JSON helpers — minimal, no deps                                   |
//+------------------------------------------------------------------+

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
