# P2 Backtest Realism Plan

Sources:

- `BACKTEST_LIVE_EXECUTION_MISMATCH_REPORT.md`
- `P1_EXECUTION_REALISM_PLAN.md`

Scope: Upgrade the current trade-summary backtest into a realistic market-simulation backtest suitable for forex scalping validation.

Rules:

- Planning only.
- Do not rewrite the backtest engine yet.
- Do not optimize entries.
- Do not change indicators.
- Do not change AI prompts.
- Do not change take-profit strategy.
- Keep strategy signals separate from execution simulation.
- Backtest output must clearly label assumptions and data quality.

## Current Weakness Summary

The current backtest is trade-summary based. It can deduct configured commission and fixed slippage costs, but it does not prove realistic live execution. It does not currently model tick path, bid/ask spread, intrabar SL/TP order, broker stop/freeze constraints, lot-step rejection, projected margin, OrderCheck rejection, order-send rejection, retry delay, no-trade windows, session spread regimes, news blackout, or latency.

P2 should add a market-simulation layer around existing signals/trades without changing how entries, indicators, AI prompts, or TP strategy are generated.

## Patch 1: Backtest Market Data Input Model

Current weakness:

- Backtest input is closed trade summaries or simple CSV-derived trades.
- There is no first-class tick/OHLC market data model.
- Spread, bid/ask, session, and timestamp quality are not represented.

Exact behavior required:

- Add market data DTOs for tick and OHLC backtesting.
- Tick data should support UTC timestamp, bid, ask, optional last price, volume, and source metadata.
- OHLC fallback should support UTC open time, timeframe, open/high/low/close, optional bid/ask OHLC, spread points/pips, volume, and source metadata.
- Add data-quality flags: `Tick`, `BidAskOhlc`, `MidOhlcOnly`, `SpreadSynthetic`, `SessionTimeUnverified`.
- Normalize all timestamps to UTC.
- Preserve broker/server timestamp if available.
- Backtest must refuse to label a run as tick-realistic unless tick data exists.
- Backtest must refuse to label a run as bid/ask realistic unless bid/ask or spread data exists.

Files likely to modify/create:

- `Domain/Models/WorkflowModels.cs`
- `Trading/Backtesting/IBacktestMarketDataLoader.cs`
- `Trading/Backtesting/BacktestMarketDataModels.cs`
- `Trading/Backtesting/CsvBacktestMarketDataLoader.cs`
- `Trading/Backtesting/BacktestDataQuality.cs`
- `Tests/ForexBot.Tests/Program.cs`

Tests required:

- Tick CSV loads bid/ask ticks with UTC timestamps.
- OHLC CSV loads bars with timeframe and UTC timestamps.
- Missing spread data marks spread quality as synthetic or unavailable.
- Non-UTC or ambiguous timestamps are normalized or rejected.
- Backtest result includes market data quality labels.

Affects strategy logic:

- No.

Expected risk if skipped:

- Backtests remain trade-summary reports and cannot validate whether scalping fills were executable.

## Patch 2: Execution Assumption Configuration

Current weakness:

- Commission and slippage exist as simple config deductions, but execution assumptions are not grouped, versioned, or reported as a backtest execution model.
- Latency, spread source, rejection model, and conservative OHLC behavior are not configurable.

Exact behavior required:

- Add a `BacktestExecutionConfig` or equivalent section under `BotConfig` or backtesting-specific config.
- Include commission model, slippage model, spread model, latency model, rejection model, and intrabar fill policy.
- Add explicit modes:
  - `TickExact`
  - `BidAskOhlc`
  - `ConservativeOhlc`
  - `SummaryOnly`
- If mode is `SummaryOnly`, output warning `BACKTEST_EXECUTION_MODEL_MISSING`.
- Every backtest result must include execution assumption summary text.
- The default must be conservative and must not claim live realism.

Files likely to modify/create:

- `Domain/Models/Models.cs`
- `Domain/Models/WorkflowModels.cs`
- `Trading/Backtesting/BacktestExecutionConfig.cs`
- `Trading/Backtesting/BacktestingService.cs`
- `Tests/ForexBot.Tests/Program.cs`

Tests required:

- Default config marks execution model as summary-only or conservative.
- Tick mode without tick data rejects or downgrades with warning.
- Backtest result includes execution assumption summary.
- Missing required execution assumptions produce clear warnings.

Affects strategy logic:

- No.

Expected risk if skipped:

- Users may confuse cost-adjusted summary backtests with live-realistic execution simulation.

