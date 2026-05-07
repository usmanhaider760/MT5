# P0 Safety Patch Plan

Source: `FOREX_BOT_AUDIT_REPORT.md`

Scope: P0 live-trading account safety only.

Rules:

- Do not optimize entries.
- Do not change indicators.
- Do not change AI prompts.
- Do not change take-profit strategy.
- Do not change strategy entry logic.

## Current P0 Status

| # | P0 Item | Current Status |
|---|---|---|
| 1 | Centralize all live order execution behind one approved execution gate | Implemented in current workspace; keep covered by tests |
| 2 | Block/reroute direct `MT5Bridge.OpenTradeAsync` calls outside approved execution service | Implemented in current workspace; keep covered by guard test |
| 3 | Max daily loss hard stop | Not implemented |
| 4 | Max weekly loss hard stop | Not implemented |
| 5 | Per-symbol exposure limits | Not implemented |
| 6 | Projected margin-level validation before every trade | Not implemented |
| 7 | Persistent kill-switch state after emergency drawdown | Not implemented |
| 8 | Live mode fail-closed when account, margin, news, or risk data is unavailable | Partially implemented; needs hardening |

## Patch 1: Central Approved Execution Gate

Status: Implemented in current workspace; retain as P0 baseline.

Files likely to modify if further hardening is needed:

- `Application/Workflows/AutoBotService.cs`
- `Trading/TradeExecution/ITradeExecutionService.cs`
- `Trading/TradeExecution/TradeExecutionService.cs`
- `UI/Forms/MainForm.cs`
- `Infrastructure/AI/ClaudeSignalService.cs`
- `Tests/ForexBot.Tests/Program.cs`

Exact behavior required:

- All live orders must pass through `AutoBotService.ExecuteTradeWithValidationAsync`.
- `AutoBotService` must perform request validation, account fetch, symbol/spread fetch, risk validation, news validation, correlation validation, and then call `ITradeExecutionService`.
- `TradeExecutionService` is the only approved service allowed to call `MT5Bridge.OpenTradeAsync`.
- Paper trading may simulate fills, but live trading must use the approved gate.

Failure scenario prevented:

- Manual, JSON, AI, or auto-scalping trade paths bypass risk validation and submit directly to MT5.

Test cases required:

- Manual trade routes through `AutoBotService.ExecuteTradeWithValidationAsync`.
- JSON trade routes through `AutoBotService.ExecuteTradeWithValidationAsync`.
- AI-generated trade routes through `AutoBotService.ExecuteTradeWithValidationAsync`.
- Paper mode does not call MT5.
- Risk rejection prevents broker send.

Changes strategy logic:

- No.

## Patch 2: Block Direct `MT5Bridge.OpenTradeAsync` Bypasses

Status: Implemented in current workspace; retain guard test.

Files likely to modify if further hardening is needed:

- `UI/Forms/MainForm.cs`
- `Application/Workflows/AutoBotService.cs`
- `Trading/TradeExecution/TradeExecutionService.cs`
- `Infrastructure/MT5/MT5Bridge.cs`
- `Tests/ForexBot.Tests/Program.cs`

Exact behavior required:

- No UI, AI, workflow fallback, or helper class may call `MT5Bridge.OpenTradeAsync` directly.
- Direct live broker send is allowed only inside `Trading/TradeExecution/TradeExecutionService.cs`.
- If the execution gate is unavailable, live execution must reject with a clear safety error instead of using a direct bridge fallback.

Failure scenario prevented:

- `_bot == null` or similar fallback path submits a live trade without risk checks.

Test cases required:

- Source guard test fails if `.OpenTradeAsync(` appears outside `MT5Bridge` declaration and `TradeExecutionService`.
- AI fallback rejects when gate is unavailable.
- Manual fallback rejects or creates the gate instead of calling bridge directly.
- JSON fallback rejects or creates the gate instead of calling bridge directly.

Changes strategy logic:

- No.

## Patch 3: Max Daily Loss Hard Stop

Status: Not implemented.

Files likely to modify:

- `Domain/Models/Models.cs`
- `Trading/RiskManagement/IRiskManager.cs`
- `Trading/RiskManagement/RiskManager.cs`
- `Application/Workflows/AutoBotService.cs`
- `Infrastructure/Persistence/ITradeRepository.cs`
- `Infrastructure/Persistence/SqliteTradeRepository.cs`
- `Tests/ForexBot.Tests/Program.cs`

