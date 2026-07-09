using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Interfaces;
using Atlas_Shared;

namespace Atlas_Market_Data.Services;

public class News_Filter_Service : I_News_Filter_Service
{
    private readonly List<News_Event_BO> _calendar = [];
    private readonly Economic_Calendar_Service? _calendar_service;
    private static readonly Dictionary<string, string[]> Symbol_Currencies = new()
    {
        ["EURUSD"] = ["EUR", "USD"],
        ["GBPUSD"] = ["GBP", "USD"],
        ["USDJPY"] = ["USD", "JPY"],
        ["USDCHF"] = ["USD", "CHF"],
        ["AUDUSD"] = ["AUD", "USD"],
        ["USDCAD"] = ["USD", "CAD"],
        ["NZDUSD"] = ["NZD", "USD"],
        ["XAUUSD"] = ["USD", "XAU"]
    };

    public News_Filter_Service(Economic_Calendar_Service? calendar_service = null)
    {
        _calendar_service = calendar_service;
    }

    public async Task<bool> Is_News_Lockout_Active_Async(string symbol_name, bool is_gold = false)
    {
        var now = DateTime.UtcNow;
        var currencies = Get_Currencies(symbol_name);

        List<News_Event_BO> events;
        if (_calendar_service != null)
            events = await _calendar_service.Get_High_Impact_Events_Async(24);
        else
            events = _calendar.Where(e => e.Is_High_Impact).ToList();

        foreach (var evt in events)
        {
            if (!currencies.Contains(evt.Currency)) continue;
            var lockout = Get_Lockout_Window(evt, is_gold);
            if (now >= evt.Event_UTC.AddMinutes(-lockout.before) && now <= evt.Event_UTC.AddMinutes(lockout.after))
                return true;
        }
        return false;
    }

    public Task<News_Event_BO?> Get_Next_Event_Async(string currency)
    {
        var now = DateTime.UtcNow;
        var next = _calendar
            .Where(e => e.Currency == currency && e.Is_High_Impact && e.Event_UTC > now)
            .OrderBy(e => e.Event_UTC)
            .FirstOrDefault();
        return Task.FromResult(next);
    }

    public Task Refresh_Calendar_Async()
    {
        // In production this fetches from an economic calendar API.
        // During dev, seed with known events.
        return Task.CompletedTask;
    }

    public void Seed_Event(News_Event_BO evt) => _calendar.Add(evt);

    private static string[] Get_Currencies(string symbol) =>
        Symbol_Currencies.TryGetValue(symbol.ToUpper(), out var c) ? c : ["USD"];

    private static (int before, int after) Get_Lockout_Window(News_Event_BO evt, bool is_gold)
    {
        bool is_major = evt.Event_Name.Contains("NFP", StringComparison.OrdinalIgnoreCase)
                     || evt.Event_Name.Contains("Non-Farm", StringComparison.OrdinalIgnoreCase);
        bool is_fomc  = evt.Event_Name.Contains("FOMC", StringComparison.OrdinalIgnoreCase)
                     || evt.Event_Name.Contains("Rate Decision", StringComparison.OrdinalIgnoreCase);
        bool is_cpi   = evt.Event_Name.Contains("CPI", StringComparison.OrdinalIgnoreCase)
                     || evt.Event_Name.Contains("Inflation", StringComparison.OrdinalIgnoreCase);

        if (is_gold)
        {
            if (is_fomc) return (Trading_Constants.News_Gold_FOMC_Before, Trading_Constants.News_Gold_FOMC_After);
            if (is_major || is_cpi) return (Trading_Constants.News_Gold_NFP_Before, Trading_Constants.News_Gold_NFP_After);
            return (Trading_Constants.News_Gold_Standard_Before, Trading_Constants.News_Gold_Standard_After);
        }

        if (is_fomc)  return (Trading_Constants.News_FOMC_Before, Trading_Constants.News_FOMC_After);
        if (is_major) return (Trading_Constants.News_NFP_Before, Trading_Constants.News_NFP_After);
        if (is_cpi)   return (Trading_Constants.News_CPI_Before, Trading_Constants.News_CPI_After);
        return (Trading_Constants.News_Standard_Before, Trading_Constants.News_Standard_After);
    }
}
