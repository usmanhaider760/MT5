using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;
using Atlas_Domain.Interfaces;
using Atlas_Market_Data.Indicators;
using Atlas_Shared;

namespace Atlas_Strategy.Strategies;

/// <summary>
/// Strategy E: Controlled Mean Reversion (least frequent, used sparingly)
/// Trades overextension at major D1/Weekly zones. Never fights a strong trend day.
/// </summary>
public class Strategy_E_Mean_Reversion : I_Strategy
{
    public Strategy_Type Strategy_Type => Strategy_Type.Controlled_Mean_Reversion;

    public bool Is_Regime_Compatible(Market_Regime_Type regime) =>
        regime == Market_Regime_Type.Range;

    public Task<Trade_Signal_BO?> Generate_Signal_Async(
        string symbol_name,
        Market_Context_BO context,
        List<Candle_BO> h4_candles,
        List<Candle_BO> h1_candles,
        List<Candle_BO> m15_candles)
    {
        if (!Is_Regime_Compatible(context.Regime)) return Task.FromResult<Trade_Signal_BO?>(null);

        // Only trade mean reversion when H4 and H1 bias conflict (no clear trend)
        if (context.Higher_Timeframes_Aligned) return Task.FromResult<Trade_Signal_BO?>(null);

        var rsi    = Technical_Indicators.Calculate_RSI(h1_candles, Trading_Constants.ATR_Period);
        var atr    = Technical_Indicators.Calculate_ATR(h1_candles, Trading_Constants.ATR_Period);
        var ema20  = Technical_Indicators.Calculate_EMA(h1_candles, Trading_Constants.EMA_Fast);

        var last_m15 = m15_candles.Last();

        bool oversold  = rsi < 30 && last_m15.Low < ema20 * 0.998m && last_m15.Is_Bullish;
        bool overbought = rsi > 70 && last_m15.High > ema20 * 1.002m && last_m15.Is_Bearish;

        if (!oversold && !overbought) return Task.FromResult<Trade_Signal_BO?>(null);

        Trade_Direction_Type direction;
        decimal entry, sl, tp;

        if (oversold)
        {
            direction = Trade_Direction_Type.Buy;
            entry     = last_m15.Close;
            sl        = last_m15.Low - atr * 0.3m;
            tp        = ema20 + atr * 0.3m;

            if ((tp - entry) / (entry - sl) < 1.5m) return Task.FromResult<Trade_Signal_BO?>(null);
        }
        else
        {
            direction = Trade_Direction_Type.Sell;
            entry     = last_m15.Close;
            sl        = last_m15.High + atr * 0.3m;
            tp        = ema20 - atr * 0.3m;

            if ((entry - tp) / (sl - entry) < 1.5m) return Task.FromResult<Trade_Signal_BO?>(null);
        }

        var signal = new Trade_Signal_BO
        {
            Symbol_Name       = symbol_name,
            Strategy          = Strategy_Type.Controlled_Mean_Reversion,
            Direction         = direction,
            Entry_Price       = entry,
            Stop_Loss_Price   = sl,
            Take_Profit_Price = tp,
            Regime_At_Signal  = context.Regime,
            Session_At_Signal = context.Current_Session,
            Spread_At_Signal  = context.Current_Spread_Pips
        };

        return Task.FromResult<Trade_Signal_BO?>(signal);
    }
}
