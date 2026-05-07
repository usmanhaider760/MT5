# Live Trade Rules Monitor & Control - Audit Report

Date: 2026-05-06

## 1. Executive Summary

The implementation is compile-safe and includes the core architecture requested by the master plan: fixed rule catalog, context model, runtime snapshot service, monitor form, disabled-rule semantics, contextual log/position/signal openings, export, snapshot history, and targeted tests.

Final verdict: **MOSTLY COMPLETE**.

The feature is not fully complete against the detailed UI/runtime requirements. The largest gaps are UI fidelity and live data coverage: rule rows are simple `DataGridView` text cells instead of typed controls with sliders/checklists, groups are filter-based rather than collapsible, Live Overview is a filtered rule grid rather than cards, and several requested runtime/live sources are placeholders or not wired.

No code changes were made during this audit except creating this report.

## 2. Pass/Fail Table By Section

| # | Section | Result | Notes |
|---|---|---|---|
| 1 | Contextual Opening | PARTIAL | Context menu and signal/position openings exist. Button text lacks gear. Buttons were added to the bot controls row, which may be considered too generic. Eligible log filter misses `[SCALP_DECISION]` style logs. |
| 2 | Context Resolution | PARTIAL | `TradeRulesContext` has required fields. Panel strategy resolution works. Running trade strategy only parses comment text, not session manager mapping. Unknown view exists. |
| 3 | UI Tabs | PASS | Scalping, Normal, and Unknown tab sets match required names. |
| 4 | Header | PARTIAL | Shows account number/server/balance/equity/free margin/margin/floating P/L and position fields. Header does not show broker/name or request id. Export includes broker/name. No secret fields intentionally shown. |
| 5 | Monitor/Edit Mode | PASS | Running context starts monitor-only; editing disabled until `Enable Runtime Editing`. Non-running context starts editable. No auto-save on preview. |
| 6 | Apply/Save/Reset | PARTIAL | Apply/save/reset exist and do not modify MT5 SL/TP. Reset only changes in-memory preview/enabled state; standard/default coverage is incomplete. Whole visible reset can affect all currently filtered visible rows. |
| 7 | Live Refresh | PASS | WinForms timer interval is 1000 ms. Main log is not written per refresh. |
| 8 | Live Data Sources | PARTIAL | MT5 account/symbol/positions, config, pair settings, scalping/normal running flags are used. BUY/SELL scores, cooldown remaining, session P/L, AutoBotService audit, news calendar details, AI/ADX snapshots are mostly placeholders or absent. Missing sources return `NOT_CHECKED` with reason. |
| 9 | Top Decision Summary | PASS | Summary has decision, main blocking rule, risk, pass/warn/block/disabled/disabled-but-would-block counts and color logic. |
| 10 | Live Overview Cards | FAIL | Live Overview is a filtered grid, not cards. ADX/news/cooldown/session values are not fully live. |
| 11 | Rule Rows | PARTIAL | Rows show code/name/source/standard/configured/preview/live/range/feedback/status/reason/reset. No checkbox column, typed numeric input, slider, enum dropdown, list editor, or true per-row button control. |
| 12 | Advanced Details | PARTIAL | Technical fields are separated in a lower details pane on selection. It is not expandable per row. |
| 13 | Editable Controls | FAIL | Editing is via text cells. No `NumericUpDown`, `TrackBar`, checkbox, dropdown, or checklist editor per value type. |
| 14 | Grouping | PARTIAL | Group filter plus Enable Group/Disable Group exists. Groups are not collapsible and are global to current filtered rules, not per-tab group panels. No global Disable All button found. |
| 15 | Disable Confirmation | PASS/PARTIAL | Individual critical disable asks confirmation. Group disable asks confirmation. Normal disable has no confirmation. Group disable does not separately identify critical rules. |
| 16 | Disabled Rule Behavior | PARTIAL | Disabled rules stay visible and would-have-result semantics exist. User-disabled group rows do not immediately recalculate `DISABLED` status until later apply/snapshot flow. |
| 17 | Decision Audit | PARTIAL | Decision Audit tab and filters exist. Ordering follows catalog order. Advanced details appear in side pane. It includes only catalog `Decision Audit` rules, not a parsed live AutoBot audit stream. |
| 18 | Search / Filter | PASS | Search covers code, name, function, variable, result, source, category. |
| 19 | Snapshot History | PASS | In-window history exists, keeps last 200, clear button exists, and main log is not spammed every refresh. |
| 20 | Export | PARTIAL | JSON/TXT export exist and include context/account/summary/rules/history. JSON redacts secret-like keys. TXT export has no generic redaction pass and omits explicit live overview cards and last checked time. |
| 21 | Fixed Rule Catalog | PASS | Catalog exists in `Application/TradeRules/TradeRuleCatalog.cs`; all expected codes are present and stable. |
| 22 | Logging | PARTIAL | Monitor logs and some scalping/trade logs include rule codes. Execution audit examples are not fully implemented; rejected logs use generic `EXEC-TRADE-REJECTED` rather than main blocking rule; no-trade coverage is incomplete. |
| 23 | Hardcoded Values | PASS | `MaxSpreadPercentOfTp` is config-driven with default/backward-compatible value and settings JSON entries. |
| 24 | Runtime Control | PARTIAL | Runtime apply mutates config objects for next checks. Many rules are effectively monitor-only but are not clearly marked as monitor-only in UI. Pair defaults only cover strategy pair dictionaries, not `PairSettings` values. |
| 25 | Regression Checks | PARTIAL | Project build passes. Test suite still has known failures. Log Detail double-click remains wired. Central gate test passes. |

