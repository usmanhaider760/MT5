
## 2. `docs/CHANGELOG.md`

```md
# ForexBot Changelog

This file stores completed work history.

Do not load this file for every small task unless history is required.

---

## Completed Work History

- Project structure reviewed.
- Existing MT5 bridge identified.
- Existing IPC/named pipe logic identified.
- Existing UI flow identified.
- Existing trade execution logic identified.
- Existing risk logic identified.
- Existing logging identified.
- Core models created.
- Core interfaces created.
- Risk engine created.
- Market data module created.
- Pair scanner created.
- Strategy engine created.
- AI module skeleton created.
- Signal decision module created.
- User approval flow created.
- Trade execution module created.
- Trade monitoring worker created.
- News filter created.
- Backtesting module created.
- Logging/audit module completed.
- Demo testing checklist added.
- WinForms designer compatibility fixes completed.
- UI stabilization and polish completed.
- MainForm design separation completed.
- Auto Bot diagnostics added.
- MT5 trade payload compatibility fixed.
- EA broker-symbol resolver added.
- EA deployment helper added.
- EA deploy status notification added.
- App icon added.
- Auto Bot signal-card Execute button fixed.
- Manual Play-button execution enforced.
- Duplicate execution issue fixed.
- Trade row P/L and auto-close controls added.
- Auto-close pips/money sync added.
- AI API Config tab updated.
- Review Trade window added.
- MT5 EA market snapshot command added.
- Review Trade live refresh added.
- Allowed Pair dropdown added.
- Watch-folder selector updated.
- Auto Bot monitoring made automatic.
- MT5 connection settings moved into AI API Config tab.
- Review Trade prompt generation added.
- Ask AI parser updated.
- Lot-size dropdown added.
- AI output schema simplified to signal-file JSON format.

---

## Important Build Command

```bash
dotnet build MT5TradingBot.csproj --no-restore -o .\bin\VerifyBuild