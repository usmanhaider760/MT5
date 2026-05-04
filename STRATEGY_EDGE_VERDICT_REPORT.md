# Strategy Edge Verdict Report

Scope: P3 strategy proof reporting only. This report does not change strategy logic, indicators, AI prompts, take-profit logic, live trading behavior, or execution behavior.

## Executive Verdict

- Verdict: Fail
- Reason for verdict: Failed objective criteria: Profit factor after costs 0 is below required 1.2. Expectancy after costs $0 is below required $0.01. Critical repaint/lookahead audit finding is present.
- Report file: `STRATEGY_EDGE_VERDICT_REPORT.md`
- Live-demo readiness score: 0/100

## Required Proof Warnings

- Backtest edge is not live proof.
- Live demo/paper validation is still required.
- AI should not be trusted unless AI impact analysis shows improvement.

## Core Metrics

| Metric | Value |
|---|---:|
| Sample size / completed trades | 0 |
| Total signals | 3 |
| Profit factor after costs | 0 |
| Expectancy after costs | $0 |
| Max drawdown | $0 |
| Worst losing streak | 0 |

## Component Verdicts

| Component | Verdict | Notes |
|---|---|---|
| Cost sensitivity verdict | Inconclusive | Cost sensitivity analysis is missing. |
| Robustness verdict | Inconclusive | Strategy robustness analysis is missing. |
| AI filter verdict | Inconclusive | AI filter impact analysis is missing. |
| Repaint/lookahead audit summary | Fail | Critical repaint/lookahead audit finding is present. |
| Strategy extraction summary | Available | Extraction summary says the base strategy itself produces mostly HOLD. |

## Best/Worst Segments

- Best segment: Not verified
- Worst segment: Not verified

## Failed Criteria

- Profit factor after costs 0 is below required 1.2.
- Expectancy after costs $0 is below required $0.01.
- Critical repaint/lookahead audit finding is present.

## Key Risks

- Cost sensitivity analysis is missing.
- Strategy robustness analysis is missing.
- AI filter impact analysis is missing; AI should remain outside edge proof.
- Completed trade sample is too small: 0 < required 300.
- No completed trades were supplied; win/loss quality metrics were returned as zero.
- One or more outcomes are missing signal source metadata and were grouped as unknown.
- Backtest-positive results can still fail in demo due to broker spread, slippage, commission, latency, rejection rate, and execution path differences.

## Missing Evidence

- Segmented performance analysis is unavailable or failed.
- Cost sensitivity analysis is unavailable or failed.
- Strategy robustness analysis is unavailable or failed.
- AI filter impact analysis is unavailable or failed.

## Assumptions

- ai_disabled_reason: AI disabled: no historical AI decisions were supplied and offline evidence generation must not call external AI APIs.
- broker_connection: No MT5 or live broker connection required
- candidates_generated_from_real_data: 3
- candidate_generation_diagnostic: OFFLINE_AUTO_SCALPING_PRICE_MOVEMENT_CANDIDATES_GENERATED
- candidate_generation_source: offline-auto-scalping-price-movement
- candidate_source: Externally provided candidates
- candles_loaded: 0
- evidence_package_scope: Configured CSV market data was supplied; still not live proof.
- execution_costs: BacktestExecutionCostModel spread, commission, and slippage estimates
- incomplete_signals: 0
- live_trading: Not enabled; no live orders are placed by this command.
- market_data: CSV/provided bid-ask ticks
- market_data_source: Configured CSV market data
- offline_live_logic_differences: Offline mirror uses the live auto-scalping price-movement fallback only. It does not call MT5 GetMarketSnapshot, does not use live M5/M15/H1 indicator snapshot scoring, does not call AI confirmation, does not inspect open positions, and does not execute orders.
- real_strategy_candidates_used: Yes
- sample_fixture_used: No
- simulation_type: Realistic simulation only; not live proof
- skipped_or_hold_signals: 2
- ticks_loaded: 5
