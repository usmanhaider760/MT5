# P4 Live Readiness / Deployment Plan

Sources:

- `P0_SAFETY_PATCH_PLAN.md`
- `P1_EXECUTION_REALISM_PLAN.md`
- `P2_BACKTEST_REALISM_PLAN.md`
- `P3_STRATEGY_EDGE_PROOF_PLAN.md`
- `FINAL_STRATEGY_PROOF_PACKAGE.md`, when generated

Scope: final live-readiness and deployment control layer after P0 safety, P1 execution realism, P2 realistic backtesting, and P3 strategy edge proof.

Rules:

- Planning only.
- Do not enable live trading.
- Do not change strategy logic.
- Do not optimize entries.
- Do not change indicators.
- Do not change AI prompts.
- Do not change take-profit strategy.
- Real-money trading remains blocked unless every required live-readiness gate passes.

## Current Weakness Summary

The system now has foundations for account safety, execution realism, realistic backtesting, and strategy proof reporting. The remaining gap is operational: live mode must remain fail-closed until safety tests pass, strategy evidence is acceptable, demo/paper reconciliation confirms assumptions, broker/EA readiness is verified, runtime monitoring is active, and the user explicitly confirms live enablement.

P4 should not improve the strategy. It should only decide whether deployment is allowed, monitored, alerted, and reversible.

## Patch 1: Final Live Trading Enablement Gate

Current weakness:

- Live mode can be treated as a configuration choice instead of a formal release gate.
- P0/P1/P3 proof status is not yet enforced as a prerequisite for live enablement.
- Explicit user confirmation for live enablement is not tied to proof artifacts.

Exact behavior required:

- Add a fail-closed live-readiness gate evaluated before any live-mode enablement.
- Block live mode unless P0 and P1 test suites have a recorded passing status from the current build or release package.
- Block live mode unless P3 final evidence classification is acceptable, preferably `Proven positive edge` or explicitly approved `Not proven` only for demo/paper, never live.
- Block live mode unless demo/paper reconciliation verdict is `Matches`.
- Block live mode if any P0 kill switch, loss limit, margin, exposure, broker metadata, or news-data safety dependency is unavailable.
- Require explicit user confirmation for live enablement after showing the readiness result and failed criteria.
- Persist live enablement decision with timestamp, user confirmation text/version, proof package hash, config hash, and build/test hash.
- Revoke live enablement automatically when config, strategy proof package, EA build, broker account, or critical dependencies change.

Files likely to modify/create:

- `Application/LiveReadiness/ILiveReadinessGate.cs`
- `Application/LiveReadiness/LiveReadinessGate.cs`
- `Application/LiveReadiness/LiveReadinessModels.cs`
- `Infrastructure/Config/SettingsManager.cs`
- `Infrastructure/Config/AppPaths.cs`
- `UI/Forms/MainForm.cs`
- `Application/Workflows/AutoBotService.cs`
- `Data/Config/live_readiness_state.json`
- `Tests/ForexBot.Tests/Program.cs`

Tests required:

- Live mode blocks when P0 tests are not marked passing.
- Live mode blocks when P1 tests are not marked passing.
- Live mode blocks when P3 classification is `Inconclusive`, `Not proven`, or `Negative edge`.
- Live mode blocks when demo/paper reconciliation is missing or `Diverges`.
- Live mode blocks when kill switch is active.
- Live mode requires explicit user confirmation and records confirmation metadata.
- Changing config/proof/build hash invalidates prior live enablement.

Whether it affects strategy logic:

- No.

Expected risk if skipped:

- The bot may enter live trading despite incomplete safety proof, unverified edge, stale evidence, or missing user acknowledgement.

## Patch 2: Demo Forward-Test Gate

Current weakness:

- Demo/paper results are not yet treated as a required bridge between backtest proof and live deployment.
- Demo sample size, duration, execution drift, and drawdown tolerances are not formal release criteria.

Exact behavior required:

