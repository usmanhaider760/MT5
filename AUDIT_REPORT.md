# MT5 Gold Trading Bot — Full Codebase Audit Report
**Date:** 2026-05-06
**Status:** Pre-refactor. Read-only analysis. No code was changed.
**Purpose:** Reference document for the Gold-only refactor project.

---

## A. EXECUTIVE SUMMARY

- **148 C# source files**, ~47,000 lines of code
- **1 MQL5 EA file**, ~1,400 lines
- **settings.json is 4,038 lines** — it should be ~340 lines (corrupted)
- The CORE trading loop (signal → validate → execute) is architecturally sound
- The surrounding infrastructure (backtesting, strategy proof, live readiness, pair scanner) is not needed and actively obscures what the bot is doing

**Verdict: Do not restart from scratch.** The core loop, the named pipe protocol, the MT5Bridge, and the RiskManager are solid. What is needed is aggressive removal and simplification, not a rewrite.

---

## B. CURRENT BOT FLOW

### Signal Sources (3 paths)
1. JSON file dropped into `C:\MT5Bot\signals\` (FileSystemWatcher)
2. ClaudeSignalService — polls MT5, asks Claude API (currently DISABLED: `Claude.enabled: false`)
3. Manual button in the UI

### Validation Chain (AutoBotService.ExecuteTradeWithValidationCoreAsync — ~600 lines, 20+ sequential gates)

```
1.  Edge health paused?                 → BLOCK
2.  Kill switch active?                 → BLOCK
3.  Live Readiness Gate audit           → BLOCK if not ready for live
4.  Rollout stage check
5.  No-trade-window check
6.  Pair allowlist check                (allowed_pairs: ["XAUUSD"])
7.  Symbol suffix appended             (only if symbol_suffix is non-empty)
8.  EffectiveTradeSettings.Resolve     (reads GLOBAL scalping/normal config — per-pair IGNORED)
9.  Daily trade count check
10. Account info fetched from MT5
11. Symbol info fetched (live bid/ask/spread)
12. Session spread check
13. *** ApplyTradePageSlTp CALLED ***   (SL and TP from signal silently OVERWRITTEN)
14. Broker stop-level check
15. Broker freeze-level check
16. Open positions fetched
17. Daily loss hard stop
18. Weekly loss hard stop
19. Symbol exposure limit check         (FIRST call)
20. Max concurrent positions cap
21. RiskManager.ValidateAsync           (R:R, risk %, spread)
22. Lot size broker check
23. Commission model check
24. Slippage model check
25. Symbol exposure limit check         (SECOND call — DUPLICATE)
26. Margin projection check
27. Correlation check
28. News filter check (Financial Modeling Prep API)
29. ADX ranging snapshot from MT5       (blocks if M5 ADX < 20 — hard block)
30. Trade rules execution audit
```

### Execution
- `TradeExecutionService.ExecuteAsync` → `MT5Bridge.TryCheckOrderAsync` (OrderCheck) → `MT5Bridge.OpenTradeAsync` → named pipe → `TradingBotEA.mq5` → `Trade.Buy()` / `Trade.Sell()`

### Post-trade Heartbeat (every 2500ms)
- SL → Breakeven check
- H1 trend reversal check
- Trailing stop check
- Drawdown protection check
- Paper position SL/TP simulation
- Closed position detection

---

## C. BIGGEST PROBLEMS FOUND

### PROBLEM 1 — settings.json is CORRUPTED (active bug)
`news_currencies` should have 4 entries: `["USD", "GBP", "EUR", "JPY"]`
It currently has those 4 entries repeated hundreds of times (file = 4,038 lines).
Every time settings are saved, the array grows by ~400 more entries.
**The bug is in the settings save logic — it appends instead of replacing.**
The file will eventually fail to parse.

### PROBLEM 2 — Per-pair settings are completely IGNORED
Settings.json has three layers of SL/TP config:
- Global: `Bot.scalping.sl_pips`
- Per-pair: `Bot.scalping_by_pair.XAUUSD.sl_pips`
- Per-pair (broker): `Bot.scalping_by_pair.XAUUSDM.sl_pips`

`EffectiveTradeSettings.Resolve` only reads from the **global** `cfg.Scalping` and `cfg.NormalTrading` objects. The `scalping_by_pair` and `normal_trading_by_pair` dictionaries serialize to JSON and look meaningful — but the validation pipeline **never reads them**.

### PROBLEM 3 — SL and TP from EVERY signal are silently overwritten
`ApplyTradePageSlTp` (AutoBotService.cs line ~3063) always fires when `effective.SlPips > 0` and `effective.TpPips > 0`. Both are currently > 0.

**Whatever SL/TP are in the signal file, or whatever Claude calculated, or whatever you typed — they are replaced with:**
```
SL = livePrice ± (effective.SlPips × pipSize)
TP = livePrice ± (effective.TpPips × pipSize)
```
No log message is written. No audit captures the original values. Signal SL/TP are silently discarded.

For Gold with current config (scalping sl_pips=500, pipSize=0.01):
- At gold price 2345: SL = 2345 ± $5.00,  TP = 2345 ± $5.50
- These are intraday swing distances — NOT scalping distances.

### PROBLEM 4 — Symbol suffix is empty but broker uses XAUUSDm
`settings.json` line 64: `"symbol_suffix": ""`
Broker symbol: `XAUUSDm`
`allowed_pairs`: `["XAUUSD"]`

Because the suffix is empty, no suffix is ever appended. The bot sends `GET_SYMBOL_INFO` for `"XAUUSD"`, not `"XAUUSDm"`. If your broker does not have a plain `XAUUSD` symbol, every trade fails at the SymbolInfo step with `NO_SYMBOL_DATA`. If it does, trades go to the wrong symbol.

### PROBLEM 5 — Scalping is globally disabled
`settings.json` line 177: `"enabled": false` under the `scalping` block.

`EffectiveTradeSettings.Resolve` logic:
```csharp
bool scalping = string.Equals(strategy, "Scalping", ...) 
    || (!string.Equals(strategy, "Normal", ...) && cfg.Scalping.Enabled);
