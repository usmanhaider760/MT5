using Atlas_Application.Backtest;
using Atlas_Domain.Enums;
using Xunit;

namespace Atlas_Tests;

/// <summary>
/// Tests for Backtest_Report text formatting and Monte Carlo section generation.
/// </summary>
public class Backtest_Report_Tests
{
    private static Backtest_Result Make_Result(int total = 40, bool passing = true)
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end   = start.AddDays(30);

        return new Backtest_Result
        {
            Symbol_Name          = "EURUSD",
            Run_Start            = start,
            Run_End              = start.AddSeconds(12),
            Total_Trades         = total,
            Winning_Trades       = passing ? (int)(total * 0.55) : (int)(total * 0.3),
            Losing_Trades        = passing ? (int)(total * 0.45) : (int)(total * 0.7),
            Gross_Profit_R       = passing ? total * 0.55m * 1.5m : total * 0.3m * 0.8m,
            Gross_Loss_R         = passing ? -(total * 0.45m) : -(total * 0.7m),
            Max_Drawdown_Percent = passing ? 5.2m : 18.0m,
            Final_Equity         = passing ? 11_200m : 9_100m,
            Total_Return_Percent = passing ? 12.0m : -9.0m,
            Total_Commission     = total * 0.7m,
            Total_Slippage_Pips  = total * 0.3m,
            Signals_Rejected     = 12,
            Filters_Blocked      = 8,
            Equity_Curve         = [(start, 10_000m), (end, passing ? 11_200m : 9_100m)]
        };
    }

    // ── Generate_Text ─────────────────────────────────────────────

    [Fact]
    public void Report_Contains_Symbol_Name()
    {
        string text = Backtest_Report.Generate_Text(Make_Result());
        Assert.Contains("EURUSD", text);
    }

    [Fact]
    public void Report_Contains_Period_Dates()
    {
        string text = Backtest_Report.Generate_Text(Make_Result());
        Assert.Contains("2026-01-01", text);
        Assert.Contains("2026-01-31", text);
    }

    [Fact]
    public void Report_Contains_Trade_Counts()
    {
        var result = Make_Result(total: 40);
        string text = Backtest_Report.Generate_Text(result);
        Assert.Contains("40", text);
    }

    [Fact]
    public void Report_Shows_Passed_Validation_When_Criteria_Met()
    {
        string text = Backtest_Report.Generate_Text(Make_Result(total: 40, passing: true));
        Assert.Contains("PASSED", text);
    }

    [Fact]
    public void Report_Shows_Failed_Validation_When_Criteria_Not_Met()
    {
        string text = Backtest_Report.Generate_Text(Make_Result(total: 10, passing: false));
        Assert.Contains("FAILED", text);
    }

    [Fact]
    public void Report_Contains_Financial_Stats()
    {
        string text = Backtest_Report.Generate_Text(Make_Result());
        Assert.Contains("Profit Factor",   text);
        Assert.Contains("Max Drawdown",    text);
        Assert.Contains("Total Return",    text);
        Assert.Contains("Final Equity",    text);
        Assert.Contains("Total Commission",text);
    }

    [Fact]
    public void Report_Includes_Strategy_Stats_When_Present()
    {
        var result = Make_Result();
        result.Strategy_Stats[Strategy_Type.Trend_Pullback_Continuation] =
            new Strategy_Backtest_Stats
            {
                Strategy     = Strategy_Type.Trend_Pullback_Continuation,
                Total_Trades = 20,
                Win_Rate     = 55.0m,
                Profit_Factor= 1.45m,
                Avg_R        = 0.22m
            };

        string text = Backtest_Report.Generate_Text(result);
        Assert.Contains("BY STRATEGY", text);
        Assert.Contains("Trend_Pullback_Continuation", text);
    }

    [Fact]
    public void Report_Includes_Walk_Forward_When_Both_Summaries_Present()
    {
        var result = Make_Result();
        result.In_Sample = new Backtest_Summary
        {
            Label        = "In-Sample",
            Total_Trades = 28,
            Win_Rate     = 55m,
            Profit_Factor= 1.4m,
            Avg_R        = 0.21m,
            Passed       = true,
            Period_Start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Period_End   = new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc)
        };
        result.Out_Sample = new Backtest_Summary
        {
            Label        = "Out-of-Sample",
            Total_Trades = 12,
            Win_Rate     = 50m,
            Profit_Factor= 1.2m,
            Avg_R        = 0.15m,
            Passed       = true,
            Period_Start = new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc),
            Period_End   = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc)
        };

        string text = Backtest_Report.Generate_Text(result);
        Assert.Contains("WALK-FORWARD", text);
        Assert.Contains("In-Sample",    text);
        Assert.Contains("Out-of-Sample",text);
    }

    // ── Generate_Monte_Carlo_Section ──────────────────────────────

    [Fact]
    public void Monte_Carlo_Section_Contains_Iteration_Count()
    {
        var mc = new Monte_Carlo_Tester(seed: 1)
            .Run(Enumerable.Repeat(1.0m, 30).ToList(), iterations: 500);
        string text = Backtest_Report.Generate_Monte_Carlo_Section(mc);

        Assert.Contains("500", text);
        Assert.Contains("30",  text);
    }

    [Fact]
    public void Monte_Carlo_Section_Shows_Passed_For_Strong_System()
    {
        // All winners → passes every stress criterion
        var mc = new Monte_Carlo_Tester(seed: 42)
            .Run(Enumerable.Repeat(1.5m, 50).ToList(), iterations: 200);
        string text = Backtest_Report.Generate_Monte_Carlo_Section(mc);

        Assert.Contains("PASSED", text);
    }

    [Fact]
    public void Monte_Carlo_Section_Shows_Failed_For_Weak_System()
    {
        // All losers → fails stress criteria
        var mc = new Monte_Carlo_Tester(seed: 42)
            .Run(Enumerable.Repeat(-2.0m, 30).ToList(), iterations: 200);
        string text = Backtest_Report.Generate_Monte_Carlo_Section(mc);

        Assert.Contains("FAILED", text);
    }

    [Fact]
    public void Monte_Carlo_Section_Contains_Ruin_Probability()
    {
        var mc = new Monte_Carlo_Tester(seed: 1)
            .Run(Enumerable.Repeat(1.0m, 20).ToList(), iterations: 100);
        string text = Backtest_Report.Generate_Monte_Carlo_Section(mc);

        Assert.Contains("Ruin", text);
        Assert.Contains("%", text);
    }
}
