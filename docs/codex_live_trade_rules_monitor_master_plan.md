# Codex Master Development Plan — Live Trade Rules Monitor & Control

## Project
Repository: `usmanhaider760/MT5`

Project type: .NET 8 Windows Forms MT5 trading bot.

Feature name: **Live Trade Rules Monitor & Control**

## How Codex Must Work

Codex must read this document once and then develop the feature step by step automatically.

Codex must follow this process:

1. Read this full document.
2. Inspect the repository structure.
3. Identify existing classes/files before changing anything.
4. Create a development checklist file in the repo.
5. Implement one phase at a time.
6. After each phase:
   - Mark the phase as DONE in the checklist.
   - Run build/compile.
   - Fix compile errors.
   - Run available tests where practical.
   - Commit or clearly list changed files.
   - Move to the next phase automatically.
7. Do not stop after every phase asking the user what to do next.
8. Stop only if:
   - A critical architecture ambiguity blocks safe progress.
   - Build cannot be fixed after reasonable attempts.
   - Required source file is missing or current repo design is very different from expected.

## Mandatory Safety Rules for Codex

- Do not remove existing trading safety checks.
- Do not bypass existing execution gate globally.
- Do not break existing Log Detail double-click behavior.
- Do not expose API keys, tokens, passwords, or provider secrets in logs/export.
- Do not print API keys in UI.
- Do not auto-save user changes.
- Do not automatically modify open MT5 position SL/TP from this feature.
- Runtime rule edits affect future checks/next decisions unless an already-supported safe position modification flow exists.
- Existing trade execution must still flow through `AutoBotService.ExecuteTradeWithValidationAsync` or equivalent central gate.

## Feature Objective

Build a contextual **Live Trade Rules Monitor & Control** window.

The window is used to:

- Monitor all live values impacting a trade/session decision.
- Show every rule checked for scalping/normal trades.
- Show PASS / WARNING / BLOCK / DISABLED / NOT_CHECKED status.
- Allow enabling/disabling rules.
- Allow editing impacting values where possible.
- Show standard/configured/live values.
- Show min/max and green-to-red visual feedback.
- Show what disabled rules would have done without actually blocking.
- Export diagnostics to JSON/TXT.
- Search/filter rules.
- Show rule codes in UI/logs/audit/export.

## Important Existing Behavior to Preserve

Existing log row double-click opens the Log Detail window.

This must remain:

```text
Log row double-click = Log Details
Right-click / Rules icon = Live Trade Rules Monitor & Control
```

Rules Monitor should open for trade-decision-related rows only:

- `[SCALP]`
- `[TRADE_AUDIT_FULL]`
- `[EXEC_AUDIT]`
- `[BOT] Trade`
- `[BOT] Rejected`
- `[AI] Signal`
- Signal card / feed row
- Running position row

Do not show/open Rules Monitor for generic logs like connection status, clock, folder opened, settings saved, etc.

## Final Window Name

**Live Trade Rules Monitor & Control**

Suggested form class:

```csharp
LiveTradeRulesMonitorControlForm
```

Suggested file path:

```text
UI/Forms/LiveTradeRulesMonitorControlForm.cs
```

## Contextual Opening Rules

Do not add a generic main-form button.

Open contextually from:

### 1. Scalping Panel

Add button:

```text
⚙ Scalping Rules
```

Behavior:

- Strategy = Scalping
- Pair = currently selected pair
- IsRunningTrade/session = based on active scalping session
- No strategy dropdown in monitor

### 2. Normal Trading Panel

Add button:

```text
⚙ Normal Rules
```

Behavior:

- Strategy = Normal
- Pair = currently selected pair
- IsRunningTrade/session = based on active normal trade/session
- No strategy dropdown in monitor

### 3. Running Trade / Position Row

Open from right-click menu or Rules icon.

Behavior:

- Use selected running position ticket
- Pair = position symbol
- Trade type = BUY/SELL
- Strategy resolved from comment/session/manager
- If comment contains Scalping → Scalping
- If comment contains Normal → Normal
- If unknown → Unknown context

### 4. Log Row / Signal Row

Log row double-click remains Log Details.

Add right-click menu and/or Rules icon:

```text
Open Log Details
Open Rules Monitor
Copy Log
Copy Decision Audit
```

Rules Monitor should parse context from log text where possible:

- Pair
- Strategy
- Ticket
- RequestId
- Rule code
- Decision status

## Required Context Model

Create model:

