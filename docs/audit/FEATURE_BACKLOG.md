# MT5TradingBotPro — Feature Backlog
**Generated:** 2026-05-01
**Source:** Audit run 2026-05-01
**Last Updated:** 2026-05-01 (post-implementation sprint)
**Total Missing Features:** 18
**Implemented:** 15 / 18  |  **Pending (Codex prompts ready):** 3 / 18
**Estimated Development Effort:** 7–9 weeks at 8hrs/day

---

## Backlog Priority Legend

| Priority | Label | Meaning |
|----------|-------|---------|
| P0 | 🔴 CRITICAL | Bot is unsafe or broken without this |
| P1 | 🟠 HIGH | Core functionality incomplete |
| P2 | 🟡 MEDIUM | Professional feature, needed before release |
| P3 | 🟢 LOW | Polish, advanced, or nice-to-have |

---

## P0 — Critical (Build These First)

### Fix BE Trigger Magic Number
**Priority:** 🔴 P0 — CRITICAL
**Layer:** Layer 4: Risk Management
**Why Critical:** `AutoBotService.cs:683` uses `tpDistance * 0.6` hardcoded — ignores `SlToBeTrigerPct` set per-trade or in config. If user sets 80% BE trigger, it fires at 60% instead — creates premature BE moves that stop out winning trades or fail to protect profits at the intended level. The field exists in the model but is silently overridden.
**Estimated Effort:** 1 hour

**Status: ✅ IMPLEMENTED (Issue 1 — via Codex)**

**Acceptance Criteria:**
- [x] `CheckSLToBreakevenAsync` reads `SlToBeTrigerPct` from `_cfg` (global) or from the open position's metadata if tracked
- [x] Default fallback is `0.6` only when no config value set
- [x] No magic `0.6` constant in the codebase

**C# Implementation Guide:**

```csharp
// In AutoBotService.cs — CheckSLToBreakevenAsync
// Replace hardcoded 0.6 with config-driven value
double beTriggerPct = _cfg.SlToBeTrigerPct > 0 ? _cfg.SlToBeTrigerPct : 0.6;
bool shouldMoveSL = currentMove >= tpDistance * beTriggerPct;
```

**Add to BotConfig.cs:**
```csharp
[JsonProperty("sl_to_be_trigger_pct")]
public double SlToBeTrigerPct { get; set; } = 0.6;
```

**Integration Points:**
- Edit: `AutoBotService.cs:683`
- Add field to: `BotConfig` (already exists in `TradeRequest`; add to `BotConfig` as global default)
- Expose in: `ReviewTradeForm` — add a NumericUpDown for this setting

**Testing Checklist:**
- [ ] Unit test: position at 50% of TP distance with trigger=0.6 → no move
- [ ] Unit test: position at 65% of TP distance with trigger=0.6 → move fires
- [ ] Unit test: position at 65% of TP distance with trigger=0.8 → no move
- [ ] Manual test: live paper trade confirms BE moves at correct level

---

### Trailing Stop Implementation ✅ DONE
**Priority:** 🔴 P0 — CRITICAL
**Layer:** Layer 4: Risk Management
**Why Critical:** `PairTradingSettings` has `TrailingStartPips` and `TrailingStepPips` fields that users may believe are active. They are not. If a user configures trailing stop expecting protection, positions will NOT trail — they will either move to BE (at 60%) or stay at BE forever, leaving profit on the table or getting stopped early. Deceptive config fields are worse than no config.
**Estimated Effort:** 6 hours

**Status: ✅ IMPLEMENTED (Issue 2 — via Codex)**

**Acceptance Criteria:**
- [x] `CheckSLToBreakevenAsync` extended to also trail stop when trailing_start_pips crossed
- [x] Trailing only moves SL in the winning direction (never back)
- [x] Trailing step respects trailing_step_pips minimum movement
- [x] Works for both BUY and SELL positions
- [x] Only activates for positions matching bot's MagicNumber

**C# Implementation Guide:**

```csharp
namespace MT5TradingBot.Services
{
    public sealed class AutoBotService : IAsyncDisposable
    {
        // Add to state tracking
        private readonly HashSet<long> _trailingActiveTickets = [];

        private async Task CheckTrailingStopAsync(
            LivePosition pos, PairTradingSettings? rules)
        {
            if (rules?.TrailingStartPips <= 0) return;

            double pipSize = rules!.PipSize > 0
                ? rules.PipSize
                : LotCalculator.GetPipSize(pos.Symbol);

            double profitPips = pos.Type == TradeType.BUY
                ? (pos.CurrentPrice - pos.OpenPrice) / pipSize
                : (pos.OpenPrice - pos.CurrentPrice) / pipSize;

            if (profitPips < rules.TrailingStartPips) return;

            // Calculate ideal trailing SL
            double idealSl = pos.Type == TradeType.BUY
                ? pos.CurrentPrice - rules.TrailingStepPips * pipSize
                : pos.CurrentPrice + rules.TrailingStepPips * pipSize;

            // Only move if better than current SL
            bool shouldMove = pos.Type == TradeType.BUY
                ? idealSl > pos.StopLoss + rules.TrailingStepPips * pipSize * 0.5
                : idealSl < pos.StopLoss - rules.TrailingStepPips * pipSize * 0.5;

            if (!shouldMove) return;

            bool ok = await _bridge.ModifyPositionAsync(
                pos.Ticket, idealSl, pos.TakeProfit).ConfigureAwait(false);

            if (ok)
                Log($"📈 Trailing SL #{pos.Ticket}: {pos.StopLoss:F5} → {idealSl:F5}");
        }
    }
}
```

