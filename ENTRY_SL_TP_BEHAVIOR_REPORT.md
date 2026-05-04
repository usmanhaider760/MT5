# Entry / SL / TP Behavior Report

Scope: reporting only. No strategy, indicator, AI prompt, SL/TP, or live-trading behavior was changed.

Source reviewed: `STRATEGY_EXTRACTION_REPORT.md` and current source files under `Trading/`, `Application/`, `Infrastructure/AI/`, `Domain/`, and focused `UI/Forms/MainForm.cs` paths.

## Entry Path Table

| Path | Creates BUY/SELL? | SL/TP source | Execution path | Evidence |
|---|---:|---|---|---|
| Base strategy scan | No, mostly HOLD | Provisional formula only | Not directly executable as BUY/SELL | `Trading/StrategyEngine/StrategyEngine.cs` / `StrategyEngine.CreateInitialSignalAsync` sets `Direction = SignalDirection.Hold` after selecting the best scanner pair. |
| Auto scalping session | Yes | Configured/scalping decision pip distances around live bid/ask | `ScalpingSessionService.RunAsync` -> request delegate -> `AutoBotService.ExecuteTradeWithValidationAsync` | `Trading/Scalping/ScalpingSessionService.cs` / `ResolveScalpingDecision`, `ResolvePriceMovementDecision`, `BuildRequest`. |
| JSON watch folder, full-auto mode | Yes, if JSON has valid `TradeType` | JSON absolute price fields | `AutoBotService.ProcessSignalFileAsync` -> `ExecuteWithRetryAsync` -> central gate | `Application/Workflows/AutoBotService.cs` deserializes `TradeRequest`; manual mode queues a card, non-manual executes through validation. |
| JSON UI text box | Yes, if JSON has valid `TradeType` | JSON absolute price fields | `MainForm.ExecuteJsonAsync` -> `ExecuteThroughCentralGateAsync` -> central gate | `UI/Forms/MainForm.cs` / `ExecuteJsonAsync`, `ExecuteThroughCentralGateAsync`. |
| Manual UI form | Yes | User-entered absolute price fields | `MainForm.ExecuteManualAsync` -> `ExecuteThroughCentralGateAsync` -> central gate | `UI/Forms/MainForm.cs` builds `TradeRequest` from selected direction, SL, TP, TP2, lot. |
| Signal card review | Yes, after user approval | Original JSON or review `FinalRequest` override | `ExecuteSignalFromCardSafeAsync` -> central gate, or starts scalping | `UI/Forms/MainForm.cs` / `ShowTradeReviewDialogAsync`, `ExecuteSignalFromCardSafeAsync`. |
| Pair analysis row/detail | Yes, if row has AI trade data or review supplies final levels | AI row absolute levels, manual review values, or review final request | `OpenPairAnalysisDetailAsync` -> central gate, or starts scalping | `UI/Forms/MainForm.cs` / `RunDecisionAnalysisForPairAsync`, `OpenPairAnalysisDetailAsync`. |
| Claude signal service | Yes, when AI JSON action is `TRADE` | AI JSON absolute price fields | `ClaudeSignalService.ParseAndExecuteAsync` -> injected execute delegate | `Infrastructure/AI/ClaudeSignalService.cs` rejects `NO_TRADE` and zero SL/TP, then builds `TradeRequest`. |
| SignalDecisionService AI confirmation | Can convert a strategy HOLD/provisional signal into BUY/SELL only if supplied AI says BUY/SELL | AI analysis absolute SL/TP | Produces `MarketSignal`; execution is elsewhere | `Application/SignalDecision/SignalDecisionService.cs` requires approved risk, AI direction not HOLD, confidence >= 70, pair match. |

## SL/TP Logic Table

