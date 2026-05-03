namespace MT5TradingBot.Modules.Backtesting
{
    public sealed record BacktestRobustnessTrade
    {
        public string Id { get; init; } = "";
        public DateTime TimestampUtc { get; init; }
        public double ProfitLossUsd { get; init; }
    }

    public sealed record OutOfSampleSplitConfig
    {
        public double? InSampleRatio { get; init; }
        public DateTime? SplitDateUtc { get; init; }
    }

    public sealed record OutOfSampleSplitResult
    {
        public bool Success { get; init; }
        public string FailureCode { get; init; } = "";
        public string FailureReason { get; init; } = "";
        public IReadOnlyList<string> Warnings { get; init; } = [];
        public IReadOnlyList<BacktestRobustnessTrade> InSample { get; init; } = [];
        public IReadOnlyList<BacktestRobustnessTrade> OutOfSample { get; init; } = [];
        public int InSampleCount => InSample.Count;
        public int OutOfSampleCount => OutOfSample.Count;
        public double InSampleProfitLossUsd => Math.Round(InSample.Sum(t => t.ProfitLossUsd), 2);
        public double OutOfSampleProfitLossUsd => Math.Round(OutOfSample.Sum(t => t.ProfitLossUsd), 2);
    }

    public sealed record WalkForwardConfig
    {
        public DateTime StartUtc { get; init; }
        public DateTime EndUtc { get; init; }
        public TimeSpan TrainingPeriod { get; init; }
        public TimeSpan TestingPeriod { get; init; }
        public TimeSpan StepSize { get; init; }
    }

    public sealed record WalkForwardWindow
    {
        public int Index { get; init; }
        public DateTime TrainingStartUtc { get; init; }
        public DateTime TrainingEndUtc { get; init; }
        public DateTime TestingStartUtc { get; init; }
        public DateTime TestingEndUtc { get; init; }
    }

    public sealed record WalkForwardWindowResult
    {
        public bool Success { get; init; }
        public string FailureCode { get; init; } = "";
        public string FailureReason { get; init; } = "";
        public IReadOnlyList<string> Warnings { get; init; } = [];
        public IReadOnlyList<WalkForwardWindow> Windows { get; init; } = [];
    }

    public sealed record MonteCarloConfig
    {
        public double StartingEquity { get; init; } = 10_000;
        public int Iterations { get; init; } = 1_000;
        public int Seed { get; init; } = 42;
    }

    public sealed record MonteCarloMetricDistribution
    {
        public double Min { get; init; }
        public double Max { get; init; }
        public double Average { get; init; }
        public double Median { get; init; }
    }

    public sealed record MonteCarloIterationResult
    {
        public int Iteration { get; init; }
        public double FinalEquity { get; init; }
        public double MaxDrawdownAmount { get; init; }
        public int WorstLosingStreak { get; init; }
    }

    public sealed record MonteCarloRobustnessResult
    {
        public bool Success { get; init; }
        public string FailureCode { get; init; } = "";
        public string FailureReason { get; init; } = "";
        public IReadOnlyList<string> Warnings { get; init; } = [];
        public int Iterations { get; init; }
        public double StartingEquity { get; init; }
        public MonteCarloMetricDistribution FinalEquity { get; init; } = new();
        public MonteCarloMetricDistribution MaxDrawdownAmount { get; init; } = new();
        public MonteCarloMetricDistribution WorstLosingStreak { get; init; } = new();
        public IReadOnlyList<MonteCarloIterationResult> SimulationResults { get; init; } = [];
    }

    public static class BacktestRobustnessTesting
    {
        public static OutOfSampleSplitResult SplitOutOfSample(
            IEnumerable<BacktestRobustnessTrade> trades,
            OutOfSampleSplitConfig config)
        {
            var ordered = trades
                .OrderBy(t => EnsureUtc(t.TimestampUtc))
                .ToList();

            if (ordered.Count == 0)
                return SplitFailure("BACKTEST_SPLIT_NO_TRADES", "No trades were supplied for out-of-sample split.");

            if (config.SplitDateUtc.HasValue)
                return SplitByDate(ordered, EnsureUtc(config.SplitDateUtc.Value));

            if (!config.InSampleRatio.HasValue ||
                config.InSampleRatio.Value <= 0 ||
                config.InSampleRatio.Value >= 1 ||
                double.IsNaN(config.InSampleRatio.Value) ||
                double.IsInfinity(config.InSampleRatio.Value))
            {
                return SplitFailure(
                    "BACKTEST_SPLIT_CONFIG_INVALID",
                    "Out-of-sample split requires an in-sample ratio between 0 and 1 or a split date.");
            }

            int splitIndex = (int)Math.Floor(ordered.Count * config.InSampleRatio.Value);
            if (splitIndex <= 0 || splitIndex >= ordered.Count)
            {
                return SplitFailure(
                    "BACKTEST_SPLIT_CONFIG_INVALID",
                    "Split ratio must leave at least one in-sample and one out-of-sample trade.");
            }

            return SplitSuccess(
                ordered.Take(splitIndex).ToList(),
                ordered.Skip(splitIndex).ToList());
        }

        public static WalkForwardWindowResult GenerateWalkForwardWindows(WalkForwardConfig config)
        {
            DateTime start = EnsureUtc(config.StartUtc);
            DateTime end = EnsureUtc(config.EndUtc);

            if (start >= end ||
                config.TrainingPeriod <= TimeSpan.Zero ||
                config.TestingPeriod <= TimeSpan.Zero ||
                config.StepSize <= TimeSpan.Zero)
            {
                return WalkForwardFailure(
                    "BACKTEST_WALK_FORWARD_CONFIG_INVALID",
                    "Walk-forward config requires start before end and positive training, testing, and step periods.");
            }

            var windows = new List<WalkForwardWindow>();
            DateTime trainingStart = start;
            int index = 1;

            while (true)
            {
                DateTime trainingEnd = trainingStart.Add(config.TrainingPeriod);
                DateTime testingStart = trainingEnd;
                DateTime testingEnd = testingStart.Add(config.TestingPeriod);

                if (testingEnd > end)
                    break;

                windows.Add(new WalkForwardWindow
                {
                    Index = index++,
                    TrainingStartUtc = trainingStart,
                    TrainingEndUtc = trainingEnd,
                    TestingStartUtc = testingStart,
                    TestingEndUtc = testingEnd
                });

                trainingStart = trainingStart.Add(config.StepSize);
            }

            if (windows.Count == 0)
            {
                return WalkForwardFailure(
                    "BACKTEST_WALK_FORWARD_CONFIG_INVALID",
                    "Walk-forward period is too short to create a complete training/testing window.");
            }

            return new WalkForwardWindowResult
            {
                Success = true,
                Windows = windows
            };
        }

        public static MonteCarloRobustnessResult RunMonteCarloTradeSequence(
            IEnumerable<double> tradeProfitLossUsd,
            MonteCarloConfig config)
        {
            var pnl = tradeProfitLossUsd.ToList();
            if (pnl.Count == 0)
            {
                return MonteCarloFailure(
                    "BACKTEST_MONTE_CARLO_NO_TRADES",
                    "At least one trade P/L result is required for Monte Carlo robustness testing.",
                    config);
            }

            if (!IsFinitePositive(config.StartingEquity) || config.Iterations <= 0)
            {
                return MonteCarloFailure(
                    "BACKTEST_MONTE_CARLO_CONFIG_INVALID",
                    "Monte Carlo config requires positive starting equity and positive iterations.",
                    config);
            }

            var random = new Random(config.Seed);
            var results = new List<MonteCarloIterationResult>(config.Iterations);

            for (int iteration = 1; iteration <= config.Iterations; iteration++)
            {
                var shuffled = pnl.ToArray();
                Shuffle(shuffled, random);
                results.Add(EvaluateSequence(iteration, config.StartingEquity, shuffled));
            }

            return new MonteCarloRobustnessResult
            {
                Success = true,
                Iterations = config.Iterations,
                StartingEquity = config.StartingEquity,
                FinalEquity = Distribution(results.Select(r => r.FinalEquity)),
                MaxDrawdownAmount = Distribution(results.Select(r => r.MaxDrawdownAmount)),
                WorstLosingStreak = Distribution(results.Select(r => (double)r.WorstLosingStreak)),
                SimulationResults = results
            };
        }

        private static OutOfSampleSplitResult SplitByDate(
            IReadOnlyList<BacktestRobustnessTrade> ordered,
            DateTime splitDateUtc)
        {
            var inSample = ordered
                .Where(t => EnsureUtc(t.TimestampUtc) < splitDateUtc)
                .ToList();
            var outOfSample = ordered
                .Where(t => EnsureUtc(t.TimestampUtc) >= splitDateUtc)
                .ToList();

            if (inSample.Count == 0 || outOfSample.Count == 0)
            {
                return SplitFailure(
                    "BACKTEST_SPLIT_CONFIG_INVALID",
                    "Split date must leave at least one in-sample and one out-of-sample trade.");
            }

            return SplitSuccess(inSample, outOfSample);
        }

        private static MonteCarloIterationResult EvaluateSequence(
            int iteration,
            double startingEquity,
            IReadOnlyList<double> tradeProfitLossUsd)
        {
            double equity = startingEquity;
            double peak = startingEquity;
            double maxDrawdown = 0;
            int currentLosingStreak = 0;
            int worstLosingStreak = 0;

            foreach (double profitLoss in tradeProfitLossUsd)
            {
                equity += profitLoss;
                if (equity > peak)
                    peak = equity;

                double drawdown = peak - equity;
                if (drawdown > maxDrawdown)
                    maxDrawdown = drawdown;

                if (profitLoss < 0)
                {
                    currentLosingStreak++;
                    if (currentLosingStreak > worstLosingStreak)
                        worstLosingStreak = currentLosingStreak;
                }
                else
                {
                    currentLosingStreak = 0;
                }
            }

            return new MonteCarloIterationResult
            {
                Iteration = iteration,
                FinalEquity = Math.Round(equity, 2),
                MaxDrawdownAmount = Math.Round(maxDrawdown, 2),
                WorstLosingStreak = worstLosingStreak
            };
        }

        private static MonteCarloMetricDistribution Distribution(IEnumerable<double> values)
        {
            var ordered = values.OrderBy(v => v).ToList();
            if (ordered.Count == 0)
                return new MonteCarloMetricDistribution();

            int middle = ordered.Count / 2;
            double median = ordered.Count % 2 == 1
                ? ordered[middle]
                : (ordered[middle - 1] + ordered[middle]) / 2.0;

            return new MonteCarloMetricDistribution
            {
                Min = Math.Round(ordered.First(), 2),
                Max = Math.Round(ordered.Last(), 2),
                Average = Math.Round(ordered.Average(), 2),
                Median = Math.Round(median, 2)
            };
        }

        private static void Shuffle(double[] values, Random random)
        {
            for (int i = values.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (values[i], values[j]) = (values[j], values[i]);
            }
        }

        private static OutOfSampleSplitResult SplitSuccess(
            IReadOnlyList<BacktestRobustnessTrade> inSample,
            IReadOnlyList<BacktestRobustnessTrade> outOfSample) => new()
        {
            Success = true,
            InSample = inSample,
            OutOfSample = outOfSample
        };

        private static OutOfSampleSplitResult SplitFailure(string code, string reason) => new()
        {
            Success = false,
            FailureCode = code,
            FailureReason = reason
        };

        private static WalkForwardWindowResult WalkForwardFailure(string code, string reason) => new()
        {
            Success = false,
            FailureCode = code,
            FailureReason = reason
        };

        private static MonteCarloRobustnessResult MonteCarloFailure(
            string code,
            string reason,
            MonteCarloConfig config) => new()
        {
            Success = false,
            FailureCode = code,
            FailureReason = reason,
            StartingEquity = config.StartingEquity,
            Iterations = Math.Max(0, config.Iterations)
        };

        private static DateTime EnsureUtc(DateTime timestamp) =>
            timestamp.Kind == DateTimeKind.Utc
                ? timestamp
                : DateTime.SpecifyKind(timestamp, DateTimeKind.Utc);

        private static bool IsFinitePositive(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;
    }
}
