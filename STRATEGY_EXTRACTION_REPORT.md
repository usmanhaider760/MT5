# Strategy Extraction Report

Scope: reporting/extraction only. No entry rules, indicators, AI prompts, take-profit logic, or live trading behavior are changed by this report generator.

## Strategy Flow Diagram

```text
Pair scanner -> StrategyEngine.CreateInitialSignalAsync -> base MarketSignal
  -> Base strategy selects candidate pair and provisional levels
  -> Base strategy direction remains HOLD
  -> AI/manual/pair-review/scalping path supplies or confirms BUY/SELL
  -> User/manual approval or workflow approval
  -> AutoBotService.ExecuteTradeWithValidationAsync safety gate
  -> Risk/news/margin/exposure/broker checks
  -> TradeExecutionService -> MT5 only after approval and OrderCheck
```

## Base Strategy Direction Verdict

The base deterministic strategy currently produces mostly HOLD. `StrategyEngine.CreateInitialSignalAsync` selects the best available scanner pair and creates provisional entry/SL/TP levels, but sets `Direction = SignalDirection.Hold`. Deterministic Buy/Sell decisions are produced in the auto-scalping layer or supplied by AI/manual signal paths, then constrained by user approval and risk/execution safety.

## Deterministic Rule Logic

- Pair selection: highest available scanner score, lower spread as tie-breaker.
- Base strategy direction: Hold.
- Base provisional entry: mid price from bid/ask.
- Base provisional stop loss: mid minus max(15 pips, spread * 3).
- Base provisional take profit: stop distance multiplied by max(configured minimum R:R, 1.5).
- Auto-scalping direction: configured BuyOnly/SellOnly/SignalDirection, or Auto chooses the passing stronger side.
- Auto-scalping confirmation: snapshot score must meet configured minimum and hard blockers must not fire.
- Auto-scalping fallback: price movement can be used when snapshot is unavailable.

## AI-Assisted Logic Boundary

- `AiAnalysisService` skeleton does not approve Buy/Sell; it returns Hold and a confidence/risk annotation.
- `SignalDecisionService` can convert a strategy signal into a trade-ready signal only when AI direction is Buy/Sell, confidence is at least 70, risk is not blocked, and pair matches.
- `ClaudeSignalService` can create trade requests from AI JSON, but NO_TRADE/HOLD-like responses return without execution.
- Auto-scalping AI confirmation is optional and only confirms an already rule-filtered opportunity.

## User And Manual Approval Logic

- Manual signal cards stop at review until the user approves.
- Review can adjust lot size and final request fields before the execution gate.
- Live auto-scalping start asks for explicit user confirmation unless paper trading is enabled.

## Risk And Execution Safety Logic

- Safety gates are not strategy edge rules. They can block trades after a signal exists.
- The central gate validates account, symbol/spread, broker rules, daily/weekly loss, exposure, risk, costs, margin, correlation, news, and kill switch state.
- Broker send is isolated in `TradeExecutionService` after request validation, risk approval, approval decision, and broker OrderCheck.

## Findings Table

| Status | Area | Summary |
|---|---|---|
| Verified | Deterministic base strategy | Selects the highest-score available scanner pair, tie-breaking by lower spread. Fetches symbol data, calculates mid price, sets market order, SL below mid, TP above mid, but returns Direction=Hold. |
| Verified | Base strategy Buy/Sell generation | The base strategy does not produce deterministic Buy/Sell. Its reason states direction remains HOLD until AI/user decision confirms setup. |
| Verified | Base strategy SL/TP generation | Uses stopDistance=max(15 pips, spread*3) and takeProfitDistance=stopDistance*max(MinRRRatio, 1.5). This creates provisional levels only because direction remains Hold. |
| Verified | Scalping deterministic preconditions | Auto scalping waits for MT5 connection, session time, max trade count, max loss/profit target, symbol data, max spread, news risk, pyramiding rules, and cooldown before evaluating a setup. |
| Verified | Scalping direction generation | DirectionMode can force BuyOnly/SellOnly, use the reviewed signal direction, or Auto-check both BUY and SELL and choose the only/stronger side that passes. |
| Verified | Scalping confirmation rules | Snapshot scoring checks M5/M15/H1 trend, M5 candle direction, M5/M15 MACD, M5 price vs EMA20/EMA50, RSI zone, stochastic guard, doji/inside-bar rejection, and room to support/resistance. Approved requires score >= MinDecisionScore. |
| Verified | Scalping fallback confirmation | If full snapshot is unavailable, price movement can approve a trade: Auto chooses BUY when current mid >= previous mid, SELL otherwise; fixed direction requires movement to agree. |
| Verified | Scalping SL/TP generation | Entry uses ask for BUY and bid for SELL. SL/TP are derived from configured or decision-suggested pip distances. EntryPrice is set to 0 for market execution. |
| Verified | Lot sizing source | When AutoLotCalculation is enabled, lot size is calculated from equity, MaxRiskPercent, reference entry, SL, and pair. Otherwise the request lot size is used. AutoBot applies ValidatedLotSize before execution. |
| Verified | AI analysis boundary | The skeleton AI analysis calculates baseline confidence but always returns Direction=Hold and states that real AI provider confirmation is required. |
| Verified | AI signal execution boundary | Claude JSON can produce NO_TRADE or TRADE. NO_TRADE updates context and returns. TRADE requires nonzero SL/TP, builds TradeRequest from AI fields, then calls the provided execution delegate. |
| Verified | Signal decision AI boundary | Risk must approve, AI must exist, AI risk cannot be Blocked, AI direction cannot be Hold, confidence must be at least 70, and pair must match. Final direction/SL/TP come from AI analysis. |
| Verified | User/manual approval boundary | Manual card execution requires the user review dialog to approve. Review can override lot size and final request before calling AutoBotService.ExecuteTradeWithValidationAsync. |
| Verified | Auto-scalping user approval boundary | Starting live auto scalping asks the user to confirm unless paper trading is enabled. The scalping session executes through AutoBotService.ExecuteTradeWithValidationAsync. |
| Verified | Risk/execution safety boundary | The central execution gate checks kill switch, no-trade windows, signal expiry, allowed pairs, account data, symbol/spread data, broker levels, positions, loss limits, exposure, risk validation, lot rules, costs, margin, correlation, news, then paper fill or trade execution. |
| Verified | Broker send boundary | Broker execution requires valid request, approved risk, approved workflow/user decision, accepted broker OrderCheck, then calls MT5Bridge.OpenTradeAsync. |
| Not verified | Historical reproducibility | This patch did not audit whether snapshot fields are calculated from closed candles only, whether support/resistance can repaint, or whether all fields are available historically. |
| Not verified | Complete deterministic exit management | SL/TP placement is documented, but full post-entry management such as break-even, trailing, manual close, and emergency close behavior is not fully proven as deterministic strategy exit logic in this report. |