**Integration Points:**
- Inject into: `AutoBotService.CheckSLToBreakevenAsync` — call `CheckTrailingStopAsync` for each position
- Called from: `HeartbeatLoopAsync` (same cycle as BE check)
- Configuration keys in `PairTradingSettings` (already exist): `trailing_start_pips`, `trailing_step_pips`

**Testing Checklist:**
- [ ] Unit test: position at 15 pips profit with trailing_start=10 → trailing activates
- [ ] Unit test: SL never moves back toward entry
- [ ] Integration test: live paper trade trails correctly over 20-pip move
- [ ] Edge case: position with no TP set — trailing still works

---

### Correlation Check (EURUSD + GBPUSD Block) ✅ DONE
**Priority:** 🔴 P0 — CRITICAL
**Layer:** Layer 4: Risk Management
**Why Critical:** Without this check, the bot will open simultaneous EURUSD and GBPUSD positions. Both pairs are ~85% correlated with USD. A single USD-moving event doubles the actual risk compared to what the position sizing algorithm calculated. This violates the user's stated risk-per-trade setting and can trigger drawdown protection sooner than expected.
**Estimated Effort:** 4 hours

**Status: ✅ IMPLEMENTED (Issue 3 — via Codex)**

**Acceptance Criteria:**
- [x] Before opening any new trade, check if a correlated pair is already open
- [x] Correlation groups are configurable in settings (not hardcoded)
- [x] Returns REJECTED_CORRELATION error code with a clear message
- [x] Logs which open position caused the block

**C# Implementation Guide:**

```csharp
namespace MT5TradingBot.Core
{
    public static class CorrelationGroups
    {
        // USD-quoted majors (highly correlated with USD)
        private static readonly List<HashSet<string>> Groups =
        [
            new(StringComparer.OrdinalIgnoreCase) { "EURUSD", "GBPUSD", "AUDUSD", "NZDUSD" },
            new(StringComparer.OrdinalIgnoreCase) { "USDJPY", "USDCHF", "USDCAD" },
            new(StringComparer.OrdinalIgnoreCase) { "XAUUSD", "XAGUSD" }
        ];

        public static bool IsCorrelated(string newPair, IEnumerable<string> openPairs)
        {
            string norm = NormalizePair(newPair);
            var group = Groups.FirstOrDefault(g => g.Contains(norm));
            if (group == null) return false;
            return openPairs.Any(p => group.Contains(NormalizePair(p)) && !NormalizePair(p).Equals(norm, StringComparison.OrdinalIgnoreCase));
        }

        private static string NormalizePair(string pair) =>
            pair.ToUpperInvariant().Replace("M", "").Replace(".", "").Replace("_", "");
    }
}
```

**Add to `AutoBotService.ExecuteTradeWithValidationAsync` after step 9 (portfolio risk):**
```csharp
// Step 9b. Correlation check
if (_cfg.CorrelationCheckEnabled)
{
    var openPairs = openPositions.Select(p => p.Symbol).ToList();
    if (CorrelationGroups.IsCorrelated(request.Pair, openPairs))
    {
        var correlated = openPairs.First(p => CorrelationGroups.IsInSameGroup(request.Pair, p));
        return Fail(request.Id, "CORRELATION_BLOCK",
            $"Correlated pair already open: {correlated}. Close it first or disable correlation check.");
    }
}
```

**Integration Points:**
- Add `CorrelationCheckEnabled` (bool, default: true) to `BotConfig`
- Call from: `AutoBotService.ExecuteTradeWithValidationAsync` and `RiskManager.ValidateAsync`
- Configuration keys:
```json
{
  "correlation_check_enabled": true
}
```

**Testing Checklist:**
- [ ] Unit test: EURUSD open, GBPUSD attempted → REJECTED
- [ ] Unit test: EURUSD open, USDJPY attempted → ALLOWED (different group)
- [ ] Unit test: EURUSD open, EURUSD attempted → blocked by existing duplicate check, not correlation
- [ ] Edge case: symbol suffix (GBPUSDm) should still match GBPUSD correlation

---

### Merge Duplicate Validation Pipeline ✅ DONE
**Priority:** 🔴 P0 — CRITICAL
**Layer:** Layer 1: Core Architecture
**Why Critical:** `AutoBotService.ExecuteTradeWithValidationAsync` and `RiskManager.ValidateAsync` contain nearly identical validation logic (pair allowlist, spread check, R:R check, margin check, portfolio risk cap). Any bug fix or rule change must be applied in both places. They will inevitably drift. Risk rules are the most safety-critical code — having two independent copies is unacceptable.
**Estimated Effort:** 5 hours

**Status: ✅ IMPLEMENTED (Issue 4 — via Codex)**

**Acceptance Criteria:**
- [x] `AutoBotService.ExecuteTradeWithValidationAsync` calls `RiskManager.ValidateAsync` for all risk checks
- [x] AutoBotService handles only: signal file parsing, news check, news blackout, retry logic, file archiving
- [x] No duplicate validation logic remains
- [x] All error codes preserved exactly

