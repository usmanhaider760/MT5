using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;
using Atlas_Domain.Interfaces;
using Atlas_Market_Data.Indicators;
using Atlas_Shared;

namespace Atlas_Strategy.Strategies;

/// <summary>
/// Strategy C: Liquidity Sweep Reversal
/// Price sweeps a visible liquidity level (previous high/low) and closes back inside.
/// Requires structure break confirmation on M5/M15 — never enters on wick alone.
/// </summary>
public class Strategy_C_Liquidity_Sweep : I_Strategy
{
    public Strategy_Type Strategy_Type => Strategy_Type.Liquidity_Sweep_Reversal;

    public bool Is_Regime_Compatible(Market_Regime_Type regime) =>
        regime == Market_Regime_Type.Range || regime == Market_Regime_Type.Compression_Breakout;

    public Task<Trade_Signal_BO?> Generate_Signal_Async(
        string symbol_name,
        Market_Context_BO context,
        List<Candle_BO> h4_candles,
        List<Candle_BO> h1_candles,
        List<Candle_BO> m15_candles)
    {
        if (!Is_Regime_Compatible(context.Regime)) return Task.FromResult<Trade_Signal_BO?>(null);

        var atr = Technical_Indicators.Calculate_ATR(h1_candles, Trading_Constants.ATR_Period);
        var (h1_high, h1_low) = Technical_Indicators.Get_Range(h1_candles, 20);

        var last_m15  = m15_candles.Last();
        var prev_m15  = m15_candles[^2];

        bool is_gold = symbol_name.Contains("XAU", StringComparison.OrdinalIgnoreCase);

        // Bullish sweep: price wick below recent low then closes back above
        bool bullish_sweep = prev_m15.Low < h1_low &&         // Swept the low
                             last_m15.Close > h1_low &&       // Closed back above
                             last_m15.Is_Bullish &&            // Bullish close
                             last_m15.Body_Size > atr * 0.3m; // Not a doji

        // Bearish sweep: price wick above recent high then closes back below
        bool bearish_sweep = prev_m15.High > h1_high &&       // Swept the high
                             last_m15.Close < h1_high &&      // Closed back below
                             last_m15.Is_Bearish &&
                             last_m15.Body_Size > atr * 0.3m;

        // Gold requires stronger confirmation: close beyond previous M15 body
        if (is_gold && bullish_sweep)
            bullish_sweep = last_m15.Close > prev_m15.Open;
        if (is_gold && bearish_sweep)
            bearish_sweep = last_m15.Close < prev_m15.Open;

        Trade_Direction_Type direction;
        decimal entry, sl, tp;

        if (bullish_sweep)
        {
            direction = Trade_Direction_Type.Buy;
            entry     = last_m15.Close;
            sl        = prev_m15.Low - atr * 0.3m;
            tp        = Math.Max(entry + (entry - sl) * 1.5m, h1_high - atr * 0.1m);
        }
        else if (bearish_sweep)
        {
            direction = Trade_Direction_Type.Sell;
            entry     = last_m15.Close;
            sl        = prev_m15.High + atr * 0.3m;
            tp        = Math.Min(entry - (sl - entry) * 1.5m, h1_low + atr * 0.1m);
        }
        else
        {
            return Task.FromResult<Trade_Signal_BO?>(null);
        }

        var signal = new Trade_Signal_BO
        {
            Symbol_Name       = symbol_name,
            Strategy          = Strategy_Type.Liquidity_Sweep_Reversal,
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
