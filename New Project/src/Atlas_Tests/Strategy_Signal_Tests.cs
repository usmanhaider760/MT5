using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;
using Atlas_Strategy.Strategies;
using Xunit;

namespace Atlas_Tests;

public class Strategy_A_Trend_Pullback_Tests
{
    private readonly Strategy_A_Trend_Pullback _sut = new();

    private static Market_Context_BO Context(Market_Regime_Type regime, Trade_Direction_Type d1, Trade_Direction_Type h4, Trade_Direction_Type h1) => new()
    {
        Regime = regime,
        D1_Bias = d1,
        H4_Bias = h4,
        H1_Bias = h1
    };

    // Flat h1 @ 1.1000, half-range 0.0005 -> EMA20=EMA50=1.1000, ATR(14)=0.0010
    private static List<Candle_BO> Baseline_H1() => Test_Data_Factory.Flat_Series(60, 1.1000m, 0.0005m, "H1");

    [Fact]
    public async Task Buy_Signal_Generated_On_Bullish_Pullback_Rejection()
    {
        var context = Context(Market_Regime_Type.Trend_Swing, Trade_Direction_Type.Buy, Trade_Direction_Type.Buy, Trade_Direction_Type.Buy);
        var h1 = Baseline_H1();
        var m15 = new List<Candle_BO> { Test_Data_Factory.Candle(1.0980m, 1.0985m, 1.0975m, 1.0982m),
            Test_Data_Factory.Candle(1.0995m, 1.1010m, 1.0990m, 1.1005m) }; // last: bullish rejection off the EMA zone

        var signal = await _sut.Generate_Signal_Async("EURUSD", context, [], h1, m15);

        Assert.NotNull(signal);
        Assert.Equal(Trade_Direction_Type.Buy, signal!.Direction);
        Assert.Equal(1.0987m, signal.Stop_Loss_Price);
    }

    [Fact]
    public async Task Sell_Signal_Generated_On_Bearish_Pullback_Rejection()
    {
        var context = Context(Market_Regime_Type.Trend_Swing, Trade_Direction_Type.Sell, Trade_Direction_Type.Sell, Trade_Direction_Type.Sell);
        var h1 = Baseline_H1();
        var m15 = new List<Candle_BO> { Test_Data_Factory.Candle(1.1020m, 1.1025m, 1.1015m, 1.1018m),
            Test_Data_Factory.Candle(1.1005m, 1.1010m, 1.0993m, 1.0995m) }; // last: bearish rejection off the EMA zone

        var signal = await _sut.Generate_Signal_Async("EURUSD", context, [], h1, m15);

        Assert.NotNull(signal);
        Assert.Equal(Trade_Direction_Type.Sell, signal!.Direction);
        Assert.Equal(1.1013m, signal.Stop_Loss_Price);
    }

    [Fact]
    public async Task No_Signal_When_Regime_Is_Range()
    {
        var context = Context(Market_Regime_Type.Range, Trade_Direction_Type.Buy, Trade_Direction_Type.Buy, Trade_Direction_Type.Buy);
        var h1 = Baseline_H1();
        var m15 = new List<Candle_BO> { Test_Data_Factory.Candle(1.0980m, 1.0985m, 1.0975m, 1.0982m),
            Test_Data_Factory.Candle(1.0995m, 1.1010m, 1.0990m, 1.1005m) };

        var signal = await _sut.Generate_Signal_Async("EURUSD", context, [], h1, m15);

        Assert.Null(signal);
    }

    [Fact]
    public async Task No_Signal_When_Timeframes_Not_Aligned()
    {
        var context = Context(Market_Regime_Type.Trend_Swing, Trade_Direction_Type.Buy, Trade_Direction_Type.Buy, Trade_Direction_Type.Sell);
        var h1 = Baseline_H1();
        var m15 = new List<Candle_BO> { Test_Data_Factory.Candle(1.0980m, 1.0985m, 1.0975m, 1.0982m),
            Test_Data_Factory.Candle(1.0995m, 1.1010m, 1.0990m, 1.1005m) };

        var signal = await _sut.Generate_Signal_Async("EURUSD", context, [], h1, m15);

        Assert.Null(signal);
    }

