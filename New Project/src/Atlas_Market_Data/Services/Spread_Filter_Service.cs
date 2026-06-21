using Atlas_Domain.BusinessObjects;

namespace Atlas_Market_Data.Services;

public class Spread_Filter_Service
{
    public bool Is_Spread_Acceptable(decimal current_spread_pips, Market_Symbol_BO symbol)
    {
        return current_spread_pips <= symbol.Max_Allowed_Spread_Pips;
    }

    public int Get_Spread_Quality_Score(decimal current_spread_pips, Market_Symbol_BO symbol)
    {
        if (current_spread_pips <= symbol.Normal_Spread_Pips) return 15;
        if (current_spread_pips <= symbol.Normal_Spread_Pips * 1.5m) return 10;
        if (current_spread_pips <= symbol.Max_Allowed_Spread_Pips) return 5;
        return 0;
    }

    public bool Is_Rollover_Period(DateTime utc_now)
    {
        // Typical broker rollover between 21:50–22:10 UTC
        var time = utc_now.TimeOfDay;
        return time >= TimeSpan.FromHours(21).Add(TimeSpan.FromMinutes(50))
            && time <= TimeSpan.FromHours(22).Add(TimeSpan.FromMinutes(10));
    }

    public bool Is_Weekend_Open_Risk(DateTime utc_now)
    {
        // Monday before 8:00 UTC — spread risk from weekend gap
        return utc_now.DayOfWeek == DayOfWeek.Monday && utc_now.Hour < 8;
    }
}
