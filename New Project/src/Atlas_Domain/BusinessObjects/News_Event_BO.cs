namespace Atlas_Domain.BusinessObjects;

public class News_Event_BO
{
    public Guid Event_Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Deterministic ID derived from the event's natural key so that
    /// INSERT OR REPLACE correctly deduplicates the same event across
    /// multiple feed fetches instead of accumulating duplicate rows.
    /// </summary>
    public static Guid Stable_Id(DateTime event_utc, string currency, string event_name)
    {
        string key  = $"{event_utc:yyyyMMddHHmm}|{currency.ToUpperInvariant()}|{event_name}";
        byte[] hash = System.Security.Cryptography.MD5.HashData(
                          System.Text.Encoding.UTF8.GetBytes(key));
        return new Guid(hash);
    }
    public DateTime Event_UTC { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Event_Name { get; set; } = string.Empty;
    public string Impact_Level { get; set; } = string.Empty; // "High", "Medium", "Low"
    public bool Is_High_Impact => Impact_Level.Equals("High", StringComparison.OrdinalIgnoreCase);

    // Lockout window in minutes (before/after event)
    public int Lockout_Before_Minutes { get; set; } = 30;
    public int Lockout_After_Minutes { get; set; } = 30;

    public bool Is_Active_Lockout(DateTime now_utc)
    {
        var window_start = Event_UTC.AddMinutes(-Lockout_Before_Minutes);
        var window_end   = Event_UTC.AddMinutes(Lockout_After_Minutes);
        return now_utc >= window_start && now_utc <= window_end;
    }
}
