using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;
using Atlas_Domain.Interfaces;

namespace Atlas_Risk.Services;

public class Drawdown_Guard
{
    private readonly Risk_Setting_BO _settings;
    private readonly I_Emergency_Stop_Service _emergency_stop;

    public Drawdown_Guard(Risk_Setting_BO settings, I_Emergency_Stop_Service emergency_stop)
    {
        _settings = settings;
        _emergency_stop = emergency_stop;
    }

    public async Task<Bot_Mode_Type> Evaluate_And_Apply_Async(Account_State_BO account)
    {
        var dd = account.Drawdown_From_Peak;

        if (dd >= _settings.Full_Stop_Drawdown_Percent)
        {
            await _emergency_stop.Activate_Emergency_Stop_Async(
                $"Drawdown {dd:F2}% exceeded full-stop threshold {_settings.Full_Stop_Drawdown_Percent}%.");
            return Bot_Mode_Type.Emergency_Stop;
        }

        await _emergency_stop.Evaluate_Drawdown_Mode_Async(account, _settings);
        return _emergency_stop.Current_Mode;
    }

    // Returns the risk multiplier to apply given drawdown level
    public decimal Get_Risk_Multiplier(decimal drawdown_percent)
    {
        if (drawdown_percent >= _settings.Recovery_Drawdown_Percent)  return 0.25m; // Recovery mode
        if (drawdown_percent >= _settings.Caution_Drawdown_Percent)   return 0.50m; // Caution mode
        return 1.0m;                                                                 // Normal
    }

    public bool Is_Gold_Allowed(decimal drawdown_percent) =>
        drawdown_percent < _settings.Recovery_Drawdown_Percent;
}
