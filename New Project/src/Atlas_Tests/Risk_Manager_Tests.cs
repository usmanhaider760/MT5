using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;
using Atlas_Risk.Services;
using Xunit;

namespace Atlas_Tests;

public class Risk_Manager_Tests
{
    private readonly Risk_Manager _sut = new(Risk_Setting_BO.Conservative_Launch());

    private static Trade_Signal_BO Sample_Signal() => new()
    {
        Symbol_Name = "EURUSD",
        Direction = Trade_Direction_Type.Buy,
        Entry_Price = 1.1000m,
        Stop_Loss_Price = 1.0980m,
        Take_Profit_Price = 1.1050m
    };

    [Theory]
    [InlineData(10_000, 1.0, 20, 10, 0.50)]   // $10k × 1% / (20 pips × $10) = $100 / $200 = 0.50
    [InlineData(50_000, 0.5, 50, 10, 0.50)]   // $50k × 0.5% / (50 × $10) = $250 / $500 = 0.50
    [InlineData(10_000, 2.0, 10,  5, 4.00)]   // $10k × 2% / (10 × $5) = $200 / $50 = 4.00
    [InlineData(20_000, 1.5, 15, 10, 2.00)]   // $20k × 1.5% / (15 × $10) = $300 / $150 = 2.00
    public void Calculate_Lot_Size_Returns_Correct_Value(
        decimal equity, decimal risk_pct, decimal sl_pips, decimal pip_value, decimal expected)
    {
        var lot = _sut.Calculate_Lot_Size(equity, risk_pct, sl_pips, pip_value);
        Assert.Equal(expected, lot);
    }

    [Fact]
    public void Calculate_Lot_Size_Returns_Zero_When_Stop_Is_Zero()
    {
        var lot = _sut.Calculate_Lot_Size(10_000, 1.0m, 0, 10);
        Assert.Equal(0, lot);
    }

    [Fact]
    public void Calculate_Lot_Size_Returns_Zero_When_Pip_Value_Is_Zero()
    {
        var lot = _sut.Calculate_Lot_Size(10_000, 1.0m, 20, 0);
        Assert.Equal(0, lot);
    }

    [Fact]
    public void Calculate_Lot_Size_Floors_Not_Rounds()
    {
        // $10k × 1% / (33 pips × $10) = $100 / $330 ≈ 0.30303… → floors to 0.30
        var lot = _sut.Calculate_Lot_Size(10_000, 1.0m, 33, 10);
        Assert.Equal(0.30m, lot);
    }

    [Fact]
    public void Calculate_Lot_Size_Floors_Instead_Of_Rounding_Up_At_The_Third_Decimal()
    {
        // $12,700 × 1% / (100 × $10) = $127 / $1,000 = 0.127 — Math.Round would give 0.13, floor must give 0.12
        var lot = _sut.Calculate_Lot_Size(12_700, 1.0m, 100, 10);
        Assert.Equal(0.12m, lot);
    }

    [Fact]
    public void Calculate_Lot_Size_Never_Overexposes_By_Rounding_Up()
    {
        // $12,900 × 1% / (100 × $10) = $129 / $1,000 = 0.129 — Math.Round would give 0.13, floor must give 0.12
        var lot = _sut.Calculate_Lot_Size(12_900, 1.0m, 100, 10);
        Assert.Equal(0.12m, lot);
    }

    [Fact]
    public void Calculate_Lot_Size_Applies_Broker_Lot_Step()
    {
        // $12,700 × 1% / (100 × $10) = 0.127 raw — with a 0.10 lot step, floors to 0.10 not 0.12
        var lot = _sut.Calculate_Lot_Size(12_700, 1.0m, 100, 10, lot_step: 0.10m);
        Assert.Equal(0.10m, lot);
    }

    [Fact]
    public void Calculate_Lot_Size_Applies_Broker_Lot_Step_At_Coarser_Increment()
    {
        // $8,700 × 1% / (100 × $10) = 0.087 raw — with a 0.10 lot step, floors to 0.00 (below one step)
        var lot = _sut.Calculate_Lot_Size(8_700, 1.0m, 100, 10, lot_step: 0.10m);
        Assert.Equal(0.00m, lot);
    }

    [Fact]
    public void Calculate_Lot_Size_Defaults_To_001_Lot_Step_When_Not_Specified()
    {
        var lot = _sut.Calculate_Lot_Size(12_700, 1.0m, 100, 10);
        Assert.Equal(0.12m, lot);
    }

