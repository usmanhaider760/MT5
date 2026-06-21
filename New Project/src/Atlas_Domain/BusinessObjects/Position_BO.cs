using Atlas_Domain.Enums;

namespace Atlas_Domain.BusinessObjects;

public class Position_BO
{
    public Guid Signal_Id { get; set; }
    public long Broker_Ticket { get; set; }
    public string Symbol_Name { get; set; } = string.Empty;
    public Strategy_Type Strategy { get; set; }
    public Trade_Direction_Type Direction { get; set; }

    public decimal Lot_Size { get; set; }
    public decimal Entry_Price { get; set; }
    public decimal Stop_Loss_Price { get; set; }
    public decimal Take_Profit_Price { get; set; }
    public decimal Current_Price { get; set; }

    public decimal Risk_Amount_Currency { get; set; }
    public decimal Risk_Percent { get; set; }
    public decimal Initial_Stop_Distance_Pips { get; set; }

    public DateTime Opened_At_UTC { get; set; }
    public Order_Status_Type Status { get; set; }

    public bool Is_At_Breakeven { get; set; }
    public bool Partial_Profit_Taken { get; set; }

    public decimal Unrealized_PnL { get; set; }
    public decimal R_Multiple_Current =>
        Initial_Stop_Distance_Pips > 0
            ? (Direction == Trade_Direction_Type.Buy
                ? Current_Price - Entry_Price
                : Entry_Price - Current_Price) / Initial_Stop_Distance_Pips
            : 0;
}