## Patch 3: Spread And Bid/Ask Fill Simulation

Current weakness:

- Backtests do not model bid/ask spread at entry, SL, TP, or close.
- Current P/L is based on entry/exit prices supplied by summary data.

Exact behavior required:

- Simulate BUY entries at ask and exits at bid.
- Simulate SELL entries at bid and exits at ask.
- If tick bid/ask data exists, use exact bid/ask ticks.
- If OHLC bid/ask exists, use bid/ask bar path according to selected intrabar policy.
- If only mid OHLC exists and spread data exists, synthesize bid/ask using half-spread.
- If no spread data exists, mark spread as `Missing/Unverified` and either fail closed or run as summary-only with warning.
- Report average spread, max spread, and spread cost by pair/session.

Files likely to modify/create:

- `Trading/Backtesting/BacktestExecutionSimulator.cs`
- `Trading/Backtesting/SpreadModel.cs`
- `Trading/Backtesting/BacktestingService.cs`
- `Domain/Models/WorkflowModels.cs`
- `Tests/ForexBot.Tests/Program.cs`

Tests required:

- BUY fill uses ask entry and bid exit.
- SELL fill uses bid entry and ask exit.
- Synthetic spread worsens P/L versus mid-price fill.
- Missing spread data marks spread realism missing/unverified.
- Report includes average and max spread.

Affects strategy logic:

- No.

Expected risk if skipped:

- Scalping profitability can be materially overstated because spread may be larger than the target edge.

## Patch 4: Commission And Slippage Cost Simulation

Current weakness:

- Commission and slippage deductions exist, but they are simple fixed configured costs.
- Slippage does not adjust entry/exit prices or vary by pair/session/volatility.

Exact behavior required:

- Preserve existing commission deduction behavior.
- Add per-symbol and per-session commission/slippage assumptions where configured.
- Support fixed slippage, random bounded slippage, and live-log-derived slippage distributions.
- Apply slippage to fill prices, not only as a cost deduction, where price data supports it.
- Report total commission, total slippage, average slippage pips, and worst slippage.
- If slippage data is required but unavailable, fail closed or downgrade with explicit warning depending config.

Files likely to modify/create:

- `Domain/Models/Models.cs`
- `Domain/Common/CommissionCalculator.cs`
- `Domain/Common/SlippageCalculator.cs`
- `Trading/Backtesting/BacktestExecutionSimulator.cs`
- `Trading/Backtesting/BacktestingService.cs`
- `Tests/ForexBot.Tests/Program.cs`

Tests required:

- Commission reduces winning and losing trade net P/L.
- Price slippage worsens BUY and SELL entries.
- Exit slippage worsens SL/TP/close fills.
- Session-specific slippage overrides default.
- Missing required slippage assumptions produce clear warning or failure.

Affects strategy logic:

- No.

Expected risk if skipped:

- Backtests may show a profitable scalping edge that disappears once true execution costs are applied.

## Patch 5: Latency Simulation

Current weakness:

- Live latency is not measured as a first-class execution assumption.
- Backtests assume immediate entry/exit at supplied prices.

Exact behavior required:

- Add configurable signal-to-send latency and send-to-fill latency.
- Support fixed milliseconds, random range, and live-log-derived latency distribution.
- For tick data, shift entry evaluation forward by configured latency.
- For OHLC fallback, apply conservative latency price movement assumptions.
- Record latency assumptions in every backtest result.
- Report average simulated latency and latency-adjusted P/L impact.

Files likely to modify/create:

- `Domain/Models/WorkflowModels.cs`
- `Trading/Backtesting/LatencyModel.cs`
- `Trading/Backtesting/BacktestExecutionSimulator.cs`
- `Infrastructure/Persistence/ITradeRepository.cs`, only if live latency logs are later reused
- `Tests/ForexBot.Tests/Program.cs`

Tests required:

- Tick-mode latency delays entry to later tick.
- Positive latency can change fill price.
- Missing tick data with latency uses conservative OHLC fallback.
- Report includes latency assumptions.

Affects strategy logic:

- No.

Expected risk if skipped:

- Very short-term scalping entries may look executable even when live delay would make the price stale.

## Patch 6: Intrabar SL/TP Simulation

Current weakness:

- Summary backtests cannot prove whether SL or TP was hit first.
- OHLC bars can contain both SL and TP in the same candle.

Exact behavior required:

- Tick mode must resolve SL/TP by exact tick sequence.
- OHLC fallback must support conservative SL-first handling when both SL and TP are inside the same candle.
- Add explicit policies:
  - `TickExact`
  - `ConservativeSlFirst`
  - `OptimisticTpFirst`, allowed only for diagnostics and clearly marked unrealistic
  - `OpenHighLowClosePath`, only if chosen explicitly
- Default OHLC fallback must be conservative SL-first.
- Report count of ambiguous candles and how many were resolved SL-first.

Files likely to modify/create:

- `Trading/Backtesting/IntrabarExecutionPolicy.cs`
- `Trading/Backtesting/BacktestExecutionSimulator.cs`
- `Trading/Backtesting/BacktestingService.cs`
- `Domain/Models/WorkflowModels.cs`
- `Tests/ForexBot.Tests/Program.cs`

Tests required:

- Tick sequence hits TP before SL when ticks prove it.
- Tick sequence hits SL before TP when ticks prove it.
- OHLC candle containing both SL and TP resolves SL-first by default.
- Ambiguous OHLC count appears in report.
- Optimistic TP-first mode is labeled non-conservative.

Affects strategy logic:

- No.

Expected risk if skipped:

- Tight SL/TP scalps can be misclassified as winners when the real path likely hit SL first.

## Patch 7: Broker Stop/Freeze/Volume Rule Simulation

Current weakness:

- Live validation checks stop level, freeze level, lot min/max/step, and volume limits.
- Backtest summary accepts supplied trades and lots without broker-rule rejection.

Exact behavior required:

- Add broker rule snapshot model for symbol, timestamp, stop level, freeze level, min lot, max lot, lot step, volume limit, digits, point size.
- Validate every simulated order before fill.
- Reject SL/TP too close to current bid/ask or pending entry using stop level.
- Reject or skip freeze-sensitive modifications using freeze level.
- Normalize or reject lot size based on min/max/step.
- Include rejected simulated orders in backtest output separately from losing trades.

Files likely to modify/create:

- `Domain/Models/Models.cs`
- `Trading/Backtesting/BrokerRuleSnapshot.cs`
- `Trading/Backtesting/BrokerRuleSimulator.cs`
- `Trading/Backtesting/BacktestExecutionSimulator.cs`
- `Tests/ForexBot.Tests/Program.cs`

Tests required:

- Stop level rejects too-close BUY SL/TP.
- Stop level rejects too-close SELL SL/TP.
- Lot below min, above max, and off-step are rejected or normalized according to config.
- Freeze-level modification is skipped/rejected.
- Rejected orders are counted separately.

Affects strategy logic:

- No.

Expected risk if skipped:

- Backtest may include orders that broker rules would have rejected live.

## Patch 8: Margin And OrderCheck-Like Validation

Current weakness:

- Live execution can run projected margin validation and broker OrderCheck.
- Backtest does not simulate margin requirements or OrderCheck rejections.

Exact behavior required:

- Add account simulation state: balance, equity, margin used, free margin, margin level, leverage.
- Add per-symbol margin assumptions or loader.
- Before simulated entry, calculate required margin and projected margin level.
- Reject trades below configured minimum margin level.
- Add OrderCheck-like validation result with retcode/reason fields.
- Surface rejection codes separately from trade losses.

Files likely to modify/create:

- `Domain/Models/WorkflowModels.cs`
- `Trading/Backtesting/BacktestAccountState.cs`
- `Trading/Backtesting/MarginSimulationService.cs`
- `Trading/Backtesting/OrderCheckSimulationService.cs`
- `Trading/Backtesting/BacktestExecutionSimulator.cs`
- `Tests/ForexBot.Tests/Program.cs`

Tests required:

- Healthy projected margin allows simulated order.
- Low projected margin rejects simulated order.
- Missing margin assumptions fail closed or warn according to config.
- OrderCheck-like rejection records retcode/reason.
- Rejected orders do not count as wins or losses.

Affects strategy logic:

- No.

Expected risk if skipped:

- Backtest may overstate scalability by allowing position sequences that live margin rules would block.

## Patch 9: Order Rejection And Retry Simulation

Current weakness:

- Live order send failures are classified and retryable failures can re-run safety gates.
- Backtests assume every order is accepted immediately.

Exact behavior required:

- Add order rejection simulation for transient and permanent failures.
- Support configured reject rates by symbol/session/spread regime.
- Support replaying live reject logs when available.
- Retry only transient simulated failures.
- Each retry must refresh simulated price/spread, no-trade window, margin, and broker-rule checks.
- Retry delay must apply latency and market movement.
- Report accepted, rejected, retried, and retry-exhausted counts.

