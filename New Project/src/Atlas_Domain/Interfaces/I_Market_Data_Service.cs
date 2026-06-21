using Atlas_Domain.BusinessObjects;

namespace Atlas_Domain.Interfaces;

public interface I_Market_Data_Service
{
    Task<List<Candle_BO>> Get_Candles_Async(string symbol_name, string timeframe, int count);
    Task<decimal> Get_Current_Spread_Pips_Async(string symbol_name);
    Task<decimal> Get_Current_Price_Async(string symbol_name, bool is_bid = true);
    Task<bool> Is_Market_Open_Async(string symbol_name);
}
