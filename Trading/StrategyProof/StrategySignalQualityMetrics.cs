using MT5TradingBot.Modules.Backtesting;

namespace MT5TradingBot.Modules.StrategyProof
{
    public static class StrategySignalSourceLabels
    {
        public const string DeterministicBaseStrategy = "deterministic/base strategy";
        public const string AutoScalping = "auto-scalping";
        public const string AiConfirmed = "AI-confirmed";
        public const string ManualUserApproved = "manual/user-approved";
        public const string Unknown = "unknown";
    }

    public sealed record StrategySignalQualityInput
    {
        public IReadOnlyList<RealisticBacktestTradeOutcome> Outcomes { get; init; } = [];
        public int? TotalSignals { get; init; }
        public int SkippedOrHeldSignals { get; init; }
        public IReadOnlyDictionary<string, string> SourceByCandidateId { get; init; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public IReadOnlyDictionary<string, double> RMultipleByCandidateId { get; init; } =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        public IReadOnlyDictionary<string, string> AssumptionsUsed { get; init; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public sealed record StrategySignalQualityReport
    {
        public bool Success { get; init; }
        public string FailureCode { get; init; } = "";
        public string FailureReason { get; init; } = "";
        public IReadOnlyList<string> Warnings { get; init; } = [];
        public StrategySignalQualitySummary OverallMetrics { get; init; } = new();
        public IReadOnlyList<StrategySignalSourceMetrics> MetricsBySignalSource { get; init; } = [];
        public IReadOnlyDictionary<string, string> AssumptionsUsed { get; init; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public sealed record StrategySignalSourceMetrics
    {
        public string SignalSource { get; init; } = StrategySignalSourceLabels.Unknown;
        public StrategySignalQualitySummary Metrics { get; init; } = new();
    }

    public sealed record StrategySignalQualitySummary
    {
        public int TotalSignals { get; init; }
        public int ExecutableSignals { get; init; }
        public int SkippedOrHeldSignals { get; init; }
        public int RejectedSignals { get; init; }
        public int OpenTrades { get; init; }
        public int CompletedTrades { get; init; }
        public int WinningTrades { get; init; }
        public int LosingTrades { get; init; }
        public double WinRateAfterCostsPercent { get; init; }
        public double AverageWinAfterCostsUsd { get; init; }
        public double AverageLossAfterCostsUsd { get; init; }
        public double ExpectancyAfterCostsUsd { get; init; }
        public double ProfitFactorAfterCosts { get; init; }
        public bool ProfitFactorAfterCostsUnlimited { get; init; }
        public double MaxDrawdownUsd { get; init; }
        public int WorstLosingStreak { get; init; }
        public TimeSpan? AverageTradeDuration { get; init; }
        public double? AverageRMultiple { get; init; }
    }

    public static class StrategySignalQualityMetrics
    {
        public const string NoDataCode = "SIGNAL_QUALITY_NO_DATA";
        private const string NoCompletedTradesWarning =
            "No completed trades were supplied; win/loss quality metrics were returned as zero.";

        public static StrategySignalQualityReport BuildReport(StrategySignalQualityInput input)
        {
            var outcomes = input.Outcomes
                .OrderBy(o => OutcomeTimestamp(o))
                .ToList();
            int executableSignals = outcomes.Count;
            int inferredTotalSignals = executableSignals + Math.Max(0, input.SkippedOrHeldSignals);
            int totalSignals = input.TotalSignals.HasValue
                ? Math.Max(input.TotalSignals.Value, inferredTotalSignals)
                : inferredTotalSignals;

            if (totalSignals == 0)
            {
                return new StrategySignalQualityReport
                {
                    Success = false,
                    FailureCode = NoDataCode,
                    FailureReason = "No signals or realistic backtest outcomes were supplied for signal-quality metrics.",
                    AssumptionsUsed = CopyAssumptions(input.AssumptionsUsed)
                };
            }

            var warnings = BuildWarnings(input, outcomes, totalSignals, inferredTotalSignals);
            var overall = Calculate(
                outcomes,
                totalSignals,
                executableSignals,
                Math.Max(0, input.SkippedOrHeldSignals),
                input.RMultipleByCandidateId);

            return new StrategySignalQualityReport
            {
                Success = true,
                Warnings = warnings,
                OverallMetrics = overall,
                MetricsBySignalSource = GroupBySource(input, outcomes),
                AssumptionsUsed = CopyAssumptions(input.AssumptionsUsed)
            };
        }

        public static StrategySignalQualityReport BuildReport(
            RealisticBacktestResult result,
            int skippedOrHeldSignals = 0,
            IReadOnlyDictionary<string, string>? sourceByCandidateId = null,
            IReadOnlyDictionary<string, double>? rMultipleByCandidateId = null,
            IReadOnlyDictionary<string, string>? assumptionsUsed = null)
        {
            var outcomes = result.SuccessfulTrades
                .Concat(result.RejectedTrades)
                .Concat(result.OpenTrades)
                .ToList();

            return BuildReport(new StrategySignalQualityInput
            {
                Outcomes = outcomes,
                SkippedOrHeldSignals = skippedOrHeldSignals,
                SourceByCandidateId = sourceByCandidateId ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                RMultipleByCandidateId = rMultipleByCandidateId ?? new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase),
                AssumptionsUsed = assumptionsUsed ?? result.AssumptionsUsed
            });
        }

        public static string NormalizeSignalSource(string? source)
        {
            if (string.IsNullOrWhiteSpace(source))
                return StrategySignalSourceLabels.Unknown;

            string value = source.Trim();
            string lower = value.ToLowerInvariant();
            if (lower.Contains("auto", StringComparison.Ordinal) || lower.Contains("scalp", StringComparison.Ordinal))
                return StrategySignalSourceLabels.AutoScalping;
            if (lower.Contains("ai", StringComparison.Ordinal) || lower.Contains("claude", StringComparison.Ordinal))
                return StrategySignalSourceLabels.AiConfirmed;
            if (lower.Contains("manual", StringComparison.Ordinal) || lower.Contains("user", StringComparison.Ordinal))
                return StrategySignalSourceLabels.ManualUserApproved;
            if (lower.Contains("deterministic", StringComparison.Ordinal) ||
                lower.Contains("base", StringComparison.Ordinal) ||
                lower.Contains("strategy", StringComparison.Ordinal))
                return StrategySignalSourceLabels.DeterministicBaseStrategy;

            return StrategySignalSourceLabels.Unknown;
        }

        private static IReadOnlyList<string> BuildWarnings(
            StrategySignalQualityInput input,
            IReadOnlyList<RealisticBacktestTradeOutcome> outcomes,
            int totalSignals,
            int inferredTotalSignals)
        {
            var warnings = new List<string>();

            if (input.TotalSignals.HasValue && input.TotalSignals.Value < inferredTotalSignals)
            {
                warnings.Add(
                    "TotalSignals was lower than executable plus skipped/held signals; inferred total was used.");
            }

            if (!outcomes.Any(o => o.Status == RealisticBacktestOutcomeStatus.Successful))
                warnings.Add(NoCompletedTradesWarning);

            if (outcomes.Any(o => !input.SourceByCandidateId.ContainsKey(o.CandidateId)))
                warnings.Add("One or more outcomes are missing signal source metadata and were grouped as unknown.");

            var completed = outcomes
                .Where(o => o.Status == RealisticBacktestOutcomeStatus.Successful)
                .ToList();
            if (completed.Count > 0 && completed.Any(o => !o.ExitTimestampUtc.HasValue))
                warnings.Add("One or more completed trades are missing exit timestamps; missing durations were excluded.");
            if (completed.Count > 0 && !completed.Any(o => o.ExitTimestampUtc.HasValue))
                warnings.Add("Duration data is unavailable; average trade duration was not calculated.");
            if (completed.Count > 0 && !completed.Any(o => input.RMultipleByCandidateId.ContainsKey(o.CandidateId)))
                warnings.Add("R-multiple data is unavailable; average R multiple was not calculated.");

            if (totalSignals == 0)
                warnings.Add("No signal count was available.");

            return warnings;
        }

        private static IReadOnlyList<StrategySignalSourceMetrics> GroupBySource(
            StrategySignalQualityInput input,
            IReadOnlyList<RealisticBacktestTradeOutcome> outcomes) =>
            outcomes
                .GroupBy(o => SourceFor(input.SourceByCandidateId, o.CandidateId), StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    var groupOutcomes = g.ToList();
                    return new StrategySignalSourceMetrics
                    {
                        SignalSource = g.Key,
                        Metrics = Calculate(
                            groupOutcomes,
                            groupOutcomes.Count,
                            groupOutcomes.Count,
                            0,
                            input.RMultipleByCandidateId)
                    };
                })
                .ToList();

        private static StrategySignalQualitySummary Calculate(
            IReadOnlyList<RealisticBacktestTradeOutcome> outcomes,
            int totalSignals,
            int executableSignals,
            int skippedOrHeldSignals,
            IReadOnlyDictionary<string, double> rMultipleByCandidateId)
        {
            var completed = outcomes
                .Where(o => o.Status == RealisticBacktestOutcomeStatus.Successful)
                .OrderBy(OutcomeTimestamp)
                .ToList();
            int completedCount = completed.Count;
            int wins = completed.Count(o => o.NetProfitLossUsd > 0);
            int losses = completed.Count(o => o.NetProfitLossUsd < 0);
            double grossProfit = completed.Where(o => o.NetProfitLossUsd > 0).Sum(o => o.NetProfitLossUsd);
            double grossLoss = Math.Abs(completed.Where(o => o.NetProfitLossUsd < 0).Sum(o => o.NetProfitLossUsd));
            double netProfit = completed.Sum(o => o.NetProfitLossUsd);
            bool profitFactorUnlimited = grossLoss == 0 && grossProfit > 0;
            double profitFactor = grossLoss > 0
                ? grossProfit / grossLoss
                : profitFactorUnlimited
                    ? double.PositiveInfinity
                    : 0;

            var durations = completed
                .Where(o => o.ExitTimestampUtc.HasValue)
                .Select(o => EnsureUtc(o.ExitTimestampUtc!.Value) - EnsureUtc(o.TimestampUtc))
                .Where(d => d >= TimeSpan.Zero)
                .ToList();
            var rMultiples = completed
                .Where(o => rMultipleByCandidateId.ContainsKey(o.CandidateId))
                .Select(o => rMultipleByCandidateId[o.CandidateId])
                .ToList();

            return new StrategySignalQualitySummary
            {
                TotalSignals = totalSignals,
                ExecutableSignals = executableSignals,
                SkippedOrHeldSignals = skippedOrHeldSignals,
                RejectedSignals = outcomes.Count(o => o.Status == RealisticBacktestOutcomeStatus.Rejected),
                OpenTrades = outcomes.Count(o => o.Status == RealisticBacktestOutcomeStatus.Open),
                CompletedTrades = completedCount,
                WinningTrades = wins,
                LosingTrades = losses,
                WinRateAfterCostsPercent = completedCount > 0 ? Round((double)wins / completedCount * 100.0) : 0,
                AverageWinAfterCostsUsd = wins > 0 ? Round(grossProfit / wins) : 0,
                AverageLossAfterCostsUsd = losses > 0 ? Round(grossLoss / losses) : 0,
                ExpectancyAfterCostsUsd = completedCount > 0 ? Round(netProfit / completedCount) : 0,
                ProfitFactorAfterCosts = double.IsPositiveInfinity(profitFactor) ? profitFactor : Round(profitFactor),
                ProfitFactorAfterCostsUnlimited = profitFactorUnlimited,
                MaxDrawdownUsd = Round(MaxDrawdown(completed)),
                WorstLosingStreak = WorstLosingStreak(completed),
                AverageTradeDuration = durations.Count > 0
                    ? TimeSpan.FromTicks((long)Math.Round(durations.Average(d => d.Ticks)))
                    : null,
                AverageRMultiple = rMultiples.Count > 0 ? Round(rMultiples.Average()) : null
            };
        }

        private static double MaxDrawdown(IReadOnlyList<RealisticBacktestTradeOutcome> completed)
        {
            double equity = 0;
            double peak = 0;
            double maxDrawdown = 0;

            foreach (var trade in completed)
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

            foreach (var trade in completed)
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
                ? NormalizeSignalSource(source)
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
