using MT5TradingBot.Modules.Backtesting;

namespace MT5TradingBot.Modules.StrategyProof
{
    public static class DemoPaperReconciliationVerdicts
    {
        public const string Matches = "Matches";
        public const string Diverges = "Diverges";
        public const string Inconclusive = "Inconclusive";
    }

    public sealed record DemoPaperReconciliationTolerances
    {
        public int MinimumDemoPaperCompletedTrades { get; init; } = 30;
        public double MaxAllowedExpectancyDegradationUsd { get; init; } = 0;
        public double MaxAllowedProfitFactorDegradation { get; init; } = 0.20;
        public double MaxAllowedDrawdownIncreaseUsd { get; init; } = double.PositiveInfinity;
        public double MaxAllowedAverageSpreadCostIncreaseUsd { get; init; } = double.PositiveInfinity;
        public double MaxAllowedAverageSlippageCostIncreaseUsd { get; init; } = double.PositiveInfinity;
        public double MaxAllowedAverageCommissionCostIncreaseUsd { get; init; } = double.PositiveInfinity;
    }

    public sealed record DemoPaperReconciliationInput
    {
        public IReadOnlyList<RealisticBacktestTradeOutcome> BacktestOutcomes { get; init; } = [];
        public IReadOnlyList<RealisticBacktestTradeOutcome> DemoPaperOutcomes { get; init; } = [];
        public DemoPaperReconciliationTolerances Tolerances { get; init; } = new();
        public IReadOnlyDictionary<string, string> AssumptionsUsed { get; init; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public sealed record DemoPaperReconciliationReport
    {
        public bool Success { get; init; }
        public string FailureCode { get; init; } = "";
        public string FailureReason { get; init; } = "";
        public string Verdict { get; init; } = DemoPaperReconciliationVerdicts.Inconclusive;
        public IReadOnlyList<string> Warnings { get; init; } = [];
        public IReadOnlyList<string> FailedToleranceCriteria { get; init; } = [];
        public DemoPaperReconciliationMetrics BacktestMetrics { get; init; } = new();
        public DemoPaperReconciliationMetrics DemoPaperMetrics { get; init; } = new();
        public DemoPaperReconciliationDeltas Deltas { get; init; } = new();
        public IReadOnlyDictionary<string, string> AssumptionsUsed { get; init; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public sealed record DemoPaperReconciliationMetrics
    {
        public int TotalTrades { get; init; }
        public int CompletedTrades { get; init; }
        public int RejectedTrades { get; init; }
        public double RejectionRatePercent { get; init; }
        public int WinningTrades { get; init; }
        public int LosingTrades { get; init; }
        public double WinRatePercent { get; init; }
        public double AverageWinUsd { get; init; }
        public double AverageLossUsd { get; init; }
        public double ExpectancyUsd { get; init; }
        public double ProfitFactor { get; init; }
        public bool ProfitFactorUnlimited { get; init; }
        public double MaxDrawdownUsd { get; init; }
        public int WorstLosingStreak { get; init; }
        public double AverageSpreadCostUsd { get; init; }
        public double AverageSlippageCostUsd { get; init; }
        public double AverageCommissionCostUsd { get; init; }
        public TimeSpan? AverageTradeDuration { get; init; }
    }

    public sealed record DemoPaperReconciliationDeltas
    {
        public int TotalTradesChange { get; init; }
        public int CompletedTradesChange { get; init; }
        public double WinRateChangePercent { get; init; }
        public double AverageWinChangeUsd { get; init; }
        public double AverageLossChangeUsd { get; init; }
        public double ExpectancyChangeUsd { get; init; }
        public double ProfitFactorChange { get; init; }
        public double MaxDrawdownChangeUsd { get; init; }
        public int WorstLosingStreakChange { get; init; }
        public double AverageSpreadCostChangeUsd { get; init; }
        public double AverageSlippageCostChangeUsd { get; init; }
        public double AverageCommissionCostChangeUsd { get; init; }
        public double RejectionRateChangePercent { get; init; }
        public TimeSpan? AverageTradeDurationChange { get; init; }
    }

    public static class DemoPaperReconciliationAnalyzer
    {
        public const string NoBacktestTradesCode = "DEMO_RECONCILIATION_NO_BACKTEST_TRADES";
        public const string NoDemoPaperTradesCode = "DEMO_RECONCILIATION_NO_DEMO_PAPER_TRADES";

        public static DemoPaperReconciliationReport Analyze(DemoPaperReconciliationInput input)
        {
            var backtestCompleted = Completed(input.BacktestOutcomes);
            if (backtestCompleted.Count == 0)
            {
                return Failure(
                    NoBacktestTradesCode,
                    "Backtest expectations contain no completed trades, so demo/paper reconciliation cannot be calculated.",
                    input.AssumptionsUsed);
            }

            if (input.DemoPaperOutcomes.Count == 0)
            {
                return Failure(
                    NoDemoPaperTradesCode,
                    "No demo/paper trade outcomes were supplied for reconciliation.",
                    input.AssumptionsUsed);
            }

            var backtest = Calculate(input.BacktestOutcomes);
            var demo = Calculate(input.DemoPaperOutcomes);
            var deltas = BuildDeltas(backtest, demo);
            var warnings = BuildWarnings(input, backtest, demo).ToList();
            var failed = BuildFailedCriteria(input.Tolerances, deltas).ToList();

            string verdict = failed.Count > 0
                ? DemoPaperReconciliationVerdicts.Diverges
                : warnings.Any(w => w.Contains("sample is too small", StringComparison.OrdinalIgnoreCase))
                    ? DemoPaperReconciliationVerdicts.Inconclusive
                    : DemoPaperReconciliationVerdicts.Matches;

            return new DemoPaperReconciliationReport
            {
                Success = true,
                Verdict = verdict,
                Warnings = warnings,
                FailedToleranceCriteria = failed,
                BacktestMetrics = backtest,
                DemoPaperMetrics = demo,
                Deltas = deltas,
                AssumptionsUsed = CopyAssumptions(input.AssumptionsUsed)
            };
        }

        private static IReadOnlyList<string> BuildWarnings(
            DemoPaperReconciliationInput input,
            DemoPaperReconciliationMetrics backtest,
            DemoPaperReconciliationMetrics demo)
        {
            var warnings = new List<string>();

            if (demo.CompletedTrades < input.Tolerances.MinimumDemoPaperCompletedTrades)
            {
                warnings.Add(
                    $"Demo/paper sample is too small: {demo.CompletedTrades} < required {input.Tolerances.MinimumDemoPaperCompletedTrades}.");
            }

            AddCostWarning(warnings, input.BacktestOutcomes, "Backtest");
            AddCostWarning(warnings, input.DemoPaperOutcomes, "Demo/paper");
            AddDurationWarning(warnings, input.BacktestOutcomes, "Backtest", backtest);
            AddDurationWarning(warnings, input.DemoPaperOutcomes, "Demo/paper", demo);

            return warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static IReadOnlyList<string> BuildFailedCriteria(
            DemoPaperReconciliationTolerances tolerances,
            DemoPaperReconciliationDeltas deltas)
        {
            var failed = new List<string>();

            if (deltas.ExpectancyChangeUsd < -Math.Abs(tolerances.MaxAllowedExpectancyDegradationUsd))
            {
                failed.Add(
                    $"Expectancy degradation {FormatUsd(deltas.ExpectancyChangeUsd)} exceeds allowed {FormatUsd(Math.Abs(tolerances.MaxAllowedExpectancyDegradationUsd))}.");
            }

            if (deltas.ProfitFactorChange < -Math.Abs(tolerances.MaxAllowedProfitFactorDegradation))
            {
                failed.Add(
                    $"Profit factor degradation {Format(deltas.ProfitFactorChange)} exceeds allowed {Format(Math.Abs(tolerances.MaxAllowedProfitFactorDegradation))}.");
            }

            if (deltas.MaxDrawdownChangeUsd > tolerances.MaxAllowedDrawdownIncreaseUsd)
            {
                failed.Add(
                    $"Drawdown increase {FormatUsd(deltas.MaxDrawdownChangeUsd)} exceeds allowed {FormatUsd(tolerances.MaxAllowedDrawdownIncreaseUsd)}.");
            }

            if (deltas.AverageSpreadCostChangeUsd > tolerances.MaxAllowedAverageSpreadCostIncreaseUsd)
            {
                failed.Add(
                    $"Average spread-cost increase {FormatUsd(deltas.AverageSpreadCostChangeUsd)} exceeds allowed {FormatUsd(tolerances.MaxAllowedAverageSpreadCostIncreaseUsd)}.");
            }

            if (deltas.AverageSlippageCostChangeUsd > tolerances.MaxAllowedAverageSlippageCostIncreaseUsd)
            {
                failed.Add(
                    $"Average slippage-cost increase {FormatUsd(deltas.AverageSlippageCostChangeUsd)} exceeds allowed {FormatUsd(tolerances.MaxAllowedAverageSlippageCostIncreaseUsd)}.");
            }

            if (deltas.AverageCommissionCostChangeUsd > tolerances.MaxAllowedAverageCommissionCostIncreaseUsd)
            {
                failed.Add(
                    $"Average commission-cost increase {FormatUsd(deltas.AverageCommissionCostChangeUsd)} exceeds allowed {FormatUsd(tolerances.MaxAllowedAverageCommissionCostIncreaseUsd)}.");
            }

            return failed;
        }

        private static DemoPaperReconciliationMetrics Calculate(
            IReadOnlyList<RealisticBacktestTradeOutcome> outcomes)
        {
            var completed = Completed(outcomes);
            int total = outcomes.Count;
            int rejected = outcomes.Count(o => o.Status == RealisticBacktestOutcomeStatus.Rejected);
            int wins = completed.Count(o => o.NetProfitLossUsd > 0);
            int losses = completed.Count(o => o.NetProfitLossUsd < 0);
            double grossProfit = completed.Where(o => o.NetProfitLossUsd > 0).Sum(o => o.NetProfitLossUsd);
            double grossLoss = Math.Abs(completed.Where(o => o.NetProfitLossUsd < 0).Sum(o => o.NetProfitLossUsd));
            double net = completed.Sum(o => o.NetProfitLossUsd);
            bool pfUnlimited = grossLoss == 0 && grossProfit > 0;
            double profitFactor = grossLoss > 0
                ? grossProfit / grossLoss
                : pfUnlimited
                    ? double.PositiveInfinity
                    : 0;
            var durations = completed
                .Where(o => o.ExitTimestampUtc.HasValue)
                .Select(o => EnsureUtc(o.ExitTimestampUtc!.Value) - EnsureUtc(o.TimestampUtc))
                .Where(d => d >= TimeSpan.Zero)
                .ToList();

            return new DemoPaperReconciliationMetrics
            {
                TotalTrades = total,
                CompletedTrades = completed.Count,
                RejectedTrades = rejected,
                RejectionRatePercent = total > 0 ? Round((double)rejected / total * 100.0) : 0,
                WinningTrades = wins,
                LosingTrades = losses,
                WinRatePercent = completed.Count > 0 ? Round((double)wins / completed.Count * 100.0) : 0,
                AverageWinUsd = wins > 0 ? Round(grossProfit / wins) : 0,
                AverageLossUsd = losses > 0 ? Round(grossLoss / losses) : 0,
                ExpectancyUsd = completed.Count > 0 ? Round(net / completed.Count) : 0,
                ProfitFactor = double.IsPositiveInfinity(profitFactor) ? profitFactor : Round(profitFactor),
                ProfitFactorUnlimited = pfUnlimited,
                MaxDrawdownUsd = Round(MaxDrawdown(completed)),
                WorstLosingStreak = WorstLosingStreak(completed),
                AverageSpreadCostUsd = completed.Count > 0 ? Round(completed.Average(o => Math.Max(0, o.SpreadCostUsd))) : 0,
                AverageSlippageCostUsd = completed.Count > 0 ? Round(completed.Average(o => Math.Max(0, o.SlippageCostUsd))) : 0,
                AverageCommissionCostUsd = completed.Count > 0 ? Round(completed.Average(o => Math.Max(0, o.CommissionCostUsd))) : 0,
                AverageTradeDuration = durations.Count > 0
                    ? TimeSpan.FromTicks((long)Math.Round(durations.Average(d => d.Ticks)))
                    : null
            };
        }

        private static DemoPaperReconciliationDeltas BuildDeltas(
            DemoPaperReconciliationMetrics backtest,
            DemoPaperReconciliationMetrics demo) => new()
        {
            TotalTradesChange = demo.TotalTrades - backtest.TotalTrades,
            CompletedTradesChange = demo.CompletedTrades - backtest.CompletedTrades,
            WinRateChangePercent = Round(demo.WinRatePercent - backtest.WinRatePercent),
            AverageWinChangeUsd = Round(demo.AverageWinUsd - backtest.AverageWinUsd),
            AverageLossChangeUsd = Round(demo.AverageLossUsd - backtest.AverageLossUsd),
            ExpectancyChangeUsd = Round(demo.ExpectancyUsd - backtest.ExpectancyUsd),
            ProfitFactorChange = ProfitFactorChange(backtest.ProfitFactor, demo.ProfitFactor),
            MaxDrawdownChangeUsd = Round(demo.MaxDrawdownUsd - backtest.MaxDrawdownUsd),
            WorstLosingStreakChange = demo.WorstLosingStreak - backtest.WorstLosingStreak,
            AverageSpreadCostChangeUsd = Round(demo.AverageSpreadCostUsd - backtest.AverageSpreadCostUsd),
            AverageSlippageCostChangeUsd = Round(demo.AverageSlippageCostUsd - backtest.AverageSlippageCostUsd),
            AverageCommissionCostChangeUsd = Round(demo.AverageCommissionCostUsd - backtest.AverageCommissionCostUsd),
            RejectionRateChangePercent = Round(demo.RejectionRatePercent - backtest.RejectionRatePercent),
            AverageTradeDurationChange = backtest.AverageTradeDuration.HasValue && demo.AverageTradeDuration.HasValue
                ? demo.AverageTradeDuration.Value - backtest.AverageTradeDuration.Value
                : null
        };

        private static double ProfitFactorChange(double backtest, double demo)
        {
            if (double.IsPositiveInfinity(backtest) && double.IsPositiveInfinity(demo))
                return 0;
            if (double.IsPositiveInfinity(backtest))
                return double.NegativeInfinity;
            if (double.IsPositiveInfinity(demo))
                return double.PositiveInfinity;
            return Round(demo - backtest);
        }

        private static void AddCostWarning(
            List<string> warnings,
            IReadOnlyList<RealisticBacktestTradeOutcome> outcomes,
            string label)
        {
            var completed = Completed(outcomes);
            if (completed.Count == 0)
                return;

            if (completed.Any(o => o.SpreadCostUsd == 0))
                warnings.Add($"{label} completed trades include missing or zero spread cost data.");
            if (completed.Any(o => o.SlippageCostUsd == 0))
                warnings.Add($"{label} completed trades include missing or zero slippage cost data.");
            if (completed.Any(o => o.CommissionCostUsd == 0))
                warnings.Add($"{label} completed trades include missing or zero commission cost data.");
        }

        private static void AddDurationWarning(
            List<string> warnings,
            IReadOnlyList<RealisticBacktestTradeOutcome> outcomes,
            string label,
            DemoPaperReconciliationMetrics metrics)
        {
            var completed = Completed(outcomes);
            if (completed.Count == 0)
                return;

            if (!metrics.AverageTradeDuration.HasValue)
                warnings.Add($"{label} completed trades are missing duration data.");
            else if (completed.Any(o => !o.ExitTimestampUtc.HasValue))
                warnings.Add($"{label} completed trades include partial missing duration data.");
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

        private static IReadOnlyList<RealisticBacktestTradeOutcome> Completed(
            IReadOnlyList<RealisticBacktestTradeOutcome> outcomes) =>
            outcomes
                .Where(o => o.Status == RealisticBacktestOutcomeStatus.Successful)
                .OrderBy(OutcomeTimestamp)
                .ToList();

        private static DemoPaperReconciliationReport Failure(
            string code,
            string reason,
            IReadOnlyDictionary<string, string> assumptions) => new()
        {
            Success = false,
            FailureCode = code,
            FailureReason = reason,
            Verdict = DemoPaperReconciliationVerdicts.Inconclusive,
            AssumptionsUsed = CopyAssumptions(assumptions)
        };

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

        private static string Format(double value)
        {
            if (double.IsPositiveInfinity(value))
                return "Unlimited";
            if (double.IsNegativeInfinity(value))
                return "-Infinity";
            return value.ToString("0.##");
        }

        private static string FormatUsd(double value) =>
            double.IsInfinity(value)
                ? value.ToString()
                : $"${value:0.##}";

        private static double Round(double value) =>
            Math.Round(value, 2);
    }
}
