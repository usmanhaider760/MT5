namespace MT5TradingBot.Modules.LiveReadiness
{
    public static class BrokerDeploymentVerdicts
    {
        public const string Pass = "Pass";
        public const string Fail = "Fail";
        public const string Inconclusive = "Inconclusive";
    }

    public static class BrokerDeploymentCodes
    {
        public const string Mt5Disconnected = "BROKER_MT5_DISCONNECTED";
        public const string EaNotResponding = "BROKER_EA_NOT_RESPONDING";
        public const string SymbolMetadataUnavailable = "BROKER_SYMBOL_METADATA_UNAVAILABLE";
        public const string StopLevelUnavailable = "BROKER_STOP_LEVEL_UNAVAILABLE";
        public const string FreezeLevelUnavailable = "BROKER_FREEZE_LEVEL_UNAVAILABLE";
        public const string LotMetadataUnavailable = "BROKER_LOT_METADATA_UNAVAILABLE";
        public const string MarginEstimateUnavailable = "BROKER_MARGIN_ESTIMATE_UNAVAILABLE";
        public const string OrderCheckUnavailable = "BROKER_ORDERCHECK_UNAVAILABLE";
        public const string OrderCheckRejected = "BROKER_ORDERCHECK_REJECTED";
        public const string AccountUnavailable = "BROKER_ACCOUNT_UNAVAILABLE";
        public const string NewsUnavailable = "BROKER_NEWS_UNAVAILABLE";
        public const string LatencyUnavailable = "BROKER_LATENCY_UNAVAILABLE";
        public const string LatencyTooHigh = "BROKER_LATENCY_TOO_HIGH";
    }

    public sealed record BrokerDeploymentCheckItem
    {
        public string Name { get; init; } = "";
        public bool Passed { get; init; }
        public string Code { get; init; } = "";
        public string Message { get; init; } = "";
    }

    public sealed record BrokerDeploymentChecklistResult
    {
        public bool Passed { get; init; }
        public string Verdict { get; init; } = BrokerDeploymentVerdicts.Inconclusive;
        public IReadOnlyList<string> FailedCriteria { get; init; } = [];
        public IReadOnlyList<string> Warnings { get; init; } = [];
        public IReadOnlyList<BrokerDeploymentCheckItem> CheckedItems { get; init; } = [];
        public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
        public double? LatencyMs { get; init; }
        public string EaVersion { get; init; } = "";
        public string EaBuildIdentifier { get; init; } = "";
    }
}
