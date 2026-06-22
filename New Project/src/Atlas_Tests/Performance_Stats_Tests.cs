using Atlas_Application.Services;
using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;
using Xunit;

namespace Atlas_Tests;

public class Performance_Stats_Tests
{
    // Lot=0.1, SL=10 pips → denominator=10, so R_Multiple = Gross_PnL_Currency / 10
    private static Trade_Result_BO Result(decimal r_multiple) => new()
    {
        Strategy               = Strategy_Type.Trend_Pullback_Continuation,
        Symbol_Name            = "EURUSD",
        Direction              = Trade_Direction_Type.Buy,
        Lot_Size               = 0.1m,
        Initial_Stop_Distance_Pips = 10m,
        Gross_PnL_Currency     = r_multiple * 10m,  // Net_PnL = Gross (comm=swap=0)
        Closed_At_UTC          = DateTime.UtcNow
    };

    private static Performance_Monitor_Service Build(IEnumerable<decimal> r_multiples)
    {
        var sut = new Performance_Monitor_Service();
        foreach (var r in r_multiples)
            sut.Record_Trade_Result(Result(r));
        return sut;
    }

    [Fact]
    public void Sharpe_Ratio_Returns_Zero_With_Fewer_Than_Two_Trades()
    {
        var sut = Build([1.5m]);
        Assert.Equal(0, sut.Calculate_Sharpe_Ratio());
    }

    [Fact]
    public void Sharpe_Ratio_Is_Positive_For_Positive_Mean_Series()
    {
        // mean = 1.5, std dev > 0 → Sharpe > 0
        var sut = Build([2.0m, 2.0m, 1.0m, 1.0m]);
        Assert.True(sut.Calculate_Sharpe_Ratio() > 0);
    }

    [Fact]
    public void Sharpe_Ratio_Is_Negative_For_Negative_Mean_Series()
    {
        // All losses → mean < 0 → Sharpe < 0
        var sut = Build([-1.0m, -2.0m, -0.5m]);
        Assert.True(sut.Calculate_Sharpe_Ratio() < 0);
    }

    [Fact]
    public void Sortino_Ratio_Returns_99_When_All_Winners()
    {
        var sut = Build([2.0m, 1.5m, 1.0m]);
        Assert.Equal(99m, sut.Calculate_Sortino_Ratio());
    }

    [Fact]
    public void Sortino_Ratio_Is_Positive_For_Positive_Mean_Mixed_Series()
    {
        // Net positive with some losses
        var sut = Build([2.0m, 1.5m, -0.5m, 2.0m, -0.5m]);
        Assert.True(sut.Calculate_Sortino_Ratio() > 0);
    }

    [Fact]
    public void Equity_Series_Accumulates_Net_PnL_In_Order()
    {
        // R multiples: +1, +2, -0.5 → Net_PnL per trade: +10, +20, -5
        var sut = Build([1.0m, 2.0m, -0.5m]);
        var series = sut.Get_Equity_Series();

        Assert.Equal(3, series.Count);
        Assert.Equal(10m,  series[0]);
        Assert.Equal(30m,  series[1]);
        Assert.Equal(25m,  series[2]);
    }
}