    [Fact]
    public void Calculate_Lot_Size_Is_Capped_At_Max_Lot_On_A_Very_Large_Account()
    {
        // $1,000,000 × 1% / (10 pips × $10) = $10,000 / $100 = 100.0 raw lots — must be capped at 5.0
        var lot = _sut.Calculate_Lot_Size(1_000_000, 1.0m, 10, 10, lot_step: 0.01m, max_lot: 5.0m);
        Assert.Equal(5.0m, lot);
    }

    [Fact]
    public void R_Multiple_Correct_For_Gold_With_Its_100_Dollar_Pip_Value()
    {
        // SL=10 pips, lot=0.1, gross_pnl=+$100, pip_value=100 (Gold) → R = +1.000
        var trade = new Trade_Result_BO
        {
            Lot_Size = 0.1m,
            Gross_PnL_Currency = 100m,
            Initial_Stop_Distance_Pips = 10m,
            Pip_Value_Per_Lot = 100m
        };
        Assert.Equal(1.000m, trade.R_Multiple);
    }

    [Fact]
    public void R_Multiple_Correct_For_Eurusd_With_Standard_10_Dollar_Pip_Value()
    {
        // SL=20 pips, lot=0.5, gross_pnl=+$100, pip_value=10 (EURUSD) → R = +1.000
        var trade = new Trade_Result_BO
        {
            Lot_Size = 0.5m,
            Gross_PnL_Currency = 100m,
            Initial_Stop_Distance_Pips = 20m,
            Pip_Value_Per_Lot = 10m
        };
        Assert.Equal(1.000m, trade.R_Multiple);
    }

    [Fact]
    public async Task Validate_Trade_Risk_Rejects_When_Daily_Loss_Exceeds_Max()
    {
        // Conservative_Launch: Max_Daily_Loss_Percent = 0.75%; account is down 1% today
        var account = new Account_State_BO
        {
            Day_Open_Balance = 10_000,
            Week_Open_Balance = 10_000,
            Peak_Equity = 10_000,
            Equity = 9_900
        };

        var (approved, reject_reason, detail) = await _sut.Validate_Trade_Risk_Async(Sample_Signal(), account, new List<Position_BO>());

        Assert.False(approved);
        Assert.Equal(Signal_Reject_Reason.Daily_Loss_Limit_Reached, reject_reason);
        Assert.Contains("Daily loss", detail);
    }

    [Fact]
    public async Task Validate_Trade_Risk_Rejects_When_Drawdown_Hits_Circuit_Breaker()
    {
        // Conservative_Launch: Account_Drawdown_Circuit_Breaker_Percent = 5.0%; peak-to-equity drawdown is 5%
        var account = new Account_State_BO
        {
            Day_Open_Balance = 9_500,
            Week_Open_Balance = 9_500,
            Peak_Equity = 10_000,
            Equity = 9_500
        };

        var (approved, reject_reason, detail) = await _sut.Validate_Trade_Risk_Async(Sample_Signal(), account, new List<Position_BO>());

        Assert.False(approved);
        Assert.Equal(Signal_Reject_Reason.Drawdown_Limit_Reached, reject_reason);
        Assert.Contains("Drawdown", detail);
    }

    [Fact]
    public async Task Validate_Trade_Risk_Rejects_With_Max_Open_Trades_Reason_Not_Daily_Loss()
    {
        // Regression for P2-1: every risk failure used to be mislabeled Daily_Loss_Limit_Reached
        var account = new Account_State_BO
        {
            Day_Open_Balance = 10_000,
            Week_Open_Balance = 10_000,
            Peak_Equity = 10_000,
            Equity = 10_000
        };
        var open_positions = new List<Position_BO>
        {
            new() { Symbol_Name = "GBPUSD" },
            new() { Symbol_Name = "USDJPY" }
        }; // Conservative_Launch: Max_Open_Trades = 2

        var (approved, reject_reason, _) = await _sut.Validate_Trade_Risk_Async(Sample_Signal(), account, open_positions);

        Assert.False(approved);
        Assert.Equal(Signal_Reject_Reason.Max_Open_Trades_Reached, reject_reason);
    }

    [Fact]
    public async Task Validate_Trade_Risk_Rejects_With_Same_Symbol_Reason()
    {
        var account = new Account_State_BO
        {
            Day_Open_Balance = 10_000,
            Week_Open_Balance = 10_000,
            Peak_Equity = 10_000,
            Equity = 10_000
        };
        var open_positions = new List<Position_BO> { new() { Symbol_Name = "EURUSD" } };

        var (approved, reject_reason, _) = await _sut.Validate_Trade_Risk_Async(Sample_Signal(), account, open_positions);

        Assert.False(approved);
        Assert.Equal(Signal_Reject_Reason.Same_Symbol_Already_Open, reject_reason);
    }
}
