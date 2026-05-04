# Repainting / Future-Data Bias Audit Report

Scope: P3 audit/reporting only. This generator does not change strategy logic, indicators, AI prompts, take-profit logic, live trading behavior, or execution behavior.

This report is a source-code audit. It flags confirmed limitations, potential look-ahead risks, and areas not verified from code alone. It is not live proof and not a positive-expectancy claim.

## Summary

- Closed-candle safeguards are visible for several EA candle and indicator snapshot fields.
- Current-period high/low and level fields need timestamped historical reconstruction before they can be used as proof inputs.
- The realistic runner is appropriate for post-entry exit simulation, but candidate generation must remain timestamp-clean.
- The old trade-summary backtest is confirmed to be outcome-summary analytics, not signal-edge proof.
- AI prompt context must be frozen at the candidate timestamp before AI-confirmed backtests can be trusted.

## Live Signal-Generation Risk

| Finding | Severity | Status | Code Evidence | Why It Matters | Recommended Fix Or Verification Step |
|---|---|---|---|---|---|
| Closed-candle snapshots are used for candle and indicator fields | Low | Confirmed | `MT5_EA/TradingBotEA.mq5` - `SnapshotCandleJson / SnapshotIndicatorsJson`: The EA requests candle data from shift 1 and indicator buffers from shift 1 for several snapshot fields. | This lowers live repainting risk because the most recent still-forming candle is skipped for these fields. | Keep closed-candle shift usage covered when adding snapshot fields. Verify any new field documents its candle shift. |
| Current daily and weekly high/low fields can become look-ahead in historical replay | High | Potential | `MT5_EA/TradingBotEA.mq5` - `SnapshotPriceJson`: The snapshot includes current-period daily and weekly highs/lows from shift 0. | In live trading, current-period high/low are known only up to the current tick. In a historical replay, using the final daily or weekly high/low before the period has completed would leak future information into entry decisions or AI context. | When replaying history, reconstruct these fields from ticks or lower-timeframe candles available at the signal timestamp, or mark them unavailable until verified. |
| Support, resistance, and structure levels need timestamped reconstruction proof | High | Potential | `MT5_EA/TradingBotEA.mq5` - `SnapshotStructureJson / SnapshotLevelsJson`: Structure uses closed H1 swings, while levels also read current and prior daily data. | Swing and level fields can repaint in historical tests if they are reconstructed from complete future candles instead of the bars known at signal time. | Add a timestamp-aware snapshot fixture proving each support/resistance input is calculated only from closed or elapsed bars available at the candidate timestamp. |
| No per-signal consumed-data watermark was found | Medium | Not verified | `Trading/Scalping/ScalpingSessionService.cs` - `EvaluateSnapshot`: The report did not verify a persisted timestamp for the newest candle, indicator, or level consumed by each generated signal. | Without a data watermark, later audits cannot prove that a signal avoided future candles or partially formed candle closes. | Persist signal timestamp, newest consumed candle timestamp by timeframe, and source snapshot timestamp for strategy-proof datasets. |

## Realistic Backtest Runner Risk

| Finding | Severity | Status | Code Evidence | Why It Matters | Recommended Fix Or Verification Step |
|---|---|---|---|---|---|
| Exit simulation uses future path after the candidate timestamp | Medium | Confirmed | `Trading/Backtesting/RealisticBacktestRunner.cs` - `RealisticBacktestRunner.ResolveExit`: The runner searches ticks and candles with timestamp greater than or equal to the candidate timestamp to determine exits. | Using future market path is correct for resolving post-entry exits, but these same future bars must never be used to create or filter the entry candidate. | Keep candidate generation separate from exit resolution and add guards that adapters cannot read ticks/candles after the signal timestamp when producing candidates. |
| Strategy adapter can accept an externally supplied historical market price | Medium | Potential | `Trading/Backtesting/StrategyToRealisticBacktestAdapter.cs` - `StrategyToRealisticBacktestAdapter.ResolveEntryPrice`: When signal entry is not positive, the adapter uses HistoricalMarketPrice supplied by the caller. | The adapter itself cannot prove whether that price came from the signal timestamp or a later candle. A caller could accidentally supply a future close. | Require fixture metadata for historical market price source and add tests where future-bar prices are rejected or explicitly marked as invalid input. |
| OHLC exit simulation uses candle high/low but resolves ambiguous same-bar hits conservatively | Low | Confirmed | `Trading/Backtesting/IntrabarExitSimulator.cs` - `IntrabarExitSimulator.SimulateOhlcExit`: OHLC exit checks final high/low for stop-loss and take-profit hits and chooses stop loss when both are hit in the same candle. | Final high/low are acceptable for closed-candle exit simulation, but they do not prove intrabar order unless tick data is available. | Prefer tick data for scalping proof. Keep OHLC both-hit behavior conservative and label OHLC reports as path-assumption based. |

