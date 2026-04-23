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

        [JsonProperty("allowed_pairs")]
        public List<string> AllowedPairs { get; set; } = ["GBPUSD", "EURUSD", "USDJPY"];

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
        public double EmergencyCloseDrawdownPct { get; set; } = 10.0; // close all if equity drops 10%

        [JsonProperty("drawdown_protection_enabled")]
        public bool DrawdownProtectionEnabled { get; set; } = true;
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
        public string Theme { get; set; } = "Dark";
        public bool AutoConnectOnLaunch { get; set; } = false;
        public DateTime LastSaved { get; set; } = DateTime.UtcNow;
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
