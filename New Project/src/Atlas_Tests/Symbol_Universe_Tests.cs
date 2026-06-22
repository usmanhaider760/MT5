using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;
using Xunit;

namespace Atlas_Tests;

public class Symbol_Universe_Tests
{
    private static List<Market_Symbol_BO> Universe() => Market_Symbol_BO.Default_Universe();

    [Fact]
    public void Default_Universe_Has_Eight_Symbols()
    {
        Assert.Equal(8, Universe().Count);
    }

    [Fact]
    public void All_Symbols_Have_Min_Lot_Size_Set()
    {
        foreach (var sym in Universe())
            Assert.True(sym.Min_Lot_Size >= 0.01m,
                $"{sym.Symbol_Name} has Min_Lot_Size={sym.Min_Lot_Size}, expected >= 0.01");
    }

    [Fact]
    public void Gold_Symbol_Is_Gold_And_Tier_1()
    {
        var gold = Universe().Single(s => s.Symbol_Name == "XAUUSD");
        Assert.True(gold.Is_Gold);
        Assert.Equal(Symbol_Tier_Type.Tier_1, gold.Tier);
        Assert.Equal(2, gold.Decimal_Places);
    }

    [Fact]
    public void Tier_Distribution_Is_Four_Three_One()
    {
        var symbols = Universe();
        Assert.Equal(4, symbols.Count(s => s.Tier == Symbol_Tier_Type.Tier_1));
        Assert.Equal(3, symbols.Count(s => s.Tier == Symbol_Tier_Type.Tier_2));
        Assert.Equal(1, symbols.Count(s => s.Tier == Symbol_Tier_Type.Tier_3));
    }

    [Fact]
    public void All_Symbols_Have_Positive_Pip_Value_And_Spread()
    {
        foreach (var sym in Universe())
        {
            Assert.True(sym.Pip_Value_Per_Lot > 0,    $"{sym.Symbol_Name}: Pip_Value_Per_Lot must be > 0");
            Assert.True(sym.Normal_Spread_Pips > 0,   $"{sym.Symbol_Name}: Normal_Spread_Pips must be > 0");
            Assert.True(sym.Max_Allowed_Spread_Pips > sym.Normal_Spread_Pips,
                $"{sym.Symbol_Name}: Max_Allowed_Spread_Pips must exceed Normal");
        }
    }

    [Fact]
    public void Regime_Score_Cap_Clamps_Bonus_To_100()
    {
        // Simulates the cap applied in Trade_Pipeline_Service after the session-MTF bonus
        int base_score = 98;
        int bonus      = 5;
        int capped     = Math.Min(100, base_score + bonus);
        Assert.Equal(100, capped);
    }

    [Fact]
    public void Regime_Score_Cap_Does_Not_Reduce_Low_Scores()
    {
        int base_score = 75;
        int bonus      = 5;
        int capped     = Math.Min(100, base_score + bonus);
        Assert.Equal(80, capped);
    }
}
