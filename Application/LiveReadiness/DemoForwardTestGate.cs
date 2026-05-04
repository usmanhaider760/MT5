using MT5TradingBot.Models;

namespace MT5TradingBot.Modules.LiveReadiness
{
    public sealed class DemoForwardTestGate
    {
        public DemoForwardTestResult Evaluate(DemoForwardTestConfig config)
        {
            var thresholds = ToThresholds(config);
            var metrics = ToMetrics(config.Metrics);
            var failed = new List<string>();
            var inconclusive = new List<string>();
            var warnings = new List<string>();

            if (metrics.TotalTrades <= 0 && metrics.CompletedTrades <= 0)
            {
                inconclusive.Add(DemoForwardTestCodes.NoTrades);
                warnings.Add("No demo/paper forward-test trades are available.");
            }

            if (thresholds.MinimumCompletedTrades > 0 &&
                metrics.CompletedTrades < thresholds.MinimumCompletedTrades)
            {
                inconclusive.Add(DemoForwardTestCodes.MinimumTrades);
                warnings.Add(
                    $"Demo/paper completed trades {metrics.CompletedTrades} < required {thresholds.MinimumCompletedTrades}.");
            }

            if (thresholds.MinimumDurationDays > 0 &&
                metrics.DurationDays < thresholds.MinimumDurationDays)
            {
                inconclusive.Add(DemoForwardTestCodes.MinimumDuration);
                warnings.Add(
                    $"Demo/paper duration {metrics.DurationDays:F1} days < required {thresholds.MinimumDurationDays} days.");
            }

            if (thresholds.RequireCostData && !metrics.CostDataAvailable)
            {
                inconclusive.Add(DemoForwardTestCodes.CostDataMissing);
                warnings.Add("Demo/paper execution-cost data is missing.");
            }

            if (thresholds.MinimumProfitFactor > 0 &&
                !metrics.ProfitFactorUnlimited &&
                metrics.ProfitFactor < thresholds.MinimumProfitFactor)
            {
                failed.Add(DemoForwardTestCodes.ProfitFactor);
            }

            if (metrics.ExpectancyUsd < thresholds.MinimumExpectancyUsd)
                failed.Add(DemoForwardTestCodes.Expectancy);

            if (thresholds.MaximumDrawdownUsd > 0 &&
                metrics.MaximumDrawdownUsd > thresholds.MaximumDrawdownUsd)
            {
                failed.Add(DemoForwardTestCodes.Drawdown);
            }

            if (thresholds.MaximumLosingStreak > 0 &&
                metrics.WorstLosingStreak > thresholds.MaximumLosingStreak)
            {
                failed.Add(DemoForwardTestCodes.LosingStreak);
            }

            if (thresholds.MaximumRejectionRatePercent > 0 &&
                metrics.RejectionRatePercent > thresholds.MaximumRejectionRatePercent)
            {
                failed.Add(DemoForwardTestCodes.RejectionRate);
            }

            EvaluateDrift(thresholds, metrics, failed, inconclusive, warnings);

            string verdict = failed.Count > 0
                ? DemoForwardTestVerdicts.Fail
                : inconclusive.Count > 0
                    ? DemoForwardTestVerdicts.Inconclusive
                    : DemoForwardTestVerdicts.Pass;

            return new DemoForwardTestResult
            {
                Passed = verdict == DemoForwardTestVerdicts.Pass,
                Verdict = verdict,
                FailedCriteria = failed
                    .Concat(inconclusive)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                Warnings = warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                Metrics = metrics,
                Thresholds = thresholds
            };
        }

