//+------------------------------------------------------------------+
//| ATLAS_Bridge.mq5                                                 |
//| TCP socket server — bridges ATLAS C# app to MetaTrader 5        |
//| Attach to any chart. Default port: 9090.                        |
//+------------------------------------------------------------------+
#property copyright "ATLAS Trading System"
#property version   "1.00"
#property strict

#include <Trade\Trade.mqh>

input int    Server_Port     = 9090;
input string Server_Host     = "127.0.0.1";
input int    Max_Connections = 1;

CTrade trade;
int    server_socket = INVALID_HANDLE;
int    client_socket = INVALID_HANDLE;

//+------------------------------------------------------------------+
int OnInit()
{
    server_socket = SocketCreate();
    if (server_socket == INVALID_HANDLE)
    {
        Print("ATLAS_Bridge: failed to create socket");
        return INIT_FAILED;
    }

    if (!SocketBind(server_socket, Server_Host, Server_Port))
    {
        Print("ATLAS_Bridge: bind failed on port ", Server_Port);
        SocketClose(server_socket);
        return INIT_FAILED;
    }

    if (!SocketListen(server_socket, Max_Connections))
    {
        Print("ATLAS_Bridge: listen failed");
        SocketClose(server_socket);
        return INIT_FAILED;
    }

    Print("ATLAS_Bridge: listening on port ", Server_Port);
    return INIT_SUCCEEDED;
}

//+------------------------------------------------------------------+
void OnDeinit(const int reason)
{
    if (client_socket != INVALID_HANDLE) SocketClose(client_socket);
    if (server_socket != INVALID_HANDLE) SocketClose(server_socket);
}

//+------------------------------------------------------------------+
void OnTick()
{
    // Accept new connection if none active
    if (client_socket == INVALID_HANDLE)
    {
        client_socket = SocketAccept(server_socket);
        if (client_socket != INVALID_HANDLE)
            Print("ATLAS_Bridge: client connected");
    }

    if (client_socket == INVALID_HANDLE) return;

    // Read request (newline-terminated JSON)
    string request = "";
    uint   bytes_available = SocketIsReadable(client_socket);
    if (bytes_available == 0) return;

    uchar buf[];
    if (SocketRead(client_socket, buf, bytes_available, 1000) <= 0)
    {
        Print("ATLAS_Bridge: client disconnected");
        SocketClose(client_socket);
        client_socket = INVALID_HANDLE;
        return;
    }

    request = CharArrayToString(buf);
    StringTrimRight(request);
    if (StringLen(request) == 0) return;

    string req_id = Json_Get(request, "req_id");
    string response = Dispatch(request);
    if (StringLen(req_id) > 0 && StringLen(response) > 0 && StringGetCharacter(response, 0) == '{')
        response = "{\"req_id\":\"" + req_id + "\"," + StringSubstr(response, 1);
    response += "\n";

    uchar resp_bytes[];
    StringToCharArray(response, resp_bytes, 0, StringLen(response));
    SocketSend(client_socket, resp_bytes, ArraySize(resp_bytes));
}

//+------------------------------------------------------------------+
string Dispatch(string req)
{
    string cmd = Json_Get(req, "cmd");

    if (cmd == "PING")               return "{\"status\":\"ok\"}";
    if (cmd == "GET_ACCOUNT_INFO")   return Get_Account_Info();
    if (cmd == "GET_TICK")           return Get_Tick(Json_Get(req, "symbol"));
    if (cmd == "GET_SPREAD")         return Get_Spread(Json_Get(req, "symbol"));
    if (cmd == "IS_MARKET_OPEN")     return Is_Market_Open(Json_Get(req, "symbol"));
    if (cmd == "GET_CANDLES")        return Get_Candles(Json_Get(req, "symbol"), Json_Get(req, "timeframe"), (int)StringToInteger(Json_Get(req, "count")));
    if (cmd == "GET_POSITIONS")      return Get_Positions();
    if (cmd == "SEND_ORDER")         return Send_Order(req);
    if (cmd == "MODIFY_SL")         return Modify_SL(req);
    if (cmd == "MODIFY_TP")         return Modify_TP(req);
    if (cmd == "CLOSE_POSITION")    return Close_Position(req);
    if (cmd == "PARTIAL_CLOSE")     return Partial_Close(req);

    return "{\"status\":\"error\",\"error\":\"Unknown command\"}";
}

//+------------------------------------------------------------------+
string Get_Account_Info()
{
    return StringFormat(
        "{\"status\":\"ok\",\"balance\":%.2f,\"equity\":%.2f,\"margin\":%.2f,\"free_margin\":%.2f,\"margin_level\":%.2f}",
        AccountInfoDouble(ACCOUNT_BALANCE),
        AccountInfoDouble(ACCOUNT_EQUITY),
        AccountInfoDouble(ACCOUNT_MARGIN),
        AccountInfoDouble(ACCOUNT_MARGIN_FREE),
        AccountInfoDouble(ACCOUNT_MARGIN_LEVEL)
    );
}