```csharp
public sealed class TradeRulesContext
{
    public string Pair { get; set; } = "";
    public TradeRulesStrategy Strategy { get; set; } = TradeRulesStrategy.Unknown;
    public long? Ticket { get; set; }
    public TradeType? TradeType { get; set; }
    public string? RequestId { get; set; }
    public bool IsRunningTrade { get; set; }
    public string OpenedFrom { get; set; } = ""; // ScalpingPanel, NormalPanel, RunningTrade, LogScreen, SignalCard
    public string? RawLogLine { get; set; }
}
```

Create enum:

```csharp
public enum TradeRulesStrategy
{
    Unknown = 0,
    Scalping = 1,
    Normal = 2
}
```

## Main Header Requirements

Header should show all available account/trade context.

Show full account details because user selected **Show all**:

- Account number
- Server
- Broker
- Balance
- Equity
- Free margin
- Margin level
- Floating P/L

Show trade/session context:

- Pair
- Strategy
- Ticket if available
- Trade type
- Entry price
- Current price
- SL
- TP
- Volume/lot
- Running P/L
- Opened time if available
- Source: ScalpingPanel / NormalPanel / RunningTrade / LogScreen / SignalCard

Do not show:

- API keys
- Tokens
- Passwords
- Provider secrets

## Default Runtime Editing Mode

When opened for running trade/session:

- Default mode = **Monitor Only**
- Editing controls disabled by default
- User must click **Enable Runtime Editing** before changing live rules

When opened before a new trade/session:

- Editing can be available immediately, but still no auto-save

## Save / Apply Behavior

There must be **no auto-save**.

User changes values as preview/runtime edits first.

Buttons:

1. **Apply Runtime**
   - Applies to current active session/trade decision runtime.
   - Affects next decision/check.
   - Does not automatically modify existing MT5 position SL/TP.

2. **Save Pair Defaults**
   - Saves pair-specific defaults for future trades.
   - Examples:
     - `scalping_by_pair.XAUUSD`
     - `normal_trading_by_pair.XAUUSD`
     - `pair_settings.XAUUSD`

3. **Save Strategy Defaults**
   - Saves global strategy/default settings.
   - Examples:
     - `scalping`
     - `normal_trading`
     - `common_trading`

4. **Reset**
   - Resets whole visible context/tab/group if applicable.

5. **Close**

Each individual rule/value row must also have its own:

```text
Reset / ↺ Default
```

This resets only that rule/value to standard/default.

## Live Refresh Behavior

- Refresh live values every **1 second** while window is open.
- Do not write to main log on every refresh.
- Only write to log when:
  - user changes a value
  - user enables/disables a rule
  - rule status changes if meaningful
  - export is performed
  - runtime apply/save occurs

## Live Value Sources

The monitor must identify all live values and their source.

Sources include:

1. MT5 bridge live market data
   - Bid
   - Ask
   - Spread
   - Current price
   - Symbol metadata

2. MT5 account data
   - Balance
   - Equity
   - Free margin
   - Margin level
   - Floating P/L

3. MT5 positions
   - Ticket
   - Symbol
   - Type
   - Entry
   - SL
   - TP
   - Volume
   - Current P/L

4. Scalping session runtime
   - Session elapsed time
   - Cooldown remaining
   - Trades count
   - Session P/L
   - Last BUY score
   - Last SELL score
   - Selected direction
   - Last no-trade reason

5. Normal trade runtime
   - Active normal trade/session state
   - Expiry/age
   - Current limits

6. AppSettings/config
   - Configured limits
   - Enabled/disabled rule states

7. PairSettings
   - Pip size
   - ATR limits
   - Key level distance
   - Trailing/slippage/session settings

8. AutoBotService audit
   - Last execution gate result
   - Layer PASS/BLOCK details
   - Blocking rule/reason

9. News calendar
   - Blackout active
   - High-impact event status
   - Relevant currency/event reason

10. AI/snapshot/market structure data where available
   - AI confirmation
   - Confidence
   - ADX
   - Trend/structure
   - Snapshot age

## Top Decision Summary

Show a live summary at the top:

```text
Current Decision: NO TRADE / TRADE ALLOWED / TRADE RUNNING / UNKNOWN
Main Blocking Rule: SCALP-DIRECTION-TIE Buy/Sell Equal Strength Block
Risk Level: Low / Medium / High
Passed: 18
Warning: 2
Blocked: 0
Disabled: 3
Disabled But Would Block: 1
```

Risk level behavior:

- If any active rule blocks → High / Red
- If warnings exist but no blocks → Medium / Yellow/Orange
- If `Disabled But Would Block > 0` → High / Red/Orange
- If all pass → Low / Green

