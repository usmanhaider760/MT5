using Atlas_Domain.BusinessObjects;
using Atlas_Risk.Services;
using Xunit;

namespace Atlas_Tests;

public class Risk_Manager_Tests
{
    private readonly Risk_Manager _sut = new(Risk_Setting_BO.Conservative_Launch());

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
    public void Calculate_Lot_Size_Rounds_To_Two_Decimal_Places()
    {
        // $10k × 1% / (33 pips × $10) = $100 / $330 ≈ 0.303… → 0.30
        var lot = _sut.Calculate_Lot_Size(10_000, 1.0m, 33, 10);
        Assert.Equal(0.30m, lot);
    }
}
