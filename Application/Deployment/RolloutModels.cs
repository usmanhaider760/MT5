using MT5TradingBot.Models;

namespace MT5TradingBot.Modules.Deployment
{
    public enum RolloutStage
    {
        PaperOnly,
        Demo,
        TinyLive,
        ScaledLive,
        RolledBack
    }

    public enum RolloutAction
    {
        Stay,
        Advance,
        RollBack,
        Block
    }

    public sealed class RolloutEvaluationInput
    {
        public BotConfig Config { get; init; } = new();
        public bool IsLiveOrderRequested { get; init; }
        public bool LiveReadinessGatePassed { get; init; }
        public bool ExplicitUserConfirmation { get; init; }
        public bool ExplicitScaleUpConfirmation { get; init; }
        public int TinyLiveCompletedTrades { get; init; }
        public int TinyLiveElapsedDays { get; init; }
        public double TinyLiveProfitFactor { get; init; }
        public double CurrentDrawdownPercent { get; init; }
        public int CurrentLosingStreak { get; init; }
        public double CurrentRejectionRate { get; init; }
        public double CurrentSpreadDrift { get; init; }
        public double CurrentSlippageDrift { get; init; }
        public bool RuntimeHealthCritical { get; init; }
        public bool KillSwitchActive { get; init; }
    }

    public sealed class RolloutEvaluationResult
    {
        public RolloutStage CurrentStage { get; init; }
        public RolloutStage RecommendedStage { get; init; }
        public RolloutAction Action { get; init; }
        public IReadOnlyList<string> FailedCriteria { get; init; } = [];
        public IReadOnlyList<string> Warnings { get; init; } = [];
        public string Reason { get; init; } = "";
        public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

        public bool AllowsLiveOrders =>
            Action != RolloutAction.Block &&
            CurrentStage is RolloutStage.TinyLive or RolloutStage.ScaledLive;
    }
}
