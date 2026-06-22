using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Atlas_Data_Access.Repositories;

/// <summary>
/// Persists the current set of open positions to SQLite so they survive an app restart.
/// Uses a full replace strategy: Save_All_Async clears the table then bulk-inserts.
/// </summary>
public class Open_Position_Repository
{
    private readonly string _connection_string;

    public Open_Position_Repository(string connection_string)
    {
        _connection_string = connection_string;
    }

    public async Task Save_All_Async(List<Position_BO> positions)
    {
        using var conn = new SqliteConnection(_connection_string);
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction();

        await conn.ExecuteAsync("DELETE FROM open_positions", transaction: tx);

        foreach (var p in positions)
        {
            await conn.ExecuteAsync(@"
                INSERT INTO open_positions
                (broker_ticket, signal_id, symbol_name, strategy, direction,
                 lot_size, entry_price, stop_loss_price, take_profit_price,
                 risk_amount_currency, risk_percent, initial_stop_distance_pips,
                 opened_at, is_at_breakeven, partial_profit_taken)
                VALUES
                (@broker_ticket, @signal_id, @symbol_name, @strategy, @direction,
                 @lot_size, @entry_price, @stop_loss_price, @take_profit_price,
                 @risk_amount_currency, @risk_percent, @initial_stop_distance_pips,
                 @opened_at, @is_at_breakeven, @partial_profit_taken)",
            new
            {
                broker_ticket              = p.Broker_Ticket,
                signal_id                  = p.Signal_Id.ToString(),
                symbol_name                = p.Symbol_Name,
                strategy                   = p.Strategy.ToString(),
                direction                  = p.Direction.ToString(),
                lot_size                   = p.Lot_Size,
                entry_price                = p.Entry_Price,
                stop_loss_price            = p.Stop_Loss_Price,
                take_profit_price          = p.Take_Profit_Price,
                risk_amount_currency       = p.Risk_Amount_Currency,
                risk_percent               = p.Risk_Percent,
                initial_stop_distance_pips = p.Initial_Stop_Distance_Pips,
                opened_at                  = p.Opened_At_UTC.ToString("O"),
                is_at_breakeven            = p.Is_At_Breakeven ? 1 : 0,
                partial_profit_taken       = p.Partial_Profit_Taken ? 1 : 0
            }, transaction: tx);
        }

        tx.Commit();
    }

    public async Task Delete_By_Ticket_Async(long broker_ticket)
    {
        using var conn = new SqliteConnection(_connection_string);
        await conn.ExecuteAsync(
            "DELETE FROM open_positions WHERE broker_ticket = @ticket",
            new { ticket = broker_ticket });
    }

    public async Task<List<Position_BO>> Get_All_Async()
    {
        using var conn = new SqliteConnection(_connection_string);
        var rows = await conn.QueryAsync("SELECT * FROM open_positions ORDER BY opened_at");
        return rows.Select(Map).ToList();
    }

    private static Position_BO Map(dynamic r) => new()
    {
        Broker_Ticket              = (long)r.broker_ticket,
        Signal_Id                  = Guid.Parse((string)r.signal_id),
        Symbol_Name                = (string)r.symbol_name,
        Strategy                   = Enum.Parse<Strategy_Type>((string)r.strategy),
        Direction                  = Enum.Parse<Trade_Direction_Type>((string)r.direction),
        Lot_Size                   = (decimal)r.lot_size,
        Entry_Price                = (decimal)r.entry_price,
        Stop_Loss_Price            = (decimal)r.stop_loss_price,
        Take_Profit_Price          = (decimal)r.take_profit_price,
        Risk_Amount_Currency       = (decimal)r.risk_amount_currency,
        Risk_Percent               = (decimal)r.risk_percent,
        Initial_Stop_Distance_Pips = (decimal)r.initial_stop_distance_pips,
        Opened_At_UTC              = DateTime.Parse((string)r.opened_at),
        Is_At_Breakeven            = (long)r.is_at_breakeven == 1,
        Partial_Profit_Taken       = (long)r.partial_profit_taken == 1,
        Status                     = Order_Status_Type.Filled
    };
}
