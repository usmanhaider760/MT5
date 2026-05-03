# P3 Strategy Edge Proof Plan

Sources:

- `P0_SAFETY_PATCH_PLAN.md`
- `P1_EXECUTION_REALISM_PLAN.md`
- `P2_BACKTEST_REALISM_PLAN.md`
- `REALISTIC_BACKTEST_REPORT.md`, when generated

Scope: prove or disprove whether the forex scalping strategy has positive expectancy after realistic execution costs.

Rules:

- Planning only.
- Do not optimize entries.
- Do not change indicators.
- Do not change AI prompts.
- Do not change take-profit strategy.
- Do not change strategy entry logic.
- Do not change live trading behavior.
- Keep deterministic strategy logic separate from AI confirmation.
- Use realistic execution assumptions from P1 and P2.
- Strategy proof output must clearly label assumptions, data quality, sample size, and whether results are in-sample, out-of-sample, walk-forward, Monte Carlo, or live-demo.

## Current Weakness Summary

P0 protects live account safety, P1 improves execution realism, and P2 creates realistic backtest simulation/reporting foundations. The remaining question is whether the strategy itself has a durable edge after spread, slippage, commission, rejections, no-trade filters, news filters, broker constraints, drawdown, and adverse sequencing.

Current strategy proof is incomplete because deterministic entry and exit rules are not yet formally extracted into an auditable backtest signal source. AI confirmation is not yet measured separately from the deterministic signal. Performance is not yet proven by symbol, session, spread regime, volatility regime, trend/range regime, AI confidence, or signal reason. Cost sensitivity and robustness tests are not yet tied to a final pass/fail verdict.

P3 should not improve the strategy. It should only measure it.

## Patch 1: Deterministic Strategy Rule Inventory

Current weakness:

- Strategy rules are implemented across workflow, scanner, scalping, AI, and UI paths rather than documented as one deterministic rule inventory.
- Entry rules, exit rules, hold/no-trade behavior, and AI confirmation boundaries are not yet listed in a machine-checkable format.
- It is not yet clear which decisions belong to deterministic strategy logic versus AI filtering, risk gating, or execution safety.

Exact behavior required:

- Produce an inventory of all deterministic strategy inputs and outputs.
- Identify exact BUY, SELL, and HOLD/no-trade rules.
- Identify exact stop-loss and take-profit source rules without changing them.
- Identify strategy parameters and their current configured values.
- Identify all pre-signal filters versus post-signal safety filters.
- Identify all AI-dependent fields and mark them as optional confirmation only.
- Record where each rule lives in source files and which data fields it consumes.
- Flag any rule that is ambiguous, UI-only, manually supplied, or not reproducible from historical data.

Files likely to modify/create:

- `docs/STRATEGY_RULE_INVENTORY.md`
- `Trading/Scalping/`
- `Trading/Strategy/`, if present or later created
- `Application/Workflows/AutoBotService.cs`, read-only unless adding proof hooks later
- `Infrastructure/AI/ClaudeSignalService.cs`, read-only unless adding proof metadata later
- `Tests/ForexBot.Tests/Program.cs`

Tests required:

- Inventory file exists and includes BUY, SELL, HOLD/no-trade, stop-loss, and take-profit sections.
- Inventory references source files for each deterministic rule.
- Inventory labels AI confirmation separately from deterministic signal generation.
- Inventory states that rules are documentation/proof only and do not change live behavior.

Changes strategy logic:

- No.

Expected risk if skipped:

- Later backtests may measure an incomplete or inaccurate version of the strategy and produce a false edge verdict.

## Patch 2: Deterministic Signal Extraction Adapter

Current weakness:

- P2 can run realistic backtests from externally supplied candidates, but the production strategy is not yet exposed as a deterministic historical signal generator.
- Historical tests may rely on hand-built candidates that do not exactly match the strategy.
- HOLD/no-trade behavior may be lost if only executable trades are exported.

Exact behavior required:

- Add a deterministic strategy extraction adapter that converts historical market data into timestamped signal candidates.
- Preserve BUY, SELL, and HOLD/no-trade decisions.
- Preserve signal reason/source text, confidence if deterministic confidence exists, and parameter values used.
- Keep the adapter separate from AI confirmation, risk validation, user approval, and live execution.
- Do not call MT5, broker services, live repositories, or UI.
- Make output compatible with `StrategyToRealisticBacktestAdapter` and `RealisticBacktestRunner`.
- Include skipped/HOLD signals in the analysis dataset so trade frequency and filter behavior can be audited.

