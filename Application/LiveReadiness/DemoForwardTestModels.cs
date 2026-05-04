using MT5TradingBot.Models;

namespace MT5TradingBot.Modules.LiveReadiness
{
    public static class DemoForwardTestVerdicts
    {
        public const string Pass = "Pass";
        public const string Fail = "Fail";
        public const string Inconclusive = "Inconclusive";
    }

    public static class DemoForwardTestCodes
    {
        public const string NotPassed = "DEMO_FORWARD_TEST_NOT_PASSED";
        public const string NoTrades = "DEMO_FORWARD_NO_TRADES";
        public const string MinimumTrades = "DEMO_FORWARD_MIN_TRADES";
        public const string MinimumDuration = "DEMO_FORWARD_MIN_DURATION";
        public const string CostDataMissing = "DEMO_FORWARD_COST_DATA_MISSING";
        public const string ProfitFactor = "DEMO_FORWARD_PROFIT_FACTOR";
        public const string Expectancy = "DEMO_FORWARD_EXPECTANCY";
        public const string Drawdown = "DEMO_FORWARD_DRAWDOWN";
        public const string LosingStreak = "DEMO_FORWARD_LOSING_STREAK";
        public const string RejectionRate = "DEMO_FORWARD_REJECTION_RATE";
        public const string BacktestComparisonMissing = "DEMO_FORWARD_BACKTEST_COMPARISON_MISSING";
        public const string SpreadDrift = "DEMO_FORWARD_SPREAD_DRIFT";
        public const string SlippageDrift = "DEMO_FORWARD_SLIPPAGE_DRIFT";
    }

    public sealed record DemoForwardTestThresholds
    {
        public int MinimumCompletedTrades { get; init; }
        public int MinimumDurationDays { get; init; }
        public double MinimumProfitFactor { get; init; }
        public double MinimumExpectancyUsd { get; init; }
        public double MaximumDrawdownUsd { get; init; }
        public int MaximumLosingStreak { get; init; }
        public double MaximumRejectionRatePercent { get; init; }
        public double MaximumAverageSpreadDriftUsd { get; init; }
        public double MaximumAverageSlippageDriftUsd { get; init; }
        public bool RequireCostData { get; init; }
    }

    public sealed record DemoForwardTestMetricsSnapshot
    {
        public int TotalTrades { get; init; }
        public int CompletedTrades { get; init; }
        public int RejectedTrades { get; init; }
        public double DurationDays { get; init; }
        public double ProfitFactor { get; init; }
        public bool ProfitFactorUnlimited { get; init; }
        public double ExpectancyUsd { get; init; }
        public double MaximumDrawdownUsd { get; init; }
        public int WorstLosingStreak { get; init; }
        public double RejectionRatePercent { get; init; }
        public double AverageSpreadCostUsd { get; init; }
        public double AverageSlippageCostUsd { get; init; }
        public double AverageCommissionCostUsd { get; init; }
        public bool CostDataAvailable { get; init; }
        public double? BacktestAverageSpreadCostUsd { get; init; }
        public double? BacktestAverageSlippageCostUsd { get; init; }
        public bool BacktestComparisonDataAvailable { get; init; }
        public double? AverageSpreadDriftUsd { get; init; }
        public double? AverageSlippageDriftUsd { get; init; }
    }

    public sealed record DemoForwardTestResult
    {
        public bool Passed { get; init; }
        public string Verdict { get; init; } = DemoForwardTestVerdicts.Inconclusive;
        public IReadOnlyList<string> FailedCriteria { get; init; } = [];
        public IReadOnlyList<string> Warnings { get; init; } = [];
        public DemoForwardTestMetricsSnapshot Metrics { get; init; } = new();
        public DemoForwardTestThresholds Thresholds { get; init; } = new();
    }
}