```
If a signal does not explicitly declare `strategy: "Scalping"` in the JSON, and `cfg.Scalping.Enabled = false`, the code treats it as a Normal trade. Scalping session service and scalping trade manager are effectively inactive.

### PROBLEM 6 — Symbol exposure limit check is called twice
`CheckSymbolExposureLimit` is called at two separate points in the validation chain. One call is redundant. It confuses code reading.

### PROBLEM 7 — ADX ranging is a hard-block on every trade
```csharp
if (!adxOk)
    return Fail(request.Id, "ADX_RANGING", $"ADX {snapshotAdx:F1} — market is ranging. No trade.");
```
If M5 ADX < 20, no trade is allowed regardless of any other factor. Gold ranges for extended periods. If you don't know this rule is there, you will be confused why no trades ever execute.

### PROBLEM 8 — Correlation check can block Gold trades invisibly
`correlation_check_enabled: true`. If any correlated position is open, the new trade is blocked. Correlation groups for XAU may or may not be configured correctly.

### PROBLEM 9 — "Scalping" SL of 500 pips is not scalping
`scalping.sl_pips = 500` with `pipSize = 0.01` = $5.00 SL distance on Gold. Real scalping SL on Gold = 10–30 pips ($0.10–$0.30 distance). Current config is mislabeled intraday swing trading.

### PROBLEM 10 — Paper positions are lost on restart
`_paperPositions` is in-memory only. App crash or restart = all simulated positions lost with no closure record. Paper trading statistics are unreliable across sessions.

---

## D. FILES CAUSING COMPLEXITY (Safe to Archive)

These subsystems exist and compile but are **not part of the core trade flow**. They are safe to move to an archive branch.

| Folder | Files | Purpose | Needed for live Gold trading? |
|---|---|---|---|
| `Trading/Backtesting/` | 15 files | Full backtesting engine | No |
| `Trading/StrategyProof/` | 11 files | Edge analysis, robustness testing | No |
| `Application/LiveReadiness/` | 9 files | Forward test gate, readiness checklists | Partially |
| `Application/Deployment/` | 3 files | EA deployment, staged rollout | No (now) |
| `Trading/PairScanner/` | 2 files | Multi-pair ranking | No |
| `Trading/MarketData/` | 7 files | Historical OHLC/tick download | No |
| `Infrastructure/AI/` | 5 files | Claude AI signal loop | No (disabled) |
| `UI/Forms/MainForm.Backtest.cs` | 1 file | Backtest tab | No |
| `UI/Forms/MainForm.Performance.cs` | 1 file | Performance tab | No |
| `UI/Forms/MainForm.AiPrompt.cs` | 1 file | AI prompt tab | No |
| `UI/Panels/EquityCurvePanel.cs` | 1 file | Equity curve chart | No |

**Files you MUST understand for day-to-day operation:**
- `Application/Workflows/AutoBotService.cs` — the entire brain
- `Trading/RiskManagement/RiskManager.cs` — risk validation
- `Infrastructure/MT5/MT5Bridge.cs` — broker communication
- `Trading/TradeExecution/TradeExecutionService.cs` — final execution
- `Domain/Models/Models.cs` — all data structures
- `MT5_EA/TradingBotEA.mq5` — what runs in MetaTrader 5
- `Data/Config/settings.json` — configuration (currently corrupted)

---

## E. SETTINGS CONFLICTS

### Spread — 5 places, 2 different values

| Location | Value | Actually Used? |
|---|---|---|
| `Bot.max_spread_pips` | 3.0 | Only in EA market snapshot (not in trade validation) |
| `Bot.default_max_spread_pips` | 0.0 | Only if EnableSessionSpreadProtection=true (it's false) |
| `Bot.scalping.max_spread_pips` | 31.0 | YES — used in validation when strategy=Scalping |
| `Bot.scalping_by_pair.XAUUSD.max_spread_pips` | 31.0 | NO — ignored |
| `Bot.normal_trading.max_spread_pips` | 31.0 | YES — used in validation when strategy=Normal |

### SL/TP — 3 layers, only 1 layer is read

| Location | SL pips | TP pips | Used? |
|---|---|---|---|
| `Bot.scalping` (global) | 500 | 550 | YES |
| `Bot.scalping_by_pair.XAUUSD` | 500 | 550 | NO |
| `Bot.scalping_by_pair.XAUUSDM` | 100 | 180 | NO |
| `Bot.normal_trading` (global) | 500 | 1000 | YES |
| `Bot.normal_trading_by_pair.XAUUSD` | 500 | 1000 | NO |

### Symbol Identity Conflict

| Setting | Value | Problem |
|---|---|---|
| `Bot.allowed_pairs` | `["XAUUSD"]` | Display name — correct |
| `Bot.symbol_suffix` | `""` (empty) | Should be `"m"` |
| `pair_settings` key | `"XAUUSD"` | Matches allowed_pairs |
| Broker actual symbol | `XAUUSDm` | Never matched because suffix is empty |

---

## F. SPREAD CALCULATION — HOW IT ACTUALLY WORKS

### EA side (TradingBotEA.mq5 line 981):
```mql5
double spread = (ask - bid) / SymbolInfoDouble(sym, SYMBOL_POINT);
```
Returns spread in **points**. For XAUUSD: SYMBOL_POINT = 0.01.

### C# side (Models.cs SymbolInfo.SpreadPips):
```csharp
public double SpreadPips => Digits == 3 || Digits == 5 ? Spread / 10.0 : Spread;
```
For XAUUSD: Digits = 2, so SpreadPips = Spread (no division).

### Is the math correct for XAUUSD?
- For XAUUSD: point = 0.01, pip = 0.01 (same)
- So points = pips for XAUUSD specifically
- The spread formula is **correct for XAUUSD by coincidence** (point equals pip on this instrument)
- The formula would be wrong for a hypothetical instrument where Digits=2 but pip ≠ point

### EA market snapshot (TradingBotEA.mq5 line 1300):
```mql5
double spreadPips = pip > 0 ? (ask - bid) / pip : 0.0;
```
Where `pip` is computed correctly from SYMBOL_POINT and digits. This is the correct general formula.

**Summary: Spread numbers are correct for XAUUSD. The formula is fragile for other instruments.**

---

## G. ApplyTradePageSlTp — THE HIDDEN OVERRIDE

This function (AutoBotService.cs line ~3063) silently replaces SL/TP on every trade:

```csharp
private static void ApplyTradePageSlTp(
    TradeRequest request, SymbolInfo? symbolInfo, 
    double livePrice, EffectiveTradeSettings effective)
{
    if (symbolInfo == null || !IsFinitePositive(livePrice)) return;
    if (!IsFinitePositive(effective.SlPips) || !IsFinitePositive(effective.TpPips)) return;

    double pipSize = LotCalculator.GetPipSize(request.Pair.ToUpperInvariant());
    if (request.TradeType == TradeType.BUY)
    {
        request.StopLoss   = livePrice - effective.SlPips * pipSize;
        request.TakeProfit = livePrice + effective.TpPips * pipSize;
    }
    else
    {
        request.StopLoss   = livePrice + effective.SlPips * pipSize;
        request.TakeProfit = livePrice - effective.TpPips * pipSize;
    }
    request.Strategy = effective.Strategy;
}
```

**This fires on every trade** because effective.SlPips and TpPips are always > 0 in the current config.

The ONLY way to prevent it from overwriting SL/TP is to set `sl_pips: 0` and `tp_pips: 0` in both `scalping` and `normal_trading` config sections. But then the R:R check in RiskManager will also break.

**This must be refactored in Phase 2.** The override should log what it replaced and why.

---

## H. GOLD-ONLY CLEAN CONFIG (Target Design)

```json
{
  "GoldTrading": {
    "Enabled": true,
    "Mode": "PaperTrading",
    "DisplayPair": "XAUUSD",
    "BrokerSymbol": "XAUUSDm",
    "PipSize": 0.01,
    "LotSize": 0.01,
    "MaxOpenTrades": 1,
    "MaxSpreadPips": 30,
    "MaxSlippagePips": 3
  },
  "GoldTrading.Scalping": {
    "Enabled": false,
    "SL_Pips": 20,
    "TP_Pips": 35,
    "MinRR": 1.5,
    "MaxDurationMinutes": 60,
    "CooldownSeconds": 60
  },
  "GoldTrading.Normal": {
    "Enabled": true,
    "SL_Pips": 80,
    "TP_Pips": 160,
    "MinRR": 2.0,
    "MaxDurationMinutes": 240
  },
  "GoldTrading.Protection": {
    "NewsFilterEnabled": true,
    "NewsFilterCurrency": "USD",
    "MoveSLToBreakeven": true,
    "BreakevenAfterPercentOfTP": 0.6,
    "TrailingStopEnabled": false,
    "MaxDailyLossUsd": 50.0
  }
}
```

---

## I. REQUIRED AUDIT MODELS (Target Design)

### GoldTradeDecisionAudit
```csharp
public sealed class GoldTradeDecisionAudit
{
    public string DisplayPair     { get; init; } = "XAUUSD";
    public string BrokerSymbol    { get; init; } = "XAUUSDm";
    public string Strategy        { get; init; } = "";      // "Scalping" | "Normal"
    public string Direction       { get; init; } = "";      // "BUY" | "SELL"
    public double Bid             { get; init; }
    public double Ask             { get; init; }
    public double EntryPrice      { get; init; }
    public double PipSize         { get; init; } = 0.01;
    public double SpreadPrice     { get; init; }            // Ask - Bid
    public double SpreadPips      { get; init; }            // SpreadPrice / PipSize
    public double MaxSpreadPips   { get; init; }
    public double SlPips          { get; init; }
    public double TpPips          { get; init; }
    public double RiskReward      { get; init; }
    public string Decision        { get; init; } = "";      // "ALLOW" | "BLOCK" | "NO_TRADE"
    public string BlockingRule    { get; init; } = "";
    public string Reason          { get; init; } = "";
    public DateTime CreatedAt     { get; init; } = DateTime.UtcNow;
}
```

### GoldTradeCloseAudit
```csharp
public sealed class GoldTradeCloseAudit
{
    public long     Ticket              { get; init; }
    public string   DisplayPair         { get; init; } = "XAUUSD";
    public string   BrokerSymbol        { get; init; } = "XAUUSDm";
    public string   Strategy            { get; init; } = "";
    public string   OpenedBy            { get; init; } = "";   // "Signal" | "Claude" | "Manual"
    public string   ClosedBy            { get; init; } = "";   // "SL" | "TP" | "Manual" | "Drawdown"
    public string   CloseReason         { get; init; } = "";
    public double   EntryPrice          { get; init; }
    public double   ClosePrice          { get; init; }
    public double   ProfitUsd           { get; init; }
    public double   ProfitPips          { get; init; }
    public double   SpreadPipsAtOpen    { get; init; }
    public double   DurationMinutes     { get; init; }
    public DateTime ClosedAt            { get; init; } = DateTime.UtcNow;
}
```

---

## J. REQUIRED SERVICES (Target Design)

```csharp
// Reads flat GoldSettings — no per-pair nesting, no ambiguity
public interface IGoldSettingsResolver
{
    GoldSettings GetCurrent();
    void Update(GoldSettings settings);
}

