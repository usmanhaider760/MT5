using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Interfaces;
using Atlas_Execution.Protocol;

namespace Atlas_Execution.MT5;

public class MT5_Market_Data_Service : I_Market_Data_Service
{
    private readonly MT5_Bridge_Client _bridge;

    private static readonly Dictionary<string, string> Timeframe_Map = new()
    {
        ["M1"]  = "1",
        ["M5"]  = "5",
        ["M15"] = "15",
        ["M30"] = "30",
        ["H1"]  = "60",
        ["H4"]  = "240",
        ["D1"]  = "1440",
        ["W1"]  = "10080"
    };

    public MT5_Market_Data_Service(MT5_Bridge_Client bridge)
    {
        _bridge = bridge;
    }

    public async Task<List<Candle_BO>> Get_Candles_Async(string symbol_name, string timeframe, int count)
    {
        var tf = Timeframe_Map.TryGetValue(timeframe.ToUpper(), out var t) ? t : "60";

        var response = await _bridge.Send_Async<MT5_Candles_Response>(new MT5_Request
        {
            Command   = MT5_Command.GET_CANDLES,
            Symbol    = symbol_name,
            Timeframe = tf,
            Count     = count
        });

        if (response?.Is_Ok != true || response.Candles.Count == 0)
            return [];

        return response.Candles.Select(c => new Candle_BO
        {
            Symbol_Name   = symbol_name,
            Timeframe     = timeframe,
            Open_Time_UTC = DateTimeOffset.FromUnixTimeSeconds(c.Time_Unix).UtcDateTime,
            Open          = (decimal)c.Open,
            High          = (decimal)c.High,
            Low           = (decimal)c.Low,
            Close         = (decimal)c.Close,
            Volume        = c.Volume
        }).ToList();
    }

    public async Task<decimal> Get_Current_Spread_Pips_Async(string symbol_name)
    {
        var response = await _bridge.Send_Async<MT5_Tick_Response>(new MT5_Request
        {
            Command = MT5_Command.GET_SPREAD,
            Symbol  = symbol_name
        });

        return response?.Is_Ok == true ? (decimal)response.Spread_Pips : 999m;
    }

    public async Task<decimal> Get_Current_Price_Async(string symbol_name, bool is_bid = true)
    {
        var response = await _bridge.Send_Async<MT5_Tick_Response>(new MT5_Request
        {
            Command = MT5_Command.GET_TICK,
            Symbol  = symbol_name
        });

        if (response?.Is_Ok != true) return 0;
        return is_bid ? (decimal)response.Bid : (decimal)response.Ask;
    }

    public async Task<bool> Is_Market_Open_Async(string symbol_name)
    {
        var response = await _bridge.Send_Async<MT5_Tick_Response>(new MT5_Request
        {
            Command = MT5_Command.IS_MARKET_OPEN,
            Symbol  = symbol_name
        });

        return response?.Is_Ok == true && response.Market_Open;
    }
}
