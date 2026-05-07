# ForexBot Development Status

## Current Phase

Phase 7 — Demo Testing

## Current Task

Run MT5 demo-account validation.

## Last Completed Task

Performance tab added: SQLite closed trades can now be viewed as an equity
curve with Last 20, Last 100, and All Time periods plus win rate, net P&L,
max drawdown, and Sharpe stats.
Verified with:

```bash
dotnet build "d:\Projects\MT5\MT5TradingBot.csproj" -v quiet
```

Telegram notification wrappers wired: AutoBotService now calls
NotifyTradeOpenedAsync for successful opens and NotifyRiskBlockedAsync for
risk-manager blocks, with the legacy trade-open log-helper notification removed
to avoid duplicate alerts.
Verified with:

```bash
dotnet build "d:\Projects\MT5\MT5TradingBot.csproj" -v quiet
```

Max concurrent position runtime guard added: AutoBotService now returns
MAX_CONCURRENT_POSITIONS before risk validation or broker execution when the
bot already has the configured number of open positions.
Verified with:

```bash
dotnet build "d:\Projects\MT5\MT5TradingBot.csproj" -v quiet
```

Extreme slippage protection added: fills above 2x configured max slippage are
closed immediately and Telegram alerts are sent for high/extreme slippage.
Verified with:

```bash
dotnet build "d:\Projects\MT5\MT5TradingBot.csproj" -v quiet
```

Settings hot-reload added: SettingsManager watches settings.json for external
changes and MainForm pushes reloaded Bot/Claude config to running services.
Verified with:

```bash
dotnet build "d:\Projects\MT5\MT5TradingBot.csproj" -v quiet
```

Claude signal analysis now runs configured watch symbols in parallel from the
same account and positions snapshot. Verified with:

```bash
dotnet build "d:\Projects\MT5\MT5TradingBot.csproj" -v quiet
```

## Next Task

Validate the full demo trading flow:

- MT5 connection
- EA reload/attach confirmation
- Signal file detection
- Play-button manual execution
- Trade opening
- Risk validation
- Auto-close by pips/money
- Review Trade live snapshot refresh
- Logging and trade history

## Active Modules

- MT5 Bridge
- TradingBotEA
- Auto Bot
- Signal File Watcher
- Review Trade Window
- AI API Config
- Risk Manager
- Trade Execution
- Trade Monitoring
- Logging/Audit

## Known Issues / Pending Checks

- Demo testing is still pending on a live MT5 demo account.
- MT5 EA must be reloaded/reattached after deployment.
- MQL5 compile should be verified through `scripts/Deploy-MT5EA.ps1`.
- Live market snapshot depends on updated EA command `GET_MARKET_SNAPSHOT`.

## Last Verified Build

```bash
dotnet build MT5TradingBot.csproj --no-restore -o .\bin\VerifyBuild
