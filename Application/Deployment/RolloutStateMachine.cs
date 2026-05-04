using MT5TradingBot.Models;
using MT5TradingBot.Modules.Monitoring;

namespace MT5TradingBot.Modules.Deployment
{
    public sealed class RolloutStateMachine
    {
        public RolloutEvaluationResult Evaluate(RolloutEvaluationInput input)
        {
            var config = input.Config;
            string current = NormalizeStage(config.CurrentRolloutStage);
            var failed = new List<string>();
            var warnings = new List<string>();
            var timestamp = input.TimestampUtc ?? DateTime.UtcNow;

            if (!config.EnableStagedRollout)
            {
                return Result(current, current, RolloutActions.Stay, failed, warnings,
                    "Staged rollout is disabled; no rollout stage change is recommended.", timestamp);
            }

            if (input.KillSwitchActive)
                failed.Add(RolloutCodes.RollbackKillSwitch);
            if (input.RuntimeHealth?.HasCriticalIssues == true ||
                string.Equals(input.RuntimeHealth?.OverallStatus, RuntimeHealthStatuses.Critical, StringComparison.OrdinalIgnoreCase))
            {
                failed.Add(RolloutCodes.RollbackRuntimeCritical);
            }

            AddRollbackThresholdFailures(input, config, failed);

            if (failed.Count > 0 && current is RolloutStages.TinyLive or RolloutStages.ScaledLive)
            {
                return Result(
                    current,
                    RolloutStages.RolledBack,
                    config.AutoRollbackEnabled ? RolloutActions.RollBack : RolloutActions.Block,
                    failed,
                    warnings,
                    config.AutoRollbackEnabled
                        ? "Rollback is recommended because severe rollout conditions were detected."
                        : "Rollout should be blocked because severe conditions were detected and auto-rollback is disabled.",
                    timestamp);
            }

            if (input.KillSwitchActive)
            {
                return Result(current, RolloutStages.RolledBack, RolloutActions.Block, failed, warnings,
                    "Kill switch is active; live rollout cannot proceed.", timestamp);
            }

            return current switch
            {
                RolloutStages.PaperOnly => Result(current, RolloutStages.PaperOnly, RolloutActions.Block,
                    [RolloutCodes.StagePaperOnly], warnings,
                    "PaperOnly stage blocks real live orders.", timestamp),
                RolloutStages.Demo => Result(current, RolloutStages.Demo, RolloutActions.Block,
                    [RolloutCodes.StageDemoOnly], warnings,
                    "Demo stage allows demo/paper validation only and blocks real live orders.", timestamp),
                RolloutStages.RolledBack => Result(current, RolloutStages.RolledBack, RolloutActions.Block,
                    [RolloutCodes.StageRolledBack], warnings,
                    "RolledBack stage blocks real live orders until explicit review.", timestamp),
                RolloutStages.TinyLive => EvaluateTinyLive(input, config, failed, warnings, timestamp),
                RolloutStages.ScaledLive => Result(current, current, RolloutActions.Stay, failed, warnings,
                    "ScaledLive stage may stay active while safety gates remain healthy.", timestamp),
                _ => Result(RolloutStages.PaperOnly, RolloutStages.PaperOnly, RolloutActions.Block,
                    [RolloutCodes.StagePaperOnly], warnings,
                    "Unknown rollout stage is treated as PaperOnly.", timestamp)
            };
        }