    [Fact]
    public async Task No_Signal_When_Price_Has_Not_Pulled_Back_To_Ema_Zone()
    {
        var context = Context(Market_Regime_Type.Trend_Swing, Trade_Direction_Type.Buy, Trade_Direction_Type.Buy, Trade_Direction_Type.Buy);
        var h1 = Baseline_H1();
        var m15 = new List<Candle_BO> { Test_Data_Factory.Candle(1.1040m, 1.1045m, 1.1035m, 1.1042m),
            Test_Data_Factory.Candle(1.1050m, 1.1060m, 1.1050m, 1.1058m) }; // far above the EMA20/50 zone

        var signal = await _sut.Generate_Signal_Async("EURUSD", context, [], h1, m15);

        Assert.Null(signal);
    }

    [Fact]
    public async Task Buy_Signal_Stop_Loss_Is_Below_M15_Low_Minus_Atr_Buffer()
    {
        var context = Context(Market_Regime_Type.Trend_Swing, Trade_Direction_Type.Buy, Trade_Direction_Type.Buy, Trade_Direction_Type.Buy);
        var h1 = Baseline_H1();
        var m15 = new List<Candle_BO> { Test_Data_Factory.Candle(1.0980m, 1.0985m, 1.0975m, 1.0982m),
            Test_Data_Factory.Candle(1.0995m, 1.1010m, 1.0990m, 1.1005m) };

        var signal = await _sut.Generate_Signal_Async("EURUSD", context, [], h1, m15);

        Assert.NotNull(signal);
        Assert.True(signal!.Stop_Loss_Price < 1.0990m); // below the M15 low
    }

    [Fact]
    public async Task Buy_Signal_Reward_Risk_Ratio_Is_At_Least_2R()
    {
        var context = Context(Market_Regime_Type.Trend_Swing, Trade_Direction_Type.Buy, Trade_Direction_Type.Buy, Trade_Direction_Type.Buy);
        var h1 = Baseline_H1();
        var m15 = new List<Candle_BO> { Test_Data_Factory.Candle(1.0980m, 1.0985m, 1.0975m, 1.0982m),
            Test_Data_Factory.Candle(1.0995m, 1.1010m, 1.0990m, 1.1005m) };

        var signal = await _sut.Generate_Signal_Async("EURUSD", context, [], h1, m15);

        Assert.NotNull(signal);
        Assert.True(signal!.Reward_Risk_Ratio >= 2.0m);
    }
}

public class Strategy_B_Session_Breakout_Tests
{
    private readonly Strategy_B_Session_Breakout _sut = new();

    private static Market_Context_BO Context(Market_Regime_Type regime, Session_Type session) => new()
    {
        Regime = regime,
        Current_Session = session
    };

    // Flat h1 @ 1.2000, half-range 0.0010 -> ATR(14)=0.0020; last-8-bar Asian range = [1.1990, 1.2010]
    private static List<Candle_BO> Baseline_H1() => Test_Data_Factory.Flat_Series(30, 1.2000m, 0.0010m, "H1");

    [Fact]
    public async Task Bullish_Breakout_With_Retest_Generates_Buy_Signal()
    {
        var context = Context(Market_Regime_Type.Compression_Breakout, Session_Type.London);
        var h1 = Baseline_H1();
        var m15 = new List<Candle_BO> { Test_Data_Factory.Candle(1.2008m, 1.2025m, 1.2005m, 1.2020m) };

        var signal = await _sut.Generate_Signal_Async("EURUSD", context, [], h1, m15);

        Assert.NotNull(signal);
        Assert.Equal(Trade_Direction_Type.Buy, signal!.Direction);
    }