**C# Implementation Guide:**

```csharp
// AutoBotService.ExecuteTradeWithValidationAsync — replace inline validation with:
var account = await _bridge.GetAccountInfoAsync().ConfigureAwait(false);
if (account == null) return Fail(request.Id, "NO_ACCOUNT", "...");

var symbolInfo = await _bridge.GetSymbolInfoAsync(request.Pair).ConfigureAwait(false);
var openPositions = await _bridge.GetPositionsAsync().ConfigureAwait(false);

// Delegate ALL risk validation to RiskManager
var riskResult = await _riskManager.ValidateAsync(
    request, account, symbolInfo, openPositions, _cfg, _cts.Token).ConfigureAwait(false);

if (!riskResult.IsApproved)
    return Fail(request.Id, "RISK_BLOCKED", riskResult.Reason);

// Apply validated lot size
request.LotSize = riskResult.ValidatedLotSize;

// Continue with news check and execution...
```

**Integration Points:**
- Inject `IRiskManager` into `AutoBotService` constructor
- Remove: inline validation blocks (steps 1-10) from `AutoBotService.ExecuteTradeWithValidationAsync`
- `RiskManager` already exists at `Modules/RiskManagement/RiskManager.cs` — just wire it

**Testing Checklist:**
- [ ] All existing validation error codes still fire correctly
- [ ] Auto lot calculation still applied
- [ ] Spread check still fires correctly for per-pair rules
- [ ] Portfolio risk cap still works

---

## P1 — High Priority

### Max Open Positions Cap ✅ DONE
**Priority:** 🟠 P1 — HIGH
**Layer:** Layer 4: Risk Management
**Why Critical:** `MaxTradesPerDay` limits trades opened per calendar day, not how many are simultaneously open. If 5 trades are opened and 3 close at TP, 2 more open later — all within the daily limit — but at one point 5 positions are open, each with 1% risk = 5% total exposure. The portfolio risk cap `MaxTotalRiskPercent` provides some protection but a dedicated concurrent limit is cleaner.
**Estimated Effort:** 2 hours

**Status: ✅ IMPLEMENTED (Issue 5 — via Codex)**

**Acceptance Criteria:**
- [x] New config field `MaxConcurrentPositions` (default: 3)
- [x] Check is performed before opening any trade
- [x] Only counts positions matching MagicNumber (not manual trades)
- [x] Error code: `MAX_CONCURRENT_POSITIONS`

**C# Implementation Guide:**

```csharp
// Add to BotConfig:
[JsonProperty("max_concurrent_positions")]
public int MaxConcurrentPositions { get; set; } = 3;

// Add to AutoBotService.ExecuteTradeWithValidationAsync after step 9:
if (_cfg.MaxConcurrentPositions > 0)
{
    var openByBot = openPositions.Count(p => p.MagicNumber == _cfg.MagicNumber);
    if (openByBot >= _cfg.MaxConcurrentPositions)
        return Fail(request.Id, "MAX_CONCURRENT_POSITIONS",
            $"Already have {openByBot} open positions (max {_cfg.MaxConcurrentPositions})");
}
```

**Integration Points:**
- Add to: `BotConfig`, `AutoBotService.ExecuteTradeWithValidationAsync`, `RiskManager.ValidateAsync`
- Expose in: `ReviewTradeForm` — add a NumericUpDown

**Testing Checklist:**
- [ ] With MaxConcurrentPositions=2: two open → third rejected
- [ ] Manual trades (different MagicNumber) do not count toward limit

---

### Connect Claude Poller to Rich Market Snapshot ✅ DONE
**Priority:** 🟠 P1 — HIGH
**Layer:** Layer 3: Signal & AI Layer
**Why Critical:** `ClaudeSignalService` uses a 10-line system prompt and sends only account/price/positions to Claude. The full 430-line institutional prompt with candles, indicators, market structure, S/R levels, and session data exists (`AiInputPromptTemplate`) but only fires in manual review mode. The automated AI poller operates with 5% of available market context.
**Estimated Effort:** 8 hours

**Status: ✅ IMPLEMENTED (Issue 6 — via Codex)**

**Acceptance Criteria:**
- [x] `ClaudeSignalService.AnalyzeAndSignalAsync` calls `_bridge.GetMarketSnapshotAsync()` for each symbol
- [x] Market snapshot data is injected into `AiInputPromptTemplate` using `BuildFilledAiInputPrompt`
- [x] System prompt is the full institutional template (or made configurable)
- [x] If market snapshot unavailable, falls back to minimal prompt with warning log
- [x] Cache stats still logged

**C# Implementation Guide:**

```csharp
// In ClaudeSignalService.AnalyzeAndSignalAsync — replace BuildMarketDataPrompt:
foreach (var sym in _cfg.WatchSymbols)
{
    var snapshot = await _bridge.GetMarketSnapshotAsync(
        new TradeRequest { Pair = sym }, _botConfig).ConfigureAwait(false);

    string userPrompt = snapshot != null
        ? BuildFilledAiInputPrompt(snapshot.ToString())
        : BuildMinimalFallbackPrompt(account, sym, await _bridge.GetSymbolInfoAsync(sym));

    var response = await _client!.Messages.Create(new MessageCreateParams
    {
        Model = _cfg.Model,
        MaxTokens = 16000,
        Thinking = new ThinkingConfigAdaptive(),
        System = [new() { Text = AiInputPromptTemplate, CacheControl = new CacheControlEphemeral() }],
        Messages = [new() { Role = Role.User, Content = userPrompt }]
    }).ConfigureAwait(false);

    await ParseAndExecuteAsync(responseText, sym).ConfigureAwait(false);
}
```

