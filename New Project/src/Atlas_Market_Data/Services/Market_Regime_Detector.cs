using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;
using Atlas_Domain.Interfaces;
using Atlas_Market_Data.Indicators;
using Atlas_Shared;

namespace Atlas_Market_Data.Services;

public class Market_Regime_Detector : I_Market_Regime_Detector
{
    public Task<Market_Context_BO> Detect_Market_Regime_Async(
        string symbol_name,
        List<Candle_BO> d1_candles,
        List<Candle_BO> h4_candles,
        List<Candle_BO> h1_candles)
    {
        var ctx = new Market_Context_BO
        {
            Symbol_Name = symbol_name,
            Evaluated_At_UTC = DateTime.UtcNow
        };

        if (d1_candles.Count < 20 || h4_candles.Count < 50 || h1_candles.Count < 20)
        {
            ctx.Regime = Market_Regime_Type.Abnormal_No_Trade;
            return Task.FromResult(ctx);
        }

        Compute_EMA_Values(ctx, h4_candles);
        Compute_ATR(ctx, h4_candles);
        Compute_Bias(ctx, d1_candles, h4_candles, h1_candles);
        Score_Regime(ctx, h4_candles, h1_candles);
        Classify_Regime(ctx, h4_candles);

        return Task.FromResult(ctx);
    }

    private static void Compute_EMA_Values(Market_Context_BO ctx, List<Candle_BO> h4)
    {
        ctx.EMA_20  = Technical_Indicators.Calculate_EMA(h4, Trading_Constants.EMA_Fast);
        ctx.EMA_50  = Technical_Indicators.Calculate_EMA(h4, Trading_Constants.EMA_Medium);
        ctx.EMA_200 = Technical_Indicators.Calculate_EMA(h4, Trading_Constants.EMA_Slow);
    }

    private static void Compute_ATR(Market_Context_BO ctx, List<Candle_BO> h4)
    {
        ctx.ATR_14 = Technical_Indicators.Calculate_ATR(h4, Trading_Constants.ATR_Period);
        ctx.ATR_Average_14d = ctx.ATR_14; // Refined by market data layer with rolling average
        ctx.Volatility_Is_Normal = ctx.ATR_14 > ctx.ATR_Average_14d * Trading_Constants.ATR_Low_Multiplier
                                && ctx.ATR_14 < ctx.ATR_Average_14d * Trading_Constants.ATR_High_Multiplier;
    }

    private static void Compute_Bias(Market_Context_BO ctx, List<Candle_BO> d1, List<Candle_BO> h4, List<Candle_BO> h1)
    {
        var last_d1 = d1.Last();
        ctx.D1_Bias = last_d1.Close > Technical_Indicators.Calculate_EMA(d1, 200)
            ? Trade_Direction_Type.Buy : Trade_Direction_Type.Sell;

        var last_h4 = h4.Last();
        bool h4_above_ema200 = last_h4.Close > ctx.EMA_200;
        bool h4_hh_hl = Technical_Indicators.Is_Higher_High_Higher_Low(h4, 20);
        bool h4_lh_ll = Technical_Indicators.Is_Lower_High_Lower_Low(h4, 20);

        ctx.H4_Bias = h4_above_ema200 && h4_hh_hl ? Trade_Direction_Type.Buy
                    : !h4_above_ema200 && h4_lh_ll ? Trade_Direction_Type.Sell
                    : Trade_Direction_Type.None;

        bool h1_hh_hl = Technical_Indicators.Is_Higher_High_Higher_Low(h1, 15);
        bool h1_lh_ll = Technical_Indicators.Is_Lower_High_Lower_Low(h1, 15);
        var h1_ema50 = Technical_Indicators.Calculate_EMA(h1, 50);

        ctx.H1_Bias = h1.Last().Close > h1_ema50 && h1_hh_hl ? Trade_Direction_Type.Buy
                    : h1.Last().Close < h1_ema50 && h1_lh_ll ? Trade_Direction_Type.Sell
                    : Trade_Direction_Type.None;
    }

    private static void Score_Regime(Market_Context_BO ctx, List<Candle_BO> h4, List<Candle_BO> h1)
    {
        // Trend alignment (0–20)
        int trend_score = 0;
        if (ctx.D1_Bias == ctx.H4_Bias && ctx.H4_Bias != Trade_Direction_Type.None) trend_score += 10;
        if (ctx.H4_Bias == ctx.H1_Bias && ctx.H1_Bias != Trade_Direction_Type.None) trend_score += 10;
        ctx.Score_Trend_Alignment = trend_score;

        // Volatility quality (0–15)
        ctx.Score_Volatility_Quality = ctx.Volatility_Is_Normal ? 12 : 4;

        // Session quality (0–15) — populated by Session_Filter_Service later
        ctx.Score_Session_Quality = 10; // Default; overridden externally

        // Spread quality (0–15) — populated by Spread_Filter_Service later
        ctx.Score_Spread_Quality = ctx.Spread_Is_Normal ? 12 : 3;

        // Structure clarity (0–15)
        bool h4_clear = Technical_Indicators.Is_Higher_High_Higher_Low(h4, 20)
                     || Technical_Indicators.Is_Lower_High_Lower_Low(h4, 20);
        bool h1_clear = Technical_Indicators.Is_Higher_High_Higher_Low(h1, 15)
                     || Technical_Indicators.Is_Lower_High_Lower_Low(h1, 15);
        ctx.Score_Structure_Clarity = (h4_clear ? 8 : 0) + (h1_clear ? 7 : 0);

        // News safety (0–10) — populated by News_Filter_Service later
        ctx.Score_News_Safety = ctx.News_Lockout_Active ? 0 : 10;

        // Correlation safety (0–10) — populated by Risk_Manager later
        ctx.Score_Correlation_Safety = 10;

        ctx.Regime_Quality_Score =
            ctx.Score_Trend_Alignment +
            ctx.Score_Volatility_Quality +
            ctx.Score_Session_Quality +
            ctx.Score_Spread_Quality +
            ctx.Score_Structure_Clarity +
            ctx.Score_News_Safety +
            ctx.Score_Correlation_Safety;
    }

    private static void Classify_Regime(Market_Context_BO ctx, List<Candle_BO> h4)
    {
        if (ctx.News_Lockout_Active || !ctx.Volatility_Is_Normal)
        {
            ctx.Regime = Market_Regime_Type.Abnormal_No_Trade;
            return;
        }

        bool has_trend = ctx.Higher_Timeframes_Aligned;
        bool structure_clear = ctx.Score_Structure_Clarity >= 10;

        var (range_high, range_low) = Technical_Indicators.Get_Range(h4, 20);
        decimal range_size = range_high - range_low;
        bool is_compressed = range_size < ctx.ATR_14 * 2;

        ctx.Regime = has_trend && structure_clear ? Market_Regime_Type.Trend_Swing
                   : is_compressed               ? Market_Regime_Type.Compression_Breakout
                   :                               Market_Regime_Type.Range;
    }
}