Files likely to modify/create:

- `Trading/Backtesting/OrderRejectionModel.cs`
- `Trading/Backtesting/RetrySimulationService.cs`
- `Trading/Backtesting/BacktestExecutionSimulator.cs`
- `Domain/Models/WorkflowModels.cs`
- `Tests/ForexBot.Tests/Program.cs`

Tests required:

- Permanent rejection does not retry.
- Transient rejection retries up to configured max.
- Retry uses later tick/bar price.
- Retry exhaustion records final rejection.
- Retry success records one accepted simulated trade.

Affects strategy logic:

- No.

Expected risk if skipped:

- Backtest ignores execution failure modes that are common during fast-moving scalping conditions.

## Patch 10: No-Trade, Session Spread, And News Filters

Current weakness:

- Live validation can block no-trade windows, session spread caps, and news risk.
- Backtest summaries do not replay those filters.

Exact behavior required:

- Apply configured rollover/no-trade windows to historical timestamps.
- Apply session spread caps and spread-widening rules using session-aware timestamp handling.
- Add optional historical news calendar loader.
- If news data exists, apply blackout rules by affected currencies.
- If required news data is unavailable, fail closed or mark news filter unverified according to config.
- Report skipped/rejected trades by filter reason.

Files likely to modify/create:

- `Trading/Backtesting/BacktestFilterSimulator.cs`
- `Trading/Backtesting/IHistoricalNewsDataLoader.cs`
- `Trading/Backtesting/HistoricalNewsDataModels.cs`
- `Domain/Common/NoTradeWindowValidator.cs`
- `Domain/Common/SessionSpreadValidator.cs`
- `Tests/ForexBot.Tests/Program.cs`

Tests required:

- Trade inside rollover window is excluded/rejected.
- Trade outside no-trade window proceeds.
- Session spread cap rejects high-spread trade.
- Historical high-impact news blackout rejects affected pair.
- Missing required news data marks backtest unverified or fails according to config.

Affects strategy logic:

- No.

Expected risk if skipped:

- Backtest may include trades that the live safety layer would never allow.

## Patch 11: Simulation Result And Reporting Upgrade

Current weakness:

- Current report focuses on total trades, win rate, net P/L, profit factor, drawdown, commission, and slippage.
- It does not segment by pair/session/spread regime or report expectancy, losing streak, rejected orders, or assumption quality.

Exact behavior required:

- Extend backtest result with:
  - profit factor after costs
  - expectancy after costs
  - max drawdown
  - worst losing streak
  - performance by pair
  - performance by session
  - performance by spread regime
  - total commission
  - total slippage
  - rejected order count by reason
  - skipped filter count by reason
  - ambiguous intrabar count
  - execution assumption summary
  - data quality warnings
- Add markdown/JSON export for full simulation results.
- UI can consume these fields later, but this patch should keep UI changes minimal.

Files likely to modify/create:

- `Domain/Models/WorkflowModels.cs`
- `Trading/Backtesting/BacktestReportBuilder.cs`
- `Trading/Backtesting/BacktestingService.cs`
- `Application/Workflows/PerformanceData.cs`
- `UI/Forms/MainForm.Backtest.cs`, only for minimal display/export hooks
- `Tests/ForexBot.Tests/Program.cs`

Tests required:

- Report includes expectancy after costs.
- Report includes worst losing streak.
- Report segments performance by pair.
- Report segments performance by session.
- Report segments performance by spread regime.
- Rejected/skipped trades are not counted as wins/losses.

Affects strategy logic:

- No.

Expected risk if skipped:

- Users may see aggregate results that hide scalping fragility by session, spread regime, or execution rejection.

## Patch 12: Out-Of-Sample And Walk-Forward Validation

Current weakness:

- Current backtesting does not separate in-sample and out-of-sample performance.
- There is no walk-forward process for validating stability.

Exact behavior required:

- Add configurable date-based and percentage-based train/test splits.
- Add walk-forward windows with anchored or rolling training periods.
- Run identical execution assumptions across windows.
- Report per-window net P/L, profit factor, expectancy, drawdown, and trade count.
- Flag unstable strategies where out-of-sample performance degrades beyond configured threshold.
- Do not tune entries or indicators in this patch; only evaluate existing signals/results across windows.

Files likely to modify/create:

