using Atlas_Domain.Enums;

namespace Atlas_Domain.BusinessObjects;

public class Risk_Setting_BO
{
    public Bot_Mode_Type Mode { get; set; } = Bot_Mode_Type.Demo;

    // Per-trade risk (% of account equity)
    public decimal Forex_Risk_Per_Trade_Percent { get; set; } = 0.25m;
    public decimal Gold_Risk_Per_Trade_Percent { get; set; } = 0.20m;

    // Daily / weekly drawdown stops
    public decimal Max_Daily_Loss_Percent { get; set; } = 1.0m;
    public decimal Max_Weekly_Loss_Percent { get; set; } = 2.0m;
    public decimal Account_Drawdown_Circuit_Breaker_Percent { get; set; } = 5.0m;

    // Portfolio limits
    public int Max_Open_Trades { get; set; } = 2;
    public int Max_Active_Forex_Symbols { get; set; } = 3;
    public int Max_Gold_Trades { get; set; } = 1;
    public int Max_Consecutive_Losses_Before_Pause { get; set; } = 2;

    // Reward-to-risk minimums
    public decimal Min_Reward_Risk_Forex { get; set; } = 1.8m;
    public decimal Min_Reward_Risk_Gold_Swing { get; set; } = 2.0m;
    public decimal Min_Reward_Risk_Intraday { get; set; } = 1.5m;

    // Trade quality gate
    public int Min_Quality_Score_Live { get; set; } = 85;
    public int Min_Quality_Score_Gold_Live { get; set; } = 85;

    // Drawdown recovery thresholds
    public decimal Caution_Drawdown_Percent { get; set; } = 2.0m;
    public decimal Recovery_Drawdown_Percent { get; set; } = 4.0m;
    public decimal Protection_Drawdown_Percent { get; set; } = 6.0m;
    public decimal Full_Stop_Drawdown_Percent { get; set; } = 8.0m;

    // Absolute lot size ceilings — a hard cap regardless of computed risk-based size
    public decimal Max_Lot_Size_Forex { get; set; } = 5.0m;
    public decimal Max_Lot_Size_Gold { get; set; } = 1.0m;

    public static Risk_Setting_BO Conservative_Launch() => new()
    {
        Mode = Bot_Mode_Type.Demo,
        Forex_Risk_Per_Trade_Percent = 0.25m,
        Gold_Risk_Per_Trade_Percent = 0.15m,
        Max_Daily_Loss_Percent = 0.75m,
        Max_Weekly_Loss_Percent = 2.0m,
        Account_Drawdown_Circuit_Breaker_Percent = 5.0m,
        Max_Open_Trades = 2,
        Max_Consecutive_Losses_Before_Pause = 2
    };
}