// Resolves display pair vs broker symbol, pip size
public interface IGoldSymbolService
{
    string DisplayPair { get; }       // "XAUUSD"
    string BrokerSymbol { get; }      // "XAUUSDm"
    double PipSize { get; }           // 0.01
}

// Single responsibility: (ask - bid) / pipSize — always returns pips
public interface IGoldSpreadCalculator
{
    double Calculate(double ask, double bid);
    bool IsWithinLimit(double spreadPips, double maxSpreadPips);
}

// Decides ALLOW / BLOCK / NO_TRADE with full audit trail
public interface IGoldDecisionService
{
    Task<GoldTradeDecisionAudit> EvaluateAsync(
        TradeRequest request, 
        CancellationToken ct = default);
}

// R:R, risk %, SL/TP distance, lot size
public interface IGoldRiskValidator
{
    RiskValidationResult Validate(
        TradeRequest request, 
        AccountInfo account, 
        GoldSettings settings);
}

// Sends to MT5, verifies ticket > 0, logs execution
public interface IGoldExecutionService
{
    Task<TradeResult> ExecuteAsync(
        TradeRequest request,
        GoldTradeDecisionAudit audit,
        CancellationToken ct = default);
}

// SL→BE, trailing stop, max duration close, drawdown close
public interface IGoldTradeManagementService
{
    Task TickAsync(IReadOnlyList<LivePosition> positions, CancellationToken ct = default);
}