## Live Overview Cards

The Live Overview tab must show cards:

1. Decision
2. Spread
3. BUY Score
4. SELL Score
5. ADX
6. Session P/L
7. Cooldown
8. News Risk
9. Margin Level
10. Daily/Weekly Loss
11. Open Positions

Each card shows:

- Live value
- Configured limit
- Status color
- Short reason
- Rule code if relevant

## Tabs

Tabs depend on strategy context.

### Scalping Context

1. Live Overview
2. Scalping Rules
3. Common Rules
4. Pair Rules
5. Broker Rules
6. Account Protection
7. Safety / News / Session
8. Decision Audit

### Normal Context

1. Live Overview
2. Normal Rules
3. Common Rules
4. Pair Rules
5. Broker Rules
6. Account Protection
7. Safety / News / Session
8. Decision Audit

### Unknown Context

1. Live Overview
2. Common Rules
3. Pair Rules
4. Broker Rules
5. Account Protection
6. Safety / News / Session
7. Decision Audit

## Rule Row Default View

Default view should be user-friendly.

Each row shows:

- Enable/disable checkbox
- Fixed rule code
- Friendly rule name
- Source name
- Standard/default value
- Configured value editable control
- Live current value
- Min/max range
- Numeric input + slider if numeric
- Green → yellow/orange → red visual feedback bar
- Result status:
  - PASS
  - WARNING
  - BLOCK
  - DISABLED
  - NOT_CHECKED
- Reason
- Last checked time
- Reset button for this rule/value

## Advanced Details Section

Function name, variable name, and source file should be hidden in expandable Advanced Details.

Show:

- Function name
- Variable name
- Source file/path
- Technical internal key
- Raw value if useful

## Editable Controls

Numeric values:

- NumericUpDown or TextBox
- Slider/TrackBar
- Colored feedback bar
- Min/max labels

Boolean values:

- Checkbox

Enum values:

- Dropdown

Text values:

- TextBox

List/session values:

- Checklist/list editor

## Grouping Requirement

Each tab should support collapsible groups.

Examples for Scalping Rules:

- Direction Rules
- Spread Rules
- Score Rules
- Runtime Session Rules
- Request Build Rules

Each group supports:

- Enable All rules in this group
- Disable All rules in this group

Group-level disable must require confirmation:

```text
You are disabling 5 Direction Rules. This may increase trading risk. Continue?
```

No global Disable All for the whole window.

## Rule Disable Confirmation Policy

- Normal rule disable = no confirmation
- Critical rule disable = confirmation
- Group disable = confirmation

Critical examples:

- Kill switch
- Margin validation
- Broker stop level
- Broker freeze level
- Broker lot size
- OrderCheck
- Daily loss
- Weekly loss
- Symbol exposure

## Disabled Rule Behavior

Disabled rules must never be hidden.

Disabled rules must remain visible in:

- Rule tabs
- Live Overview where relevant
- Decision Audit
- Export JSON/TXT
- Snapshot history

When a rule is disabled:

- Backend should still calculate/check the rule.
- It should show what result would have happened.
- It must not block trade.

Display:

```text
Status: DISABLED
Would-have-result: PASS / WARNING / BLOCK
Actual effect: Ignored because rule is disabled
```

Example:

```text
SCALP-SPREAD-LIMIT — Scalping Spread Limit
Status: DISABLED
Live Spread: 45 pips
Limit: 32 pips
Would Have Result: BLOCK
Actual Effect: Ignored because rule is disabled
```

Top summary must count:

```text
Passed: 18
Warning: 2
Blocked: 0
Disabled: 3
Disabled But Would Block: 1
```

If `Disabled But Would Block > 0`:

- Risk Level = High
- Color = Red/Orange

## Decision Audit

Default ordering = original checking order.

Add filter buttons:

- All
- Blocked
- Warning
- Disabled
- Passed

Decision Audit should show:

- Rule code
- Friendly name
- Status
- Standard value
- Configured value
- Live value
- Would-have-result if disabled
- Reason
- Function/variable/source in Advanced Details

## Search / Filter

Add search/filter box.

Search by:

- Rule code
- Rule name
- Function name
- Variable name
- Status/result
- Source
- Category

Example:

Search: `spread`

Shows:

- Spread Filter
- Session Spread
- Spread Percent of TP
- Max Spread Pips

## Snapshot History

Keep in-window snapshot history of status/value/user changes.

Examples:

```text
10:30:01 Spread PASS
10:30:02 Spread WARNING
10:30:03 Direction Tie BLOCK
10:30:04 User disabled Direction Tie rule
```

