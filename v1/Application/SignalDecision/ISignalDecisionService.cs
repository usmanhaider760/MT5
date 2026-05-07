using MT5TradingBot.Models;

namespace MT5TradingBot.Modules.SignalDecision
{
    public interface ISignalDecisionService
    {
        Task<MarketSignal> CreateDecisionAsync(
            MarketSignal strategySignal,
            AiAnalysisResult? aiAnalysis,
            RiskValidationResult riskResult,
            CancellationToken cancellationToken = default);
    }
}
