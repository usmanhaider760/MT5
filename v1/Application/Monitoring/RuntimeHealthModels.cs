using MT5TradingBot.Models;

namespace MT5TradingBot.Modules.Monitoring
{
    public static class RuntimeHealthStatuses
    {
        public const string Healthy = "Healthy";
        public const string Warning = "Warning";
        public const string Critical = "Critical";
        public const string Unknown = "Unknown";
    }

    public static class RuntimeHealthCodes
    {
        public const string Mt5Disconnected = "RUNTIME_MT5_DISCONNECTED";
        public const string EaHeartbeatFailed = "RUNTIME_EA_HEARTBEAT_FAILED";
        public const string LatencyUnavailable = "RUNTIME_LATENCY_UNAVAILABLE";
        public const string LatencyHigh = "RUNTIME_LATENCY_HIGH";
        public const string LatencyCritical = "RUNTIME_LATENCY_CRITICAL";
        public const string SpreadDriftHigh = "RUNTIME_SPREAD_DRIFT_HIGH";
        public const string SlippageDriftHigh = "RUNTIME_SLIPPAGE_DRIFT_HIGH";
        public const string OrderRejectionRateHigh = "RUNTIME_ORDER_REJECTION_RATE_HIGH";
        public const string OrderRejectionRateCritical = "RUNTIME_ORDER_REJECTION_RATE_CRITICAL";
        public const string DrawdownWarning = "RUNTIME_DRAWDOWN_WARNING";
        public const string DrawdownCritical = "RUNTIME_DRAWDOWN_CRITICAL";
        public const string KillSwitchActive = "RUNTIME_KILL_SWITCH_ACTIVE";
        public const string DailyLossWarning = "RUNTIME_DAILY_LOSS_WARNING";
        public const string WeeklyLossWarning = "RUNTIME_WEEKLY_LOSS_WARNING";
        public const string SymbolExposureWarning = "RUNTIME_SYMBOL_EXPOSURE_WARNING";
        public const string MarginLevelCritical = "RUNTIME_MARGIN_LEVEL_CRITICAL";
        public const string MissingAccountData = "RUNTIME_ACCOUNT_DATA_MISSING";
        public const string MissingSymbolData = "RUNTIME_SYMBOL_DATA_MISSING";
        public const string MissingPositionData = "RUNTIME_POSITION_DATA_MISSING";
        public const string NewsUnavailable = "RUNTIME_NEWS_UNAVAILABLE";
    }

    public sealed record RuntimeHealthInput
    {
        public TradeRequest ProbeRequest { get; init; } = new()
        {
            Pair = "EURUSD",
            StopLoss = 1.0950,
            TakeProfit = 1.1100
        };

        public BotConfig Config { get; init; } = new();
        public bool IsLiveMode { get; init; }
        public bool KillSwitchActive { get; init; }
        public double? SpreadDriftPips { get; init; }
        public double? SlippageDriftPips { get; init; }
        public double? OrderRejectionRatePercent { get; init; }
        public double? DrawdownPercent { get; init; }
        public double? DailyLossUsagePercent { get; init; }
        public double? WeeklyLossUsagePercent { get; init; }
        public double? SymbolExposureUsagePercent { get; init; }
    }

    public sealed record RuntimeHealthMetricValues
    {
        public bool? Mt5Connected { get; init; }
        public bool? EaHealthy { get; init; }
        public double? LatencyMs { get; init; }
        public double? SpreadPips { get; init; }
        public double? SpreadDriftPips { get; init; }
        public double? SlippageDriftPips { get; init; }
        public double? OrderRejectionRatePercent { get; init; }
        public double? DrawdownPercent { get; init; }
        public bool KillSwitchActive { get; init; }
        public double? DailyLossUsagePercent { get; init; }
        public double? WeeklyLossUsagePercent { get; init; }
        public double? SymbolExposureUsagePercent { get; init; }
        public double? MarginLevelPercent { get; init; }
        public int? OpenPositionCount { get; init; }
        public bool? NewsProviderHealthy { get; init; }
        public string EaVersion { get; init; } = "";
        public string EaBuildIdentifier { get; init; } = "";
    }

    public sealed record RuntimeHealthSnapshot
    {
        public string OverallStatus { get; init; } = RuntimeHealthStatuses.Unknown;
        public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
        public RuntimeHealthMetricValues Metrics { get; init; } = new();
        public IReadOnlyList<string> Warnings { get; init; } = [];
        public IReadOnlyList<string> CriticalIssues { get; init; } = [];
        public string RecommendedAction { get; init; } = "";
        public bool HasCriticalIssues => CriticalIssues.Count > 0;
    }
}