        public static string NormalizeStage(string? stage)
        {
            if (string.Equals(stage, RolloutStages.Demo, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(stage, "DemoForwardTest", StringComparison.OrdinalIgnoreCase))
                return RolloutStages.Demo;
            if (string.Equals(stage, RolloutStages.TinyLive, StringComparison.OrdinalIgnoreCase))
                return RolloutStages.TinyLive;
            if (string.Equals(stage, RolloutStages.ScaledLive, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(stage, "LimitedLive", StringComparison.OrdinalIgnoreCase))
                return RolloutStages.ScaledLive;
            if (string.Equals(stage, RolloutStages.RolledBack, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(stage, "Blocked", StringComparison.OrdinalIgnoreCase))
                return RolloutStages.RolledBack;
            return RolloutStages.PaperOnly;
        }

        private static RolloutEvaluationResult EvaluateTinyLive(
            RolloutEvaluationInput input,
            BotConfig config,
            List<string> failed,
            List<string> warnings,
            DateTime timestamp)
        {
            if (input.LiveReadiness?.IsAllowed == false)
                failed.AddRange(input.LiveReadiness.FailedCriteria);

            if (input.CompletedTrades < config.MinTradesBeforeScaleUp)
                warnings.Add(RolloutCodes.ScaleUpMinTrades);
            if (input.DurationDays < config.MinDaysBeforeScaleUp)
                warnings.Add(RolloutCodes.ScaleUpMinDays);
            if (config.MinProfitFactorBeforeScaleUp > 0 &&
                input.ProfitFactor < config.MinProfitFactorBeforeScaleUp)
                warnings.Add(RolloutCodes.ScaleUpProfitFactor);

            if (failed.Count > 0)
                return Result(RolloutStages.TinyLive, RolloutStages.TinyLive, RolloutActions.Block,
                    failed, warnings, "TinyLive cannot proceed while live readiness or rollback criteria fail.", timestamp);

            if (warnings.Count == 0)
            {
                if (input.UserConfirmedScaleUp)
                    return Result(RolloutStages.TinyLive, RolloutStages.ScaledLive, RolloutActions.Advance,
                        failed, warnings, "Scale-up criteria passed and explicit user confirmation was supplied.", timestamp);

                warnings.Add(RolloutCodes.ScaleUpNeedsConfirmation);
                return Result(RolloutStages.TinyLive, RolloutStages.TinyLive, RolloutActions.Stay,
                    failed, warnings, "Scale-up criteria passed, but explicit user confirmation is required.", timestamp);
            }

            return Result(RolloutStages.TinyLive, RolloutStages.TinyLive, RolloutActions.Stay,
                failed, warnings, "TinyLive remains active until scale-up criteria pass.", timestamp);
        }

        private static void AddRollbackThresholdFailures(
            RolloutEvaluationInput input,
            BotConfig config,
            List<string> failed)
        {
            if (config.MaxDrawdownBeforeRollback > 0 &&
                input.DrawdownPercent >= config.MaxDrawdownBeforeRollback)
                failed.Add(RolloutCodes.RollbackDrawdown);
            if (config.MaxLosingStreakBeforeRollback > 0 &&
                input.LosingStreak >= config.MaxLosingStreakBeforeRollback)
                failed.Add(RolloutCodes.RollbackLosingStreak);
            if (config.MaxRejectionRateBeforeRollback > 0 &&
                input.RejectionRatePercent >= config.MaxRejectionRateBeforeRollback)
                failed.Add(RolloutCodes.RollbackRejectionRate);
            if (config.MaxSpreadDriftBeforeRollback > 0 &&
                input.SpreadDriftPips >= config.MaxSpreadDriftBeforeRollback)
                failed.Add(RolloutCodes.RollbackSpreadDrift);
            if (config.MaxSlippageDriftBeforeRollback > 0 &&
                input.SlippageDriftPips >= config.MaxSlippageDriftBeforeRollback)
                failed.Add(RolloutCodes.RollbackSlippageDrift);
        }

        private static RolloutEvaluationResult Result(
            string current,
            string recommended,
            string action,
            IReadOnlyList<string> failed,
            IReadOnlyList<string> warnings,
            string reason,
            DateTime timestamp) => new()
        {
            CurrentStage = current,
            RecommendedStage = recommended,
            Action = action,
            FailedCriteria = failed.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Warnings = warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Reason = reason,
            TimestampUtc = timestamp
        };
    }
}