## Code Evidence

| Status | File | Class/Method | Evidence |
|---|---|---|---|
| Verified | `Trading/StrategyEngine/StrategyEngine.cs` | `StrategyEngine.CreateInitialSignalAsync` | Required source fragments were found. |
| Verified | `Trading/StrategyEngine/StrategyEngine.cs` | `StrategyEngine.CreateInitialSignalAsync` | Required source fragments were found. |
| Verified | `Trading/StrategyEngine/StrategyEngine.cs` | `StrategyEngine.CreateInitialSignalAsync` | Required source fragments were found. |
| Verified | `Trading/Scalping/ScalpingSessionService.cs` | `ScalpingSessionService.RunAsync` | Required source fragments were found. |
| Verified | `Trading/Scalping/ScalpingSessionService.cs` | `ResolveScalpingDecision / ResolveProbeDirection` | Required source fragments were found. |
| Verified | `Trading/Scalping/ScalpingSessionService.cs` | `EvaluateSnapshot` | Required source fragments were found. |
| Verified | `Trading/Scalping/ScalpingSessionService.cs` | `ResolvePriceMovementDecision` | Required source fragments were found. |
| Verified | `Trading/Scalping/ScalpingSessionService.cs` | `BuildRequest` | Required source fragments were found. |
| Verified | `Trading/RiskManagement/RiskManager.cs` | `RiskManager.ValidateAsync` | Required source fragments were found. |
| Verified | `Infrastructure/AI/AiAnalysisService.cs` | `AiAnalysisService.AnalyzeAsync` | Required source fragments were found. |
| Verified | `Infrastructure/AI/ClaudeSignalService.cs` | `ClaudeSignalService.ParseAndExecuteAsync` | Required source fragments were found. |
| Verified | `Application/SignalDecision/SignalDecisionService.cs` | `SignalDecisionService.CreateDecisionAsync` | Required source fragments were found. |
| Verified | `UI/Forms/MainForm.cs` | `ExecuteSignalFromCardSafeAsync / ShowTradeReviewDialogAsync` | Required source fragments were found. |
| Verified | `UI/Forms/MainForm.cs` | `StartAutoScalpingFromReviewAsync` | Required source fragments were found. |
| Verified | `Application/Workflows/AutoBotService.cs` | `AutoBotService.ExecuteTradeWithValidationAsync` | Required source fragments were found. |
| Verified | `Trading/TradeExecution/TradeExecutionService.cs` | `TradeExecutionService.ExecuteAsync` | Required source fragments were found. |
| Not verified | `MT5_EA/TradingBotEA.mq5` | `GET_MARKET_SNAPSHOT / indicator calculation` | Not verified |
| Not verified | `Application/Workflows/AutoBotService.cs` | `HeartbeatLoopAsync / position management` | Not verified |

## Hold/No-Trade Behavior

- Base strategy returns Hold when no scanner pair is available, market data is unavailable, strategy creation throws, or after creating provisional levels pending AI/user confirmation.
- Signal decision returns Hold when risk blocks, AI is missing, AI blocks, AI says Hold, confidence is below 70, or pair mismatches.
- Claude path treats `NO_TRADE` as no execution and updates AI context.
- Pair analysis UI maps AI `NO_TRADE` to no trade and unknown responses to WAIT.
- Auto-scalping waits/no-trades on failed preconditions, insufficient confirmation score, unclear candle, RSI hard blockers, equal/failed auto sides, optional AI rejection, or safety gate rejection.

## Exit Conditions And SL/TP Generation

- Base strategy provisional SL/TP are generated from mid price, spread-aware stop distance, and configured minimum R:R.
- AI/manual JSON paths can supply final SL/TP directly.
- Auto-scalping builds SL/TP from configured pip distances or decision-suggested pip distances around current bid/ask.
- TakeProfit2 is logged by the execution gate but one-click execution opens one trade using TakeProfit.
- Not verified: full post-entry exit management, break-even movement, trailing stop behavior, manual closes, and emergency closes are outside this patch's deterministic strategy extraction proof.

## Areas Marked Not Verified

- Historical reproducibility: This patch did not audit whether snapshot fields are calculated from closed candles only, whether support/resistance can repaint, or whether all fields are available historically.
- Complete deterministic exit management: SL/TP placement is documented, but full post-entry management such as break-even, trailing, manual close, and emergency close behavior is not fully proven as deterministic strategy exit logic in this report.
- Future-data/repainting risk is not proven by this patch; it belongs to P3 Patch 3.
