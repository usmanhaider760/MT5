# Live Trade Rules Monitor & Control Development Checklist

## Phase 0 - Repository Inspection

Status: DONE

Discovered files/classes:

- Main WinForms surface: `UI/Forms/MainForm.cs`, `UI/Forms/MainForm.Designer.cs`, `UI/Forms/MainForm.Design.cs`
- Log Detail window/code: `UI/Controls/AppLogDetailBox.cs`, `UI/Controls/LogLineExplainer.cs`
- Existing log double-click behavior: `UI/Forms/MainForm.cs` wires `_txtLog.DoubleClick += TxtLog_DoubleClick`; `TxtLog_DoubleClick` calls `ShowSelectedLogDetail()`
- Scalping panel/control code: `UI/Forms/MainForm.cs` and `UI/Forms/MainForm.Design.cs`; scalping start/review helpers live near `StartAutoScalpingFromReviewAsync`, saved config helpers, and `_btnStopScalping`
- Normal trading panel/control code: `UI/Forms/MainForm.cs`; normal settings helpers include `GetSavedNormalTradingSettingsForPair`, `SaveNormalTradingSettingsForPairAsync`, and review/apply helpers
- Running positions grid: `UI/Forms/MainForm.cs` `RefreshPositionsAsync`, `_gridPos`, `CloseSelectedAsync`; layout in `UI/Forms/MainForm.Designer.cs` / `MainForm.Design.cs`
- Signal card/feed row code: `UI/Forms/MainForm.cs` `EnsureSignalFeedRowForPair`, `BuildPairAnalysisCard`, `AddOrUpdateSignalCard`, signal feed refresh/load helpers
- Central execution gate: `Application/Workflows/AutoBotService.cs` `ExecuteTradeWithValidationAsync` -> `ExecuteTradeWithValidationCoreAsync`; execution audit in `RunExecutionGateAuditAsync` and `TRADE_AUDIT_FULL` logging
- Settings models: `Domain/Models/Models.cs` (`AppSettings`, `BotConfig`, `CommonTradingSettings`, `ScalpingConfig`, `ScalpingSettings`, `NormalTradingSettings`, `PairTradingSettings`)
- Config load/save logic: `Infrastructure/Config/SettingsManager.cs`; runtime config file `Data/Config/settings.json`
- Pair settings service: `Trading/PairSettings/PairSettingsService.cs`
- Scalping session service/manager: `Trading/Scalping/ScalpingSessionService.cs`, `Trading/Scalping/ScalpingTradeManager.cs`, `Trading/Scalping/IScalpingSessionService.cs`
- Normal trade manager: `Trading/NormalTrading/NormalTradeManager.cs`
- MT5 bridge live data methods: `Infrastructure/MT5/MT5Bridge.cs` (`GetAccountInfoAsync`, `GetPositionsAsync`, `GetSymbolInfoAsync`, `GetMarketSnapshotAsync`, `TryCheckOrderAsync`, `TryGetMarginEstimateAsync`)
- Risk validation: `Trading/RiskManagement/RiskManager.cs`
- News filter/calendar: `Infrastructure/News/NewsFilterService.cs`, `Infrastructure/News/INewsCalendarService.cs`
- Existing tests: `Tests/ForexBot.Tests/Program.cs`, project `Tests/ForexBot.Tests/ForexBot.Tests.csproj`

Notes:

- Do not change log row double-click; add Rules Monitor access through right-click/menu/icon in later phases.
- Known hardcoded spread-percent values found in `UI/Forms/MainForm.cs` (`ScalpingMaxSpreadPercentOfTp = 20.0`) and `Trading/Scalping/ScalpingSessionService.cs` (`ProfessionalMaxSpreadPercentOfTp = 20.0`) for Phase 2 inspection/update.
- No behavior changed in Phase 0.

Build/test verification:

- `dotnet build`: could not run from repo root because both `MT5TradingBotPro.sln` and `MT5TradingBot.csproj` exist; MSBuild requested an explicit target.
- `dotnet build MT5TradingBot.csproj -p:UseAppHost=false`: passed, 0 warnings, 0 errors.
- `dotnet run --project Tests/ForexBot.Tests/ForexBot.Tests.csproj -p:UseAppHost=false`: failed baseline, 265/334 passed. Failures appear pre-feature and mostly return `SESSION_CLOSED` where tests expect more specific live-gate rejection codes.

## Phase 1 - Add Rule Catalog and Core Models

Status: DONE

Changed files:

- Added `Application/TradeRules/TradeRuleModels.cs`
- Added `Application/TradeRules/TradeRuleCatalog.cs`

Build/test verification:

