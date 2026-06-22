using System.Text.Json;
using System.Text.Json.Serialization;
using Atlas_Domain.BusinessObjects;

namespace Atlas_Market_Data.Services;

/// <summary>
/// Fetches the economic calendar from a JSON feed.
/// Supports ForexFactory-style JSON or a custom feed.
/// Falls back to a manually seeded calendar when the feed is unavailable.
/// </summary>
public class Economic_Calendar_Service
{
    private readonly HttpClient _http;
    private readonly string _feed_url;
    private List<News_Event_BO> _cached_events = [];
    private DateTime _last_refresh = DateTime.MinValue;
    private readonly TimeSpan _cache_ttl = TimeSpan.FromHours(6);

    public Economic_Calendar_Service(HttpClient http, string feed_url = "")
    {
        _http     = http;
        _feed_url = feed_url;
    }

    public async Task<List<News_Event_BO>> Get_High_Impact_Events_Async(int look_ahead_hours = 72)
    {
        await Refresh_If_Stale_Async();

        var cutoff = DateTime.UtcNow.AddHours(look_ahead_hours);
        return _cached_events
            .Where(e => e.Is_High_Impact && e.Event_UTC >= DateTime.UtcNow && e.Event_UTC <= cutoff)
            .OrderBy(e => e.Event_UTC)
            .ToList();
    }

    public async Task<bool> Is_High_Impact_Active_Async(string currency, bool is_gold = false)
    {
        await Refresh_If_Stale_Async();

        var now = DateTime.UtcNow;
        return _cached_events.Any(e =>
            e.Is_High_Impact &&
            e.Currency.Equals(currency, StringComparison.OrdinalIgnoreCase) &&
            e.Is_Active_Lockout(now));
    }

    private async Task Refresh_If_Stale_Async()
    {
        if (DateTime.UtcNow - _last_refresh < _cache_ttl) return;

        try
        {
            if (!string.IsNullOrEmpty(_feed_url))
                await Fetch_From_Feed_Async();
            else
                Seed_Demo_Events();

            _last_refresh = DateTime.UtcNow;
        }
        catch
        {
            if (_cached_events.Count == 0)
                Seed_Demo_Events();
        }
    }

    private async Task Fetch_From_Feed_Async()
    {
        var opts  = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var json  = await _http.GetStringAsync(_feed_url).ConfigureAwait(false);
        var items = JsonSerializer.Deserialize<List<Calendar_Feed_Item>>(json, opts);
        if (items == null) return;

        _cached_events = items
            .Where(i => i.Impact?.Equals("High", StringComparison.OrdinalIgnoreCase) == true)
            .Select(Map_Feed_Item)
            .ToList();
    }

    private static News_Event_BO Map_Feed_Item(Calendar_Feed_Item item)
    {
        // Date is a DateTimeOffset — always convert to UTC regardless of source timezone
        DateTime event_utc = item.Date.UtcDateTime;

        // Prefer the "country" field (ForexFactory); fall back to "currency"
        string currency = !string.IsNullOrWhiteSpace(item.Country) ? item.Country
                        : item.Currency ?? string.Empty;

        string event_name = item.Title ?? string.Empty;
        var evt = new News_Event_BO
        {
            Event_Id     = News_Event_BO.Stable_Id(event_utc, currency, event_name),
            Event_UTC    = event_utc,
            Currency     = currency,
            Event_Name   = event_name,
            Impact_Level = item.Impact ?? string.Empty
        };

        bool is_nfp  = (item.Title ?? "").Contains("Non-Farm",    StringComparison.OrdinalIgnoreCase)
                    || (item.Title ?? "").Contains("NFP",          StringComparison.OrdinalIgnoreCase);
        bool is_fomc = (item.Title ?? "").Contains("FOMC",        StringComparison.OrdinalIgnoreCase)
                    || (item.Title ?? "").Contains("Rate Decision",StringComparison.OrdinalIgnoreCase);
        bool is_cpi  = (item.Title ?? "").Contains("CPI",         StringComparison.OrdinalIgnoreCase);

        if (is_fomc)           { evt.Lockout_Before_Minutes = 120; evt.Lockout_After_Minutes = 120; }
        else if (is_nfp || is_cpi) { evt.Lockout_Before_Minutes = 60;  evt.Lockout_After_Minutes = 90;  }
        else                   { evt.Lockout_Before_Minutes = 30;  evt.Lockout_After_Minutes = 30;  }

        return evt;
    }

    // Seed with realistic test events for development/demo mode
    private void Seed_Demo_Events()
    {
        var base_time = DateTime.UtcNow.Date.AddHours(13.5); // typical NY open event
        _cached_events = Make_Demo_Event("USD", "Non-Farm Payrolls",  base_time.AddDays(1), 60,  90)
            .Concat(Make_Demo_Event("USD", "CPI m/m",              base_time.AddDays(3), 60,  60))
            .Concat(Make_Demo_Event("USD", "FOMC Rate Decision",   base_time.AddDays(7), 120, 120))
            .Concat(Make_Demo_Event("EUR", "ECB Rate Decision",    base_time.AddDays(2), 60,  60))
            .Concat(Make_Demo_Event("GBP", "BOE Rate Decision",    base_time.AddDays(4), 60,  60))
            .Concat(Make_Demo_Event("AUD", "RBA Rate Statement",   base_time.AddDays(5), 30,  30))
            .ToList();
    }

    private static IEnumerable<News_Event_BO> Make_Demo_Event(
        string currency, string name, DateTime utc, int before, int after)
    {
        yield return new News_Event_BO
        {
            Event_Id               = News_Event_BO.Stable_Id(utc, currency, name),
            Event_UTC              = utc,
            Currency               = currency,
            Event_Name             = name,
            Impact_Level           = "High",
            Lockout_Before_Minutes = before,
            Lockout_After_Minutes  = after
        };
    }

    private sealed class Calendar_Feed_Item
    {
        // DateTimeOffset handles both plain UTC strings and strings with timezone offsets
        [JsonPropertyName("date")]
        public DateTimeOffset Date { get; set; }

        // ForexFactory uses "country" for the currency code; other feeds use "currency"
        [JsonPropertyName("country")]
        public string? Country { get; set; }

        [JsonPropertyName("currency")]
        public string? Currency { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("impact")]
        public string? Impact { get; set; }
    }
}
