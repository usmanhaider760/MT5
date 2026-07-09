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
             r_multiple, is_winner, close_reason, notes, pip_value_per_lot, exit_reason)
            VALUES
            (@Signal_Id, @Broker_Ticket, @Symbol_Name, @Strategy, @Regime_At_Entry, @Session_At_Entry,
             @Direction, @Lot_Size, @Entry_Price, @Exit_Price, @Stop_Loss_Price, @Take_Profit_Price,
             @Opened_At, @Closed_At, @Gross_PnL, @Commission, @Swap, @Slippage_Pips, @Net_PnL,
             @R_Multiple, @Is_Winner, @Close_Reason, @Notes, @Pip_Value_Per_Lot, @Exit_Reason);",
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
            result.Close_Reason,
            result.Notes,
            result.Pip_Value_Per_Lot,
            Exit_Reason = result.Exit_Reason.ToString()
        });
    }

    public async Task<List<Trade_Result_BO>> Get_All_Results_Async()
    {
        using var conn = new SqliteConnection(_connection_string);
        var rows = await conn.QueryAsync("SELECT * FROM trade_results ORDER BY closed_at DESC");
        return rows.Select(Map_Result).ToList();
    }

    public async Task<List<Trade_Result_BO>> Get_By_Date_Async(DateTime date_utc)
    {
        string from_str = date_utc.Date.ToString("yyyy-MM-dd") + "T00:00:00";
        string to_str   = date_utc.Date.AddDays(1).ToString("yyyy-MM-dd") + "T00:00:00";
        using var conn  = new SqliteConnection(_connection_string);
        var rows = await conn.QueryAsync(
            "SELECT * FROM trade_results WHERE closed_at >= @from AND closed_at < @to ORDER BY closed_at",
            new { from = from_str, to = to_str });
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

    public async Task Update_Notes_Async(Guid signal_id, string notes)
    {
        using var conn = new SqliteConnection(_connection_string);
        await conn.ExecuteAsync(
            "UPDATE trade_results SET notes = @notes WHERE signal_id = @signal_id",
            new { notes, signal_id = signal_id.ToString() });
    }

    private static Trade_Result_BO Map_Result(dynamic row)
    {
        decimal lot       = (decimal)row.lot_size;
        decimal gross_pnl = (decimal)row.gross_pnl;
        decimal comm      = (decimal)row.commission;
        decimal swap      = (decimal)row.swap;
        decimal net_pnl   = gross_pnl - comm - Math.Abs(swap);
        decimal r_stored  = (decimal)(row.r_multiple ?? 0.0);
        decimal pip_value = (decimal)(row.pip_value_per_lot ?? 10.0);

        // Back-calculate SL pips from stored R and net PnL so the computed R_Multiple property is correct
        decimal sl_pips = (r_stored != 0 && lot != 0 && pip_value != 0)
            ? Math.Abs(net_pnl) / (Math.Abs(r_stored) * lot * pip_value)
            : 0m;

        Strategy_Type        st = default;
        Market_Regime_Type   re = default;
        Session_Type         se = default;
        Trade_Direction_Type di = default;
        Exit_Reason_Type     er = default;
        Enum.TryParse((string)(row.strategy         ?? ""), out st);
        Enum.TryParse((string)(row.regime_at_entry  ?? ""), out re);
        Enum.TryParse((string)(row.session_at_entry ?? ""), out se);
        Enum.TryParse((string)(row.direction        ?? ""), out di);
        Enum.TryParse((string)(row.exit_reason       ?? ""), out er);

        return new Trade_Result_BO
        {
            Signal_Id                  = Guid.Parse(row.signal_id),
            Broker_Ticket              = (long)row.broker_ticket,
            Symbol_Name                = row.symbol_name,
            Strategy                   = st,
            Regime_At_Entry            = re,
            Session_At_Entry           = se,
            Direction                  = di,
            Lot_Size                   = lot,
            Entry_Price                = (decimal)row.entry_price,
            Exit_Price                 = (decimal)row.exit_price,
            Stop_Loss_Price            = (decimal)row.stop_loss_price,
            Take_Profit_Price          = (decimal)row.take_profit_price,
            Opened_At_UTC              = DateTime.Parse(row.opened_at),
            Closed_At_UTC              = DateTime.Parse(row.closed_at),
            Gross_PnL_Currency         = gross_pnl,
            Commission                 = comm,
            Swap                       = swap,
            Slippage_Pips              = (decimal)row.slippage_pips,
            Initial_Stop_Distance_Pips = sl_pips,
            Pip_Value_Per_Lot          = pip_value,
            Exit_Reason                = er,
            Close_Reason               = row.close_reason ?? string.Empty,
            Notes                      = row.notes         ?? string.Empty
        };
    }
}
