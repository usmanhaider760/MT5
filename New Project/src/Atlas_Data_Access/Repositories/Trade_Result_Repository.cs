using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Atlas_Data_Access.Repositories;

public class Trade_Result_Repository
{
    private readonly string _connection_string;

    public Trade_Result_Repository(string connection_string)
    {
        _connection_string = connection_string;
    }

    public async Task Save_Result_Async(Trade_Result_BO result)
    {
        using var conn = new SqliteConnection(_connection_string);
        await conn.ExecuteAsync(@"
            INSERT OR REPLACE INTO trade_results
            (signal_id, broker_ticket, symbol_name, strategy, regime_at_entry, session_at_entry,
             direction, lot_size, entry_price, exit_price, stop_loss_price, take_profit_price,
             opened_at, closed_at, gross_pnl, commission, swap, slippage_pips, net_pnl,
             r_multiple, is_winner, close_reason)
            VALUES
            (@Signal_Id, @Broker_Ticket, @Symbol_Name, @Strategy, @Regime_At_Entry, @Session_At_Entry,
             @Direction, @Lot_Size, @Entry_Price, @Exit_Price, @Stop_Loss_Price, @Take_Profit_Price,
             @Opened_At, @Closed_At, @Gross_PnL, @Commission, @Swap, @Slippage_Pips, @Net_PnL,
             @R_Multiple, @Is_Winner, @Close_Reason);",
        new
        {
            Signal_Id       = result.Signal_Id.ToString(),
            result.Broker_Ticket,
            result.Symbol_Name,
            Strategy        = result.Strategy.ToString(),
            Regime_At_Entry = result.Regime_At_Entry.ToString(),
            Session_At_Entry= result.Session_At_Entry.ToString(),
            Direction       = result.Direction.ToString(),
            result.Lot_Size,
            result.Entry_Price,
            result.Exit_Price,
            result.Stop_Loss_Price,
            result.Take_Profit_Price,
            Opened_At       = result.Opened_At_UTC.ToString("O"),
            Closed_At       = result.Closed_At_UTC.ToString("O"),
            Gross_PnL       = result.Gross_PnL_Currency,
            result.Commission,
            result.Swap,
            result.Slippage_Pips,
            Net_PnL         = result.Net_PnL_Currency,
            R_Multiple      = result.R_Multiple,
            Is_Winner       = result.Is_Winner ? 1 : 0,
            result.Close_Reason
        });
    }

    public async Task<List<Trade_Result_BO>> Get_All_Results_Async()
    {
        using var conn = new SqliteConnection(_connection_string);
        var rows = await conn.QueryAsync("SELECT * FROM trade_results ORDER BY closed_at DESC");
        return rows.Select(Map_Result).ToList();
    }

    public async Task<List<Trade_Result_BO>> Get_Results_By_Strategy_Async(Strategy_Type strategy)
    {
        using var conn = new SqliteConnection(_connection_string);
        var rows = await conn.QueryAsync(
            "SELECT * FROM trade_results WHERE strategy = @strategy ORDER BY closed_at DESC",
            new { strategy = strategy.ToString() });
        return rows.Select(Map_Result).ToList();
    }

    public async Task<List<Trade_Result_BO>> Get_Recent_Async(int count = 50)
    {
        using var conn = new SqliteConnection(_connection_string);
        var rows = await conn.QueryAsync(
            "SELECT * FROM trade_results ORDER BY closed_at DESC LIMIT @count",
            new { count });
        return rows.Select(Map_Result).ToList();
    }

    public async Task<(decimal Win_Rate, decimal Avg_R, decimal Profit_Factor, int Total)> Get_Stats_Async()
    {
        using var conn = new SqliteConnection(_connection_string);
        var row = await conn.QueryFirstOrDefaultAsync(@"
            SELECT
                COUNT(*) as total,
                SUM(CASE WHEN is_winner = 1 THEN 1 ELSE 0 END) as wins,
                AVG(r_multiple) as avg_r,
                SUM(CASE WHEN net_pnl > 0 THEN net_pnl ELSE 0 END) as gross_profit,
                SUM(CASE WHEN net_pnl < 0 THEN ABS(net_pnl) ELSE 0 END) as gross_loss
            FROM trade_results");

        if (row == null) return (0, 0, 0, 0);

        int total       = (int)(row.total ?? 0);
        if (total == 0) return (0, 0, 0, 0);
        int wins        = (int)(row.wins ?? 0);
        decimal avg_r   = (decimal)(row.avg_r ?? 0.0);
        decimal g_p     = (decimal)(row.gross_profit ?? 0.0);
        decimal g_l     = (decimal)(row.gross_loss ?? 0.0);
        decimal pf      = g_l > 0 ? Math.Round(g_p / g_l, 2) : 0;
        decimal wr      = total > 0 ? Math.Round((decimal)wins / total * 100, 1) : 0;

        return (wr, Math.Round(avg_r, 3), pf, total);
    }

    private static Trade_Result_BO Map_Result(dynamic row) => new()
    {
        Signal_Id         = Guid.Parse(row.signal_id),
        Broker_Ticket     = (long)row.broker_ticket,
        Symbol_Name       = row.symbol_name,
        Lot_Size          = (decimal)row.lot_size,
        Entry_Price       = (decimal)row.entry_price,
        Exit_Price        = (decimal)row.exit_price,
        Stop_Loss_Price   = (decimal)row.stop_loss_price,
        Take_Profit_Price = (decimal)row.take_profit_price,
        Opened_At_UTC     = DateTime.Parse(row.opened_at),
        Closed_At_UTC     = DateTime.Parse(row.closed_at),
        Gross_PnL_Currency= (decimal)row.gross_pnl,
        Commission        = (decimal)row.commission,
        Swap              = (decimal)row.swap,
        Slippage_Pips     = (decimal)row.slippage_pips,
        Close_Reason      = row.close_reason ?? string.Empty
        // Is_Winner is computed from Net_PnL_Currency (read-only property)
    };
}