    [Fact]
    public async Task Bearish_Breakout_With_Retest_Generates_Sell_Signal()
    {
        var context = Context(Market_Regime_Type.Compression_Breakout, Session_Type.London_NY_Overlap);
        var h1 = Baseline_H1();
        var m15 = new List<Candle_BO> { Test_Data_Factory.Candle(1.1992m, 1.1995m, 1.1975m, 1.1980m) };

        var signal = await _sut.Generate_Signal_Async("EURUSD", context, [], h1, m15);

        Assert.NotNull(signal);
        Assert.Equal(Trade_Direction_Type.Sell, signal!.Direction);
    }

    [Fact]
    public async Task No_Signal_When_Asian_Session_Active()
    {
        var context = Context(Market_Regime_Type.Compression_Breakout, Session_Type.Asian);
        var h1 = Baseline_H1();
        var m15 = new List<Candle_BO> { Test_Data_Factory.Candle(1.2008m, 1.2025m, 1.2005m, 1.2020m) };

        var signal = await _sut.Generate_Signal_Async("EURUSD", context, [], h1, m15);

        Assert.Null(signal);
    }

    [Fact]
    public async Task No_Signal_When_Asian_Range_Is_Too_Wide_Relative_To_Atr()
    {
        var context = Context(Market_Regime_Type.Compression_Breakout, Session_Type.London);
        // First 22 bars set a tiny baseline ATR; the last 8 (the "Asian range") are blown wide open.
        var h1 = new List<Candle_BO>();
        h1.AddRange(Test_Data_Factory.Flat_Series(22, 1.2000m, 0.0005m, "H1"));
        h1.AddRange(Test_Data_Factory.Flat_Series(8, 1.2000m, 0.0500m, "H1", h1[^1].Open_Time_UTC.AddHours(1)));
        var m15 = new List<Candle_BO> { Test_Data_Factory.Candle(1.2008m, 1.2025m, 1.2005m, 1.2020m) };

        var signal = await _sut.Generate_Signal_Async("EURUSD", context, [], h1, m15);

        Assert.Null(signal);
    }

    [Fact]
    public async Task No_Signal_When_Asian_Range_Is_Too_Compressed_Relative_To_Atr()
    {
        var context = Context(Market_Regime_Type.Compression_Breakout, Session_Type.London);
        // First 22 bars set a wider baseline ATR; the last 8 (the "Asian range") collapse to almost nothing.
        var h1 = new List<Candle_BO>();
        h1.AddRange(Test_Data_Factory.Flat_Series(22, 1.2000m, 0.0010m, "H1"));
        h1.AddRange(Test_Data_Factory.Flat_Series(8, 1.2000m, 0.00001m, "H1", h1[^1].Open_Time_UTC.AddHours(1)));
        var m15 = new List<Candle_BO> { Test_Data_Factory.Candle(1.2000m, 1.2003m, 1.1999m, 1.2002m) };

        var signal = await _sut.Generate_Signal_Async("EURUSD", context, [], h1, m15);

        Assert.Null(signal);
    }
}

public class Strategy_C_Liquidity_Sweep_Tests
{
    private readonly Strategy_C_Liquidity_Sweep _sut = new();

    private static Market_Context_BO Context(Market_Regime_Type regime) => new() { Regime = regime };

    // Flat h1 @ 1.2000, half-range 0.0010 -> ATR(14)=0.0020; 20-bar range = [1.1990, 1.2010]
    private static List<Candle_BO> Baseline_H1() => Test_Data_Factory.Flat_Series(30, 1.2000m, 0.0010m, "H1");