**Integration Points:**
- `ClaudeSignalService` needs access to `BotConfig` for `GetMarketSnapshotAsync`
- Move `AiInputPromptTemplate` from `MainForm.AiPrompt.cs` to a shared location (e.g. `Services/AiPrompts.cs`)
- Move `BuildFilledAiInputPrompt` similarly

**Testing Checklist:**
- [ ] Snapshot JSON populated → rich prompt used
- [ ] Snapshot unavailable → minimal prompt with warning, no crash
- [ ] Claude response still parsed and executed correctly

---

### AIContextManager (In-Memory Regime Cache) ✅ DONE
**Priority:** 🟠 P1 — HIGH
**Layer:** Layer 1: Core Architecture
**Why Critical:** Each Claude call is stateless — no memory of previous bias, confidence history, or recent decisions. This causes thrashing: the bot can alternate BUY/SELL every polling cycle if market is ranging, and there is no temporal context. A simple regime cache prevents contradictory signals and enables staleness detection.
**Estimated Effort:** 4 hours

**Status: ✅ IMPLEMENTED (Issue 7 — via Codex)**

**Acceptance Criteria:**
- [x] `IAiContextManager` with `Update(ClaudeSignal)`, `GetCurrent(TimeSpan maxAge)`, `IsStale(TimeSpan maxAge)` methods
- [x] Persists: last direction, confidence, pair, timestamp, reason
- [x] `ClaudeSignalService` skips execution if new signal contradicts regime within stale window
- [x] Configurable stale threshold (default: 5 minutes)

**C# Implementation Guide:**

```csharp
namespace MT5TradingBot.Services
{
    public interface IAiContextManager
    {
        void Update(string pair, string direction, int confidence, string reason);
        AiRegimeState? GetCurrent(string pair, TimeSpan maxAge);
    }

    public sealed class AiRegimeState
    {
        public string Pair { get; init; } = "";
        public string Direction { get; init; } = "";
        public int Confidence { get; init; }
        public string Reason { get; init; } = "";
        public DateTime CapturedAt { get; init; } = DateTime.UtcNow;
    }

    public sealed class AiContextManager : IAiContextManager
    {
        private readonly ConcurrentDictionary<string, AiRegimeState> _cache = new(StringComparer.OrdinalIgnoreCase);

        public void Update(string pair, string direction, int confidence, string reason)
            => _cache[pair] = new AiRegimeState
            {
                Pair = pair, Direction = direction,
                Confidence = confidence, Reason = reason
            };

        public AiRegimeState? GetCurrent(string pair, TimeSpan maxAge)
        {
            if (!_cache.TryGetValue(pair, out var state)) return null;
            return DateTime.UtcNow - state.CapturedAt <= maxAge ? state : null;
        }
    }
}
```

**Integration Points:**
- Register as singleton in DI container
- Inject into: `ClaudeSignalService`
- Called from: `ParseAndExecuteAsync` — update after each Claude call; check before execution
- Configuration keys:
```json
{
  "ai_context_max_age_minutes": 5,
  "ai_context_direction_conflict_block": true
}
```

**Testing Checklist:**
- [ ] Unit test: regime=BUY, new signal=SELL within 5 min → blocked
- [ ] Unit test: regime=BUY, new signal=BUY → passes
- [ ] Unit test: regime 6 min old → stale, new signal accepted regardless

---

### Telegram Alert Service ✅ DONE
**Priority:** 🟠 P1 — HIGH
**Layer:** Layer 5: UI / Dashboard
**Why Critical:** `ApiIntegrationConfig` has `TelegramBotToken`, `TelegramChatId`, and 5 `notify_*` boolean flags. Users who configure these expect alerts. No alert will ever be sent — there is no `TelegramService` class. This is a misleading feature that creates false confidence.
**Estimated Effort:** 5 hours

**Status: ✅ IMPLEMENTED (Issue 8 — via Codex)**

**Acceptance Criteria:**
- [x] `TelegramService` sends HTTP POST to Telegram Bot API
- [x] All 5 notify flags respected: signals, approval_needed, trade_opened, trade_closed, risk_blocked
- [x] Messages are concise and formatted for mobile readability
- [x] Failure to send does NOT block trade execution
- [x] Test button in UI (`_btnTestTelegram`) actually sends a test message

**C# Implementation Guide:**