Requirements:

- Keep last 200 rows.
- Add Clear History button.
- Do not spam main app log every second.

## Export

Add Export button with:

1. Export JSON
2. Export readable TXT

Export includes:

- Context
- Full account number/server/broker/account values
- Decision summary
- Live overview cards
- All rules
- Standard/configured/live values
- Status
- Would-have-result
- Reasons
- Last checked time
- Snapshot history

Export must not include:

- API keys
- Tokens
- Passwords
- Provider secrets

## Fixed Rule Code Catalog

Codes must be fixed/stable and human-readable.

UI must show both:

```text
SCALP-DIRECTION-TIE
Buy/Sell Equal Strength Block
```

Logs should include both code and friendly name.

### Common Rules

| Rule Code | User-Friendly Rule Name | Meaning |
|---|---|---|
| COMMON-TRADING-MODE | Trading Mode | Paper/manual/live mode affecting execution behavior |
| COMMON-AI-CONFIRM | AI Confirmation | Whether AI confirmation is required |
| COMMON-AUTO-CLOSE | Auto Close After Open | Auto-close behavior after trade opens |
| COMMON-PROFIT-PIPS | Common Profit Target Pips | Common pip profit target |
| COMMON-PROFIT-USD | Common Profit Target USD | Common money profit target |
| COMMON-BREAKEVEN-TRIGGER | Move SL to Breakeven Trigger | Percent of TP required before SL moves to breakeven |
| COMMON-MAX-SPREAD | Common Max Spread Limit | Shared max spread limit if used globally |
| COMMON-MAX-POSITIONS | Max Open Positions | Maximum bot positions allowed |
| COMMON-CORRELATION | Correlation Protection | Blocks correlated positions if enabled |

### Scalping Rules

| Rule Code | User-Friendly Rule Name | Meaning |
|---|---|---|
| SCALP-ENABLED | Scalping Enabled | Allows or blocks scalping strategy |
| SCALP-MAX-TRADES | Max Scalping Trades | Maximum scalping trades/session/day depending implementation |
| SCALP-MAX-MINUTES | Scalping Session Time Limit | Maximum scalping session duration |
| SCALP-SESSION-LOSS | Scalping Session Loss Limit | Stops/blocks when session loss limit is reached |
| SCALP-PROFIT-TARGET | Scalping Profit Target | Stops/blocks when scalping profit target is reached |
| SCALP-SL-PIPS | Scalping Stop Loss Pips | Stop-loss distance for scalping entries |
| SCALP-TP-PIPS | Scalping Take Profit Pips | Take-profit distance for scalping entries |
| SCALP-RISK-REWARD | Scalping Risk Reward | Minimum/target RR for scalping |
| SCALP-SPREAD-LIMIT | Scalping Spread Limit | Max spread allowed for scalping |
| SCALP-SPREAD-TP-PERCENT | Spread Percent of TP | Spread cannot exceed configured percent of TP |
| SCALP-DYNAMIC-VALUES | Dynamic Scalping Values | Enables dynamic SL/TP/decision values where supported |
| SCALP-POLL-INTERVAL | Scalping Check Interval | How often scalping checks market |
| SCALP-COOLDOWN | Scalping Cooldown | Wait time between scalping entries |
| SCALP-DIRECTION-MODE | Scalping Direction Mode | Auto/buy/sell direction behavior |
| SCALP-PYRAMIDING | Pyramiding Rule | Allows or blocks stacking entries |
| SCALP-SNAPSHOT-CONFIRM | Snapshot Confirmation | Requires live market snapshot confirmation |
| SCALP-MIN-SCORE | Minimum Scalping Score | Minimum score required to trade |
| SCALP-AI-CONFIRM | Scalping AI Confirmation | AI confirmation for scalping |
| SCALP-BUY-SCORE | BUY Score | Current BUY setup score |
| SCALP-SELL-SCORE | SELL Score | Current SELL setup score |
| SCALP-DIRECTION-TIE | Buy/Sell Equal Strength Block | Blocks trade when BUY and SELL are equally strong |
| SCALP-REQUEST-BUILD | Scalping Trade Request Build | Final scalping request values before execution gate |

### Normal Trading Rules