| Source | SL calculation | TP calculation | Fixed / ATR / score / AI / config | Varies by symbol/session/volatility/confidence? |
|---|---|---|---|---|
| `StrategyEngine.CreateInitialSignalAsync` | `mid - max(15 pips, spread * 3)` rounded to symbol digits | `mid + stopDistance * max(config.MinRRRatio, 1.5)` rounded to symbol digits | Spread-aware plus config R:R; not ATR, score, or AI | Symbol pip size and symbol digits vary; spread varies. Direction remains HOLD, so levels are provisional. |
| `ScalpingSessionService.BuildRequest` BUY | `ask - slPips * pipSize` | `ask + tpPips * pipSize` | Config pip distances, optionally `ScalpingDecision.SuggestedSlPips/TpPips` if set | Actual code currently does not set suggested pips in `EvaluateSnapshot`; effective values are config. Pip size varies by symbol. |
| `ScalpingSessionService.BuildRequest` SELL | `bid + slPips * pipSize` | `bid - tpPips * pipSize` | Config pip distances, optionally decision-suggested pips if present | Same as BUY. Rounded to 5 decimals regardless of symbol digits. |
| UI suggested scalping config | `StopLossPips = max(pair min SL, spread * 2, pair ATR floor)` clamped to pair max SL, rounded half pip | `TakeProfitPips = max(pair min TP, slPips * preferredRR, spread * 4)` capped 500, rounded half pip | Config suggestion uses pair settings, live spread, preferred R:R, and pair ATR floor setting | Varies by symbol pair settings and live spread. This creates config values, not direct trade SL/TP. |
| Manual UI | User-entered SL absolute price | User-entered TP absolute price | Manual | Varies only by user input. |
| JSON / watch folder | JSON `stop_loss` absolute price | JSON `take_profit` absolute price; optional `take_profit_2` logged but one-click execution uses TP1 | External JSON | Varies by signal provider. |
| Claude AI signal | AI JSON `stop_loss` absolute price | AI JSON `take_profit` and optional `take_profit_2` | AI-provided | Varies by AI output; service only rejects missing/zero SL/TP. |
| Pair-analysis AI row | AI JSON `stop_loss` absolute price | AI JSON `take_profit` absolute price | AI-provided | Varies by AI output. |
| `SignalDecisionService` | `aiAnalysis.StopLoss` | `aiAnalysis.TakeProfit` | AI analysis result | Varies by AI analysis; built-in skeleton AI does not create BUY/SELL. |

## Code Evidence

| Question | Current behavior | Evidence |
|---|---|---|
| Automatic BUY/SELL code path | Auto scalping is the deterministic automatic BUY/SELL creator. It evaluates direction, builds a `TradeRequest`, then calls the supplied execution delegate. JSON full-auto can also execute a submitted BUY/SELL request automatically when bot mode is not manual approval. | `Trading/Scalping/ScalpingSessionService.cs` / `RunAsync`, `ResolveScalpingDecision`, `BuildRequest`; `Application/Workflows/AutoBotService.cs` / signal-file processing and `ExecuteTradeWithValidationAsync`. |
| Base `StrategyEngine` BUY/SELL | Base strategy selects a pair and provisional levels, but returns HOLD. | `Trading/StrategyEngine/StrategyEngine.cs` / `CreateInitialSignalAsync`: `Direction = SignalDirection.Hold`. |
| AutoScalping BUY/SELL | Yes. Direction can be forced by `BuyOnly`/`SellOnly`, inherited from reviewed signal with `SignalDirection`, or auto-selected by scoring BUY and SELL. If no snapshot exists, price movement can choose direction. | `Trading/Scalping/ScalpingSessionService.cs` / `ResolveProbeDirection`, `ResolveScalpingDecision`, `ResolvePriceMovementDecision`. |
| AI creates/overrides BUY/SELL | Built-in `AiAnalysisService` does not; it always returns HOLD. `SignalDecisionService` can use a supplied AI BUY/SELL with confidence >= 70. `ClaudeSignalService` and pair-analysis UI can create requests from AI JSON. `BuildSignalFromAiDecision` can override direction and SL/TP from AI JSON when used. Scalping AI confirmation only approves/rejects an existing rule-filtered opportunity. | `Infrastructure/AI/AiAnalysisService.cs`; `Application/SignalDecision/SignalDecisionService.cs`; `Infrastructure/AI/ClaudeSignalService.cs`; `UI/Forms/MainForm.cs` / `BuildSignalFromAiDecision`, `RunDecisionAnalysisForPairAsync`, `StartAutoScalpingFromReviewAsync`. |
| Manual/JSON creates trades | Yes. Manual form and JSON text box create `TradeRequest`s and route through `ExecuteThroughCentralGateAsync`. Watch-folder JSON can execute in non-manual mode; manual mode queues a card for user approval. | `UI/Forms/MainForm.cs` / `ExecuteManualAsync`, `ExecuteJsonAsync`; `Application/Workflows/AutoBotService.cs` / signal file processing. |
| Lot size calculation | If `BotConfig.AutoLotCalculation` is true, `RiskManager.ValidateAsync` calls `LotCalculator.Calculate(equity, MaxRiskPercent, referenceEntry, StopLoss, Pair)`. Otherwise request lot is preserved. `AutoBotService` applies `riskResult.ValidatedLotSize` before execution. UI review also displays/calculates estimates with `LotCalculator`. | `Trading/RiskManagement/RiskManager.cs`; `Domain/Common/LotCalculator.cs`; `Application/Workflows/AutoBotService.cs`. |
| Broker stop/freeze validation | Live mode validates generated/requested SL/TP after symbol data and live price are fetched, before risk validation and before trade execution. Broker `OrderCheck` runs again inside `TradeExecutionService` before `MT5Bridge.OpenTradeAsync`. | `Application/Workflows/AutoBotService.cs` / `CheckBrokerStopLevel`, `CheckBrokerFreezeLevel`; `Domain/Common/BrokerStopLevelValidator.cs`; `Domain/Common/BrokerFreezeLevelValidator.cs`; `Trading/TradeExecution/TradeExecutionService.cs`. |
| Can any path trade without valid SL/TP? | Intended executable paths cannot send zero SL/TP because `TradeRequest.Validate()` rejects zero and directional inconsistencies. Live broker stop/freeze validation also rejects unavailable SL/TP data. Pair-analysis detail can open review with missing levels, but central validation should reject if user approves without fixing. | `Domain/Models/Models.cs` / `TradeRequest.Validate`; `AutoBotService.ExecuteTradeWithValidationCoreAsync`; `TradeExecutionService.ExecuteAsync`. |