```csharp
namespace MT5TradingBot.Services
{
    public sealed class TelegramService
    {
        private static readonly HttpClient Http = new();
        private readonly ApiIntegrationConfig _cfg;

        public TelegramService(ApiIntegrationConfig cfg) => _cfg = cfg;

        public async Task SendAsync(string message, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_cfg.TelegramBotToken) ||
                string.IsNullOrWhiteSpace(_cfg.TelegramChatId))
                return;

            try
            {
                string url = $"https://api.telegram.org/bot{_cfg.TelegramBotToken}/sendMessage";
                var payload = new FormUrlEncodedContent([
                    new KeyValuePair<string, string>("chat_id", _cfg.TelegramChatId),
                    new KeyValuePair<string, string>("text", message),
                    new KeyValuePair<string, string>("parse_mode", "HTML")
                ]);
                await Http.PostAsync(url, payload, ct).ConfigureAwait(false);
            }
            catch { /* never block trading on Telegram failure */ }
        }

        public Task NotifyTradeOpenedAsync(TradeResult result, TradeRequest req) =>
            _cfg.NotifyTradeOpened
                ? SendAsync($"<b>✅ TRADE OPENED</b>\n{req.TradeType} {req.Pair} @ {result.ExecutedPrice:F5}\nSL: {req.StopLoss:F5} | TP: {req.TakeProfit:F5}\nTicket: #{result.Ticket}")
                : Task.CompletedTask;

        public Task NotifyRiskBlockedAsync(TradeRequest req, string reason) =>
            _cfg.NotifyRiskBlocked
                ? SendAsync($"<b>🚫 TRADE BLOCKED</b>\n{req.TradeType} {req.Pair}\nReason: {reason}")
                : Task.CompletedTask;
    }
}
```

**Integration Points:**
- Register in DI, inject into `AutoBotService`
- Wire to: `OnTradeExecuted` event, `Fail()` method for risk blocks
- Wire `_btnTestTelegram.Click` in MainForm to call `SendAsync("MT5 Bot test message")`

**Testing Checklist:**
- [ ] Test button sends message and shows success/failure in log
- [ ] Trade opened notification sent with correct pair/price/ticket
- [ ] Risk blocked notification sent with reason
- [ ] Missing token/chatId → silently skips, no exception

---

### Full DI Container Wiring ✅ DONE
**Priority:** 🟠 P1 — HIGH
**Layer:** Layer 1: Core Architecture
**Why Critical:** All services are manually `new`-ed inside `MainForm`. This makes unit testing impossible, creates tight coupling, and prevents the codebase from scaling. The ServiceCollection in `Program.cs` exists but only has `SettingsManager` registered.
**Estimated Effort:** 6 hours

**Status: ✅ IMPLEMENTED (Issue 9 — via Codex)**

**Acceptance Criteria:**
- [x] All services registered in `Program.cs` ServiceCollection
- [x] `MainForm` receives services via constructor injection
- [x] No `new AutoBotService(...)` in `MainForm`
- [x] Services can be mocked in unit tests

**C# Implementation Guide:**

```csharp
// Program.cs — extend DI registration
services.AddSingleton<SettingsManager>();
services.AddSingleton<MT5Bridge>(sp =>
{
    var settings = sp.GetRequiredService<SettingsManager>();
    return new MT5Bridge(settings.Current.Mt5);
});
services.AddSingleton<IRiskManager, RiskManager>();
services.AddSingleton<ITradeExecutionService, TradeExecutionService>();
services.AddSingleton<IMarketDataService, MarketDataService>();
services.AddSingleton<IPairSettingsService, PairSettingsService>();
services.AddSingleton<INewsCalendarService, FmpNewsCalendarService>();
services.AddSingleton<IAiContextManager, AiContextManager>();
services.AddSingleton<TelegramService>();
services.AddTransient<MainForm>();
```

**Testing Checklist:**
- [ ] App starts correctly with full DI wiring
- [ ] Unit tests can substitute mock IRiskManager, IMT5Bridge
- [ ] No NullReferenceException on null service injection

---

## P2 — Medium Priority

### SQLite Trade Database ✅ DONE
**Priority:** 🟡 P2 — MEDIUM
**Layer:** Layer 6: Operations & Safety
**Why Critical:** Trade history is CSV only — no query capability, no structured access, no aggregation for win rate or drawdown analysis. A proper trade database enables the edge health monitor, equity curve, and audit queries.
**Estimated Effort:** 8 hours

**Status: ✅ IMPLEMENTED (Issue 10 — via Codex)**

**Acceptance Criteria:**
- [x] All trades (signal, execution, close) stored in SQLite
- [x] Schema: trades table with full TradeRecord fields
- [x] `ITradeRepository` interface with `InsertAsync`, `UpdateCloseAsync`, `GetByDateRangeAsync`, `GetRecentClosedAsync`
- [x] Backward-compatible: existing CSV logs kept, DB adds new records going forward
- [x] DB file stored in `%APPDATA%/MT5TradingBot/trades.db`

**C# Implementation Guide:**

```csharp
namespace MT5TradingBot.Modules.LoggingDiagnostics
{
    public interface ITradeRepository
    {
        Task InsertSignalAsync(TradeRequest request, CancellationToken ct = default);
        Task InsertResultAsync(TradeResult result, CancellationToken ct = default);
        Task<IReadOnlyList<TradeLogEntry>> GetByDateAsync(DateTime date, CancellationToken ct = default);
        Task<double> GetWinRateAsync(DateTime since, CancellationToken ct = default);
        Task<double> GetDailyPnlAsync(DateTime date, CancellationToken ct = default);
    }

    public sealed class SqliteTradeRepository : ITradeRepository
    {
        private readonly string _connectionString;
        // Use Microsoft.Data.Sqlite (no Entity Framework needed)
        // ...
    }
}
```