| Rule Code | User-Friendly Rule Name | Meaning |
|---|---|---|
| NORMAL-ENABLED | Normal Trading Enabled | Allows or blocks normal strategy |
| NORMAL-MAX-TRADES | Max Normal Trades | Maximum normal trades |
| NORMAL-EXPIRY | Normal Trade Expiry | Signal/request expiry time |
| NORMAL-SL-PIPS | Normal Stop Loss Pips | Stop-loss distance for normal trade |
| NORMAL-TP-PIPS | Normal Take Profit Pips | Take-profit distance for normal trade |
| NORMAL-RISK-REWARD | Normal Risk Reward | Minimum/target RR for normal trade |
| NORMAL-SPREAD-LIMIT | Normal Spread Limit | Max spread for normal trading if still strategy-specific |

### Pair Rules

| Rule Code | User-Friendly Rule Name | Meaning |
|---|---|---|
| PAIR-PIP-SIZE | Pair Pip Size | Pip size for selected pair |
| PAIR-M5-ATR-MIN | M5 Minimum ATR | Minimum M5 volatility allowed |
| PAIR-M5-ATR-MAX | M5 Maximum ATR | Maximum M5 volatility allowed |
| PAIR-M15-ATR-MIN | M15 Minimum ATR | Minimum M15 volatility allowed |
| PAIR-M15-ATR-MAX | M15 Maximum ATR | Maximum M15 volatility allowed |
| PAIR-KEYLEVEL-DISTANCE | Key Level Distance | Minimum distance from support/resistance/key level |
| PAIR-TRAILING-START | Trailing Start | Profit pips before trailing starts |
| PAIR-TRAILING-STEP | Trailing Step | Trailing step size |
| PAIR-MAX-SLIPPAGE | Pair Max Slippage | Max slippage allowed for pair |
| PAIR-RECOMMENDED-SESSION | Recommended Session | Preferred session check |
| PAIR-AVOID-SESSION | Avoid Session | Session that should block/avoid trades |

### Broker Rules

| Rule Code | User-Friendly Rule Name | Meaning |
|---|---|---|
| BROKER-SYMBOL-DATA | Broker Symbol Data | Bid/ask/spread/metadata availability |
| BROKER-STOP-LEVEL | Broker Stop Level | SL/TP minimum broker distance |
| BROKER-FREEZE-LEVEL | Broker Freeze Level | Broker freeze-zone validation |
| BROKER-LOT-SIZE | Broker Lot Size | Min/max/step/volume validation |
| BROKER-ORDER-CHECK | Broker OrderCheck | Broker pre-check before live order |
| BROKER-MARKET-OPEN | Market Open / Trade Allowed | Market/trading permission check |
| BROKER-COMMISSION | Commission Model | Commission model validation/calculation |
| BROKER-SLIPPAGE | Slippage Model | Slippage model validation/calculation |

### Account Protection Rules

| Rule Code | User-Friendly Rule Name | Meaning |
|---|---|---|
| ACCOUNT-DATA | Account Data Available | Balance/equity/margin data availability |
| ACCOUNT-DAILY-LOSS | Daily Loss Limit | Daily loss hard stop/limit |
| ACCOUNT-WEEKLY-LOSS | Weekly Loss Limit | Weekly loss hard stop/limit |
| ACCOUNT-FLOATING-LOSS | Floating Loss Protection | Floating loss contribution to limits |
| ACCOUNT-SYMBOL-EXPOSURE | Same Symbol Exposure | Same-symbol lots/risk/position cap |
| ACCOUNT-MAX-CONCURRENT | Max Concurrent Positions | Maximum open bot positions |
| ACCOUNT-MARGIN | Projected Margin Validation | Margin level after trade |
| ACCOUNT-DRAWDOWN | Emergency Drawdown Stop | Emergency drawdown protection |
| ACCOUNT-KILL-SWITCH | Kill Switch | Persistent kill-switch protection |

### Safety / News / Session Rules

| Rule Code | User-Friendly Rule Name | Meaning |
|---|---|---|
| SAFETY-ROLLOUT-STAGE | Rollout Stage | Paper/demo/tiny-live/full-live stage gate |
| SAFETY-NO-TRADE-WINDOW | No-Trade Window | Configured blocked time window |
| SAFETY-SESSION-GATE | Pair Session Gate | Pair/session time validation |
| SAFETY-SIGNAL-AGE | Signal Age / Expiry | Signal not too old |
| SAFETY-PAIR-ALLOWLIST | Pair Allowlist | Pair must be allowed |
| SAFETY-NEWS-BLACKOUT | News Blackout Filter | High-impact news blackout |
| SAFETY-ADX-RANGING | ADX Ranging Filter | Blocks low-ADX/ranging market |
| SAFETY-FINAL-LIVE-READY | Final Live Readiness Gate | Final live-readiness protection |
| SAFETY-EDGE-MONITOR | Edge Monitor | Win-rate/edge degradation protection |

