using MT5TradingBot.Data;
using MT5TradingBot.Models;
using MT5TradingBot.Modules.Alerts;
using MT5TradingBot.Modules.Monitoring;

namespace MT5TradingBot.Modules.LiveReadiness
{
    public static class OperationalReadinessStatuses
    {
        public const string Ready = "Ready";
        public const string NotReady = "Not Ready";
        public const string Warning = "Warning";
        public const string Unknown = "Unknown";
    }

    public sealed record OperationalReadinessReportInput
    {
        public BotConfig Config { get; init; } = new();
        public LiveReadinessResult? LiveReadiness { get; init; }
        public DemoForwardTestResult? DemoForwardTest { get; init; }
        public BrokerDeploymentChecklistResult? BrokerDeployment { get; init; }
        public RuntimeHealthSnapshot? RuntimeHealth { get; init; }
        public KillSwitchState? KillSwitch { get; init; }
        public IReadOnlyList<TradeRecord> RecentTrades { get; init; } = [];
        public IReadOnlyList<SafetyAlert> RecentAlerts { get; init; } = [];
        public string OutputDirectory { get; init; } = "";
        public string StrategyEvidenceClassification { get; init; } = "";
        public string ModeRecommendation { get; init; } = "";
        public DateTime? TimestampUtc { get; init; }
    }

    public sealed record OperationalReadinessReportResult
    {
        public string OverallReadiness { get; init; } = OperationalReadinessStatuses.Unknown;
        public string ReportPath { get; init; } = "";
        public IReadOnlyList<string> FailedCriteria { get; init; } = [];
        public IReadOnlyList<string> Warnings { get; init; } = [];
        public string RecommendedAction { get; init; } = "";
        public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
        public string Markdown { get; init; } = "";
    }
}