**Integration Points:**
- Add NuGet: `Microsoft.Data.Sqlite`
- Inject into: `AutoBotService`, `TradeMonitoringService`
- Configuration keys:
```json
{
  "database_path": "%APPDATA%/MT5TradingBot/trades.db"
}
```

**Testing Checklist:**
- [ ] Trade inserted on execution success
- [ ] Trade updated on close detection
- [ ] GetByDate returns correct records
- [ ] Win rate calculation correct over known dataset

---

### Edge Health Monitor ✅ DONE
**Priority:** 🟡 P2 — MEDIUM
**Layer:** Layer 6: Operations & Safety
**Why Critical:** No automated alerting when strategy edge degrades. If win rate drops from 60% to 35% over 20 trades, the bot keeps trading. A win rate tracker with auto-pause protects capital while the user investigates.
**Estimated Effort:** 6 hours

**Status: ✅ IMPLEMENTED (Issue 11 — via Codex)**

**Acceptance Criteria:**
- [x] Track win/loss/flat on rolling 20-trade window
- [x] Auto-pause bot if win rate < configurable threshold (default: 35%)
- [x] Auto-pause bot if consecutive losses >= configurable threshold (default: 5)
- [x] Telegram alert sent when pause triggered
- [x] Win rate and streak displayed in UI

**C# Implementation Guide:**

```csharp
namespace MT5TradingBot.Services
{
    public sealed class EdgeHealthMonitor
    {
        private readonly Queue<bool> _recentResults = new(20);
        private int _consecutiveLosses;
        private readonly EdgeHealthConfig _cfg;

        public bool IsEdgeDegraded { get; private set; }

        public void RecordTrade(bool isWin)
        {
            if (_recentResults.Count >= 20) _recentResults.Dequeue();
            _recentResults.Enqueue(isWin);
            _consecutiveLosses = isWin ? 0 : _consecutiveLosses + 1;

            double winRate = _recentResults.Count > 0
                ? (double)_recentResults.Count(r => r) / _recentResults.Count * 100
                : 100;

            IsEdgeDegraded =
                (_recentResults.Count >= 10 && winRate < _cfg.MinWinRatePct) ||
                _consecutiveLosses >= _cfg.MaxConsecutiveLosses;
        }
    }
}
```

**Integration Points:**
- Inject into: `AutoBotService`
- Check `IsEdgeDegraded` before executing any trade
- Requires: `ITradeRepository` to initialize from historical data on startup
- Configuration keys:
```json
{
  "edge_monitor": {
    "min_win_rate_pct": 35,
    "max_consecutive_losses": 5,
    "rolling_window_trades": 20
  }
}
```

**Testing Checklist:**
- [ ] 8 wins + 12 losses over 20 trades → 40% win rate → NOT paused
- [ ] 5 wins + 15 losses over 20 trades → 25% win rate → PAUSED
- [ ] 5 consecutive losses → PAUSED regardless of win rate

---

### BotMode State Machine ✅ DONE
**Priority:** 🟡 P2 — MEDIUM
**Layer:** Layer 2: Trading Modes
**Why Critical:** Currently there is no formal mode representation. `ManualExecuteOnly` is hardcoded `true` in two places. Users cannot switch between Manual, Semi-Auto, and Auto without editing source code.
**Estimated Effort:** 5 hours

**Status: ✅ IMPLEMENTED (Issue 12 — directly)**

**Acceptance Criteria:**
- [x] `BotMode` enum: `Monitor=0`, `ManualApproval=1`, `FullAuto=2` (in `Models/Models.cs`)
- [x] `SetMode(BotMode)` guards transitions: rejects FullAuto when edge paused/emergency stop
- [x] UI ComboBox in `ReviewTradeForm` to select mode (addedRows=4)
- [x] Mode persisted in `BotConfig.OperatingMode` (JSON: `operating_mode`)
- [ ] Session gate for AutoScalping — not yet enforced (future polish)

**C# Implementation Guide:**

```csharp
namespace MT5TradingBot.Services
{
    public enum BotMode { Manual, SemiAuto, AutoScalping, AutoSwing }

    public sealed class ModeController
    {
        private BotMode _current = BotMode.Manual;
        private readonly AutoBotService _autoBot;
        private readonly ClaudeSignalService _claude;

        public async Task SwitchAsync(BotMode target)
        {
            if (target == _current) return;

            // Stop current mode services
            if (_current is BotMode.AutoScalping or BotMode.AutoSwing)
                await _claude.StopAsync();

            // Configure for new mode
            _autoBot.ManualExecuteOnly = target == BotMode.Manual || target == BotMode.SemiAuto;

            if (target is BotMode.AutoScalping or BotMode.AutoSwing)
            {
                _claude.UpdateConfig(BuildConfigForMode(target));
                await _claude.StartAsync();
            }

            _current = target;
        }

        private static ClaudeConfig BuildConfigForMode(BotMode mode) =>
            mode == BotMode.AutoScalping
                ? new ClaudeConfig { PollIntervalSeconds = 60, /* M5 focus */ }
                : new ClaudeConfig { PollIntervalSeconds = 300, /* H1/H4 focus */ };
    }
}
```

