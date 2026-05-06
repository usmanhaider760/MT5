using MT5TradingBot.Models;

namespace MT5TradingBot.Modules.TradeRules
{
    public enum TradeRulesStrategy
    {
        Unknown = 0,
        Scalping = 1,
        Normal = 2
    }

    public sealed class TradeRulesContext
    {
        public string Pair { get; set; } = "";
        public TradeRulesStrategy Strategy { get; set; } = TradeRulesStrategy.Unknown;
        public long? Ticket { get; set; }
        public TradeType? TradeType { get; set; }
        public string? RequestId { get; set; }
        public bool IsRunningTrade { get; set; }
        public string OpenedFrom { get; set; } = "";
        public string? RawLogLine { get; set; }
        public string? OpenedLogTimestamp { get; set; }
    }

    public sealed class TradeRuleRuntimeSnapshot
    {
        public string RuleCode { get; set; } = "";
        public string RuleName { get; set; } = "";
        public string Category { get; set; } = "";
        public string GroupName { get; set; } = "";

        public string FunctionName { get; set; } = "";
        public string VariableName { get; set; } = "";
        public string SourceFile { get; set; } = "";
        public string SourceName { get; set; } = "";

        public bool IsEnabled { get; set; } = true;
        public bool IsCritical { get; set; }

        public object? StandardValue { get; set; }
        public object? ConfiguredValue { get; set; }
        public object? LiveValue { get; set; }
        public object? PreviewValue { get; set; }

        public double? MinValue { get; set; }
        public double? MaxValue { get; set; }
        public string Unit { get; set; } = "";

        public string Result { get; set; } = TradeRuleResults.NotChecked;
        public string? WouldHaveResult { get; set; }
        public string Reason { get; set; } = "";
        public string ActualEffect { get; set; } = "";
        public string RuntimeMode { get; set; } = TradeRuleRuntimeModes.NotAvailable;
        public string ValueType { get; set; } = TradeRuleValueTypes.Number;
        public DateTime? LastCheckedAtUtc { get; set; }
    }

    public sealed class TradeRuleCatalogItem
    {
        public string RuleCode { get; set; } = "";
        public string RuleName { get; set; } = "";
        public string Category { get; set; } = "";
        public string GroupName { get; set; } = "";
        public string SourceName { get; set; } = "";
        public string FunctionName { get; set; } = "";
        public string VariableName { get; set; } = "";
        public string SourceFile { get; set; } = "";
        public bool IsCritical { get; set; }
        public bool IsEditable { get; set; } = true;
        public string ValueType { get; set; } = TradeRuleValueTypes.Number;
    }

    public sealed class TradeRuleDecisionSummary
    {
        public string CurrentDecision { get; set; } = "UNKNOWN";
        public string MainBlockingRule { get; set; } = "";
        public string RiskLevel { get; set; } = "Low";
        public int Passed { get; set; }
        public int Warning { get; set; }
        public int Blocked { get; set; }
        public int Disabled { get; set; }
        public int DisabledButWouldBlock { get; set; }
    }

    public sealed class TradeRulesRuntimeSnapshotResult
    {
        public TradeRulesContext Context { get; set; } = new();
        public AccountInfo? Account { get; set; }
        public SymbolInfo? Symbol { get; set; }
        public LivePosition? Position { get; set; }
        public IReadOnlyList<TradeRuleRuntimeSnapshot> Rules { get; set; } = [];
        public TradeRuleDecisionSummary Summary { get; set; } = new();
        public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;
    }

    public static class TradeRuleResults
    {
        public const string Pass = "PASS";
        public const string Warning = "WARNING";
        public const string Block = "BLOCK";
        public const string Disabled = "DISABLED";
        public const string NotChecked = "NOT_CHECKED";
    }

    public static class TradeRuleValueTypes
    {
        public const string Number = "Number";
        public const string Bool = "Bool";
        public const string Enum = "Enum";
        public const string Text = "Text";
        public const string List = "List";
    }

    public static class TradeRuleRuntimeModes
    {
        public const string RuntimeControllable = "Runtime Controllable";
        public const string SaveOnly = "Save Only";
        public const string MonitorOnly = "Monitor Only";
        public const string NotAvailable = "Not Available";
    }

    public sealed class TradeRulesRuntimeApplyResult
    {
        public int AppliedCount { get; set; }
        public int SkippedMonitorOnlyCount { get; set; }
        public int FailedCount { get; set; }
    }

    public sealed class TradeRuleAuditSnapshot
    {
        public string RuleCode { get; set; } = "";
        public string RuleName { get; set; } = "";
        public string Result { get; set; } = TradeRuleResults.NotChecked;
        public string Reason { get; set; } = "";
        public int Order { get; set; }
        public DateTime CheckedAtUtc { get; set; } = DateTime.UtcNow;
        public string Pair { get; set; } = "";
        public string RequestId { get; set; } = "";
    }
}