// Writes GoldTradeDecisionAudit and GoldTradeCloseAudit to SQLite
public interface IGoldAuditService
{
    Task RecordDecisionAsync(GoldTradeDecisionAudit audit);
    Task RecordCloseAsync(GoldTradeCloseAudit audit);
    Task<IReadOnlyList<GoldTradeDecisionAudit>> GetRecentDecisionsAsync(int count = 20);
}
```

---

## K. STEP-BY-STEP REFACTOR PLAN

### Phase 1 — Config Fixes Only (No code changes, ~1–2 hours)

**Step 1 — Fix settings.json corruption**
- Open `Data/Config/settings.json`
- Find `news_currencies` (line 307 onwards)
- Replace the entire bloated array with: `["USD"]`
- Also find and fix the bug in the settings save code that appends instead of replacing
- File should drop from 4,038 lines to ~340 lines

**Step 2 — Fix symbol configuration**
- Set `symbol_suffix: "m"`
- Keep `allowed_pairs: ["XAUUSD"]` (suffix is appended at validation time)

**Step 3 — Fix SL/TP to realistic Gold values**
- `scalping.sl_pips: 20`
- `scalping.tp_pips: 35`
- `normal_trading.sl_pips: 80`
- `normal_trading.tp_pips: 160`
- Enable scalping if desired: `scalping.enabled: true`

**Step 4 — Disable irrelevant checks**
- `correlation_check_enabled: false`
- `market_data_symbols: []`
- `news_currencies: ["USD"]` (Gold is USD-denominated)

**Step 5 — Test paper trading end-to-end**
- Set mode to PaperTrading
- Drop a signal JSON in `C:\MT5Bot\signals\`
- Read every log line until you can explain each gate result
- Do NOT proceed to Phase 2 until you can do this

---

### Phase 2 — Code Simplification (1–2 weeks, one safe commit at a time)

**Step 6 — Add SL/TP override logging**
- In `ApplyTradePageSlTp`, add a log line before and after overwriting:
  `[BOT] SL/TP override: original=({origSL},{origTP}) → computed=({newSL},{newTP}) using {strategy} SL={slPips}pips TP={tpPips}pips`
- This single change makes the most confusing behavior visible

**Step 7 — Create GoldTrading/ folder**
- Create `GoldTrading/GoldSettings.cs` — flat config, no nested dictionaries
- Create `GoldTrading/GoldSpreadCalculator.cs` — one method: `(ask-bid)/pipSize`
- Create `GoldTrading/GoldDecisionAudit.cs` — the audit model above
- Wire to a test button in UI that runs a decision audit without placing a trade

**Step 8 — Migrate validation to GoldDecisionService**
- Replace `ApplyTradePageSlTp` with `GoldDecisionService.ComputeSlTp` (same logic + logging)
- Replace `EffectiveTradeSettings.Resolve` with `GoldSettings.Resolve` (simpler, no per-pair lookup)
- Each replacement = one commit + one manual paper test

**Step 9 — Simplify the UI**
- Add `GoldDashboardPanel.cs` as the first panel visible on launch
- Content: bot status, mode, connection, bid/ask/spread, last decision with blocking rule
- Do not remove existing panels yet — add alongside them

**Step 10 — Archive unused subsystems**
- Create a Git branch `archive/unused-subsystems`
- Move to archive or delete:
  - `Trading/Backtesting/`
  - `Trading/StrategyProof/`
  - `Trading/MarketData/` (confirm not needed first)
  - `Trading/PairScanner/`
  - `UI/Forms/MainForm.Backtest.cs`
  - `UI/Forms/MainForm.Performance.cs`
  - `UI/Forms/MainForm.AiPrompt.cs`
- Confirm the project still compiles

---

## L. SAFETY RULES FOR REFACTOR

These must be true before any live trading is re-enabled:

- Live trading disabled by default (`user_live_trading_enabled: false`)
- Default mode is PaperTrading
- No auto-start live trading on launch (`auto_start_on_launch: false`)
- No martingale, no grid, no averaging down
- No AI-based execution until deterministic rules are clean and tested
- No trade can open without a `GoldTradeDecisionAudit` record
- No trade can close without a `GoldTradeCloseAudit` record
- No hidden close logic — every closure must have an explicit reason logged

---

## M. WHAT IS WORKING CORRECTLY (DO NOT BREAK)

- Named pipe protocol (C# ↔ MT5 EA) — solid binary framing
- MT5Bridge API surface — clean, well-typed, handles reconnect
- RiskManager — correct R:R, risk %, spread validation logic
- TradeExecutionService — performs OrderCheck before placing trades
- Signal file dedup (processed_ids.txt), retry, and archive logic
- Kill switch + drawdown protection emergency close
- SL→Breakeven heartbeat logic

---

## N. FIRST SAFE COMMIT

```
Fix settings.json: remove corrupted news_currencies duplication,
set broker symbol suffix to "m", fix Gold SL/TP to realistic values,
disable correlation check and EURUSD market data sync.

Changes:
- news_currencies: ["USD"]  (was 2000+ duplicate entries)
- symbol_suffix: "m"  (was empty — broker uses XAUUSDm)
- scalping.sl_pips: 20  (was 500 — not scalping)
- scalping.tp_pips: 35  (was 550)
- normal_trading.sl_pips: 80  (was 500)
- normal_trading.tp_pips: 160  (was 1000)
- correlation_check_enabled: false  (was true — single-pair bot)
- market_data_symbols: []  (was ["EURUSD"] — irrelevant)
```

---

*Audit performed: 2026-05-06. No code was changed. All findings traceable to specific files and line numbers.*
