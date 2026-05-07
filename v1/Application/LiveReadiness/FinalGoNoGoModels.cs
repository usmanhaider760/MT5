namespace MT5TradingBot.Modules.LiveReadiness
{
    public enum FinalGoNoGoDecision
    {
        Go,
        NoGo,
        ConditionalGo,
        Unknown
    }

    public enum FinalGoNoGoTarget
    {
        PaperOrDemo,
        TinyLive,
        FullLive
    }

    public enum FinalChecklistStatus
    {
        Pass,
        Fail,
        Missing,
        Warning
    }

    public enum FinalRuntimeHealthStatus
    {
        Healthy,
        Degraded,
        Critical,
        Missing
    }

    public sealed record FinalGoNoGoInput
    {
        public FinalGoNoGoTarget Target { get; init; } = FinalGoNoGoTarget.FullLive;
        public string ReportDirectory { get; init; } = "";
        public bool AllowFullLiveGo { get; init; }
        public bool TinyLiveRiskCapsConfigured { get; init; }
        public bool NewsProviderRequired { get; init; }

        public FinalChecklistStatus P0AccountSafetyReadiness { get; init; } = FinalChecklistStatus.Missing;
        public FinalChecklistStatus P1ExecutionRealismReadiness { get; init; } = FinalChecklistStatus.Missing;
        public FinalChecklistStatus P2RealisticBacktestReadiness { get; init; } = FinalChecklistStatus.Missing;
        public FinalChecklistStatus P3StrategyEdgeProofReadiness { get; init; } = FinalChecklistStatus.Missing;
        public FinalChecklistStatus P4LiveReadinessGate { get; init; } = FinalChecklistStatus.Missing;
        public FinalChecklistStatus DemoForwardTestGate { get; init; } = FinalChecklistStatus.Missing;
        public FinalChecklistStatus BrokerEaDeploymentChecklist { get; init; } = FinalChecklistStatus.Missing;
        public FinalRuntimeHealthStatus RuntimeHealthStatus { get; init; } = FinalRuntimeHealthStatus.Missing;
        public FinalChecklistStatus SafetyAlertStatus { get; init; } = FinalChecklistStatus.Missing;
        public FinalChecklistStatus OperationalReadinessReportStatus { get; init; } = FinalChecklistStatus.Missing;
        public FinalChecklistStatus StagedRolloutStatus { get; init; } = FinalChecklistStatus.Missing;
        public bool? KillSwitchInactive { get; init; }
        public bool? UserLiveEnablementConfirmed { get; init; }
        public FinalChecklistStatus EaCompiledRedeployedNote { get; init; } = FinalChecklistStatus.Missing;
        public FinalChecklistStatus Mt5ConnectionHealth { get; init; } = FinalChecklistStatus.Missing;
        public FinalChecklistStatus NewsProviderStatus { get; init; } = FinalChecklistStatus.Missing;
    }

    public sealed class FinalChecklistItem
    {
        public string Name { get; init; } = "";
        public FinalChecklistStatus Status { get; init; }
        public bool Required { get; init; }
        public string Detail { get; init; } = "";
        public string ManualAction { get; init; } = "";
    }

    public sealed class FinalGoNoGoResult
    {
        public FinalGoNoGoDecision Decision { get; init; }
        public IReadOnlyList<string> FailedCriteria { get; init; } = [];
        public IReadOnlyList<string> Warnings { get; init; } = [];
        public IReadOnlyList<string> RequiredManualActions { get; init; } = [];
        public string RecommendedNextStep { get; init; } = "";
        public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
        public string ReportPath { get; init; } = "";
        public string Markdown { get; init; } = "";
        public IReadOnlyList<FinalChecklistItem> ChecklistItems { get; init; } = [];
    }
}
