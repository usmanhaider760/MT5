using MT5TradingBot.Models;
using Serilog;

namespace MT5TradingBot.Modules.AIAnalysis
{
    public sealed class AiAnalysisService : IAIAnalysisService
    {
        public Task<AiAnalysisResult> AnalyzeAsync(
            MarketSignal signal,
            AccountInfo account,
            SymbolInfo? symbolInfo,
            IReadOnlyList<LivePosition> openPositions,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                int confidence = CalculateBaselineConfidence(signal, symbolInfo, openPositions);
                var riskLevel = confidence >= 70
                    ? RiskLevel.Medium
                    : confidence >= 50
                        ? RiskLevel.High
                        : RiskLevel.Blocked;

                return Task.FromResult(new AiAnalysisResult
                {
                    SignalId = signal.Id,
                    Pair = signal.Pair,
                    Direction = SignalDirection.Hold,
                    EntryPrice = signal.EntryPrice,
                    StopLoss = signal.StopLoss,
                    TakeProfit = signal.TakeProfit,
                    ConfidenceScore = confidence,
                    RiskLevel = riskLevel,
                    Reason = BuildReason(signal, symbolInfo, openPositions),
                    InvalidationCondition = "Invalid until a real AI provider confirms trend, candles, news risk, and entry quality."
                });
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "AI analysis skeleton failed for signal {SignalId}", signal.Id);
                return Task.FromResult(new AiAnalysisResult
                {
                    SignalId = signal.Id,
                    Pair = signal.Pair,
                    Direction = SignalDirection.Hold,
                    ConfidenceScore = 0,
                    RiskLevel = RiskLevel.Blocked,
                    Reason = $"AI analysis failed: {ex.Message}",
                    InvalidationCondition = "Analysis error"
                });
            }
        }

        private static int CalculateBaselineConfidence(
            MarketSignal signal,
            SymbolInfo? symbolInfo,
            IReadOnlyList<LivePosition> openPositions)
        {
            int score = 40;

            if (!string.IsNullOrWhiteSpace(signal.Pair)) score += 10;
            if (signal.EntryPrice > 0 && signal.StopLoss > 0 && signal.TakeProfit > 0) score += 10;
            if (symbolInfo != null) score += 10;
            if (!openPositions.Any(p => string.Equals(p.Symbol, signal.Pair, StringComparison.OrdinalIgnoreCase))) score += 5;

            return Math.Clamp(score, 0, 100);
        }

        private static string BuildReason(
            MarketSignal signal,
            SymbolInfo? symbolInfo,
            IReadOnlyList<LivePosition> openPositions)
        {
            string dataState = symbolInfo == null
                ? "market data unavailable"
                : $"spread {symbolInfo.SpreadPips:F1} pips";

            int samePairPositions = openPositions.Count(p =>
                string.Equals(p.Symbol, signal.Pair, StringComparison.OrdinalIgnoreCase));

            return $"Skeleton AI review for {signal.Pair}: {dataState}; open same-pair positions: {samePairPositions}. No live trade approval is produced by this module.";
        }
    }
}
