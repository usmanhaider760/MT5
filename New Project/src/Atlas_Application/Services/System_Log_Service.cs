using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;

namespace Atlas_Application.Services;

/// <summary>
/// Structured, leveled logging with an in-memory ring buffer (last 1000 entries).
/// Sits alongside the existing raw string On_Log event chain rather than replacing
/// it, so the dashboard's log tab keeps working unchanged while gaining a queryable,
/// leveled log store for future use (filtering, a dedicated logs view, DB export, etc.).
/// </summary>
public class System_Log_Service
{
    private const int Max_Entries = 1000;
    private readonly object _lock = new();
    private readonly Queue<Log_Entry_BO> _buffer = new();

    public event Action<Log_Entry_BO>? On_Log;

    public void Log(Log_Level_Type level, string message, string? source = null)
    {
        var entry = new Log_Entry_BO
        {
            Timestamp_UTC = DateTime.UtcNow,
            Level = level,
            Message = message,
            Source = source ?? string.Empty
        };

        lock (_lock)
        {
            _buffer.Enqueue(entry);
            while (_buffer.Count > Max_Entries)
                _buffer.Dequeue();
        }

        On_Log?.Invoke(entry);
    }

    public List<Log_Entry_BO> Get_Recent(int count = 100)
    {
        lock (_lock)
            return _buffer.TakeLast(count).ToList();
    }
}