## Exact SL/TP Details

- Base strategy: `mid = (Ask + Bid) / 2`; `pipSize` is `0.01` for JPY, `0.1` for XAU/GOLD, `0.01` for XAG, `1.0` for BTC/ETH, else `0.0001`. Stop distance is `max(15 * pipSize, SpreadPips * 3 * pipSize)`. TP distance is stop distance times `max(config.MinRRRatio, 1.5)`. SL is always below mid and TP above mid because base signal stays HOLD rather than directional SELL.
- Scalping: market entry reference is ask for BUY and bid for SELL; actual `EntryPrice` is set to `0` for market execution. SL/TP are fixed pip distances from current bid/ask using `LotCalculator.GetPipSize(pair)`, rounded to 5 decimals.
- AI/manual/JSON: SL/TP are absolute prices supplied by the user, JSON producer, or AI JSON. The central gate validates them but does not recalculate them.
- TP2: `AutoBotService` logs `TakeProfit2` but one-click broker execution sends one trade using `TakeProfit`.

## Risks Or Unclear Areas

- `ScalpingDecision` supports `SuggestedSlPips` and `SuggestedTpPips`, but current snapshot scoring does not populate them, so the effective scalping SL/TP are config-based unless another future decision source sets those fields.
- `ScalpingSessionService.BuildRequest` rounds SL/TP to 5 decimals for all symbols. Broker validation may catch invalid distances, but symbol-specific digit rounding should be covered by tests for JPY/metals/crypto.
- `ResolvePriceMovementDecision` approves the first no-snapshot tick with reason "waiting for one more price update" but `Approved = true`; with no snapshot this can allow a trade before a real previous-mid comparison.
- Base strategy creates BUY-shaped provisional levels even though direction is HOLD; if future code treats those levels as directional without AI/user confirmation, SELL semantics would be wrong.
- UI suggested scalping config uses pair ATR floor settings, spread, and pair rules, but the executed trade only sees the resulting config pips; tests should distinguish config suggestion behavior from execution behavior.
- Pair-analysis detail can enter review with `sl = 0` or `tp = 0`; this should remain blocked by `TradeRequest.Validate()` if not fixed before execution.

## Recommendations For Scalping-Specific SL/TP Testing

- Test BUY scalping request generation: ask-based entry reference, `SL = ask - StopLossPips * pip`, `TP = ask + TakeProfitPips * pip`, market `EntryPrice = 0`.
- Test SELL scalping request generation: bid-based entry reference, `SL = bid + StopLossPips * pip`, `TP = bid - TakeProfitPips * pip`.
- Test per-symbol pip sizes and rounding for EURUSD, USDJPY, XAUUSD/GOLD, XAGUSD, and one crypto symbol if supported.
- Test pair-specific suggested scalping config separately: min/max SL, min TP, preferred R:R, live spread, and ATR floor should produce expected `StopLossPips`/`TakeProfitPips`.
- Test broker stop/freeze rejection after scalping SL/TP generation for too-close SL and too-close TP.
- Test no-snapshot fallback behavior explicitly, especially the first-tick approval case.
- Test AI confirmation in scalping proves AI can only approve/reject the selected direction and cannot change SL/TP or direction in `ScalpingSessionService`.
