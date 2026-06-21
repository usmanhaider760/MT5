using Atlas_Domain.Enums;

namespace Atlas_Domain.BusinessObjects;

public class Market_Symbol_BO
{
    public string Symbol_Name { get; set; } = string.Empty;
    public Symbol_Tier_Type Tier { get; set; }
    public bool Is_Gold { get; set; }
    public bool Is_Enabled { get; set; } = true;
    public decimal Normal_Spread_Pips { get; set; }
    public decimal Max_Allowed_Spread_Pips { get; set; }
    public decimal Pip_Value_Per_Lot { get; set; }
    public int Decimal_Places { get; set; }
    public decimal Min_Lot_Size { get; set; }
    public decimal Lot_Step { get; set; }
    public int Max_Simultaneous_Trades { get; set; } = 1;
    public List<Strategy_Type> Allowed_Strategies { get; set; } = [];

    public static List<Market_Symbol_BO> Default_Universe() =>
    [
        new() { Symbol_Name="EURUSD", Tier=Symbol_Tier_Type.Tier_1, Normal_Spread_Pips=0.8m,  Max_Allowed_Spread_Pips=2.0m,  Pip_Value_Per_Lot=10m, Decimal_Places=5 },
        new() { Symbol_Name="GBPUSD", Tier=Symbol_Tier_Type.Tier_1, Normal_Spread_Pips=1.0m,  Max_Allowed_Spread_Pips=2.5m,  Pip_Value_Per_Lot=10m, Decimal_Places=5 },
        new() { Symbol_Name="USDJPY", Tier=Symbol_Tier_Type.Tier_1, Normal_Spread_Pips=0.7m,  Max_Allowed_Spread_Pips=2.0m,  Pip_Value_Per_Lot=9m,  Decimal_Places=3 },
        new() { Symbol_Name="XAUUSD", Tier=Symbol_Tier_Type.Tier_1, Normal_Spread_Pips=20m,   Max_Allowed_Spread_Pips=50m,   Pip_Value_Per_Lot=100m,Decimal_Places=2, Is_Gold=true },
        new() { Symbol_Name="AUDUSD", Tier=Symbol_Tier_Type.Tier_2, Normal_Spread_Pips=0.9m,  Max_Allowed_Spread_Pips=2.5m,  Pip_Value_Per_Lot=10m, Decimal_Places=5 },
        new() { Symbol_Name="USDCAD", Tier=Symbol_Tier_Type.Tier_2, Normal_Spread_Pips=1.2m,  Max_Allowed_Spread_Pips=3.0m,  Pip_Value_Per_Lot=10m, Decimal_Places=5 },
        new() { Symbol_Name="USDCHF", Tier=Symbol_Tier_Type.Tier_2, Normal_Spread_Pips=1.0m,  Max_Allowed_Spread_Pips=2.5m,  Pip_Value_Per_Lot=10m, Decimal_Places=5 },
        new() { Symbol_Name="NZDUSD", Tier=Symbol_Tier_Type.Tier_3, Normal_Spread_Pips=1.5m,  Max_Allowed_Spread_Pips=3.5m,  Pip_Value_Per_Lot=10m, Decimal_Places=5 },
    ];
}