- `dotnet build MT5TradingBot.csproj -p:UseAppHost=false`: passed, 0 warnings, 0 errors.
- `dotnet run --project Tests/ForexBot.Tests/ForexBot.Tests.csproj -p:UseAppHost=false`: failed baseline, 265/334 passed. Failure pattern unchanged from Phase 0.

## Phase 2 - Config Support for Rule Enable/Disable and Hardcoded Values

Status: DONE

Changed files:

- Updated `Domain/Models/Models.cs`
- Updated `UI/Forms/MainForm.cs`
- Updated `Trading/Scalping/ScalpingSessionService.cs`
- Updated `Data/Config/settings.json`

New config properties:

- `Bot.trade_rule_enabled`: dictionary for per-rule enable/disable state.
- `Bot.scalping.max_spread_percent_of_tp`: replaces hardcoded 20.0 spread-percent-of-TP guardrail, default 20.0.
- Pair-specific scalping entries can also carry `max_spread_percent_of_tp`.

Build/test verification:

- `dotnet build MT5TradingBot.csproj -p:UseAppHost=false`: passed, 0 warnings, 0 errors.
- `dotnet run --project Tests/ForexBot.Tests/ForexBot.Tests.csproj -p:UseAppHost=false`: failed baseline, 265/334 passed. Failure pattern unchanged from Phase 0.

## Phase 3 - Runtime Snapshot Service

Status: DONE

Changed files:

- Updated `Application/TradeRules/TradeRuleModels.cs`
- Added `Application/TradeRules/TradeRulesRuntimeSnapshotService.cs`

Notes:

- Snapshot service builds catalog-backed rule rows from config plus MT5 account/symbol/position data when a bridge is connected.
- Inaccessible sources are represented as `NOT_CHECKED` with a reason instead of failing or changing trade flow.
- AutoBotService last audit, news calendar, and detailed scalping score runtime are not yet directly wired; rows are present and safely marked unavailable/not checked where needed.

Build/test verification:

- `dotnet build MT5TradingBot.csproj -p:UseAppHost=false`: passed, 0 warnings, 0 errors.
- `dotnet run --project Tests/ForexBot.Tests/ForexBot.Tests.csproj -p:UseAppHost=false`: failed baseline, 265/334 passed. Failure pattern unchanged from Phase 0.

## Phase 4 - Disabled Rule Calculation Semantics

Status: DONE

Changed files:

- Added `Application/TradeRules/TradeRuleStatusEvaluator.cs`
- Updated `Application/TradeRules/TradeRulesRuntimeSnapshotService.cs`

Notes:

- Enabled rules keep their calculated PASS/WARNING/BLOCK/NOT_CHECKED result.
- Disabled rules display `DISABLED`, preserve `WouldHaveResult`, and are counted separately.
- Summary counters now include Passed, Warning, Blocked, Disabled, and Disabled But Would Block.
- Disabled-but-would-block raises summary risk to High.

Build/test verification:

- `dotnet build MT5TradingBot.csproj -p:UseAppHost=false`: passed, 0 warnings, 0 errors.
- `dotnet run --project Tests/ForexBot.Tests/ForexBot.Tests.csproj -p:UseAppHost=false`: failed baseline, 265/334 passed. Failure pattern unchanged from Phase 0.

## Phase 5 - Main Monitor Form UI Skeleton

Status: DONE

Changed files:

- Added `UI/Forms/LiveTradeRulesMonitorControlForm.cs`

Notes:

- Added main `Live Trade Rules Monitor & Control` form skeleton.
- Header shows account/trade/position context when available.
- Top decision summary shows current decision, blocking rule, risk, and counters.
- Strategy-aware tabs are created for Scalping, Normal, or Unknown contexts.
- 1-second timer refreshes snapshots while open.
- Running trade context starts in monitor-only mode; runtime editing must be explicitly enabled.
- Added placeholder rule grids, search box, Export button placeholder, and Apply/Save/Reset/Close buttons.

Build/test verification:

- `dotnet build MT5TradingBot.csproj -p:UseAppHost=false`: passed, 0 warnings, 0 errors.
- `dotnet run --project Tests/ForexBot.Tests/ForexBot.Tests.csproj -p:UseAppHost=false`: failed baseline, 265/334 passed. Failure pattern unchanged from Phase 0.

## Phase 6 - Rule Row UI and Visual Controls

Status: DONE

Changed files:

- Updated `UI/Forms/LiveTradeRulesMonitorControlForm.cs`
- Updated `Application/TradeRules/TradeRulesRuntimeSnapshotService.cs`

Notes:

- Rule rows now show enabled state, fixed rule code/name, source, standard/configured/preview/live values, min/max range, visual feedback, status, would-have result, reason, and reset placeholder.
- Added status and feedback colors for PASS/WARNING/BLOCK/DISABLED/NOT_CHECKED.
- Added Advanced Details panel for function, variable, source file, source key, raw values, result, effect, reason, and last checked time.
- Added numeric range/unit metadata for common numeric rule types.
- Editing is still monitor-only/placeholder until Phase 9; Phase 6 focuses on the row surface and visual controls.

