using Atlas_Application.Services;
using Atlas_Data_Access;
using Atlas_Data_Access.Repositories;
using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Atlas_Tests;

/// <summary>
/// Verifies Performance_Monitor_Service.Load_History_From_Db_Async restores equity/stat state
/// after a simulated restart, using a real SQLite file (the bug this fixes only reproduces
/// against a genuinely empty in-memory _results list, i.e. process restart).
/// </summary>
public class Performance_History_Reload_Tests : IDisposable
{
    private readonly string _db_path;
    private readonly Trade_Result_Repository _result_repo;
    private readonly Strategy_Performance_Repository _perf_repo;

    public Performance_History_Reload_Tests()
    {
        _db_path = Path.Combine(Path.GetTempPath(), $"atlas_test_{Guid.NewGuid():N}.db");
        var db = new Database_Schema(_db_path);
        db.Ensure_Created();
        _result_repo = new Trade_Result_Repository(db.Connection_String);
        _perf_repo   = new Strategy_Performance_Repository(db.Connection_String);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_db_path)) File.Delete(_db_path);
    }

    private static Trade_Result_BO Result(decimal r_multiple, DateTime closed_at) => new()
    {
        Signal_Id                  = Guid.NewGuid(),
        Strategy                   = Strategy_Type.Trend_Pullback_Continuation,
        Symbol_Name                = "EURUSD",
        Direction                  = Trade_Direction_Type.Buy,
        Lot_Size                   = 0.1m,
        Initial_Stop_Distance_Pips = 10m,
        Pip_Value_Per_Lot          = 10m,
        Gross_PnL_Currency         = r_multiple * 10m,
        Closed_At_UTC              = closed_at,
        Opened_At_UTC              = closed_at.AddHours(-1)
    };

    [Fact]
    public async Task Load_History_Restores_Equity_Series_And_Stats_After_Simulated_Restart()
    {
        await _result_repo.Save_Result_Async(Result(1.0m, DateTime.UtcNow.AddMinutes(-30)));
        await _result_repo.Save_Result_Async(Result(2.0m, DateTime.UtcNow.AddMinutes(-20)));
        await _result_repo.Save_Result_Async(Result(-0.5m, DateTime.UtcNow.AddMinutes(-10)));

        // Fresh instance — simulates the empty in-memory state right after process restart
        var sut = new Performance_Monitor_Service(_perf_repo);
        Assert.Equal(0, sut.Total_Trades_Count);

        await sut.Load_History_From_Db_Async(_result_repo);

        Assert.Equal(3, sut.Total_Trades_Count);
        var series = sut.Get_Equity_Series();
        Assert.Equal(3, series.Count);
        Assert.Equal(10m, series[0]);
        Assert.Equal(30m, series[1]);
        Assert.Equal(25m, series[2]);
        Assert.True(sut.Calculate_Sharpe_Ratio() != 0);
    }

    [Fact]
    public async Task Load_History_Does_Not_Spam_Strategy_Performance_Snapshots()
    {
        await _result_repo.Save_Result_Async(Result(1.0m, DateTime.UtcNow.AddMinutes(-30)));
        await _result_repo.Save_Result_Async(Result(2.0m, DateTime.UtcNow.AddMinutes(-20)));
        await _result_repo.Save_Result_Async(Result(-0.5m, DateTime.UtcNow.AddMinutes(-10)));

        var sut = new Performance_Monitor_Service(_perf_repo);
        await sut.Load_History_From_Db_Async(_result_repo);

        // Replaying 3 historical trades on load must not write 3 new snapshot rows
        var snapshots = await _perf_repo.Get_All_Async();
        Assert.Empty(snapshots);
    }

    [Fact]
    public async Task Exit_Reason_Round_Trips_Through_Save_And_Reload()
    {
        var trade = Result(1.0m, DateTime.UtcNow);
        trade.Exit_Reason = Exit_Reason_Type.Trailing_Stop_Hit;

        await _result_repo.Save_Result_Async(trade);
        var all = await _result_repo.Get_All_Results_Async();
        var reloaded = Assert.Single(all);

        Assert.Equal(Exit_Reason_Type.Trailing_Stop_Hit, reloaded.Exit_Reason);
    }
}