Exact behavior required:

- Add daily loss settings to `BotConfig`, for example `MaxDailyLossPercent` and/or `MaxDailyLossAmount`.
- Before every live trade, calculate today’s realized P/L from persisted trade history plus current floating P/L from open positions.
- If daily loss is at or beyond the configured limit, reject the trade with `DAILY_LOSS_LIMIT`.
- Daily loss check must run before `TradeExecutionService.ExecuteAsync`.
- If daily loss data cannot be calculated in live mode, reject the trade.

Failure scenario prevented:

- Bot keeps opening trades after the account has already hit the maximum allowed daily loss.

Test cases required:

- Daily loss below limit allows risk validation to continue.
- Daily realized loss at limit blocks.
- Daily realized loss above limit blocks.
- Floating loss contributes to daily loss.
- Missing account, position, or trade-history data blocks in live mode.
- Paper mode behavior remains explicit and does not affect live hard stop.

Changes strategy logic:

- No.

## Patch 4: Max Weekly Loss Hard Stop

Status: Not implemented.

Files likely to modify:

- `Domain/Models/Models.cs`
- `Trading/RiskManagement/IRiskManager.cs`
- `Trading/RiskManagement/RiskManager.cs`
- `Application/Workflows/AutoBotService.cs`
- `Infrastructure/Persistence/ITradeRepository.cs`
- `Infrastructure/Persistence/SqliteTradeRepository.cs`
- `Tests/ForexBot.Tests/Program.cs`

Exact behavior required:

- Add weekly loss settings to `BotConfig`, for example `MaxWeeklyLossPercent` and/or `MaxWeeklyLossAmount`.
- Before every live trade, calculate current trading-week realized P/L plus current floating P/L.
- If weekly loss is at or beyond the configured limit, reject the trade with `WEEKLY_LOSS_LIMIT`.
- Define week boundaries clearly and consistently, preferably UTC week start.
- If weekly loss data cannot be calculated in live mode, reject the trade.

Failure scenario prevented:

- Bot continues trading after several losing days have already reached a weekly loss cap.

Test cases required:

- Weekly loss below limit allows risk validation to continue.
- Weekly realized loss at limit blocks.
- Weekly realized loss above limit blocks.
- Floating loss contributes to weekly loss.
- Trades outside the current week are excluded.
- Missing account, position, or trade-history data blocks in live mode.

Changes strategy logic:

- No.

## Patch 5: Per-Symbol Exposure Limits

Status: Not implemented.

Files likely to modify:

- `Domain/Models/Models.cs`
- `Trading/RiskManagement/RiskManager.cs`
- `Domain/Common/LotCalculator.cs`
- `Application/Workflows/AutoBotService.cs`
- `Tests/ForexBot.Tests/Program.cs`

Exact behavior required:

- Add per-symbol exposure settings, for example `MaxSymbolRiskPercent`, `MaxSymbolLots`, and optionally `BlockOppositeSymbolExposure`.
- Before every live trade, calculate existing same-symbol gross exposure using open positions plus the new trade.
- Use gross exposure, not only net exposure, so hedged positions still count.
- Reject with `SYMBOL_EXPOSURE_LIMIT` if same-symbol exposure exceeds configured limits.
- Include paper positions in paper-mode risk checks.

Failure scenario prevented:

- Bot opens multiple trades on the same symbol and concentrates account risk even though total open-trade count is under the max.

Test cases required:

- Same-symbol lots below cap allows.
- Same-symbol lots at or above cap blocks.
- Same-symbol risk below cap allows.
- Same-symbol risk at or above cap blocks.
- Opposite-direction same-symbol position still counts as exposure.
- Different symbols do not count toward same-symbol cap.
- Paper positions are included when paper trading is active.

Changes strategy logic:

- No.

## Patch 6: Projected Margin-Level Validation

Status: Not implemented.

Files likely to modify:

- `Domain/Models/Models.cs`
- `Trading/RiskManagement/RiskManager.cs`
- `Infrastructure/MT5/MT5Bridge.cs`
- `MT5_EA/TradingBotEA.mq5`
- `UI/Forms/MainForm.cs`
- `Tests/ForexBot.Tests/Program.cs`

