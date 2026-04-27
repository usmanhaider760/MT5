using MT5TradingBot.Models;

namespace MT5TradingBot.Modules.AIAnalysis
{
    public interface IAIAnalysisService
    {
        Task<AiAnalysisResult> AnalyzeAsync(
            MarketSignal signal,
            AccountInfo account,
            SymbolInfo? symbolInfo,
            IReadOnlyList<LivePosition> openPositions,
            CancellationToken cancellationToken = default);
    }
}
