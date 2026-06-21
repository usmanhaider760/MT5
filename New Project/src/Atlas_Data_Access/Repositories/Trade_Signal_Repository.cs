using Atlas_Domain.BusinessObjects;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Atlas_Data_Access.Repositories;

public class Trade_Signal_Repository
{
    private readonly string _connection_string;

    public Trade_Signal_Repository(string connection_string)
    {
        _connection_string = connection_string;
    }

    public async Task Save_Signal_Async(Trade_Signal_BO signal)
    {
        using var conn = new SqliteConnection(_connection_string);
        await conn.ExecuteAsync(@"
            INSERT OR REPLACE INTO trade_signals
            (signal_id, generated_at, symbol_name, strategy, direction,
             entry_price, stop_loss_price, take_profit_price, reward_risk_ratio,
             quality_score, quality_grade, regime, session, spread_at_signal,
             is_approved, reject_reason, reject_detail)
            VALUES
            (@Signal_Id, @Generated_At, @Symbol_Name, @Strategy, @Direction,
             @Entry_Price, @Stop_Loss_Price, @Take_Profit_Price, @Reward_Risk_Ratio,
             @Quality_Score, @Quality_Grade, @Regime, @Session, @Spread_At_Signal,
             @Is_Approved, @Reject_Reason, @Reject_Detail);",
        new
        {
            Signal_Id         = signal.Signal_Id.ToString(),
            Generated_At      = signal.Generated_At_UTC.ToString("O"),
            signal.Symbol_Name,
            Strategy          = signal.Strategy.ToString(),
            Direction         = signal.Direction.ToString(),
            signal.Entry_Price,
            signal.Stop_Loss_Price,
            signal.Take_Profit_Price,
            signal.Reward_Risk_Ratio,
            signal.Quality_Score,
            Quality_Grade     = signal.Quality_Grade.ToString(),
            Regime            = signal.Regime_At_Signal.ToString(),
            Session           = signal.Session_At_Signal.ToString(),
            Spread_At_Signal  = signal.Spread_At_Signal,
            Is_Approved       = signal.Is_Approved ? 1 : 0,
            Reject_Reason     = signal.Reject_Reason.ToString(),
            signal.Reject_Detail
        });
    }

    public async Task<List<Trade_Signal_BO>> Get_Recent_Signals_Async(int limit = 100)
    {
        using var conn = new SqliteConnection(_connection_string);
        var rows = await conn.QueryAsync(
            "SELECT * FROM trade_signals ORDER BY generated_at DESC LIMIT @limit",
            new { limit });
        return rows.Select(Map_Signal).ToList();
    }

    private static Trade_Signal_BO Map_Signal(dynamic row) => new()
    {
        Signal_Id        = Guid.Parse(row.signal_id),
        Generated_At_UTC = DateTime.Parse(row.generated_at),
        Symbol_Name      = row.symbol_name,
        Entry_Price      = (decimal)row.entry_price,
        Stop_Loss_Price  = (decimal)row.stop_loss_price,
        Take_Profit_Price= (decimal)row.take_profit_price,
        Quality_Score    = row.quality_score,
        Is_Approved      = row.is_approved == 1,
        Reject_Detail    = row.reject_detail ?? string.Empty
    };
}