- Add a demo forward-test gate that can return `Pass`, `Fail`, or `Inconclusive`.
- Require a minimum number of paper/demo trades, for example 100 completed trades.
- Require a minimum demo duration, for example 4 calendar weeks and coverage across active sessions.
- Require demo profit factor at or above the configured threshold, for example 1.15 or the P3 threshold.
- Require positive demo expectancy after costs.
- Enforce maximum demo drawdown and maximum losing streak tolerances.
- Enforce maximum spread, slippage, commission, rejection-rate, and latency drift from P2/P3 backtest assumptions.
- Fail or mark inconclusive if demo data is missing costs, durations, spread, slippage, rejection records, or session metadata.
- Allow paper-only mode to continue even when demo gate fails; block live escalation.

Files likely to modify/create:

- `Application/LiveReadiness/DemoForwardTestGate.cs`
- `Application/LiveReadiness/DemoForwardTestModels.cs`
- `Trading/StrategyProof/DemoPaperReconciliationAnalyzer.cs`
- `Infrastructure/Persistence/ITradeRepository.cs`
- `Infrastructure/Persistence/SqliteTradeRepository.cs`
- `UI/Forms/MainForm.cs`
- `Tests/ForexBot.Tests/Program.cs`

Tests required:

- Demo gate passes when sample size, duration, PF, expectancy, drawdown, streak, and drift criteria pass.
- Demo gate returns `Inconclusive` when sample size is too small.
- Demo gate returns `Inconclusive` when demo duration is too short.
- Demo gate fails when demo expectancy is below threshold.
- Demo gate fails when drawdown or losing streak exceeds tolerance.
- Demo gate fails when spread/slippage drift exceeds tolerance.
- Demo gate blocks live escalation but does not block paper mode.

Whether it affects strategy logic:

- No.

Expected risk if skipped:

- A historically profitable backtest may be promoted to live despite demo execution proving materially worse broker conditions.

## Patch 3: Broker / EA Deployment Checklist

Current weakness:

- Broker and EA readiness checks are spread across runtime paths instead of being one auditable deployment checklist.
- Live deployment can fail late if EA, MT5, symbol metadata, OrderCheck, margin estimates, news provider, or VPS latency are unavailable.

Exact behavior required:

- Add a broker/EA deployment checklist service with a structured result model.
- Verify EA is compiled and deployed to the expected MT5 terminal/version.
- Verify MT5 is connected and account mode, server, login, and trade permission status are known.
- Verify symbol metadata is available for all allowed pairs: tick size, point size, digits, lot min/max/step, stop level, freeze level, volume limit, contract size if needed.
- Verify `OrderCheck` is available and returns structured diagnostics.
- Verify margin estimate is available through MT5/EA for each allowed symbol and direction.
- Verify news provider is available when news filtering is required.
- Verify VPS latency and named-pipe round trip are within configured thresholds.
- Fail closed for live mode if any mandatory checklist item fails.

Files likely to modify/create:

- `Infrastructure/Deployment/EaDeploymentVerifier.cs`
- `Infrastructure/MT5/MT5Bridge.cs`
- `MT5_EA/TradingBotEA.mq5`
- `Application/LiveReadiness/BrokerDeploymentChecklist.cs`
- `Application/LiveReadiness/BrokerDeploymentModels.cs`
- `Infrastructure/News/FmpNewsCalendarService.cs`
- `UI/Forms/MainForm.cs`
- `Tests/ForexBot.Tests/Program.cs`

Tests required:

- Checklist passes when EA, MT5, symbol metadata, OrderCheck, margin estimate, news, and latency checks pass.
- Checklist fails when EA deployment status is stale.
- Checklist fails when MT5 is disconnected.
- Checklist fails when symbol metadata is missing for an allowed pair.
- Checklist fails when OrderCheck is unavailable.
- Checklist fails when margin estimate is unavailable.
- Checklist fails when required news provider is unavailable.
- Checklist fails when latency exceeds threshold.

