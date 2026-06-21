namespace Atlas_Domain.BusinessObjects;

public class News_Event_BO
{
    public Guid Event_Id { get; set; } = Guid.NewGuid();
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
