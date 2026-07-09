using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;
using Atlas_Market_Data.Services;
using Xunit;

namespace Atlas_Tests;

public class Market_Regime_Detector_Tests
{
    private readonly Market_Regime_Detector _sut = new();

    // A generic "filler" series used where a timeframe's exact bias doesn't matter to the
    // assertion, but the minimum candle-count gate must still be satisfied.
    private static List<Candle_BO> Filler(int count, string tf) =>
        Test_Data_Factory.Zigzag(count, base_price: 1.2000m, trend_step: 0.0005m, amplitude: 0.0010m, timeframe: tf);

    [Fact]
    public async Task Fewer_Than_20_D1_Candles_Returns_Abnormal_No_Trade()
    {
        var d1 = Filler(10, "D1"); // below the 20-candle minimum
        var h4 = Filler(60, "H4");
        var h1 = Filler(30, "H1");

        var ctx = await _sut.Detect_Market_Regime_Async("EURUSD", d1, h4, h1);

        Assert.Equal(Market_Regime_Type.Abnormal_No_Trade, ctx.Regime);
    }

    [Fact]
    public async Task D1_Bias_Is_Buy_When_Last_Close_Above_Ema200()
    {
        // EMA200 only becomes meaningful with >=200 candles; use a real 220-bar uptrend
        // so the last close is genuinely above its own 200-EMA, not the count<200 trivial-zero case.
        var d1 = Test_Data_Factory.Zigzag(220, base_price: 1.1000m, trend_step: 0.0006m, amplitude: 0.0015m, timeframe: "D1");
        var h4 = Filler(60, "H4");
        var h1 = Filler(30, "H1");

        var ctx = await _sut.Detect_Market_Regime_Async("EURUSD", d1, h4, h1);

        Assert.Equal(Trade_Direction_Type.Buy, ctx.D1_Bias);
    }

    [Fact]
    public async Task H4_Bias_Is_Sell_When_Below_Ema200_With_Lower_High_Lower_Low()
    {
        var d1 = Filler(20, "D1");
        var h4 = Test_Data_Factory.Zigzag(220, base_price: 1.3000m, trend_step: -0.0006m, amplitude: 0.0015m, timeframe: "H4");
        var h1 = Filler(30, "H1");

        var ctx = await _sut.Detect_Market_Regime_Async("EURUSD", d1, h4, h1);

        Assert.Equal(Trade_Direction_Type.Sell, ctx.H4_Bias);
    }

    [Fact]
    public async Task Higher_Timeframes_Aligned_True_When_D1_H4_H1_All_Buy()
    {
        var d1 = Test_Data_Factory.Zigzag(60, base_price: 1.1000m, trend_step: 0.0006m, amplitude: 0.0015m, timeframe: "D1");
        var h4 = Test_Data_Factory.Zigzag(60, base_price: 1.1000m, trend_step: 0.0006m, amplitude: 0.0015m, timeframe: "H4");
        var h1 = Test_Data_Factory.Zigzag(30, base_price: 1.1000m, trend_step: 0.0006m, amplitude: 0.0015m, timeframe: "H1");

        var ctx = await _sut.Detect_Market_Regime_Async("EURUSD", d1, h4, h1);

        Assert.Equal(Trade_Direction_Type.Buy, ctx.D1_Bias);
        Assert.Equal(Trade_Direction_Type.Buy, ctx.H4_Bias);
        Assert.Equal(Trade_Direction_Type.Buy, ctx.H1_Bias);
        Assert.True(ctx.Higher_Timeframes_Aligned);
    }

    [Fact]
    public async Task Trend_Swing_Detected_When_Aligned_With_Clear_Structure()
    {
        var d1 = Test_Data_Factory.Zigzag(60, base_price: 1.1000m, trend_step: 0.0006m, amplitude: 0.0015m, timeframe: "D1");
        var h4 = Test_Data_Factory.Zigzag(60, base_price: 1.1000m, trend_step: 0.0006m, amplitude: 0.0015m, timeframe: "H4");
        var h1 = Test_Data_Factory.Zigzag(30, base_price: 1.1000m, trend_step: 0.0006m, amplitude: 0.0015m, timeframe: "H1");

        var ctx = await _sut.Detect_Market_Regime_Async("EURUSD", d1, h4, h1);

        Assert.Equal(Market_Regime_Type.Trend_Swing, ctx.Regime);
    }

    [Fact]
    public async Task Compression_Breakout_Detected_When_Flat_And_Tight()
    {
        // A flat (non-drifting) zigzag has no directional structure (swing points don't
        // progress) and its 20-bar range is inherently tight relative to its own ATR.
        var d1 = Filler(20, "D1");
        var h4 = Test_Data_Factory.Flat_Range(60, base_price: 1.2000m, amplitude: 0.0010m, timeframe: "H4");
        var h1 = Filler(30, "H1");

        var ctx = await _sut.Detect_Market_Regime_Async("EURUSD", d1, h4, h1);

        Assert.False(ctx.Higher_Timeframes_Aligned);
        Assert.Equal(Market_Regime_Type.Compression_Breakout, ctx.Regime);
    }

    [Fact]
    public async Task Range_Detected_When_Not_Trending_And_Not_Compressed()
    {
        var d1 = Filler(20, "D1");
        var h1 = Filler(30, "H1");

        // A flat baseline followed by one large outlier bar: the 20-bar range balloons
        // (Get_Range takes a raw max/min) while ATR barely moves (it's a smoothed rolling
        // average), so the range is no longer "compressed" relative to ATR — yet the flat
        // baseline's swing lows never progress, so no clean trend structure exists either.
        var h4 = new List<Candle_BO>(Test_Data_Factory.Flat_Range(60, base_price: 1.2000m, amplitude: 0.0010m, timeframe: "H4"));
        var last_time = h4[^1].Open_Time_UTC.AddHours(4);
        h4.Add(new Candle_BO
        {
            Symbol_Name = "EURUSD", Timeframe = "H4", Open_Time_UTC = last_time,
            Open = 1.2000m, Close = 1.2050m, High = 1.2500m, Low = 1.1990m, Volume = 100
        });

        var ctx = await _sut.Detect_Market_Regime_Async("EURUSD", d1, h4, h1);

        Assert.False(ctx.Higher_Timeframes_Aligned);
        Assert.Equal(Market_Regime_Type.Range, ctx.Regime);
    }

    [Fact]
    public async Task Score_Components_Sum_To_Regime_Quality_Score()
    {
        var d1 = Filler(20, "D1");
        var h4 = Filler(60, "H4");
        var h1 = Filler(30, "H1");

        var ctx = await _sut.Detect_Market_Regime_Async("EURUSD", d1, h4, h1);

        int expected = ctx.Score_Trend_Alignment + ctx.Score_Volatility_Quality + ctx.Score_Session_Quality
                     + ctx.Score_Spread_Quality + ctx.Score_Structure_Clarity + ctx.Score_News_Safety
                     + ctx.Score_Correlation_Safety;

        Assert.Equal(expected, ctx.Regime_Quality_Score);
    }
}