Whether it affects strategy logic:

- No.

Expected risk if skipped:

- Live trading may start with an outdated EA, missing broker metadata, unavailable margin checks, or degraded VPS connectivity.

## Patch 4: Runtime Health Monitor

Current weakness:

- Runtime safety state is checked in multiple places but not consolidated into one live-readiness health stream.
- Spread/slippage drift, rejection rate, VPS latency, kill switch, and daily/weekly loss usage need continuous monitoring once live or demo mode is active.

Exact behavior required:

- Add a runtime monitor that samples and records health snapshots on a fixed cadence.
- Track heartbeat age, MT5 connection status, named-pipe status, account fetch success, symbol metadata freshness, margin-estimate freshness, news-data freshness, VPS latency, spread drift, slippage drift, rejected-order rate, current drawdown, kill-switch state, daily loss usage, weekly loss usage, open exposure, and active trade count.
- Return `Healthy`, `Degraded`, or `Critical` status with specific reason codes.
- In live mode, critical runtime status must pause new entries and may trigger kill switch depending on severity.
- In demo/paper mode, critical status must be visible and logged but should not imply live trading is safe.
- Persist recent health snapshots for audit and dashboard display.

Files likely to modify/create:

- `Application/Monitoring/RuntimeHealthMonitor.cs`
- `Application/Monitoring/RuntimeHealthModels.cs`
- `Application/Workflows/AutoBotService.cs`
- `Infrastructure/MT5/MT5Bridge.cs`
- `Infrastructure/Persistence/ITradeRepository.cs`
- `Infrastructure/Persistence/SqliteTradeRepository.cs`
- `Infrastructure/Logging/`
- `UI/Forms/MainForm.cs`
- `Tests/ForexBot.Tests/Program.cs`

Tests required:

- Monitor reports healthy when all sampled dependencies are fresh and within thresholds.
- Monitor reports degraded on elevated spread/slippage drift.
- Monitor reports critical on MT5 disconnect.
- Monitor reports critical on stale margin estimate in live mode.
- Monitor reports critical on active kill switch.
- Monitor calculates daily/weekly loss usage from supplied state.
- Critical live health blocks new entries.

Whether it affects strategy logic:

- No.

Expected risk if skipped:

- The bot may keep accepting live entries while broker connectivity, data freshness, execution quality, or risk usage has degraded.

## Patch 5: Safety Alerting Layer

Current weakness:

- Critical runtime events may be logged but not surfaced as actionable alerts.
- Repeated warnings can be missed during unattended VPS operation.

Exact behavior required:

- Add an alerting layer with severity levels: `Info`, `Warning`, `Critical`.
- Alert on kill-switch triggered, live gate blocked, MT5 disconnected, news data unavailable, account data unavailable, margin data unavailable, symbol metadata unavailable, abnormal spread, abnormal slippage, repeated order rejection, daily/weekly loss usage above thresholds, drawdown threshold breach, stale heartbeat, and failed emergency close.
- Deduplicate repeated alerts with cooldowns while preserving count and last occurrence.
- Persist alert history for audit.
- Support UI alert display first; external notification channels can be planned but must not be required for core safety.
- Live mode critical alerts should pause new live entries until cleared or resolved.

Files likely to modify/create:

- `Application/Alerts/IAlertService.cs`
- `Application/Alerts/AlertService.cs`
- `Application/Alerts/AlertModels.cs`
- `Application/Monitoring/RuntimeHealthMonitor.cs`
- `Application/Workflows/AutoBotService.cs`
- `Infrastructure/Notifications/`
- `Infrastructure/Persistence/ITradeRepository.cs`
- `UI/Forms/MainForm.cs`
- `Tests/ForexBot.Tests/Program.cs`

Tests required:

- Critical kill-switch alert is emitted and persisted.
- MT5 disconnect alert is emitted with critical severity.
- News unavailable alert is emitted when news filtering is required.
- Margin unavailable alert is emitted in live mode.
- Spread/slippage abnormal alerts include observed and threshold values.
- Repeated order rejection alert triggers after configured count.
- Alert deduplication suppresses noisy repeats but increments occurrence count.

