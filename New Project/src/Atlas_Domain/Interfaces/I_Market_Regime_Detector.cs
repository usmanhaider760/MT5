using Atlas_Domain.BusinessObjects;

namespace Atlas_Domain.Interfaces;

public interface I_Market_Regime_Detector
{
    Task<Market_Context_BO> Detect_Market_Regime_Async(string symbol_name, List<Candle_BO> d1_candles, List<Candle_BO> h4_candles, List<Candle_BO> h1_candles);
}
