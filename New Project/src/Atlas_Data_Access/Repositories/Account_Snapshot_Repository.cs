using Atlas_Domain.BusinessObjects;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Atlas_Data_Access.Repositories;

public class Account_Snapshot_Repository
{
    private readonly string _connection_string;

    public Account_Snapshot_Repository(string connection_string)
    {
        _connection_string = connection_string;
    }

    public async Task Save_Snapshot_Async(Account_State_BO account)
    {
        using var conn = new SqliteConnection(_connection_string);
        await conn.ExecuteAsync(@"
            INSERT INTO account_snapshots
            (snapshot_utc, balance, equity, margin_used, free_margin,
             daily_pnl, weekly_pnl, drawdown_pct, open_trades)
            VALUES
            (@Snapshot_UTC, @Balance, @Equity, @Margin_Used, @Free_Margin,
             @Daily_PnL, @Weekly_PnL, @Drawdown_Pct, @Open_Trades);",
        new
        {
            Snapshot_UTC  = account.Snapshot_UTC.ToString("O"),
            account.Balance,
            account.Equity,
            Margin_Used   = account.Margin_Used,
            Free_Margin   = account.Free_Margin,
            Daily_PnL     = account.Daily_PnL,
            Weekly_PnL    = account.Weekly_PnL,
            Drawdown_Pct  = account.Drawdown_From_Peak,
            Open_Trades   = account.Open_Trade_Count
        });
    }

    public async Task<List<(DateTime UTC, decimal Equity, decimal Drawdown)>> Get_Equity_Curve_Async(int days = 30)
    {
        using var conn = new SqliteConnection(_connection_string);
        var cutoff = DateTime.UtcNow.AddDays(-days).ToString("O");
        var rows = await conn.QueryAsync(
            "SELECT snapshot_utc, equity, drawdown_pct FROM account_snapshots WHERE snapshot_utc >= @cutoff ORDER BY snapshot_utc",
            new { cutoff });

        return rows.Select(r => (
            UTC:       DateTime.Parse((string)r.snapshot_utc),
            Equity:    (decimal)r.equity,
            Drawdown:  (decimal)r.drawdown_pct
        )).ToList();
    }

    public async Task<decimal> Get_Peak_Equity_Async()
    {
        using var conn = new SqliteConnection(_connection_string);
        var result = await conn.QueryFirstOrDefaultAsync<double?>("SELECT MAX(equity) FROM account_snapshots");
        return result.HasValue ? (decimal)result.Value : 0;
    }
}