## Old Trade-Summary Backtest Limitation

| Finding | Severity | Status | Code Evidence | Why It Matters | Recommended Fix Or Verification Step |
|---|---|---|---|---|---|
| SQLite trade-history backtest reconstructs exits from realized P/L | Critical | Confirmed | `Trading/Backtesting/IBacktestDataLoader.cs` - `DbBacktestLoader.LoadAsync`: The old loader derives pips from ProfitUsd and reconstructs an exit price from executed price and realized profit. | Closed trade summaries prove what happened to already-taken trades, not whether historical signals had positive expectancy. They cannot detect skipped signals, future-data bias, or entry-rule edge. | Keep the old summary backtest for trade-summary reporting only. Use realistic timestamped market-data backtests for strategy edge proof. |
| Summary backtest calculates results from supplied exit prices | High | Confirmed | `Trading/Backtesting/BacktestingService.cs` - `BacktestingService.CalculatePips`: The service computes pips from BacktestTrade entry and exit prices. | If the input is a closed-trade summary, the service evaluates historical trade outcomes rather than replaying entry decisions without future knowledge. | Report this as trade-summary analytics, not signal-edge proof. Do not use it as evidence that current deterministic strategy entries are profitable. |

## AI-Prompt Leakage Risk

| Finding | Severity | Status | Code Evidence | Why It Matters | Recommended Fix Or Verification Step |
|---|---|---|---|---|---|
| AI prompt includes recent trade outcomes and daily performance fields | High | Potential | `Infrastructure/AI/AiPrompts.cs` - `AiPrompts.AiInputPromptTemplate`: The prompt contains TRADE HISTORY, consecutive losses, win rate today, total PnL today, and Last 5 Trades. | Those fields are valid live context only if they contain outcomes known before the signal timestamp. In historical AI-filter tests, including later trades or end-of-day statistics would leak future results into the decision. | Freeze AI prompt context as of each candidate timestamp and add tests that future closed trades are excluded from prompt fixtures. |
| AI instructions ask for current-market derivation, but historical prompt snapshots are not proven frozen | Medium | Not verified | `Infrastructure/AI/AiPrompts.cs` - `AiPrompts.AiInputPromptTemplate`: The template says to derive SL/TP from current market structure, ATR, and levels, but this audit did not verify archived prompt inputs by timestamp. | A correct prompt can still leak if the snapshot filler injects fields calculated from future candles or future trade outcomes. | For AI-filter proof, store the exact prompt payload, signal timestamp, data watermark, and model response used for each historical decision. |
| No confirmed use of realized P/L, exit price, or future result in deterministic entry logic was found | Low | Not verified | `Trading/StrategyEngine/StrategyEngine.cs` - `StrategyEngine.CreateInitialSignalAsync`: This audit did not find source evidence that base deterministic entry generation reads exit price or realized P/L. The result is marked not verified because the full runtime data flow was not exhaustively traced. | If realized outcomes feed entry generation indirectly, the strategy can overfit to prior or future performance instead of market state. | Add a source guard for deterministic strategy modules that blocks references to ExitPrice, ProfitUsd, realized P/L, or completed-trade outcome fields in entry-signal methods. |

## Severity And Status Legend

- Severity: Critical / High / Medium / Low.
- Status: Confirmed / Potential / Not verified.
- Confirmed means the listed behavior is directly evidenced by source fragments.
- Potential means source evidence identifies a risk that depends on runtime data preparation or historical replay usage.
- Not verified means this audit could not prove the behavior from currently inspected code.
