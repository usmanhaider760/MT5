# Realistic Backtest Report

Realistic simulation only. This report is not live proof and does not prove live broker execution quality.

## Summary

| Item | Value |
|---|---:|
| Backtest period | 2026-05-01 08:21:00 UTC to 2026-05-04 08:21:00 UTC |
| Symbols tested | Unavailable |
| Ticks loaded | 0 |
| Candles loaded | 1376 |
| Total candidates | 0 |
| Completed trades | 0 |
| Rejected trades | 0 |
| Unresolved/open trades | 0 |
| Total net profit | 0.00 USD |
| Profit factor | 0.00 |
| Expectancy | 0.00 USD |
| Max drawdown | 0.00 USD |
| Worst losing streak | 0 |
| Total commission | 0.00 USD |
| Total slippage | 0.00 USD |
| Total spread cost | 0.00 USD |
| Backtest status | Failed |
| Failure reason | REALISTIC_BACKTEST_NO_CANDIDATES: At least one trade candidate is required for realistic backtest runner. |

## Candidate Generation

- Market data source: Configured CSV market data
- Candidate generation source: offline-auto-scalping-price-movement
- Sample fixture used: No
- Real strategy candidates used: No
- Candidates generated from real data: 0
- Skipped/hold signals: 1376
- Incomplete signals: 0
- AI disabled reason: AI disabled: no historical AI decisions were supplied and offline evidence generation must not call external AI APIs.
- Offline/live logic differences: Offline mirror uses the live auto-scalping price-movement fallback only. It does not call MT5 GetMarketSnapshot, does not use live M5/M15/H1 indicator snapshot scoring, does not call AI confirmation, does not inspect open positions, and does not execute orders.
- Diagnostic: REAL_MARKET_DATA_LOADED_BUT_NO_STRATEGY_CANDIDATES

## Rejection Breakdown

| Reason | Count |
|---|---:|
| None | 0 |

## Assumptions And Warnings

- ai_disabled_reason: AI disabled: no historical AI decisions were supplied and offline evidence generation must not call external AI APIs.
- broker_connection: No MT5 or live broker connection required
- candidates_generated_from_real_data: 0
- candidate_generation_diagnostic: REAL_MARKET_DATA_LOADED_BUT_NO_STRATEGY_CANDIDATES
- candidate_generation_source: offline-auto-scalping-price-movement
- candidate_source: No candidates supplied
- candles_loaded: 1376
- evidence_package_scope: Configured CSV market data was supplied; still not live proof.
- execution_costs: BacktestExecutionCostModel spread, commission, and slippage estimates
- incomplete_signals: 0
- live_trading: Not enabled; no live orders are placed by this command.
- market_data: CSV/provided OHLC candles
- market_data_source: Configured CSV market data
- offline_live_logic_differences: Offline mirror uses the live auto-scalping price-movement fallback only. It does not call MT5 GetMarketSnapshot, does not use live M5/M15/H1 indicator snapshot scoring, does not call AI confirmation, does not inspect open positions, and does not execute orders.
- real_strategy_candidates_used: No
- sample_fixture_used: No
- simulation_type: Realistic simulation only; not live proof
- skipped_or_hold_signals: 1376
- ticks_loaded: 0
- This is realistic simulation only, not live proof.
- No additional warnings were produced.
