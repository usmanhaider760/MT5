# MT5TradingBotPro — Full Professional Audit Prompt
# Save this file as: /docs/audit/AUDIT_PROMPT.md in your project
# Usage: Paste this entire prompt into Claude Code (VS Code) with your codebase open
# Re-run after each development cycle to track progress
# ─────────────────────────────────────────────────────────────────────────────

---

## ROLE & CONTEXT

You are a **30-year veteran quant developer, trading systems architect, and algorithmic trading
expert**. You have designed and shipped production Forex bots for institutional hedge funds,
proprietary trading desks, and retail algo platforms. You have deep expertise in:

- MetaTrader 5 (MQL5 + C# bridge architecture)
- .NET 8 / C# WinForms desktop trading applications
- AI/LLM integration in latency-sensitive financial systems
- Risk management systems for live Forex and CFD trading
- Scalping, swing, and hybrid automated trading strategies
- Professional software architecture: Clean Architecture, SOLID, DDD
- Production-grade reliability: watchdogs, failsafes, reconnection logic
- Regulatory awareness: broker compliance, order execution best practices

Your job is to perform a **comprehensive, uncompromising, expert-level technical audit** of the
MT5TradingBotPro codebase provided below. You will produce two output documents saved directly
into the project. You do not sugarcoat. You identify real problems. You give real solutions.

---

## BOT PROFILE

- **Project:** MT5TradingBotPro
- **Platform:** MetaTrader 5 via C# MT5 bridge / socket connection
- **Language:** C# .NET 8, WinForms
- **AI Integration:** Anthropic Claude SDK (`claude-sonnet-4-5`)
- **Target Modes:** Auto Scalping, Auto Swing Trading, Manual Trading, Semi-Auto
- **Target Pairs:** EURUSD, GBPUSD, XAUUSD (Gold), USDJPY
- **Architecture Goal:** Layered, modular, production-grade with AI regime detection
- **UI Goal:** Single unified trade management window for reviewing/managing all trades

---

## AUDIT SCOPE — READ THE ENTIRE CODEBASE FIRST

Before writing anything, read and analyse:

1. Every `.cs` file in the solution
2. `appsettings.json` / any config files
3. Project structure (`.csproj`, folder layout, namespaces)
4. Any existing documentation in `/docs`
5. Any existing `AUDIT_REPORT.md` or `FEATURE_BACKLOG.md` (compare against previous run)

---

## SCORING SYSTEM

Score each layer out of the points shown. Calculate total score out of 100.

| Score | Grade |
|-------|-------|
| 90–100 | Expert / Production Grade |
| 75–89 | Professional |
| 55–74 | Intermediate |
| 30–54 | Basic |
| 0–29 | Early Stage / Not Production Ready |

---

## AUDIT OUTPUT — TWO DOCUMENTS

After completing your analysis, generate BOTH files below.
Save them to the project at the exact paths specified.

---

# ═══════════════════════════════════════════════════════════
# DOCUMENT 1: AUDIT REPORT
# Save to: /docs/audit/AUDIT_REPORT.md
# ═══════════════════════════════════════════════════════════

Generate this document with the following exact structure:

```
# MT5TradingBotPro — Audit Report
**Audit Date:** [TODAY'S DATE]
**Auditor:** Claude (30-year Expert Mode)
**Codebase Version:** [detect from git tag or project version]
**Previous Audit Score:** [read from previous AUDIT_REPORT.md if exists, else N/A]

---

## Executive Summary

[3–5 sentences. Overall health of the bot. Is it safe to run live RIGHT NOW?
What is the single biggest risk? What is the most impressive thing already built?]

---

## Overall Score: [X] / 100 — [GRADE]

[Progress bar in markdown: ████████░░ 78%]

---

## Layer-by-Layer Audit

### Layer 1: Core Architecture [X / 15 pts]

| Feature | Status | Score | Notes |
|---------|--------|-------|-------|
| Fast execution layer (<5ms, no API in hot path) | ✅/⚠️/❌ | x/3 | [specific finding] |
| Slow AI layer (async, background, cached) | ✅/⚠️/❌ | x/3 | [specific finding] |
| AIContextManager (in-memory regime cache) | ✅/⚠️/❌ | x/2 | [specific finding] |
| Modular design (separate namespaces/classes per concern) | ✅/⚠️/❌ | x/3 | [specific finding] |
| Config-driven parameters (no magic numbers) | ✅/⚠️/❌ | x/2 | [specific finding] |
| Dependency injection / IoC | ✅/⚠️/❌ | x/2 | [specific finding] |

**Layer Score: [X] / 15**

**Critical Issues:**
- [List any ❌ items with exact file/line reference]

**Code Smell Found:**
- [List actual code smells found in the real code, with file names and line numbers]

**Recommendations:**
- [Specific, actionable fixes with C# code examples where needed]

---

### Layer 2: Trading Modes [X / 20 pts]

| Feature | Status | Score | Notes |
|---------|--------|-------|-------|
| Auto Scalping mode (M1/M5, AI regime filter) | ✅/⚠️/❌ | x/4 | |
| Auto Swing/Position trading mode (H1/H4) | ✅/⚠️/❌ | x/3 | |
| Manual trading panel (one-click BUY/SELL, lot/SL/TP) | ✅/⚠️/❌ | x/3 | |
| Semi-auto mode (AI suggests, human confirms) | ✅/⚠️/❌ | x/3 | |
| Unified Trade Review Window (single window) | ✅/⚠️/❌ | x/3 | |
| Hot-switch between modes without restart | ✅/⚠️/❌ | x/2 | |
| Mode state machine (clean transitions) | ✅/⚠️/❌ | x/2 | |

**Layer Score: [X] / 20**

[Same Critical Issues / Code Smells / Recommendations format]

---

### Layer 3: Signal & AI Layer [X / 20 pts]

| Feature | Status | Score | Notes |
|---------|--------|-------|-------|
| AI market bias (BUY/SELL/NEUTRAL + confidence JSON) | ✅/⚠️/❌ | x/3 | |
| Multi-timeframe confluence (M5 entry + H1 + H4) | ✅/⚠️/❌ | x/3 | |
| Candlestick pattern scanner (pin bar, engulfing, doji) | ✅/⚠️/❌ | x/2 | |
| S/R level detection (swing highs/lows) | ✅/⚠️/❌ | x/2 | |
| Session filter (London/NY/Asian, time-gated) | ✅/⚠️/❌ | x/2 | |
| Economic calendar news filter (auto-pause) | ✅/⚠️/❌ | x/2 | |
| AI prompt quality (structured, compressed, context-rich) | ✅/⚠️/❌ | x/2 | |
| AI reasoning log per trade (WHY it traded/skipped) | ✅/⚠️/❌ | x/2 | |
| Sentiment analysis integration | ✅/⚠️/❌ | x/2 | |

**Layer Score: [X] / 20**

[Prompt Quality Sub-Audit: Paste the actual Claude prompt used in the code.
Rate it: Is it structured? Is it compressed? Does it return structured JSON?
Does it hallucination-guard? Rewrite it if poor.]

---

### Layer 4: Risk Management Layer [X / 20 pts]

| Feature | Status | Score | Notes |
|---------|--------|-------|-------|
| Percentage-based position sizing (equity × risk%) | ✅/⚠️/❌ | x/3 | |
| Daily drawdown hard stop (auto-pause all trading) | ✅/⚠️/❌ | x/3 | |
| Spread filter (skip if spread > X pips) | ✅/⚠️/❌ | x/3 | |
| Max concurrent trades cap | ✅/⚠️/❌ | x/2 | |
| Correlation check (EURUSD + GBPUSD block) | ✅/⚠️/❌ | x/2 | |
| Breakeven move on partial TP hit | ✅/⚠️/❌ | x/2 | |
| Trailing stop management | ✅/⚠️/❌ | x/2 | |
| Slippage guard (cancel if fill > X pips off) | ✅/⚠️/❌ | x/2 | |
| Emergency close all (panic button) | ✅/⚠️/❌ | x/1 | |

**Layer Score: [X] / 20**

[CRITICAL: For risk layer, explicitly state: "Is it SAFE to run this bot live with $1,000?
What is the worst-case scenario if a bug exists in the risk layer?"]

---

### Layer 5: UI / Dashboard Layer [X / 13 pts]

| Feature | Status | Score | Notes |
|---------|--------|-------|-------|
| Live dashboard (equity, open trades, floating P&L) | ✅/⚠️/❌ | x/2 | |
| Unified trade management window (review/modify/close) | ✅/⚠️/❌ | x/3 | |
| AI Analysis panel (live regime, confidence, last update) | ✅/⚠️/❌ | x/2 | |
| Real-time log viewer (color-coded INFO/WARN/ERROR) | ✅/⚠️/❌ | x/2 | |
| Performance chart (equity curve, drawdown periods) | ✅/⚠️/❌ | x/2 | |
| Alert system (sound + popup + Telegram) | ✅/⚠️/❌ | x/1 | |
| Dark mode UI | ✅/⚠️/❌ | x/1 | |

**Layer Score: [X] / 13**

---

### Layer 6: Operations & Safety Layer [X / 12 pts]

| Feature | Status | Score | Notes |
|---------|--------|-------|-------|
| Auto-reconnect on MT5 disconnect | ✅/⚠️/❌ | x/2 | |
| Heartbeat monitor / watchdog timer | ✅/⚠️/❌ | x/2 | |
| Trade log to SQLite/SQL Server (full history) | ✅/⚠️/❌ | x/2 | |
| Backtest / simulation mode | ✅/⚠️/❌ | x/2 | |
| Paper trading mode (virtual funds, live signals) | ✅/⚠️/❌ | x/1 | |
| Edge health monitor (win rate tracker, auto-alert) | ✅/⚠️/❌ | x/1 | |
| Multi-symbol parallel support | ✅/⚠️/❌ | x/1 | |
| Hot config reload (no restart) | ✅/⚠️/❌ | x/1 | |

**Layer Score: [X] / 12**

---

## Score Summary

| Layer | Score | Max | % |
|-------|-------|-----|---|
| 1. Core Architecture | | 15 | |
| 2. Trading Modes | | 20 | |
| 3. Signal & AI Layer | | 20 | |
| 4. Risk Management | | 20 | |
| 5. UI / Dashboard | | 13 | |
| 6. Operations & Safety | | 12 | |
| **TOTAL** | | **100** | |

---

## Architecture Quality Findings

### Separation of Concerns Violations
[List every place where logic bleeds across layers with file + line number]

### Reusability Violations
[List every duplicated logic that should be a shared service/utility]
Example: "Lot size calculation found in 3 files: TradeExecutor.cs:112,
ScalpingEngine.cs:87, ManualTradePanel.cs:203 — extract to RiskCalculator service"

### Naming & Code Quality
[Method names, class names, magic numbers, commented-out code, dead code]

### Thread Safety Issues
[Any shared state accessed from multiple threads without locking]

### Error Handling Gaps
[Missing try-catch, unhandled exceptions, silent failures]

### Memory & Resource Leaks
[Unclosed connections, undisposed objects, Timer leaks]

---

## Live Trading Safety Assessment

**Safe to run live: YES / NO / CONDITIONAL**

Conditions that must be met before live trading:
1. [Condition 1]
2. [Condition 2]
...

Maximum recommended account size with current code: $[X]
Reason: [explain]

---

## Comparison to Previous Audit
[Only if previous AUDIT_REPORT.md exists]

| Metric | Previous | Current | Change |
|--------|----------|---------|--------|
| Total Score | | | +/- |
| Features Complete | | | +/- |
| Critical Issues | | | +/- |
| Layer 1 | | | |
| Layer 2 | | | |
| Layer 3 | | | |
| Layer 4 | | | |
| Layer 5 | | | |
| Layer 6 | | | |

[Commentary on progress made since last audit]
```

---

# ═══════════════════════════════════════════════════════════
# DOCUMENT 2: FEATURE BACKLOG
# Save to: /docs/audit/FEATURE_BACKLOG.md
# ═══════════════════════════════════════════════════════════

Generate this document with the following exact structure:

```
# MT5TradingBotPro — Feature Backlog
**Generated:** [TODAY'S DATE]
**Source:** Audit run [DATE]
**Total Missing Features:** [N]
**Estimated Development Effort:** [X weeks at 8hrs/day]

---

## Backlog Priority Legend

| Priority | Label | Meaning |
|----------|-------|---------|
| P0 | 🔴 CRITICAL | Bot is unsafe or broken without this |
| P1 | 🟠 HIGH | Core functionality incomplete |
| P2 | 🟡 MEDIUM | Professional feature, needed before release |
| P3 | 🟢 LOW | Polish, advanced, or nice-to-have |

---

## P0 — Critical (Build These First)

### [FEATURE NAME]
**Priority:** 🔴 P0 — CRITICAL
**Layer:** [Which of the 6 layers]
**Why Critical:** [Exact risk if missing — e.g., "Without daily drawdown stop,
a bug in position sizing can blow the entire account in one session"]
**Estimated Effort:** [X hours]

**Acceptance Criteria:**
- [ ] [Specific, testable criterion 1]
- [ ] [Specific, testable criterion 2]
- [ ] [Specific, testable criterion 3]

**C# Implementation Guide:**

```csharp
// Suggested class/interface structure

namespace MT5TradingBotPro.[Layer]
{
    /// <summary>
    /// [What this class does and why it exists]
    /// </summary>
    public class [ClassName] : I[InterfaceName]
    {
        // Key dependencies to inject
        private readonly ILogger _logger;
        private readonly AppSettings _settings;

        // Key properties
        public [ReturnType] [PropertyName] { get; private set; }

        // Key methods with signatures and logic outline
        public async Task<[ReturnType]> [MethodName]([Params])
        {
            // Step 1: [what happens]
            // Step 2: [what happens]
            // Step 3: [what happens]
            // Edge cases to handle: [list them]
        }
    }
}
```

**Integration Points:**
- Inject into: [which existing class uses this]
- Called from: [which event/timer/method triggers this]
- Configuration keys needed in appsettings.json:
```json
{
  "[FeatureName]": {
    "[SettingKey]": [defaultValue],
    "[SettingKey2]": [defaultValue2]
  }
}
```

**Testing Checklist:**
- [ ] Unit test: [specific scenario]
- [ ] Integration test: [specific scenario]
- [ ] Manual test: [specific scenario]
- [ ] Edge case: [what could go wrong]

---

[Repeat above block for each P0 feature]

---

## P1 — High Priority

[Same structure as P0 but for HIGH priority features]

---

## P2 — Medium Priority

[Same structure for MEDIUM priority features]

---

## P3 — Low Priority / Polish

[Same structure for LOW priority features]

---

## Development Roadmap

Based on the backlog above, recommended build sequence:

### Sprint 1 (Week 1–2): Foundation Safety
Goal: Make the bot safe enough to paper trade
- [ ] [Feature 1]
- [ ] [Feature 2]
- [ ] [Feature 3]

### Sprint 2 (Week 3–4): Core Trading Modes
Goal: All 3 trading modes functional
- [ ] [Feature 1]
- [ ] [Feature 2]

### Sprint 3 (Week 5–6): AI & Signal Quality
Goal: AI layer professional grade
- [ ] [Feature 1]
- [ ] [Feature 2]

### Sprint 4 (Week 7–8): UI & Operations
Goal: Production-ready dashboard and safety systems
- [ ] [Feature 1]
- [ ] [Feature 2]

### Sprint 5 (Week 9–10): Edge Monitoring & Live Readiness
Goal: Live trading sign-off
- [ ] [Feature 1]
- [ ] [Feature 2]

---

## Reusable Services To Build First

These are shared utilities that multiple features depend on.
Build these BEFORE the features that use them.

| Service | Used By | Interface |
|---------|---------|-----------|
| `RiskCalculator` | ScalpingEngine, SwingEngine, ManualPanel | `IRiskCalculator` |
| `IndicatorEngine` | All signal modules | `IIndicatorEngine` |
| `AIContextManager` | All trading engines | `IAIContextManager` |
| `TradeRepository` | Logging, UI, EdgeMonitor | `ITradeRepository` |
| `EventBus` | All layers (decoupled messaging) | `IEventBus` |
| `SessionManager` | SignalLayer, ScalpingEngine | `ISessionManager` |
| [Add more based on actual codebase analysis] | | |

---

## Definition of Done (for each feature)

A feature is complete when:
- [ ] Code written and compiles without warnings
- [ ] Unit tests written and passing
- [ ] Integrated and tested in paper trading mode
- [ ] Configuration documented in appsettings.json
- [ ] No magic numbers (all values from config)
- [ ] Logging added (entry, exit, errors)
- [ ] Reviewed in next audit run (score improved)
```

---

# ═══════════════════════════════════════════════════════════
# AUDIT EXECUTION INSTRUCTIONS (for Claude Code)
# ═══════════════════════════════════════════════════════════

When running this audit in Claude Code (VS Code), follow these steps:

## Step 1 — Read All Source Files
```
Read every .cs file in the solution.
Read appsettings.json.
Read existing /docs/audit/AUDIT_REPORT.md if it exists (for comparison).
Read existing /docs/audit/FEATURE_BACKLOG.md if it exists.
List all files read before starting the audit.
```

## Step 2 — Internal Analysis (think before writing)
```
Before generating any document, internally assess:
- What layers exist vs what is missing?
- Where does logic bleed across layers?
- What is duplicated that should be shared?
- Is there any thread safety risk?
- Is the bot safe to run live in current state?
- What are the top 3 risks right now?
```

## Step 3 — Generate AUDIT_REPORT.md
```
Save to: [PROJECT_ROOT]/docs/audit/AUDIT_REPORT.md
Use real file names and line numbers from the actual code.
Do not invent features that don't exist.
Do not miss features that do exist.
Score honestly — a 45/100 is more useful than a fake 80/100.
```

## Step 4 — Generate FEATURE_BACKLOG.md
```
Save to: [PROJECT_ROOT]/docs/audit/FEATURE_BACKLOG.md
Every missing feature must have:
  - Priority (P0/P1/P2/P3)
  - C# code structure (not pseudocode — real C# signatures)
  - Acceptance criteria
  - Effort estimate
  - Integration points
Only include genuinely missing features — not features already implemented.
```

## Step 5 — Summary in Chat
```
After saving both files, post a short summary in chat:
- Overall score
- Top 3 critical issues
- Next 3 things to build
- Estimated time to production-ready
```

---

# ═══════════════════════════════════════════════════════════
# HOW TO USE THIS AUDIT SYSTEM
# ═══════════════════════════════════════════════════════════

## Audit Cycle (repeat every 2 weeks or after each sprint)

```
1. Open VS Code with MT5TradingBotPro solution
2. Open Claude Code (Ctrl+Shift+P → Claude)
3. Say: "Run the full audit using /docs/audit/AUDIT_PROMPT.md"
4. Claude reads all code + this prompt
5. Claude saves AUDIT_REPORT.md + FEATURE_BACKLOG.md
6. Review AUDIT_REPORT.md — fix critical issues first
7. Pick top items from FEATURE_BACKLOG.md for next sprint
8. Develop sprint
9. Repeat from step 3
```

## Recommended Folder Structure

```
MT5TradingBotPro/
├── docs/
│   └── audit/
│       ├── AUDIT_PROMPT.md          ← this file (never modify)
│       ├── AUDIT_REPORT.md          ← generated each run (overwritten)
│       ├── FEATURE_BACKLOG.md       ← generated each run (overwritten)
│       └── history/
│           ├── AUDIT_REPORT_2025-05-01.md   ← archive copies
│           └── FEATURE_BACKLOG_2025-05-01.md
├── src/
│   ├── MT5TradingBotPro/
│   │   ├── AIAnalysis/
│   │   ├── RiskManagement/
│   │   ├── TradeExecution/
│   │   ├── SignalDecision/
│   │   ├── MarketData/
│   │   ├── UI/
│   │   └── Common/          ← shared/reusable services live here
├── tests/
└── appsettings.json
```

## Archiving Previous Reports

Before each audit run, Claude should archive the previous report:
```
Copy AUDIT_REPORT.md → history/AUDIT_REPORT_[DATE].md
Copy FEATURE_BACKLOG.md → history/FEATURE_BACKLOG_[DATE].md
Then overwrite with new versions
```

This gives you a full history of your bot's progress over time.

---

*End of Audit Prompt — Version 1.0*
*Do not modify this file. It is the audit standard.*