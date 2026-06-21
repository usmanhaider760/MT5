using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;
using Atlas_Domain.Interfaces;
using Atlas_Strategy.Strategies;

namespace Atlas_Strategy;

public class Strategy_Orchestrator
{
    private readonly List<I_Strategy> _strategies;
    private readonly Dictionary<Strategy_Type, bool> _active_strategies;

    public Strategy_Orchestrator()
    {
        _strategies =
        [
            new Strategy_A_Trend_Pullback(),
            new Strategy_B_Session_Breakout(),
            new Strategy_C_Liquidity_Sweep(),
            new Strategy_D_Breakout_Retest(),
            new Strategy_E_Mean_Reversion()
        ];

        // Strategies A and B active by default at launch (as per spec)
        _active_strategies = new()
        {
            [Strategy_Type.Trend_Pullback_Continuation] = true,
            [Strategy_Type.Session_Breakout]            = true,
            [Strategy_Type.Liquidity_Sweep_Reversal]    = false, // Enabled after testing
            [Strategy_Type.Breakout_Retest_Expansion]   = false,
            [Strategy_Type.Controlled_Mean_Reversion]   = false
        };
    }

    public async Task<List<Trade_Signal_BO>> Generate_All_Signals_Async(
        string symbol_name,
        Market_Context_BO context,
        List<Candle_BO> h4_candles,
        List<Candle_BO> h1_candles,
        List<Candle_BO> m15_candles)
    {
        var signals = new List<Trade_Signal_BO>();

        if (context.Regime == Market_Regime_Type.Abnormal_No_Trade)
            return signals;

        foreach (var strategy in _strategies)
        {
            if (!Is_Strategy_Active(strategy.Strategy_Type)) continue;
            if (!strategy.Is_Regime_Compatible(context.Regime)) continue;

            var signal = await strategy.Generate_Signal_Async(symbol_name, context, h4_candles, h1_candles, m15_candles);
            if (signal != null)
                signals.Add(signal);
        }

        return signals;
    }

    public void Enable_Strategy(Strategy_Type type) => _active_strategies[type] = true;
    public void Disable_Strategy(Strategy_Type type) => _active_strategies[type] = false;
    public bool Is_Strategy_Active(Strategy_Type type) =>
        _active_strategies.TryGetValue(type, out var active) && active;

    public IReadOnlyDictionary<Strategy_Type, bool> Get_Strategy_Status() =>
        _active_strategies.AsReadOnly();
}
