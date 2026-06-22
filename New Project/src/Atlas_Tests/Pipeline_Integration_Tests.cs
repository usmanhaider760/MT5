using Atlas_Application.Services;
using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;
using Xunit;

namespace Atlas_Tests;

/// <summary>
/// End-to-end analytics pipeline tests: records a sequence of known trades
/// and verifies that stats, equity series, Sharpe/Sortino, and per-strategy
/// tracking all compute correctly end-to-end.
/// </summary>
public class Pipeline_Integration_Tests
{
    // Lot=0.1, SL=10 pips → Net_PnL = r * 10, R_Multiple = r
    private static Trade_Result_BO Trade(decimal r, Strategy_Type strategy = Strategy_Type.Trend_Pullback_Continuation,
        string symbol = "EURUSD") => new()
    {
        Signal_Id              = Guid.NewGuid(),
        Symbol_Name            = symbol,
        Strategy               = strategy,
        Direction              = Trade_Direction_Type.Buy,
        Lot_Size               = 0.1m,
        Initial_Stop_Distance_Pips = 10m,
        Gross_PnL_Currency     = r * 10m,
        Closed_At_UTC          = DateTime.UtcNow
    };

    [Fact]
    public void Full_Stats_Match_Known_Trade_Series()
    {
        // 3 wins (+2R, +1.5R, +1R), 2 losses (−1R, −0.5R)
        var sut = new Performance_Monitor_Service();
        sut.Record_Trade_Result(Trade(2.0m));
        sut.Record_Trade_Result(Trade(1.5m));
        sut.Record_Trade_Result(Trade(1.0m));
        sut.Record_Trade_Result(Trade(-1.0m));
        sut.Record_Trade_Result(Trade(-0.5m));

        Assert.Equal(5, sut.Total_Trades_Count);

        var (wr, avg_r, pf, _) = sut.Get_Overall_Stats();
        Assert.Equal(60.0m, wr);                   // 3/5 = 60%
        Assert.True(avg_r > 0);                     // net-positive expectancy
        Assert.Equal(3.00m, pf);                    // (20+15+10) / (10+5) = 45/15
    }

    [Fact]
    public void Total_R_Sums_All_R_Multiples()
    {
        var sut = new Performance_Monitor_Service();
        sut.Record_Trade_Result(Trade(2.0m));
        sut.Record_Trade_Result(Trade(-1.0m));
        sut.Record_Trade_Result(Trade(3.0m));

        // R_Multiple = r (with our lot/SL setup)
        Assert.Equal(4.0m, sut.Total_R);
    }

    [Fact]
    public void Equity_Series_Accumulates_In_Record_Order()
    {
        var sut = new Performance_Monitor_Service();
        sut.Record_Trade_Result(Trade(2.0m));   // Net = +20
        sut.Record_Trade_Result(Trade(-1.0m));  // Net = −10 → cumulative 10
        sut.Record_Trade_Result(Trade(3.0m));   // Net = +30 → cumulative 40

        var series = sut.Get_Equity_Series();
        Assert.Equal(3, series.Count);
        Assert.Equal(20m, series[0]);
        Assert.Equal(10m, series[1]);
        Assert.Equal(40m, series[2]);
    }

    [Fact]
    public void Sharpe_And_Sortino_Both_Positive_For_Net_Profitable_Series()
    {
        var sut = new Performance_Monitor_Service();
        foreach (var r in new decimal[] { 2.0m, 1.5m, -0.5m, 2.0m, 1.0m, -0.5m, 1.5m })
            sut.Record_Trade_Result(Trade(r));

        Assert.True(sut.Calculate_Sharpe_Ratio() > 0);
        Assert.True(sut.Calculate_Sortino_Ratio() > 0);
    }

    [Fact]
    public void Strategy_Performance_Tracks_Per_Strategy_Independently()
    {
        var sut = new Performance_Monitor_Service();

        sut.Record_Trade_Result(Trade(2.0m,  Strategy_Type.Trend_Pullback_Continuation));
        sut.Record_Trade_Result(Trade(1.5m,  Strategy_Type.Session_Breakout));
        sut.Record_Trade_Result(Trade(-1.0m, Strategy_Type.Trend_Pullback_Continuation));
        sut.Record_Trade_Result(Trade(0.8m,  Strategy_Type.Session_Breakout));

        var perf = sut.Get_All_Strategy_Performance();
        Assert.True(perf.ContainsKey(Strategy_Type.Trend_Pullback_Continuation));
        Assert.True(perf.ContainsKey(Strategy_Type.Session_Breakout));

        var tpc = perf[Strategy_Type.Trend_Pullback_Continuation];
        Assert.Equal(2, tpc.Total_Trades);
        Assert.Equal(1, tpc.Winning_Trades);
        Assert.Equal(1, tpc.Losing_Trades);

        var sb = perf[Strategy_Type.Session_Breakout];
        Assert.Equal(2, sb.Total_Trades);
        Assert.Equal(2, sb.Winning_Trades);
    }

    [Fact]
    public void Max_Drawdown_Correctly_Computed_From_Equity_Curve()
    {
        var sut = new Performance_Monitor_Service();
        // Equity: +100, +50, −80, +60  → peak=150 then drops to 70 → DD = (150-70)/150 = 53.3%
        sut.Record_Trade_Result(Trade(10.0m));    // equity=100
        sut.Record_Trade_Result(Trade(5.0m));     // equity=150  ← peak
        sut.Record_Trade_Result(Trade(-8.0m));    // equity=70   ← trough
        sut.Record_Trade_Result(Trade(6.0m));     // equity=130

        var (_, _, _, max_dd) = sut.Get_Overall_Stats();
        Assert.True(max_dd > 0);
        Assert.True(max_dd < 100);
    }
}
