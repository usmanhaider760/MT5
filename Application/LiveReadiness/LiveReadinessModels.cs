using MT5TradingBot.Models;
using MT5TradingBot.Modules.Deployment;

namespace MT5TradingBot.Modules.LiveReadiness
{
    public static class LiveReadinessCodes
    {
        public const string Blocked = "LIVE_READINESS_GATE_BLOCKED";
        public const string StrategyEdgeNotProven = "STRATEGY_EDGE_NOT_PROVEN";
        public const string DemoReconciliationRequired = "DEMO_RECONCILIATION_REQUIRED";
        public const string KillSwitchActive = "KILL_SWITCH_ACTIVE";
        public const string UserLiveEnableRequired = "USER_LIVE_ENABLE_REQUIRED";
        public const string BrokerReadinessFailed = "BROKER_READINESS_FAILED";
        public const string TestStatusNotVerified = "TEST_STATUS_NOT_VERIFIED";
        public const string DemoForwardTestNotPassed = "DEMO_FORWARD_TEST_NOT_PASSED";
        public const string RolloutStageBlocked = "ROLLOUT_STAGE_BLOCKED";
    }

    public sealed record LiveReadinessContext
    {
        public bool IsLiveMode { get; init; }
        public bool KillSwitchActive { get; init; }
        public bool EmergencyStopActive { get; init; }
        public BrokerDeploymentChecklistResult? BrokerDeploymentResult { get; init; }
        public RolloutEvaluationResult? RolloutEvaluation { get; init; }
    }

    public sealed record LiveReadinessResult
    {
        public bool IsAllowed { get; init; }
        public IReadOnlyList<string> FailedCriteria { get; init; } = [];
        public string EvidenceClassification { get; init; } = "";
        public string StrategyEdgeVerdict { get; init; } = "";
        public string DemoReconciliationVerdict { get; init; } = "";
        public DemoForwardTestResult? DemoForwardTestResult { get; init; }
        public BrokerDeploymentChecklistResult? BrokerDeploymentResult { get; init; }
        public RolloutEvaluationResult? RolloutEvaluation { get; init; }

        public string FailureMessage =>
            IsAllowed
                ? ""
                : $"Final live readiness gate blocked live trading: {string.Join(", ", FailedCriteria)}";
    }

    public sealed record LiveReadinessEvidence
    {
        public string EvidenceClassification { get; init; } = "";
        public string StrategyEdgeVerdict { get; init; } = "";
        public string DemoReconciliationVerdict { get; init; } = "";
    }
}