    [Fact]
    public async Task Bullish_Sweep_Below_Range_Low_Generates_Buy_Signal()
    {
        var context = Context(Market_Regime_Type.Range);
        var h1 = Baseline_H1();
        var m15 = new List<Candle_BO>
        {
            Test_Data_Factory.Candle(1.1985m, 1.1990m, 1.1980m, 1.1988m), // prev: wicks below 1.1990
            Test_Data_Factory.Candle(1.1993m, 1.2002m, 1.1992m, 1.2000m)  // last: closes back above, bullish
        };

        var signal = await _sut.Generate_Signal_Async("EURUSD", context, [], h1, m15);

        Assert.NotNull(signal);
        Assert.Equal(Trade_Direction_Type.Buy, signal!.Direction);
    }

    [Fact]
    public async Task Bearish_Sweep_Above_Range_High_Generates_Sell_Signal()
    {
        var context = Context(Market_Regime_Type.Range);
        var h1 = Baseline_H1();
        var m15 = new List<Candle_BO>
        {
            Test_Data_Factory.Candle(1.2015m, 1.2020m, 1.2010m, 1.2012m), // prev: wicks above 1.2010
            Test_Data_Factory.Candle(1.2007m, 1.2008m, 1.1998m, 1.2000m)  // last: closes back below, bearish
        };

        var signal = await _sut.Generate_Signal_Async("EURUSD", context, [], h1, m15);

        Assert.NotNull(signal);
        Assert.Equal(Trade_Direction_Type.Sell, signal!.Direction);
    }

    [Fact]
    public async Task Gold_Bullish_Sweep_Requires_Close_Beyond_Previous_Open()
    {
        var context = Context(Market_Regime_Type.Range);
        var h1 = Baseline_H1();
        var m15 = new List<Candle_BO>
        {
            Test_Data_Factory.Candle(1.2005m, 1.1990m, 1.1980m, 1.1988m), // prev: Open above where last will close
            Test_Data_Factory.Candle(1.1993m, 1.2002m, 1.1992m, 1.2000m)  // last close (1.2000) <= prev Open (1.2005)
        };

        var signal = await _sut.Generate_Signal_Async("XAUUSD", context, [], h1, m15);

        Assert.Null(signal);
    }

    [Fact]
    public async Task No_Signal_When_Regime_Is_Trend_Swing()
    {
        var context = Context(Market_Regime_Type.Trend_Swing);
        var h1 = Baseline_H1();
        var m15 = new List<Candle_BO>
        {
            Test_Data_Factory.Candle(1.1985m, 1.1990m, 1.1980m, 1.1988m),
            Test_Data_Factory.Candle(1.1993m, 1.2002m, 1.1992m, 1.2000m)
        };

        var signal = await _sut.Generate_Signal_Async("EURUSD", context, [], h1, m15);

        Assert.Null(signal);
    }
}

public class Strategy_D_Breakout_Retest_Tests
{
    private readonly Strategy_D_Breakout_Retest _sut = new();

    private static Market_Context_BO Context(Market_Regime_Type regime) => new() { Regime = regime };

    // Flat h1 -> ATR(14) = 0.0020
    private static List<Candle_BO> Baseline_H1() => Test_Data_Factory.Flat_Series(20, 1.2000m, 0.0010m, "H1");

    // 5 H4 bars; index [^3] (Count-3 = index 2) is the structural level: High=1.2050, Low=1.2010
    private static List<Candle_BO> H4_With_Key_Level() => new()
    {
        Test_Data_Factory.Candle(1.2000m, 1.2010m, 1.1990m, 1.2000m, tf: "H4"),
        Test_Data_Factory.Candle(1.2000m, 1.2020m, 1.1990m, 1.2010m, tf: "H4"),
        Test_Data_Factory.Candle(1.2000m, 1.2050m, 1.2010m, 1.2030m, tf: "H4"), // key level bar ([^3])
        Test_Data_Factory.Candle(1.2030m, 1.2060m, 1.2025m, 1.2055m, tf: "H4"),
        Test_Data_Factory.Candle(1.2065m, 1.2075m, 1.2055m, 1.2070m, tf: "H4"),
    };

