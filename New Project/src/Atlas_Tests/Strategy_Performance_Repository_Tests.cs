using Atlas_Data_Access;
using Atlas_Data_Access.Repositories;
using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Atlas_Tests;

/// <summary>
/// Round-trips Strategy_Performance_BO snapshots through a real SQLite file
/// to verify the save/reload path used to restore performance history on restart.
/// </summary>
public class Strategy_Performance_Repository_Tests : IDisposable
{
    private readonly string _db_path;
    private readonly Strategy_Performance_Repository _sut;

    public Strategy_Performance_Repository_Tests()
    {
        _db_path = Path.Combine(Path.GetTempPath(), $"atlas_test_{Guid.NewGuid():N}.db");
        var db = new Database_Schema(_db_path);
        db.Ensure_Created();
        _sut = new Strategy_Performance_Repository(db.Connection_String);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_db_path)) File.Delete(_db_path);
    }

    [Fact]
    public async Task Save_Snapshot_Then_Reload_Matches_Original()
    {
        var original = new Strategy_Performance_BO
        {
            Strategy = Strategy_Type.Trend_Pullback_Continuation,
            Symbol_Name = "EURUSD",
            Total_Trades = 25,
            Winning_Trades = 15,
            Losing_Trades = 10,
            Gross_Profit_R = 30m,
            Gross_Loss_R = -12m,
            Max_Drawdown_Percent = 4.5m,
            Rolling_20_Expectancy = 0.8m,
            Rolling_30_Profit_Factor = 1.4m,
            Is_Active = true,
            Is_Live_Enabled = true,
            Disable_Reason = string.Empty
        };

        await _sut.Save_Snapshot_Async(original);
        var all = await _sut.Get_All_Async();
        var reloaded = Assert.Single(all);

        Assert.Equal(original.Strategy, reloaded.Strategy);
        Assert.Equal(original.Symbol_Name, reloaded.Symbol_Name);
        Assert.Equal(original.Total_Trades, reloaded.Total_Trades);
        Assert.Equal(original.Winning_Trades, reloaded.Winning_Trades);
        Assert.Equal(original.Losing_Trades, reloaded.Losing_Trades);
        Assert.Equal(original.Gross_Profit_R, reloaded.Gross_Profit_R);
        Assert.Equal(original.Gross_Loss_R, reloaded.Gross_Loss_R);
        Assert.Equal(original.Profit_Factor, reloaded.Profit_Factor);
        Assert.Equal(original.Average_R, reloaded.Average_R);
        Assert.Equal(original.Max_Drawdown_Percent, reloaded.Max_Drawdown_Percent);
        Assert.Equal(original.Rolling_20_Expectancy, reloaded.Rolling_20_Expectancy);
        Assert.Equal(original.Rolling_30_Profit_Factor, reloaded.Rolling_30_Profit_Factor);
        Assert.Equal(original.Is_Active, reloaded.Is_Active);
        Assert.Equal(original.Is_Live_Enabled, reloaded.Is_Live_Enabled);
    }

    [Fact]
    public async Task Get_All_Async_Returns_Only_The_Latest_Snapshot_Per_Strategy()
    {
        var perf = new Strategy_Performance_BO { Strategy = Strategy_Type.Trend_Pullback_Continuation, Symbol_Name = "EURUSD", Total_Trades = 5 };
        await _sut.Save_Snapshot_Async(perf);

        perf.Total_Trades = 10; // simulate more trades recorded, then a second snapshot
        await _sut.Save_Snapshot_Async(perf);

        var all = await _sut.Get_All_Async();
        var latest = Assert.Single(all);
        Assert.Equal(10, latest.Total_Trades);
    }

    [Fact]
    public async Task Get_By_Strategy_Async_Returns_Full_History_Oldest_First()
    {
        var perf = new Strategy_Performance_BO { Strategy = Strategy_Type.Breakout_Retest_Expansion, Symbol_Name = "XAUUSD", Total_Trades = 3 };
        await _sut.Save_Snapshot_Async(perf);

        perf.Total_Trades = 7;
        await _sut.Save_Snapshot_Async(perf);

        var history = await _sut.Get_By_Strategy_Async(Strategy_Type.Breakout_Retest_Expansion);

        Assert.Equal(2, history.Count);
        Assert.Equal(3, history[0].Total_Trades);
        Assert.Equal(7, history[1].Total_Trades);
    }
}
