using Newtonsoft.Json;

namespace MT5TradingBot.Models
{
    [JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
    public enum SignalDirection
    {
        Hold,
        Buy,
        Sell
    }

    [JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
    public enum RiskLevel
    {
        Low,
        Medium,
        High,
        Blocked
    }

    public sealed class MarketSignal
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        public string Pair { get; set; } = "";
        public SignalDirection Direction { get; set; } = SignalDirection.Hold;
        public OrderType OrderType { get; set; } = OrderType.MARKET;
        public double EntryPrice { get; set; }
        public double StopLoss { get; set; }
        public double TakeProfit { get; set; }
        public double TakeProfit2 { get; set; }
        public string Source { get; set; } = "";
        public string Reason { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public sealed class AiAnalysisResult
    {
        public string SignalId { get; set; } = "";
        public string Pair { get; set; } = "";
        public SignalDirection Direction { get; set; } = SignalDirection.Hold;
        public double EntryPrice { get; set; }
        public double StopLoss { get; set; }
        public double TakeProfit { get; set; }
        public int ConfidenceScore { get; set; }
        public RiskLevel RiskLevel { get; set; } = RiskLevel.Medium;
        public string Reason { get; set; } = "";
        public string InvalidationCondition { get; set; } = "";
        public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
    }

    public sealed class RiskValidationResult
    {
        public bool IsApproved { get; set; }
        public RiskLevel RiskLevel { get; set; } = RiskLevel.Blocked;
        public string Reason { get; set; } = "";
        public double RiskPercent { get; set; }
        public double DollarRisk { get; set; }
        public double RiskRewardRatio { get; set; }
        public double SpreadPips { get; set; }
        public double ReferenceEntryPrice { get; set; }
        public double ValidatedLotSize { get; set; }
        public IReadOnlyList<string> Warnings { get; set; } = [];
    }

    public sealed class UserApprovalDecision
    {
        public string SignalId { get; set; } = "";
        public bool IsApproved { get; set; }
        public string ApprovedBy { get; set; } = "";
        public string ApprovalMode { get; set; } = "";
        public string Notes { get; set; } = "";
        public DateTime DecidedAt { get; set; } = DateTime.UtcNow;
    }

    public sealed class TradeAuditEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        public string SignalId { get; set; } = "";
        public string Action { get; set; } = "";
        public string Reason { get; set; } = "";
        public string InputJson { get; set; } = "";
        public string ResultJson { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public sealed class PairScanResult
    {
        public string Pair { get; set; } = "";
        public bool IsAvailable { get; set; }
        public double SpreadPips { get; set; }
        public double Score { get; set; }
        public string Reason { get; set; } = "";
        public DateTime ScannedAt { get; set; } = DateTime.UtcNow;
    }

    public sealed class TradeMonitoringSnapshot
    {
        public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
        public int OpenPositionCount { get; set; }
        public double FloatingProfit { get; set; }
        public IReadOnlyList<LivePosition> Positions { get; set; } = [];
        public IReadOnlyList<string> Events { get; set; } = [];
    }

    public sealed class NewsEvent
    {
        public string Currency { get; set; } = "";
        public string Title { get; set; } = "";
        public string Impact { get; set; } = "";
        public DateTime EventTimeUtc { get; set; }
    }

    public sealed class NewsFilterResult
    {
        public bool IsBlocked { get; set; }
        public string Reason { get; set; } = "";
        public IReadOnlyList<NewsEvent> BlockingEvents { get; set; } = [];
    }

    public sealed class BacktestTrade
    {
        public MarketSignal Signal { get; set; } = new();
        public double EntryPrice { get; set; }
        public double ExitPrice { get; set; }
        public double Lots { get; set; } = 0.01;
        public DateTime OpenedAt { get; set; }
        public DateTime ClosedAt { get; set; }
    }

    public sealed class BacktestResult
    {
        public int TotalTrades { get; set; }
        public int WinningTrades { get; set; }
        public int LosingTrades { get; set; }
        public double NetProfitPips { get; set; }
        public double WinRatePercent { get; set; }
        public IReadOnlyList<string> Notes { get; set; } = [];
    }
}
