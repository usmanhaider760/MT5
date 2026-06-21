using Atlas_Domain.BusinessObjects;

namespace Atlas_Application.Backtest;

/// <summary>
/// Slices a flat list of candles into the timeframe windows the strategy engine expects.
/// The engine calls this once per bar as it replays history forward.
/// </summary>
public class Backtest_Data_Provider
{
    private readonly List<Candle_BO> _m15_all;
    private readonly List<Candle_BO> _h1_all;
    private readonly List<Candle_BO> _h4_all;
    private readonly List<Candle_BO> _d1_all;

    public Backtest_Data_Provider(
        List<Candle_BO> m15_candles,
        List<Candle_BO> h1_candles,
        List<Candle_BO> h4_candles,
        List<Candle_BO> d1_candles)
    {
        _m15_all = m15_candles;
        _h1_all  = h1_candles;
        _h4_all  = h4_candles;
        _d1_all  = d1_candles;
    }

    /// <summary>
    /// Returns candle slices visible at the given point in time (no look-ahead).
    /// </summary>
    public (List<Candle_BO> D1, List<Candle_BO> H4, List<Candle_BO> H1, List<Candle_BO> M15)
        Get_Visible_Candles(DateTime bar_time_utc, int d1_count = 50, int h4_count = 200, int h1_count = 100, int m15_count = 50)
    {
        return (
            D1  : Slice(_d1_all,  bar_time_utc, d1_count),
            H4  : Slice(_h4_all,  bar_time_utc, h4_count),
            H1  : Slice(_h1_all,  bar_time_utc, h1_count),
            M15 : Slice(_m15_all, bar_time_utc, m15_count)
        );
    }

    public IEnumerable<Candle_BO> Replay_M15_Bars(DateTime from, DateTime to) =>
        _m15_all.Where(c => c.Open_Time_UTC >= from && c.Open_Time_UTC < to);

    private static List<Candle_BO> Slice(List<Candle_BO> candles, DateTime up_to, int count)
    {
        var visible = candles.Where(c => c.Open_Time_UTC < up_to).ToList();
        return visible.Count <= count ? visible : visible.Skip(visible.Count - count).ToList();
    }
}
