# Codex Tasks

## Ready Tasks

- Review MT5 named pipe connection flow and improve diagnostics.
- Add tests for `TradeRequest.Validate`.
- Add tests for `LotCalculator`.
- Fix mojibake/encoding issues in comments and UI/log text.
- Add a setup checklist inside the UI for first-time MT5 connection.

## Investigation Tasks

- Check whether all manual trades should pass through `AutoBotService` validation.
- Review EA JSON parsing robustness.
- Review how closed positions are detected and logged.
- Review whether TP2 split trades should count as one signal or two daily trades.

## Safety Tasks

- Add demo account warning before first trade.
- Add global emergency stop UI control.
- Add maximum daily drawdown lockout persistence.
- Add trade preview showing dollar risk, pips risk, and risk/reward.