Build/test verification:

- `dotnet build MT5TradingBot.csproj -p:UseAppHost=false`: passed, 0 warnings, 0 errors.
- `dotnet run --project Tests/ForexBot.Tests/ForexBot.Tests.csproj -p:UseAppHost=false`: failed baseline, 265/334 passed. Failure pattern unchanged from Phase 0.

## Phase 7 - Grouping, Filters, Search, Decision Audit

Status: DONE

Changed files:

- Updated `UI/Forms/LiveTradeRulesMonitorControlForm.cs`

Notes:

- Added group filter populated from rule group names.
- Added Enable Group / Disable Group buttons; group disable prompts for confirmation.
- Added critical individual disable confirmation when editing the Enabled cell.
- Added Decision Audit status filters: All, Blocked, Warning, Disabled, Passed.
- Existing search now combines with group/status filtering and still covers rule code/name/function/variable/status/source/category.
- Decision Audit preserves catalog/checking order from the rule catalog.
- Group enable/disable changes are UI/runtime-preview only until Phase 9 persists/applies them.

Build/test verification:

- `dotnet build MT5TradingBot.csproj -p:UseAppHost=false`: passed, 0 warnings, 0 errors.
- `dotnet run --project Tests/ForexBot.Tests/ForexBot.Tests.csproj -p:UseAppHost=false`: failed baseline, 265/334 passed. Failure pattern unchanged from Phase 0.

## Phase 8 - Contextual Opening Integration

Status: DONE

Changed files:

- Updated `UI/Forms/MainForm.cs`
- Updated `UI/Forms/MainForm.Design.cs`

Notes:

- Added `Scalping Rules` and `Normal Rules` buttons to the bot controls row.
- Added log right-click menu with Open Log Details, Open Rules Monitor, Copy Log, Copy Decision Audit.
- Preserved existing log double-click behavior: double-click still calls `ShowSelectedLogDetail()`.
- Rules Monitor opens only for eligible trade-decision log rows.
- Added running positions right-click menu to open Rules Monitor for the selected position; right-click selects the row under the cursor.
- Added `Rules` button to signal cards.
- Added `Rules` button to pair analysis/feed rows.
- Context is resolved from panel source, log text, position row, or card data where available.

Build/test verification:

- `dotnet build MT5TradingBot.csproj -p:UseAppHost=false`: passed, 0 warnings, 0 errors.
- `dotnet run --project Tests/ForexBot.Tests/ForexBot.Tests.csproj -p:UseAppHost=false`: failed baseline, 265/334 passed. Failure pattern unchanged from Phase 0.

## Phase 9 - Runtime Apply / Save / Reset

Status: DONE

Changed files:

- Added `Application/TradeRules/TradeRulesRuntimeControlService.cs`
- Updated `UI/Forms/LiveTradeRulesMonitorControlForm.cs`
- Updated `UI/Forms/MainForm.cs`

Notes:

- Added explicit Apply Runtime, Save Pair Defaults, Save Strategy Defaults, and Reset behavior.
- No auto-save: grid changes remain preview values until the user clicks Apply/Save.
- Apply Runtime updates the in-memory active settings object and rule enabled states for next checks where that config object is reused.
- Save Pair Defaults writes pair-specific scalping/normal defaults through `SettingsManager.SaveAsync`.
- Save Strategy Defaults writes strategy/common defaults through `SettingsManager.SaveAsync`.
- Per-rule reset and visible-tab/group reset restore preview values and enabled state.
- Existing MT5 position SL/TP is not modified by this feature.
- Some rules remain monitor-only because no safe runtime update hook exists yet for external live sources, broker metadata, historical audit, news state, and already-open MT5 position parameters.

Build/test verification:

- `dotnet build MT5TradingBot.csproj -p:UseAppHost=false`: passed, 0 warnings, 0 errors.
- `dotnet run --project Tests/ForexBot.Tests/ForexBot.Tests.csproj -p:UseAppHost=false`: failed, 318/334 passed. Remaining failures are in pre-existing live-gate/market-data expectations; no Phase 9 compile failures.

## Phase 10 - Logging and Rule Code Integration

Status: DONE

Changed files:

- Updated `UI/Forms/LiveTradeRulesMonitorControlForm.cs`
- Updated `UI/Forms/MainForm.cs`
- Updated `Trading/Scalping/ScalpingSessionService.cs`
- Updated `Application/Workflows/AutoBotService.cs`

Notes:

