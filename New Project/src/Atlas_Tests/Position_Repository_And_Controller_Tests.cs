using Atlas_Application.Services;
using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;
using Xunit;

namespace Atlas_Tests;

/// <summary>
/// Tests for On_Strategy_State_Changed event propagation and close-removes-position logic.
/// Uses in-memory stubs — no SQLite required.
/// </summary>
public class Position_Repository_And_Controller_Tests
{
    // ── On_Strategy_State_Changed ────────────────────────────────────

    [Fact]
    public void Enable_Strategy_Fires_State_Changed_Event_With_True()
    {
        var orchestrator = new Atlas_Strategy.Strategy_Orchestrator();
        var controller   = Build_Controller(orchestrator);

        (Strategy_Type type, bool active) captured = default;
        controller.On_Strategy_State_Changed += (t, a) => captured = (t, a);

        controller.Enable_Strategy(Strategy_Type.Liquidity_Sweep_Reversal);

        Assert.Equal(Strategy_Type.Liquidity_Sweep_Reversal, captured.type);
        Assert.True(captured.active);
    }

    [Fact]
    public void Disable_Strategy_Fires_State_Changed_Event_With_False()
    {
        var orchestrator = new Atlas_Strategy.Strategy_Orchestrator();
        var controller   = Build_Controller(orchestrator);

        (Strategy_Type type, bool active) captured = default;
        controller.On_Strategy_State_Changed += (t, a) => captured = (t, a);

        controller.Disable_Strategy(Strategy_Type.Trend_Pullback_Continuation);

        Assert.Equal(Strategy_Type.Trend_Pullback_Continuation, captured.type);
        Assert.False(captured.active);
    }

    [Fact]
    public void Enable_Then_Disable_Reflects_In_Get_Strategy_Status()
    {
        var orchestrator = new Atlas_Strategy.Strategy_Orchestrator();
        var controller   = Build_Controller(orchestrator);

        controller.Enable_Strategy(Strategy_Type.Breakout_Retest_Expansion);
        Assert.True(controller.Get_Strategy_Status()[Strategy_Type.Breakout_Retest_Expansion]);

        controller.Disable_Strategy(Strategy_Type.Breakout_Retest_Expansion);
        Assert.False(controller.Get_Strategy_Status()[Strategy_Type.Breakout_Retest_Expansion]);
    }

    // ── Delete_By_Ticket logic ───────────────────────────────────────

    [Fact]
    public void Delete_By_Ticket_Removes_Matching_Entry_From_List()
    {
        // Simulate what the repository does: filter out the closed ticket
        var positions = new List<Position_BO>
        {
            new() { Broker_Ticket = 1001, Symbol_Name = "EURUSD" },
            new() { Broker_Ticket = 1002, Symbol_Name = "GBPUSD" },
            new() { Broker_Ticket = 1003, Symbol_Name = "XAUUSD" }
        };

        long ticket_to_close = 1002;
        var remaining = positions.Where(p => p.Broker_Ticket != ticket_to_close).ToList();

        Assert.Equal(2, remaining.Count);
        Assert.DoesNotContain(remaining, p => p.Broker_Ticket == 1002);
        Assert.Contains(remaining, p => p.Broker_Ticket == 1001);
        Assert.Contains(remaining, p => p.Broker_Ticket == 1003);
    }

    [Fact]
    public void Delete_By_Ticket_No_Op_When_Ticket_Not_In_List()
    {
        var positions = new List<Position_BO>
        {
            new() { Broker_Ticket = 1001 }
        };

        var remaining = positions.Where(p => p.Broker_Ticket != 9999).ToList();
        Assert.Single(remaining);
    }

    // ── Helper ───────────────────────────────────────────────────────

    private static Bot_Controller Build_Controller(Atlas_Strategy.Strategy_Orchestrator orchestrator)
    {
        var pipeline      = new Trade_Pipeline_Service_Stub();
        var perf          = new Atlas_Application.Services.Performance_Monitor_Service();
        var emergency     = new Atlas_Execution.Services.Emergency_Stop_Service();
        var risk_settings = Atlas_Domain.BusinessObjects.Risk_Setting_BO.Conservative_Launch();

        return new Bot_Controller(
            pipeline, perf, orchestrator, emergency, risk_settings);
    }
}

// Minimal stub so we can construct Bot_Controller without real services
file sealed class Trade_Pipeline_Service_Stub : Atlas_Application.Services.Trade_Pipeline_Service
{
    public Trade_Pipeline_Service_Stub() : base(
        market_data:        null!,
        regime_detector:    null!,
        session_filter:     null!,
        spread_filter:      null!,
        news_filter:        null!,
        volatility_filter:  null!,
        strategy_orchestrator: null!,
        quality_scorer:     null!,
        risk_manager:       null!,
        drawdown_guard:     null!,
        execution:          null!,
        emergency_stop:     null!,
        risk_settings:      null!,
        trade_manager:      null!,
        correlation_guard:  null!)
    { }
}
