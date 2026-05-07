using MT5TradingBot.Modules.Backtesting;

namespace MT5TradingBot.Modules.StrategyProof
{
    public static class AiFilterImpactVerdicts
    {
        public const string Improves = "Improves";
        public const string Hurts = "Hurts";
        public const string Inconclusive = "Inconclusive";
    }

    public sealed record AiFilterImpactThresholds
    {
        public int MinimumAiConfirmedTrades { get; init; } = 10;
        public int MinimumNonAiTrades { get; init; } = 10;
        public double MinimumExpectancyDeltaUsd { get; init; } = 0;
    }

    public sealed record AiFilterImpactInput
    {
        public IReadOnlyList<RealisticBacktestTradeOutcome> Outcomes { get; init; } = [];
        public IReadOnlyList<RealisticBacktestTradeOutcome> BlockedSignalCounterfactualOutcomes { get; init; } = [];
        public IReadOnlyDictionary<string, string> SignalSourceByCandidateId { get; init; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public IReadOnlyDictionary<string, double> AiConfidenceByCandidateId { get; init; } =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        public int SkippedSignals { get; init; }
        public AiFilterImpactThresholds Thresholds { get; init; } = new();
        public IReadOnlyDictionary<string, string> AssumptionsUsed { get; init; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public sealed record AiFilterImpactReport
    {
        public bool Success { get; init; }
        public string FailureCode { get; init; } = "";
        public string FailureReason { get; init; } = "";
        public string Verdict { get; init; } = AiFilterImpactVerdicts.Inconclusive;
        public IReadOnlyList<string> Warnings { get; init; } = [];
        public AiFilterImpactComparison OverallComparison { get; init; } = new();
        public IReadOnlyList<AiConfidenceBucketComparison> ConfidenceBucketComparison { get; init; } = [];
        public AiBlockedSignalAnalysis BlockedSignalAnalysis { get; init; } = new();
        public IReadOnlyDictionary<string, string> AssumptionsUsed { get; init; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public sealed record AiFilterImpactComparison
    {
        public AiFilterCohortMetrics AiConfirmed { get; init; } = new();
        public AiFilterCohortMetrics NonAi { get; init; } = new();
        public AiFilterCohortMetrics DeterministicBaseStrategy { get; init; } = new();
        public AiFilterCohortMetrics AutoScalping { get; init; } = new();
        public AiFilterCohortMetrics ManualUserApproved { get; init; } = new();
        public double ExpectancyDeltaAiVsNonAiUsd { get; init; }
        public double NetProfitDeltaAiVsNonAiUsd { get; init; }
        public bool AiOutperformsNonAi { get; init; }
    }

    public sealed record AiConfidenceBucketComparison
    {
        public string Bucket { get; init; } = StrategySegmentAnalyzer.UnknownSegment;
        public AiFilterCohortMetrics Metrics { get; init; } = new();
    }

    public sealed record AiBlockedSignalAnalysis
    {
        public int BlockedSignalsWithCounterfactuals { get; init; }
        public int BlockedWouldHaveWon { get; init; }
        public int BlockedWouldHaveLost { get; init; }
        public double BlockedCounterfactualNetProfitUsd { get; init; }
        public double BlockedCounterfactualExpectancyUsd { get; init; }
        public bool AiMostlyBlocksWinners { get; init; }
        public bool AiMostlyBlocksLosers { get; init; }
    }

    public sealed record AiFilterCohortMetrics
    {
        public int TotalSignals { get; init; }
        public int CompletedTrades { get; init; }
        public int RejectedOrSkippedSignals { get; init; }
        public int WinningTrades { get; init; }
        public int LosingTrades { get; init; }
        public double WinRateAfterCostsPercent { get; init; }
        public double NetProfitUsd { get; init; }
        public double ProfitFactor { get; init; }
        public bool ProfitFactorUnlimited { get; init; }
        public double ExpectancyUsd { get; init; }
        public double MaxDrawdownUsd { get; init; }
        public int WorstLosingStreak { get; init; }
        public double? AverageAiConfidence { get; init; }
    }

    public static class AiFilterImpactAnalyzer
    {
        public const string NoDataCode = "AI_FILTER_IMPACT_NO_SIGNALS";

        public static AiFilterImpactReport Analyze(AiFilterImpactInput input)
        {
            var outcomes = input.Outcomes
                .OrderBy(OutcomeTimestamp)
                .ToList();
            int totalSignals = outcomes.Count + Math.Max(0, input.SkippedSignals);

            if (totalSignals == 0)
            {
                return new AiFilterImpactReport
                {
                    Success = false,
                    FailureCode = NoDataCode,
                    FailureReason = "No signal outcomes were supplied for AI filter impact analysis.",
                    AssumptionsUsed = CopyAssumptions(input.AssumptionsUsed)
                };
            }

            var aiConfirmed = outcomes
                .Where(o => SourceFor(input.SignalSourceByCandidateId, o.CandidateId) == StrategySignalSourceLabels.AiConfirmed)
                .ToList();
            var nonAi = outcomes
                .Where(o =>
                {
                    string source = SourceFor(input.SignalSourceByCandidateId, o.CandidateId);
                    return source != StrategySignalSourceLabels.AiConfirmed &&
                        source != StrategySignalSourceLabels.Unknown;
                })
                .ToList();

            var comparison = new AiFilterImpactComparison
            {
                AiConfirmed = Calculate(aiConfirmed, input.AiConfidenceByCandidateId),
                NonAi = Calculate(nonAi, input.AiConfidenceByCandidateId),
                DeterministicBaseStrategy = Calculate(BySource(outcomes, input.SignalSourceByCandidateId, StrategySignalSourceLabels.DeterministicBaseStrategy), input.AiConfidenceByCandidateId),
                AutoScalping = Calculate(BySource(outcomes, input.SignalSourceByCandidateId, StrategySignalSourceLabels.AutoScalping), input.AiConfidenceByCandidateId),
                ManualUserApproved = Calculate(BySource(outcomes, input.SignalSourceByCandidateId, StrategySignalSourceLabels.ManualUserApproved), input.AiConfidenceByCandidateId)
            };
            comparison = comparison with
            {
                ExpectancyDeltaAiVsNonAiUsd = Round(comparison.AiConfirmed.ExpectancyUsd - comparison.NonAi.ExpectancyUsd),
                NetProfitDeltaAiVsNonAiUsd = Round(comparison.AiConfirmed.NetProfitUsd - comparison.NonAi.NetProfitUsd),
                AiOutperformsNonAi = comparison.AiConfirmed.ExpectancyUsd > comparison.NonAi.ExpectancyUsd
            };

            var warnings = BuildWarnings(input, outcomes, comparison);
            string verdict = Verdict(input.Thresholds, comparison, warnings);

            return new AiFilterImpactReport
            {
                Success = true,
                Verdict = verdict,
                Warnings = warnings,
                OverallComparison = comparison,
                ConfidenceBucketComparison = BuildConfidenceBuckets(aiConfirmed, input.AiConfidenceByCandidateId),
                BlockedSignalAnalysis = AnalyzeBlocked(input.BlockedSignalCounterfactualOutcomes),
                AssumptionsUsed = CopyAssumptions(input.AssumptionsUsed)
            };
        }

        private static string Verdict(
            AiFilterImpactThresholds thresholds,
            AiFilterImpactComparison comparison,
            IReadOnlyList<string> warnings)
        {
            if (warnings.Any(w => w.Contains("sample is too small", StringComparison.OrdinalIgnoreCase) ||
                w.Contains("No AI-confirmed", StringComparison.OrdinalIgnoreCase) ||
                w.Contains("No non-AI", StringComparison.OrdinalIgnoreCase)))
                return AiFilterImpactVerdicts.Inconclusive;

            double delta = comparison.ExpectancyDeltaAiVsNonAiUsd;
            double required = Math.Max(0, thresholds.MinimumExpectancyDeltaUsd);
            if (delta > required)
                return AiFilterImpactVerdicts.Improves;
            if (delta < -required)
                return AiFilterImpactVerdicts.Hurts;
            return AiFilterImpactVerdicts.Inconclusive;
        }

        private static IReadOnlyList<string> BuildWarnings(
            AiFilterImpactInput input,
            IReadOnlyList<RealisticBacktestTradeOutcome> outcomes,
            AiFilterImpactComparison comparison)
        {
            var warnings = new List<string>();

            if (comparison.AiConfirmed.CompletedTrades == 0)
                warnings.Add("No AI-confirmed completed trades were available for comparison.");
            if (comparison.NonAi.CompletedTrades == 0)
                warnings.Add("No non-AI completed trades were available for comparison.");
            if (comparison.AiConfirmed.CompletedTrades < input.Thresholds.MinimumAiConfirmedTrades)
                warnings.Add($"AI-confirmed sample is too small: {comparison.AiConfirmed.CompletedTrades} < required {input.Thresholds.MinimumAiConfirmedTrades}.");
            if (comparison.NonAi.CompletedTrades < input.Thresholds.MinimumNonAiTrades)
                warnings.Add($"Non-AI sample is too small: {comparison.NonAi.CompletedTrades} < required {input.Thresholds.MinimumNonAiTrades}.");
            if (outcomes.Any(o => SourceFor(input.SignalSourceByCandidateId, o.CandidateId) == StrategySignalSourceLabels.AiConfirmed &&
                !input.AiConfidenceByCandidateId.ContainsKey(o.CandidateId)))
                warnings.Add("One or more AI-confirmed signals are missing AI confidence metadata.");
            if (input.BlockedSignalCounterfactualOutcomes.Count == 0)
                warnings.Add("AI-blocked signal counterfactual data is unavailable.");

            return warnings;
        }

        private static IReadOnlyList<AiConfidenceBucketComparison> BuildConfidenceBuckets(
            IReadOnlyList<RealisticBacktestTradeOutcome> aiConfirmed,
            IReadOnlyDictionary<string, double> confidenceByCandidateId) =>
            aiConfirmed
                .GroupBy(o => confidenceByCandidateId.TryGetValue(o.CandidateId, out double confidence)
                    ? StrategySegmentAnalyzer.AiConfidenceBucket(confidence)
                    : StrategySegmentAnalyzer.UnknownSegment)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g => new AiConfidenceBucketComparison
                {
                    Bucket = g.Key,
                    Metrics = Calculate(g.ToList(), confidenceByCandidateId)
                })
                .ToList();

        private static AiBlockedSignalAnalysis AnalyzeBlocked(
            IReadOnlyList<RealisticBacktestTradeOutcome> blockedCounterfactuals)
        {
            var completed = blockedCounterfactuals
                .Where(o => o.Status == RealisticBacktestOutcomeStatus.Successful)
                .OrderBy(OutcomeTimestamp)
                .ToList();
            int winners = completed.Count(o => o.NetProfitLossUsd > 0);
            int losers = completed.Count(o => o.NetProfitLossUsd < 0);
            double net = completed.Sum(o => o.NetProfitLossUsd);

            return new AiBlockedSignalAnalysis
            {
                BlockedSignalsWithCounterfactuals = completed.Count,
                BlockedWouldHaveWon = winners,
                BlockedWouldHaveLost = losers,
                BlockedCounterfactualNetProfitUsd = Round(net),
                BlockedCounterfactualExpectancyUsd = completed.Count > 0 ? Round(net / completed.Count) : 0,
                AiMostlyBlocksWinners = winners > losers,
                AiMostlyBlocksLosers = losers > winners
            };
        }

        private static IReadOnlyList<RealisticBacktestTradeOutcome> BySource(
            IReadOnlyList<RealisticBacktestTradeOutcome> outcomes,
            IReadOnlyDictionary<string, string> sourceByCandidateId,
            string source) =>
            outcomes
                .Where(o => SourceFor(sourceByCandidateId, o.CandidateId) == source)
                .ToList();

        private static AiFilterCohortMetrics Calculate(
            IReadOnlyList<RealisticBacktestTradeOutcome> outcomes,
            IReadOnlyDictionary<string, double> confidenceByCandidateId)
        {
            var completed = outcomes
                .Where(o => o.Status == RealisticBacktestOutcomeStatus.Successful)
                .OrderBy(OutcomeTimestamp)
                .ToList();
            int total = outcomes.Count;
            int wins = completed.Count(o => o.NetProfitLossUsd > 0);
            int losses = completed.Count(o => o.NetProfitLossUsd < 0);
            double grossProfit = completed.Where(o => o.NetProfitLossUsd > 0).Sum(o => o.NetProfitLossUsd);
            double grossLoss = Math.Abs(completed.Where(o => o.NetProfitLossUsd < 0).Sum(o => o.NetProfitLossUsd));
            double net = completed.Sum(o => o.NetProfitLossUsd);
            bool pfUnlimited = grossLoss == 0 && grossProfit > 0;
            double pf = grossLoss > 0
                ? grossProfit / grossLoss
                : pfUnlimited
                    ? double.PositiveInfinity
                    : 0;
            var confidence = completed
                .Where(o => confidenceByCandidateId.ContainsKey(o.CandidateId))
                .Select(o => confidenceByCandidateId[o.CandidateId])
                .ToList();

            return new AiFilterCohortMetrics
            {
                TotalSignals = total,
                CompletedTrades = completed.Count,
                RejectedOrSkippedSignals = outcomes.Count(o => o.Status != RealisticBacktestOutcomeStatus.Successful),
                WinningTrades = wins,
                LosingTrades = losses,
                WinRateAfterCostsPercent = completed.Count > 0 ? Round((double)wins / completed.Count * 100.0) : 0,
                NetProfitUsd = Round(net),
                ProfitFactor = double.IsPositiveInfinity(pf) ? pf : Round(pf),
                ProfitFactorUnlimited = pfUnlimited,
                ExpectancyUsd = completed.Count > 0 ? Round(net / completed.Count) : 0,
                MaxDrawdownUsd = Round(MaxDrawdown(completed)),
                WorstLosingStreak = WorstLosingStreak(completed),
                AverageAiConfidence = confidence.Count > 0 ? Round(confidence.Average()) : null
            };
        }

        private static double MaxDrawdown(IReadOnlyList<RealisticBacktestTradeOutcome> completed)
        {
            double equity = 0;
            double peak = 0;
            double maxDrawdown = 0;

            foreach (var trade in completed.OrderBy(OutcomeTimestamp))
            {
                equity += trade.NetProfitLossUsd;
                if (equity > peak)
                    peak = equity;
                double drawdown = peak - equity;
                if (drawdown > maxDrawdown)
                    maxDrawdown = drawdown;
            }

            return maxDrawdown;
        }

        private static int WorstLosingStreak(IReadOnlyList<RealisticBacktestTradeOutcome> completed)
        {
            int current = 0;
            int worst = 0;

            foreach (var trade in completed.OrderBy(OutcomeTimestamp))
            {
                if (trade.NetProfitLossUsd < 0)
                {
                    current++;
                    if (current > worst)
                        worst = current;
                }
                else
                {
                    current = 0;
                }
            }

            return worst;
        }

        private static string SourceFor(
            IReadOnlyDictionary<string, string> sourceByCandidateId,
            string candidateId) =>
            sourceByCandidateId.TryGetValue(candidateId, out string? source)
                ? StrategySignalQualityMetrics.NormalizeSignalSource(source)
                : StrategySignalSourceLabels.Unknown;

        private static IReadOnlyDictionary<string, string> CopyAssumptions(
            IReadOnlyDictionary<string, string>? assumptionsUsed) =>
            assumptionsUsed == null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(assumptionsUsed, StringComparer.OrdinalIgnoreCase);

        private static DateTime OutcomeTimestamp(RealisticBacktestTradeOutcome outcome) =>
            EnsureUtc(outcome.ExitTimestampUtc ?? outcome.TimestampUtc);

        private static DateTime EnsureUtc(DateTime timestamp) =>
            timestamp.Kind == DateTimeKind.Utc
                ? timestamp
                : DateTime.SpecifyKind(timestamp, DateTimeKind.Utc);

        private static double Round(double value) =>
            Math.Round(value, 2);
    }
}
