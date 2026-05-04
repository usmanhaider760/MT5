using MT5TradingBot.Models;

namespace MT5TradingBot.Modules.Deployment
{
    public sealed class RolloutEvaluator
    {
        public RolloutEvaluationResult Evaluate(RolloutEvaluationInput input)
        {
            var config = input.Config;
            var currentStage = NormalizeStage(config.CurrentRolloutStage);
            var failed = new List<string>();
            var warnings = new List<string>();
            DateTime now = DateTime.UtcNow;

            if (!config.EnableStagedRollout)
            {
                return Result(
                    currentStage,
                    currentStage,
                    RolloutAction.Stay,
                    failed,
                    warnings,
                    "Staged rollout is disabled.",
                    now);
            }

            AddRollbackFailures(input, config, failed);
            if (failed.Count > 0 && config.AutoRollbackEnabled)
            {
                var action = input.KillSwitchActive ? RolloutAction.Block : RolloutAction.RollBack;
                return Result(
                    currentStage,
                    RolloutStage.RolledBack,
                    action,
                    failed,
                    warnings,
                    "Rollback criteria breached; live escalation must stop until review.",
                    now);
            }

            if (input.IsLiveOrderRequested)
            {
                AddLiveOrderFailures(input, currentStage, failed);
                if (failed.Count > 0)
                {
                    return Result(
                        currentStage,
                        currentStage,
                        RolloutAction.Block,
                        failed,
                        warnings,
                        "Rollout stage blocks real live order execution.",
                        now);
                }
            }

            if (currentStage == RolloutStage.TinyLive)
                return EvaluateTinyLiveScaleUp(input, config, failed, warnings, now);

            return Result(
                currentStage,
                currentStage,
                RolloutAction.Stay,
                failed,
                warnings,
                "Current rollout stage remains unchanged.",
                now);
        }

        public static bool IsTinyLive(BotConfig config) =>
            config.EnableStagedRollout &&
            NormalizeStage(config.CurrentRolloutStage) == RolloutStage.TinyLive;

        public static double EffectiveMaxRiskPercent(BotConfig config) =>
            IsTinyLive(config) && config.MaxTinyLiveRiskPercent > 0
                ? Math.Min(config.MaxRiskPercent, config.MaxTinyLiveRiskPercent)
                : config.MaxRiskPercent;

        public static double ApplyTinyLiveLotCap(
            BotConfig config,
            double proposedLotSize,
            double accountEquity,
            double referenceEntry,
            double stopLoss,
            string symbol,
            Func<double, double> lotForRiskPercent)
        {
            if (!IsTinyLive(config) || proposedLotSize <= 0)
                return proposedLotSize;

            double cappedLot = proposedLotSize;

            if (config.MaxTinyLiveRiskPercent > 0)
                cappedLot = Math.Min(cappedLot, lotForRiskPercent(config.MaxTinyLiveRiskPercent));

            if (config.MaxTinyLiveLotMultiplier > 0 && config.MaxTinyLiveLotMultiplier < 1)
            {
                double normalRiskLot = lotForRiskPercent(config.MaxRiskPercent);
                cappedLot = Math.Min(cappedLot, normalRiskLot * config.MaxTinyLiveLotMultiplier);
            }

            return Math.Max(0.01, Math.Round(cappedLot, 2));
        }

        private static RolloutEvaluationResult EvaluateTinyLiveScaleUp(
            RolloutEvaluationInput input,
            BotConfig config,
            List<string> failed,
            List<string> warnings,
            DateTime now)
        {
            if (input.TinyLiveCompletedTrades < config.MinTradesBeforeScaleUp)
                failed.Add($"TinyLive completed trades {input.TinyLiveCompletedTrades} below required {config.MinTradesBeforeScaleUp}.");

            if (input.TinyLiveElapsedDays < config.MinDaysBeforeScaleUp)
                failed.Add($"TinyLive elapsed days {input.TinyLiveElapsedDays} below required {config.MinDaysBeforeScaleUp}.");

            if (input.TinyLiveProfitFactor < config.MinProfitFactorBeforeScaleUp)
                failed.Add($"TinyLive profit factor {input.TinyLiveProfitFactor:F2} below required {config.MinProfitFactorBeforeScaleUp:F2}.");

            if (failed.Count > 0)
                return Result(
                    RolloutStage.TinyLive,
                    RolloutStage.TinyLive,
                    RolloutAction.Stay,
                    failed,
                    warnings,
                    "TinyLive scale-up criteria are not met.",
                    now);

            if (!input.ExplicitScaleUpConfirmation)
            {
                warnings.Add("Scale-up criteria pass, but explicit user confirmation is required before advancing.");
                return Result(
                    RolloutStage.TinyLive,
                    RolloutStage.ScaledLive,
                    RolloutAction.Stay,
                    failed,
                    warnings,
                    "Scale-up is recommended only; no automatic advance was performed.",
                    now);
            }

            return Result(
                RolloutStage.TinyLive,
                RolloutStage.ScaledLive,
                RolloutAction.Advance,
                failed,
                warnings,
                "TinyLive scale-up criteria passed with explicit user confirmation.",
                now);
        }

