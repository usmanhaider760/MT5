using MT5TradingBot.Modules.Backtesting;

namespace MT5TradingBot.Modules.StrategyProof
{
    public sealed record StrategySegmentAnalysisInput
    {
        public IReadOnlyList<RealisticBacktestTradeOutcome> Outcomes { get; init; } = [];
        public IReadOnlyDictionary<string, string> VolatilityRegimeByCandidateId { get; init; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public IReadOnlyDictionary<string, string> TrendRangeRegimeByCandidateId { get; init; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public IReadOnlyDictionary<string, double> AiConfidenceByCandidateId { get; init; } =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        public IReadOnlyDictionary<string, string> SignalSourceByCandidateId { get; init; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public IReadOnlyDictionary<string, string> SignalReasonByCandidateId { get; init; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public IReadOnlyDictionary<string, string> AssumptionsUsed { get; init; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public sealed record StrategySegmentAnalysisReport
    {
        public bool Success { get; init; }
        public string FailureCode { get; init; } = "";
        public string FailureReason { get; init; } = "";
        public IReadOnlyList<string> Warnings { get; init; } = [];
        public IReadOnlyList<StrategySegmentGroup> SegmentGroups { get; init; } = [];
        public IReadOnlyDictionary<string, string> AssumptionsUsed { get; init; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public sealed record StrategySegmentGroup
    {
        public string Name { get; init; } = "";
        public IReadOnlyList<StrategySegmentMetrics> Segments { get; init; } = [];
    }

    public sealed record StrategySegmentMetrics
    {
        public string Key { get; init; } = StrategySegmentAnalyzer.UnknownSegment;
        public int TotalTrades { get; init; }
        public int WinningTrades { get; init; }
        public int LosingTrades { get; init; }
        public double WinRatePercent { get; init; }
        public double NetProfitUsd { get; init; }
        public double ProfitFactor { get; init; }
        public bool ProfitFactorUnlimited { get; init; }
        public double ExpectancyUsd { get; init; }
        public double MaxDrawdownUsd { get; init; }
        public int WorstLosingStreak { get; init; }
        public double AverageWinUsd { get; init; }
        public double AverageLossUsd { get; init; }
        public double TotalExecutionCostUsd { get; init; }
    }

    public static class StrategySegmentAnalyzer
    {
        public const string NoDataCode = "SEGMENT_ANALYSIS_NO_COMPLETED_TRADES";
        public const string UnknownSegment = "Unknown";

        public static StrategySegmentAnalysisReport BuildReport(StrategySegmentAnalysisInput input)
        {
            var completed = input.Outcomes
                .Where(o => o.Status == RealisticBacktestOutcomeStatus.Successful)
                .OrderBy(OutcomeTimestamp)
                .ToList();

            if (completed.Count == 0)
            {
                return new StrategySegmentAnalysisReport
                {
                    Success = false,
                    FailureCode = NoDataCode,
                    FailureReason = "No completed realistic backtest trades were supplied for segmented performance analysis.",
                    AssumptionsUsed = CopyAssumptions(input.AssumptionsUsed)
                };
            }

            return new StrategySegmentAnalysisReport
            {
                Success = true,
                Warnings = BuildWarnings(input, completed),
                SegmentGroups =
                [
                    Group("Symbol", completed, o => Normalize(o.Symbol)),
                    Group("Session", completed, o => Normalize(o.Session)),
                    Group("Spread Regime", completed, o => Normalize(o.SpreadRegime)),
                    Group("Volatility Regime", completed, o => Lookup(input.VolatilityRegimeByCandidateId, o.CandidateId)),
                    Group("Trend/Range Regime", completed, o => Lookup(input.TrendRangeRegimeByCandidateId, o.CandidateId)),
                    Group("AI Confidence Bucket", completed, o => AiConfidenceBucket(input.AiConfidenceByCandidateId, o.CandidateId)),
                    Group("Signal Reason/Source", completed, o => SignalReasonOrSource(input, o.CandidateId))
                ],
                AssumptionsUsed = CopyAssumptions(input.AssumptionsUsed)
            };
        }

        public static StrategySegmentAnalysisReport BuildReport(
            RealisticBacktestResult result,
            IReadOnlyDictionary<string, string>? volatilityRegimeByCandidateId = null,
            IReadOnlyDictionary<string, string>? trendRangeRegimeByCandidateId = null,
            IReadOnlyDictionary<string, double>? aiConfidenceByCandidateId = null,
            IReadOnlyDictionary<string, string>? signalSourceByCandidateId = null,
            IReadOnlyDictionary<string, string>? signalReasonByCandidateId = null,
            IReadOnlyDictionary<string, string>? assumptionsUsed = null) =>
            BuildReport(new StrategySegmentAnalysisInput
            {
                Outcomes = result.SuccessfulTrades,
                VolatilityRegimeByCandidateId = volatilityRegimeByCandidateId ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                TrendRangeRegimeByCandidateId = trendRangeRegimeByCandidateId ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                AiConfidenceByCandidateId = aiConfidenceByCandidateId ?? new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase),
                SignalSourceByCandidateId = signalSourceByCandidateId ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                SignalReasonByCandidateId = signalReasonByCandidateId ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                AssumptionsUsed = assumptionsUsed ?? result.AssumptionsUsed
            });

        public static string AiConfidenceBucket(double? confidence)
        {
            if (!confidence.HasValue || double.IsNaN(confidence.Value) || double.IsInfinity(confidence.Value))
                return UnknownSegment;

            double value = Math.Clamp(confidence.Value, 0, 100);
            return value switch
            {
                < 50 => "0-49",
                < 70 => "50-69",
                < 80 => "70-79",
                < 90 => "80-89",
                _ => "90-100"
            };
        }

        private static IReadOnlyList<string> BuildWarnings(
            StrategySegmentAnalysisInput input,
            IReadOnlyList<RealisticBacktestTradeOutcome> completed)
        {
            var warnings = new List<string>();

            AddMissingWarning(warnings, completed, o => o.Session, "session");
            AddMissingWarning(warnings, completed, o => o.SpreadRegime, "spread regime");
            AddMissingWarning(warnings, completed, o => Lookup(input.VolatilityRegimeByCandidateId, o.CandidateId), "volatility regime");
            AddMissingWarning(warnings, completed, o => Lookup(input.TrendRangeRegimeByCandidateId, o.CandidateId), "trend/range regime");
            AddMissingWarning(warnings, completed, o => AiConfidenceBucket(input.AiConfidenceByCandidateId, o.CandidateId), "AI confidence");
            AddMissingWarning(warnings, completed, o => SignalReasonOrSource(input, o.CandidateId), "signal reason/source");

            return warnings;
        }

        private static void AddMissingWarning(
            List<string> warnings,
            IReadOnlyList<RealisticBacktestTradeOutcome> completed,
            Func<RealisticBacktestTradeOutcome, string> selector,
            string label)
        {
            if (completed.Any(o => selector(o) == UnknownSegment))
                warnings.Add($"One or more completed trades are missing {label} metadata and were grouped as Unknown.");
        }

        private static StrategySegmentGroup Group(
            string name,
            IReadOnlyList<RealisticBacktestTradeOutcome> completed,
            Func<RealisticBacktestTradeOutcome, string> keySelector) => new()
        {
            Name = name,
            Segments = completed
                .GroupBy(keySelector, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g => Calculate(g.Key, g.OrderBy(OutcomeTimestamp).ToList()))
                .ToList()
        };

        private static StrategySegmentMetrics Calculate(
            string key,
            IReadOnlyList<RealisticBacktestTradeOutcome> trades)
        {
            int total = trades.Count;
            int wins = trades.Count(t => t.NetProfitLossUsd > 0);
            int losses = trades.Count(t => t.NetProfitLossUsd < 0);
            double grossProfit = trades.Where(t => t.NetProfitLossUsd > 0).Sum(t => t.NetProfitLossUsd);
            double grossLoss = Math.Abs(trades.Where(t => t.NetProfitLossUsd < 0).Sum(t => t.NetProfitLossUsd));
            double netProfit = trades.Sum(t => t.NetProfitLossUsd);
            bool profitFactorUnlimited = grossLoss == 0 && grossProfit > 0;
            double profitFactor = grossLoss > 0
                ? grossProfit / grossLoss
                : profitFactorUnlimited
                    ? double.PositiveInfinity
                    : 0;

            return new StrategySegmentMetrics
            {
                Key = key,
                TotalTrades = total,
                WinningTrades = wins,
                LosingTrades = losses,
                WinRatePercent = total > 0 ? Round((double)wins / total * 100.0) : 0,
                NetProfitUsd = Round(netProfit),
                ProfitFactor = double.IsPositiveInfinity(profitFactor) ? profitFactor : Round(profitFactor),
                ProfitFactorUnlimited = profitFactorUnlimited,
                ExpectancyUsd = total > 0 ? Round(netProfit / total) : 0,
                MaxDrawdownUsd = Round(MaxDrawdown(trades)),
                WorstLosingStreak = WorstLosingStreak(trades),
                AverageWinUsd = wins > 0 ? Round(grossProfit / wins) : 0,
                AverageLossUsd = losses > 0 ? Round(grossLoss / losses) : 0,
                TotalExecutionCostUsd = Round(trades.Sum(t => t.TotalExecutionCostUsd))
            };
        }

        private static double MaxDrawdown(IReadOnlyList<RealisticBacktestTradeOutcome> trades)
        {
            double equity = 0;
            double peak = 0;
            double maxDrawdown = 0;

            foreach (var trade in trades)
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

        private static int WorstLosingStreak(IReadOnlyList<RealisticBacktestTradeOutcome> trades)
        {
            int current = 0;
            int worst = 0;

            foreach (var trade in trades)
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

        private static string SignalReasonOrSource(StrategySegmentAnalysisInput input, string candidateId)
        {
            string reason = Lookup(input.SignalReasonByCandidateId, candidateId);
            if (reason != UnknownSegment)
                return reason;

            string source = Lookup(input.SignalSourceByCandidateId, candidateId);
            return source == UnknownSegment
                ? UnknownSegment
                : StrategySignalQualityMetrics.NormalizeSignalSource(source);
        }

        private static string AiConfidenceBucket(
            IReadOnlyDictionary<string, double> aiConfidenceByCandidateId,
            string candidateId) =>
            aiConfidenceByCandidateId.TryGetValue(candidateId, out double confidence)
                ? AiConfidenceBucket(confidence)
                : UnknownSegment;

        private static string Lookup(IReadOnlyDictionary<string, string> values, string candidateId) =>
            values.TryGetValue(candidateId, out string? value)
                ? Normalize(value)
                : UnknownSegment;

        private static string Normalize(string? value) =>
            string.IsNullOrWhiteSpace(value) ? UnknownSegment : value.Trim();

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
