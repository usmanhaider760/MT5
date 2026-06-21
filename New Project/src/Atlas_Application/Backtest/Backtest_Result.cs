using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;

namespace Atlas_Application.Backtest;

public class Backtest_Result
{
    public string Symbol_Name { get; set; } = string.Empty;
    public DateTime Run_Start  { get; set; }
    public DateTime Run_End    { get; set; }
    public TimeSpan Duration   => Run_End - Run_Start;

    // Equity curve: (date, equity)
    public List<(DateTime Date, decimal Equity)> Equity_Curve { get; set; } = [];

    // Trade log
    public List<Trade_Result_BO> Trades { get; set; } = [];

    // Aggregate stats
    public int    Total_Trades      { get; set; }
    public int    Winning_Trades    { get; set; }
    public int    Losing_Trades     { get; set; }
    public decimal Win_Rate         => Total_Trades > 0 ? Math.Round((decimal)Winning_Trades / Total_Trades * 100, 1) : 0;

    public decimal Gross_Profit_R   { get; set; }
    public decimal Gross_Loss_R     { get; set; }
    public decimal Net_R            => Gross_Profit_R + Gross_Loss_R;
    public decimal Avg_R            => Total_Trades > 0 ? Math.Round(Net_R / Total_Trades, 3) : 0;
    public decimal Profit_Factor    => Gross_Loss_R != 0 ? Math.Round(Gross_Profit_R / Math.Abs(Gross_Loss_R), 2) : 0;
    public decimal Expectancy       => Avg_R;

    public decimal Max_Drawdown_Percent { get; set; }
    public decimal Final_Equity     { get; set; }
    public decimal Total_Return_Percent { get; set; }

    public decimal Total_Commission { get; set; }
    public decimal Total_Slippage_Pips { get; set; }
    public int     Signals_Rejected { get; set; }
    public int     Filters_Blocked  { get; set; }

    // Per-strategy breakdown
    public Dictionary<Strategy_Type, Strategy_Backtest_Stats> Strategy_Stats { get; set; } = [];

    // Walk-forward split results
    public Backtest_Summary? In_Sample  { get; set; }
    public Backtest_Summary? Out_Sample { get; set; }

    public bool Passed_Validation =>
        Profit_Factor >= 1.20m &&
        Expectancy    >  0     &&
        Max_Drawdown_Percent < 12m &&
        Total_Trades  >= 30;
}

public class Strategy_Backtest_Stats
{
    public Strategy_Type Strategy    { get; set; }
    public int   Total_Trades        { get; set; }
    public decimal Win_Rate          { get; set; }
    public decimal Profit_Factor     { get; set; }
    public decimal Avg_R             { get; set; }
}

public class Backtest_Summary
{
    public string Label              { get; set; } = string.Empty;
    public DateTime Period_Start     { get; set; }
    public DateTime Period_End       { get; set; }
    public int   Total_Trades        { get; set; }
    public decimal Win_Rate          { get; set; }
    public decimal Profit_Factor     { get; set; }
    public decimal Avg_R             { get; set; }
    public decimal Max_Drawdown_Pct  { get; set; }
    public bool   Passed             { get; set; }
}
