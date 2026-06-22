using Atlas_Application.Services;
using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;
using Xunit;

namespace Atlas_Tests;

/// <summary>
/// Verifies that Bot_Controller wires signal/account repos correctly.
/// All tests are pure in-memory — no SQLite.
/// </summary>
public class Repository_Wiring_Tests
{
    // ── Account snapshot throttle ────────────────────────────────────

    [Fact]
    public async Task Save_Account_Snapshot_Skips_When_Balance_Is_Zero()
    {
        var spy    = new Snapshot_Spy();
        var ctrl   = Build_Controller(account_repo_spy: spy);
        var state  = new Account_State_BO { Balance = 0, Equity = 0 };

        await ctrl.Save_Account_Snapshot_Async(state);

        Assert.Equal(0, spy.Save_Count);
    }

    [Fact]
    public async Task Save_Account_Snapshot_Saves_When_Balance_Nonzero()
    {
        var spy   = new Snapshot_Spy();
        var ctrl  = Build_Controller(account_repo_spy: spy);
        var state = new Account_State_BO { Balance = 10_000m, Equity = 10_050m, Snapshot_UTC = DateTime.UtcNow };

        await ctrl.Save_Account_Snapshot_Async(state);

        Assert.Equal(1, spy.Save_Count);
    }

    [Fact]
    public async Task Save_Account_Snapshot_Throttles_Within_Five_Minutes()
    {
        var spy   = new Snapshot_Spy();
        var ctrl  = Build_Controller(account_repo_spy: spy);
        var state = new Account_State_BO { Balance = 10_000m, Equity = 10_050m, Snapshot_UTC = DateTime.UtcNow };

        await ctrl.Save_Account_Snapshot_Async(state);
        await ctrl.Save_Account_Snapshot_Async(state);
        await ctrl.Save_Account_Snapshot_Async(state);

        // Throttle: only first call goes through within the same 5-minute window
        Assert.Equal(1, spy.Save_Count);
    }

    // ── Signal repo null safety ──────────────────────────────────────

    [Fact]
    public void Controller_Constructed_Without_Signal_Repo_Does_Not_Throw()
    {
        // Signals are not wired when signal_repo is null — just verify no crash
        var ctrl = Build_Controller();
        Assert.NotNull(ctrl);
    }

    // ── Helper ───────────────────────────────────────────────────────

    private static Bot_Controller Build_Controller(
        Atlas_Data_Access.Repositories.Account_Snapshot_Repository? account_repo_spy = null)
    {
        var orchestrator = new Atlas_Strategy.Strategy_Orchestrator();
        var perf         = new Performance_Monitor_Service();
        var emergency    = new Atlas_Execution.Services.Emergency_Stop_Service();
        var risk         = Risk_Setting_BO.Conservative_Launch();
        var pipeline     = new Trade_Pipeline_Null_Stub();

        return new Bot_Controller(
            pipeline, perf, orchestrator, emergency, risk,
            account_repo: account_repo_spy);
    }
}

// ── Test doubles ─────────────────────────────────────────────────────

file sealed class Snapshot_Spy : Atlas_Data_Access.Repositories.Account_Snapshot_Repository
{
    public int Save_Count { get; private set; }

    public Snapshot_Spy() : base("Data Source=:memory:") { }

    public override Task Save_Snapshot_Async(Account_State_BO account)
    {
        Save_Count++;
        return Task.CompletedTask;
    }
}

file sealed class Trade_Pipeline_Null_Stub : Atlas_Application.Services.Trade_Pipeline_Service
{
    public Trade_Pipeline_Null_Stub() : base(
        null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!)
    { }
}
