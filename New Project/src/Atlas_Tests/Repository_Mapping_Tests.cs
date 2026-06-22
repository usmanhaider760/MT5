using Atlas_Application.Services;
using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;
using Xunit;

namespace Atlas_Tests;

/// <summary>
/// Verifies Map_Result back-calculation logic and CSV export correctness.
/// Tests run against pure logic (no SQLite required).
/// </summary>
public class Repository_Mapping_Tests
{
    // Mirrors the back-calculation in Trade_Result_Repository.Map_Result
    private static decimal Calc_Sl_Pips(decimal net_pnl, decimal r_stored, decimal lot) =>
        r_stored != 0 && lot != 0
            ? Math.Abs(net_pnl) / (Math.Abs(r_stored) * lot * 10m)
            : 0m;

    [Fact]
    public void Back_Calc_Sl_Pips_Correct_For_Winner()
    {
        // 0.1 lots, 10-pip SL, +2R winner → net_pnl = 2 × 10 × 0.1 × 10 = $20
        decimal sl = Calc_Sl_Pips(net_pnl: 20m, r_stored: 2.0m, lot: 0.1m);
        Assert.Equal(10m, sl);
    }

    [Fact]
    public void Back_Calc_Sl_Pips_Correct_For_Loser()
    {
        // 0.1 lots, 10-pip SL, −1R loss → net_pnl = −$10
        decimal sl = Calc_Sl_Pips(net_pnl: -10m, r_stored: -1.0m, lot: 0.1m);
        Assert.Equal(10m, sl);
    }

    [Fact]
    public void Back_Calc_Returns_Zero_When_R_Is_Zero()
    {
        decimal sl = Calc_Sl_Pips(net_pnl: 0m, r_stored: 0m, lot: 0.1m);
        Assert.Equal(0m, sl);
    }

    [Fact]
    public void Trade_Result_R_Multiple_Correct_After_Sl_Restoration()
    {
        // Simulates what happens after Map_Result sets Initial_Stop_Distance_Pips from back-calc
        var trade = new Trade_Result_BO
        {
            Lot_Size                   = 0.1m,
            Gross_PnL_Currency         = 20m,
            Commission                 = 0m,
            Swap                       = 0m,
            Initial_Stop_Distance_Pips = 10m   // back-calculated from stored R=2 and net_pnl=20
        };
        Assert.Equal(2.0m, trade.R_Multiple);
    }

    [Fact]
    public void Trade_Result_Is_Winner_Correct_From_Gross_Pnl()
    {
        var win  = new Trade_Result_BO { Gross_PnL_Currency =  10m };
        var loss = new Trade_Result_BO { Gross_PnL_Currency = -10m };
        Assert.True(win.Is_Winner);
        Assert.False(loss.Is_Winner);
    }

    [Fact]
    public void Csv_Header_Contains_Notes_Column()
    {
        string csv = Daily_Report_Service.To_Csv([]);
        Assert.StartsWith("Closed_UTC", csv);
        Assert.Contains(",Notes", csv);
    }

    [Fact]
    public void Csv_Row_Exports_Trade_Notes()
    {
        var trade = new Trade_Result_BO
        {
            Symbol_Name        = "EURUSD",
            Strategy           = Strategy_Type.Trend_Pullback_Continuation,
            Direction          = Trade_Direction_Type.Buy,
            Lot_Size           = 0.1m,
            Gross_PnL_Currency = 20m,
            Closed_At_UTC      = new DateTime(2026, 6, 22, 14, 0, 0, DateTimeKind.Utc),
            Notes              = "Strong breakout — kept full size"
        };
        string csv = Daily_Report_Service.To_Csv([trade]);
        Assert.Contains("Strong breakout", csv);
    }

    [Fact]
    public void Csv_Escapes_Notes_With_Commas()
    {
        var trade = new Trade_Result_BO
        {
            Symbol_Name        = "EURUSD",
            Gross_PnL_Currency = 20m,
            Closed_At_UTC      = DateTime.UtcNow,
            Notes              = "Entry good, exit early"
        };
        string csv = Daily_Report_Service.To_Csv([trade]);
        Assert.Contains("\"Entry good, exit early\"", csv);
    }
}