        private static void EvaluateDrift(
            DemoForwardTestThresholds thresholds,
            DemoForwardTestMetricsSnapshot metrics,
            List<string> failed,
            List<string> inconclusive,
            List<string> warnings)
        {
            bool spreadDriftRequired = thresholds.MaximumAverageSpreadDriftUsd > 0;
            bool slippageDriftRequired = thresholds.MaximumAverageSlippageDriftUsd > 0;
            if (!spreadDriftRequired && !slippageDriftRequired)
                return;

            if (!metrics.BacktestComparisonDataAvailable ||
                (spreadDriftRequired && !metrics.BacktestAverageSpreadCostUsd.HasValue) ||
                (slippageDriftRequired && !metrics.BacktestAverageSlippageCostUsd.HasValue))
            {
                inconclusive.Add(DemoForwardTestCodes.BacktestComparisonMissing);
                warnings.Add("Backtest comparison data is missing for demo/paper drift checks.");
                return;
            }

            if (spreadDriftRequired &&
                metrics.AverageSpreadDriftUsd.HasValue &&
                metrics.AverageSpreadDriftUsd.Value > thresholds.MaximumAverageSpreadDriftUsd)
            {
                failed.Add(DemoForwardTestCodes.SpreadDrift);
            }

            if (slippageDriftRequired &&
                metrics.AverageSlippageDriftUsd.HasValue &&
                metrics.AverageSlippageDriftUsd.Value > thresholds.MaximumAverageSlippageDriftUsd)
            {
                failed.Add(DemoForwardTestCodes.SlippageDrift);
            }

            if ((metrics.AverageSpreadDriftUsd ?? 0) < 0 ||
                (metrics.AverageSlippageDriftUsd ?? 0) < 0)
            {
                warnings.Add("Demo/paper execution costs outperformed the backtest comparison baseline.");
            }
        }

        private static DemoForwardTestThresholds ToThresholds(DemoForwardTestConfig config) => new()
        {
            MinimumCompletedTrades = config.MinimumCompletedTrades,
            MinimumDurationDays = config.MinimumDurationDays,
            MinimumProfitFactor = config.MinimumProfitFactor,
            MinimumExpectancyUsd = config.MinimumExpectancyUsd,
            MaximumDrawdownUsd = config.MaximumDrawdownUsd,
            MaximumLosingStreak = config.MaximumLosingStreak,
            MaximumRejectionRatePercent = config.MaximumRejectionRatePercent,
            MaximumAverageSpreadDriftUsd = config.MaximumAverageSpreadDriftUsd,
            MaximumAverageSlippageDriftUsd = config.MaximumAverageSlippageDriftUsd,
            RequireCostData = config.RequireCostData
        };

        private static DemoForwardTestMetricsSnapshot ToMetrics(DemoForwardTestMetricsConfig metrics)
        {
            int totalTrades = metrics.TotalTrades > 0
                ? metrics.TotalTrades
                : metrics.CompletedTrades + metrics.RejectedTrades;
            double rejectionRate = metrics.RejectionRatePercent > 0
                ? metrics.RejectionRatePercent
                : totalTrades > 0
                    ? metrics.RejectedTrades * 100.0 / totalTrades
                    : 0;
            double durationDays = metrics.DurationDays > 0
                ? metrics.DurationDays
                : metrics.FirstTradeAtUtc.HasValue && metrics.LastTradeAtUtc.HasValue
                    ? Math.Max(0, (metrics.LastTradeAtUtc.Value - metrics.FirstTradeAtUtc.Value).TotalDays)
                    : 0;

            double? spreadDrift = metrics.BacktestAverageSpreadCostUsd.HasValue
                ? metrics.AverageSpreadCostUsd - metrics.BacktestAverageSpreadCostUsd.Value
                : null;
            double? slippageDrift = metrics.BacktestAverageSlippageCostUsd.HasValue
                ? metrics.AverageSlippageCostUsd - metrics.BacktestAverageSlippageCostUsd.Value
                : null;

            return new DemoForwardTestMetricsSnapshot
            {
                TotalTrades = totalTrades,
                CompletedTrades = metrics.CompletedTrades,
                RejectedTrades = metrics.RejectedTrades,
                DurationDays = durationDays,
                ProfitFactor = metrics.ProfitFactor,
                ProfitFactorUnlimited = metrics.ProfitFactorUnlimited,
                ExpectancyUsd = metrics.ExpectancyUsd,
                MaximumDrawdownUsd = metrics.MaximumDrawdownUsd,
                WorstLosingStreak = metrics.WorstLosingStreak,
                RejectionRatePercent = rejectionRate,
                AverageSpreadCostUsd = metrics.AverageSpreadCostUsd,
                AverageSlippageCostUsd = metrics.AverageSlippageCostUsd,
                AverageCommissionCostUsd = metrics.AverageCommissionCostUsd,
                CostDataAvailable = metrics.CostDataAvailable,
                BacktestAverageSpreadCostUsd = metrics.BacktestAverageSpreadCostUsd,
                BacktestAverageSlippageCostUsd = metrics.BacktestAverageSlippageCostUsd,
                BacktestComparisonDataAvailable = metrics.BacktestComparisonDataAvailable,
                AverageSpreadDriftUsd = spreadDrift,
                AverageSlippageDriftUsd = slippageDrift
            };
        }
    }
}
