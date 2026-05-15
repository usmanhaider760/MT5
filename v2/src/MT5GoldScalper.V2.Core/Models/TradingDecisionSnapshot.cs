namespace MT5GoldScalper.V2.Core.Models;

public sealed class TradingDecisionSnapshot
{
    public string SnapshotId { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime AsOfUtc { get; set; } = DateTime.UtcNow;
    public string Pair { get; set; } = "XAUUSD";
    public MarketDataModel Market { get; set; } = new();
    public AccountRiskModel AccountRisk { get; set; } = new();
    public SessionNewsModel SessionNews { get; set; } = new();
    public StrategySignalModel StrategySignal { get; set; } = new();
    public ExecutionSafetyModel ExecutionSafety { get; set; } = new();
    public SignalDecision SignalDecision { get; set; } = SignalDecision.Wait;
    public ExecutionReadiness ExecutionReadiness { get; set; } = ExecutionReadiness.Review;
    public TradeDirection TradeDirection { get; set; } = TradeDirection.None;
    public string FinalDecisionText => ExecutionReadiness == ExecutionReadiness.Blocked
        ? "BLOCKED"
        : SignalDecision.ToString().ToUpperInvariant();
    public bool CanPlaceTrade =>
        ExecutionReadiness == ExecutionReadiness.Ready &&
        SignalDecision is SignalDecision.Buy or SignalDecision.Sell &&
        TradeDirection is TradeDirection.Buy or TradeDirection.Sell &&
        BlockReasons.All(reason => !reason.IsHardBlock);
    public BlockReason? PrimaryBlockReason => BlockReasons.FirstOrDefault(reason => reason.IsHardBlock) ?? BlockReasons.FirstOrDefault();
    public decimal ConfidenceScore { get; set; }
    public List<BlockReason> BlockReasons { get; set; } = [];
    public List<DecisionSectionModel> Sections { get; set; } = [];
}

public enum SignalDecision
{
    Buy,
    Sell,
    Wait,
    Skip
}

public enum ExecutionReadiness
{
    Ready,
    Blocked,
    Review
}

public enum TradeDirection
{
    Buy,
    Sell,
    None
}

public enum BlockReasonCode
{
    TerminalDisconnected,
    TradingDisabled,
    MarketClosed,
    StalePrice,
    SessionNotAllowed,
    NewsBlackout,
    SpreadTooWide,
    InsufficientMargin,
    RiskLimitReached,
    DuplicateTrade,
    InvalidStops,
    InvalidVolume,
    OrderCheckFailed
}

public sealed class BlockReason
{
    public BlockReasonCode Code { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = "BLOCK";
    public string Source { get; set; } = "AUTO";
    public bool IsHardBlock { get; set; } = true;
}

public sealed class MarketDataModel
{
    public string Pair { get; set; } = "XAUUSD";
    public decimal Bid { get; set; }
    public decimal Ask { get; set; }
    public decimal CurrentPrice { get; set; }
    public int Digits { get; set; }
    public decimal Point { get; set; }
    public decimal TickSize { get; set; }
    public decimal TickValue { get; set; }
    public decimal ContractSize { get; set; }
    public decimal SpreadPrice { get; set; }
    public int SpreadPoints { get; set; }
    public decimal SpreadPips { get; set; }
    public decimal SpreadPercentOfTp1 { get; set; }
    public DateTime LastTickTimeUtc { get; set; } = DateTime.UtcNow;
    public int LastTickAgeMs { get; set; }
    public decimal MinLot { get; set; }
    public decimal MaxLot { get; set; }
    public decimal LotStep { get; set; }
    public int StopsLevelPoints { get; set; }
    public int FreezeLevelPoints { get; set; }
    public bool MarketOpen { get; set; } = true;
    public bool PriceFresh { get; set; } = true;
}

public sealed class AccountRiskModel
{
    public decimal Balance { get; set; }
    public decimal Equity { get; set; }
    public decimal FreeMargin { get; set; }
    public decimal MarginUsed { get; set; }
    public decimal MarginLevel { get; set; }
    public decimal RiskPercent { get; set; }
    public decimal RiskAmount { get; set; }
    public decimal LotSize { get; set; }
    public decimal EstimatedMarginRequired { get; set; }
    public decimal DailyProfitLoss { get; set; }
    public decimal DailyLossLimit { get; set; }
    public decimal DailyLossRemaining { get; set; }
    public int TradesTakenToday { get; set; }
    public int MaxTradesPerDay { get; set; }
    public int OpenPositionsCount { get; set; }
    public bool SamePairOpenPosition { get; set; }
    public bool DuplicateTradeAllowed { get; set; }
}

public sealed class SessionNewsModel
{
    public string CurrentSession { get; set; } = "Unknown";
    public bool IsSessionAllowed { get; set; }
    public DateTime UtcTime { get; set; } = DateTime.UtcNow;
    public DateTime ServerTime { get; set; } = DateTime.UtcNow;
    public string NextHighImpactEvent { get; set; } = "None";
    public string NewsCurrency { get; set; } = "USD";
    public string NewsImpact { get; set; } = "Low";
    public DateTime? NewsTimeUtc { get; set; }
    public bool NewsBlackoutActive { get; set; }
    public int MinutesToNews { get; set; }
}

public sealed class StrategySignalModel
{
    public string SetupDirection { get; set; } = "NONE";
    public string HigherTimeframeTrend { get; set; } = "Range";
    public string M15Trend { get; set; } = "Range";
    public string M5Trend { get; set; } = "Range";
    public bool TrendAligned { get; set; }
    public decimal Alma34 { get; set; }
    public decimal Alma99 { get; set; }
    public bool AlmaPass { get; set; }
    public decimal RsiValue { get; set; }
    public bool RsiPass { get; set; }
    public decimal AtrValue { get; set; }
    public bool AtrPass { get; set; }
    public decimal BollingerUpper { get; set; }
    public decimal BollingerMiddle { get; set; }
    public decimal BollingerLower { get; set; }
    public bool BollingerPass { get; set; }
    public bool LiquiditySweepFound { get; set; }
    public string LiquiditySweepSide { get; set; } = "None";
    public bool MomentumCandleFound { get; set; }
    public bool ConfirmationCandleClosed { get; set; }
    public decimal EntryPrice { get; set; }
    public decimal StopLossPrice { get; set; }
    public decimal Tp1Price { get; set; }
    public decimal Tp2Price { get; set; }
    public decimal StopLossPips { get; set; }
    public decimal Tp1Pips { get; set; }
    public decimal Tp2Pips { get; set; }
    public decimal RiskRewardTp1 { get; set; }
    public decimal RiskRewardTp2 { get; set; }
    public decimal EstimatedTp1Profit { get; set; }
    public decimal EstimatedTp2Profit { get; set; }
}

public sealed class ExecutionSafetyModel
{
    public bool TerminalConnected { get; set; }
    public bool TradingAllowed { get; set; }
    public bool MarketOpen { get; set; }
    public bool PriceFresh { get; set; }
    public bool SpreadAcceptable { get; set; }
    public bool VolumeValid { get; set; }
    public bool StopsValid { get; set; }
    public bool MarginEnough { get; set; }
    public bool RiskLimitPass { get; set; }
    public bool NewsFilterPass { get; set; }
    public bool DuplicateTradePass { get; set; }
    public bool RiskWithinLimits { get; set; }
    public bool OrderCheckPassed { get; set; }
    public int OrderCheckRetcode { get; set; }
    public string OrderCheckComment { get; set; } = string.Empty;
    public bool FinalReadyToTrade { get; set; }
}

public sealed class DecisionSectionModel
{
    public string Title { get; set; } = string.Empty;
    public string DisplayTitle => Title.ToUpperInvariant();
    public string Icon { get; set; } = "*";
    public string Status { get; set; } = "INFO";
    public string Severity { get; set; } = "Good";
    public decimal Score { get; set; }
    public string Percentage => $"{Score:0}%";
    public List<DecisionCheckModel> Summary { get; set; } = [];
    public List<DecisionCheckModel> Details { get; set; } = [];
}

public sealed class DecisionCheckModel
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public decimal Score { get; set; }
    public string Percentage => $"{Score:0}%";
    public string Source { get; set; } = "AUTO";
    public string Note { get; set; } = string.Empty;
}
