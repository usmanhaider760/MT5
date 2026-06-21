using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;

namespace Atlas_Application.Backtest;

public class Backtest_Config
{
    public string Symbol_Name { get; set; } = "EURUSD";
    public DateTime Start_Date { get; set; } = DateTime.UtcNow.AddMonths(-6);
    public DateTime End_Date   { get; set; } = DateTime.UtcNow;

    public decimal Initial_Balance { get; set; } = 10_000m;
    public Risk_Setting_BO Risk_Settings { get; set; } = Risk_Setting_BO.Conservative_Launch();

    // Execution simulation
    public decimal Spread_Pips   { get; set; } = 1.0m;    // Fixed spread applied to every order
    public decimal Slippage_Pips { get; set; } = 0.3m;    // Added to entry on market orders
    public decimal Commission_Per_Lot { get; set; } = 7m; // Round-turn per lot

    // Which strategies to include
    public List<Strategy_Type> Enabled_Strategies { get; set; } =
    [
        Strategy_Type.Trend_Pullback_Continuation,
        Strategy_Type.Session_Breakout
    ];

    // Walk-forward split: what fraction of data is in-sample
    public double In_Sample_Ratio { get; set; } = 0.7;
}