**Testing Checklist:**
- [ ] Switch Manual → SemiAuto: no Claude poller starts
- [ ] Switch SemiAuto → AutoScalping: Claude poller starts with scalping config
- [ ] Switch back to Manual: Claude poller stops cleanly
- [ ] Mode persists across app restart

---

### Performance Chart (Equity Curve) ✅ DONE
**Priority:** 🟡 P2 — MEDIUM
**Layer:** Layer 5: UI / Dashboard
**Why Critical:** No visual performance tracking. Users cannot assess whether the bot is profitable without exporting CSV to Excel.
**Estimated Effort:** 6 hours

**Status: ✅ IMPLEMENTED (Issue 13 — Codex prompt applied + direct)**

**Acceptance Criteria:**
- [x] Real-time equity curve in a "📈 Performance" tab (`UI/MainForm.Performance.cs`)
- [x] Custom GDI+ `EquityCurvePanel` with gradient fill, colour-coded line, dot markers
- [x] Reads from SQLite trade history via `ITradeRepository.GetRecentClosedAsync`
- [x] Period buttons: Last 20, Last 100, All Time
- [x] `PerformanceCalculator.Calculate()` in `Services/PerformanceData.cs`
- [x] Auto-refreshes when tab is selected

**Testing Checklist:**
- [x] Chart renders correctly with EquityCurvePanel (custom GDI+, no external lib)
- [x] Stats label shows win rate, net P&L, max DD, Sharpe
- [ ] Backtest tab also shows equity curve (via `UI/MainForm.Backtest.cs`)

---

### Paper Trading Mode ✅ DONE
**Priority:** 🟡 P2 — MEDIUM
**Layer:** Layer 6: Operations & Safety
**Why Critical:** No way to test strategy changes without risking real money. Currently must use a demo account in MT5 — but demo and live behavior differ (requotes, spread). A paper trading mode intercepts the execution layer and simulates fills.
**Estimated Effort:** 10 hours

**Status: ✅ IMPLEMENTED (Issue 14 — directly)**

**Acceptance Criteria:**
- [x] Paper trading intercepts `_bridge.OpenTradeAsync` → `SimulatePaperTrade` in `AutoBotService`
- [x] Simulates fills at live price; fake ticket ≥ 90,000,000; tracks `_paperPositions`
- [x] `CheckPaperPositionsAsync` in heartbeat: fetches live prices, detects SL/TP hits, calls `UpdateCloseAsync` + Telegram on close
- [x] `_paperPositions` merged into `openPositions` so risk validation sees them
- [x] UI badge shows `[PAPER]` in gold when paper trading active
- [x] `BotConfig.PaperTrading` persisted; toggled via `ReviewTradeForm` checkbox

**C# Implementation Guide:**

```csharp
namespace MT5TradingBot.Modules.TradeExecution
{
    public sealed class PaperTradingService : ITradeExecutionService
    {
        private readonly List<LivePosition> _paperPositions = [];
        private double _paperEquity;
        private int _nextTicket = 90000001;

        public Task<TradeResult> ExecuteAsync(
            TradeRequest request, RiskValidationResult risk,
            UserApprovalDecision approval, CancellationToken ct = default)
        {
            // Simulate fill at current ask/bid with small slippage
            double fillPrice = request.TradeType == TradeType.BUY
                ? request.EntryPrice * 1.00001  // 0.1 pip slippage
                : request.EntryPrice * 0.99999;

            _paperPositions.Add(new LivePosition
            {
                Ticket = _nextTicket++,
                Symbol = request.Pair,
                Type = request.TradeType,
                Lots = request.LotSize,
                OpenPrice = fillPrice,
                StopLoss = request.StopLoss,
                TakeProfit = request.TakeProfit
            });

            return Task.FromResult(new TradeResult
            {
                Status = TradeStatus.Filled,
                Ticket = _nextTicket - 1,
                ExecutedPrice = fillPrice
            });
        }
    }
}
```

**Testing Checklist:**
- [ ] Paper trade fills immediately at simulated price
- [ ] Paper positions tracked and displayed
- [ ] Paper P&L calculated correctly
- [ ] Real trades NOT sent to MT5 when paper mode active

---

## P3 — Low Priority / Polish

### Backtest Mode with Historical Data Feed ✅ DONE
**Priority:** 🟢 P3 — LOW
**Layer:** Layer 6: Operations & Safety
**Why:** `BacktestingService` exists as a skeleton but requires historical data. Full backtesting against broker-supplied OHLC data validates strategy before live deployment.
**Estimated Effort:** 16 hours

**Status: ✅ IMPLEMENTED (Issue 15 — directly)**

**Acceptance Criteria:**
- [x] Load from SQLite DB (`DbBacktestLoader`) or CSV (`CsvBacktestLoader`) with flexible column detection
- [x] `BacktestingService.RunAsync`: USD P&L via `LotCalculator.GetPipValuePerLot`, peak→trough max DD, per-trade Sharpe ×√252, profit factor, equity curve
- [x] Output displayed in "📊 Backtest" tab (`UI/MainForm.Backtest.cs`)
- [x] `EquityCurvePanel` renders colour-coded equity curve with grid, fill, dots
- [x] Stats label shows: trades, win rate, net P&L, PF, max DD, Sharpe, avg W/L

---

