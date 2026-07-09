namespace Atlas_Domain.Enums;

public enum Exit_Reason_Type
{
    Unknown,
    Take_Profit_Hit,
    Stop_Loss_Hit,
    Trailing_Stop_Hit,
    Breakeven_Stop,
    Partial_Close,
    Manual_Close,
    Emergency_Stop,
    Daily_Loss_Limit
}
