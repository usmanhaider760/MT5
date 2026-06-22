using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;
using System.Text.Json;
using Xunit;

namespace Atlas_Tests;

/// <summary>
/// Tests for strategy state JSON round-trip and live position R-Multiple from PnL.
/// </summary>
public class Strategy_State_And_Position_R_Tests
{
    // ── Strategy state persistence ────────────────────────────────────

    [Fact]
    public void Strategy_State_Serializes_And_Deserializes_Correctly()
    {
        var state = new Dictionary<string, bool>
        {
            [Strategy_Type.Trend_Pullback_Continuation.ToString()] = true,
            [Strategy_Type.Session_Breakout.ToString()]            = true,
            [Strategy_Type.Liquidity_Sweep_Reversal.ToString()]    = false,
            [Strategy_Type.Breakout_Retest_Expansion.ToString()]   = false,
            [Strategy_Type.Controlled_Mean_Reversion.ToString()]   = false,
        };

        string json      = JsonSerializer.Serialize(state);
        var    restored  = JsonSerializer.Deserialize<Dictionary<string, bool>>(json);

        Assert.NotNull(restored);
        Assert.Equal(5, restored.Count);
        Assert.True(restored[Strategy_Type.Trend_Pullback_Continuation.ToString()]);
        Assert.False(restored[Strategy_Type.Controlled_Mean_Reversion.ToString()]);
    }

    [Fact]
    public void Strategy_State_Unknown_Keys_Are_Ignored()
    {
        string json = """{"Trend_Pullback_Continuation":true,"UnknownStrategy":false}""";
        var restored = JsonSerializer.Deserialize<Dictionary<string, bool>>(json)!;

        int applied = 0;
        foreach (var kv in restored)
        {
            if (Enum.TryParse<Strategy_Type>(kv.Key, out _))
                applied++;
        }
        Assert.Equal(1, applied);
    }

    // ── Position R from PnL ──────────────────────────────────────────

    private static decimal Calc_R_From_Pnl(decimal unrealized_pnl, decimal sl_pips, decimal lot, decimal pip_val)
        => sl_pips > 0 && lot > 0
            ? unrealized_pnl / (sl_pips * lot * pip_val)
            : 0m;

    [Fact]
    public void Position_R_From_Pnl_Buy_At_Plus_One_R()
    {
        // 0.1 lots, 10-pip SL, EURUSD pip_val=10 → 1R profit = $10
        decimal r = Calc_R_From_Pnl(unrealized_pnl: 10m, sl_pips: 10m, lot: 0.1m, pip_val: 10m);
        Assert.Equal(1.0m, r);
    }

    [Fact]
    public void Position_R_From_Pnl_Gold_At_Minus_Half_R()
    {
        // 0.01 lots, 20-pip SL, XAUUSD pip_val=100 → 0.5R loss = -$10
        decimal r = Calc_R_From_Pnl(unrealized_pnl: -10m, sl_pips: 20m, lot: 0.01m, pip_val: 100m);
        Assert.Equal(-0.5m, r);
    }

    [Fact]
    public void Position_R_From_Pnl_Returns_Zero_When_Sl_Is_Zero()
    {
        decimal r = Calc_R_From_Pnl(unrealized_pnl: 15m, sl_pips: 0m, lot: 0.1m, pip_val: 10m);
        Assert.Equal(0m, r);
    }

    [Fact]
    public void Position_R_From_Pnl_Returns_Zero_When_Lot_Is_Zero()
    {
        decimal r = Calc_R_From_Pnl(unrealized_pnl: 15m, sl_pips: 10m, lot: 0m, pip_val: 10m);
        Assert.Equal(0m, r);
    }

    [Fact]
    public void Position_R_Scales_Correctly_With_Larger_Position()
    {
        // 0.5 lots, 20-pip SL, pip_val=10 → 1R = $100, so +$200 = +2R
        decimal r = Calc_R_From_Pnl(unrealized_pnl: 200m, sl_pips: 20m, lot: 0.5m, pip_val: 10m);
        Assert.Equal(2.0m, r);
    }
}
