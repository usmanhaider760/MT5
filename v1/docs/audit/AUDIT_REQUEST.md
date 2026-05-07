# Forex Scalping Bot Audit Request

You are auditing this forex scalping bot as a senior quantitative trading systems reviewer.

IMPORTANT:
- Do not modify code yet.
- Do not optimize parameters yet.
- Do not suggest cosmetic refactors.
- First produce an audit report.
- Assume live profitability is not proven unless supported by realistic evidence.

Audit the bot for:

1. Strategy edge
   - Exact entry conditions
   - Exact exit conditions
   - Whether signals repaint or use future data
   - Whether logic is overfitted
   - Whether it adapts to market regime

2. Execution realism
   - Spread handling
   - Slippage handling
   - Commission handling
   - Broker minimum stop distance
   - Rollover/spread-widening protection
   - Latency assumptions
   - Requotes/order failures

3. Risk management
   - Lot sizing formula
   - Stop-loss logic
   - Take-profit logic
   - Daily loss limit
   - Max drawdown limit
   - Consecutive loss stop
   - Max open trades
   - Correlation exposure
   - Margin protection
   - Martingale/grid/averaging detection

4. Backtesting validity
   - Variable spread
   - Commission
   - Slippage
   - Tick data vs candle data
   - Look-ahead bias
   - Curve fitting
   - Out-of-sample test
   - Walk-forward test
   - Monte Carlo/stress test

5. Live readiness
   - Logging quality
   - Error handling
   - Kill switch
   - News filter
   - VPS/latency assumptions
   - Broker compatibility
   - Demo/live result tracking

Output format:

## Executive Summary
Give a clear verdict:
- Audit-ready / Not audit-ready
- Live-ready / Not live-ready
- Biggest profit blockers
- Biggest account-blowup risks

## Findings Table
Columns:
- Area
- Finding
- Severity: Critical / High / Medium / Low
- Evidence from code
- Why it matters
- Recommended fix

## Missing Information
List what cannot be verified from the current codebase.

## Questions for Owner
Ask only questions needed to finish the audit.

## Suggested Next Steps
Prioritize fixes in order:
1. Account safety
2. Execution realism
3. Backtest validity
4. Strategy improvement