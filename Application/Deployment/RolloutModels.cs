using MT5TradingBot.Models;
using MT5TradingBot.Modules.LiveReadiness;
using MT5TradingBot.Modules.Monitoring;

namespace MT5TradingBot.Modules.Deployment
{
    public static class RolloutStages
    {
        public const string PaperOnly = "PaperOnly";
        public const string Demo = "Demo";
        public const string TinyLive = "TinyLive";
        public const string ScaledLive = "ScaledLive";
        public const string RolledBack = "RolledBack";
    }

    public static class RolloutActions
    {
        public const string Stay = "Stay";
        public const string Advance = "Advance";
        public const string RollBack = "RollBack";
        public const string Block = "Block";
    }

    public static class RolloutCodes
    {
        public const string StagePaperOnly = "ROLLOUT_STAGE_PAPER_ONLY";
        public const string StageDemoOnly = "ROLLOUT_STAGE_DEMO_ONLY";
        public const string StageRolledBack = "ROLLOUT_STAGE_ROLLED_BACK";
        public const string TinyRiskCap = "ROLLOUT_TINY_RISK_CAP";
        public const string TinyLotCap = "ROLLOUT_TINY_LOT_CAP";
        public const string ScaleUpNeedsConfirmation = "ROLLOUT_SCALE_UP_REQUIRES_CONFIRMATION";
        public const string ScaleUpMinTrades = "ROLLOUT_SCALE_UP_MIN_TRADES";
        public const string ScaleUpMinDays = "ROLLOUT_SCALE_UP_MIN_DAYS";
        public const string ScaleUpProfitFactor = "ROLLOUT_SCALE_UP_PROFIT_FACTOR";
        public const string RollbackDrawdown = "ROLLOUT_ROLLBACK_DRAWDOWN";
        public const string RollbackLosingStreak = "ROLLOUT_ROLLBACK_LOSING_STREAK";
        public const string RollbackRejectionRate = "ROLLOUT_ROLLBACK_REJECTION_RATE";
        public const string RollbackSpreadDrift = "ROLLOUT_ROLLBACK_SPREAD_DRIFT";
        public const string RollbackSlippageDrift = "ROLLOUT_ROLLBACK_SLIPPAGE_DRIFT";
        public const string RollbackRuntimeCritical = "ROLLOUT_ROLLBACK_RUNTIME_CRITICAL";
        public const string RollbackKillSwitch = "ROLLOUT_ROLLBACK_KILL_SWITCH";
    }

    public sealed record RolloutEvaluationInput
    {
        public BotConfig Config { get; init; } = new();
        public LiveReadinessResult? LiveReadiness { get; init; }
        public RuntimeHealthSnapshot? RuntimeHealth { get; init; }
        public int CompletedTrades { get; init; }
        public double DurationDays { get; init; }
        public double ProfitFactor { get; init; }
        public double DrawdownPercent { get; init; }
        public int LosingStreak { get; init; }
        public double RejectionRatePercent { get; init; }
        public double SpreadDriftPips { get; init; }
        public double SlippageDriftPips { get; init; }
        public bool KillSwitchActive { get; init; }
        public bool UserConfirmedScaleUp { get; init; }
        public DateTime? TimestampUtc { get; init; }
    }

    public sealed record RolloutEvaluationResult
    {
        public string CurrentStage { get; init; } = RolloutStages.PaperOnly;
        public string RecommendedStage { get; init; } = RolloutStages.PaperOnly;
        public string Action { get; init; } = RolloutActions.Stay;
        public IReadOnlyList<string> FailedCriteria { get; init; } = [];
        public IReadOnlyList<string> Warnings { get; init; } = [];
        public string Reason { get; init; } = "";
        public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
    }
}
