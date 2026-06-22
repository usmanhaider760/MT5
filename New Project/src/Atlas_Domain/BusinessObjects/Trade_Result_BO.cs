using Atlas_Domain.Enums;

namespace Atlas_Domain.BusinessObjects;

public class Trade_Result_BO
{
    public Guid Signal_Id { get; set; }
    public long Broker_Ticket { get; set; }
    public string Symbol_Name { get; set; } = string.Empty;
    public Strategy_Type Strategy { get; set; }
    public Market_Regime_Type Regime_At_Entry { get; set; }
    public Session_Type Session_At_Entry { get; set; }
    public Trade_Direction_Type Direction { get; set; }

    public decimal Lot_Size { get; set; }
    public decimal Entry_Price { get; set; }
    public decimal Exit_Price { get; set; }
    public decimal Stop_Loss_Price { get; set; }
    public decimal Take_Profit_Price { get; set; }

    public DateTime Opened_At_UTC { get; set; }
    public DateTime Closed_At_UTC { get; set; }

    public decimal Gross_PnL_Currency { get; set; }
    public decimal Commission { get; set; }
    public decimal Swap { get; set; }
    public decimal Slippage_Pips { get; set; }
    public decimal Net_PnL_Currency => Gross_PnL_Currency - Commission - Math.Abs(Swap);

    public decimal Initial_Stop_Distance_Pips { get; set; }
    public decimal R_Multiple => Initial_Stop_Distance_Pips > 0
        ? Net_PnL_Currency / (Initial_Stop_Distance_Pips * Lot_Size * 10m)
        : 0;

    public bool Is_Winner => Net_PnL_Currency > 0;
    public string Close_Reason { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}
