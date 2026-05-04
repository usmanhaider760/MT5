using Newtonsoft.Json;

namespace MT5TradingBot.Modules.Alerts
{
    public static class SafetyAlertSeverities
    {
        public const string Info = "Info";
        public const string Warning = "Warning";
        public const string Critical = "Critical";
    }

    public static class SafetyAlertCategories
    {
        public const string KillSwitch = "KillSwitch";
        public const string LiveReadiness = "LiveReadiness";
        public const string Mt5Connection = "MT5Connection";
        public const string EaHeartbeat = "EAHeartbeat";
        public const string Margin = "Margin";
        public const string RiskLimits = "RiskLimits";
        public const string ExecutionQuality = "ExecutionQuality";
        public const string OrderRejection = "OrderRejection";
        public const string News = "News";
        public const string OrderCheck = "OrderCheck";
        public const string EmergencyDrawdown = "EmergencyDrawdown";
        public const string AccountData = "AccountData";
        public const string SymbolData = "SymbolData";
    }

    public static class SafetyAlertCodes
    {
        public const string KillSwitchActive = "KILL_SWITCH_ACTIVE";
        public const string LiveReadinessGateBlocked = "LIVE_READINESS_GATE_BLOCKED";
        public const string Mt5Disconnected = "MT5_DISCONNECTED";
        public const string EaHeartbeatFailed = "EA_HEARTBEAT_FAILED";
        public const string MarginDataUnavailable = "MARGIN_DATA_UNAVAILABLE";
        public const string MarginLevelCritical = "MARGIN_LEVEL_CRITICAL";
        public const string DailyLossWarning = "DAILY_LOSS_WARNING";
        public const string DailyLossLimit = "DAILY_LOSS_LIMIT";
        public const string WeeklyLossWarning = "WEEKLY_LOSS_WARNING";
        public const string WeeklyLossLimit = "WEEKLY_LOSS_LIMIT";
        public const string HighSpreadDrift = "HIGH_SPREAD_DRIFT";
        public const string HighSlippageDrift = "HIGH_SLIPPAGE_DRIFT";
        public const string RepeatedOrderRejection = "REPEATED_ORDER_REJECTION";
        public const string NewsDataUnavailable = "NEWS_DATA_UNAVAILABLE";
        public const string OrderCheckUnavailable = "ORDERCHECK_UNAVAILABLE";
        public const string OrderCheckRejected = "ORDERCHECK_REJECTED";
        public const string EmergencyCloseAttempted = "EMERGENCY_CLOSE_ATTEMPTED";
        public const string EmergencyCloseFailed = "EMERGENCY_CLOSE_FAILED";
        public const string AccountDataUnavailable = "ACCOUNT_DATA_UNAVAILABLE";
        public const string SymbolMetadataUnavailable = "SYMBOL_METADATA_UNAVAILABLE";
    }

    public sealed class SafetyAlert
    {
        [JsonProperty("alert_id")]
        public string AlertId { get; set; } = Guid.NewGuid().ToString("N");

        [JsonProperty("severity")]
        public string Severity { get; set; } = SafetyAlertSeverities.Info;

        [JsonProperty("category")]
        public string Category { get; set; } = "";

        [JsonProperty("message")]
        public string Message { get; set; } = "";

        [JsonProperty("timestamp_utc")]
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

        [JsonProperty("last_seen_utc")]
        public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;

        [JsonProperty("related_code")]
        public string RelatedCode { get; set; } = "";

        [JsonProperty("recommended_action")]
        public string RecommendedAction { get; set; } = "";

        [JsonProperty("acknowledged")]
        public bool Acknowledged { get; set; }

        [JsonProperty("occurrence_count")]
        public int OccurrenceCount { get; set; } = 1;
    }

    public sealed record SafetyAlertRequest
    {
        public string Severity { get; init; } = SafetyAlertSeverities.Info;
        public string Category { get; init; } = "";
        public string Message { get; init; } = "";
        public string RelatedCode { get; init; } = "";
        public string RecommendedAction { get; init; } = "";
    }
}
