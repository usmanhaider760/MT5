using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Atlas_Data_Access.Repositories;

public class Strategy_Performance_Repository
{
    private readonly string _connection_string;

    public Strategy_Performance_Repository(string connection_string)
    {
        _connection_string = connection_string;
    }

    public virtual async Task Save_Snapshot_Async(Strategy_Performance_BO perf)
    {
        using var conn = new SqliteConnection(_connection_string);
        await conn.ExecuteAsync(@"
            INSERT INTO strategy_performance
            (strategy, symbol_name, snapshot_at, total_trades, winning_trades, losing_trades,
             gross_profit_r, gross_loss_r, profit_factor, average_r, max_drawdown_percent,
             rolling_20_expectancy, rolling_30_profit_factor, is_active, is_live_enabled, disable_reason)
            VALUES
            (@Strategy, @Symbol_Name, @Snapshot_At, @Total_Trades, @Winning_Trades, @Losing_Trades,
             @Gross_Profit_R, @Gross_Loss_R, @Profit_Factor, @Average_R, @Max_Drawdown_Percent,
             @Rolling_20_Expectancy, @Rolling_30_Profit_Factor, @Is_Active, @Is_Live_Enabled, @Disable_Reason);",
        new
        {
            Strategy                 = perf.Strategy.ToString(),
            perf.Symbol_Name,
            Snapshot_At              = DateTime.UtcNow.ToString("O"),
            perf.Total_Trades,
            perf.Winning_Trades,
            perf.Losing_Trades,
            perf.Gross_Profit_R,
            perf.Gross_Loss_R,
            Profit_Factor            = perf.Profit_Factor,
            Average_R                = perf.Average_R,
            perf.Max_Drawdown_Percent,
            perf.Rolling_20_Expectancy,
            perf.Rolling_30_Profit_Factor,
            Is_Active                = perf.Is_Active ? 1 : 0,
            Is_Live_Enabled          = perf.Is_Live_Enabled ? 1 : 0,
            perf.Disable_Reason
        });
    }

    /// <summary>Returns the most recent snapshot for each strategy — the current known state.</summary>
    public async Task<List<Strategy_Performance_BO>> Get_All_Async()
    {
        using var conn = new SqliteConnection(_connection_string);
        var rows = await conn.QueryAsync(@"
            SELECT * FROM strategy_performance
            WHERE id IN (SELECT MAX(id) FROM strategy_performance GROUP BY strategy)");
        return rows.Select(Map_Result).ToList();
    }

    /// <summary>Returns the full historical snapshot log for one strategy, oldest first.</summary>
    public async Task<List<Strategy_Performance_BO>> Get_By_Strategy_Async(Strategy_Type strategy)
    {
        using var conn = new SqliteConnection(_connection_string);
        var rows = await conn.QueryAsync(
            "SELECT * FROM strategy_performance WHERE strategy = @strategy ORDER BY snapshot_at",
            new { strategy = strategy.ToString() });
        return rows.Select(Map_Result).ToList();
    }

    private static Strategy_Performance_BO Map_Result(dynamic row)
    {
        Strategy_Type strategy = default;
        Enum.TryParse((string)(row.strategy ?? ""), out strategy);

        return new Strategy_Performance_BO
        {
            Strategy                 = strategy,
            Symbol_Name              = row.symbol_name ?? string.Empty,
            Total_Trades             = (int)row.total_trades,
            Winning_Trades           = (int)row.winning_trades,
            Losing_Trades            = (int)row.losing_trades,
            Gross_Profit_R           = (decimal)row.gross_profit_r,
            Gross_Loss_R             = (decimal)row.gross_loss_r,
            Max_Drawdown_Percent     = (decimal)row.max_drawdown_percent,
            Rolling_20_Expectancy    = (decimal)row.rolling_20_expectancy,
            Rolling_30_Profit_Factor = (decimal)row.rolling_30_profit_factor,
            Is_Active                = (long)row.is_active == 1,
            Is_Live_Enabled          = (long)row.is_live_enabled == 1,
            Disable_Reason           = row.disable_reason ?? string.Empty
        };
    }
}