## 3. Evidence: Files / Classes / Methods Checked

- `Docs/Codex_Live_Trade_Rules_Monitor_Master_Plan.md`
- `Docs/LiveTradeRulesMonitor_DevelopmentChecklist.md`
- `Application/TradeRules/TradeRuleModels.cs`
- `Application/TradeRules/TradeRuleCatalog.cs`
- `Application/TradeRules/TradeRuleStatusEvaluator.cs`
- `Application/TradeRules/TradeRulesRuntimeSnapshotService.cs`
- `Application/TradeRules/TradeRulesRuntimeControlService.cs`
- `Application/TradeRules/TradeRulesExportService.cs`
- `UI/Forms/LiveTradeRulesMonitorControlForm.cs`
- `UI/Forms/MainForm.cs`
- `UI/Forms/MainForm.Design.cs`
- `Domain/Models/Models.cs`
- `Data/Config/settings.json`
- `Trading/Scalping/ScalpingSessionService.cs`
- `Application/Workflows/AutoBotService.cs`
- `Tests/ForexBot.Tests/Program.cs`

Specific evidence:

- Log double-click preserved: `MainForm.TxtLog_DoubleClick()` still calls `ShowSelectedLogDetail()`.
- Context menu opening: `MainForm.ConfigureRulesMonitorContextMenus()`.
- Context model: `TradeRulesContext` contains pair, strategy, ticket, trade type, request id, running flag, source, raw log line.
- Tabs: `LiveTradeRulesMonitorControlForm.BuildTabs()`.
- Timer: `_refreshTimer = new() { Interval = 1000 }`.
- Disabled semantics: `TradeRuleStatusEvaluator.ApplyEnabledState()` and `BuildSummary()`.
- Export redaction: `TradeRulesExportService.RedactSecrets()`.
- Rule catalog: `TradeRuleCatalog.FixedItems`.

## 4. Missing / Incomplete Items

- Required button labels are `Scalping Rules` and `Normal Rules`, not `⚙ Scalping Rules` / `⚙ Normal Rules`.
- Live Overview is not implemented as cards; it is a filtered rules grid.
- Rule rows do not use typed controls: no checkbox column, `NumericUpDown`, `TrackBar`, enum dropdown, or checklist/list editor.
- Groups are not collapsible.
- Advanced Details are not expandable per row; they are shown in a separate side/lower pane.
- Header does not show broker/name or request id.
- Snapshot service does not pull detailed scalping runtime values such as elapsed time, cooldown remaining, session P/L, BUY/SELL scores, selected direction, and no-trade reason.
- Snapshot service does not pull full normal runtime state.
- AutoBotService audit, news calendar, AI snapshot, ADX, market-structure values are not truly integrated; several are placeholders or `NOT_CHECKED`.
- Pair default save does not update `PairSettings` values despite Pair Rules being editable in the grid.
- Monitor-only/runtime-only limitations are not clearly marked per rule.
- TXT export lacks generic secret redaction and does not include `LastCheckedAtUtc`.
- `EXEC-TRADE-ACCEPTED` live value has a typo in snapshot service lookup: `EXEC-TRADE_ACCEPTED`.

