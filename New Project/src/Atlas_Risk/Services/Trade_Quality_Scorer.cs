using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;
using Atlas_Domain.Interfaces;

namespace Atlas_Risk.Services;

public class Trade_Quality_Scorer : I_Trade_Quality_Scorer
{
    public (int Score, Trade_Quality_Grade Grade) Score_Trade(Trade_Signal_BO signal, Market_Context_BO context)
    {
        int score = 0;

        // 1. Higher-timeframe bias (0–20)
        score += Score_Bias(signal, context);

        // 2. Market regime match (0–15)
        score += Score_Regime_Match(signal, context);

        // 3. Clean technical level — inferred from signal quality (0–15)
        // Strategy generated a signal at a meaningful level; base score on R:R and direction
        score += signal.Reward_Risk_Ratio >= 2.0m ? 15 : signal.Reward_Risk_Ratio >= 1.5m ? 10 : 5;

        // 4. Entry confirmation — non-zero score implies trigger was found (0–15)
        score += 12; // Strategies only emit signals after confirmation rules pass

        // 5. Reward-to-risk quality (0–15)
        score += Score_RR(signal);

        // 6. Spread/execution quality (0–10)
        score += context.Score_Spread_Quality >= 10 ? 10 : context.Score_Spread_Quality >= 5 ? 5 : 0;

        // 7. News safety (0–5)
        score += context.News_Lockout_Active ? 0 : 5;

        // 8. Correlation safety (0–5)
        score += context.Score_Correlation_Safety >= 8 ? 5 : 2;

        score = Math.Min(score, 100);

        var grade = score >= 85 ? Trade_Quality_Grade.Live_Ready
                  : score >= 75 ? Trade_Quality_Grade.Demo_Only
                  : score >= 60 ? Trade_Quality_Grade.Watch_Only
                  :               Trade_Quality_Grade.Rejected;

        return (score, grade);
    }

    private static int Score_Bias(Trade_Signal_BO signal, Market_Context_BO context)
    {
        bool d1_match = context.D1_Bias == signal.Direction || context.D1_Bias == Trade_Direction_Type.None;
        bool h4_match = context.H4_Bias == signal.Direction || context.H4_Bias == Trade_Direction_Type.None;
        bool h1_match = context.H1_Bias == signal.Direction || context.H1_Bias == Trade_Direction_Type.None;

        return (d1_match ? 7 : 0) + (h4_match ? 8 : 0) + (h1_match ? 5 : 0);
    }

    private static int Score_Regime_Match(Trade_Signal_BO signal, Market_Context_BO context)
    {
        bool match = (signal.Strategy, context.Regime) switch
        {
            (Strategy_Type.Trend_Pullback_Continuation,  Market_Regime_Type.Trend_Swing)          => true,
            (Strategy_Type.Breakout_Retest_Expansion,    Market_Regime_Type.Trend_Swing)          => true,
            (Strategy_Type.Session_Breakout,             Market_Regime_Type.Compression_Breakout) => true,
            (Strategy_Type.Liquidity_Sweep_Reversal,     Market_Regime_Type.Range)                => true,
            (Strategy_Type.Controlled_Mean_Reversion,    Market_Regime_Type.Range)                => true,
            _ => false
        };
        return match ? 15 : 0;
    }

    private static int Score_RR(Trade_Signal_BO signal)
    {
        return signal.Reward_Risk_Ratio >= 2.5m ? 15
             : signal.Reward_Risk_Ratio >= 2.0m ? 12
             : signal.Reward_Risk_Ratio >= 1.8m ? 10
             : signal.Reward_Risk_Ratio >= 1.5m ? 6
             : 0;
    }
}
