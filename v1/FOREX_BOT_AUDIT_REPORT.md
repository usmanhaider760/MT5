# Forex Bot Audit Report

Audit date: 2026-05-02
Scope: Trading logic, risk logic, execution logic, MT5 connector, config, backtesting, logs/persistence, and safety controls.
Mode: Documentation-only audit. Source code was not changed for this audit.

## Executive Summary

Audit-ready: Partially audit-ready for code review, not audit-ready for profitability claims.

Live-ready: Not live-ready for real-money trading until the P0/P1 safety gaps below are fixed and tested on demo/paper trading.

Biggest profit blockers:

- Strategy edge is not proven from code. `Trading/StrategyEngine/StrategyEngine.cs` selects the best scanned pair but returns `SignalDirection.Hold`; final direction depends on AI/user/scalping confirmation.
- Backtesting is performance-summary based, not market-simulation based. It does not verify tick data, variable spread, commission, slippage, intrabar SL/TP behavior, rejected orders, latency, out-of-sample testing, walk-forward testing, or Monte Carlo stress testing.
- AI confirmation and prompt quality may help filtering, but no verified statistical edge exists in the repository.

Biggest account-blowup risks:

- No verified max daily loss or max weekly loss hard stop.
- Margin protection is weak: current code checks free margin roughly but does not verify projected margin level or MT5 `OrderCheck` / `OrderCalcMargin`.
- Per-symbol exposure limits are missing; only total portfolio risk and max concurrent positions are checked.
- Some UI/AI fallback paths can call `MT5Bridge.OpenTradeAsync` directly when `_bot` is null.
- Emergency drawdown close exists but does not persist kill-switch state and does not verify/retry every close result robustly.

## Findings Table

