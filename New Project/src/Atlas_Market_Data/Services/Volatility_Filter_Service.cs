using Atlas_Domain.BusinessObjects;
using Atlas_Market_Data.Indicators;
using Atlas_Shared;

namespace Atlas_Market_Data.Services;

public class Volatility_Filter_Service
{
    public (bool Is_Normal, string Reason) Check_Volatility(
        List<Candle_BO> h4_candles,
        decimal rolling_average_atr)
    {
        var current_atr = Technical_Indicators.Calculate_ATR(h4_candles, Trading_Constants.ATR_Period);

        if (current_atr > rolling_average_atr * Trading_Constants.ATR_High_Multiplier)
            return (false, $"ATR {current_atr:F5} is abnormally HIGH (>{Trading_Constants.ATR_High_Multiplier}x average {rolling_average_atr:F5})");

        if (current_atr < rolling_average_atr * Trading_Constants.ATR_Low_Multiplier)
            return (false, $"ATR {current_atr:F5} is abnormally LOW (<{Trading_Constants.ATR_Low_Multiplier}x average {rolling_average_atr:F5})");

        return (true, string.Empty);
    }

    public decimal Calculate_Rolling_Average_ATR(List<Candle_BO> h4_candles, int look_back_days = 14)
    {
        // Compute ATR for each day window and average
        int candles_per_day = 6; // H4: 6 candles per day
        int total = look_back_days * candles_per_day;
        if (h4_candles.Count < total + 1) return Technical_Indicators.Calculate_ATR(h4_candles, Trading_Constants.ATR_Period);

        var daily_atrs = new List<decimal>();
        for (int i = 0; i < look_back_days; i++)
        {
            var window = h4_candles.Skip(h4_candles.Count - total + i * candles_per_day)
                                   .Take(candles_per_day + 1)
                                   .ToList();
            daily_atrs.Add(Technical_Indicators.Calculate_ATR(window, Math.Min(Trading_Constants.ATR_Period, window.Count - 1)));
        }
        return daily_atrs.Where(a => a > 0).DefaultIfEmpty(0).Average();
    }
}
