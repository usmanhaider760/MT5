using MT5TradingBot.Modules.Backtesting;

namespace MT5TradingBot.Modules.StrategyProof
{
    public static class StrategyRobustnessVerdicts
    {
        public const string Pass = "Pass";
        public const string Fail = "Fail";
        public const string Inconclusive = "Inconclusive";
    }

    public sealed record StrategyRobustnessThresholds
    {
        public int MinimumTotalTrades { get; init; } = 30;
        public int MinimumOutOfSampleTrades { get; init; } = 10;
        public double MaximumOosExpectancyDegradationUsd { get; init; } = 0;
        public double MaximumMonteCarloDrawdownUsd { get; init; } = double.PositiveInfinity;
        public int MaximumMonteCarloLosingStreak { get; init; } = int.MaxValue;
    }

    public sealed record StrategyRobustnessInput
    {
        public IReadOnlyList<RealisticBacktestTradeOutcome> Outcomes { get; init; } = [];
        public OutOfSampleSplitConfig SplitConfig { get; init; } = new() { InSampleRatio = 0.70 };
        public WalkForwardConfig? WalkForwardConfig { get; init; }
        public MonteCarloConfig MonteCarloConfig { get; init; } = new();
        public StrategyRobustnessThresholds Thresholds { get; init; } = new();
        public IReadOnlyDictionary<string, string> AssumptionsUsed { get; init; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public sealed record StrategyRobustnessReport
    {
        public bool Success { get; init; }
        public string FailureCode { get; init; } = "";
        public string FailureReason { get; init; } = "";
        public string Verdict { get; init; } = StrategyRobustnessVerdicts.Inconclusive;
        public IReadOnlyList<string> Warnings { get; init; } = [];
        public IReadOnlyList<string> FailedCriteria { get; init; } = [];
        public StrategyRobustnessMetrics InSampleMetrics { get; init; } = new();
        public StrategyRobustnessMetrics OutOfSampleMetrics { get; init; } = new();
        public StrategyRobustnessDegradation OutOfSampleDegradation { get; init; } = new();
        public IReadOnlyList<StrategyWalkForwardWindowSummary> WalkForwardWindows { get; init; } = [];
        public StrategyMonteCarloSummary MonteCarloSummary { get; init; } = new();
        public IReadOnlyDictionary<string, string> AssumptionsUsed { get; init; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public sealed record StrategyRobustnessMetrics
    {
        public int TotalTrades { get; init; }
        public int WinningTrades { get; init; }
        public int LosingTrades { get; init; }
        public double NetProfitUsd { get; init; }
        public double ExpectancyUsd { get; init; }
        public double ProfitFactor { get; init; }
        public bool ProfitFactorUnlimited { get; init; }
        public double WinRatePercent { get; init; }
        public double MaxDrawdownUsd { get; init; }
        public int WorstLosingStreak { get; init; }
    }

    public sealed record StrategyRobustnessDegradation
    {
        public double NetProfitChangeUsd { get; init; }
        public double ExpectancyChangeUsd { get; init; }
        public double ProfitFactorChange { get; init; }
        public double WinRateChangePercent { get; init; }
        public double MaxDrawdownChangeUsd { get; init; }
        public int WorstLosingStreakChange { get; init; }
    }

    public sealed record StrategyWalkForwardWindowSummary
    {
        public int Index { get; init; }
        public DateTime TrainingStartUtc { get; init; }
        public DateTime TrainingEndUtc { get; init; }
        public DateTime TestingStartUtc { get; init; }
        public DateTime TestingEndUtc { get; init; }
        public StrategyRobustnessMetrics TrainingMetrics { get; init; } = new();
        public StrategyRobustnessMetrics TestingMetrics { get; init; } = new();
    }

    public sealed record StrategyMonteCarloDistribution
    {
        public double Min { get; init; }
        public double Max { get; init; }
        public double Average { get; init; }
        public double Median { get; init; }
        public double Percentile5 { get; init; }
    }

    public sealed record StrategyMonteCarloSummary
    {
        public int Iterations { get; init; }
        public double StartingEquity { get; init; }
        public StrategyMonteCarloDistribution FinalEquity { get; init; } = new();
        public StrategyMonteCarloDistribution MaxDrawdown { get; init; } = new();
        public StrategyMonteCarloDistribution WorstLosingStreak { get; init; } = new();
    }

    public static class StrategyRobustnessRunner
    {
        public const string NoDataCode = "STRATEGY_ROBUSTNESS_NO_COMPLETED_TRADES";
        public const string SplitConfigInvalidCode = "STRATEGY_ROBUSTNESS_SPLIT_INVALID";
        public const string WalkForwardConfigInvalidCode = "STRATEGY_ROBUSTNESS_WALK_FORWARD_INVALID";
        public const string MonteCarloConfigInvalidCode = "STRATEGY_ROBUSTNESS_MONTE_CARLO_INVALID";

        public static StrategyRobustnessReport Run(StrategyRobustnessInput input)
        {
            var completed = input.Outcomes
                .Where(o => o.Status == RealisticBacktestOutcomeStatus.Successful)
                .OrderBy(OutcomeTimestamp)
                .ToList();

            if (completed.Count == 0)
            {
                return Failure(
                    NoDataCode,
                    "No completed realistic backtest trades were supplied for strategy robustness analysis.",
                    input.AssumptionsUsed);
            }

            var robustnessTrades = completed.Select(ToRobustnessTrade).ToList();
            var split = BacktestRobustnessTesting.SplitOutOfSample(robustnessTrades, input.SplitConfig);
            if (!split.Success)
                return Failure(SplitConfigInvalidCode, split.FailureReason, input.AssumptionsUsed);

            var monteCarlo = BacktestRobustnessTesting.RunMonteCarloTradeSequence(
                robustnessTrades.Select(t => t.ProfitLossUsd),
                input.MonteCarloConfig);
            if (!monteCarlo.Success)
                return Failure(MonteCarloConfigInvalidCode, monteCarlo.FailureReason, input.AssumptionsUsed);

            var walkForward = BuildWalkForward(input.WalkForwardConfig, robustnessTrades, input.AssumptionsUsed);
            if (!walkForward.Success)
                return Failure(WalkForwardConfigInvalidCode, walkForward.FailureReason, input.AssumptionsUsed);

            var inSample = Calculate(split.InSample);
            var outOfSample = Calculate(split.OutOfSample);
            var degradation = Degradation(inSample, outOfSample);
            var warnings = BuildWarnings(completed.Count, outOfSample.TotalTrades, input.Thresholds);
            var failedCriteria = BuildFailedCriteria(outOfSample, degradation, monteCarlo, input.Thresholds);
            string verdict = failedCriteria.Count > 0
                ? StrategyRobustnessVerdicts.Fail
                : warnings.Count > 0
                    ? StrategyRobustnessVerdicts.Inconclusive
                    : StrategyRobustnessVerdicts.Pass;

            return new StrategyRobustnessReport
            {
                Success = true,
                Verdict = verdict,
                Warnings = warnings,
                FailedCriteria = failedCriteria,
                InSampleMetrics = inSample,
                OutOfSampleMetrics = outOfSample,
                OutOfSampleDegradation = degradation,
                WalkForwardWindows = walkForward.Windows,
                MonteCarloSummary = ToSummary(monteCarlo),
                AssumptionsUsed = CopyAssumptions(input.AssumptionsUsed)
            };
        }

        private static WalkForwardBuildResult BuildWalkForward(
            WalkForwardConfig? config,
            IReadOnlyList<BacktestRobustnessTrade> trades,
            IReadOnlyDictionary<string, string> assumptions)
        {
            if (config == null)
            {
                return new WalkForwardBuildResult
                {
                    Success = true,
                    Windows = []
                };
            }

            var windows = BacktestRobustnessTesting.GenerateWalkForwardWindows(config);
            if (!windows.Success)
            {
                return new WalkForwardBuildResult
                {
                    Success = false,
                    FailureReason = windows.FailureReason
                };
            }

            return new WalkForwardBuildResult
            {
                Success = true,
                Windows = windows.Windows.Select(w => new StrategyWalkForwardWindowSummary
                {
                    Index = w.Index,
                    TrainingStartUtc = w.TrainingStartUtc,
                    TrainingEndUtc = w.TrainingEndUtc,
                    TestingStartUtc = w.TestingStartUtc,
                    TestingEndUtc = w.TestingEndUtc,
                    TrainingMetrics = Calculate(TradesInWindow(trades, w.TrainingStartUtc, w.TrainingEndUtc)),
                    TestingMetrics = Calculate(TradesInWindow(trades, w.TestingStartUtc, w.TestingEndUtc))
                }).ToList()
            };
        }

        private static IReadOnlyList<string> BuildWarnings(
            int totalTrades,
            int outOfSampleTrades,
            StrategyRobustnessThresholds thresholds)
        {
            var warnings = new List<string>();
            if (totalTrades < thresholds.MinimumTotalTrades)
            {
                warnings.Add(
                    $"Completed trade sample is too small: {totalTrades} < required {thresholds.MinimumTotalTrades}.");
            }

            if (outOfSampleTrades < thresholds.MinimumOutOfSampleTrades)
            {
                warnings.Add(
                    $"Out-of-sample trade sample is too small: {outOfSampleTrades} < required {thresholds.MinimumOutOfSampleTrades}.");
            }

            return warnings;
        }

        private static IReadOnlyList<string> BuildFailedCriteria(
            StrategyRobustnessMetrics outOfSample,
            StrategyRobustnessDegradation degradation,
            MonteCarloRobustnessResult monteCarlo,
            StrategyRobustnessThresholds thresholds)
        {
            var failed = new List<string>();

            if (degradation.ExpectancyChangeUsd < -Math.Abs(thresholds.MaximumOosExpectancyDegradationUsd))
            {
                failed.Add(
                    $"OOS expectancy degradation {degradation.ExpectancyChangeUsd} exceeds allowed {thresholds.MaximumOosExpectancyDegradationUsd}.");
            }

            if (monteCarlo.MaxDrawdownAmount.Max > thresholds.MaximumMonteCarloDrawdownUsd)
            {
                failed.Add(
                    $"Monte Carlo max drawdown {monteCarlo.MaxDrawdownAmount.Max} exceeds allowed {thresholds.MaximumMonteCarloDrawdownUsd}.");
            }

            if (monteCarlo.WorstLosingStreak.Max > thresholds.MaximumMonteCarloLosingStreak)
            {
                failed.Add(
                    $"Monte Carlo worst losing streak {monteCarlo.WorstLosingStreak.Max} exceeds allowed {thresholds.MaximumMonteCarloLosingStreak}.");
            }

            if (outOfSample.ExpectancyUsd < 0)
                failed.Add($"Out-of-sample expectancy is negative: {outOfSample.ExpectancyUsd}.");

            return failed;
        }

        private static StrategyRobustnessMetrics Calculate(IReadOnlyList<BacktestRobustnessTrade> trades)
        {
            int total = trades.Count;
            if (total == 0)
                return new StrategyRobustnessMetrics();

            int wins = trades.Count(t => t.ProfitLossUsd > 0);
            int losses = trades.Count(t => t.ProfitLossUsd < 0);
            double grossProfit = trades.Where(t => t.ProfitLossUsd > 0).Sum(t => t.ProfitLossUsd);
            double grossLoss = Math.Abs(trades.Where(t => t.ProfitLossUsd < 0).Sum(t => t.ProfitLossUsd));
            double netProfit = trades.Sum(t => t.ProfitLossUsd);
            bool pfUnlimited = grossLoss == 0 && grossProfit > 0;
            double profitFactor = grossLoss > 0
                ? grossProfit / grossLoss
                : pfUnlimited
                    ? double.PositiveInfinity
                    : 0;

            return new StrategyRobustnessMetrics
            {
                TotalTrades = total,
                WinningTrades = wins,
                LosingTrades = losses,
                NetProfitUsd = Round(netProfit),
                ExpectancyUsd = Round(netProfit / total),
                ProfitFactor = double.IsPositiveInfinity(profitFactor) ? profitFactor : Round(profitFactor),
                ProfitFactorUnlimited = pfUnlimited,
                WinRatePercent = Round((double)wins / total * 100.0),
                MaxDrawdownUsd = Round(MaxDrawdown(trades)),
                WorstLosingStreak = WorstLosingStreak(trades)
            };
        }

        private static StrategyRobustnessDegradation Degradation(
            StrategyRobustnessMetrics inSample,
            StrategyRobustnessMetrics outOfSample) => new()
        {
            NetProfitChangeUsd = Round(outOfSample.NetProfitUsd - inSample.NetProfitUsd),
            ExpectancyChangeUsd = Round(outOfSample.ExpectancyUsd - inSample.ExpectancyUsd),
            ProfitFactorChange = ProfitFactorChange(inSample.ProfitFactor, outOfSample.ProfitFactor),
            WinRateChangePercent = Round(outOfSample.WinRatePercent - inSample.WinRatePercent),
            MaxDrawdownChangeUsd = Round(outOfSample.MaxDrawdownUsd - inSample.MaxDrawdownUsd),
            WorstLosingStreakChange = outOfSample.WorstLosingStreak - inSample.WorstLosingStreak
        };

        private static StrategyMonteCarloSummary ToSummary(MonteCarloRobustnessResult monteCarlo) => new()
        {
            Iterations = monteCarlo.Iterations,
            StartingEquity = monteCarlo.StartingEquity,
            FinalEquity = Distribution(monteCarlo.SimulationResults.Select(r => r.FinalEquity)),
            MaxDrawdown = Distribution(monteCarlo.SimulationResults.Select(r => r.MaxDrawdownAmount)),
            WorstLosingStreak = Distribution(monteCarlo.SimulationResults.Select(r => (double)r.WorstLosingStreak))
        };

        private static StrategyMonteCarloDistribution Distribution(IEnumerable<double> values)
        {
            var ordered = values.OrderBy(v => v).ToList();
            if (ordered.Count == 0)
                return new StrategyMonteCarloDistribution();

            int middle = ordered.Count / 2;
            double median = ordered.Count % 2 == 1
                ? ordered[middle]
                : (ordered[middle - 1] + ordered[middle]) / 2.0;
            int p5Index = Math.Clamp((int)Math.Floor((ordered.Count - 1) * 0.05), 0, ordered.Count - 1);

            return new StrategyMonteCarloDistribution
            {
                Min = Round(ordered.First()),
                Max = Round(ordered.Last()),
                Average = Round(ordered.Average()),
                Median = Round(median),
                Percentile5 = Round(ordered[p5Index])
            };
        }

        private static IReadOnlyList<BacktestRobustnessTrade> TradesInWindow(
            IReadOnlyList<BacktestRobustnessTrade> trades,
            DateTime startUtc,
            DateTime endUtc) =>
            trades
                .Where(t => EnsureUtc(t.TimestampUtc) >= EnsureUtc(startUtc) && EnsureUtc(t.TimestampUtc) < EnsureUtc(endUtc))
                .OrderBy(t => EnsureUtc(t.TimestampUtc))
                .ToList();

        private static double MaxDrawdown(IReadOnlyList<BacktestRobustnessTrade> trades)
        {
            double equity = 0;
            double peak = 0;
            double maxDrawdown = 0;

            foreach (var trade in trades.OrderBy(t => EnsureUtc(t.TimestampUtc)))
            {
                equity += trade.ProfitLossUsd;
                if (equity > peak)
                    peak = equity;

                double drawdown = peak - equity;
                if (drawdown > maxDrawdown)
                    maxDrawdown = drawdown;
            }

            return maxDrawdown;
        }

        private static int WorstLosingStreak(IReadOnlyList<BacktestRobustnessTrade> trades)
        {
            int current = 0;
            int worst = 0;

            foreach (var trade in trades.OrderBy(t => EnsureUtc(t.TimestampUtc)))
            {
                if (trade.ProfitLossUsd < 0)
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

        private static double ProfitFactorChange(double baseline, double scenario)
        {
            if (double.IsPositiveInfinity(baseline) && double.IsPositiveInfinity(scenario))
                return 0;
            if (double.IsPositiveInfinity(baseline))
                return double.NegativeInfinity;
            if (double.IsPositiveInfinity(scenario))
                return double.PositiveInfinity;
            return Round(scenario - baseline);
        }

        private static BacktestRobustnessTrade ToRobustnessTrade(RealisticBacktestTradeOutcome outcome) => new()
        {
            Id = outcome.CandidateId,
            TimestampUtc = OutcomeTimestamp(outcome),
            ProfitLossUsd = outcome.NetProfitLossUsd
        };

        private static StrategyRobustnessReport Failure(
            string code,
            string reason,
            IReadOnlyDictionary<string, string> assumptions) => new()
        {
            Success = false,
            FailureCode = code,
            FailureReason = reason,
            Verdict = StrategyRobustnessVerdicts.Inconclusive,
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

        private static double Round(double value) =>
            Math.Round(value, 2);

        private sealed record WalkForwardBuildResult
        {
            public bool Success { get; init; }
            public string FailureReason { get; init; } = "";
            public IReadOnlyList<StrategyWalkForwardWindowSummary> Windows { get; init; } = [];
        }
    }
}
