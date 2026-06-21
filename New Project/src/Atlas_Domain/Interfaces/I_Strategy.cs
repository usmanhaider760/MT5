using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;

namespace Atlas_Domain.Interfaces;

public interface I_Strategy
{
    Strategy_Type Strategy_Type { get; }
    bool Is_Regime_Compatible(Market_Regime_Type regime);
    Task<Trade_Signal_BO?> Generate_Signal_Async(string symbol_name, Market_Context_BO context, List<Candle_BO> h4_candles, List<Candle_BO> h1_candles, List<Candle_BO> m15_candles);
}
