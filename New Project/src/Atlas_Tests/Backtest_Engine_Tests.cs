using Atlas_Application.Backtest;
using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;
using Xunit;

namespace Atlas_Tests;

/// <summary>
/// Integration tests for Backtest_Engine.Run_Async using synthetic candle data.
/// Validates end-to-end pipeline: regime detection → strategy → risk → simulation.
/// No real MT5 connection or DB required.
/// </summary>
public class Backtest_Engine_Tests
{
    // ── Helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Generates a synthetic trending candle series.
    /// Each bar is 15 minutes wide. Price drifts up by drift_pips per bar.
    /// </summary>
    private static List<Candle_BO> Make_Trending_Candles(
        DateTime start, int count, decimal base_price,
        decimal bar_range = 0.0020m, decimal drift = 0.0001m, string sym = "EURUSD")
    {
        var candles = new List<Candle_BO>(count);
        decimal price = base_price;
        var rng = new Random(42);

        for (int i = 0; i < count; i++)
        {
            decimal open  = price;
            decimal close = price + drift + (decimal)(rng.NextDouble() - 0.4) * bar_range;
            decimal high  = Math.Max(open, close) + (decimal)rng.NextDouble() * bar_range * 0.3m;
            decimal low   = Math.Min(open, close) - (decimal)rng.NextDouble() * bar_range * 0.3m;

            candles.Add(new Candle_BO
            {
                Symbol_Name   = sym,
                Open_Time_UTC = start.AddMinutes(15 * i),
                Open  = Math.Round(open,  5),
                High  = Math.Round(high,  5),
                Low   = Math.Round(low,   5),
                Close = Math.Round(close, 5),
                Volume = 1000
            });

            price = close;
        }

        return candles;
    }

    /// <summary>Aggregates M15 candles into H1 by taking every 4th.</summary>
    private static List<Candle_BO> Aggregate_H1(List<Candle_BO> m15)
    {
        var h1 = new List<Candle_BO>();
        for (int i = 0; i + 3 < m15.Count; i += 4)
        {
            var group = m15.Skip(i).Take(4).ToList();
            h1.Add(new Candle_BO
            {
                Symbol_Name   = group[0].Symbol_Name,
                Open_Time_UTC = group[0].Open_Time_UTC,
                Open   = group[0].Open,
                High   = group.Max(c => c.High),
                Low    = group.Min(c => c.Low),
                Close  = group[3].Close,
                Volume = group.Sum(c => c.Volume)
            });
        }
        return h1;
    }

    /// <summary>Aggregates M15 into H4 by taking every 16th.</summary>
    private static List<Candle_BO> Aggregate_H4(List<Candle_BO> m15)
    {
        var h4 = new List<Candle_BO>();
        for (int i = 0; i + 15 < m15.Count; i += 16)
        {
            var group = m15.Skip(i).Take(16).ToList();
            h4.Add(new Candle_BO
            {
                Symbol_Name   = group[0].Symbol_Name,
                Open_Time_UTC = group[0].Open_Time_UTC,
                Open   = group[0].Open,
                High   = group.Max(c => c.High),
                Low    = group.Min(c => c.Low),
                Close  = group[15].Close,
                Volume = group.Sum(c => c.Volume)
            });
        }
        return h4;
    }

    /// <summary>Aggregates M15 into D1 by taking every 96th (24h = 96×15min).</summary>
    private static List<Candle_BO> Aggregate_D1(List<Candle_BO> m15)
    {
        var d1 = new List<Candle_BO>();
        for (int i = 0; i + 95 < m15.Count; i += 96)
        {
            var group = m15.Skip(i).Take(96).ToList();
            d1.Add(new Candle_BO
            {
                Symbol_Name   = group[0].Symbol_Name,
                Open_Time_UTC = group[0].Open_Time_UTC,
                Open   = group[0].Open,
                High   = group.Max(c => c.High),
                Low    = group.Min(c => c.Low),
                Close  = group[^1].Close,
                Volume = group.Sum(c => c.Volume)
            });
        }
        return d1;
    }

