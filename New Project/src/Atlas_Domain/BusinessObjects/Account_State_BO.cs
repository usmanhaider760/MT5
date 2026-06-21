namespace Atlas_Domain.BusinessObjects;

public class Account_State_BO
{
    public DateTime Snapshot_UTC { get; set; } = DateTime.UtcNow;
    public decimal Balance { get; set; }
    public decimal Equity { get; set; }
    public decimal Margin_Used { get; set; }
    public decimal Free_Margin { get; set; }
    public decimal Margin_Level_Percent { get; set; }

    public decimal Session_High_Equity { get; set; }
    public decimal Day_Open_Balance { get; set; }
    public decimal Week_Open_Balance { get; set; }
    public decimal Peak_Equity { get; set; }

    public decimal Daily_PnL => Equity - Day_Open_Balance;
    public decimal Weekly_PnL => Equity - Week_Open_Balance;
    public decimal Drawdown_From_Peak => Peak_Equity > 0
        ? Math.Round((Peak_Equity - Equity) / Peak_Equity * 100m, 2)
        : 0;

    public int Open_Trade_Count { get; set; }
    public int Consecutive_Losses { get; set; }
    public bool Is_Daily_Loss_Limit_Hit { get; set; }
    public bool Is_Weekly_Loss_Limit_Hit { get; set; }
    public bool Is_Drawdown_Limit_Hit { get; set; }
}
