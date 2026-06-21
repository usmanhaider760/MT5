using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;
using Atlas_Domain.Interfaces;
using Atlas_Market_Data.Indicators;
using Atlas_Shared;

namespace Atlas_Strategy.Strategies;

/// <summary>
/// Strategy D: Breakout Retest Trend Expansion
/// H1/H4 structural level breaks with strong close, then price retests and holds.
/// </summary>
public class Strategy_D_Breakout_Retest : I_Strategy
{
    public Strategy_Type Strategy_Type => Strategy_Type.Breakout_Retest_Expansion;

    public bool Is_Regime_Compatible(Market_Regime_Type regime) =>
        regime == Market_Regime_Type.Trend_Swing || regime == Market_Regime_Type.Compression_Breakout;

    public Task<Trade_Signal_BO?> Generate_Signal_Async(
        string symbol_name,
        Market_Context_BO context,
        List<Candle_BO> h4_candles,
        List<Candle_BO> h1_candles,
        List<Candle_BO> m15_candles)
    {
        if (!Is_Regime_Compatible(context.Regime)) return Task.FromResult<Trade_Signal_BO?>(null);

        var atr = Technical_Indicators.Calculate_ATR(h1_candles, Trading_Constants.ATR_Period);
        if (h4_candles.Count < 3 || m15_candles.Count < 3)
            return Task.FromResult<Trade_Signal_BO?>(null);

        // Find the last structural level on H4 (look at high of 3 candles ago as resistance)
        var key_level_high = h4_candles[^3].High;
        var key_level_low  = h4_candles[^3].Low;

        var last_m15 = m15_candles.Last();
        var prev_m15 = m15_candles[^2];

        bool bullish_retest = prev_m15.Close > key_level_high      // Prior breakout above level
                           && last_m15.Low  <= key_level_high * 1.001m // Pulled back to level
                           && last_m15.Close > key_level_high      // Held above on close
                           && last_m15.Is_Bullish;

        bool bearish_retest = prev_m15.Close < key_level_low
                           && last_m15.High  >= key_level_low * 0.999m
                           && last_m15.Close < key_level_low
                           && last_m15.Is_Bearish;

        if (!bullish_retest && !bearish_retest) return Task.FromResult<Trade_Signal_BO?>(null);

        Trade_Direction_Type direction;
        decimal entry, sl, tp;

        if (bullish_retest)
        {
            direction = Trade_Direction_Type.Buy;
            entry     = last_m15.Close;
            sl        = last_m15.Low - atr * 0.25m;
            tp        = entry + (entry - sl) * 2.0m;
        }
        else
        {
            direction = Trade_Direction_Type.Sell;
            entry     = last_m15.Close;
            sl        = last_m15.High + atr * 0.25m;
            tp        = entry - (sl - entry) * 2.0m;
        }

        var signal = new Trade_Signal_BO
        {
            Symbol_Name       = symbol_name,
            Strategy          = Strategy_Type.Breakout_Retest_Expansion,
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
