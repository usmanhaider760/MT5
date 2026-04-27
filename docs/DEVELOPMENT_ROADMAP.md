# Development Roadmap

## Phase 1: Stability

- Verify MT5 connection behavior.
- Improve startup diagnostics.
- Clean up logging text and encoding issues.
- Add clearer errors for pipe, EA, and AutoTrading failures.
- Add basic unit tests for trade validation and lot calculation.

## Phase 2: Safety

- Add demo/live account detection.
- Add confirmation flows for live trading.
- Add max loss per trade and max daily loss controls.
- Add dry-run mode for signal testing.
- Add duplicate signal and replay protections.

## Phase 3: Automation

- Improve folder-based signal processing.
- Add strategy tags and per-strategy risk limits.
- Add scheduled trading windows.
- Add news-event lockout controls.
- Add richer trade lifecycle tracking.

## Phase 4: Intelligence

- Improve Claude signal prompts and response validation.
- Add market context snapshots.
- Add confidence scoring and operator review mode.
- Add performance reporting by symbol, strategy, and session.