| Area | Finding | Severity | Evidence from code | Why it matters | Recommended fix |
|---|---|---:|---|---|---|
| Strategy edge | Base strategy does not generate a Buy/Sell direction; it selects a candidate and returns Hold. | High | `Trading/StrategyEngine/StrategyEngine.cs:17-23` chooses candidate by score/spread; `StrategyEngine.cs:42-52` returns `Direction = SignalDirection.Hold`. | No deterministic entry edge is proven in the base strategy. | Keep strategy unchanged for now; first harden execution/risk, then add measurable strategy tests. |
| Strategy edge | AI decision can become the final directional filter. | Medium | `Application/SignalDecision/SignalDecisionService.cs:25-31` blocks AI Hold/low confidence; `SignalDecisionService.cs:45-49` uses AI SL/TP/reason. | AI output is not a validated trading edge by itself. | Require paper/demo result tracking and reject live mode until strategy metrics are verified. |
| Strategy edge | Auto-scalping has rule filters, but edge is not statistically verified. | Medium | `Trading/Scalping/ScalpingSessionService.cs:139-143` spread filter; `ScalpingSessionService.cs:165-171` open-scalp/pyramiding check; `ScalpingSessionService.cs:437-449` indicator scoring. | Rule filters reduce bad entries but do not prove expectancy. | Add paper-trade performance gates before enabling live auto-scalping. |
| Execution | Main bot execution path runs risk validation before MT5 order send. | Low | `Application/Workflows/AutoBotService.cs:724-748` delegates to `RiskManager`; `AutoBotService.cs:815-819` sends only after risk/news checks. | This is the correct primary safety flow. | Preserve this path and route all order entry through it. |
| Execution | Dedicated execution service enforces risk approval and user approval, but it is not clearly used everywhere. | High | `Trading/TradeExecution/TradeExecutionService.cs:25-30` blocks risk/user denial; `TradeExecutionService.cs:48` calls `_bridge.OpenTradeAsync`. Direct calls also exist at `UI/Forms/MainForm.cs:194`, `MainForm.cs:356`, `MainForm.cs:375`. | Any direct bridge fallback can bypass central controls if `_bot` is null. | Remove or block direct live fallback calls; require one central execution gate for all live orders. |
| Execution | MT5 EA validates SL/TP are non-zero but does not show broker stop-distance/order-check validation. | High | `MT5_EA/TradingBotEA.mq5:319-320` rejects zero SL/TP; searches found no verified `OrderCheck` or `OrderCalcMargin`. | Broker may reject orders after app approval, or stops may be too close for symbol rules. | Add EA-side `OrderCheck`, `OrderCalcMargin`, symbol stop-level/freeze-level validation. |
| Execution | Slippage is configured in EA and monitored after fill in C#; extreme slippage closes position. | Medium | `MT5_EA/TradingBotEA.mq5:19` `InpSlippage`; `TradingBotEA.mq5:34` `SetDeviationInPoints`; `Application/Workflows/AutoBotService.cs:828-858` warning/close logic. | Good protection, but it occurs after the fill and close may fail. | Verify close result, retry on failure, and persist a risk event. |
| Execution | Spread is checked before entry. | Low | `Trading/RiskManagement/RiskManager.cs:173-185` blocks spread above max; `Application/Workflows/AutoBotService.cs:139-143` waits in scalping when spread is high. | Reduces high-cost entries. | Keep centralized in risk validation and apply to all live paths. |
| Execution | Commission handling is not verified. | Medium | No verified commission model in `RiskManager`, `AutoBotService`, `BacktestingService`, or EA order path. | R:R and backtest profitability can be overstated. | Add configurable commission per lot/side and include it in live previews and backtests. |
| Execution | Rollover/spread-widening protection is not verified. | Medium | News blackout exists, but no verified rollover/session spread-widening hard block was found. | Rollover can cause spread spikes and stop-outs. | Add configurable no-trade windows around rollover and illiquid sessions. |
| Risk | Risk-per-trade lot formula is present. | Low | `Domain/Common/LotCalculator.cs:6` formula; `LotCalculator.cs:25` risk amount; `LotCalculator.cs:37` lot formula; `Trading/RiskManagement/RiskManager.cs:81-89` uses it. | This is a core protection. | Keep centralized; validate symbol pip value/contract size against broker data. |
| Risk | Max lot size is hardcoded, not broker-derived. | High | `Domain/Common/LotCalculator.cs:40-41` clamps to `0.01` and `100.0`; EA defaults lots under `0.01` to `0.01` at `MT5_EA/TradingBotEA.mq5:290`. | Hardcoded max may exceed broker or account-safe lot size. | Use live symbol min/max/step and reject unsafe lot sizes. |
| Risk | Max daily loss is missing as a hard loss stop. | Critical | `Domain/Models/Models.cs:240` has `MaxTradesPerDay`; `Application/Workflows/AutoBotService.cs:703-705` limits trade count, not daily loss. | Bot can continue after a large daily realized loss. | Add daily realized+floating P/L hard stop before every trade. |
| Risk | Max weekly loss is missing. | Critical | No verified `MaxWeeklyLoss` setting or weekly-loss guard found. | Bot can continue trading through a bad week. | Add weekly realized+floating P/L guard from MT5 history/SQLite. |
| Risk | Drawdown emergency close exists but is partial. | High | `Domain/Models/Models.cs:280` `EmergencyCloseDrawdownPct`; `AutoBotService.cs:1095-1117` closes positions on threshold. | If account fetch or close fails, protection may not complete. | Persist kill switch, fail closed on missing account data, retry/verify closes. |
| Risk | Consecutive-loss pause exists but depends on detected closed trades. | Medium | `Domain/Models/Models.cs:325` `MaxConsecutiveLosses`; `Infrastructure/Monitoring/EdgeHealthMonitor.cs:56`; `Application/Workflows/AutoBotService.cs:1045-1054`. | Missed close logging can delay the stop. | Add persisted pre-trade consecutive-loss check from DB/MT5 history. |
| Risk | Max concurrent positions is present. | Low | `Domain/Models/Models.cs:247` default `MaxConcurrentPositions = 3`; `Trading/RiskManagement/RiskManager.cs:49-57`; `AutoBotService.cs:735-741`. | Limits open-trade count. | Keep one authoritative central check. |
| Risk | Max exposure by symbol is missing. | Critical | `RiskManager.cs:155-164` checks total portfolio risk only; no verified per-symbol cap. | One pair can accumulate excessive gross risk. | Add `MaxSymbolRiskPercent`, `MaxSymbolLots`, and same-symbol duplicate/hedge rules. |
| Risk | Correlation control exists but is partial. | Medium | `Domain/Models/Models.cs:313` `CorrelationCheckEnabled`; `Application/Workflows/AutoBotService.cs:769-781`; `Domain/Common/CorrelationGroups.cs:8`. | Static groups may miss real exposure and are outside `RiskManager`. | Move into risk validation and make correlation matrix configurable. |
| Risk | Margin protection is weak. | Critical | `Domain/Models/Models.cs:213-214` has margin fields; `RiskManager.cs:65` only checks `FreeMargin < Balance * 0.05`; EA snapshot sets `margin_required` to `0.00` at `MT5_EA/TradingBotEA.mq5:1048`. | A trade can pass risk but leave account near margin call. | Add projected margin-level validation and EA `OrderCalcMargin`/`OrderCheck`. |
| Risk | No-stop-loss trades are blocked. | Low | `Domain/Models/Models.cs:114-117` rejects zero SL/TP; `MT5_EA/TradingBotEA.mq5:319-320` rejects zero SL/TP. | Good baseline account protection. | Preserve this in every path. |
| Risk | Moving stop-loss farther away was not verified in management code. | Low | `AutoBotService.cs:988-989` trailing stop only improves position; `AutoBotService.cs:931-932` moves SL to breakeven. | Good protection against increasing risk after entry. | Keep broker-side validation so modify requests cannot widen risk. |
| Risk | Martingale/grid/recovery behavior was not verified. | Low | Search found no verified martingale/grid/recovery lot increase logic; `Domain/Models/Models.cs:376-377` has `AllowPyramiding = false` default. | Hidden lot escalation is a blow-up risk, but not found here. | Keep explicit tests to prevent lot increase after loss and grid/averaging features. |
| Backtesting | Backtest summarizes closed trades; it does not simulate market data. | High | `Trading/Backtesting/IBacktestDataLoader.cs:14-50` loads closed DB trades; `IBacktestDataLoader.cs:56-133` loads CSV trades; `BacktestingService.cs:35-41` calculates P/L from entry/exit. | It cannot prove strategy behavior under realistic live conditions. | Build a market-data simulator before trusting backtest results. |
| Backtesting | Variable spread, commission, slippage, realistic fills, latency, rejected orders, broker min stops are missing/unclear. | High | `Trading/Backtesting/BacktestingService.cs:35-41` computes pips/USD directly; no spread/commission/slippage/order-rejection model found. | Results can be materially overstated. | Add execution-cost model and rejected-order model matching live execution. |
| Backtesting | Tick data and intrabar SL/TP behavior are missing/unclear. | High | `BacktestingService.cs:102-110` uses final entry/exit prices only; no tick/candle walk or SL/TP intrabar resolution found. | SL/TP sequencing can be wrong, especially for scalping. | Use tick data or at least OHLC intrabar rules with conservative fill ordering. |
| Backtesting | Out-of-sample, walk-forward, and Monte Carlo testing are not verified. | Medium | No verified OOS/walk-forward/Monte Carlo code found in `Trading/Backtesting`. | Profitability may be overfit or sample-specific. | Add OOS splits, walk-forward windows, and Monte Carlo trade-sequence stress tests. |
| Live readiness | Paper trading exists. | Low | `Application/Workflows/AutoBotService.cs:215` `SimulatePaperTrade`; `AutoBotService.cs:817-819` uses paper/live branch. | Good for validation before live trading. | Require minimum paper-trade sample before enabling live mode. |
| Live readiness | Trade persistence exists. | Low | `Infrastructure/Persistence/SqliteTradeRepository.cs:24-73` inserts trades; `SqliteTradeRepository.cs:116-146` updates close P/L. | Supports audit trail and performance review. | Use DB history for daily/weekly/consecutive-loss controls. |
| Live readiness | Logging exists but some catch blocks hide safety failures. | Medium | `Application/Workflows/AutoBotService.cs:895`, `952`, `1024`, `1101` catch and return silently; `Infrastructure/MT5/MT5Bridge.cs:42` catches ping failure. | Silent failures can disable monitoring or emergency checks. | Log every safety-related catch and fail closed where account safety depends on the result. |
| Live readiness | News filter exists but can be unavailable/disabled. | Medium | `Infrastructure/News/FmpNewsCalendarService.cs:24-35` returns unavailable for disabled/missing provider; `AutoBotService.cs:787-807` decides block vs warning by config. | Trading through news may occur depending on config. | In live mode, default to blocking when news is unavailable unless explicitly overridden. |
| Live readiness | No automated test project was found. | High | `rg --files -g "*Test*" -g "*Tests*"` returned only `MT5TradingBot.csproj`; no xUnit/NUnit/MSTest usage found. | Safety changes can regress without tests. | Add focused unit tests for risk validation, lot sizing, execution gate, and backtest assumptions. |

