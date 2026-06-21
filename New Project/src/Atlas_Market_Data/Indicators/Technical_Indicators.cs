using Atlas_Domain.BusinessObjects;

namespace Atlas_Market_Data.Indicators;

public static class Technical_Indicators
{
    public static decimal Calculate_EMA(List<Candle_BO> candles, int period)
    {
        if (candles.Count < period) return 0;

        var closes = candles.Select(c => c.Close).ToList();
        decimal multiplier = 2m / (period + 1);
        decimal ema = closes.Take(period).Average();

        foreach (var close in closes.Skip(period))
            ema = (close - ema) * multiplier + ema;

        return Math.Round(ema, 5);
    }

    public static decimal Calculate_ATR(List<Candle_BO> candles, int period = 14)
    {
        if (candles.Count < period + 1) return 0;

        var trs = new List<decimal>();
        for (int i = 1; i < candles.Count; i++)
        {
            var high = candles[i].High;
            var low  = candles[i].Low;
            var prev_close = candles[i - 1].Close;
            var tr = Math.Max(high - low, Math.Max(Math.Abs(high - prev_close), Math.Abs(low - prev_close)));
            trs.Add(tr);
        }

        if (trs.Count < period) return 0;
        decimal atr = trs.Take(period).Average();
        foreach (var tr in trs.Skip(period))
            atr = (atr * (period - 1) + tr) / period;

        return Math.Round(atr, 5);
    }

    public static decimal Calculate_RSI(List<Candle_BO> candles, int period = 14)
    {
        if (candles.Count < period + 1) return 50;

        var closes = candles.Select(c => c.Close).ToList();
        var gains  = new List<decimal>();
        var losses = new List<decimal>();

        for (int i = 1; i < closes.Count; i++)
        {
            var change = closes[i] - closes[i - 1];
            gains.Add(change > 0 ? change : 0);
            losses.Add(change < 0 ? Math.Abs(change) : 0);
        }

        if (gains.Count < period) return 50;

        decimal avg_gain = gains.Take(period).Average();
        decimal avg_loss = losses.Take(period).Average();

        foreach (var (g, l) in gains.Skip(period).Zip(losses.Skip(period)))
        {
            avg_gain = (avg_gain * (period - 1) + g) / period;
            avg_loss = (avg_loss * (period - 1) + l) / period;
        }

        if (avg_loss == 0) return 100;
        decimal rs = avg_gain / avg_loss;
        return Math.Round(100 - 100 / (1 + rs), 2);
    }

    public static (decimal High, decimal Low) Get_Range(List<Candle_BO> candles, int look_back)
    {
        var window = candles.TakeLast(look_back).ToList();
        return (window.Max(c => c.High), window.Min(c => c.Low));
    }

    public static bool Is_Higher_High_Higher_Low(List<Candle_BO> candles, int look_back = 10)
    {
        var window = candles.TakeLast(look_back).ToList();
        if (window.Count < 4) return false;
        var swing_highs = Get_Swing_Points(window, is_high: true);
        var swing_lows  = Get_Swing_Points(window, is_high: false);
        return swing_highs.Count >= 2 && swing_highs[^1] > swing_highs[^2]
            && swing_lows.Count >= 2  && swing_lows[^1]  > swing_lows[^2];
    }

    public static bool Is_Lower_High_Lower_Low(List<Candle_BO> candles, int look_back = 10)
    {
        var window = candles.TakeLast(look_back).ToList();
        if (window.Count < 4) return false;
        var swing_highs = Get_Swing_Points(window, is_high: true);
        var swing_lows  = Get_Swing_Points(window, is_high: false);
        return swing_highs.Count >= 2 && swing_highs[^1] < swing_highs[^2]
            && swing_lows.Count >= 2  && swing_lows[^1]  < swing_lows[^2];
    }

    private static List<decimal> Get_Swing_Points(List<Candle_BO> candles, bool is_high)
    {
        var points = new List<decimal>();
        for (int i = 1; i < candles.Count - 1; i++)
        {
            if (is_high && candles[i].High > candles[i - 1].High && candles[i].High > candles[i + 1].High)
                points.Add(candles[i].High);
            else if (!is_high && candles[i].Low < candles[i - 1].Low && candles[i].Low < candles[i + 1].Low)
                points.Add(candles[i].Low);
        }
        return points;
    }
}
