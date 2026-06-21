using Atlas_Domain.Enums;

namespace Atlas_Domain.BusinessObjects;

public class Strategy_Performance_BO
{
    public Strategy_Type Strategy { get; set; }
    public string Symbol_Name { get; set; } = string.Empty;
    public bool Is_Active { get; set; } = true;
    public bool Is_Live_Enabled { get; set; } = false;
    public string Disable_Reason { get; set; } = string.Empty;

    public int Total_Trades { get; set; }
    public int Winning_Trades { get; set; }
    public int Losing_Trades { get; set; }
    public decimal Win_Rate => Total_Trades > 0 ? Math.Round((decimal)Winning_Trades / Total_Trades * 100, 1) : 0;

    public decimal Gross_Profit_R { get; set; }
    public decimal Gross_Loss_R { get; set; }
    public decimal Profit_Factor => Gross_Loss_R != 0 ? Math.Round(Gross_Profit_R / Math.Abs(Gross_Loss_R), 2) : 0;

    public decimal Net_R => Gross_Profit_R + Gross_Loss_R;
    public decimal Average_R => Total_Trades > 0 ? Math.Round(Net_R / Total_Trades, 3) : 0;
    public decimal Expectancy => Average_R;

    public decimal Max_Drawdown_Percent { get; set; }
    public decimal Avg_Slippage_Pips { get; set; }

    // Rolling windows for disable logic
    public decimal Rolling_20_Expectancy { get; set; }
    public decimal Rolling_30_Profit_Factor { get; set; }

    // Strategy should be auto-disabled when rolling metrics deteriorate
    public bool Should_Auto_Disable =>
        (Total_Trades >= 20 && Rolling_20_Expectancy < 0) ||
        (Total_Trades >= 30 && Rolling_30_Profit_Factor < 1.0m);
}