## Backtest Assumption Matrix

| Item | Status | Evidence |
|---|---|---|
| Variable spread | Missing | No spread model in `Trading/Backtesting/BacktestingService.cs`. |
| Commission | Missing | No commission input/model found in backtesting service. |
| Slippage | Missing | Live slippage exists in `AutoBotService.cs:828-858`; backtest has no equivalent. |
| Realistic fills | Missing | Backtest uses stored/CSV entry and exit prices directly. |
| Tick data | Missing | No tick-data loader found. |
| Candle data | Unclear | Backtest loaders consume DB/CSV trades, not OHLC candles. |
| Intrabar SL/TP behavior | Missing | No intrabar order of SL/TP events found. |
| Broker minimum stop distance | Missing | No backtest stop-level validation found. |
| Rejected orders | Missing | Backtest has no rejected-order simulation. |
| Latency | Missing | No latency simulation found. |
| Out-of-sample testing | Missing | No OOS split found. |
| Walk-forward testing | Missing | No walk-forward framework found. |
| Monte Carlo stress testing | Missing | No Monte Carlo framework found. |

## Risk-Control Matrix

| Control | Status | Evidence |
|---|---|---|
| Risk per trade formula | Present | `Domain/Common/LotCalculator.cs:25-37`; `RiskManager.cs:81-89`. |
| Max lot size | Present but weak | Hardcoded `100.0` cap at `LotCalculator.cs:41`. |
| Max daily loss | Missing | Only max trades/day found at `AutoBotService.cs:703-705`. |
| Max weekly loss | Missing | No verified setting/check found. |
| Max drawdown stop | Present but partial | `AutoBotService.cs:1095-1117`. |
| Max consecutive loss stop | Present but partial | `EdgeHealthMonitor.cs:56`; `AutoBotService.cs:1045-1054`. |
| Max open trades | Present | `RiskManager.cs:49-57`; `AutoBotService.cs:735-741`. |
| Max exposure by symbol | Missing | Total portfolio risk only at `RiskManager.cs:155-164`. |
| Correlation control | Present but partial | `AutoBotService.cs:769-781`; `CorrelationGroups.cs:8`. |
| Margin level protection | Weak | `RiskManager.cs:65`; EA `margin_required` is `0.00` at `TradingBotEA.mq5:1048`. |
| Emergency kill switch | Present but partial | `_emergencyStopFired` at `AutoBotService.cs:672-674`; close-all at `AutoBotService.cs:1107-1117`. |