        private static void AddLiveOrderFailures(
            RolloutEvaluationInput input,
            RolloutStage currentStage,
            List<string> failed)
        {
            if (currentStage == RolloutStage.PaperOnly)
                failed.Add("PaperOnly stage blocks real live orders.");

            if (currentStage == RolloutStage.Demo)
                failed.Add("Demo stage blocks real live orders.");

            if (currentStage == RolloutStage.RolledBack)
                failed.Add("RolledBack stage blocks real live orders until explicit review.");

            if (currentStage == RolloutStage.TinyLive || currentStage == RolloutStage.ScaledLive)
            {
                if (!input.LiveReadinessGatePassed)
                    failed.Add("Live readiness gate has not passed.");

                if (!input.ExplicitUserConfirmation)
                    failed.Add("Explicit user confirmation is required for live rollout stage.");
            }
        }

        private static void AddRollbackFailures(
            RolloutEvaluationInput input,
            BotConfig config,
            List<string> failed)
        {
            if (input.KillSwitchActive)
                failed.Add("Kill switch is active.");

            if (input.RuntimeHealthCritical)
                failed.Add("Runtime health is critical.");

            if (config.MaxDrawdownBeforeRollback > 0 &&
                input.CurrentDrawdownPercent > config.MaxDrawdownBeforeRollback)
                failed.Add($"Drawdown {input.CurrentDrawdownPercent:F2}% exceeds rollback threshold {config.MaxDrawdownBeforeRollback:F2}%.");

            if (config.MaxLosingStreakBeforeRollback > 0 &&
                input.CurrentLosingStreak > config.MaxLosingStreakBeforeRollback)
                failed.Add($"Losing streak {input.CurrentLosingStreak} exceeds rollback threshold {config.MaxLosingStreakBeforeRollback}.");

            if (config.MaxRejectionRateBeforeRollback > 0 &&
                input.CurrentRejectionRate > config.MaxRejectionRateBeforeRollback)
                failed.Add($"Rejection rate {input.CurrentRejectionRate:F2} exceeds rollback threshold {config.MaxRejectionRateBeforeRollback:F2}.");

            if (config.MaxSpreadDriftBeforeRollback > 0 &&
                input.CurrentSpreadDrift > config.MaxSpreadDriftBeforeRollback)
                failed.Add($"Spread drift {input.CurrentSpreadDrift:F2} exceeds rollback threshold {config.MaxSpreadDriftBeforeRollback:F2}.");

            if (config.MaxSlippageDriftBeforeRollback > 0 &&
                input.CurrentSlippageDrift > config.MaxSlippageDriftBeforeRollback)
                failed.Add($"Slippage drift {input.CurrentSlippageDrift:F2} exceeds rollback threshold {config.MaxSlippageDriftBeforeRollback:F2}.");
        }

        private static RolloutStage NormalizeStage(RolloutStage stage) =>
            Enum.IsDefined(stage) ? stage : RolloutStage.PaperOnly;

        private static RolloutEvaluationResult Result(
            RolloutStage current,
            RolloutStage recommended,
            RolloutAction action,
            IReadOnlyList<string> failed,
            IReadOnlyList<string> warnings,
            string reason,
            DateTime timestampUtc) =>
            new()
            {
                CurrentStage = current,
                RecommendedStage = recommended,
                Action = action,
                FailedCriteria = failed.ToList(),
                Warnings = warnings.ToList(),
                Reason = reason,
                TimestampUtc = DateTime.SpecifyKind(timestampUtc, DateTimeKind.Utc)
            };
    }
}