### Execution / Decision Audit Rules

| Rule Code | User-Friendly Rule Name | Meaning |
|---|---|---|
| EXEC-FINAL-GATE | Final Execution Gate | Overall execution gate result |
| EXEC-RISK-VALIDATION | Risk Validation | Risk manager result |
| EXEC-EFFECTIVE-SETTINGS | Effective Settings Resolve | Resolved common/scalping/normal settings |
| EXEC-REQUEST-VALIDATION | Trade Request Validation | Request structure/required fields |
| EXEC-ORDER-SEND | Order Send Result | MT5 order send result |
| EXEC-ORDER-RETRY | Order Retry Policy | Retry behavior after order failure |
| EXEC-TRADE-ACCEPTED | Trade Accepted | Trade approved/executed |
| EXEC-TRADE-REJECTED | Trade Rejected | Trade blocked/rejected |
| EXEC-NO-TRADE | No Trade Decision | Strategy decided no trade |

## Logging Requirements

When Rules Monitor opens:

```text
[RULES_MONITOR] Opened | Source=... | Pair=... | Strategy=... | Ticket=...
```

When value changes:

```text
[RULES_MONITOR] ValueChanged | Rule=SCALP-MIN-SCORE Minimum Scalping Score | Old=6 | New=7 | Pair=XAUUSD | Strategy=Scalping
```

When bypass changes:

```text
[RULES_MONITOR] BypassChanged | Rule=SCALP-DIRECTION-TIE Buy/Sell Equal Strength Block | Enabled=False | Pair=XAUUSD | Strategy=Scalping
```

When critical rule disabled:

```text
[RULES_MONITOR] CriticalRuleDisabled | Rule=BROKER-ORDER-CHECK Broker OrderCheck | Pair=XAUUSD | Strategy=Scalping
```

When trade accepted/rejected/no-trade:

```text
[SCALP_DECISION] NO_TRADE | Rule=SCALP-DIRECTION-TIE Buy/Sell Equal Strength Block | BUY=7/7 | SELL=7/7 | Reason=Both sides equally strong
[EXEC_AUDIT] BLOCK | Rule=BROKER-STOP-LEVEL Broker Stop Level | Current=20 pips | Min=30 pips
[BOT] Rejected | MainRule=SAFETY-NEWS-BLACKOUT News Blackout Filter | Reason=High impact USD news active
```

## Hardcoded Values to Move to Config

No trade decision value should remain hardcoded if it belongs to scalping/normal/common/pair rules.

Known current hardcoded value:

```csharp
ScalpingMaxSpreadPercentOfTp = 20.0
```

Move to config/model, preferably:

```json
"common_trading": {
  "max_spread_percent_of_tp": 20.0
}
```

or if Codex finds it is scalping-only:

```json
"scalping": {
  "max_spread_percent_of_tp": 20.0
}
```

Codex must inspect actual usage before deciding final location.

## Suggested New Models

### TradeRuleRuntimeSnapshot

```csharp
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

    public string Result { get; set; } = "NOT_CHECKED";
    public string? WouldHaveResult { get; set; }
    public string Reason { get; set; } = "";
    public string ActualEffect { get; set; } = "";
    public DateTime? LastCheckedAtUtc { get; set; }
}
```

### TradeRulesContext

As defined above.

### TradeRuleCatalogItem

```csharp
public sealed class TradeRuleCatalogItem
{
    public string RuleCode { get; set; } = "";
    public string RuleName { get; set; } = "";
    public string Category { get; set; } = "";
    public string GroupName { get; set; } = "";
    public string FunctionName { get; set; } = "";
    public string VariableName { get; set; } = "";
    public string SourceFile { get; set; } = "";
    public bool IsCritical { get; set; }
    public bool IsEditable { get; set; } = true;
    public string ValueType { get; set; } = "Number"; // Number, Bool, Enum, Text, List
}
```

## Suggested Services

### TradeRuleCatalog

Responsible for fixed rule codes and metadata.

### TradeRulesRuntimeSnapshotService

Responsible for:

- Building snapshots from current config/runtime/live data.
- Pulling MT5 live values.
- Pulling active session/trade data.
- Pulling last audit values.
- Calculating PASS/WARNING/BLOCK/DISABLED.
- Calculating WouldHaveResult for disabled rules.

### TradeRulesRuntimeControlService

Responsible for:

- Applying runtime edits.
- Saving pair defaults.
- Saving strategy defaults.
- Resetting rules to defaults.
- Enabling/disabling rules.