Whether it affects strategy logic:

- No.

Expected risk if skipped:

- Critical live-safety failures may occur silently or be buried in logs until account damage has already happened.

## Patch 6: Operational Dashboard / Readiness Report

Current weakness:

- The user lacks one clear operational view showing whether live trading is blocked, why, and what evidence supports readiness.
- Risk usage, open exposure, recent trades, rejected trades, safety blocks, and P3 classification are not presented as one deployment report.

Exact behavior required:

- Add a live readiness dashboard/report view.
- Show live readiness status, failed gates, current risk usage, daily/weekly loss usage, open exposure by symbol, active trade count, kill-switch state, MT5/account connection status, EA deployment status, recent trades, rejected trades, safety blocks, alert status, and strategy evidence classification.
- Show P3 final proof package classification and demo/paper reconciliation verdict.
- Show broker/EA checklist status and runtime health status.
- Make live enablement controls unavailable until all required gates pass.
- Export a markdown or JSON operational readiness report for audit.

Files likely to modify/create:

- `UI/Forms/MainForm.cs`
- `UI/Controls/LiveReadinessPanel.cs`
- `Application/LiveReadiness/LiveReadinessReportService.cs`
- `Application/LiveReadiness/LiveReadinessReportModels.cs`
- `Application/Monitoring/RuntimeHealthModels.cs`
- `Application/Alerts/AlertModels.cs`
- `Tests/ForexBot.Tests/Program.cs`

Tests required:

- Readiness report includes live gate status and failed criteria.
- Readiness report includes current risk usage and exposure.
- Readiness report includes recent completed and rejected trades.
- Readiness report includes safety blocks and active alerts.
- Readiness report includes evidence classification.
- UI model disables live enablement when gates fail.
- Report export contains no live execution side effects.

Whether it affects strategy logic:

- No.

Expected risk if skipped:

- The user may enable or operate live trading without seeing the full safety, evidence, and broker-readiness picture.

## Patch 7: Tiny-Live Rollout And Automatic Rollback

Current weakness:

- There is no formal staged rollout from paper to demo to tiny live, and no automatic rollback rules if live behavior diverges.
- Scaling up can become subjective or too fast.

Exact behavior required:

- Define staged modes: `PaperOnly`, `DemoForwardTest`, `TinyLive`, `LimitedLive`, `Blocked`.
- Paper-only remains the default and safest mode.
- Demo requires P0/P1 tests passing, broker/EA checklist passing for demo, and P3 proof package not negative.
- Tiny live requires all live gates passing, demo reconciliation `Matches`, explicit user confirmation, and minimal lot/risk settings.
- Tiny live must enforce reduced max lot, reduced daily/weekly loss caps, reduced exposure caps, and reduced max open trades.
- Scale-up requires minimum tiny-live sample size, minimum duration, acceptable PF/expectancy, drawdown below threshold, no kill-switch events, no unresolved critical alerts, and execution drift within tolerance.
- Automatic rollback to paper/demo when drawdown, losing streak, rejected order rate, spread/slippage drift, latency, data availability, or proof invalidation thresholds are breached.
- Rollback must persist state and block re-enable until explicit review.

Files likely to modify/create:

- `Application/Deployment/RolloutStateMachine.cs`
- `Application/Deployment/RolloutModels.cs`
- `Application/LiveReadiness/LiveReadinessGate.cs`
- `Application/Monitoring/RuntimeHealthMonitor.cs`
- `Application/Workflows/AutoBotService.cs`
- `Infrastructure/Config/SettingsManager.cs`
- `UI/Forms/MainForm.cs`
- `Tests/ForexBot.Tests/Program.cs`

Tests required:

