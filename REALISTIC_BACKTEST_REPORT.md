# Realistic Backtest Report

Realistic simulation only. This report is not live proof and does not prove live broker execution quality.

## Summary

| Item | Value |
|---|---:|
| Backtest period | 2026-05-03 22:03:00 UTC to 2026-05-04 08:45:00 UTC |
| Symbols tested | XAUUSD |
| Ticks loaded | 0 |
| Candles loaded | 643 |
| Total candidates | 20 |
| Completed trades | 20 |
| Rejected trades | 0 |
| Unresolved/open trades | 0 |
| Total net profit | -16.16 USD |
| Profit factor | 0.72 |
| Expectancy | -0.81 USD |
| Max drawdown | 20.88 USD |
| Worst losing streak | 2 |
| Total commission | 0.00 USD |
| Total slippage | 0.00 USD |
| Total spread cost | 6.16 USD |
| Backtest status | Success |

## Candidate Generation

- Market data source: Configured CSV market data
- Candidate generation source: offline-auto-scalping-price-movement
- Sample fixture used: No
- Real strategy candidates used: Yes
- Candidates generated from real data: 20
- Skipped/hold signals: 623
- Incomplete signals: 0
- AI disabled reason: AI disabled: no historical AI decisions were supplied and offline evidence generation must not call external AI APIs.
- Offline/live logic differences: Offline mirror uses the live auto-scalping price-movement fallback only. It does not call MT5 GetMarketSnapshot, does not use live M5/M15/H1 indicator snapshot scoring, does not call AI confirmation, does not inspect open positions, and does not execute orders.
- Diagnostic: OFFLINE_AUTO_SCALPING_PRICE_MOVEMENT_CANDIDATES_GENERATED

## Rejection Breakdown

| Reason | Count |
|---|---:|
| None | 0 |

## Assumptions And Warnings

- ai_disabled_reason: AI disabled: no historical AI decisions were supplied and offline evidence generation must not call external AI APIs.
- broker_connection: No MT5 or live broker connection required
- candidates_generated_from_real_data: 20
- candidate_generation_diagnostic: OFFLINE_AUTO_SCALPING_PRICE_MOVEMENT_CANDIDATES_GENERATED
- candidate_generation_source: offline-auto-scalping-price-movement
- candidate_source: Externally provided candidates
- candles_loaded: 643
- evidence_package_scope: Configured CSV market data was supplied; still not live proof.
- execution_costs: BacktestExecutionCostModel spread, commission, and slippage estimates
- incomplete_signals: 0
- live_trading: Not enabled; no live orders are placed by this command.
- market_data: CSV/provided OHLC candles
- market_data_source: Configured CSV market data
- offline_live_logic_differences: Offline mirror uses the live auto-scalping price-movement fallback only. It does not call MT5 GetMarketSnapshot, does not use live M5/M15/H1 indicator snapshot scoring, does not call AI confirmation, does not inspect open positions, and does not execute orders.
- real_strategy_candidates_used: Yes
- sample_fixture_used: No
- simulation_type: Realistic simulation only; not live proof
- skipped_or_hold_signals: 623
- ticks_loaded: 0
- This is realistic simulation only, not live proof.
- Warning: OFFLINE-SCALP-000001: Margin simulation was not supplied; margin requirement is unverified.
- Warning: OFFLINE-SCALP-000002: Margin simulation was not supplied; margin requirement is unverified.
- Warning: OFFLINE-SCALP-000003: Margin simulation was not supplied; margin requirement is unverified.
- Warning: OFFLINE-SCALP-000004: Margin simulation was not supplied; margin requirement is unverified.
- Warning: OFFLINE-SCALP-000005: Margin simulation was not supplied; margin requirement is unverified.
- Warning: OFFLINE-SCALP-000006: Margin simulation was not supplied; margin requirement is unverified.
- Warning: OFFLINE-SCALP-000007: Margin simulation was not supplied; margin requirement is unverified.
- Warning: OFFLINE-SCALP-000008: Margin simulation was not supplied; margin requirement is unverified.
- Warning: OFFLINE-SCALP-000009: Margin simulation was not supplied; margin requirement is unverified.
- Warning: OFFLINE-SCALP-000010: Margin simulation was not supplied; margin requirement is unverified.
- Warning: OFFLINE-SCALP-000011: Margin simulation was not supplied; margin requirement is unverified.
- Warning: OFFLINE-SCALP-000012: Margin simulation was not supplied; margin requirement is unverified.
- Warning: OFFLINE-SCALP-000013: Margin simulation was not supplied; margin requirement is unverified.
- Warning: OFFLINE-SCALP-000014: Margin simulation was not supplied; margin requirement is unverified.
- Warning: OFFLINE-SCALP-000015: Margin simulation was not supplied; margin requirement is unverified.
- Warning: OFFLINE-SCALP-000016: Margin simulation was not supplied; margin requirement is unverified.
- Warning: OFFLINE-SCALP-000017: Margin simulation was not supplied; margin requirement is unverified.
- Warning: OFFLINE-SCALP-000018: Margin simulation was not supplied; margin requirement is unverified.
- Warning: OFFLINE-SCALP-000019: Margin simulation was not supplied; margin requirement is unverified.
- Warning: OFFLINE-SCALP-000020: Margin simulation was not supplied; margin requirement is unverified.
- Warning: R-multiple data is unavailable; average R multiple was not calculated.
