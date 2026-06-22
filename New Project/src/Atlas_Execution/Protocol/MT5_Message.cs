using System.Text.Json.Serialization;

namespace Atlas_Execution.Protocol;

// Commands sent from C# to MT5 EA
public static class MT5_Command
{
    public const string GET_ACCOUNT_INFO  = "GET_ACCOUNT_INFO";
    public const string GET_CANDLES       = "GET_CANDLES";
    public const string GET_TICK          = "GET_TICK";
    public const string GET_SPREAD        = "GET_SPREAD";
    public const string IS_MARKET_OPEN    = "IS_MARKET_OPEN";
    public const string GET_POSITIONS     = "GET_POSITIONS";
    public const string SEND_ORDER        = "SEND_ORDER";
    public const string MODIFY_SL         = "MODIFY_SL";
    public const string CLOSE_POSITION    = "CLOSE_POSITION";
    public const string PARTIAL_CLOSE     = "PARTIAL_CLOSE";
    public const string PING              = "PING";
}

public class MT5_Request
{
    [JsonPropertyName("cmd")]
    public string Command { get; set; } = string.Empty;

    [JsonPropertyName("symbol")]
    public string? Symbol { get; set; }

    [JsonPropertyName("timeframe")]
    public string? Timeframe { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("ticket")]
    public long Ticket { get; set; }

    [JsonPropertyName("direction")]
    public string? Direction { get; set; }

    [JsonPropertyName("lot")]
    public double Lot { get; set; }

    [JsonPropertyName("price")]
    public double Price { get; set; }

    [JsonPropertyName("sl")]
    public double StopLoss { get; set; }

    [JsonPropertyName("tp")]
    public double TakeProfit { get; set; }

    [JsonPropertyName("comment")]
    public string Comment { get; set; } = "ATLAS";
}

public class MT5_Response
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    public bool Is_Ok => Status == "ok";
}

public class MT5_Account_Response : MT5_Response
{
    [JsonPropertyName("balance")]
    public double Balance { get; set; }

    [JsonPropertyName("equity")]
    public double Equity { get; set; }

    [JsonPropertyName("margin")]
    public double Margin { get; set; }

    [JsonPropertyName("free_margin")]
    public double Free_Margin { get; set; }

    [JsonPropertyName("margin_level")]
    public double Margin_Level { get; set; }
}

public class MT5_Tick_Response : MT5_Response
{
    [JsonPropertyName("bid")]
    public double Bid { get; set; }

    [JsonPropertyName("ask")]
    public double Ask { get; set; }

    [JsonPropertyName("spread_pips")]
    public double Spread_Pips { get; set; }

    [JsonPropertyName("market_open")]
    public bool Market_Open { get; set; }
}

public class MT5_Candle_Item
{
    [JsonPropertyName("t")]
    public long Time_Unix { get; set; }

    [JsonPropertyName("o")]
    public double Open { get; set; }

    [JsonPropertyName("h")]
    public double High { get; set; }

    [JsonPropertyName("l")]
    public double Low { get; set; }

    [JsonPropertyName("c")]
    public double Close { get; set; }

    [JsonPropertyName("v")]
    public long Volume { get; set; }
}

public class MT5_Candles_Response : MT5_Response
{
    [JsonPropertyName("candles")]
    public List<MT5_Candle_Item> Candles { get; set; } = [];
}

public class MT5_Position_Item
{
    [JsonPropertyName("ticket")]
    public long Ticket { get; set; }

    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty; // "buy" or "sell"

    [JsonPropertyName("lots")]
    public double Lots { get; set; }

    [JsonPropertyName("open_price")]
    public double Open_Price { get; set; }

    [JsonPropertyName("sl")]
    public double Sl { get; set; }

    [JsonPropertyName("tp")]
    public double Tp { get; set; }

    [JsonPropertyName("profit")]
    public double Profit { get; set; }

    [JsonPropertyName("open_time")]
    public long Open_Time_Unix { get; set; }

    [JsonPropertyName("comment")]
    public string Comment { get; set; } = string.Empty;
}

public class MT5_Positions_Response : MT5_Response
{
    [JsonPropertyName("positions")]
    public List<MT5_Position_Item> Positions { get; set; } = [];
}

public class MT5_Order_Response : MT5_Response
{
    [JsonPropertyName("ticket")]
    public long Ticket { get; set; }

    [JsonPropertyName("broker_message")]
    public string Broker_Message { get; set; } = string.Empty;

    [JsonPropertyName("executed_price")]
    public double Executed_Price { get; set; }
}
