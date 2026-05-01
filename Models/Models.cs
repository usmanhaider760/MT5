using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MT5TradingBot.Models
{
    // ══════════════════════════════════════════════════════════════
    //  ENUMS
    // ══════════════════════════════════════════════════════════════

    [JsonConverter(typeof(StringEnumConverter))]
    public enum TradeType { BUY, SELL }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum OrderType { MARKET, LIMIT, STOP }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum TradeStatus
    {
        Pending, Submitted, Filled, PartiallyFilled,
        Cancelled, Rejected, Closed, Error
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ConnectionMode { NamedPipe, Socket }

    public enum SignalCardStatus { Pending, Executing, Executed, Rejected, Error }

    // Monitor=0  ManualApproval=1  FullAuto=2  (index order must not change — ReviewTradeForm uses cast)
    public enum BotMode { Monitor, ManualApproval, FullAuto }

    public enum ScalpingDirectionMode { Auto, SignalDirection, BuyOnly, SellOnly }

    public sealed record class SignalCardInfo
    {
        public string          SignalId    { get; init; } = "";
        public string          FileName    { get; init; } = "";
        public string          FilePath    { get; init; } = "";
        public string          RawJson     { get; init; } = "";
        public string          Pair        { get; init; } = "";
        public string          TradeType   { get; init; } = "";
        public double          StopLoss    { get; init; }
        public double          TakeProfit  { get; init; }
        public double          LotSize     { get; init; }
        public SignalCardStatus Status      { get; set;  } = SignalCardStatus.Pending;
        public string          StatusText  { get; set;  } = "";
        public long            Ticket      { get; set;  } = 0;
        public DateTime        CreatedAt   { get; init; } = DateTime.MinValue;
        public DateTime        Time        { get; set;  } = DateTime.Now;
    }

    // ══════════════════════════════════════════════════════════════
    //  TRADE REQUEST  — JSON input from bot/user
    // ══════════════════════════════════════════════════════════════

    public sealed class TradeRequest
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpper();

        [JsonProperty("pair")]
        public string Pair { get; set; } = "";

        [JsonProperty("trade_type")]
        public TradeType TradeType { get; set; } = TradeType.BUY;

        [JsonProperty("order_type")]
        public OrderType OrderType { get; set; } = OrderType.MARKET;

        /// <summary>0 = market price for MARKET orders</summary>
        [JsonProperty("entry_price")]
        public double EntryPrice { get; set; } = 0;

        [JsonProperty("stop_loss")]
        public double StopLoss { get; set; }

        [JsonProperty("take_profit")]
        public double TakeProfit { get; set; }

        /// <summary>Optional second TP. 0 = disabled.</summary>
        [JsonProperty("take_profit_2")]
        public double TakeProfit2 { get; set; } = 0;

        [JsonProperty("lot_size")]
        public double LotSize { get; set; } = 0.01;

        [JsonProperty("max_spread_pips")]
        public double MaxSpreadPips { get; set; } = 0;

        [JsonProperty("comment")]
        public string Comment { get; set; } = "MT5Bot";

        [JsonProperty("magic_number")]
        public int MagicNumber { get; set; } = 999001;

        [JsonProperty("expiry_minutes")]
        public int ExpiryMinutes { get; set; } = 60;

        [JsonProperty("move_sl_to_be_after_tp1")]
        public bool MoveSLToBreakevenAfterTP1 { get; set; } = true;

        [JsonProperty("sl_to_be_trigger_pct")]
        public double SlToBeTrigerPct { get; set; } = 0.6; // trigger BE at 60% of TP distance

        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // ── Validation ────────────────────────────────────────────

        public (bool Valid, string Error) Validate()
        {
            if (string.IsNullOrWhiteSpace(Pair))
                return (false, "Pair is required");
            if (StopLoss == 0)
                return (false, "StopLoss cannot be 0");
            if (TakeProfit == 0)
                return (false, "TakeProfit cannot be 0");
            if (LotSize < 0.01)
                return (false, $"LotSize {LotSize} is below minimum 0.01");
            if (OrderType != OrderType.MARKET && EntryPrice == 0)
                return (false, "EntryPrice required for LIMIT/STOP orders");

            // Directional sanity
            if (TradeType == TradeType.BUY)
            {
                if (StopLoss >= (EntryPrice > 0 ? EntryPrice : TakeProfit))
                    return (false, "BUY: StopLoss must be below entry price");
                if (TakeProfit <= (EntryPrice > 0 ? EntryPrice : StopLoss))
                    return (false, "BUY: TakeProfit must be above entry price");
            }
            else
            {
                if (StopLoss <= (EntryPrice > 0 ? EntryPrice : TakeProfit))
                    return (false, "SELL: StopLoss must be above entry price");
                if (TakeProfit >= (EntryPrice > 0 ? EntryPrice : StopLoss))
                    return (false, "SELL: TakeProfit must be below entry price");
            }

            return (true, "");
        }

        public override string ToString() =>
            $"[{Id}] {TradeType} {Pair} @ {(EntryPrice == 0 ? "MARKET" : EntryPrice.ToString("F5"))} " +
            $"SL:{StopLoss:F5} TP:{TakeProfit:F5} Lots:{LotSize:F2}";
    }

    // ══════════════════════════════════════════════════════════════
    //  TRADE RESULT  — response from MT5 EA
    // ══════════════════════════════════════════════════════════════

    public sealed class TradeResult
    {
        public string RequestId { get; set; } = "";
        public TradeStatus Status { get; set; }
        public long Ticket { get; set; }
        public double ExecutedPrice { get; set; }
        public double ExecutedLots { get; set; }
        public string ErrorCode { get; set; } = "";
        public string ErrorMessage { get; set; } = "";
        public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;

        public bool IsSuccess =>
            Status is TradeStatus.Filled or TradeStatus.Submitted;

        public override string ToString() =>
            IsSuccess
                ? $"✅ #{Ticket} filled @ {ExecutedPrice:F5} | {ExecutedLots:F2} lots"
                : $"❌ [{ErrorCode}] {ErrorMessage}";
    }

    // ══════════════════════════════════════════════════════════════
    //  LIVE POSITION
    // ══════════════════════════════════════════════════════════════

    public sealed class LivePosition
    {
        public long Ticket { get; set; }
        public string Symbol { get; set; } = "";
        public TradeType Type { get; set; }
        public double Lots { get; set; }
        public double OpenPrice { get; set; }
        public double CurrentPrice { get; set; }
        public double StopLoss { get; set; }
        public double TakeProfit { get; set; }
        public double Profit { get; set; }
        public int MagicNumber { get; set; }
        public string Comment { get; set; } = "";
        public DateTime OpenTime { get; set; }
        public bool SlMovedToBreakeven { get; set; }   // tracked by us

        // Computed
        public double ProfitPips =>
            Type == TradeType.BUY
                ? (CurrentPrice - OpenPrice) * (Symbol.Contains("JPY") ? 100 : 10000)
                : (OpenPrice - CurrentPrice) * (Symbol.Contains("JPY") ? 100 : 10000);
    }

    // ══════════════════════════════════════════════════════════════
    //  ACCOUNT INFO
    // ══════════════════════════════════════════════════════════════

    public sealed class AccountInfo
    {
        public long AccountNumber { get; set; }
        public string Name { get; set; } = "";
        public string Server { get; set; } = "";
        public string Currency { get; set; } = "USD";
        public double Balance { get; set; }
        public double Equity { get; set; }
        public double Margin { get; set; }
        public double FreeMargin { get; set; }
        public double MarginLevel { get; set; }
        public double Profit { get; set; }
        public int Leverage { get; set; }
        public bool IsConnected { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    // ══════════════════════════════════════════════════════════════
    //  BOT CONFIGURATION
    // ══════════════════════════════════════════════════════════════

    public sealed class BotConfig
    {
        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = false;

        [JsonProperty("watch_folder")]
        public string WatchFolder { get; set; } = @"C:\MT5Bot\signals";

        [JsonProperty("poll_interval_ms")]
        public int PollIntervalMs { get; set; } = 2000;

        [JsonProperty("max_risk_percent")]
        public double MaxRiskPercent { get; set; } = 1.0;

        [JsonProperty("max_trades_per_day")]
        public int MaxTradesPerDay { get; set; } = 5;

        /// <summary>
        /// Maximum number of simultaneously open positions managed by this bot
        /// (matched by MagicNumber). 0 = disabled (no cap).
        /// </summary>
        [JsonProperty("max_concurrent_positions")]
        public int MaxConcurrentPositions { get; set; } = 3;

        [JsonProperty("allowed_pairs")]
        public List<string> AllowedPairs { get; set; } = [];

        [JsonProperty("auto_lot_calculation")]
        public bool AutoLotCalculation { get; set; } = true;

        [JsonProperty("magic_number")]
        public int MagicNumber { get; set; } = 999001;

        [JsonProperty("min_rr_ratio")]
        public double MinRRRatio { get; set; } = 1.5;

        [JsonProperty("enforce_rr")]
        public bool EnforceRR { get; set; } = true;

        [JsonProperty("max_spread_pips")]
        public double MaxSpreadPips { get; set; } = 3.0;

        [JsonProperty("retry_on_fail")]
        public bool RetryOnFail { get; set; } = true;

        [JsonProperty("retry_count")]
        public int RetryCount { get; set; } = 3;

        [JsonProperty("retry_delay_ms")]
        public int RetryDelayMs { get; set; } = 1000;

        [JsonProperty("auto_start_on_launch")]
        public bool AutoStartOnLaunch { get; set; } = false;

        [JsonProperty("emergency_close_all_on_drawdown_pct")]
        public double EmergencyCloseDrawdownPct { get; set; } = 10.0;

        [JsonProperty("drawdown_protection_enabled")]
        public bool DrawdownProtectionEnabled { get; set; } = true;

        /// <summary>Max total risk across ALL open positions as % of equity. 0 = disabled.</summary>
        [JsonProperty("max_total_risk_percent")]
        public double MaxTotalRiskPercent { get; set; } = 5.0;

        /// <summary>Warn if market order fills more than this many pips from expected price. 0 = disabled.</summary>
        [JsonProperty("max_slippage_pips")]
        public double MaxSlippagePips { get; set; } = 3.0;

        /// <summary>
        /// Price must move this fraction of the TP distance before SL is moved to breakeven.
        /// 0.6 = 60% of the way to TP. Range: 0.1 to 1.0.
        /// </summary>
        [JsonProperty("sl_to_be_trigger_pct")]
        public double SlToBeTrigerPct { get; set; } = 0.6;

        /// <summary>
        /// Broker symbol suffix appended to pair names before sending to MT5 (e.g. "m" → GBPUSDm).
        /// Leave empty for brokers that use plain names (GBPUSD). The EA also resolves suffixes
        /// automatically, but setting this avoids a round-trip failure on GetSymbolInfo.
        /// </summary>
        [JsonProperty("symbol_suffix")]
        public string SymbolSuffix { get; set; } = "";

        /// <summary>
        /// When true, blocks a new trade if a correlated pair is already open.
        /// Example: blocks GBPUSD if EURUSD is already open (both are USD-quoted majors).
        /// </summary>
        [JsonProperty("correlation_check_enabled")]
        public bool CorrelationCheckEnabled { get; set; } = true;

        [JsonProperty("edge_monitor_enabled")]
        public bool EdgeMonitorEnabled { get; set; } = true;

        [JsonProperty("edge_window_trades")]
        public int EdgeWindowTrades { get; set; } = 20;

        [JsonProperty("min_win_rate_pct")]
        public double MinWinRatePct { get; set; } = 40.0;

        [JsonProperty("max_consecutive_losses")]
        public int MaxConsecutiveLosses { get; set; } = 5;

        [JsonProperty("operating_mode")]
        public BotMode OperatingMode { get; set; } = BotMode.ManualApproval;

        /// <summary>
        /// When true all validation runs normally but trades are NOT sent to MT5.
        /// Simulated fills are tracked in memory; SL/TP auto-close is detected in the heartbeat.
        /// </summary>
        [JsonProperty("paper_trading")]
        public bool PaperTrading { get; set; } = false;

        [JsonProperty("scalping")]
        public ScalpingConfig Scalping { get; set; } = new();

        [JsonProperty("scalping_by_pair")]
        public Dictionary<string, ScalpingConfig> ScalpingByPair { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class ScalpingConfig
    {
        [JsonProperty("max_trades")]
        public int MaxTrades { get; set; } = 3;

        [JsonProperty("max_minutes")]
        public int MaxMinutes { get; set; } = 15;

        [JsonProperty("max_session_loss_usd")]
        public double MaxSessionLossUsd { get; set; } = 20;

        [JsonProperty("profit_target_usd")]
        public double ProfitTargetUsd { get; set; } = 20;

        [JsonProperty("sl_pips")]
        public double StopLossPips { get; set; } = 10;

        [JsonProperty("tp_pips")]
        public double TakeProfitPips { get; set; } = 6;

        [JsonProperty("max_spread_pips")]
        public double MaxSpreadPips { get; set; } = 3;

        [JsonProperty("poll_interval_ms")]
        public int PollIntervalMs { get; set; } = 2000;

        [JsonProperty("cooldown_seconds")]
        public int CooldownSeconds { get; set; } = 20;

        [JsonProperty("direction_mode")]
        public ScalpingDirectionMode DirectionMode { get; set; } = ScalpingDirectionMode.Auto;

        [JsonProperty("allow_pyramiding")]
        public bool AllowPyramiding { get; set; } = false;

        [JsonProperty("require_snapshot_confirmation")]
        public bool RequireSnapshotConfirmation { get; set; } = true;

        [JsonProperty("min_decision_score")]
        public int MinDecisionScore { get; set; } = 4;

        [JsonProperty("use_ai_confirmation")]
        public bool UseAiConfirmation { get; set; } = false;
    }

    public sealed class PairTradingSettings
    {
        [JsonIgnore]
        public string Pair { get; set; } = "";

        [JsonProperty("pip_size")]
        public double PipSize { get; set; } = 0.0001;

        [JsonProperty("max_spread_pips")]
        public double MaxSpreadPips { get; set; } = 3;

        [JsonProperty("good_spread_pips")]
        public double GoodSpreadPips { get; set; } = 1.5;

        [JsonProperty("acceptable_spread_pips")]
        public double AcceptableSpreadPips { get; set; } = 2;

        [JsonProperty("min_sl_pips")]
        public double MinSlPips { get; set; } = 8;

        [JsonProperty("max_sl_pips")]
        public double MaxSlPips { get; set; } = 35;

        [JsonProperty("min_tp_pips")]
        public double MinTpPips { get; set; } = 8;

        [JsonProperty("scalping_min_rr")]
        public double ScalpingMinRR { get; set; } = 1.0;

        [JsonProperty("preferred_rr")]
        public double PreferredRR { get; set; } = 1.5;

        [JsonProperty("atr_multiplier_sl")]
        public double AtrMultiplierSl { get; set; } = 1.0;

        [JsonProperty("atr_multiplier_tp")]
        public double AtrMultiplierTp { get; set; } = 1.2;

        [JsonProperty("min_atr_pips_m5")]
        public double MinAtrPipsM5 { get; set; }

        [JsonProperty("max_atr_pips_m5")]
        public double MaxAtrPipsM5 { get; set; }

        [JsonProperty("min_atr_pips_m15")]
        public double MinAtrPipsM15 { get; set; }

        [JsonProperty("max_atr_pips_m15")]
        public double MaxAtrPipsM15 { get; set; }

        [JsonProperty("avoid_trade_if_spread_above_percent_of_tp")]
        public double AvoidTradeIfSpreadAbovePercentOfTp { get; set; }

        [JsonProperty("minimum_distance_from_key_level_pips")]
        public double MinimumDistanceFromKeyLevelPips { get; set; }

        [JsonProperty("break_even_after_profit_pips")]
        public double BreakEvenAfterProfitPips { get; set; }

        [JsonProperty("trailing_start_pips")]
        public double TrailingStartPips { get; set; }

        [JsonProperty("trailing_step_pips")]
        public double TrailingStepPips { get; set; }

        [JsonProperty("max_slippage_pips")]
        public double MaxSlippagePips { get; set; }

        [JsonProperty("recommended_sessions")]
        public List<string> RecommendedSessions { get; set; } = [];

        [JsonProperty("avoid_sessions")]
        public List<string> AvoidSessions { get; set; } = [];
    }

    public sealed class PairSettingsDocument
    {
        [JsonProperty("pair_settings")]
        public Dictionary<string, PairTradingSettings> PairSettings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    // ══════════════════════════════════════════════════════════════
    //  MT5 CONNECTION SETTINGS
    // ══════════════════════════════════════════════════════════════

    public sealed class MT5Settings
    {
        public string PipeName { get; set; } = "MT5TradingBotPipe";
        public string Host { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 51234;
        public int TimeoutMs { get; set; } = 5000;
        public ConnectionMode Mode { get; set; } = ConnectionMode.NamedPipe;
        public int ReconnectIntervalMs { get; set; } = 5000;
        public int MaxReconnectAttempts { get; set; } = 0; // 0 = infinite
    }

    // ══════════════════════════════════════════════════════════════
    //  IPC  (C# ↔ MQL5 messages)
    // ══════════════════════════════════════════════════════════════

    public sealed class IpcMessage
    {
        [JsonProperty("cmd")]
        public string Command { get; set; } = "";

        [JsonProperty("data")]
        public object? Data { get; set; }

        [JsonProperty("req_id")]
        public string RequestId { get; set; } = Guid.NewGuid().ToString("N")[..8];

        [JsonProperty("ts")]
        public long TimestampMs { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    public sealed class IpcResponse
    {
        [JsonProperty("req_id")]
        public string RequestId { get; set; } = "";

        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("data")]
        public object? Data { get; set; }

        [JsonProperty("error")]
        public string Error { get; set; } = "";
    }

    // ══════════════════════════════════════════════════════════════
    //  APP SETTINGS  (persisted to disk)
    // ══════════════════════════════════════════════════════════════

    public sealed class AppSettings
    {
        public MT5Settings Mt5 { get; set; } = new();
        public BotConfig Bot { get; set; } = new();
        [JsonProperty("pair_settings")]
        public Dictionary<string, PairTradingSettings> PairSettings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public ClaudeConfig Claude { get; set; } = new();
        public ApiIntegrationConfig ApiIntegrations { get; set; } = new();
        public string Theme { get; set; } = "Dark";
        public bool AutoConnectOnLaunch { get; set; } = false;
        public DateTime LastSaved { get; set; } = DateTime.UtcNow;
    }

    public sealed class ApiIntegrationConfig
    {
        [JsonProperty("ai_provider")]
        public string AiProvider { get; set; } = "Claude";

        [JsonProperty("openai_api_key")]
        public string OpenAiApiKey { get; set; } = "";

        [JsonProperty("openai_model")]
        public string OpenAiModel { get; set; } = "gpt-5.1";

        [JsonProperty("minimum_confidence_pct")]
        public int MinimumConfidencePercent { get; set; } = 70;

        [JsonProperty("news_provider")]
        public string NewsProvider { get; set; } = "Financial Modeling Prep";

        [JsonProperty("news_api_key")]
        public string NewsApiKey { get; set; } = "";

        [JsonProperty("news_currencies")]
        public List<string> NewsCurrencies { get; set; } = ["USD", "GBP", "EUR", "JPY"];

        [JsonProperty("news_impact_filter")]
        public string NewsImpactFilter { get; set; } = "High only";

        [JsonProperty("news_blackout_before_minutes")]
        public int NewsBlackoutBeforeMinutes { get; set; } = 30;

        [JsonProperty("news_blackout_after_minutes")]
        public int NewsBlackoutAfterMinutes { get; set; } = 15;

        [JsonProperty("block_trades_on_high_impact_news")]
        public bool BlockTradesOnHighImpactNews { get; set; } = true;

        [JsonProperty("block_trades_when_news_unavailable")]
        public bool BlockTradesWhenNewsUnavailable { get; set; } = false;

        [JsonProperty("telegram_bot_token")]
        public string TelegramBotToken { get; set; } = "";

        [JsonProperty("telegram_chat_id")]
        public string TelegramChatId { get; set; } = "";

        [JsonProperty("notify_signals")]
        public bool NotifySignals { get; set; } = true;

        [JsonProperty("notify_approval_needed")]
        public bool NotifyApprovalNeeded { get; set; } = true;

        [JsonProperty("notify_trade_opened")]
        public bool NotifyTradeOpened { get; set; } = true;

        [JsonProperty("notify_trade_closed")]
        public bool NotifyTradeClosed { get; set; } = true;

        [JsonProperty("notify_risk_blocked")]
        public bool NotifyRiskBlocked { get; set; } = true;
    }

    // ══════════════════════════════════════════════════════════════
    //  SYMBOL INFO  — live spread + pricing from MT5
    // ══════════════════════════════════════════════════════════════

    public sealed class SymbolInfo
    {
        public string Symbol { get; set; } = "";
        public double Ask { get; set; }
        public double Bid { get; set; }
        public double Spread { get; set; }   // in points (MT5 native unit)
        public double MinLot { get; set; }
        public double MaxLot { get; set; }
        public int Digits { get; set; }

        /// <summary>Spread converted to pips (1 pip = 10 points for 5-decimal pairs)</summary>
        public double SpreadPips => Digits == 3 || Digits == 5 ? Spread / 10.0 : Spread;
    }

    // ══════════════════════════════════════════════════════════════
    //  CLAUDE AI CONFIG
    // ══════════════════════════════════════════════════════════════

    public sealed class ClaudeConfig
    {
        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = false;

        [JsonProperty("api_key")]
        public string ApiKey { get; set; } = "";

        [JsonProperty("model")]
        public string Model { get; set; } = "claude-opus-4-7";

        [JsonProperty("poll_interval_seconds")]
        public int PollIntervalSeconds { get; set; } = 60;

        [JsonProperty("watch_symbols")]
        public List<string> WatchSymbols { get; set; } = [];

        [JsonProperty("system_prompt")]
        public string SystemPrompt { get; set; } = DefaultPrompt;

        /// <summary>
        /// How long (minutes) a cached AI regime decision is considered fresh.
        /// A new signal contradicting the cached direction within this window
        /// is blocked. 0 = context manager disabled.
        /// </summary>
        [JsonProperty("ai_context_max_age_minutes")]
        public int AiContextMaxAgeMinutes { get; set; } = 5;

        /// <summary>
        /// When true, a TRADE signal whose direction contradicts the cached
        /// regime within AiContextMaxAgeMinutes is blocked with a log warning.
        /// </summary>
        [JsonProperty("ai_context_block_conflicts")]
        public bool AiContextBlockConflicts { get; set; } = true;

        public static readonly string DefaultPrompt =
            "You are a professional FX trading analyst. Analyze the live market data and decide whether to trade.\n\n" +
            "RULES:\n" +
            "- Only trade with clear, high-probability setups\n" +
            "- Minimum R:R = 1.5:1\n" +
            "- Use key support/resistance for SL and TP\n" +
            "- Return ONLY a JSON object — no markdown, no extra text\n\n" +
            "If NO clear setup:\n" +
            "{\"action\":\"NO_TRADE\",\"reason\":\"your reason\"}\n\n" +
            "If you identify a trade:\n" +
            "{\n" +
            "  \"action\":\"TRADE\",\n" +
            "  \"pair\":\"GBPUSD\",\n" +
            "  \"trade_type\":\"BUY\",\n" +
            "  \"order_type\":\"MARKET\",\n" +
            "  \"entry_price\":0,\n" +
            "  \"stop_loss\":1.34750,\n" +
            "  \"take_profit\":1.35200,\n" +
            "  \"take_profit_2\":1.35600,\n" +
            "  \"lot_size\":0.01,\n" +
            "  \"comment\":\"Claude_AI\",\n" +
            "  \"magic_number\":999001,\n" +
            "  \"move_sl_to_be_after_tp1\":true\n" +
            "}";
    }

    // ══════════════════════════════════════════════════════════════
    //  TRADE LOG ENTRY  (for history grid)
    // ══════════════════════════════════════════════════════════════

    public sealed class TradeLogEntry
    {
        public string Id { get; set; } = "";
        public DateTime Time { get; set; }
        public string Pair { get; set; } = "";
        public string Direction { get; set; } = "";
        public double Lots { get; set; }
        public double EntryPrice { get; set; }
        public double SL { get; set; }
        public double TP { get; set; }
        public long Ticket { get; set; }
        public string Status { get; set; } = "";
        public string Error { get; set; } = "";
        public string Source { get; set; } = "Manual"; // Manual / Signal / Bot
    }
}
