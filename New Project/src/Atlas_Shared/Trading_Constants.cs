namespace Atlas_Shared;

public static class Trading_Constants
{
    // Regime quality thresholds
    public const int Regime_Score_Tradeable = 75;
    public const int Regime_Score_Gold_Tradeable = 80;
    public const int Regime_Score_Demo_Only = 60;

    // News lockout windows (minutes)
    public const int News_Standard_Before = 30;
    public const int News_Standard_After = 30;
    public const int News_CPI_Before = 60;
    public const int News_CPI_After = 60;
    public const int News_NFP_Before = 60;
    public const int News_NFP_After = 90;
    public const int News_FOMC_Before = 120;
    public const int News_FOMC_After = 120;

    public const int News_Gold_Standard_Before = 45;
    public const int News_Gold_Standard_After = 45;
    public const int News_Gold_CPI_Before = 120;
    public const int News_Gold_CPI_After = 120;
    public const int News_Gold_NFP_Before = 120;
    public const int News_Gold_NFP_After = 120;
    public const int News_Gold_FOMC_Before = 180;
    public const int News_Gold_FOMC_After = 180;

    // Session times (UTC)
    public static readonly TimeOnly Asian_Open = new(23, 0);
    public static readonly TimeOnly Asian_Close = new(8, 0);
    public static readonly TimeOnly London_Open = new(7, 0);
    public static readonly TimeOnly London_Close = new(16, 0);
    public static readonly TimeOnly NY_Open = new(12, 0);
    public static readonly TimeOnly NY_Close = new(21, 0);

    // Portfolio limits
    public const int Max_Active_Forex_Symbols = 3;
    public const int Max_Gold_Trades = 1;
    public const int Max_Total_Open_Trades_Normal = 3;

    // EMA periods
    public const int EMA_Fast = 20;
    public const int EMA_Medium = 50;
    public const int EMA_Slow = 200;

    // ATR period
    public const int ATR_Period = 14;

    // Minimum trades before performance evaluation
    public const int Min_Trades_For_Evaluation = 20;
    public const int Trades_For_Profit_Factor_Check = 30;

    // R-multiple targets per strategy type
    public const decimal Min_RR_Forex_Trend = 1.8m;
    public const decimal Min_RR_Gold_Swing = 2.0m;
    public const decimal Min_RR_Intraday = 1.5m;

    // ATR multiplier for volatility abnormality detection
    public const decimal ATR_High_Multiplier = 2.0m;
    public const decimal ATR_Low_Multiplier = 0.3m;
}
