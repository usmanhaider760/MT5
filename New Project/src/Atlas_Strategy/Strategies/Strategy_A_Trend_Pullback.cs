using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;
using Atlas_Domain.Interfaces;
using Atlas_Market_Data.Indicators;
using Atlas_Shared;

namespace Atlas_Strategy.Strategies;

/// <summary>
/// Strategy A: Trend Pullback Continuation
/// Trades pullbacks into EMA 20/50 or structure during a confirmed higher-timeframe trend.
/// </summary>
public class Strategy_A_Trend_Pullback : I_Strategy
{
    public Strategy_Type Strategy_Type => Strategy_Type.Trend_Pullback_Continuation;

    public bool Is_Regime_Compatible(Market_Regime_Type regime) =>
        regime == Market_Regime_Type.Trend_Swing;

    public Task<Trade_Signal_BO?> Generate_Signal_Async(
        string symbol_name,
        Market_Context_BO context,
        List<Candle_BO> h4_candles,
        List<Candle_BO> h1_candles,
        List<Candle_BO> m15_candles)
    {
        if (!Is_Regime_Compatible(context.Regime)) return Task.FromResult<Trade_Signal_BO?>(null);
        if (!context.Higher_Timeframes_Aligned)     return Task.FromResult<Trade_Signal_BO?>(null);

        var direction = context.H4_Bias;
        if (direction == Trade_Direction_Type.None) return Task.FromResult<Trade_Signal_BO?>(null);

        var ema20_h1 = Technical_Indicators.Calculate_EMA(h1_candles, Trading_Constants.EMA_Fast);
        var ema50_h1 = Technical_Indicators.Calculate_EMA(h1_candles, Trading_Constants.EMA_Medium);

        var last_m15 = m15_candles.Last();
        var prev_m15 = m15_candles[^2];
        var atr = Technical_Indicators.Calculate_ATR(h1_candles, Trading_Constants.ATR_Period);

        bool pullback_to_ema;
        Trade_Signal_BO? signal = null;

        if (direction == Trade_Direction_Type.Buy)
        {
            // Price must be above EMA200 H4, pulling back into EMA20/50 on H1
            pullback_to_ema = last_m15.Low <= ema50_h1 * 1.001m && last_m15.Close > ema20_h1;
            bool rejection  = last_m15.Is_Bullish && last_m15.Body_Size > last_m15.Lower_Wick;

            if (pullback_to_ema && rejection)
            {
                decimal entry = last_m15.Close;
                decimal sl    = last_m15.Low - atr * 0.3m;
                decimal tp    = entry + (entry - sl) * 2.0m;

                signal = Build_Signal(symbol_name, direction, entry, sl, tp, context);
            }
        }
        else // Sell
        {
            pullback_to_ema = last_m15.High >= ema50_h1 * 0.999m && last_m15.Close < ema20_h1;
            bool rejection  = last_m15.Is_Bearish && last_m15.Body_Size > last_m15.Upper_Wick;

            if (pullback_to_ema && rejection)
            {
                decimal entry = last_m15.Close;
                decimal sl    = last_m15.High + atr * 0.3m;
                decimal tp    = entry - (sl - entry) * 2.0m;

                signal = Build_Signal(symbol_name, direction, entry, sl, tp, context);
            }
        }

        return Task.FromResult(signal);
    }

    private static Trade_Signal_BO Build_Signal(
        string symbol, Trade_Direction_Type direction,
        decimal entry, decimal sl, decimal tp,
        Market_Context_BO context) => new()
    {
        Symbol_Name          = symbol,
        Strategy             = Strategy_Type.Trend_Pullback_Continuation,
        Direction            = direction,
        Entry_Price          = entry,
        Stop_Loss_Price      = sl,
        Take_Profit_Price    = tp,
        Regime_At_Signal     = context.Regime,
        Session_At_Signal    = context.Current_Session,
        Spread_At_Signal     = context.Current_Spread_Pips
    };
}
