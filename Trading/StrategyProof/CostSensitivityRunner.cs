using MT5TradingBot.Modules.Backtesting;

namespace MT5TradingBot.Modules.StrategyProof
{
    public sealed record CostSensitivityScenario
    {
        public string Name { get; init; } = "";
        public double SpreadMultiplier { get; init; } = 1.0;
        public double AdditionalSpreadPips { get; init; }
        public double SlippageMultiplier { get; init; } = 1.0;
        public double AdditionalSlippagePips { get; init; }
        public double CommissionMultiplier { get; init; } = 1.0;
        public double AdditionalCommissionPerLot { get; init; }
    }

    public sealed record CostSensitivityInput
    {
        public IReadOnlyList<RealisticBacktestTradeOutcome> Outcomes { get; init; } = [];
        public IReadOnlyList<CostSensitivityScenario> Scenarios { get; init; } = [];
        public IReadOnlyDictionary<string, double> PipCostUsdPerPipByCandidateId { get; init; } =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        public IReadOnlyDictionary<string, double> LotSizeByCandidateId { get; init; } =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        public IReadOnlyDictionary<string, string> AssumptionsUsed { get; init; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public sealed record CostSensitivityReport
    {
        public bool Success { get; init; }
        public string FailureCode { get; init; } = "";
        public string FailureReason { get; init; } = "";
        public IReadOnlyList<string> Warnings { get; init; } = [];
        public CostSensitivityScenarioMetrics BaseMetrics { get; init; } = new();
        public IReadOnlyList<CostSensitivityScenarioResult> ScenarioMetrics { get; init; } = [];
        public IReadOnlyDictionary<string, string> AssumptionsUsed { get; init; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public sealed record CostSensitivityScenarioResult
    {
        public CostSensitivityScenario Scenario { get; init; } = new();
        public CostSensitivityScenarioMetrics Metrics { get; init; } = new();
        public CostSensitivityDegradation DegradationFromBase { get; init; } = new();
        public IReadOnlyList<string> Warnings { get; init; } = [];
    }

    public sealed record CostSensitivityScenarioMetrics
    {
        public int TotalTrades { get; init; }
        public int WinningTrades { get; init; }
        public int LosingTrades { get; init; }
        public double NetProfitUsd { get; init; }
        public double ProfitFactor { get; init; }
        public bool ProfitFactorUnlimited { get; init; }
        public double ExpectancyUsd { get; init; }
        public double WinRateAfterCostsPercent { get; init; }
        public double MaxDrawdownUsd { get; init; }
        public int WorstLosingStreak { get; init; }
        public double TotalExecutionCostUsd { get; init; }
        public int WinToLossFlipCount { get; init; }
    }

    public sealed record CostSensitivityDegradation
    {
        public double NetProfitChangeUsd { get; init; }
        public double ExpectancyChangeUsd { get; init; }
        public double ProfitFactorChange { get; init; }
        public double WinRateChangePercent { get; init; }
        public double MaxDrawdownChangeUsd { get; init; }
        public int WorstLosingStreakChange { get; init; }
        public double TotalExecutionCostChangeUsd { get; init; }
        public int WinToLossFlipCountChange { get; init; }
    }

    public static class CostSensitivityRunner
    {
        public const string NoDataCode = "COST_SENSITIVITY_NO_COMPLETED_TRADES";
        public const string InvalidScenarioCode = "COST_SENSITIVITY_SCENARIO_INVALID";

        public static CostSensitivityReport Run(CostSensitivityInput input)
        {
            var completed = input.Outcomes
                .Where(o => o.Status == RealisticBacktestOutcomeStatus.Successful)
                .OrderBy(OutcomeTimestamp)
                .ToList();

            if (completed.Count == 0)
            {
                return new CostSensitivityReport
                {
                    Success = false,
                    FailureCode = NoDataCode,
                    FailureReason = "No completed realistic backtest trades were supplied for cost-sensitivity analysis.",
                    AssumptionsUsed = CopyAssumptions(input.AssumptionsUsed)
                };
            }

            var validation = ValidateScenarios(input.Scenarios);
            if (validation.Count > 0)
            {
                return new CostSensitivityReport
                {
                    Success = false,
                    FailureCode = InvalidScenarioCode,
                    FailureReason = string.Join(" ", validation),
                    AssumptionsUsed = CopyAssumptions(input.AssumptionsUsed)
                };
            }

            var warnings = BuildInputWarnings(completed).ToList();
            var baseMetrics = Calculate(completed, completed.Select(ToBaseTrade).ToList());
            var scenarios = input.Scenarios.Count == 0
                ? DefaultScenarios()
                : input.Scenarios;

            var scenarioResults = scenarios
                .Select(s => RunScenario(s, completed, input, baseMetrics))
                .ToList();
            warnings.AddRange(scenarioResults.SelectMany(r => r.Warnings).ToList());

            return new CostSensitivityReport
            {
                Success = true,
                Warnings = warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                BaseMetrics = baseMetrics,
                ScenarioMetrics = scenarioResults,
                AssumptionsUsed = CopyAssumptions(input.AssumptionsUsed)
            };
        }

        private static IReadOnlyList<CostSensitivityScenario> DefaultScenarios() =>
        [
            new() { Name = "Base/original costs" },
            new() { Name = "Increased spread", SpreadMultiplier = 1.5 },
            new() { Name = "Increased slippage", SlippageMultiplier = 1.5 },
            new() { Name = "Increased commission", CommissionMultiplier = 1.5 },
            new()
            {
                Name = "Combined worse-than-normal broker conditions",
                SpreadMultiplier = 1.5,
                SlippageMultiplier = 1.5,
                CommissionMultiplier = 1.5
            }
        ];

        private static CostSensitivityScenarioResult RunScenario(
            CostSensitivityScenario scenario,
            IReadOnlyList<RealisticBacktestTradeOutcome> completed,
            CostSensitivityInput input,
            CostSensitivityScenarioMetrics baseMetrics)
        {
            var warnings = new List<string>();
            var stressed = completed
                .Select(t => StressTrade(t, scenario, input, warnings))
                .ToList();
            var metrics = Calculate(completed, stressed);

            return new CostSensitivityScenarioResult
            {
                Scenario = scenario,
                Metrics = metrics,
                DegradationFromBase = Degradation(baseMetrics, metrics),
                Warnings = warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            };
        }

        private static StressedTrade StressTrade(
            RealisticBacktestTradeOutcome outcome,
            CostSensitivityScenario scenario,
            CostSensitivityInput input,
            List<string> warnings)
        {
            double gross = outcome.GrossProfitLossUsd != 0
                ? outcome.GrossProfitLossUsd
                : outcome.NetProfitLossUsd + outcome.TotalExecutionCostUsd;

            double spreadCost = Math.Max(0, outcome.SpreadCostUsd) * scenario.SpreadMultiplier;
            if (scenario.AdditionalSpreadPips > 0)
                spreadCost += AdditionalPipCost(input.PipCostUsdPerPipByCandidateId, outcome.CandidateId, scenario.AdditionalSpreadPips, "spread", warnings);

            double slippageCost = Math.Max(0, outcome.SlippageCostUsd) * scenario.SlippageMultiplier;
            if (scenario.AdditionalSlippagePips > 0)
                slippageCost += AdditionalPipCost(input.PipCostUsdPerPipByCandidateId, outcome.CandidateId, scenario.AdditionalSlippagePips, "slippage", warnings);

            double commissionCost = Math.Max(0, outcome.CommissionCostUsd) * scenario.CommissionMultiplier;
            if (scenario.AdditionalCommissionPerLot > 0)
                commissionCost += AdditionalCommission(input.LotSizeByCandidateId, outcome.CandidateId, scenario.AdditionalCommissionPerLot, warnings);

            double totalCost = Round(spreadCost + slippageCost + commissionCost);
            double net = Round(gross - totalCost);

            return new StressedTrade(
                outcome.CandidateId,
                OutcomeTimestamp(outcome),
                gross,
                net,
                totalCost);
        }

        private static double AdditionalPipCost(
            IReadOnlyDictionary<string, double> pipCostUsdPerPipByCandidateId,
            string candidateId,
            double additionalPips,
            string label,
            List<string> warnings)
        {
            if (!pipCostUsdPerPipByCandidateId.TryGetValue(candidateId, out double pipCost) ||
                !IsFiniteNonNegative(pipCost))
            {
                warnings.Add($"Additional {label} pips could not be priced for {candidateId}; pip-cost metadata is missing.");
                return 0;
            }

            return additionalPips * pipCost;
        }

        private static double AdditionalCommission(
            IReadOnlyDictionary<string, double> lotSizeByCandidateId,
            string candidateId,
            double additionalCommissionPerLot,
            List<string> warnings)
        {
            if (!lotSizeByCandidateId.TryGetValue(candidateId, out double lotSize) ||
                !IsFiniteNonNegative(lotSize))
            {
                warnings.Add($"Additional commission per lot could not be priced for {candidateId}; lot-size metadata is missing.");
                return 0;
            }

            return additionalCommissionPerLot * lotSize;
        }

        private static CostSensitivityScenarioMetrics Calculate(
            IReadOnlyList<RealisticBacktestTradeOutcome> original,
            IReadOnlyList<StressedTrade> trades)
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

            return new CostSensitivityScenarioMetrics
            {
                TotalTrades = total,
                WinningTrades = wins,
                LosingTrades = losses,
                NetProfitUsd = Round(netProfit),
                ProfitFactor = double.IsPositiveInfinity(profitFactor) ? profitFactor : Round(profitFactor),
                ProfitFactorUnlimited = profitFactorUnlimited,
                ExpectancyUsd = total > 0 ? Round(netProfit / total) : 0,
                WinRateAfterCostsPercent = total > 0 ? Round((double)wins / total * 100.0) : 0,
                MaxDrawdownUsd = Round(MaxDrawdown(trades)),
                WorstLosingStreak = WorstLosingStreak(trades),
                TotalExecutionCostUsd = Round(trades.Sum(t => t.TotalExecutionCostUsd)),
                WinToLossFlipCount = original
                    .Join(trades, o => o.CandidateId, t => t.CandidateId, (o, t) => new { Original = o, Stressed = t })
                    .Count(x => x.Original.NetProfitLossUsd > 0 && x.Stressed.NetProfitLossUsd < 0)
            };
        }

        private static CostSensitivityDegradation Degradation(
            CostSensitivityScenarioMetrics baseline,
            CostSensitivityScenarioMetrics scenario) => new()
        {
            NetProfitChangeUsd = Round(scenario.NetProfitUsd - baseline.NetProfitUsd),
            ExpectancyChangeUsd = Round(scenario.ExpectancyUsd - baseline.ExpectancyUsd),
            ProfitFactorChange = ProfitFactorChange(baseline.ProfitFactor, scenario.ProfitFactor),
            WinRateChangePercent = Round(scenario.WinRateAfterCostsPercent - baseline.WinRateAfterCostsPercent),
            MaxDrawdownChangeUsd = Round(scenario.MaxDrawdownUsd - baseline.MaxDrawdownUsd),
            WorstLosingStreakChange = scenario.WorstLosingStreak - baseline.WorstLosingStreak,
            TotalExecutionCostChangeUsd = Round(scenario.TotalExecutionCostUsd - baseline.TotalExecutionCostUsd),
            WinToLossFlipCountChange = scenario.WinToLossFlipCount - baseline.WinToLossFlipCount
        };

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

        private static IReadOnlyList<string> BuildInputWarnings(IReadOnlyList<RealisticBacktestTradeOutcome> completed)
        {
            var warnings = new List<string>();
            if (completed.Any(t => t.SpreadCostUsd == 0))
                warnings.Add("One or more completed trades have zero spread cost; missing spread cost data may understate sensitivity.");
            if (completed.Any(t => t.SlippageCostUsd == 0))
                warnings.Add("One or more completed trades have zero slippage cost; missing slippage cost data may understate sensitivity.");
            if (completed.Any(t => t.CommissionCostUsd == 0))
                warnings.Add("One or more completed trades have zero commission cost; missing commission data may understate sensitivity.");
            if (completed.Any(t => Math.Round(t.SpreadCostUsd + t.SlippageCostUsd + t.CommissionCostUsd, 2) != Math.Round(t.TotalExecutionCostUsd, 2)))
                warnings.Add("One or more completed trades have execution cost components that do not sum to total execution cost.");
            return warnings;
        }

        private static IReadOnlyList<string> ValidateScenarios(IReadOnlyList<CostSensitivityScenario> scenarios)
        {
            var failures = new List<string>();
            foreach (var scenario in scenarios)
            {
                string name = string.IsNullOrWhiteSpace(scenario.Name) ? "<unnamed>" : scenario.Name;
                if (!IsFiniteNonNegative(scenario.SpreadMultiplier))
                    failures.Add($"Scenario {name} has invalid spread multiplier.");
                if (!IsFiniteNonNegative(scenario.AdditionalSpreadPips))
                    failures.Add($"Scenario {name} has invalid additional spread pips.");
                if (!IsFiniteNonNegative(scenario.SlippageMultiplier))
                    failures.Add($"Scenario {name} has invalid slippage multiplier.");
                if (!IsFiniteNonNegative(scenario.AdditionalSlippagePips))
                    failures.Add($"Scenario {name} has invalid additional slippage pips.");
                if (!IsFiniteNonNegative(scenario.CommissionMultiplier))
                    failures.Add($"Scenario {name} has invalid commission multiplier.");
                if (!IsFiniteNonNegative(scenario.AdditionalCommissionPerLot))
                    failures.Add($"Scenario {name} has invalid additional commission per lot.");
            }

            return failures;
        }

        private static StressedTrade ToBaseTrade(RealisticBacktestTradeOutcome outcome) => new(
            outcome.CandidateId,
            OutcomeTimestamp(outcome),
            outcome.GrossProfitLossUsd != 0
                ? outcome.GrossProfitLossUsd
                : outcome.NetProfitLossUsd + outcome.TotalExecutionCostUsd,
            outcome.NetProfitLossUsd,
            outcome.TotalExecutionCostUsd);

        private static double MaxDrawdown(IReadOnlyList<StressedTrade> trades)
        {
            double equity = 0;
            double peak = 0;
            double maxDrawdown = 0;

            foreach (var trade in trades.OrderBy(t => t.TimestampUtc))
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

        private static int WorstLosingStreak(IReadOnlyList<StressedTrade> trades)
        {
            int current = 0;
            int worst = 0;

            foreach (var trade in trades.OrderBy(t => t.TimestampUtc))
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

        private static bool IsFiniteNonNegative(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0;

        private static double Round(double value) =>
            Math.Round(value, 2);

        private sealed record StressedTrade(
            string CandidateId,
            DateTime TimestampUtc,
            double GrossProfitLossUsd,
            double NetProfitLossUsd,
            double TotalExecutionCostUsd);
    }
}