### Multi-Symbol Parallel Claude Polling ⏳ CODEX PROMPT READY
**Priority:** 🟢 P3 — LOW
**Layer:** Layer 6: Operations & Safety
**Why:** `ClaudeSignalService` iterates `WatchSymbols` sequentially. With 4 symbols at 60s intervals, the 4th symbol gets analysis that is 3×60s = 3 minutes stale by the time it executes.
**Estimated Effort:** 3 hours

**Status: ⏳ PENDING — Codex prompt generated**

**Acceptance Criteria:**
- [ ] Symbols analyzed in parallel with `Task.WhenAll`
- [ ] Each symbol gets its own Claude call context
- [ ] Error in one symbol does not block others (per-task try/catch)
- [ ] Cancellation token checked per task

**Target:** `Services/ClaudeSignalService.cs` lines 165–169 — replace sequential `foreach` with `Task.WhenAll`

---

### Hot Config Reload (No Restart) ⏳ CODEX PROMPT READY
**Priority:** 🟢 P3 — LOW
**Layer:** Layer 6: Operations & Safety
**Why:** Risk settings changed in the UI require clicking Save which calls `UpdateConfig()`. A file watcher on `settings.json` would allow config changes from external tools.
**Estimated Effort:** 2 hours

**Status: ⏳ PENDING — Codex prompt generated**

**Target:** Add `FileSystemWatcher` to `Services/SettingsManager.cs` with 300ms debounce; subscribe in `MainForm.cs` to push to `_bot.UpdateConfig()` + `_bot.SetMode()` + `_claude.UpdateConfig()`

---

### Slippage Guard — Take Action ⏳ CODEX PROMPT READY
**Priority:** 🟢 P3 — LOW
**Layer:** Layer 4: Risk Management
**Why:** Current slippage guard (`AutoBotService.cs:817–833`) only logs a warning. Extreme slippage should trigger a Telegram alert and optionally close the position if slippage exceeds 2× configured maximum.
**Estimated Effort:** 2 hours

**Status: ⏳ PENDING — Codex prompt generated**

**Target:** `Services/AutoBotService.cs` lines ~817–833 — add two tiers:
- `slippagePips > maxSlippagePips`: Telegram moderate alert, keep position
- `slippagePips > maxSlippagePips * 2`: Telegram extreme alert + `_bridge.CloseTradeAsync(result.Ticket)`

---

## Development Roadmap

Based on the backlog above, recommended build sequence:

### Sprint 1 (Week 1–2): Foundation Safety
Goal: Make the bot safe for paper trading with correct risk behavior
- [ ] Fix BE trigger hardcoded 0.6 (1 hour)
- [ ] Implement trailing stop (6 hours)
- [ ] Implement correlation check (4 hours)
- [ ] Merge duplicate validation pipeline (5 hours)
- [ ] Add max concurrent positions cap (2 hours)

### Sprint 2 (Week 3–4): AI Layer Upgrade
Goal: Connect automated AI poller to full market context
- [ ] Connect ClaudeSignalService to rich market snapshot (8 hours)
- [ ] Implement AIContextManager (4 hours)
- [ ] Move AiInputPromptTemplate to shared Services layer (2 hours)

### Sprint 3 (Week 5–6): Operations & Alerting
Goal: Production-grade observability
- [ ] SQLite trade database + ITradeRepository (8 hours)
- [ ] Telegram alert service (5 hours)
- [ ] Edge health monitor (6 hours)

### Sprint 4 (Week 7–8): UI & Mode Control
Goal: Proper trading mode management and dashboard
- [ ] BotMode state machine (5 hours)
- [ ] Full DI container wiring (6 hours)
- [ ] Performance chart / equity curve (6 hours)

### Sprint 5 (Week 9–10): Paper Trading & Live Sign-Off
Goal: Paper test strategy, verify all safety systems
- [ ] Paper trading mode (10 hours)
- [ ] Multi-symbol parallel polling (3 hours)
- [ ] Slippage guard — take action (2 hours)
- [ ] Run 2-week paper test; resolve edge health monitor alerts
- [ ] Re-run audit; target score ≥ 80

---

## Reusable Services To Build First

These are shared utilities that multiple features depend on. Build these BEFORE the features that use them.

| Service | Used By | Interface |
|---------|---------|-----------|
| `ITradeRepository` (SQLite) | EdgeHealthMonitor, EquityCurve, TradeAuditLogger | `ITradeRepository` |
| `AiContextManager` | ClaudeSignalService, ModeController | `IAiContextManager` |
| `TelegramService` | AutoBotService, EdgeHealthMonitor, DrawdownProtection | `ITelegramService` |
| `CorrelationGroups` (static) | AutoBotService, RiskManager | static utility |
| `ModeController` | MainForm, ClaudeSignalService, AutoBotService | `IModeController` |
| `EdgeHealthMonitor` | AutoBotService | `IEdgeHealthMonitor` |
| `PaperTradingService` | TradeExecutionService (swap out) | `ITradeExecutionService` |

---

## Definition of Done (for each feature)

A feature is complete when:
- [ ] Code written and compiles without warnings
- [ ] Unit tests written and passing
- [ ] Integrated and tested in paper trading mode
- [ ] Configuration documented in AppSettings / BotConfig
- [ ] No magic numbers (all values from config)
- [ ] Logging added (entry, exit, errors)
- [ ] Reviewed in next audit run (score improved)