## 5. Incorrect Or Risky Items

- `ToggleVisibleGroup()` sets `IsEnabled` but does not call `TradeRuleStatusEvaluator.ApplyEnabledState()` or audit each rule; the grid may not immediately show `DISABLED` / would-have-result after group disable.
- `Grid_CellEndEdit()` handles critical disable confirmation, but the grid uses text cells rather than checkboxes; user input like accidental text can be parsed unexpectedly.
- Reset logs as `ValueChanged` after changing preview state, which is semantically confusing.
- `SaveStrategyDefaultsAsync()` applies all common values even when the window is opened in Unknown context; this may update global/common settings from a context that should probably be monitor-only or restricted.
- Rules Monitor eligibility does not include `[SCALP_DECISION]` unless another eligible token is present.
- Execution/trade rejected logs do not consistently include the specific main blocking rule and friendly name required by examples.

## 6. Build Result

Required command:

```text
dotnet build
```

Result:

```text
Failed: MSB1011 Specify which project or solution file to use because this folder contains more than one project or solution file.
```

Project-specific verification:

```text
dotnet build MT5TradingBot.csproj -p:UseAppHost=false
```

Result:

```text
Passed: 0 warnings, 0 errors.
```

## 7. Test Result

Required command:

```text
dotnet run --project Tests/ForexBot.Tests
```

Initial sandbox run failed due NuGet repository signature SSL/network access. Re-run outside sandbox completed.

Result:

```text
322/338 tests passed.
```

The four Live Trade Rules Monitor tests passed:

- `trade rule catalog has unique fixed codes`
- `disabled trade rule preserves would-have result`
- `trade rule summary counts disabled would block`
- `trade rules export redacts secrets`

Remaining failures are in existing live-gate/market-data expectation areas:

- Daily/weekly loss code expectations.
- Cross-midnight session spread expectation.
- Retry symbol/spread refetch counts.
- Market data CLI started banner.
- Missing account expected `NO_ACCOUNT`, got `NO_SYMBOL_DATA`.
- News unavailable expected `NEWS_UNAVAILABLE`, got `NEWS_BLACKOUT`.

## 8. Recommended Fixes

Small safe fixes to consider first:

- Change button labels to `⚙ Scalping Rules` and `⚙ Normal Rules`.
- Add `[SCALP_DECISION]` to `IsRulesMonitorEligibleLog()`.
- Fix `EXEC-TRADE_ACCEPTED` typo to `EXEC-TRADE-ACCEPTED`.
- Add broker/name and request id to the monitor header.
- In group enable/disable, call disabled-state evaluator per rule and log each changed rule or a clearer group audit entry.
- Add `LastCheckedAtUtc` to TXT export and run a simple redaction pass over TXT output.
- Mark rules with no runtime apply path as monitor-only in `Reason` or `ActualEffect`.

Larger follow-up work:

- Replace text-cell editing with value-type-specific controls.
- Implement actual Live Overview cards.
- Implement collapsible group panels.
- Wire detailed scalping runtime, normal runtime, news, AI/ADX, and AutoBot audit sources.
- Improve execution/rejection/no-trade logs to include specific `Rule=<code> <friendly name>` / `MainRule=<code> <friendly name>`.

## 9. Final Verdict

**MOSTLY COMPLETE**

The implementation is safe enough as a monitor/control foundation and does not appear to bypass central execution or modify open MT5 SL/TP automatically. It is not fully complete against the master plan’s detailed UI and live-source requirements. The feature should be treated as an operational diagnostic/control MVP until the missing UI controls, live data integrations, and logging specificity are completed.
