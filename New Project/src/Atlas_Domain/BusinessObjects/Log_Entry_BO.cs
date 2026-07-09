using Atlas_Domain.Enums;

namespace Atlas_Domain.BusinessObjects;

public class Log_Entry_BO
{
    public DateTime Timestamp_UTC { get; set; } = DateTime.UtcNow;
    public Log_Level_Type Level { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
}