- Added `[RULES_MONITOR]` logs for bypass changes, critical rule disable, reset, apply runtime, save pair defaults, and save strategy defaults.
- Existing Rules Monitor open log remains in `MainForm`.
- Added stable rule codes/friendly names to selected scalping wait/no-trade logs:
  - `SCALP-MAX-MINUTES`
  - `SCALP-MAX-TRADES`
  - `SCALP-SESSION-LOSS`
  - `SCALP-SPREAD-LIMIT`
  - `SCALP-DIRECTION-TIE`
- Added `EXEC-FINAL-GATE Final Execution Gate` to `TRADE_AUDIT_FULL`.
- Added `EXEC-TRADE-ACCEPTED` / `EXEC-TRADE-REJECTED` to main bot trade/rejection log lines.
- No API keys, tokens, passwords, or provider secrets are logged.
- No 1-second refresh logging was added.

Build/test verification:

- `dotnet build MT5TradingBot.csproj -p:UseAppHost=false`: passed, 0 warnings, 0 errors.
- `dotnet run --project Tests/ForexBot.Tests/ForexBot.Tests.csproj -p:UseAppHost=false`: failed, 318/334 passed. Remaining failures are in pre-existing live-gate/market-data expectations.

## Phase 11 - Snapshot History

Status: DONE

Changed files:

- Updated `UI/Forms/LiveTradeRulesMonitorControlForm.cs`

Notes:

- Added in-window snapshot history panel.
- Keeps the last 200 rows.
- Added Clear History button.
- Adds history entries for rule status/live/enabled transitions, user value changes, enable/disable changes, apply/save/reset actions, and group enable/disable actions.
- Does not write every 1-second refresh to the main log.

Build/test verification:

- `dotnet build MT5TradingBot.csproj -p:UseAppHost=false`: passed, 0 warnings, 0 errors.
- `dotnet run --project Tests/ForexBot.Tests/ForexBot.Tests.csproj -p:UseAppHost=false`: failed, 318/334 passed. Remaining failures are in pre-existing live-gate/market-data expectations.

## Phase 12 - Export JSON/TXT

Status: DONE

Changed files:

- Added `Application/TradeRules/TradeRulesExportService.cs`
- Updated `UI/Forms/LiveTradeRulesMonitorControlForm.cs`

Notes:

- Added Export JSON and readable TXT.
- Export includes context, account details, symbol/position context, decision summary, all rules, and snapshot history.
- JSON export recursively redacts secret-like keys including API keys, tokens, passwords, secrets, Telegram token, and chat id.
- Export action is logged once via `[RULES_MONITOR] Export`; refreshes are not logged.

Build/test verification:

- `dotnet build MT5TradingBot.csproj -p:UseAppHost=false`: passed, 0 warnings, 0 errors.
- `dotnet run --project Tests/ForexBot.Tests/ForexBot.Tests.csproj -p:UseAppHost=false`: failed, 318/334 passed. Remaining failures are in pre-existing live-gate/market-data expectations.

## Phase 13 - Tests / Validation

Status: DONE

Changed files:

- `Tests/ForexBot.Tests/Program.cs`

Implemented:

- Added rule monitor validation coverage for fixed unique rule codes.
- Added disabled-rule evaluator coverage proving the would-have-blocked result remains visible.
- Added summary counting coverage for disabled-but-would-block rules.
- Added export redaction coverage for secret-like keys.

Build/test verification:

- `dotnet build MT5TradingBot.csproj -p:UseAppHost=false`: passed, 0 warnings, 0 errors.
- `dotnet run --project Tests/ForexBot.Tests/ForexBot.Tests.csproj -p:UseAppHost=false`: failed, 322/338 passed. The 4 new Phase 13 tests passed; remaining failures are pre-existing live-gate/market-data expectations.

## Phase 14 - Final Review

Status: DONE

Reviewed:

- Log double-click behavior remains intact: `_txtLog.DoubleClick` still routes to `ShowSelectedLogDetail()`.
- Contextual openings are present from scalping/normal controls, eligible trade logs, running positions, signal cards, and pair analysis/feed rows.
- Runtime edits are preview/apply only until explicit Save Pair Defaults or Save Strategy Defaults.
- Running trade context is monitor-only; this feature does not modify existing MT5 position SL/TP.
- Disabled rules remain visible and preserve the would-have status, including disabled-but-would-block risk.
- Critical rule disable requires confirmation and writes `[RULES_MONITOR]` audit log entries.
- Exports include current snapshot/history and redact secret-like keys.

Build/test verification:

- `dotnet build MT5TradingBot.csproj -p:UseAppHost=false`: passed, 0 warnings, 0 errors.
- `dotnet run --project Tests/ForexBot.Tests/ForexBot.Tests.csproj -p:UseAppHost=false`: failed, 322/338 passed. Phase 13 monitor tests passed; remaining failures are pre-existing live-gate/market-data expectations.
