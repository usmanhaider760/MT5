# ForexBot Agents

This file is the coordination guide for AI agents (Codex) and contributors working on ForexBot.

---

# Project Context

ForexBot is an MT5-connected forex trading system with:

- Windows desktop control application
- MT5 Expert Advisor bridge (IPC / Named Pipe)
- Manual + semi-automatic trade execution
- JSON signal execution
- AI-assisted signal analysis
- Risk management and account protection
- Real-time monitoring and logging

---

# Core Principles

- Safety first — never bypass risk controls
- AI assists, does NOT control money directly
- User approval required before live trade execution
- Keep modules separate and easy to audit
- Prefer simple, readable, maintainable code
- Make small, safe, incremental changes
- Always log important decisions and actions
- Minimize token usage whenever possible

---

# Token Efficiency Rules

- Do NOT scan the full repository unless required
- Inspect only files directly related to the current task
- Avoid repeated full-project analysis
- Reuse existing context whenever possible
- Keep responses concise
- Do not explain obvious code
- Show only changed files and important notes
- Avoid unnecessary refactoring
- Avoid rewriting working code
- Prefer incremental modifications over regeneration
- Do not repeatedly summarize completed work
- Do not generate large architecture explanations unless requested

---

# Response Rules

- Keep explanations short and practical
- Prefer implementation over discussion
- Do not restate AGENTS.md content
- Do not generate unnecessary markdown
- Show:
  - What changed
  - Which files changed
  - Important notes only
- Avoid long summaries unless user explicitly requests details

---

# Architecture Rules

- Use clean modular architecture
- Do NOT mix responsibilities across modules
- Separate these clearly:
  - MT5 bridge
  - IPC communication
  - Strategy logic
  - AI analysis
  - Risk management
  - Trade execution
  - UI
  - Database

---

# Required Modules

- Broker Integration Module
- MT5 Expert Advisor Bridge Module
- IPC / Named Pipe Communication Module
- Market Data Module
- Pair Scanner Module
- Technical Indicator Module
- Strategy Engine Module
- AI Analysis Module
- Signal Decision Module
- User Approval Module
- Risk Management Module
- Trade Execution Module
- Active Trade Monitoring Module
- AI Re-Analysis Module
- Trade Management Module
- News Filter Module
- Kill Switch / Safety Module
- Database / Trade History Module
- Logging & Diagnostics Module
- Dashboard / Windows UI Module
- Notification Module
- Backtesting Module
- Demo Trading Module

---

# AI Module Rules

AI must be implemented as a separate module.

AI responsibilities:

- Market trend analysis
- Candle behavior analysis
- Support & resistance detection
- Volatility analysis
- Spread evaluation
- News impact analysis
- Session analysis
- Entry quality evaluation
- Stop loss & take profit suggestion
- Confidence scoring
- Trade invalidation condition

AI output must include:

- Pair
- Direction (Buy/Sell/Hold)
- Entry price
- Stop loss
- Take profit
- Confidence score
- Risk level
- Reason
- Invalidation condition

AI must NOT directly execute trades.

---

# Trading Decision Flow (STRICT)

1. User starts bot
2. Bot checks MT5 connection
3. Bot checks account status
4. Pair scanner finds best pairs
5. Market data module collects live data
6. Strategy engine creates initial signal
7. AI module analyzes signal
8. News filter checks events
9. Risk engine validates trade
10. Signal is shown to user
11. User approves trade
12. Trade execution places order
13. Trade monitoring starts
14. AI re-analysis monitors trade validity
15. Trade management adjusts or closes trade
16. All actions are logged

---

# Development Workflow for Codex

Every time Codex runs, it MUST:

1. Read AGENTS.md
2. Read docs/DEVELOPMENT_STATUS.md only if task tracking is required
3. Inspect only files relevant to the current task
4. Avoid full repository analysis unless necessary
5. Identify the next required implementation step
6. Implement ONLY the requested or next logical task
7. Do NOT jump ahead unnecessarily
8. Do NOT rewrite unrelated working code
9. Keep changes small and safe
10. Update DEVELOPMENT_STATUS.md only when task tracking is being used

---

# Task Execution Rules

Allowed:

- One task at a time
- Small safe changes
- Compile-safe code
- Interface-first design
- Logging for important actions
- Demo-safe implementation
- Minimal focused refactoring

Not allowed:

- Full system rewrite
- Large unrelated refactors
- Breaking MT5 bridge
- Bypassing risk checks
- Mixing AI with execution logic
- Direct live trading without approval
- Removing working code unnecessarily

---

# Development Phases

## Phase 1: Project Review

- Analyze current structure
- Identify MT5 bridge
- Identify IPC communication
- Identify UI
- Identify trade execution logic
- Identify risk logic
- Identify logging

## Phase 2: Structure Setup

- Add missing folders
- Add interfaces
- Add DTOs
- Add placeholder services
- Do NOT break existing logic

## Phase 3: Core Safety

- Risk management engine
- Validation rules
- Max loss protection
- Kill switch

## Phase 4: Market Intelligence

- Market data module
- Pair scanner
- Indicators
- Strategy engine

## Phase 5: AI Module

- Separate AI folder
- Trend analyzer
- Candle analyzer
- Volatility analyzer
- News analyzer
- Confidence scoring

## Phase 6: Trade Workflow

- Signal decision
- User approval
- Trade execution
- Trade monitoring
- Trade management

## Phase 7: Testing

- Unit tests
- Demo account testing
- Backtesting
- Logging validation

---

# Development Status Tracking

Optional file:

docs/DEVELOPMENT_STATUS.md

Track:

- Completed tasks
- Current task
- Next task
- Important notes

Only update when project tracking workflow is active.

---

# Automatic Next Task Rule

When user says:

- continue
- next
- start

Codex should:

1. Read DEVELOPMENT_STATUS.md if available
2. Find next pending task
3. Briefly explain task
4. Implement it
5. Update status file if tracking is enabled

Keep response concise.

---

# Code Quality Rules

- Use .NET 8 best practices
- Use dependency injection
- Use interfaces for services
- Keep methods small and focused
- Add proper error handling
- Add logging for important actions
- Avoid hardcoding values
- Use configuration files
- Write unit tests for important logic

---

# Safety Rules (CRITICAL)

- Never execute trade without risk validation
- Never execute trade without user approval in live mode
- Always check spread before trade
- Always check news before trade
- Stop trading if daily loss limit reached
- Stop trading if connection fails
- Always log trade reason and result

---

# Final Principle

System flow:

AI -> Analysis  
Rules -> Validation  
Risk -> Control  
User -> Approval  
Bot -> Execution