    [Fact]
    public async Task Bullish_Retest_Of_Broken_Resistance_Generates_Buy_Signal()
    {
        var context = Context(Market_Regime_Type.Trend_Swing);
        var h4 = H4_With_Key_Level(); // key_level_high = 1.2050
        var h1 = Baseline_H1();
        var m15 = new List<Candle_BO>
        {
            Test_Data_Factory.Candle(1.2040m, 1.2045m, 1.2035m, 1.2042m), // filler (only Count>=3 required)
            Test_Data_Factory.Candle(1.2055m, 1.2065m, 1.2052m, 1.2060m), // prev: closed above the level
            Test_Data_Factory.Candle(1.2053m, 1.2060m, 1.2055m, 1.2058m)  // last: pulled back, held, bullish close
        };

        var signal = await _sut.Generate_Signal_Async("EURUSD", context, h4, h1, m15);

        Assert.NotNull(signal);
        Assert.Equal(Trade_Direction_Type.Buy, signal!.Direction);
    }

    [Fact]
    public async Task Bearish_Retest_Of_Broken_Support_Generates_Sell_Signal()
    {
        var context = Context(Market_Regime_Type.Trend_Swing);
        var h4 = H4_With_Key_Level(); // key_level_low = 1.2010
        var h1 = Baseline_H1();
        var m15 = new List<Candle_BO>
        {
            Test_Data_Factory.Candle(1.2015m, 1.2020m, 1.2010m, 1.2012m), // filler (only Count>=3 required)
            Test_Data_Factory.Candle(1.2005m, 1.2008m, 1.1995m, 1.2000m), // prev: closed below the level
            Test_Data_Factory.Candle(1.2007m, 1.2005m, 1.2000m, 1.2002m)  // last: pulled back, held, bearish close
        };

        var signal = await _sut.Generate_Signal_Async("EURUSD", context, h4, h1, m15);

        Assert.NotNull(signal);
        Assert.Equal(Trade_Direction_Type.Sell, signal!.Direction);
    }

    [Fact]
    public async Task No_Signal_When_Price_Never_Retested_The_Level()
    {
        var context = Context(Market_Regime_Type.Trend_Swing);
        var h4 = H4_With_Key_Level();
        var h1 = Baseline_H1();
        var m15 = new List<Candle_BO>
        {
            Test_Data_Factory.Candle(1.2040m, 1.2045m, 1.2035m, 1.2042m), // filler (only Count>=3 required)
            Test_Data_Factory.Candle(1.2055m, 1.2065m, 1.2052m, 1.2060m), // prev: broke out above the level
            Test_Data_Factory.Candle(1.2095m, 1.2110m, 1.2090m, 1.2100m)  // last: ran away, never pulled back
        };

        var signal = await _sut.Generate_Signal_Async("EURUSD", context, h4, h1, m15);

        Assert.Null(signal);
    }
}

public class Strategy_E_Mean_Reversion_Tests
{
    private readonly Strategy_E_Mean_Reversion _sut = new();

    private static Market_Context_BO Not_Aligned_Range_Context() => new()
    {
        Regime = Market_Regime_Type.Range,
        D1_Bias = Trade_Direction_Type.Buy,
        H4_Bias = Trade_Direction_Type.Sell,
        H1_Bias = Trade_Direction_Type.Buy
    };

    // 20 bars, strictly falling closes 1.2000 -> 1.1810 => RSI(14) = 0, EMA(20) = 1.1905, ATR(14) = 0.0015
    private static List<Candle_BO> Oversold_H1()
    {
        var list = new List<Candle_BO>();
        var time = DateTime.UtcNow.AddHours(-20);
        for (int i = 0; i < 20; i++)
        {
            decimal close = 1.2000m - i * 0.0010m;
            list.Add(new Candle_BO { Timeframe = "H1", Open_Time_UTC = time.AddHours(i), Open = close, Close = close, High = close + 0.0005m, Low = close - 0.0005m, Volume = 100 });
        }
        return list;
    }