### TradeRulesExportService

Responsible for:

- Export JSON.
- Export TXT.
- Redacting API keys/tokens/secrets.

## Suggested Development Checklist File

Codex must create:

```text
Docs/LiveTradeRulesMonitor_DevelopmentChecklist.md
```

The checklist must include all phases below and Codex must mark each phase done.

## Development Phases

### Phase 0 — Repository Inspection

Tasks:

- Inspect repo structure.
- Identify MainForm files.
- Identify Log Detail window code.
- Identify scalping panel code.
- Identify normal trading panel code.
- Identify running positions grid code.
- Identify signal card/feed row code.
- Identify AutoBotService execution audit code.
- Identify settings models and config load/save logic.
- Identify scalping session service/manager.
- Identify normal trade manager.
- Identify MT5Bridge data methods.

Output:

- Update checklist with discovered files/classes.
- Do not change behavior yet.

Build:

```bash
dotnet build
```

If tests exist:

```bash
dotnet run --project Tests/ForexBot.Tests
```

Mark Phase 0 DONE only after build succeeds or existing pre-feature errors are documented.

### Phase 1 — Add Rule Catalog and Core Models

Tasks:

- Add `TradeRulesStrategy` enum.
- Add `TradeRulesContext` model.
- Add `TradeRuleRuntimeSnapshot` model.
- Add `TradeRuleCatalogItem` model.
- Add `TradeRuleCatalog` with fixed rule-code list.
- Ensure rule codes from this doc are represented.

No UI yet.

Build and fix compile errors.

Mark Phase 1 DONE.

### Phase 2 — Config Support for Rule Enable/Disable and Hardcoded Values

Tasks:

- Add config properties needed for rule enable/disable where not present.
- Add config-driven value for `ScalpingMaxSpreadPercentOfTp`.
- Do not remove old behavior until all references updated.
- Add default values compatible with existing settings.
- Update `settings.json` with new properties.
- Preserve backward compatibility if config fields missing.

Build and fix compile errors.

Mark Phase 2 DONE.

### Phase 3 — Runtime Snapshot Service

Tasks:

- Add `TradeRulesRuntimeSnapshotService`.
- Build snapshots from:
  - AppSettings
  - PairSettings
  - MT5Bridge live market/account/position values
  - Scalping session runtime if accessible
  - Normal trade runtime if accessible
  - AutoBotService last audit if accessible
  - News calendar if accessible
- If some live source is not accessible, mark snapshot status as `NOT_CHECKED` with reason.
- Do not break existing trading flow.

Build and fix compile errors.

Mark Phase 3 DONE.

### Phase 4 — Disabled Rule Calculation Semantics

Tasks:

- Implement rule status behavior:
  - Enabled + pass → PASS
  - Enabled + warning → WARNING
  - Enabled + block → BLOCK
  - Disabled + would pass → DISABLED with WouldHaveResult=PASS
  - Disabled + would warn → DISABLED with WouldHaveResult=WARNING
  - Disabled + would block → DISABLED with WouldHaveResult=BLOCK
- Disabled rules must not block.
- Active rules continue existing behavior.
- Add summary counters:
  - Passed
  - Warning
  - Blocked
  - Disabled
  - Disabled But Would Block

Build and fix compile errors.

Mark Phase 4 DONE.

### Phase 5 — Main Monitor Form UI Skeleton

Tasks:

- Add `LiveTradeRulesMonitorControlForm`.
- Add header area.
- Add top decision summary.
- Add tabs based on strategy context.
- Add 1-second timer.
- Add monitor-only default mode for running trade/session.
- Add `Enable Runtime Editing` button.
- Add Apply Runtime / Save Pair Defaults / Save Strategy Defaults / Reset / Close buttons.
- Add Export button.
- Add Search box.

No complex rule rows yet; use placeholder grid/list if needed.

Build and fix compile errors.

Mark Phase 5 DONE.

### Phase 6 — Rule Row UI and Visual Controls

Tasks:

- Implement rule rows/cards.
- Default user-friendly view.
- Advanced Details expandable section.
- Numeric value editor + slider/TrackBar.
- Colored green/yellow/orange/red bar.
- Boolean checkbox.
- Enum dropdown.
- Text/list editors where applicable.
- Per-rule reset button.
- Rule status colors.
- Source/standard/configured/live values.

Build and fix compile errors.

Mark Phase 6 DONE.

### Phase 7 — Grouping, Filters, Search, Decision Audit

Tasks:

- Add collapsible groups per tab.
- Add group Enable All / Disable All.
- Group disable confirmation.
- Individual critical disable confirmation.
- Decision Audit original checking order.
- Filter buttons:
  - All
  - Blocked
  - Warning
  - Disabled
  - Passed
- Search by rule code/name/function/variable/status/source/category.

Build and fix compile errors.

Mark Phase 7 DONE.

### Phase 8 — Contextual Opening Integration

Tasks:

- Add Scalping Rules button in scalping panel.
- Add Normal Rules button in normal panel.
- Add right-click log menu item:
  - Open Rules Monitor
- Preserve log row double-click = Log Details.
- Add Rules icon/button on eligible signal/feed rows if practical.
- Add Rules action for running position row.
- Resolve context from pair/strategy/ticket/log/request.

Build and fix compile errors.

Mark Phase 8 DONE.

### Phase 9 — Runtime Apply / Save / Reset

Tasks:

- Implement Apply Runtime.
- Implement Save Pair Defaults.
- Implement Save Strategy Defaults.
- Implement per-rule reset.
- Implement group reset if applicable.
- No auto-save.
- Log user changes.
- Ensure runtime changes affect next scalping/normal checks where possible.
- If a session cannot accept runtime update yet, add safe update method or document limitation.

Build and fix compile errors.

Mark Phase 9 DONE.

### Phase 10 — Logging and Rule Code Integration

Tasks:

- Add rule codes to:
  - scalping decision logs
  - execution audit logs
  - trade accepted/rejected logs
  - no-trade logs
- Keep existing log text but enrich with RuleCode + FriendlyName.
- Add `[RULES_MONITOR]` logs for open/value change/bypass/export/apply/save.
- Do not log every 1-second refresh.
- Do not log API keys/secrets.

Build and fix compile errors.

Mark Phase 10 DONE.

### Phase 11 — Snapshot History

Tasks:

- Keep last 200 rows in window.
- Add Clear History button.
- Add entries only for:
  - status transition
  - value change
  - enable/disable change
  - apply/save/export actions
- Do not spam main log.

Build and fix compile errors.

Mark Phase 11 DONE.

### Phase 12 — Export JSON/TXT

Tasks:

- Implement Export JSON.
- Implement Export TXT.
- Include full account details.
- Include context/summary/overview/rules/audit/history.
- Redact API keys/tokens/passwords/provider secrets.

Build and fix compile errors.

Mark Phase 12 DONE.

### Phase 13 — Tests / Validation

Tasks:

- Add or update tests where practical.
- Test rule catalog uniqueness.
- Test disabled rule would-have-result logic.
- Test export redaction.
- Test summary counters.
- Test no auto-save behavior if practical.
- Run:

```bash
dotnet build
```

and if available:

```bash
dotnet run --project Tests/ForexBot.Tests
```

Fix errors.

Mark Phase 13 DONE.

### Phase 14 — Final Review

Tasks:

- Verify no double-click log behavior was broken.
- Verify Rules Monitor opens from correct contexts.
- Verify all eligible rows have right-click/menu/icon access.
- Verify disabled rules remain visible.
- Verify critical disable confirmation works.
- Verify `Disabled But Would Block` risk turns High red/orange.
- Verify no secrets in logs/export.
- Verify build/tests pass.
- Update checklist final status.

Mark Phase 14 DONE.

## Final Output Codex Must Return

When done, Codex must report:

1. Changed files.
2. New files/classes.
3. New config properties.
4. Rule catalog location.
5. How to open monitor from:
   - Scalping panel
   - Normal panel
   - Log row
   - Running position row
   - Signal card/feed row
6. Which rules are fully runtime-controllable.
7. Which rules are monitor-only because runtime update was not safely possible.
8. Build result.
9. Test result.
10. Any remaining limitations.

## Codex Start Command / Prompt

Use this prompt in Codex:

```text
Read the file `Docs/Codex_Live_Trade_Rules_Monitor_Master_Plan.md` if present. If not present, use the complete master plan pasted in this prompt.

You must implement the Live Trade Rules Monitor & Control feature step by step.

Follow the Development Phases exactly.
After each phase:
- Update `Docs/LiveTradeRulesMonitor_DevelopmentChecklist.md`
- Mark the phase DONE
- Run `dotnet build`
- Fix compile errors
- Run `dotnet run --project Tests/ForexBot.Tests` when practical
- Continue automatically to the next phase

Do not ask after each phase.
Do not break existing log double-click behavior.
Do not expose API keys/secrets.
Do not auto-save user edits.
Do not bypass central trade execution flow.

Start with Phase 0 repository inspection now.
```