//+------------------------------------------------------------------+
string Get_Tick(string symbol)
{
    MqlTick tick;
    if (!SymbolInfoTick(symbol, tick))
        return "{\"status\":\"error\",\"error\":\"SymbolInfoTick failed\"}";

    double spread_pips = (tick.ask - tick.bid) / Get_Pip_Size(symbol);

    return StringFormat(
        "{\"status\":\"ok\",\"bid\":%.5f,\"ask\":%.5f,\"spread_pips\":%.1f,\"market_open\":true}",
        tick.bid, tick.ask, spread_pips
    );
}

//+------------------------------------------------------------------+
string Get_Spread(string symbol)
{
    MqlTick tick;
    if (!SymbolInfoTick(symbol, tick))
        return "{\"status\":\"error\",\"error\":\"tick failed\"}";

    double spread_pips = (tick.ask - tick.bid) / Get_Pip_Size(symbol);
    return StringFormat("{\"status\":\"ok\",\"spread_pips\":%.2f}", spread_pips);
}

//+------------------------------------------------------------------+
string Is_Market_Open(string symbol)
{
    MqlTick tick;
    bool open = SymbolInfoTick(symbol, tick);
    return StringFormat("{\"status\":\"ok\",\"market_open\":%s}", open ? "true" : "false");
}

//+------------------------------------------------------------------+
string Get_Candles(string symbol, string tf_str, int count)
{
    ENUM_TIMEFRAMES tf = String_To_TF(tf_str);
    MqlRates rates[];
    ArraySetAsSeries(rates, true);
    int copied = CopyRates(symbol, tf, 0, count, rates);
    if (copied <= 0)
        return "{\"status\":\"error\",\"error\":\"CopyRates failed\"}";

    string candles = "";
    for (int i = copied - 1; i >= 0; i--)
    {
        if (StringLen(candles) > 0) candles += ",";
        candles += StringFormat(
            "{\"t\":%d,\"o\":%.5f,\"h\":%.5f,\"l\":%.5f,\"c\":%.5f,\"v\":%d}",
            (int)rates[i].time,
            rates[i].open, rates[i].high, rates[i].low, rates[i].close,
            (int)rates[i].tick_volume
        );
    }

    return StringFormat("{\"status\":\"ok\",\"candles\":[%s]}", candles);
}

//+------------------------------------------------------------------+
string Get_Positions()
{
    string positions = "";
    int total = PositionsTotal();
    for (int i = 0; i < total; i++)
    {
        ulong ticket = PositionGetTicket(i);
        if (ticket == 0) continue;

        if (StringLen(positions) > 0) positions += ",";
        string ptype = PositionGetInteger(POSITION_TYPE) == POSITION_TYPE_BUY ? "buy" : "sell";
        positions += StringFormat(
            "{\"ticket\":%d,\"symbol\":\"%s\",\"type\":\"%s\",\"lots\":%.2f,\"open_price\":%.5f,\"sl\":%.5f,\"tp\":%.5f,\"profit\":%.2f,\"open_time\":%d,\"comment\":\"%s\"}",
            ticket,
            PositionGetString(POSITION_SYMBOL),
            ptype,
            PositionGetDouble(POSITION_VOLUME),
            PositionGetDouble(POSITION_PRICE_OPEN),
            PositionGetDouble(POSITION_SL),
            PositionGetDouble(POSITION_TP),
            PositionGetDouble(POSITION_PROFIT),
            (int)PositionGetInteger(POSITION_TIME),
            PositionGetString(POSITION_COMMENT)
        );
    }
    return StringFormat("{\"status\":\"ok\",\"positions\":[%s]}", positions);
}

//+------------------------------------------------------------------+
string Send_Order(string req)
{
    string symbol    = Json_Get(req, "symbol");
    string direction = Json_Get(req, "direction");
    double lot       = StringToDouble(Json_Get(req, "lot"));
    double price     = StringToDouble(Json_Get(req, "price"));
    double sl        = StringToDouble(Json_Get(req, "sl"));
    double tp        = StringToDouble(Json_Get(req, "tp"));
    string comment   = Json_Get(req, "comment");

    ENUM_ORDER_TYPE order_type = direction == "buy" ? ORDER_TYPE_BUY : ORDER_TYPE_SELL;

    MqlTradeRequest  mql_req  = {};
    MqlTradeResult   mql_res  = {};

    mql_req.action    = TRADE_ACTION_DEAL;
    mql_req.symbol    = symbol;
    mql_req.volume    = lot;
    mql_req.type      = order_type;
    mql_req.price     = price;
    mql_req.sl        = sl;
    mql_req.tp        = tp;
    mql_req.comment   = comment;
    mql_req.type_filling = ORDER_FILLING_IOC;

    if (!OrderSend(mql_req, mql_res))
    {
        return StringFormat("{\"status\":\"error\",\"error\":\"OrderSend failed: %d %s\",\"ticket\":0}",
            mql_res.retcode, mql_res.comment);
    }

    return StringFormat("{\"status\":\"ok\",\"ticket\":%d,\"broker_message\":\"%s\",\"executed_price\":%.5f}",
        mql_res.order, mql_res.comment, mql_res.price);
}