    // 20 bars, strictly rising closes 1.2000 -> 1.2190 => RSI(14) = 100, EMA(20) = 1.2095, ATR(14) = 0.0015
    private static List<Candle_BO> Overbought_H1()
    {
        var list = new List<Candle_BO>();
        var time = DateTime.UtcNow.AddHours(-20);
        for (int i = 0; i < 20; i++)
        {
            decimal close = 1.2000m + i * 0.0010m;
            list.Add(new Candle_BO { Timeframe = "H1", Open_Time_UTC = time.AddHours(i), Open = close, Close = close, High = close + 0.0005m, Low = close - 0.0005m, Volume = 100 });
        }
        return list;
    }

    [Fact]
    public async Task Oversold_Rsi_Below_30_Generates_Buy_Signal()
    {
        var context = Not_Aligned_Range_Context();
        var h1 = Oversold_H1();
        var m15 = new List<Candle_BO> { Test_Data_Factory.Candle(1.1840m, 1.1855m, 1.1830m, 1.1850m) };

        var signal = await _sut.Generate_Signal_Async("EURUSD", context, [], h1, m15);

        Assert.NotNull(signal);
        Assert.Equal(Trade_Direction_Type.Buy, signal!.Direction);
    }

    [Fact]
    public async Task Overbought_Rsi_Above_70_Generates_Sell_Signal()
    {
        var context = Not_Aligned_Range_Context();
        var h1 = Overbought_H1();
        var m15 = new List<Candle_BO> { Test_Data_Factory.Candle(1.2140m, 1.2150m, 1.2110m, 1.2130m) };

        var signal = await _sut.Generate_Signal_Async("EURUSD", context, [], h1, m15);

        Assert.NotNull(signal);
        Assert.Equal(Trade_Direction_Type.Sell, signal!.Direction);
    }

    [Fact]
    public async Task No_Signal_When_Higher_Timeframes_Are_Aligned()
    {
        var context = new Market_Context_BO
        {
            Regime = Market_Regime_Type.Range,
            D1_Bias = Trade_Direction_Type.Buy,
            H4_Bias = Trade_Direction_Type.Buy,
            H1_Bias = Trade_Direction_Type.Buy // fully aligned — contradicts mean reversion's premise
        };
        var h1 = Oversold_H1();
        var m15 = new List<Candle_BO> { Test_Data_Factory.Candle(1.1840m, 1.1855m, 1.1830m, 1.1850m) };

        var signal = await _sut.Generate_Signal_Async("EURUSD", context, [], h1, m15);

        Assert.Null(signal);
    }

    [Fact]
    public async Task No_Signal_When_Reward_Risk_Ratio_Is_Below_1point5()
    {
        var context = Not_Aligned_Range_Context();
        var h1 = Oversold_H1();
        // Entry sits too close to the EMA target relative to its stop distance -> R:R < 1.5
        var m15 = new List<Candle_BO> { Test_Data_Factory.Candle(1.1875m, 1.1890m, 1.1870m, 1.1885m) };

        var signal = await _sut.Generate_Signal_Async("EURUSD", context, [], h1, m15);

        Assert.Null(signal);
    }

    [Fact]
    public async Task No_Signal_When_Regime_Is_Not_Range()
    {
        var context = new Market_Context_BO
        {
            Regime = Market_Regime_Type.Trend_Swing,
            D1_Bias = Trade_Direction_Type.Buy,
            H4_Bias = Trade_Direction_Type.Sell,
            H1_Bias = Trade_Direction_Type.Buy
        };
        var h1 = Oversold_H1();
        var m15 = new List<Candle_BO> { Test_Data_Factory.Candle(1.1840m, 1.1855m, 1.1830m, 1.1850m) };

        var signal = await _sut.Generate_Signal_Async("EURUSD", context, [], h1, m15);

        Assert.Null(signal);
    }
}
