using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;

namespace Atlas_Domain.Interfaces;

public interface I_Emergency_Stop_Service
{
    bool Kill_Switch_Active { get; }
    Bot_Mode_Type Current_Mode { get; }
    Task Activate_Emergency_Stop_Async(string reason);
    Task Evaluate_Drawdown_Mode_Async(Account_State_BO account, Risk_Setting_BO risk_settings);
}
