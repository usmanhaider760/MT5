# Demo Testing Checklist

Use this checklist before marking demo testing complete.

## Required Environment

- MT5 is connected to a demo account.
- TradingBotEA is attached to a chart.
- Algo Trading is enabled in MT5.
- ForexBot connects through `MT5TradingBotPipe`.
- Test lot size is `0.01`.

## Connection Tests

- [ ] App connects to MT5 without pipe timeout.
- [ ] Account info refreshes correctly.
- [ ] Open positions refresh correctly.
- [ ] Symbol info/spread is available for configured pairs.

## Safety Tests

- [ ] Risk validation rejects invalid SL/TP.
- [ ] Risk validation rejects high spread.
- [ ] Risk validation rejects pair outside allowlist.
- [ ] User approval is required for live/manual mode.
- [ ] Demo auto-approval is only enabled deliberately.

## Execution Tests

- [ ] One `0.01` lot demo trade executes successfully.
- [ ] Trade result is logged.
- [ ] Position appears in MT5 and ForexBot.
- [ ] Position can be closed from MT5 or the app.
- [ ] No duplicate execution occurs.

## Completion Criteria

Demo testing can be marked complete only after all required checks pass on a demo account.
