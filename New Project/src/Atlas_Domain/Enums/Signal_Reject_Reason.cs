namespace Atlas_Domain.Enums;

public enum Signal_Reject_Reason
{
    None,
    Low_Quality_Score,
    Spread_Too_High,
    News_Lockout_Active,
    Volatility_Abnormal,
    Volatility_Too_Low,
    Session_Not_Active,
    Regime_Mismatch,
    Strategy_Disabled,
    Daily_Loss_Limit_Reached,
    Weekly_Loss_Limit_Reached,
    Drawdown_Limit_Reached,
    Max_Open_Trades_Reached,
    Correlation_Exposure_Exceeded,
    Reward_Risk_Insufficient,
    Stop_Loss_Too_Wide,
    Minimum_Lot_Below_Broker_Minimum,
    Higher_Timeframe_Bias_Unclear,
    No_Valid_Entry_Trigger,
    Gold_Regime_Score_Below_Threshold,
    Max_Gold_Trades_Reached,
    Consecutive_Loss_Pause,
    Same_Symbol_Already_Open,
    Ai_Filter_Rejected
}
