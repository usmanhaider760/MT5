# P1 Execution Realism Plan

Scope: execution realism only, after P0 safety patches.

Rules:

- Do not optimize entries.
- Do not change indicators.
- Do not change AI prompts.
- Do not change take-profit strategy.
- Do not change strategy entry logic.
- Implement one patch at a time.

## Current P1 Status

| # | Item | Current Status |
|---|---|---|
| 1 | Commission model | Missing |
| 2 | Slippage model | Partial |
| 3 | Broker minimum stop-level validation | Missing |
| 4 | Broker freeze-level validation | Missing |
| 5 | Lot min/max/step validation using live symbol data | Partial |
| 6 | Rollover/no-trade window | Missing |
| 7 | Spread-widening protection by session | Partial |
| 8 | OrderCheck validation before live order send | Missing |
| 9 | Order rejection handling and retry policy | Partial |
| 10 | Backtest/live execution mismatch | Missing |

## 1. Commission Model

Current status:

- Missing.
- `Trading/Backtesting/BacktestingService.cs` calculates P/L from pips only.
- `MT5_EA/TradingBotEA.mq5` emits `"commission":0.00` in the market snapshot.
- `Domain/Models/Models.cs` has no live commission fields on `SymbolInfo`.

Files likely to modify:

- `Domain/Models/Models.cs`
- `Domain/Common/LotCalculator.cs`
- `Trading/RiskManagement/RiskManager.cs`
- `Trading/Backtesting/BacktestingService.cs`
- `Trading/Backtesting/IBacktestingService.cs`
- `MT5_EA/TradingBotEA.mq5`
- `Tests/ForexBot.Tests/Program.cs`

Exact behavior required:

- Add explicit commission settings, preferably per lot round-turn and/or per side.
- Include commission in backtest net P/L.
- Include commission in risk/reward and minimum R:R checks only when configured or broker data is available.
- If live cost-aware validation is enabled and commission data is unavailable, fail closed.
- Do not alter signal entry or TP selection.

Rejection/error code needed:

- `COMMISSION_DATA_UNAVAILABLE`
- `COMMISSION_COST_LIMIT`, only if a configured max cost threshold is exceeded.

Tests required:

- Backtest subtracts commission from winning trades.
- Backtest subtracts commission from losing trades.
- Commission changes net P/L and profit factor.
- Live cost-aware validation rejects when commission is required but unavailable.
- Disabled commission model preserves current behavior.

Changes strategy logic:

- No.

## 2. Slippage Model

Current status:

- Partial.
- `MT5_EA/TradingBotEA.mq5` sets `Trade.SetDeviationInPoints(InpSlippage)`.
- `Application/Workflows/AutoBotService.cs` logs high slippage after fill and closes on extreme slippage.
- No pre-trade slippage estimate, no per-symbol/session slippage model, and backtest does not model slippage.

Files likely to modify:

- `Domain/Models/Models.cs`
- `Trading/PairSettings/PairSettingsService.cs`
- `Application/Workflows/AutoBotService.cs`
- `Trading/TradeExecution/TradeExecutionService.cs`
- `Infrastructure/MT5/MT5Bridge.cs`
- `MT5_EA/TradingBotEA.mq5`
- `Trading/Backtesting/BacktestingService.cs`
- `Tests/ForexBot.Tests/Program.cs`

Exact behavior required:

- Add configured expected slippage and maximum allowed slippage by global config and pair settings.
- Pass max deviation/slippage intent to the EA where possible.
- Reject before live send if configured slippage data is required but unavailable.
- Keep post-fill slippage logging and extreme-slippage close behavior.
- Apply configured slippage assumptions in backtest fills.

Rejection/error code needed:

- `SLIPPAGE_DATA_UNAVAILABLE`
- `SLIPPAGE_LIMIT`
- `SLIPPAGE_CLOSE_FAILED`

Tests required:

- Live trade rejects when required slippage data is unavailable.
- Live trade rejects when expected slippage exceeds configured limit.
- EA/open request receives the configured deviation value.
- Backtest worsens entry/exit by configured slippage.
- Extreme post-fill slippage attempts close and reports close failure.

Changes strategy logic:

- No.

## 3. Broker Minimum Stop-Level Validation

Current status:

- Missing.
- `MT5_EA/TradingBotEA.mq5` exposes `stop_level` in snapshot, but `Domain/Models/Models.cs` `SymbolInfo` does not carry it.
- `Trading/RiskManagement/RiskManager.cs` checks pair SL/TP distance rules, not broker stop-level distance.
- Live orders rely on MT5 rejection instead of pre-send validation.

Files likely to modify:

- `Domain/Models/Models.cs`
- `Infrastructure/MT5/MT5Bridge.cs`
- `MT5_EA/TradingBotEA.mq5`
- `Trading/RiskManagement/RiskManager.cs`
- `Application/Workflows/AutoBotService.cs`
- `Tests/ForexBot.Tests/Program.cs`

Exact behavior required:

- Add stop-level points/pips to `SymbolInfo`.
- Before every live order, validate SL and TP distance from current bid/ask or pending entry against broker stop level.
- Reject orders whose SL/TP are too close before `TradeExecutionService.ExecuteAsync`.
- Fail closed when live stop-level data is required but unavailable.

Rejection/error code needed:

- `BROKER_STOP_LEVEL`
- `BROKER_STOP_LEVEL_UNAVAILABLE`

Tests required:

- BUY SL too close rejects.
- BUY TP too close rejects.
- SELL SL too close rejects.
- SELL TP too close rejects.
- Pending order entry/SL/TP distances honor broker stop level.
- Missing stop-level data blocks live trade when validation is enabled.

Changes strategy logic:

- No.

## 4. Broker Freeze-Level Validation

Current status:

- Missing.
- `MT5_EA/TradingBotEA.mq5` exposes `freeze_level` in snapshot, but app models do not use it.
- SL-to-breakeven and trailing stop modifications in `Application/Workflows/AutoBotService.cs` can attempt broker modifications inside freeze distance.

Files likely to modify:

- `Domain/Models/Models.cs`
- `Application/Workflows/AutoBotService.cs`
- `Infrastructure/MT5/MT5Bridge.cs`
- `MT5_EA/TradingBotEA.mq5`
- `Tests/ForexBot.Tests/Program.cs`

Exact behavior required:

- Add freeze-level points/pips to `SymbolInfo`.
- Before modify/close/delete actions that are freeze-sensitive, check current price distance.
- Skip modification and log a clear reason when inside freeze level.
- For live entries with pending orders, reject if entry/SL/TP operation would be invalid under freeze constraints.

Rejection/error code needed:

- `BROKER_FREEZE_LEVEL`
- `BROKER_FREEZE_LEVEL_UNAVAILABLE`

Tests required:

- SL-to-breakeven skips modify inside freeze level.
- Trailing stop skips modify inside freeze level.
- Pending order operation rejects inside freeze level.
- Missing freeze-level data blocks freeze-sensitive live operations when validation is enabled.
- Outside freeze level allows current modify behavior.

Changes strategy logic:

- No.

## 5. Lot Min/Max/Step Validation Using Live Symbol Data

Current status:

- Partial.
- `Domain/Models/Models.cs` `SymbolInfo` has `MinLot` and `MaxLot`, but no `LotStep`.
- `Domain/Common/LotCalculator.cs` clamps to hard-coded `0.01` and `100.0`.
- `Trading/RiskManagement/RiskManager.cs` does not normalize lots using live broker min/max/step before approval.
- `MT5_EA/TradingBotEA.mq5` margin estimate checks min/max, but open order path relies on broker rejection.

Files likely to modify:

- `Domain/Models/Models.cs`
- `Domain/Common/LotCalculator.cs`
- `Trading/RiskManagement/RiskManager.cs`
- `Application/Workflows/AutoBotService.cs`
- `MT5_EA/TradingBotEA.mq5`
- `Tests/ForexBot.Tests/Program.cs`

Exact behavior required:

- Add `LotStep` to `SymbolInfo`.
- Normalize requested and auto-calculated lot size to broker step.
- Reject if normalized lot is below min, above max, zero, NaN, or cannot be calculated.
- Use the same normalization in margin estimate and live order send.
- Paper mode may use configured defaults but must be explicit.

Rejection/error code needed:

- `BROKER_LOT_LIMIT`
- `BROKER_LOT_STEP`
- `BROKER_LOT_DATA_UNAVAILABLE`

Tests required:

- Auto lot rounds down or nearest-safe to broker lot step.
- Lot below broker min rejects or raises to min only when risk-safe.
- Lot above broker max rejects.
- Missing lot min/max/step blocks live trade when validation is enabled.
- Paper mode remains explicitly separate.

Changes strategy logic:

- No.

## 6. Rollover / No-Trade Window

Current status:

- Missing.
- Session data exists in MT5 snapshots and scalping checks market-open status.
- No global no-trade window around rollover or daily broker maintenance.

Files likely to modify:

- `Domain/Models/Models.cs`
- `Application/Workflows/AutoBotService.cs`
- `Trading/Scalping/ScalpingSessionService.cs`
- `Trading/PairSettings/PairSettingsService.cs`
- `Tests/ForexBot.Tests/Program.cs`

Exact behavior required:

- Add configurable no-trade windows using UTC and/or broker server time.
- Default should cover rollover if enabled, for example 5 minutes before to 10 minutes after configured rollover time.
- Live trades reject during active no-trade windows before risk validation and execution.
- Paper mode behavior must be explicit.

Rejection/error code needed:

- `ROLLOVER_WINDOW`
- `NO_TRADE_WINDOW`
- `SESSION_DATA_UNAVAILABLE`, if broker-time/session data is required but unavailable.

Tests required:

- Trade inside rollover window rejects.
- Trade before/after window allows validation to continue.
- Window crossing midnight works.
- Missing session/broker-time data blocks live trade when enabled.
- Paper mode remains explicitly separate.

Changes strategy logic:

- No.

## 7. Spread-Widening Protection By Session

Current status:

- Partial.
- Static global and pair max spread checks exist in `Trading/RiskManagement/RiskManager.cs`.
- Pair setting supports spread as percent of TP.
- No session-specific spread caps and no spread-widening detection relative to normal spread for London, New York, rollover, or off-session periods.

Files likely to modify:

- `Domain/Models/Models.cs`
- `Trading/PairSettings/PairSettingsService.cs`
- `Trading/RiskManagement/RiskManager.cs`
- `Application/Workflows/AutoBotService.cs`
- `Trading/Scalping/ScalpingSessionService.cs`
- `Tests/ForexBot.Tests/Program.cs`

Exact behavior required:

- Add session-aware spread caps, preferably by pair and session.
- Reject if spread exceeds the active session cap.
- Reject or warn if spread is widened above configured multiple of normal/good spread.
- Treat rollover/off-session spread caps separately from normal active sessions.

Rejection/error code needed:

- `SESSION_SPREAD_LIMIT`
- `SPREAD_WIDENING_LIMIT`
- `SESSION_SPREAD_DATA_UNAVAILABLE`

Tests required:

- London session uses London spread cap.
- New York session uses New York spread cap.
- Rollover/off-session uses stricter cap or no-trade rule.
- Spread above widening multiple rejects.
- Missing session data blocks live trade when session-aware spread protection is enabled.

Changes strategy logic:

- No.

## 8. OrderCheck Validation Before Live Order Send

Current status:

- Missing.
- `MT5_EA/TradingBotEA.mq5` uses `OrderCalcMargin` for margin estimate, but does not run `OrderCheck` before live open.
- `Trading/TradeExecution/TradeExecutionService.cs` sends approved orders directly to `MT5Bridge.OpenTradeAsync`.

Files likely to modify:

- `Domain/Models/Models.cs`
- `Infrastructure/MT5/MT5Bridge.cs`
- `MT5_EA/TradingBotEA.mq5`
- `Trading/TradeExecution/TradeExecutionService.cs`
- `Application/Workflows/AutoBotService.cs`
- `Tests/ForexBot.Tests/Program.cs`

Exact behavior required:

- Add bridge command for broker-side order precheck.
- EA should build an `MqlTradeRequest`, call `OrderCheck`, and return retcode, margin, fee/cost fields if available, normalized price/volume, and reason.
- Live execution must call OrderCheck after risk/user approval but before `OPEN_TRADE`.
- Fail closed if OrderCheck is unavailable or rejected.

Rejection/error code needed:

- `ORDERCHECK_FAILED`
- `ORDERCHECK_UNAVAILABLE`
- `ORDERCHECK_REJECTED`

Tests required:

- Approved risk/user trade still blocks when OrderCheck rejects.
- Missing OrderCheck response blocks live trade.
- OrderCheck success allows broker send.
- OrderCheck retcode/reason is surfaced in `TradeResult`.
- Paper mode does not require broker OrderCheck.

Changes strategy logic:

- No.

## 9. Order Rejection Handling And Retry Policy

Current status:

- Partial.
- `Application/Workflows/AutoBotService.cs` has retry support, but only a small set of rejection codes are non-retry.
- Current P0 safety codes can be retried from JSON workflow even though they are deterministic safety blocks.
- `MT5_EA/TradingBotEA.mq5` returns MT5 retcodes, but C# does not classify transient vs permanent broker rejects.

Files likely to modify:

- `Application/Workflows/AutoBotService.cs`
- `Trading/TradeExecution/TradeExecutionService.cs`
- `Infrastructure/MT5/MT5Bridge.cs`
- `MT5_EA/TradingBotEA.mq5`
- `Domain/Models/Models.cs`
- `Tests/ForexBot.Tests/Program.cs`

Exact behavior required:

- Centralize retry classification.
- Never retry deterministic safety failures: P0 codes, validation, news, risk, margin, exposure, kill switch, stop/freeze/lot/session/spread limits.
- Retry only transient transport or broker conditions, with capped attempts and delay.
- Persist and log final broker retcode/reason.
- Do not widen strategy parameters during retry.

Rejection/error code needed:

- `ORDER_REJECTED_FINAL`
- `ORDER_RETRY_EXHAUSTED`
- `BROKER_REJECTED_PERMANENT`
- `BROKER_REJECTED_TRANSIENT`

Tests required:

- Safety rejection is not retried.
- Permanent broker rejection is not retried.
- Transient broker rejection retries up to configured count.
- Retry exhaustion returns final failure code and retcode.
- Successful retry records one final success and no duplicate processed signal.

Changes strategy logic:

- No.

## 10. Backtest / Live Execution Mismatch

Current status:

- Missing.
- `Trading/Backtesting/BacktestingService.cs` calculates results from entry/exit only.
- No variable spread, commission, slippage, stop/freeze level, lot step, OrderCheck rejection, latency, or realistic fill model.
- `Trading/Backtesting/IBacktestDataLoader.cs` derives historical trades from closed results or CSV, not tick/intrabar execution conditions.

Files likely to modify:

- `Domain/Models/WorkflowModels.cs`
- `Domain/Models/Models.cs`
- `Trading/Backtesting/BacktestingService.cs`
- `Trading/Backtesting/IBacktestingService.cs`
- `Trading/Backtesting/IBacktestDataLoader.cs`
- `Application/Workflows/PerformanceData.cs`
- `UI/Forms/MainForm.Backtest.cs`
- `Tests/ForexBot.Tests/Program.cs`

Exact behavior required:

- Add a backtest execution model that can apply spread, commission, slippage, lot step, stop-level, order rejection, and fill assumptions.
- Backtest output must clearly state which assumptions were used.
- Backtest should not claim live realism unless all required assumptions are present.
- Compare live execution logs against backtest assumptions and flag mismatches.

Rejection/error code needed:

- `BACKTEST_EXECUTION_MODEL_MISSING`
- `BACKTEST_LIVE_MISMATCH`
- `BACKTEST_DATA_UNAVAILABLE`

Tests required:

- Backtest result includes execution assumptions.
- Backtest with spread/commission/slippage differs from ideal fill backtest.
- Lot step and stop-level assumptions can reject simulated trades.
- Missing cost/fill assumptions produce a warning or fail-closed mode, depending config.
- Live trade log fields can be compared to configured backtest assumptions.

Changes strategy logic:

- No.

## Recommended P1 Implementation Order

1. Lot min/max/step validation using live symbol data.
2. Broker minimum stop-level validation.
3. Broker freeze-level validation for modifications.
4. OrderCheck validation before live order send.
5. Order rejection handling and retry classification.
6. Slippage model.
7. Commission model.
8. Rollover/no-trade window.
9. Spread-widening protection by session.
10. Backtest/live execution mismatch reporting.

Reason for order:

- First fix broker constraints that can cause immediate live rejection.
- Then harden pre-send broker validation and retry behavior.
- Then improve cost/fill realism and backtest alignment.