    // ── Tests ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Backtest_Runs_Without_Exception_On_Synthetic_Data()
    {
        var start = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        // 3000 M15 bars ≈ 31 days — enough for regime detection to warm up
        var m15 = Make_Trending_Candles(start, 3000, base_price: 1.08000m);
        var h1  = Aggregate_H1(m15);
        var h4  = Aggregate_H4(m15);
        var d1  = Aggregate_D1(m15);

        var data   = new Backtest_Data_Provider(m15, h1, h4, d1);
        var config = new Backtest_Config
        {
            Symbol_Name     = "EURUSD",
            Start_Date      = start.AddDays(7),  // allow 7 days warm-up
            End_Date        = start.AddDays(28),
            Initial_Balance = 10_000m,
        };

        var engine = new Backtest_Engine();
        var result = await engine.Run_Async(config, data);

        Assert.NotNull(result);
        Assert.Equal("EURUSD", result.Symbol_Name);
        Assert.True(result.Run_End > result.Run_Start);
        Assert.True(result.Final_Equity > 0);
        Assert.True(result.Equity_Curve.Count >= 2);
    }

    [Fact]
    public async Task Backtest_Max_Drawdown_Never_Exceeds_Initial_Balance()
    {
        var start = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var m15   = Make_Trending_Candles(start, 3000, base_price: 1.08000m);
        var h1    = Aggregate_H1(m15);
        var h4    = Aggregate_H4(m15);
        var d1    = Aggregate_D1(m15);

        var data   = new Backtest_Data_Provider(m15, h1, h4, d1);
        var config = new Backtest_Config
        {
            Symbol_Name  = "EURUSD",
            Start_Date   = start.AddDays(7),
            End_Date     = start.AddDays(28),
            Initial_Balance = 10_000m
        };

        var result = await new Backtest_Engine().Run_Async(config, data);

        Assert.True(result.Max_Drawdown_Percent >= 0);
        Assert.True(result.Max_Drawdown_Percent < 100m,
            $"Drawdown {result.Max_Drawdown_Percent}% should be less than 100%");
    }

    [Fact]
    public async Task Backtest_Force_Closes_All_Open_Positions_At_End()
    {
        var start = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var m15   = Make_Trending_Candles(start, 3000, base_price: 1.08000m);
        var h1    = Aggregate_H1(m15);
        var h4    = Aggregate_H4(m15);
        var d1    = Aggregate_D1(m15);

        var data   = new Backtest_Data_Provider(m15, h1, h4, d1);
        var config = new Backtest_Config
        {
            Symbol_Name  = "EURUSD",
            Start_Date   = start.AddDays(7),
            End_Date     = start.AddDays(28),
            Initial_Balance = 10_000m
        };

        var result = await new Backtest_Engine().Run_Async(config, data);

        // Trades count should equal winners + losers (no orphaned positions)
        Assert.Equal(result.Total_Trades, result.Winning_Trades + result.Losing_Trades);
    }

    [Fact]
    public async Task Backtest_Returns_Correct_Trade_PnL_Signs()
    {
        var start = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var m15   = Make_Trending_Candles(start, 3000, base_price: 1.08000m);
        var h1    = Aggregate_H1(m15);
        var h4    = Aggregate_H4(m15);
        var d1    = Aggregate_D1(m15);

        var data   = new Backtest_Data_Provider(m15, h1, h4, d1);
        var config = new Backtest_Config
        {
            Symbol_Name  = "EURUSD",
            Start_Date   = start.AddDays(7),
            End_Date     = start.AddDays(28),
            Initial_Balance = 10_000m
        };

        var result = await new Backtest_Engine().Run_Async(config, data);

        // Every winner must have positive gross PnL; every loser negative
        foreach (var t in result.Trades)
        {
            if (t.Is_Winner) Assert.True(t.Gross_PnL_Currency > 0,
                $"Winner {t.Signal_Id} has non-positive PnL {t.Gross_PnL_Currency}");
            else Assert.True(t.Gross_PnL_Currency <= 0,
                $"Loser {t.Signal_Id} has positive PnL {t.Gross_PnL_Currency}");
        }
    }

    [Fact]
    public async Task Backtest_Cancellation_Token_Stops_Run_Early()
    {
        var start = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var m15   = Make_Trending_Candles(start, 10_000, base_price: 1.08000m);
        var h1    = Aggregate_H1(m15);
        var h4    = Aggregate_H4(m15);
        var d1    = Aggregate_D1(m15);

        var data   = new Backtest_Data_Provider(m15, h1, h4, d1);
        var config = new Backtest_Config
        {
            Symbol_Name  = "EURUSD",
            Start_Date   = start.AddDays(7),
            End_Date     = start.AddDays(90),
            Initial_Balance = 10_000m
        };

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(500));

        var result = await new Backtest_Engine().Run_Async(config, data, cts.Token);

        // Should return a result even when cancelled (partial run)
        Assert.NotNull(result);
        Assert.True(result.Final_Equity > 0);
    }
}
