using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;
using Atlas_Domain.Interfaces;

namespace Atlas_Execution.Services;

public class Emergency_Stop_Service : I_Emergency_Stop_Service
{
    public bool Kill_Switch_Active { get; private set; }
    public Bot_Mode_Type Current_Mode { get; private set; } = Bot_Mode_Type.Demo;

    public event Action<string>? On_Emergency_Stop;
    public event Action<Bot_Mode_Type>? On_Mode_Changed;

    public Task Activate_Emergency_Stop_Async(string reason)
    {
        Kill_Switch_Active = true;
        Current_Mode = Bot_Mode_Type.Emergency_Stop;
        On_Emergency_Stop?.Invoke(reason);
        return Task.CompletedTask;
    }

    public Task Evaluate_Drawdown_Mode_Async(Account_State_BO account, Risk_Setting_BO risk_settings)
    {
        if (Kill_Switch_Active) return Task.CompletedTask;

        var dd = account.Drawdown_From_Peak;
        Bot_Mode_Type new_mode;

        if (dd >= risk_settings.Protection_Drawdown_Percent)
            new_mode = Bot_Mode_Type.Emergency_Stop;
        else if (dd >= risk_settings.Recovery_Drawdown_Percent)
            new_mode = Bot_Mode_Type.Micro_Live; // Reduced risk mode
        else
            new_mode = risk_settings.Mode; // Drawdown has recovered — return to the configured base mode

        if (new_mode != Current_Mode)
        {
            Current_Mode = new_mode;
            On_Mode_Changed?.Invoke(new_mode);
        }

        return Task.CompletedTask;
    }

    public void Reset_Kill_Switch()
    {
        Kill_Switch_Active = false;
        Current_Mode = Bot_Mode_Type.Demo;
    }
}
