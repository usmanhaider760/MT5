using Atlas_Domain.Enums;

namespace Atlas_Domain.BusinessObjects;

public class Trade_Signal_BO
{
    public Guid Signal_Id { get; set; } = Guid.NewGuid();
    public DateTime Generated_At_UTC { get; set; } = DateTime.UtcNow;

    // Identity
    public string Symbol_Name { get; set; } = string.Empty;
    public Strategy_Type Strategy { get; set; }
    public Trade_Direction_Type Direction { get; set; }

    // Prices
    public decimal Entry_Price { get; set; }
    public decimal Stop_Loss_Price { get; set; }
    public decimal Take_Profit_Price { get; set; }

    // Derived
    public decimal Stop_Loss_Pips => Direction == Trade_Direction_Type.Buy
        ? Entry_Price - Stop_Loss_Price
        : Stop_Loss_Price - Entry_Price;

    public decimal Take_Profit_Pips => Direction == Trade_Direction_Type.Buy
        ? Take_Profit_Price - Entry_Price
        : Entry_Price - Take_Profit_Price;

    public decimal Reward_Risk_Ratio => Stop_Loss_Pips > 0
        ? Math.Round(Take_Profit_Pips / Stop_Loss_Pips, 2)
        : 0;

    // Quality
    public int Quality_Score { get; set; }
    public Trade_Quality_Grade Quality_Grade { get; set; }

    // Context snapshot
    public Market_Regime_Type Regime_At_Signal { get; set; }
    public Session_Type Session_At_Signal { get; set; }
    public decimal Spread_At_Signal { get; set; }

    // Validation
    public bool Is_Approved { get; set; }
    public Signal_Reject_Reason Reject_Reason { get; set; }
    public string Reject_Detail { get; set; } = string.Empty;
}
