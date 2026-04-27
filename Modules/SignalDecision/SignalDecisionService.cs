using MT5TradingBot.Models;
using Serilog;

namespace MT5TradingBot.Modules.SignalDecision
{
    public sealed class SignalDecisionService : ISignalDecisionService
    {
        public Task<MarketSignal> CreateDecisionAsync(
            MarketSignal strategySignal,
            AiAnalysisResult? aiAnalysis,
            RiskValidationResult riskResult,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (!riskResult.IsApproved)
                    return Task.FromResult(Hold(strategySignal, $"Risk blocked signal: {riskResult.Reason}"));

                if (aiAnalysis == null)
                    return Task.FromResult(Hold(strategySignal, "AI analysis is missing."));

                if (aiAnalysis.RiskLevel == RiskLevel.Blocked)
                    return Task.FromResult(Hold(strategySignal, $"AI blocked signal: {aiAnalysis.Reason}"));

                if (aiAnalysis.Direction == SignalDirection.Hold)
                    return Task.FromResult(Hold(strategySignal, $"AI recommends HOLD: {aiAnalysis.Reason}"));

                if (aiAnalysis.ConfidenceScore < 70)
                    return Task.FromResult(Hold(strategySignal, $"AI confidence {aiAnalysis.ConfidenceScore}% is below 70%."));

                if (!string.Equals(strategySignal.Pair, aiAnalysis.Pair, StringComparison.OrdinalIgnoreCase))
                    return Task.FromResult(Hold(strategySignal, "Strategy and AI pair mismatch."));

                return Task.FromResult(new MarketSignal
                {
                    Id = strategySignal.Id,
                    Pair = strategySignal.Pair,
                    Direction = aiAnalysis.Direction,
                    OrderType = strategySignal.OrderType,
                    EntryPrice = riskResult.ReferenceEntryPrice > 0
                        ? riskResult.ReferenceEntryPrice
                        : aiAnalysis.EntryPrice,
                    StopLoss = aiAnalysis.StopLoss,
                    TakeProfit = aiAnalysis.TakeProfit,
                    TakeProfit2 = strategySignal.TakeProfit2,
                    Source = "SignalDecision",
                    Reason = $"Ready for user review. AI confidence {aiAnalysis.ConfidenceScore}%. Risk: {riskResult.RiskLevel}. {aiAnalysis.Reason}",
                    CreatedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Signal decision failed for signal {SignalId}", strategySignal.Id);
                return Task.FromResult(Hold(strategySignal, $"Signal decision failed: {ex.Message}"));
            }
        }

        private static MarketSignal Hold(MarketSignal source, string reason) =>
            new()
            {
                Id = source.Id,
                Pair = source.Pair,
                Direction = SignalDirection.Hold,
                OrderType = source.OrderType,
                EntryPrice = source.EntryPrice,
                StopLoss = source.StopLoss,
                TakeProfit = source.TakeProfit,
                TakeProfit2 = source.TakeProfit2,
                Source = "SignalDecision",
                Reason = reason,
                CreatedAt = DateTime.UtcNow
            };
    }
}