Files likely to modify/create:

- `Trading/StrategyProof/DeterministicStrategySignalExtractor.cs`
- `Trading/StrategyProof/StrategyProofModels.cs`
- `Trading/Backtesting/StrategyToRealisticBacktestAdapter.cs`
- `Trading/Backtesting/RealisticBacktestRunner.cs`, only if result metadata needs non-executed signal counts
- `Tests/ForexBot.Tests/Program.cs`

Tests required:

- Historical input produces deterministic BUY candidates.
- Historical input produces deterministic SELL candidates.
- Historical input records HOLD/no-trade decisions without creating executable trade candidates.
- Extractor output is deterministic for the same input data and config.
- Extractor does not reference MT5, `OpenTradeAsync`, live execution service, user approval, or AI prompt service.

Changes strategy logic:

- No.

Expected risk if skipped:

- Strategy proof remains dependent on externally supplied candidates and cannot prove the actual bot strategy.

## Patch 3: Future-Data And Repainting Audit

Current weakness:

- Scalping backtests can look profitable if indicators, candle states, support/resistance, trend labels, or AI context use future data.
- There is no automated check that a signal at time `T` only consumes data available at or before `T`.
- Open-candle versus closed-candle behavior is not explicitly verified.

Exact behavior required:

- Audit every deterministic signal input for timestamp boundaries.
- Add a proof mode that records the latest market-data timestamp consumed by each signal.
- Reject or warn when a signal consumes data after its signal timestamp.
- Identify whether indicators use closed candles only or live/incomplete candles.
- Flag repaint-prone calculations, revised support/resistance levels, lookahead labels, and future candle references.
- Add a report section for `FUTURE_DATA_RISK`, `OPEN_CANDLE_RISK`, and `REPAINT_RISK`.

Files likely to modify/create:

- `Trading/StrategyProof/FutureDataAuditService.cs`
- `Trading/StrategyProof/StrategyProofModels.cs`
- `Trading/Indicators/`, only if timestamp metadata needs proof hooks
- `Trading/Scalping/`, only if timestamp metadata needs proof hooks
- `Tests/ForexBot.Tests/Program.cs`

Tests required:

- Signal using only prior data passes audit.
- Signal consuming a future candle fails with `FUTURE_DATA_RISK`.
- Open-candle dependent signal is flagged with `OPEN_CANDLE_RISK`.
- Repaint-prone indicator metadata is surfaced in warnings.
- Audit does not alter generated signal direction, entry, stop loss, or take profit.

Changes strategy logic:

- No.

Expected risk if skipped:

- Backtest expectancy may be overstated by lookahead bias or repainting behavior.

## Patch 4: Signal Quality Metrics Dataset

Current weakness:

- P2 reporting covers realistic trade outcomes, but P3 needs a strategy-focused dataset that includes every candidate, completed trade, rejected trade, open trade, HOLD signal, and skipped signal.
- R multiple, duration, signal reason/source, and confidence metadata are not yet guaranteed for edge analysis.

Exact behavior required:

- Build a strategy proof result model around realistic backtest results.
- Include win rate after realistic costs, average win, average loss, expectancy, profit factor, max drawdown, worst losing streak, trade duration, and R multiple.
- Count total historical bars/ticks reviewed, total signals, BUY signals, SELL signals, HOLD/no-trade decisions, completed trades, rejected trades, skipped trades, and unresolved/open trades.
- Calculate R multiple using original stop distance and final net result after costs.
- Calculate trade duration from signal/open timestamp to simulated exit timestamp.
- Keep rejected and HOLD signals out of win/loss counts while retaining them in frequency and filter analysis.

Files likely to modify/create:

- `Trading/StrategyProof/StrategyEdgeMetrics.cs`
- `Trading/StrategyProof/StrategyProofModels.cs`
- `Trading/Backtesting/BacktestReportingMetrics.cs`
- `Trading/Backtesting/RealisticBacktestRunner.cs`, only if missing fields must be surfaced
- `Tests/ForexBot.Tests/Program.cs`

Tests required:

- Completed winners and losers produce correct win rate after costs.
- Average win and average loss are calculated from net P/L after costs.
- Expectancy and profit factor use realistic net P/L.
- Rejected, HOLD, skipped, and open signals are not counted as wins or losses.
- Trade duration and R multiple are calculated correctly.

Changes strategy logic:

- No.

Expected risk if skipped:

- The strategy verdict may rely on incomplete trade-summary metrics and miss whether the signal itself has positive expectancy.

## Patch 5: Segmented Performance Analysis

Current weakness:

- Aggregate results can hide that the strategy only works on one symbol, one session, one spread regime, or one market regime.
- Volatility and trend/range regimes are not yet first-class proof dimensions.
- AI confidence and signal reason/source are not yet analyzed as performance groups.

Exact behavior required:

- Segment realistic net performance by symbol.
- Segment by session using UTC session rules already used by P1/P2.
- Segment by spread regime using P2 spread metadata.
- Segment by volatility regime, such as low, normal, high, extreme, using fixed historical volatility buckets.
- Segment by trend/range regime using deterministic, non-lookahead classification.
- Segment by AI confidence if AI output exists.
- Segment by signal reason/source.
- For each segment, report sample size, win rate, average win, average loss, expectancy, profit factor, max drawdown, worst losing streak, average duration, and average R.
- Flag segments that have too few samples for a verdict.

Files likely to modify/create:

- `Trading/StrategyProof/StrategySegmentAnalyzer.cs`
- `Trading/StrategyProof/MarketRegimeClassifier.cs`
- `Trading/StrategyProof/StrategyProofModels.cs`
- `Trading/Backtesting/BacktestReportingMetrics.cs`
- `Tests/ForexBot.Tests/Program.cs`

Tests required:

- Symbol segmentation separates EURUSD and GBPUSD results.
- Session segmentation separates London, New York, Asia, rollover, and unknown sessions.
- Spread-regime segmentation separates tight, normal, wide, and extreme spread regimes.
- Volatility-regime segmentation is deterministic and uses no future data.
- Trend/range segmentation is deterministic and uses no future data.
- AI confidence and signal reason/source grouping works when metadata exists.
- Low-sample segments are flagged instead of treated as proof.

Changes strategy logic:

- No.

Expected risk if skipped:

- A weak overall strategy may appear acceptable because one favorable segment masks broad underperformance, or a good niche edge may be discarded because bad segments are mixed in.

## Patch 6: Execution Cost Sensitivity Analysis

Current weakness:

- P1/P2 model realistic costs, but edge proof needs to show whether expectancy survives worse spreads, slippage, commission, and broker stress.
- Scalping strategies often fail under modest cost increases.

Exact behavior required:

- Run the same deterministic candidates under multiple execution-cost scenarios.
- Include baseline cost assumptions from P1/P2.
- Include spread sensitivity scenarios, for example baseline, +25%, +50%, +100%, and session-widened.
- Include slippage sensitivity scenarios, for example baseline, +0.2 pip, +0.5 pip, +1.0 pip, and high-volatility slippage.
- Include commission sensitivity scenarios, for example baseline, +25%, +50%, and high-commission broker.
- Include worse-than-normal broker condition test combining wider spread, higher slippage, higher commission, higher rejection rate, and longer latency.
- Report expectancy, profit factor, max drawdown, and verdict under each cost scenario.
- Do not alter entries, indicators, take-profit, or stop-loss behavior between scenarios.

Files likely to modify/create:

- `Trading/StrategyProof/CostSensitivityRunner.cs`
- `Trading/StrategyProof/StrategyProofModels.cs`
- `Trading/Backtesting/BacktestExecutionCostModel.cs`
- `Trading/Backtesting/RealisticBacktestRunner.cs`, only if configurable scenario input needs extension
- `Tests/ForexBot.Tests/Program.cs`

Tests required:

- Higher spread reduces or preserves net P/L, never improves it.
- Higher slippage reduces or preserves net P/L, never improves it.
- Higher commission reduces or preserves net P/L, never improves it.
- Combined broker stress scenario produces a separate verdict.
- All cost scenarios use identical candidate timestamps, direction, entry, SL, and TP.

Changes strategy logic:

- No.

Expected risk if skipped:

- A marginal scalping edge may pass only under optimistic broker costs and fail in normal live conditions.

## Patch 7: Robustness And Stability Proof

Current weakness:

- P2 can support out-of-sample, walk-forward, and Monte Carlo foundations, but P3 needs a strategy-level robustness verdict.
- A strategy can look profitable on one historical period while failing under chronological splits or adverse trade ordering.

Exact behavior required:

- Run in-sample and out-of-sample analysis with chronological splits.
- Run walk-forward validation using fixed windows and identical execution assumptions.
- Run Monte Carlo trade-sequence stress on realistic net trade results.
- Report median, 5th percentile, and worst-case net profit and drawdown.
- Report probability of breaching configured drawdown limit.
- Report losing streak distribution.
- If parameters exist, run parameter sensitivity around current values without optimizing or selecting new values.
- Flag unstable results where small parameter changes or window changes materially flip expectancy.

Files likely to modify/create:

- `Trading/StrategyProof/StrategyRobustnessRunner.cs`
- `Trading/Backtesting/BacktestRobustnessTesting.cs`
- `Trading/StrategyProof/StrategyProofModels.cs`
- `Tests/ForexBot.Tests/Program.cs`

Tests required:

- Out-of-sample split preserves chronological order.
- Walk-forward uses the same execution assumptions in every window.
- Monte Carlo result is deterministic with fixed seed.
- Drawdown breach probability is calculated.
- Parameter sensitivity uses current parameter values as the center and does not select optimized values.
- Robustness verdict fails when out-of-sample expectancy is negative.

Changes strategy logic:

- No.

Expected risk if skipped:

- The strategy may be overfit to one historical sequence and fail in live-demo despite a positive aggregate backtest.

## Patch 8: AI Filter Audit

Current weakness:

- AI confirmation may improve, degrade, or simply reduce trade frequency, but its effect is not measured separately.
- AI may block winners more often than losers, or approve low-quality trades.
- AI output can vary unless historical prompts/responses or frozen fixtures are used.

Exact behavior required:

- Compare deterministic signal alone versus AI-confirmed signal using the same historical candidates and execution assumptions.
- Measure AI-approved trades, AI-blocked trades, AI-HOLD decisions, and unavailable AI decisions.
- Measure whether AI improves or hurts expectancy, profit factor, drawdown, losing streak, and trade frequency.
- Measure blocked winners, blocked losers, approved winners, approved losers, and net opportunity cost.
- Segment AI effect by confidence bucket, reason/source, symbol, session, and spread regime.
- Require frozen historical AI outputs or deterministic AI fixtures for proof runs.
- Mark AI proof as unavailable when historical AI outputs are missing.
- Keep AI out of live edge proof unless its historical impact is measured and reported.

Files likely to modify/create:

- `Trading/StrategyProof/AiFilterAuditService.cs`
- `Trading/StrategyProof/StrategyProofModels.cs`
- `Infrastructure/AI/ClaudeSignalService.cs`, read-only unless adding exportable metadata later
- `Data/Backtesting/AI/`, optional frozen fixture location
- `Tests/ForexBot.Tests/Program.cs`

Tests required:

- Deterministic-only and AI-confirmed paths use identical baseline candidates.
- AI approval/block counts are correct.
- AI-blocked winners and AI-blocked losers are counted separately.
- AI expectancy delta is calculated.
- Missing historical AI data marks AI audit unavailable, not passed.
- AI audit does not call the live AI API during tests.

Changes strategy logic:

- No.

Expected risk if skipped:

- The system may assume AI improves signal quality when it actually reduces expectancy or blocks profitable trades.

## Patch 9: Strategy Edge Verdict Report

Current weakness:

- P2 can generate realistic backtest reports, but there is no final strategy verdict that combines deterministic extraction, realistic costs, segmentation, robustness, and AI audit.
- Pass/fail thresholds are not defined.
- Live-demo readiness is subjective.

Exact behavior required:

- Generate `STRATEGY_EDGE_VERDICT_REPORT.md`.
- Include clear pass/fail/inconclusive verdict.
- Include minimum sample size requirements, preferably per total strategy and per key segment.
- Include minimum profit factor threshold.
- Include minimum expectancy threshold after realistic costs.
- Include maximum drawdown limit.
- Include maximum losing streak tolerance.
- Include out-of-sample and walk-forward pass criteria.
- Include Monte Carlo survival criteria.
- Include AI filter verdict when AI data exists.
- Include live-demo readiness score from 0 to 100.
- Mark the verdict as inconclusive when sample size, market data quality, or AI fixture data is insufficient.
- Include a clear note that positive backtest expectancy is not live proof.

Suggested initial verdict thresholds:

- Minimum completed trades: 300 total, or inconclusive.
- Minimum out-of-sample completed trades: 100, or inconclusive.
- Minimum profit factor after costs: 1.20.
- Minimum expectancy after costs: greater than 0.00 USD and greater than 0.05R.
- Maximum historical drawdown: within configured demo account tolerance.
- Maximum Monte Carlo 5th percentile drawdown: within configured demo account tolerance.
- Worst losing streak: within user-defined psychological and risk tolerance.
- No critical future-data or repainting warnings.
- Live-demo readiness score requires positive out-of-sample expectancy, acceptable cost sensitivity, and no critical audit warnings.