- Default deployment state is `PaperOnly` or `Blocked`, never live.
- Demo state requires required gates but not live enablement.
- Tiny live state requires all live gates plus explicit confirmation.
- Tiny live applies reduced risk limits.
- Scale-up is blocked without minimum tiny-live sample and duration.
- Runtime critical breach rolls back to paper/demo.
- Rollback state persists and requires explicit review before re-enable.

Whether it affects strategy logic:

- No.

Expected risk if skipped:

- A strategy may jump from paper/demo directly to meaningful real-money exposure without measured live drift controls or rollback.

## Patch 8: Final Go/No-Go Checklist

Current weakness:

- There is no single final checklist that proves all safety, evidence, broker, monitoring, alerting, dashboard, and rollout gates are satisfied before real-money trading.

Exact behavior required:

- Add a final go/no-go checklist service and report.
- Checklist must require:
  - P0 safety tests passing.
  - P1 execution realism tests passing.
  - P2 realistic backtest report generated without MT5/live dependency.
  - P3 final proof package classification acceptable.
  - Demo/paper reconciliation `Matches`.
  - No critical repaint/lookahead findings.
  - Broker/EA checklist passing.
  - Runtime health monitor active and healthy.
  - Alerting active.
  - Operational dashboard/report available.
  - Kill switch inactive but tested.
  - Daily/weekly loss limits configured.
  - Per-symbol exposure and margin gates configured.
  - Explicit user confirmation captured.
- Real-money trading must remain blocked unless every mandatory item is `Go`.
- Produce `LIVE_GO_NO_GO_REPORT.md` or equivalent export.

Files likely to modify/create:

- `Application/LiveReadiness/FinalGoNoGoChecklist.cs`
- `Application/LiveReadiness/FinalGoNoGoModels.cs`
- `Application/LiveReadiness/LiveReadinessGate.cs`
- `Application/LiveReadiness/LiveReadinessReportService.cs`
- `UI/Forms/MainForm.cs`
- `Tests/ForexBot.Tests/Program.cs`

Tests required:

- Checklist returns no-go when any mandatory item fails.
- Checklist returns go only when every mandatory item passes.
- Checklist includes failed item names and remediation text.
- Checklist blocks real-money trading when P3 classification is not acceptable.
- Checklist blocks when demo reconciliation is not `Matches`.
- Checklist blocks when broker/EA checklist fails.
- Checklist blocks when explicit user confirmation is missing.
- Exported report includes final go/no-go result and evidence references.

Whether it affects strategy logic:

- No.

Expected risk if skipped:

- The final deployment decision may be made informally, allowing live trading when one required safety, proof, broker, or operational gate is still missing.

## Recommended P4 Implementation Order

1. Final live trading enablement gate.
2. Demo forward-test gate.
3. Broker/EA deployment checklist.
4. Runtime health monitor.
5. Safety alerting layer.
6. Operational dashboard/readiness report.
7. Tiny-live rollout and automatic rollback.
8. Final go/no-go checklist.

Reason for order:

- Live must fail closed first.
- Demo proof should be formal before broker deployment and rollout.
- Broker/EA checks and runtime monitoring provide the dependency state needed by alerts and dashboards.
- Rollout and final go/no-go should come last because they compose all previous gates.

## Done Criteria For P4

- Live mode cannot be enabled unless P0/P1 tests pass, P3 evidence is acceptable, demo/paper reconciliation passes, broker/EA checklist passes, runtime health is healthy, alerting is active, and the user explicitly confirms.
- Demo forward-test requirements are objective and enforced.
- Broker/EA deployment readiness is auditable.
- Runtime monitoring continuously detects connection, latency, spread, slippage, rejection, drawdown, kill-switch, and loss-usage issues.
- Critical alerts are surfaced and persisted.
- Operational dashboard/report shows readiness, risk usage, exposure, recent trades, rejections, safety blocks, and evidence classification.
- Tiny-live rollout is staged, reduced-risk, measurable, and reversible.
- Final go/no-go checklist blocks real-money trading unless every mandatory item passes.
