using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;
using Atlas_Domain.Interfaces;
using Atlas_Market_Data.Indicators;
using Atlas_Shared;

namespace Atlas_Strategy.Strategies;

/// <summary>
/// Strategy B: London / New York Session Breakout
/// Captures directional expansion after Asian range compression.
/// Entry requires candle close outside range + retest hold.
/// </summary>
public class Strategy_B_Session_Breakout : I_Strategy
{
    public Strategy_Type Strategy_Type => Strategy_Type.Session_Breakout;

    public bool Is_Regime_Compatible(Market_Regime_Type regime) =>
        regime == Market_Regime_Type.Compression_Breakout;

    public Task<Trade_Signal_BO?> Generate_Signal_Async(
        string symbol_name,
        Market_Context_BO context,
        List<Candle_BO> h4_candles,
        List<Candle_BO> h1_candles,
        List<Candle_BO> m15_candles)
    {
        if (!Is_Regime_Compatible(context.Regime)) return Task.FromResult<Trade_Signal_BO?>(null);
        if (context.Current_Session != Session_Type.London &&
            context.Current_Session != Session_Type.London_NY_Overlap)
            return Task.FromResult<Trade_Signal_BO?>(null);

        // Identify Asian session range from last 8 H1 candles
        var asian_candles = h1_candles.TakeLast(8).ToList();
        var (range_high, range_low) = Technical_Indicators.Get_Range(asian_candles, asian_candles.Count);
        decimal range_size = range_high - range_low;
        var atr = Technical_Indicators.Calculate_ATR(h1_candles, Trading_Constants.ATR_Period);

        // Range must be reasonably compressed
        if (range_size < atr * 0.3m || range_size > atr * 1.5m)
            return Task.FromResult<Trade_Signal_BO?>(null);

        var last_m15 = m15_candles.Last();
        Trade_Direction_Type direction;
        decimal entry, sl, tp;

        if (last_m15.Close > range_high && last_m15.Low <= range_high) // Bullish breakout + retest
        {
            direction = Trade_Direction_Type.Buy;
            entry     = last_m15.Close;
            sl        = range_low - atr * 0.2m;
            tp        = entry + (entry - sl) * 2.0m;
        }
        else if (last_m15.Close < range_low && last_m15.High >= range_low) // Bearish breakout + retest
        {
            direction = Trade_Direction_Type.Sell;
            entry     = last_m15.Close;
            sl        = range_high + atr * 0.2m;
            tp        = entry - (sl - entry) * 2.0m;
        }
        else
        {
            return Task.FromResult<Trade_Signal_BO?>(null);
        }

        // Reject if breakout is directly into major level without room for target
        if (direction == Trade_Direction_Type.Buy && tp <= range_high * 1.005m)
            return Task.FromResult<Trade_Signal_BO?>(null);

        var signal = new Trade_Signal_BO
        {
            Symbol_Name       = symbol_name,
            Strategy          = Strategy_Type.Session_Breakout,
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