//+------------------------------------------------------------------+
string Modify_SL(string req)
{
    long   ticket = StringToInteger(Json_Get(req, "ticket"));
    double new_sl = StringToDouble(Json_Get(req, "sl"));

    if (!PositionSelectByTicket(ticket))
        return "{\"status\":\"error\",\"error\":\"Position not found\"}";

    trade.PositionModify(ticket, new_sl, PositionGetDouble(POSITION_TP));
    return "{\"status\":\"ok\"}";
}

//+------------------------------------------------------------------+
string Modify_TP(string req)
{
    long   ticket = StringToInteger(Json_Get(req, "ticket"));
    double new_tp = StringToDouble(Json_Get(req, "tp"));

    if (!PositionSelectByTicket(ticket))
        return "{\"status\":\"error\",\"error\":\"Position not found\"}";

    trade.PositionModify(ticket, PositionGetDouble(POSITION_SL), new_tp);
    return "{\"status\":\"ok\"}";
}

//+------------------------------------------------------------------+
string Close_Position(string req)
{
    long ticket = StringToInteger(Json_Get(req, "ticket"));
    if (!PositionSelectByTicket(ticket))
        return "{\"status\":\"error\",\"error\":\"Position not found\"}";

    trade.PositionClose(ticket);
    return "{\"status\":\"ok\"}";
}

//+------------------------------------------------------------------+
string Partial_Close(string req)
{
    long   ticket = StringToInteger(Json_Get(req, "ticket"));
    double lots   = StringToDouble(Json_Get(req, "lot"));

    if (!PositionSelectByTicket(ticket))
        return "{\"status\":\"error\",\"error\":\"Position not found\"}";

    double available = PositionGetDouble(POSITION_VOLUME);
    if (lots <= 0 || lots > available)
        return StringFormat("{\"status\":\"error\",\"error\":\"Invalid lot %.2f (position has %.2f)\"}",
            lots, available);

    if (!trade.PositionClosePartial(ticket, lots))
        return StringFormat("{\"status\":\"error\",\"error\":\"PositionClosePartial failed: %d\"}",
            trade.ResultRetcode());

    return "{\"status\":\"ok\"}";
}

//+------------------------------------------------------------------+
string Json_Get(string json, string key)
{
    string search = "\"" + key + "\":";
    int start = StringFind(json, search);
    if (start < 0) return "";

    start += StringLen(search);
    // Skip whitespace
    while (start < StringLen(json) && StringGetCharacter(json, start) == ' ') start++;

    bool is_string = StringGetCharacter(json, start) == '"';
    if (is_string) start++;

    int end = start;
    if (is_string)
    {
        while (end < StringLen(json) && StringGetCharacter(json, end) != '"') end++;
    }
    else
    {
        while (end < StringLen(json) && StringGetCharacter(json, end) != ',' && StringGetCharacter(json, end) != '}') end++;
    }

    return StringSubstr(json, start, end - start);
}

//+------------------------------------------------------------------+
double Get_Pip_Size(string symbol)
{
    int digits = (int)SymbolInfoInteger(symbol, SYMBOL_DIGITS);
    if (digits == 3 || digits == 5) return 0.0001;
    if (digits == 2)                return 0.01;   // XAUUSD
    return 0.0001;
}

//+------------------------------------------------------------------+
ENUM_TIMEFRAMES String_To_TF(string tf)
{
    if (tf == "M1")  return PERIOD_M1;
    if (tf == "M5")  return PERIOD_M5;
    if (tf == "M15") return PERIOD_M15;
    if (tf == "M30") return PERIOD_M30;
    if (tf == "H1")  return PERIOD_H1;
    if (tf == "H4")  return PERIOD_H4;
    if (tf == "D1")  return PERIOD_D1;
    if (tf == "W1")  return PERIOD_W1;
    return PERIOD_H1;
}

//+------------------------------------------------------------------+
ENUM_TIMEFRAMES Minutes_To_TF(int minutes)
{
    switch (minutes)
    {
        case 1:     return PERIOD_M1;
        case 5:     return PERIOD_M5;
        case 15:    return PERIOD_M15;
        case 30:    return PERIOD_M30;
        case 60:    return PERIOD_H1;
        case 240:   return PERIOD_H4;
        case 1440:  return PERIOD_D1;
        case 10080: return PERIOD_W1;
        default:    return PERIOD_H1;
    }
}