- `Trading/Backtesting/BacktestValidationService.cs`
- `Trading/Backtesting/WalkForwardModels.cs`
- `Domain/Models/WorkflowModels.cs`
- `Tests/ForexBot.Tests/Program.cs`

Tests required:

- Date split assigns trades to in-sample/out-of-sample correctly.
- Percentage split preserves chronological order.
- Walk-forward windows are generated correctly.
- Per-window metrics are reported.
- Degradation warning appears when out-of-sample performance drops.

Affects strategy logic:

- No.

Expected risk if skipped:

- A strategy can look good due to overfitting or favorable historical periods while failing on unseen data.

## Patch 13: Monte Carlo Robustness Stress Tests

Current weakness:

- Current backtesting reports one historical sequence only.
- Scalping systems can be highly sensitive to trade order, slippage, spreads, and losing streak clustering.

Exact behavior required:

- Add Monte Carlo trade-sequence reshuffling.
- Add cost perturbation simulations for spread, slippage, and commission.
- Add rejection-rate perturbation if rejection model is enabled.
- Report median, 5th percentile, and worst-case net P/L/drawdown.
- Report probability of exceeding max drawdown threshold.
- Report worst losing streak distribution.

Files likely to modify/create:

- `Trading/Backtesting/MonteCarloBacktestService.cs`
- `Trading/Backtesting/MonteCarloModels.cs`
- `Domain/Models/WorkflowModels.cs`
- `Tests/ForexBot.Tests/Program.cs`

Tests required:

- Monte Carlo preserves individual trade P/L while shuffling order.
- Fixed seed produces deterministic output.
- Drawdown distribution is calculated.
- Losing streak distribution is calculated.
- Cost perturbation worsens P/L when costs increase.

Affects strategy logic:

- No.

Expected risk if skipped:

- The bot may pass one historical sequence but fail under normal variation in trade order and execution costs.

## Patch 14: Live-Backtest Assumption Reconciliation

Current weakness:

- P1 audit can report mismatches, but the backtest does not yet compare simulated assumptions against live execution logs.

Exact behavior required:

- Add comparison between live trade logs and backtest execution assumptions.
- Compare live average spread, slippage, commission, rejection rates, retry rates, fill latency, and session distribution.
- Flag backtest assumptions that are more optimistic than recent live results.
- Export reconciliation report.
- Do not auto-change strategy settings.

Files likely to modify/create:

- `Trading/Backtesting/LiveBacktestReconciliationService.cs`
- `Infrastructure/Persistence/ITradeRepository.cs`
- `Infrastructure/Persistence/SqliteTradeRepository.cs`
- `Trading/Backtesting/BacktestReportBuilder.cs`
- `Tests/ForexBot.Tests/Program.cs`

Tests required:

- Live slippage worse than backtest assumption produces warning.
- Live spread worse than backtest assumption produces warning.
- Live rejection rate higher than backtest assumption produces warning.
- Missing live data marks reconciliation unverified.

Affects strategy logic:

- No.

Expected risk if skipped:

- Backtest assumptions may silently drift away from real broker execution quality.

## Recommended P2 Implementation Order

1. Backtest market data input model.
2. Execution assumption configuration.
3. Spread and bid/ask fill simulation.
4. Intrabar SL/TP simulation.
5. Commission and slippage cost simulation.
6. Latency simulation.
7. Broker stop/freeze/volume rule simulation.
8. Margin and OrderCheck-like validation.
9. Order rejection and retry simulation.
10. No-trade, session spread, and news filters.
11. Simulation result and reporting upgrade.
12. Out-of-sample and walk-forward validation.
13. Monte Carlo robustness stress tests.
14. Live-backtest assumption reconciliation.

Reason for order:

- Data and assumptions must exist before fills can be simulated.
- Bid/ask spread and intrabar path are the highest-risk scalping realism gaps.
- Broker and safety rules should be added before reporting claims improve.
- Robustness and reconciliation are most useful after the simulator produces realistic trade outcomes.

## Done Criteria For P2

- Backtests clearly identify data quality and execution model.
- Tick mode uses exact tick sequence when available.
- OHLC fallback uses conservative SL-first behavior when both SL and TP are hit.
- Spread, commission, slippage, latency, broker rules, margin, rejections, retries, no-trade windows, session spread, and news filters are either modeled or explicitly marked missing/unverified.
- Reports include profit factor after costs, expectancy after costs, max drawdown, worst losing streak, performance by pair, performance by session, and performance by spread regime.
- Backtest output never claims live realism when required assumptions are missing.
