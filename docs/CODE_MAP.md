# Code Map

Use this map first to avoid scanning unrelated folders.

- Shared models and reusable domain helpers: `Domain/` (`Domain/Common/` contains pip, lot, and pair-correlation helpers)
- Application workflows and cross-module interfaces: `Application/`
- Trading logic: `Trading/`
- MT5, IPC, AI, news, persistence, logging, config, notifications: `Infrastructure/`
- Windows Forms UI: `UI/`
- Runtime project files: `Data/Config/settings.json`, `Data/Database/trades.db`, `Data/Deployment/ea_deploy_status.json`, `Data/Logs/*.log`

Common placement rules:

- Put shared pure helpers in `Domain/Common/`.
- Put workflow-level reusable code in `Application/Common/`.
- Keep UI code in `UI/` only.
- Keep MT5, database, API, file, and external service code in `Infrastructure/`.
- AI analysis must not execute trades directly.
- Trade execution must stay behind risk validation and user approval.
