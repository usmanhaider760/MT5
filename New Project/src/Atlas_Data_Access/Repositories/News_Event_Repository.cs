using Atlas_Domain.BusinessObjects;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Atlas_Data_Access.Repositories;

public class News_Event_Repository
{
    private readonly string _connection_string;

    public News_Event_Repository(string connection_string)
    {
        _connection_string = connection_string;
    }

    public async Task Save_Events_Async(IEnumerable<News_Event_BO> events)
    {
        using var conn = new SqliteConnection(_connection_string);
        await conn.OpenAsync();
        using var tx = await conn.BeginTransactionAsync();

        foreach (var evt in events)
        {
            await conn.ExecuteAsync(@"
                INSERT OR REPLACE INTO news_events
                (event_id, event_utc, currency, event_name, impact_level, lockout_before_min, lockout_after_min)
                VALUES
                (@Event_Id, @Event_UTC, @Currency, @Event_Name, @Impact_Level, @Lockout_Before, @Lockout_After);",
            new
            {
                Event_Id     = evt.Event_Id.ToString(),
                Event_UTC    = evt.Event_UTC.ToString("O"),
                evt.Currency,
                Event_Name   = evt.Event_Name,
                Impact_Level = evt.Impact_Level,
                Lockout_Before = evt.Lockout_Before_Minutes,
                Lockout_After  = evt.Lockout_After_Minutes
            }, tx);
        }

        await tx.CommitAsync();
    }

    public async Task<List<News_Event_BO>> Get_Upcoming_High_Impact_Async(int hours = 48)
    {
        using var conn = new SqliteConnection(_connection_string);
        var from = DateTime.UtcNow.ToString("O");
        var to   = DateTime.UtcNow.AddHours(hours).ToString("O");

        var rows = await conn.QueryAsync(@"
            SELECT * FROM news_events
            WHERE impact_level = 'High'
              AND event_utc BETWEEN @from AND @to
            ORDER BY event_utc",
            new { from, to });

        return rows.Select(r => new News_Event_BO
        {
            Event_Id             = Guid.Parse((string)r.event_id),
            Event_UTC            = DateTime.Parse((string)r.event_utc),
            Currency             = (string)r.currency,
            Event_Name           = (string)r.event_name,
            Impact_Level         = (string)r.impact_level,
            Lockout_Before_Minutes = (int)r.lockout_before_min,
            Lockout_After_Minutes  = (int)r.lockout_after_min
        }).ToList();
    }
}