## Missing Information

- Verified profitable live results.
- Verified profitable paper/demo results over a meaningful sample.
- Broker symbol contract details used by the live account.
- Exact broker minimum stop/freeze levels enforced before orders.
- Commission schedule.
- VPS latency and execution environment assumptions.
- Tick or OHLC historical data source for strategy simulation.
- Out-of-sample, walk-forward, and Monte Carlo reports.
- Unit/integration test suite for risk and execution safety.

## Questions for Owner

1. Which broker/account type is the bot intended to trade live on?
2. What maximum real-money risk limits should be enforced: daily loss, weekly loss, max symbol exposure, and minimum margin level?
3. Should live mode fail closed when news data is unavailable?
4. Is auto-scalping allowed live, or should it remain paper-only until performance is proven?
5. What minimum paper/demo sample size is required before any live trading?

## Suggested Next Steps

1. Account safety: centralize all live order paths behind one execution gate, add persisted kill switch, add daily/weekly loss stops, add projected margin validation, add broker lot min/max/step validation, add per-symbol exposure cap.
2. Execution realism: add EA `OrderCheck` / `OrderCalcMargin`, broker stop-level/freeze-level checks, commission model, rollover no-trade window, and robust slippage close retry/verification.
3. Backtest validity: replace trade-summary backtest with market-data simulation that models spread, commission, slippage, latency, rejected orders, and intrabar SL/TP behavior.
4. Strategy improvement: only after safety/execution/backtest fixes, measure deterministic strategy rules separately from AI confirmation and require paper/demo performance evidence before live trading.