Exact behavior required:

- Add minimum projected margin-level setting to `BotConfig`, for example `MinProjectedMarginLevelPercent`.
- Before every live trade, calculate or request required margin for the requested symbol, direction, and lot size.
- Project margin level after the new trade.
- Reject with `MARGIN_LEVEL_LIMIT` if projected margin level is below the configured minimum.
- EA should expose margin estimate using MT5 APIs such as `OrderCalcMargin` and/or validate with `OrderCheck`.
- If margin estimate is unavailable in live mode, reject the trade.

Failure scenario prevented:

- Bot opens a trade that passes lot/risk checks but pushes the account close to margin call or stop-out.

Test cases required:

- Healthy projected margin level allows.
- Projected margin level below configured minimum blocks.
- Missing margin estimate blocks live trade.
- Existing margin level already below threshold blocks.
- EA margin estimate error maps to a clear rejection.

Changes strategy logic:

- No.

## Patch 7: Persistent Kill Switch After Emergency Drawdown

Status: Not implemented.

Files likely to modify:

- `Domain/Models/Models.cs`
- `Application/Workflows/AutoBotService.cs`
- `Infrastructure/Config/SettingsManager.cs`
- `Infrastructure/Config/AppPaths.cs`
- `Data/Config/`
- `UI/Forms/MainForm.cs`
- `Tests/ForexBot.Tests/Program.cs`

Exact behavior required:

- When emergency drawdown fires, persist kill-switch state to disk.
- On application restart, load kill-switch state before any live trade can be placed.
- While kill switch is active, reject all live orders with `KILL_SWITCH_ACTIVE`.
- Clearing kill switch must require explicit user action.
- Emergency close-all attempts must log each close result and keep kill switch active if any close fails.

Failure scenario prevented:

- User restarts the app after emergency drawdown and the bot resumes live trading with no memory of the emergency stop.

Test cases required:

- Drawdown threshold triggers kill-switch persistence.
- Restart loads active kill switch.
- Active kill switch blocks manual, JSON, AI, and auto trades.
- Explicit clear disables kill switch.
- Failed emergency close keeps kill switch active.

Changes strategy logic:

- No.

## Patch 8: Live Mode Fail-Closed On Missing Safety Data

Status: Partially implemented; needs hardening.

Files likely to modify:

- `Application/Workflows/AutoBotService.cs`
- `Trading/RiskManagement/RiskManager.cs`
- `Infrastructure/MT5/MT5Bridge.cs`
- `Infrastructure/News/FmpNewsCalendarService.cs`
- `Domain/Models/Models.cs`
- `Tests/ForexBot.Tests/Program.cs`

Exact behavior required:

- In live mode, reject trades when account data is unavailable.
- In live mode, reject trades when symbol/spread data is unavailable.
- In live mode, reject trades when margin estimate is unavailable.
- In live mode, reject trades when required news data is unavailable.
- In live mode, reject trades when risk validation throws or returns incomplete data.
- Paper/demo behavior may be more permissive only if explicitly configured.
- Rejection codes should be specific: `NO_ACCOUNT`, `NO_SYMBOL_DATA`, `NO_MARGIN_DATA`, `NEWS_UNAVAILABLE`, `RISK_DATA_UNAVAILABLE`.

Failure scenario prevented:

- Bot places a live trade while blind to account, margin, spread, news, or risk conditions.

Test cases required:

- Missing account blocks live trade.
- Missing symbol/spread blocks live trade.
- Missing margin estimate blocks live trade.
- News unavailable blocks live trade when live fail-closed is enabled.
- Risk manager exception blocks live trade.
- Paper mode behavior remains explicitly separate and tested.

Changes strategy logic:

- No.

## Recommended P0 Implementation Order

1. Keep execution gate and direct-call guard green.
2. Add fail-closed handling for unavailable safety data.
3. Add max daily loss hard stop.
4. Add max weekly loss hard stop.
5. Add per-symbol exposure limits.
6. Add projected margin-level validation.
7. Add persistent kill-switch state after emergency drawdown.

Do not start strategy, indicator, AI-prompt, or take-profit changes until all P0 safety patches are complete and tested.

