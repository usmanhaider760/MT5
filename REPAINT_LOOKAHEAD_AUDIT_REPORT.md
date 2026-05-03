# Repainting / Future-Data Bias Audit Report

Scope: P3 audit/reporting only. This report does not change strategy logic, indicators, AI prompts, take-profit logic, live trading behavior, or execution behavior.

This is a source-code audit. It flags confirmed limitations, potential look-ahead risks, and areas not verified from code alone. It is not live proof and not a positive-expectancy claim.

## Summary

- Closed-candle safeguards are visible for several EA candle and indicator snapshot fields.
- Current-period high/low and level fields need timestamped historical reconstruction before they can be used as proof inputs.
- The realistic runner is appropriate for post-entry exit simulation, but candidate generation must remain timestamp-clean.
- The old trade-summary backtest is confirmed to be outcome-summary analytics, not signal-edge proof.
- AI prompt context must be frozen at the candidate timestamp before AI-confirmed backtests can be trusted.

## Live Signal-Generation Risk

| Finding | Severity | Status | Code Evidence | Why It Matters | Recommended Fix Or Verification Step |
|---|---|---|---|---|---|
| Closed-candle snapshots are used for candle and indicator fields | Low | Confirmed | `MT5_EA/TradingBotEA.mq5` - `SnapshotCandleJson / SnapshotIndicatorsJson`: uses shift 1 candle and indicator reads for multiple snapshot fields | This lowers live repainting risk because the most recent still-forming candle is skipped for these fields | Keep closed-candle shift usage covered when adding snapshot fields |
| Current daily and weekly high/low fields can become look-ahead in historical replay | High | Potential | `MT5_EA/TradingBotEA.mq5` - `SnapshotPriceJson`: reads current-period daily and weekly high/low fields | Historical replay must not use final period high/low before that period has completed | Reconstruct these fields from ticks or elapsed candles available at the signal timestamp |
| Support, resistance, and structure levels need timestamped reconstruction proof | High | Potential | `MT5_EA/TradingBotEA.mq5` - `SnapshotStructureJson / SnapshotLevelsJson`: mixes closed H1 swings with current/prior daily data | Level fields can repaint in tests if reconstructed from complete future candles | Add timestamp-aware snapshot fixtures proving all level inputs are known at signal time |
| No per-signal consumed-data watermark was found | Medium | Not verified | `Trading/Scalping/ScalpingSessionService.cs` - `EvaluateSnapshot` | Later audits cannot prove the newest candle, indicator, or level consumed by a signal | Persist signal timestamp and newest consumed candle timestamp by timeframe |

## Realistic Backtest Runner Risk

| Finding | Severity | Status | Code Evidence | Why It Matters | Recommended Fix Or Verification Step |
|---|---|---|---|---|---|
| Exit simulation uses future path after the candidate timestamp | Medium | Confirmed | `Trading/Backtesting/RealisticBacktestRunner.cs` - `RealisticBacktestRunner.ResolveExit` | Future market path is correct for exits, but must never create or filter entry candidates | Keep candidate generation separate from exit resolution and guard adapters against future-bar reads |
| Strategy adapter can accept an externally supplied historical market price | Medium | Potential | `Trading/Backtesting/StrategyToRealisticBacktestAdapter.cs` - `StrategyToRealisticBacktestAdapter.ResolveEntryPrice` | The adapter cannot prove the price came from the signal timestamp instead of a later close | Require fixture metadata for the price source and reject future-timestamped inputs |
| OHLC exit simulation uses candle high/low but resolves ambiguous same-bar hits conservatively | Low | Confirmed | `Trading/Backtesting/IntrabarExitSimulator.cs` - `IntrabarExitSimulator.SimulateOhlcExit` | Closed-candle high/low can resolve whether a stop or target was touched, but not exact intrabar order | Prefer tick data for scalping proof and label OHLC results as path-assumption based |

## Old Trade-Summary Backtest Limitation

| Finding | Severity | Status | Code Evidence | Why It Matters | Recommended Fix Or Verification Step |
|---|---|---|---|---|---|
| SQLite trade-history backtest reconstructs exits from realized P/L | Critical | Confirmed | `Trading/Backtesting/IBacktestDataLoader.cs` - `DbBacktestLoader.LoadAsync`: derives pips from `ProfitUsd` and reconstructs exit price | Closed trade summaries prove outcomes of already-taken trades, not signal edge across all historical opportunities | Keep the old summary backtest for trade-summary reporting only |
| Summary backtest calculates results from supplied exit prices | High | Confirmed | `Trading/Backtesting/BacktestingService.cs` - `BacktestingService.CalculatePips` | This evaluates historical trade outcomes rather than replaying entry decisions without future knowledge | Do not use it as proof that current deterministic strategy entries are profitable |

## AI-Prompt Leakage Risk

| Finding | Severity | Status | Code Evidence | Why It Matters | Recommended Fix Or Verification Step |
|---|---|---|---|---|---|
| AI prompt includes recent trade outcomes and daily performance fields | High | Potential | `Infrastructure/AI/AiPrompts.cs` - `AiPrompts.AiInputPromptTemplate`: includes `TRADE HISTORY`, win rate, PnL, and `Last 5 Trades` | In historical AI-filter tests, later trades or end-of-day statistics would leak future results into the decision | Freeze AI prompt context as of each candidate timestamp |
| AI instructions ask for current-market derivation, but historical prompt snapshots are not proven frozen | Medium | Not verified | `Infrastructure/AI/AiPrompts.cs` - `AiPrompts.AiInputPromptTemplate` | A correct prompt can still leak if snapshot filling injects future candles or future outcomes | Store exact prompt payload, data watermark, and model response for each historical decision |
| No confirmed use of realized P/L, exit price, or future result in deterministic entry logic was found | Low | Not verified | `Trading/StrategyEngine/StrategyEngine.cs` - `StrategyEngine.CreateInitialSignalAsync` | The full runtime data flow was not exhaustively traced | Add a source guard blocking exit/outcome fields in deterministic entry-signal methods |

## Severity And Status Legend

- Severity: Critical / High / Medium / Low.
- Status: Confirmed / Potential / Not verified.
- Confirmed means the listed behavior is directly evidenced by source fragments.
- Potential means source evidence identifies a risk that depends on runtime data preparation or historical replay usage.
- Not verified means this audit could not prove the behavior from currently inspected code.