Files likely to modify/create:

- `Trading/StrategyProof/StrategyEdgeVerdictReportBuilder.cs`
- `Trading/StrategyProof/StrategyProofModels.cs`
- `STRATEGY_EDGE_VERDICT_REPORT.md`
- `Tests/ForexBot.Tests/Program.cs`

Tests required:

- Report includes pass/fail/inconclusive verdict.
- Report includes sample size, profit factor, expectancy, drawdown, losing streak, and readiness score.
- Report fails when profit factor is below threshold.
- Report fails when expectancy is negative.
- Report fails or marks inconclusive when sample size is too small.
- Report fails when future-data/repainting audit has critical warnings.
- Report states that backtest expectancy is not live proof.

Changes strategy logic:

- No.

Expected risk if skipped:

- Users may interpret raw backtest output as proof without objective thresholds or readiness criteria.

## Patch 10: Demo Forward-Test Reconciliation

Current weakness:

- Backtest edge is not live proof, even with realistic execution modeling.
- There is no plan to compare strategy proof assumptions against demo-forward results before live use.
- Drift between simulated and demo spread, slippage, rejection rate, session mix, and signal frequency can invalidate the verdict.

Exact behavior required:

- Add a demo-only forward-test reconciliation report.
- Compare demo trades against P3 backtest expectations by symbol, session, spread regime, signal reason, and AI confidence.
- Compare live-demo spread, slippage, commission, latency, rejection rate, fill rate, and trade duration against assumptions.
- Compare demo expectancy, profit factor, drawdown, and losing streak against P3 thresholds.
- Require a minimum demo sample before any live-readiness claim.
- Keep live trading behavior unchanged and keep user approval/risk gates intact.
- Mark strategy live readiness as blocked if demo results materially underperform P3 assumptions.

Files likely to modify/create:

- `Trading/StrategyProof/DemoForwardTestReconciliationService.cs`
- `Trading/StrategyProof/StrategyProofModels.cs`
- `Infrastructure/Persistence/ITradeRepository.cs`
- `Infrastructure/Persistence/SqliteTradeRepository.cs`
- `Tests/ForexBot.Tests/Program.cs`

Tests required:

- Demo reconciliation marks insufficient demo sample as inconclusive.
- Demo spread worse than backtest assumption produces warning.
- Demo slippage worse than backtest assumption produces warning.
- Demo expectancy below threshold fails readiness.
- Demo results are segmented by symbol and session.
- Report does not place trades or alter live execution behavior.

Changes strategy logic:

- No.

Expected risk if skipped:

- A strategy may pass historical proof but fail in broker-demo conditions before the mismatch is detected.

## Recommended P3 Implementation Order

1. Deterministic strategy rule inventory.
2. Deterministic signal extraction adapter.
3. Future-data and repainting audit.
4. Signal quality metrics dataset.
5. Segmented performance analysis.
6. Execution cost sensitivity analysis.
7. Robustness and stability proof.
8. AI filter audit.
9. Strategy edge verdict report.
10. Demo forward-test reconciliation.

Reason for order:

- The exact strategy must be identified before it can be measured.
- Future-data risk must be eliminated before trusting any backtest.
- Signal quality and segmentation should come before final verdict thresholds.
- Cost sensitivity and robustness determine whether the edge survives realistic broker conditions.
- AI should be audited only after deterministic baseline expectancy is known.
- Demo reconciliation comes last because historical edge proof is still not live proof.

## Done Criteria For P3

- Deterministic strategy rules are documented and machine-extractable.
- BUY, SELL, HOLD/no-trade, SL, and TP behavior are reproducible from historical data.
- AI confirmation is measured separately from deterministic strategy logic.
- No critical future-data or repainting risks remain unresolved.
- Realistic net expectancy is positive after spread, slippage, commission, and broker constraints.
- Performance is segmented by symbol, session, spread regime, volatility regime, trend/range regime, AI confidence, and signal source where data exists.
- Cost sensitivity shows whether the edge survives worse broker conditions.
- Out-of-sample, walk-forward, and Monte Carlo results are reported.
- Strategy verdict report gives pass/fail/inconclusive status with objective thresholds.
- Report clearly states that backtest success is realistic simulation only, not live proof.
