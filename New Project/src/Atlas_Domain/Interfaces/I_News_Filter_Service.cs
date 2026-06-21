using Atlas_Domain.BusinessObjects;

namespace Atlas_Domain.Interfaces;

public interface I_News_Filter_Service
{
    Task<bool> Is_News_Lockout_Active_Async(string symbol_name, bool is_gold = false);
    Task<News_Event_BO?> Get_Next_Event_Async(string currency);
    Task Refresh_Calendar_Async();
}
