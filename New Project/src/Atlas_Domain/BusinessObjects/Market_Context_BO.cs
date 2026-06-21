using Atlas_Domain.Enums;

namespace Atlas_Domain.BusinessObjects;

public class Market_Context_BO
{
    public string Symbol_Name { get; set; } = string.Empty;
    public DateTime Evaluated_At_UTC { get; set; }

    // Regime
    public Market_Regime_Type Regime { get; set; }
    public int Regime_Quality_Score { get; set; }      // 0–100

    // Timeframe bias
    public Trade_Direction_Type D1_Bias { get; set; }
    public Trade_Direction_Type H4_Bias { get; set; }
    public Trade_Direction_Type H1_Bias { get; set; }
    public bool Higher_Timeframes_Aligned => D1_Bias == H4_Bias && H4_Bias == H1_Bias && D1_Bias != Trade_Direction_Type.None;

    // Session
    public Session_Type Current_Session { get; set; }

    // Spread & volatility
    public decimal Current_Spread_Pips { get; set; }
    public decimal ATR_14 { get; set; }
    public decimal ATR_Average_14d { get; set; }
    public bool Spread_Is_Normal { get; set; }
    public bool Volatility_Is_Normal { get; set; }

    // News safety
    public bool News_Lockout_Active { get; set; }
    public string? Next_High_Impact_Event { get; set; }
    public DateTime? Next_Event_UTC { get; set; }

    // EMA values (H4 timeframe)
    public decimal EMA_20 { get; set; }
    public decimal EMA_50 { get; set; }
    public decimal EMA_200 { get; set; }

    // Score sub-components (per the methodology)
    public int Score_Trend_Alignment { get; set; }    // 0–20
    public int Score_Volatility_Quality { get; set; } // 0–15
    public int Score_Session_Quality { get; set; }    // 0–15
    public int Score_Spread_Quality { get; set; }     // 0–15
    public int Score_Structure_Clarity { get; set; }  // 0–15
    public int Score_News_Safety { get; set; }        // 0–10
    public int Score_Correlation_Safety { get; set; } // 0–10

    public bool Is_Tradeable => Regime_Quality_Score >= 75 && !News_Lockout_Active && Spread_Is_Normal;
    public bool Is_Gold_Tradeable => Regime_Quality_Score >= 80 && !News_Lockout_Active && Spread_Is_Normal;
}
