using Atlas_Domain.BusinessObjects;

namespace Atlas_Tests;

/// <summary>
/// Builds synthetic Candle_BO series with known trend/range/EMA characteristics for
/// deterministic strategy and regime-detector tests.
/// </summary>
public static class Test_Data_Factory
{
    /// <summary>
    /// A zigzag series that produces clean Higher-High/Higher-Low (uptrend) or
    /// Lower-High/Lower-Low (downtrend) swing points at every odd/even bar, or a flat
    /// range when trend_step is 0 (equal swing points fail the strict HH/HL and LH/LL checks).
    /// </summary>
    public static List<Candle_BO> Zigzag(
        int count, decimal base_price, decimal trend_step, decimal amplitude,
        string timeframe = "H4", DateTime? start_time = null)
    {
        var candles = new List<Candle_BO>();
        var time = start_time ?? DateTime.UtcNow.AddHours(-count * 4);

        for (int i = 0; i < count; i++)
        {
            decimal center = base_price + i * trend_step;
            bool peak_bar = i % 2 == 1;
            decimal high, low, open, close;

            if (peak_bar)
            {
                high = center + amplitude;
                low  = center;
                open = low + amplitude * 0.3m;
                close = low + amplitude * 0.7m;
            }
            else
            {
                high = center;
                low  = center - amplitude;
                open = low + amplitude * 0.7m;
                close = low + amplitude * 0.3m;
            }

            candles.Add(new Candle_BO
            {
                Symbol_Name   = "EURUSD",
                Timeframe     = timeframe,
                Open_Time_UTC = time.AddHours(i),
                Open  = open,
                High  = high,
                Low   = low,
                Close = close,
                Volume = 100
            });
        }

        return candles;
    }

    /// <summary>A tight, flat range — no directional structure, low ATR relative to price.</summary>
    public static List<Candle_BO> Flat_Range(int count, decimal base_price, decimal amplitude, string timeframe = "H4", DateTime? start_time = null) =>
        Zigzag(count, base_price, trend_step: 0, amplitude, timeframe, start_time);

    /// <summary>
    /// A perfectly flat, constant-price series: every bar has the same Open/Close and the
    /// same tiny High/Low half-range around it. Gives fully deterministic, count-independent
    /// EMA (= price) and ATR (= 2 * half_range) values for strategy-signal tests.
    /// </summary>
    public static List<Candle_BO> Flat_Series(int count, decimal price, decimal half_range, string timeframe, DateTime? start_time = null)
    {
        var list = new List<Candle_BO>();
        var time = start_time ?? DateTime.UtcNow.AddHours(-count);
        for (int i = 0; i < count; i++)
        {
            list.Add(new Candle_BO
            {
                Symbol_Name = "EURUSD",
                Timeframe = timeframe,
                Open_Time_UTC = time.AddHours(i),
                Open = price,
                Close = price,
                High = price + half_range,
                Low = price - half_range,
                Volume = 100
            });
        }
        return list;
    }

    public static Candle_BO Candle(decimal open, decimal high, decimal low, decimal close, DateTime? time = null, string tf = "M15") => new()
    {
        Symbol_Name = "EURUSD",
        Timeframe = tf,
        Open_Time_UTC = time ?? DateTime.UtcNow,
        Open = open,
        High = high,
        Low = low,
        Close = close,
        Volume = 100
    };
}
