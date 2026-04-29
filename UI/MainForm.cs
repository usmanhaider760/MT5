using MT5TradingBot.Core;
using MT5TradingBot.Models;
using MT5TradingBot.Modules.BrokerIntegration;
using MT5TradingBot.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using System.ComponentModel;
using System.Globalization;

namespace MT5TradingBot.UI
{
    public sealed partial class MainForm : Form
    {
        // -- Services ----------------------------------------------
        private MT5Bridge? _bridge;
        private AutoBotService? _bot;
        private ClaudeSignalService? _claude;
        private readonly SettingsManager _settings = new();
        private AppSettings _cfg = new();
        private bool _warnedZeroAccountValues;
        private bool _shownEaDeployNotice;
        private readonly ToolTip _cardTooltip = new() { InitialDelay = 400, ShowAlways = true };
        private readonly object _signalExecutionLock = new();
        private readonly HashSet<string> _executingSignalIds = [];
        private readonly Dictionary<long, AutoCloseTarget> _autoCloseTargets = [];
        private readonly HashSet<long> _autoCloseInProgress = [];
        private bool _syncingAutoCloseValues;

        // -- Timers ------------------------------------------------
        private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = 2500 };

        private sealed class AutoCloseTarget
        {
            public bool Enabled { get; set; }
            public double TargetPips { get; set; }
            public double TargetMoney { get; set; }
        }

        private sealed record TradeReviewDecision(
            bool Approved,
            bool AutoCloseEnabled,
            double TargetPips,
            double TargetMoney);

        // ==========================================================
        public MainForm()
        {
            InitializeComponent();
            AppIcon.ApplyTo(this);
            ApplyStableLayout();

            if (!IsDesignerHosted())
            {
                _tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
                _txtClaudePrompt.Text = ClaudeConfig.DefaultPrompt;
                WireEvents();
                _txtJson.Text = DefaultJsonSample();
                _clockTimer.Start();
                _ = InitAsync();
            }
        }

        private bool IsDesignerHosted() =>
            DesignMode || LicenseManager.UsageMode == LicenseUsageMode.Designtime;

        private async Task InitAsync()
        {
            await _settings.LoadAsync();
            _cfg = _settings.Current;
            ApplySettingsToUI();

            if (_cfg.AutoConnectOnLaunch)
                await ConnectAsync();

            if (_cfg.Bot.AutoStartOnLaunch && _bridge?.IsConnected == true)
                await StartBotAsync();

            Log(_bridge?.IsConnected == true
                ? "MT5 Trading Bot ready. MT5 is connected."
                : "MT5 Trading Bot ready. Connect to MT5 to begin.", C_ACCENT);
            ShowEaDeployNoticeIfNeeded();
            await RefreshSignalFeedAsync();
        }

        // ==========================================================
        //  WIRE EVENTS - named handlers only, no lambdas
        // ==========================================================
        private void WireEvents()
        {
            _clockTimer.Tick    += ClockTimer_Tick;
            _refreshTimer.Tick  += RefreshTimer_Tick;
            this.FormClosing    += OnFormClosingAsync;
            _tabControl.DrawItem += DrawTabItem;

            _btnConnect.Click           += BtnConnect_Click;
            _btnDisconnect.Click        += BtnDisconnect_Click;
            _chkAutoConn.CheckedChanged += ChkAutoConn_CheckedChanged;

            _cmbPair.SelectedIndexChanged      += CmbPair_SelectedIndexChanged;
            _cmbDir.SelectedIndexChanged       += CmbDir_SelectedIndexChanged;
            _cmbOrderType.SelectedIndexChanged += CmbOrderType_SelectedIndexChanged;
            _txtEntry.TextChanged              += TxtEntry_TextChanged;
            _txtSL.TextChanged                 += TxtSL_TextChanged;
            _txtTP.TextChanged                 += TxtTP_TextChanged;
            _chkAutoLot.CheckedChanged         += ChkAutoLot_CheckedChanged;
            _txtLot.TextChanged                += TxtLot_TextChanged;
            _btnBuy.Click                      += BtnBuy_Click;
            _btnSell.Click                     += BtnSell_Click;

            _txtJson.DragEnter   += TxtJson_DragEnter;
            _txtJson.DragDrop    += TxtJson_DragDrop;
            _btnJsonLoad.Click   += BtnJsonLoad_Click;
            _btnJsonExec.Click   += BtnJsonExec_Click;
            _btnJsonFmt.Click    += BtnJsonFmt_Click;
            _btnJsonSample.Click += BtnJsonSample_Click;

            _btnClosePos.Click    += BtnClosePos_Click;
            _btnCloseAllPos.Click += BtnCloseAllPos_Click;
            _btnRefreshPos.Click  += BtnRefreshPos_Click;

            _btnImportHistory.Click += BtnImportHistory_Click;
            _btnClearHistory.Click  += BtnClearHistory_Click;

            _btnStartBot.Click        += BtnStartBot_Click;
            _btnStopBot.Click         += BtnStopBot_Click;
            _btnOpenFolder.Click      += BtnOpenFolder_Click;
            _btnBotInstructions.Click += BtnBotInstructions_Click;

            _btnStartClaude.Click  += BtnStartClaude_Click;
            _btnStopClaude.Click   += BtnStopClaude_Click;
            _btnResetPrompt.Click  += BtnResetPrompt_Click;

            _btnClearLog.Click += BtnClearLog_Click;
            _btnSaveLog.Click  += BtnSaveLog_Click;
        }

        // ==========================================================
        //  CONNECT / DISCONNECT
        // ==========================================================
        private async Task ConnectAsync()
        {
            _bridge?.Dispose();

            _cfg.Mt5 = new MT5Settings
            {
                Mode = _cmbMode.SelectedIndex == 0 ? ConnectionMode.NamedPipe : ConnectionMode.Socket,
                PipeName = _txtPipeName.Text.Trim(),
                TimeoutMs = 5000,
                ReconnectIntervalMs = 5000
            };

            _bridge = new MT5Bridge(_cfg.Mt5);
            _bridge.OnLog += msg => Log(msg);
            _bridge.OnConnectionChanged += SetConnectedUI;

            SetBtnState(_btnConnect, false);
            Log("Connecting to MT5...", C_ACCENT);

            bool ok = await _bridge.PingAsync();
            if (ok)
            {
                _bridge.StartReconnectLoop();
                _refreshTimer.Start();
                Log("[OK] Connected to MT5 EA", C_GREEN);
                await RefreshAsync();
                ShowEaDeployNoticeIfNeeded();
            }
            else
            {
                Log("[ERROR] Cannot connect. Ensure:\n" +
                    "  1. MT5 is open\n" +
                    "  2. TradingBotEA.ex5 is attached to a chart\n" +
                    "  3. AutoTrading (green button) is ON in MT5\n" +
                    "  4. Pipe name matches exactly", C_RED);
            }

            SetBtnState(_btnConnect, true);
        }

        private async Task DisconnectAsync()
        {
            _refreshTimer.Stop();
            if (_bot?.IsRunning == true)
                await StopBotAsync();
            _bridge?.Dispose();
            _bridge = null;
            SetConnectedUI(false);
            Log("Disconnected.");
        }

        // ==========================================================
        //  TRADE EXECUTION
        // ==========================================================
        private async Task SubmitTradeAsync(TradeType dir)
        {
            if (!AssertConnected()) return;

            if (!double.TryParse(_txtSL.Text, out double sl) || sl == 0)
            { Log("[ERROR] Invalid Stop Loss", C_RED); return; }
            if (!double.TryParse(_txtTP.Text, out double tp) || tp == 0)
            { Log("[ERROR] Invalid Take Profit", C_RED); return; }

            double.TryParse(_txtEntry.Text, out double entry);
            double.TryParse(_txtTP2.Text, out double tp2);
            double.TryParse(_txtLot.Text, out double lot);
            if (lot < 0.01) lot = 0.01;

            var req = new TradeRequest
            {
                Pair      = _cmbPair.SelectedItem?.ToString() ?? "GBPUSD",
                TradeType = dir,
                OrderType = _cmbOrderType.SelectedIndex switch
                { 1 => OrderType.LIMIT, 2 => OrderType.STOP, _ => OrderType.MARKET },
                EntryPrice  = entry,
                StopLoss    = sl,
                TakeProfit  = tp,
                TakeProfit2 = tp2,
                LotSize     = _chkAutoLot.Checked ? 0.01 : lot,
                MoveSLToBreakevenAfterTP1 = _chkMoveSLBE.Checked,
                MagicNumber = _cfg.Bot.MagicNumber,
                Comment     = "Manual"
            };

            TradeResult result = _bot != null
                ? await _bot.ExecuteTradeWithValidationAsync(req)
                : await _bridge!.OpenTradeAsync(req);

            Log(result.IsSuccess ? $"[OK] {result}" : $"[ERROR] {result}", result.IsSuccess ? C_GREEN : C_RED);
            AddHistoryRow(req, result);
        }

        private async Task ExecuteJsonAsync()
        {
            if (!AssertConnected()) return;
            try
            {
                var req = JsonConvert.DeserializeObject<TradeRequest>(_txtJson.Text);
                if (req == null) { Log("[ERROR] Invalid JSON structure", C_RED); return; }

                var (valid, err) = req.Validate();
                if (!valid) { Log($"[ERROR] Validation: {err}", C_RED); return; }

                TradeResult result = _bot != null
                    ? await _bot.ExecuteTradeWithValidationAsync(req)
                    : await _bridge!.OpenTradeAsync(req);

                Log(result.IsSuccess ? $"[OK] {result}" : $"[ERROR] {result}", result.IsSuccess ? C_GREEN : C_RED);
                AddHistoryRow(req, result);
            }
            catch (JsonException ex) { Log($"[ERROR] JSON parse error: {ex.Message}", C_RED); }
        }

        private void LoadJsonFile()
        {
            using var d = new OpenFileDialog { Filter = "JSON files (*.json)|*.json|All (*.*)|*.*" };
            if (d.ShowDialog() == DialogResult.OK)
                _txtJson.Text = File.ReadAllText(d.FileName);
        }

        private void FormatJson()
        {
            try
            {
                var obj = JsonConvert.DeserializeObject(_txtJson.Text);
                _txtJson.Text = JsonConvert.SerializeObject(obj, Formatting.Indented);
            }
            catch { Log("[ERROR] Cannot format - invalid JSON", C_RED); }
        }

        // ==========================================================
        //  AUTO BOT
        // ==========================================================
        private async Task StartBotAsync()
        {
            try
            {
                SetBotBadge("CHECKING...", C_ACCENT);
                Log("[BOT] Checking requirements before monitoring...", C_ACCENT);
                bool allOk = true;

                // ── 1. MT5 connection ──────────────────────────────
                if (_bridge?.IsConnected != true)
                {
                    Log("[BOT] ✗ MT5 is not connected. Click Connect first.", C_RED);
                    SetBotBadge("BOT STOPPED - MT5 NOT CONNECTED", C_RED);
                    return;
                }
                Log("[BOT] ✓ MT5 is connected.", C_GREEN);

                // ── 2. Watch folder ────────────────────────────────
                _cfg.Claude = ReadClaudeConfigFromUI();
                UpdateAiApiConfigStatus(_cfg.Claude, logResult: true);

                _cfg.Bot = ReadBotConfigFromUI();
                string watchFolder = _cfg.Bot.WatchFolder.Trim();
                if (string.IsNullOrWhiteSpace(watchFolder))
                {
                    Log("[BOT] ✗ Watch folder is empty. Set a folder path first.", C_RED);
                    SetBotBadge("BOT STOPPED - WATCH FOLDER EMPTY", C_RED);
                    return;
                }
                Directory.CreateDirectory(watchFolder);
                Log($"[BOT] ✓ Watch folder: {watchFolder}", C_GREEN);

                // ── 3. Account info ────────────────────────────────
                var account = await _bridge.GetAccountInfoAsync();
                if (account != null)
                {
                    Log($"[BOT] ✓ Account #{account.AccountNumber} {account.Server} | Balance ${account.Balance:F2} | Equity ${account.Equity:F2}", C_GREEN);
                    if (account.Balance == 0 && account.Equity == 0)
                    {
                        Log("[BOT] ⚠ Balance and Equity are 0. Ensure your MT5 account has funds.", C_YELLOW);
                        allOk = false;
                    }
                }
                else
                {
                    Log("[BOT] ⚠ Could not fetch account info from MT5.", C_YELLOW);
                    allOk = false;
                }

                // ── 4. Open positions ──────────────────────────────
                var positions = await _bridge.GetPositionsAsync();
                Log($"[BOT] ✓ MT5 has {positions.Count} open position(s).", C_GREEN);

                // ── 5. Pending signals ─────────────────────────────
                var pendingFiles = Directory.GetFiles(watchFolder, "*.json");
                if (pendingFiles.Length == 0)
                    Log("[BOT] ✓ Watch folder is empty — ready to receive signals.", C_GREEN);
                else
                    Log($"[BOT] ✓ {pendingFiles.Length} pending signal file(s) in folder.", C_ACCENT);

                // ── 6. Config summary ──────────────────────────────
                Log($"[BOT] Settings → Risk: {_cfg.Bot.MaxRiskPercent:F1}% | Max trades/day: {_cfg.Bot.MaxTradesPerDay} | Min R:R: {_cfg.Bot.MinRRRatio:F1} | Enforce R:R: {_cfg.Bot.EnforceRR}", C_ACCENT);
                int pairCount = _cfg.Bot.AllowedPairs.Count;
                string pairSummary = pairCount == 0 ? "All pairs"
                    : pairCount <= 5 ? string.Join(", ", _cfg.Bot.AllowedPairs)
                    : string.Join(", ", _cfg.Bot.AllowedPairs.Take(5)) + $" +{pairCount - 5} more";
                Log($"[BOT] Allowed pairs: {pairSummary}", C_ACCENT);

                if (!allOk)
                    Log("[BOT] ⚠ Some checks have warnings. Review above before trading.", C_YELLOW);

                // ── 7. Start monitoring ────────────────────────────
                Log("[BOT] Monitoring only: trades will NOT start from this button.", C_ACCENT);
                Log("[BOT] To place a trade, click the Play button on the signal row.", C_ACCENT);

                await _settings.SaveAsync(_cfg);
                await (_bot?.DisposeAsync() ?? ValueTask.CompletedTask);

                _bot = new AutoBotService(_bridge, _cfg.Bot) { ManualExecuteOnly = true };
                _bot.OnLog += msg => Log(msg);
                _bot.OnTradeExecuted += r =>
                {
                    Log(r.IsSuccess ? $"[BOT] Trade: {r}" : $"[BOT] Rejected: {r.ErrorMessage}",
                        r.IsSuccess ? C_GREEN : C_RED);
                    _ = RefreshBotTradeStatusAsync(r);
                };
                _bot.OnBotStatusChanged += on => UpdateBotBadge(on);
                _bot.OnSignalUpdate += info => AddOrUpdateSignalCard(info);

                await _bot.StartAsync();
                Log("[BOT] Monitoring started — new signal files will appear in the feed below.", C_GREEN);
                Log("[BOT] Use the Play button on each signal row to place a trade.", C_ACCENT);
            }
            catch (Exception ex)
            {
                SetBotBadge("BOT START FAILED", C_RED);
                Log($"[BOT] Start failed: {ex.Message}", C_RED);
            }
        }

        private async Task RefreshBotTradeStatusAsync(TradeResult result)
        {
            try
            {
                await RefreshAsync();
                if (_bridge?.IsConnected != true) return;

                var positions = await _bridge.GetPositionsAsync();
                Log(result.IsSuccess
                    ? $"[BOT] MT5 accepted trade. Ticket #{result.Ticket}. Open positions now: {positions.Count}."
                    : $"[BOT] MT5/account status refreshed after rejection. Open positions: {positions.Count}.",
                    result.IsSuccess ? C_GREEN : C_YELLOW);
            }
            catch (Exception ex)
            {
                Log($"[BOT] Could not refresh MT5 trade status: {ex.Message}", C_YELLOW);
            }
        }

        private async Task StopBotAsync()
        {
            if (_bot == null) return;
            await _bot.DisposeAsync();
            _bot = null;
            UpdateBotBadge(false);
        }

        // ==========================================================
        //  AI API CONFIGURATION
        // ==========================================================
        private async Task StartClaudeAsync()
        {
            if (_bridge?.IsConnected != true)
            { Log("[ERROR] Connect to MT5 first.", C_RED); return; }

            _cfg.Claude = ReadClaudeConfigFromUI();
            await _settings.SaveAsync(_cfg);

            if (_claude != null) { await _claude.DisposeAsync(); _claude = null; }

            var bridge = _bridge;
            _claude = new ClaudeSignalService(
                bridge,
                _cfg.Claude,
                req => _bot != null
                    ? _bot.ExecuteTradeWithValidationAsync(req)
                    : bridge.OpenTradeAsync(req));

            _claude.OnLog            += msg => Log($"[AI] {msg}");
            _claude.OnSignalGenerated += req => Log($"[AI] Signal: {req}", C_ACCENT);
            _claude.OnStatusChanged  += on => UpdateClaudeBadge(on);

            try { await _claude.StartAsync(); }
            catch (Exception ex)
            {
                Log($"[ERROR] AI monitor start failed: {ex.Message}", C_RED);
                await _claude.DisposeAsync();
                _claude = null;
            }
        }

        private async Task StopClaudeAsync()
        {
            if (_claude == null) return;
            await _claude.DisposeAsync();
            _claude = null;
            UpdateClaudeBadge(false);
        }

        private void UpdateClaudeBadge(bool running)
        {
            UIThread(() =>
            {
                _lblClaudeBadge.Text      = running ? "AI MONITOR RUNNING" : "AI MONITOR STOPPED";
                _lblClaudeBadge.ForeColor = running ? C_GREEN : C_RED;
                _btnStartClaude.Enabled   = !running;
                _btnStopClaude.Enabled    = running;
            });
        }

        private void UpdateAiApiConfigStatus(ClaudeConfig config, bool logResult = false)
        {
            bool configured = !string.IsNullOrWhiteSpace(config.ApiKey)
                && !string.IsNullOrWhiteSpace(config.Model);
            UIThread(() =>
            {
                if (_claude?.IsRunning == true) return;
                _lblClaudeBadge.Text = configured
                    ? "AI API CONFIGURED - NO-TOKEN CHECK"
                    : "AI API NOT CONFIGURED";
                _lblClaudeBadge.ForeColor = configured ? C_GREEN : C_YELLOW;
            });

            if (!logResult) return;
            Log(configured
                    ? $"[AI] API configuration found for {config.Model}. Startup check did not send a prompt or consume tokens."
                    : "[AI] API key/model missing. Configure the AI API Config tab before AI analysis.",
                configured ? C_GREEN : C_YELLOW);
        }

        // ==========================================================
        //  POSITIONS
        // ==========================================================
        private async Task RefreshPositionsAsync()
        {
            if (_bridge?.IsConnected != true) return;
            var positions = await _bridge.GetPositionsAsync();
            UIThread(() =>
            {
                _gridPos.Rows.Clear();
                foreach (var p in positions)
                {
                    int i = _gridPos.Rows.Add(
                        p.Ticket, p.Symbol, p.Type, $"{p.Lots:F2}",
                        $"{p.OpenPrice:F5}", $"{p.CurrentPrice:F5}",
                        $"{p.StopLoss:F5}", $"{p.TakeProfit:F5}",
                        $"{p.Profit:F2}", $"{p.ProfitPips:F1}",
                        p.OpenTime.ToString("HH:mm:ss"), p.Comment);
                    _gridPos.Rows[i].DefaultCellStyle.ForeColor = p.Profit >= 0 ? C_GREEN : C_RED;
                }
                UpdateSignalCardsWithPositions(positions);
            });
            await ProcessAutoCloseTargetsAsync(positions);
        }

        private async Task CloseSelectedAsync()
        {
            if (_bridge == null || _gridPos.SelectedRows.Count == 0) return;
            if (!long.TryParse(_gridPos.SelectedRows[0].Cells[0].Value?.ToString(), out long t)) return;
            if (Confirm($"Close ticket #{t}?"))
            {
                bool ok = await _bridge.CloseTradeAsync(t);
                Log(ok ? $"[OK] Closed #{t}" : $"[ERROR] Failed to close #{t}", ok ? C_GREEN : C_RED);
                await RefreshPositionsAsync();
            }
        }

        private async Task CloseAllAsync()
        {
            if (_bridge == null) return;
            if (!Confirm("Close ALL open positions? This cannot be undone.")) return;
            var positions = await _bridge.GetPositionsAsync();
            int count = 0;
            foreach (var p in positions)
                if (await _bridge.CloseTradeAsync(p.Ticket)) count++;
            Log($"Closed {count}/{positions.Count} positions.", C_YELLOW);
            await RefreshPositionsAsync();
        }

        // ==========================================================
        //  REFRESH
        // ==========================================================
        private async Task RefreshAsync()
        {
            if (_bridge?.IsConnected != true) return;
            try
            {
                var account = await _bridge.GetAccountInfoAsync();
                if (account != null) UpdateAccountUI(account);
                await RefreshPositionsAsync();
            }
            catch (Exception ex) { Log($"Refresh error: {ex.Message}", C_RED); }
        }

        private async Task OnRefreshTickAsync()
        {
            try { await RefreshAsync(); }
            catch { /* swallow on timer */ }
        }

        // ==========================================================
        //  R:R CALCULATOR
        // ==========================================================
        private void RecalcRR()
        {
            try
            {
                if (!double.TryParse(_txtSL.Text, out double sl) || sl == 0) return;
                if (!double.TryParse(_txtTP.Text, out double tp) || tp == 0) return;

                double.TryParse(_txtEntry.Text, out double entry);
                if (entry == 0) entry = (sl + tp) / 2.0;

                double rr = LotCalculator.RiskRewardRatio(entry, sl, tp);

                double lots = 0.01;
                if (!_chkAutoLot.Checked) double.TryParse(_txtLot.Text, out lots);

                string sym    = _cmbPair.SelectedItem?.ToString() ?? "GBPUSD";
                double risk   = LotCalculator.DollarRisk(lots, entry, sl, sym);
                double profit = LotCalculator.DollarProfit(lots, entry, tp, sym);

                _lblRR.Text           = $"R:R  1 : {rr:F2}";
                _lblRR.ForeColor      = rr >= 1.5 ? C_GREEN : rr >= 1.0 ? C_YELLOW : C_RED;
                _lblDollarRisk.Text   = $"Risk  ${risk:F2}";
                _lblDollarProfit.Text = $"Profit  ${profit:F2}";
            }
            catch { /* parsing incomplete */ }
        }

        // ==========================================================
        //  UI HELPERS
        // ==========================================================
        private void UpdateAccountUI(AccountInfo a)
        {
            UIThread(() =>
            {
                _lblAccNum.Text     = $"#{a.AccountNumber}  {a.Server}";
                _lblBalance.Text    = $"Balance: ${a.Balance:F2}";
                _lblEquity.Text     = $"Equity: ${a.Equity:F2}";
                _lblFreeMargin.Text = $"Free: ${a.FreeMargin:F2}";
                _lblPnl.Text        = $"P&L: {(a.Profit >= 0 ? "+" : "")}${a.Profit:F2}";
                _lblPnl.ForeColor   = a.Profit >= 0 ? C_GREEN : C_RED;
                _lblMarginLvl.Text  = $"ML: {a.MarginLevel:F0}%";

                bool hasAccountIdentity = a.AccountNumber > 0 || !string.IsNullOrWhiteSpace(a.Server);
                bool accountValuesAreZero = a.Balance == 0 && a.Equity == 0 && a.FreeMargin == 0;

                _lblBalance.ForeColor = accountValuesAreZero && hasAccountIdentity ? C_YELLOW : C_TEXT;
                _lblEquity.ForeColor = accountValuesAreZero && hasAccountIdentity ? C_YELLOW : C_TEXT;
                _lblFreeMargin.ForeColor = accountValuesAreZero && hasAccountIdentity ? C_YELLOW : C_TEXT;

                if (hasAccountIdentity && accountValuesAreZero && !_warnedZeroAccountValues)
                {
                    _warnedZeroAccountValues = true;
                    Log("MT5 is connected, but it returned Balance/Equity/Free Margin as 0.00. If MT5 Toolbox > Trade also shows 0.00, top up or recreate the Exness demo account. If MT5 shows funds, reattach TradingBotEA after login and reconnect the bot.", C_YELLOW);
                }
                else if (!accountValuesAreZero)
                {
                    _warnedZeroAccountValues = false;
                }
            });
        }

        private void SetConnectedUI(bool connected)
        {
            UIThread(() =>
            {
                _pnlDot.BackColor        = connected ? C_GREEN : C_RED;
                _lblConnStatus.Text      = connected ? "Connected" : "Disconnected";
                _lblConnStatus.ForeColor = connected ? C_GREEN : C_RED;
                _btnDisconnect.Enabled   = connected;
                if (!connected) _refreshTimer.Stop();
            });
        }

        private void UpdateBotBadge(bool running)
        {
            UIThread(() =>
            {
                _lblBotBadge.Text      = running ? "BOT MONITORING" : "BOT STOPPED";
                _lblBotBadge.ForeColor = running ? C_ACCENT : C_RED;
                _btnStartBot.Enabled   = !running;
                _btnStopBot.Enabled    = running;
            });
        }

        private void SetBotBadge(string text, Color color)
        {
            UIThread(() =>
            {
                _lblBotBadge.Text = text;
                _lblBotBadge.ForeColor = color;
            });
        }

        private void ShowEaDeployNoticeIfNeeded()
        {
            if (_shownEaDeployNotice) return;

            try
            {
                string statusPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "MT5TradingBot",
                    "ea_deploy_status.json");

                if (!File.Exists(statusPath)) return;

                var status = JObject.Parse(File.ReadAllText(statusPath));
                bool needsReload = status.Value<bool?>("needs_mt5_reload") == true;
                if (!needsReload) return;

                string compileResult = status.Value<string>("compile_result") ?? "compile completed";
                string ex5Path = status.Value<string>("ex5_path") ?? "TradingBotEA.ex5";
                string deployedAtText = "";
                if (DateTime.TryParse(status.Value<string>("deployed_at"), out var deployedAt))
                    deployedAtText = $" at {deployedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss}";

                Log($"[EA] TradingBotEA compiled{deployedAtText} ({compileResult}).", C_GREEN);
                Log($"[EA] Reload required in MT5: remove and re-attach TradingBotEA on the chart, or restart MT5. EX5: {ex5Path}", C_YELLOW);
                _shownEaDeployNotice = true;
            }
            catch (Exception ex)
            {
                Log($"[EA] Could not read EA deployment status: {ex.Message}", C_YELLOW);
                _shownEaDeployNotice = true;
            }
        }

        private void UpdateBuySellColors()
        {
            bool buy = _cmbDir.SelectedItem?.ToString() == "BUY";
            _btnBuy.BackColor  = buy  ? C_GREEN : Color.FromArgb(45, 45, 60);
            _btnSell.BackColor = !buy ? C_RED   : Color.FromArgb(45, 45, 60);
        }

        private void AddHistoryRow(TradeRequest req, TradeResult result)
        {
            UIThread(() =>
                _gridHistory.Rows.Insert(0,
                    DateTime.Now.ToString("HH:mm:ss"), req.Id, req.Pair,
                    req.TradeType.ToString(), $"{req.LotSize:F2}",
                    $"{req.EntryPrice:F5}", $"{req.StopLoss:F5}", $"{req.TakeProfit:F5}",
                    result.Ticket, result.Status, $"{result.ExecutedPrice:F5}",
                    result.ErrorMessage));
        }

        private void LoadHistoryFromCsv()
        {
            using var d = new OpenFileDialog { Filter = "CSV files|*.csv|All|*.*" };
            if (d.ShowDialog() != DialogResult.OK) return;
            _gridHistory.Rows.Clear();
            foreach (var line in File.ReadLines(d.FileName).Skip(1))
            {
                var p = line.Split(',');
                if (p.Length >= 12)
                    _gridHistory.Rows.Add(p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9], p[10], p[11]);
            }
        }

        private void ApplySettingsToUI()
        {
            _cmbMode.SelectedIndex   = _cfg.Mt5.Mode == ConnectionMode.NamedPipe ? 0 : 1;
            _txtPipeName.Text        = _cfg.Mt5.PipeName;
            _chkAutoConn.Checked     = _cfg.AutoConnectOnLaunch;
            _txtWatchFolder.Text     = _cfg.Bot.WatchFolder;
            _nudRisk.Value           = (decimal)_cfg.Bot.MaxRiskPercent;
            _nudMaxTrades.Value      = _cfg.Bot.MaxTradesPerDay;
            _nudPollMs.Value         = _cfg.Bot.PollIntervalMs;
            _txtAllowedPairs.Text    = string.Join(",", _cfg.Bot.AllowedPairs);
            _nudMinRR.Value          = (decimal)_cfg.Bot.MinRRRatio;
            _nudDrawdownPct.Value    = (decimal)_cfg.Bot.EmergencyCloseDrawdownPct;
            _nudRetry.Value          = _cfg.Bot.RetryCount;
            _txtClaudeApiKey.Text    = _cfg.Claude.ApiKey;
            _txtClaudeSymbols.Text   = string.Join(",", _cfg.Claude.WatchSymbols);
            _nudClaudePollSec.Value  = _cfg.Claude.PollIntervalSeconds;
            _txtClaudePrompt.Text    = string.IsNullOrEmpty(_cfg.Claude.SystemPrompt)
                ? ClaudeConfig.DefaultPrompt : _cfg.Claude.SystemPrompt;
            _lblModelValue.Text      = _cfg.Claude.Model;
            _lblClaudeNote1.Text     = "Startup checks validate saved AI configuration only; no prompt is sent.";
            _lblClaudeNote2.Text     = "Tokens are used only when AI analysis/monitoring sends market data to the provider.";
            UpdateAiApiConfigStatus(_cfg.Claude);
        }

        private ClaudeConfig ReadClaudeConfigFromUI() => new()
        {
            ApiKey              = _txtClaudeApiKey.Text.Trim(),
            WatchSymbols        = [.. _txtClaudeSymbols.Text.Split(',').Select(s => s.Trim().ToUpper()).Where(s => s.Length > 0)],
            PollIntervalSeconds = (int)_nudClaudePollSec.Value,
            SystemPrompt        = _txtClaudePrompt.Text,
            Model               = string.IsNullOrWhiteSpace(_cfg.Claude.Model) ? "claude-opus-4-7" : _cfg.Claude.Model
        };

        private BotConfig ReadBotConfigFromUI() => new()
        {
            Enabled                  = true,
            WatchFolder              = _txtWatchFolder.Text,
            MaxRiskPercent           = (double)_nudRisk.Value,
            MaxTradesPerDay          = (int)_nudMaxTrades.Value,
            PollIntervalMs           = (int)_nudPollMs.Value,
            AllowedPairs             = [.. _txtAllowedPairs.Text.Split(',').Select(p => p.Trim().ToUpper())],
            AutoLotCalculation       = _chkAutoLotBot.Checked,
            MinRRRatio               = (double)_nudMinRR.Value,
            EnforceRR                = _chkEnforceRR.Checked,
            DrawdownProtectionEnabled = _chkDrawdown.Checked,
            EmergencyCloseDrawdownPct = (double)_nudDrawdownPct.Value,
            RetryOnFail              = true,
            RetryCount               = (int)_nudRetry.Value,
            RetryDelayMs             = 1000,
            AutoStartOnLaunch        = _chkAutoStart.Checked,
            MagicNumber              = 999001
        };

        // -- Log ---------------------------------------------------
        public void Log(string msg, Color? color = null)
        {
            if (InvokeRequired) { Invoke(() => Log(msg, color)); return; }
            string line = $"[{DateTime.Now:HH:mm:ss}] {msg}\n";
            Serilog.Log.Information("{msg}", msg);
            _txtLog.SuspendLayout();
            int start = _txtLog.TextLength;
            _txtLog.AppendText(line);
            _txtLog.Select(start, line.Length);
            _txtLog.SelectionColor = color ?? C_TEXT;
            _txtLog.Select(_txtLog.TextLength, 0);
            _txtLog.ResumeLayout();
            _txtLog.ScrollToCaret();
        }

        // -- Utility -----------------------------------------------
        private void UIThread(Action a) { if (InvokeRequired) Invoke(a); else a(); }

        private bool AssertConnected()
        {
            if (_bridge?.IsConnected == true) return true;
            Log("[ERROR] Not connected to MT5. Click Connect first.", C_RED);
            return false;
        }

        private static bool Confirm(string msg) =>
            MessageBox.Show(msg, "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;

        private static void SetBtnState(Button btn, bool enabled)
        {
            if (btn.InvokeRequired) btn.Invoke(() => btn.Enabled = enabled);
            else btn.Enabled = enabled;
        }

        private async void OnFormClosingAsync(object? sender, FormClosingEventArgs e)
        {
            _refreshTimer.Stop();
            await StopClaudeAsync();
            await StopBotAsync();
            await _settings.SaveAsync(_cfg);
            _bridge?.Dispose();
        }

        [System.Runtime.InteropServices.DllImport("Gdi32.dll")]
        private static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int cx, int cy);

        // ==========================================================
        //  STATIC DATA
        // ==========================================================
        private static string DefaultJsonSample() =>
            JsonConvert.SerializeObject(new TradeRequest
            {
                Pair       = "GBPUSD", TradeType = TradeType.BUY,
                OrderType  = OrderType.MARKET, EntryPrice = 0,
                StopLoss   = 1.34750, TakeProfit = 1.35200,
                TakeProfit2 = 1.35500, LotSize = 0.01,
                Comment    = "BotSignal", MagicNumber = 999001,
                MoveSLToBreakevenAfterTP1 = true
            }, Formatting.Indented);

        private static string BotHelpText() => """
            AUTO BOT MONITORING - HOW IT WORKS
            -------------------------------------

            1. Connect the app to MT5 (Named Pipe)
            2. Start Monitoring -> it watches your folder
            3. Drop a .json file into the folder
            4. Click the Play button on a signal row to start trade

            Monitoring then:
              - Reads and validates the JSON
              - Shows the signal row in the feed
              - Waits for your Play-button approval

            Play button execution then:
              - Reads and validates the JSON
              - Checks: pair allowed, daily limit,
                R:R ratio, free margin, equity
              - Auto-calculates lot size from risk %
              - Sends trade to MT5 via named pipe
              - Retries on failure (configurable)
              - Moves file to /executed or /rejected
              - Logs to trade_history.csv

            Every 2 seconds the bot also:
              - Checks SL -> breakeven (at 60% TP)
              - Monitors drawdown -> emergency close
              - Polls folder (watcher backup)

            SIGNAL FOLDERS:
            -------------------------------------
            signals/              <- drop files here
            signals/executed/     <- success
            signals/rejected/     <- validation fail
            signals/error/        <- bad JSON
            signals/trade_history.csv <- full log

            SAMPLE JSON FILE:
            -------------------------------------
            {
              "pair": "GBPUSD",
              "trade_type": "BUY",
              "order_type": "MARKET",
              "entry_price": 0,
              "stop_loss": 1.34750,
              "take_profit": 1.35200,
              "lot_size": 0.01,
              "comment": "MyBot",
              "magic_number": 999001
            }

            REQUIREMENTS:
            -------------------------------------
            - MT5 running with TradingBotEA.ex5
            - AutoTrading ON (green button in MT5)
            - Pipe name matches in both apps
            """;

        // ==========================================================
        //  NAMED EVENT HANDLERS
        // ==========================================================
        private void ClockTimer_Tick(object? sender, EventArgs e)
            => _lblTime.Text = $"UTC {DateTime.UtcNow:HH:mm:ss}  |  Local {DateTime.Now:HH:mm:ss}";

        private async void RefreshTimer_Tick(object? sender, EventArgs e)  => await OnRefreshTickAsync();
        private async void BtnConnect_Click(object? sender, EventArgs e)    => await ConnectAsync();
        private async void BtnDisconnect_Click(object? sender, EventArgs e) => await DisconnectAsync();
        private void ChkAutoConn_CheckedChanged(object? sender, EventArgs e) => _cfg.AutoConnectOnLaunch = _chkAutoConn.Checked;

        private void CmbPair_SelectedIndexChanged(object? sender, EventArgs e)      => RecalcRR();
        private void CmbDir_SelectedIndexChanged(object? sender, EventArgs e)       { UpdateBuySellColors(); RecalcRR(); }
        private void CmbOrderType_SelectedIndexChanged(object? sender, EventArgs e) => _txtEntry.Enabled = _cmbOrderType.SelectedIndex != 0;
        private void TxtEntry_TextChanged(object? sender, EventArgs e) => RecalcRR();
        private void TxtSL_TextChanged(object? sender, EventArgs e)    => RecalcRR();
        private void TxtTP_TextChanged(object? sender, EventArgs e)    => RecalcRR();
        private void TxtLot_TextChanged(object? sender, EventArgs e)   => RecalcRR();
        private void ChkAutoLot_CheckedChanged(object? sender, EventArgs e) { _txtLot.Enabled = !_chkAutoLot.Checked; RecalcRR(); }

        private async void BtnBuy_Click(object? sender, EventArgs e)  => await SubmitTradeAsync(TradeType.BUY);
        private async void BtnSell_Click(object? sender, EventArgs e) => await SubmitTradeAsync(TradeType.SELL);

        private void TxtJson_DragEnter(object? sender, DragEventArgs e)
            => e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Copy : DragDropEffects.None;

        private void TxtJson_DragDrop(object? sender, DragEventArgs e)
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                _txtJson.Text = File.ReadAllText(files[0]);
        }

        private void BtnJsonLoad_Click(object? sender, EventArgs e)       => LoadJsonFile();
        private async void BtnJsonExec_Click(object? sender, EventArgs e) => await ExecuteJsonAsync();
        private void BtnJsonFmt_Click(object? sender, EventArgs e)        => FormatJson();
        private void BtnJsonSample_Click(object? sender, EventArgs e)     => _txtJson.Text = DefaultJsonSample();

        private async void BtnClosePos_Click(object? sender, EventArgs e)    => await CloseSelectedAsync();
        private async void BtnCloseAllPos_Click(object? sender, EventArgs e) => await CloseAllAsync();
        private async void BtnRefreshPos_Click(object? sender, EventArgs e)  => await RefreshPositionsAsync();

        private void BtnImportHistory_Click(object? sender, EventArgs e) => LoadHistoryFromCsv();
        private void BtnClearHistory_Click(object? sender, EventArgs e)  => _gridHistory.Rows.Clear();

        private async void BtnStartBot_Click(object? sender, EventArgs e) => await StartBotAsync();
        private async void BtnStopBot_Click(object? sender, EventArgs e)  => await StopBotAsync();

        private void BtnOpenFolder_Click(object? sender, EventArgs e)
        {
            Directory.CreateDirectory(_txtWatchFolder.Text);
            System.Diagnostics.Process.Start("explorer.exe", _txtWatchFolder.Text);
        }

        private void BtnBotInstructions_Click(object? sender, EventArgs e)
        {
            using var dlg = new Form
            {
                Text            = "How It Works — Auto Bot",
                Size            = new Size(560, 560),
                StartPosition   = FormStartPosition.CenterParent,
                BackColor       = Color.FromArgb(18, 18, 28),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox     = false,
                MinimizeBox     = false
            };
            var rtb = new RichTextBox
            {
                Dock         = DockStyle.Fill,
                ReadOnly     = true,
                BackColor    = Color.FromArgb(22, 22, 32),
                ForeColor    = Color.FromArgb(218, 218, 230),
                Font         = new Font("Consolas", 10F),
                BorderStyle  = BorderStyle.None,
                ScrollBars   = RichTextBoxScrollBars.Vertical,
                Text         = BotHelpText()
            };
            dlg.Controls.Add(rtb);
            dlg.ShowDialog(this);
        }

        // ── Signal Feed ───────────────────────────────────────────

        private async Task RefreshSignalFeedAsync()
        {
            string root = _cfg.Bot.WatchFolder;
            if (!Directory.Exists(root)) return;

            await LoadSignalFolderToFeedAsync(Path.Combine(root, "error"),    SignalCardStatus.Error);
            await LoadSignalFolderToFeedAsync(Path.Combine(root, "rejected"), SignalCardStatus.Rejected);
            await LoadSignalFolderToFeedAsync(Path.Combine(root, "executed"), SignalCardStatus.Executed);
            await LoadSignalFolderToFeedAsync(root,                           SignalCardStatus.Pending);
        }

        private async Task LoadSignalFolderToFeedAsync(string folder, SignalCardStatus status)
        {
            if (!Directory.Exists(folder)) return;
            foreach (var file in Directory.GetFiles(folder, "*.json").OrderBy(File.GetLastWriteTime).Take(20))
            {
                try
                {
                    string json = await Task.Run(() => File.ReadAllText(file)).ConfigureAwait(false);
                    var req = JsonConvert.DeserializeObject<TradeRequest>(json);
                    if (req == null) continue;
                    AddOrUpdateSignalCard(new SignalCardInfo
                    {
                        SignalId   = req.Id,
                        FileName   = Path.GetFileName(file),
                        FilePath   = file,
                        Pair       = req.Pair,
                        TradeType  = req.TradeType.ToString(),
                        StopLoss   = req.StopLoss,
                        TakeProfit = req.TakeProfit,
                        LotSize    = req.LotSize,
                        CreatedAt  = req.CreatedAt.ToLocalTime(),
                        Status     = status,
                        StatusText = status.ToString(),
                        Time       = File.GetLastWriteTime(file)
                    });
                }
                catch { }
            }
        }

        private void AddOrUpdateSignalCard(SignalCardInfo info)
        {
            if (InvokeRequired) { Invoke(() => AddOrUpdateSignalCard(info)); return; }

            var existing = _flpSignals.Controls.OfType<Panel>()
                .FirstOrDefault(p => (p.Tag as SignalCardInfo)?.SignalId == info.SignalId);

            if (existing != null)
            {
                UpdateCardStatus(existing, info);
                ReorderSignalFeed();
                return;
            }

            var card = BuildSignalCard(info);
            _flpSignals.SuspendLayout();
            _flpSignals.Controls.Add(card);
            _flpSignals.ResumeLayout(true);
            ReorderSignalFeed();
        }

        private void ReorderSignalFeed()
        {
            var cards = _flpSignals.Controls.OfType<Panel>().ToList();
            if (cards.Count < 2) return;

            // Executing first, then Pending, then everything else (preserve their relative order)
            static int Priority(Panel c) => (c.Tag as SignalCardInfo)?.Status switch
            {
                SignalCardStatus.Executing => 0,
                SignalCardStatus.Pending   => 1,
                _                          => 2
            };

            var ordered = cards.OrderBy(Priority).ToList();
            _flpSignals.SuspendLayout();
            for (int i = 0; i < ordered.Count; i++)
                _flpSignals.Controls.SetChildIndex(ordered[i], i);
            _flpSignals.ResumeLayout(true);
        }

        private Panel BuildSignalCard(SignalCardInfo info)
        {
            int w      = Math.Max(200, _flpSignals.ClientSize.Width - _flpSignals.Padding.Horizontal - 4);
            bool isBuy = info.TradeType.Equals("BUY", StringComparison.OrdinalIgnoreCase);
            var dirColor = isBuy ? Color.FromArgb(99, 179, 237) : Color.FromArgb(214, 164, 255);
            var (bgColor, stripeColor) = GetNeutralStatusColors(info.Status);

            var card = new Panel
            {
                Width     = w,
                Height    = 184,
                BackColor = bgColor,
                Margin    = new Padding(0, 0, 0, 5),
                Tag       = info
            };

            // Left status stripe
            card.Controls.Add(new Panel { Width = 5, Dock = DockStyle.Left, BackColor = stripeColor });

            // ── Row 1: direction+pair  |  action buttons ─────────────
            card.Controls.Add(new Label
            {
                Text      = $"{(isBuy ? "▲ BUY" : "▼ SELL")}  {info.Pair}",
                Font      = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold),
                ForeColor = dirColor,
                Location  = new Point(14, 8),
                AutoSize  = true
            });

            // ✕ Delete button (always visible, disabled while Executing)
            var btnDel = MakeCardButton("✕", Color.FromArgb(80, 30, 30), Color.FromArgb(252, 95, 95),
                "Delete — remove this signal card and file");
            btnDel.Anchor  = AnchorStyles.Top | AnchorStyles.Right;
            btnDel.Location = new Point(w - 28, 8);
            btnDel.Enabled  = info.Status != SignalCardStatus.Executing;
            btnDel.Tag      = "delete";
            btnDel.Click   += (_, _) => DeleteSignalCard(card);
            card.Controls.Add(btnDel);

            // ■ Close position button (only meaningful for Executed with ticket)
            var btnClose = MakeCardButton("■", Color.FromArgb(60, 20, 20), Color.FromArgb(252, 95, 95),
                "Close Position — close this trade on MT5");
            btnClose.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
            btnClose.Location = new Point(w - 56, 8);
            btnClose.Visible  = info.Status == SignalCardStatus.Executed && info.Ticket > 0;
            btnClose.Tag      = "close";
            btnClose.Click   += (_, _) => _ = CloseTradeFromCardAsync(card);
            card.Controls.Add(btnClose);

            // ▶ Execute / Retry button (Pending / Rejected / Error)
            var btnExec = MakeCardButton("▶", Color.FromArgb(20, 50, 30), Color.FromArgb(72, 199, 142),
                "Start Trade — send this signal to MT5 for execution");
            btnExec.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
            btnExec.Location = new Point(w - 84, 8);
            btnExec.Visible  = CanExecuteSignal(info);
            btnExec.Tag      = "execute";
            btnExec.Click   += (_, _) => _ = ExecuteSignalFromCardSafeAsync(card);
            card.Controls.Add(btnExec);

            // ── Row 2: status label ───────────────────────────────────
            var (statusText, statusColor) = GetNeutralStatusDisplay(info);
            card.Controls.Add(new Label
            {
                Text      = statusText,
                Font      = new Font("Segoe UI Semibold", 9F),
                ForeColor = statusColor,
                Location  = new Point(14, 32),
                AutoSize  = false,
                Size      = new Size(w - 20, 18),
                Tag       = "status"
            });

            // ── Row 3: SL / TP / Lots ─────────────────────────────────
            card.Controls.Add(new Label
            {
                Text      = $"SL: {info.StopLoss:F5}   TP: {info.TakeProfit:F5}   Lots: {info.LotSize:F2}",
                Font      = new Font("Consolas", 8.5F),
                ForeColor = Color.FromArgb(175, 175, 195),
                Location  = new Point(14, 54),
                AutoSize  = true
            });

            // ── Row 4: timestamps ─────────────────────────────────────
            string genPart  = info.CreatedAt > DateTime.MinValue
                ? $"Gen: {info.CreatedAt:dd MMM HH:mm:ss}"
                : $"File: {info.Time:dd MMM HH:mm:ss}";
            string donePart = info.Status is SignalCardStatus.Executed
                                           or SignalCardStatus.Rejected
                                           or SignalCardStatus.Error
                ? $"   →   Done: {info.Time:HH:mm:ss}"
                : "";
            card.Controls.Add(new Label
            {
                Text      = genPart + donePart,
                Font      = new Font("Segoe UI", 8F),
                ForeColor = Color.FromArgb(125, 130, 155),
                Location  = new Point(14, 108),
                AutoSize  = true,
                Tag       = "timestamps"
            });

            // ── Row 5: filename + ID ──────────────────────────────────
            card.Controls.Add(new Label
            {
                Text      = $"{info.FileName}   ID: {info.SignalId}",
                Font      = new Font("Segoe UI", 7.5F),
                ForeColor = Color.FromArgb(75, 78, 100),
                Location  = new Point(14, 126),
                AutoSize  = true
            });

            card.Controls.Add(new Panel
            {
                Location  = new Point(14, 74),
                Size      = new Size(Math.Max(120, w - 28), 4),
                BackColor = Color.FromArgb(80, 80, 100),
                Tag       = "performance"
            });

            card.Controls.Add(new Label
            {
                Text      = "P/L: --",
                Font      = new Font("Segoe UI Semibold", 12.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(150, 155, 175),
                Location  = new Point(14, 78),
                Size      = new Size(Math.Max(120, w - 28), 26),
                TextAlign = ContentAlignment.MiddleLeft,
                Tag       = "live-pnl"
            });

            var chkAutoClose = new CheckBox
            {
                Text      = "Auto close",
                Font      = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(32, 36, 54),
                FlatStyle = FlatStyle.Flat,
                Location  = new Point(14, 150),
                Size      = new Size(102, 26),
                Tag       = "auto-close"
            };
            chkAutoClose.CheckedChanged += (_, _) => UpdateAutoCloseTargetFromCard(card, requestImmediateCheck: true);
            card.Controls.Add(chkAutoClose);

            card.Controls.Add(new Label
            {
                Text      = "Pips",
                Font      = new Font("Segoe UI", 8F),
                ForeColor = Color.FromArgb(210, 215, 230),
                Location  = new Point(124, 156),
                AutoSize  = true
            });

            var nudPips = new NumericUpDown
            {
                Font      = new Font("Consolas", 8.5F),
                ForeColor = Color.FromArgb(230, 235, 245),
                BackColor = Color.FromArgb(18, 20, 32),
                BorderStyle = BorderStyle.FixedSingle,
                DecimalPlaces = 1,
                Increment = 0.5M,
                Minimum = 0,
                Maximum = 10000,
                Value = 0,
                Location  = new Point(158, 152),
                Size      = new Size(70, 22),
                Tag       = "target-pips"
            };
            nudPips.ValueChanged += (_, _) =>
            {
                SyncMoneyFromPips(card);
                UpdateAutoCloseTargetFromCard(card, requestImmediateCheck: true);
            };
            card.Controls.Add(nudPips);

            card.Controls.Add(new Label
            {
                Text      = "Money",
                Font      = new Font("Segoe UI", 8F),
                ForeColor = Color.FromArgb(210, 215, 230),
                Location  = new Point(238, 156),
                AutoSize  = true
            });

            var nudMoney = new NumericUpDown
            {
                Font      = new Font("Consolas", 8.5F),
                ForeColor = Color.FromArgb(230, 235, 245),
                BackColor = Color.FromArgb(18, 20, 32),
                BorderStyle = BorderStyle.FixedSingle,
                DecimalPlaces = 2,
                Increment = 0.10M,
                Minimum = 0,
                Maximum = 100000,
                Value = 0,
                Location  = new Point(286, 152),
                Size      = new Size(82, 22),
                Tag       = "target-money"
            };
            nudMoney.ValueChanged += (_, _) =>
            {
                SyncPipsFromMoney(card);
                UpdateAutoCloseTargetFromCard(card, requestImmediateCheck: true);
            };
            card.Controls.Add(nudMoney);

            SyncMoneyFromPips(card);
            UpdateAutoCloseControlsState(card, info);

            return card;
        }

        private Button MakeCardButton(string text, Color bg, Color fg, string tooltip = "")
        {
            var b = new Button
            {
                Text      = text,
                Font      = new Font("Segoe UI", 8F),
                Size      = new Size(24, 22),
                FlatStyle = FlatStyle.Flat,
                BackColor = bg,
                ForeColor = fg,
                Cursor    = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            if (!string.IsNullOrEmpty(tooltip))
                _cardTooltip.SetToolTip(b, tooltip);
            return b;
        }

        private void UpdateCardStatus(Panel card, SignalCardInfo info)
        {
            var (bgColor, stripeColor) = GetNeutralStatusColors(info.Status);
            card.BackColor = bgColor;

            // Update left stripe
            var stripe = card.Controls.OfType<Panel>().FirstOrDefault();
            if (stripe != null) stripe.BackColor = stripeColor;

            // Status label
            var (statusText, statusColor) = GetNeutralStatusDisplay(info);
            var lblStatus = card.Controls.OfType<Label>().FirstOrDefault(l => l.Tag?.ToString() == "status");
            if (lblStatus != null) { lblStatus.Text = statusText; lblStatus.ForeColor = statusColor; }

            // Timestamps
            var lblTs = card.Controls.OfType<Label>().FirstOrDefault(l => l.Tag?.ToString() == "timestamps");
            if (lblTs != null)
            {
                string genPart  = info.CreatedAt > DateTime.MinValue
                    ? $"Gen: {info.CreatedAt:dd MMM HH:mm:ss}"
                    : $"File: {info.Time:dd MMM HH:mm:ss}";
                string donePart = info.Status is SignalCardStatus.Executed
                                               or SignalCardStatus.Rejected
                                               or SignalCardStatus.Error
                    ? $"   →   Done: {info.Time:HH:mm:ss}"
                    : "";
                lblTs.Text = genPart + donePart;
            }

            // Button visibility + enabled state
            foreach (var btn in card.Controls.OfType<Button>())
            {
                switch (btn.Tag?.ToString())
                {
                    case "delete":
                        btn.Enabled = info.Status != SignalCardStatus.Executing;
                        break;
                    case "close":
                        btn.Visible = info.Status == SignalCardStatus.Executed && info.Ticket > 0;
                        break;
                    case "execute":
                        btn.Visible = CanExecuteSignal(info);
                        break;
                }
            }

            card.Tag = info;
            SyncMoneyFromPips(card);
            UpdateAutoCloseControlsState(card, info);
            card.Invalidate();
        }

        private void DeleteSignalCard(Panel card)
        {
            if (card.Tag is not SignalCardInfo info) return;
            if (info.Ticket > 0)
            {
                _autoCloseTargets.Remove(info.Ticket);
                _autoCloseInProgress.Remove(info.Ticket);
            }
            string root = _cfg.Bot.WatchFolder;
            foreach (var p in new[]
            {
                info.FilePath,
                Path.Combine(root,             info.FileName),
                Path.Combine(root, "executed", info.FileName),
                Path.Combine(root, "rejected", info.FileName),
                Path.Combine(root, "error",    info.FileName)
            }.Where(p => !string.IsNullOrEmpty(p) && File.Exists(p)))
            {
                try { File.Delete(p); } catch { }
                break;
            }
            _flpSignals.Controls.Remove(card);
            card.Dispose();
        }

        private bool CanExecuteSignal(SignalCardInfo info)
        {
            if (info.Status != SignalCardStatus.Pending) return false;
            string path = ResolveSignalFilePath(info);
            return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
        }

        private void UpdateSignalCardsWithPositions(IReadOnlyCollection<LivePosition> positions)
        {
            foreach (var card in _flpSignals.Controls.OfType<Panel>())
            {
                if (card.Tag is not SignalCardInfo info)
                    continue;

                if (info.Ticket <= 0 && info.Status == SignalCardStatus.Executed)
                {
                    var matched = FindLikelyPositionForSignal(info, positions);
                    if (matched != null)
                    {
                        info = info with { Ticket = matched.Ticket };
                        card.Tag = info;
                    }
                }

                if (info.Ticket <= 0)
                {
                    UpdateLivePnlDisplay(card, null);
                    UpdateAutoCloseControlsState(card, info);
                continue;
            }

            var position = positions.FirstOrDefault(p => p.Ticket == info.Ticket);
            UpdateLivePnlDisplay(card, position);
            SyncMoneyFromPips(card, position);
            UpdateAutoCloseControlsState(card, info, position);
            }
        }

        private static LivePosition? FindLikelyPositionForSignal(SignalCardInfo info, IReadOnlyCollection<LivePosition> positions)
        {
            string pair = info.Pair.ToUpperInvariant();
            bool isBuy = info.TradeType.Equals("BUY", StringComparison.OrdinalIgnoreCase);
            return positions
                .Where(p => p.Symbol.ToUpperInvariant().StartsWith(pair, StringComparison.OrdinalIgnoreCase))
                .Where(p => isBuy ? p.Type == TradeType.BUY : p.Type == TradeType.SELL)
                .OrderByDescending(p => p.OpenTime)
                .FirstOrDefault();
        }

        private void UpdateLivePnlDisplay(Panel card, LivePosition? position)
        {
            var pnl = card.Controls.OfType<Label>().FirstOrDefault(c => c.Tag?.ToString() == "live-pnl");
            var perf = card.Controls.OfType<Panel>().FirstOrDefault(c => c.Tag?.ToString() == "performance");
            if (pnl == null) return;

            if (position == null)
            {
                pnl.Text = "P/L: --";
                pnl.ForeColor = Color.FromArgb(150, 155, 175);
                if (perf != null) perf.BackColor = Color.FromArgb(80, 80, 100);
                return;
            }

            bool good = position.Profit >= 0;
            pnl.Text = $"P/L {(good ? "+" : "")}${position.Profit:F2} | {position.ProfitPips:F1} pips";
            pnl.ForeColor = good ? C_GREEN : C_RED;
            if (perf != null)
                perf.BackColor = good ? C_GREEN : Color.FromArgb(230, 88, 88);
        }

        private void UpdateAutoCloseControlsState(Panel card, SignalCardInfo info, LivePosition? position = null)
        {
            bool activeTrade = info.Ticket > 0 && position != null;
            var chk = card.Controls.OfType<CheckBox>().FirstOrDefault(c => c.Tag?.ToString() == "auto-close");
            var nudControls = card.Controls.OfType<NumericUpDown>()
                .Where(c => c.Tag?.ToString() is "target-pips" or "target-money")
                .ToList();

            if (chk != null)
            {
                chk.Enabled = info.Ticket > 0;
                chk.ForeColor = info.Ticket > 0
                    ? Color.White
                    : Color.FromArgb(150, 155, 175);
                chk.BackColor = info.Ticket > 0
                    ? Color.FromArgb(44, 50, 74)
                    : Color.FromArgb(30, 32, 46);
            }

            foreach (var nud in nudControls)
            {
                nud.Enabled = info.Ticket > 0;
                nud.ForeColor = activeTrade
                    ? Color.FromArgb(230, 235, 245)
                    : Color.FromArgb(130, 135, 155);
                nud.BackColor = info.Ticket > 0
                    ? Color.FromArgb(18, 20, 32)
                    : Color.FromArgb(28, 30, 42);
            }
        }

        private void UpdateAutoCloseTargetFromCard(Panel card, bool requestImmediateCheck = false)
        {
            if (card.Tag is not SignalCardInfo info || info.Ticket <= 0) return;
            var chk = card.Controls.OfType<CheckBox>().FirstOrDefault(c => c.Tag?.ToString() == "auto-close");
            var pips = card.Controls.OfType<NumericUpDown>().FirstOrDefault(c => c.Tag?.ToString() == "target-pips");
            var money = card.Controls.OfType<NumericUpDown>().FirstOrDefault(c => c.Tag?.ToString() == "target-money");
            if (chk == null || pips == null || money == null) return;

            _autoCloseTargets[info.Ticket] = new AutoCloseTarget
            {
                Enabled = chk.Checked,
                TargetPips = Math.Max(0, (double)pips.Value),
                TargetMoney = Math.Max(0, (double)money.Value)
            };

            if (requestImmediateCheck && chk.Checked)
                _ = RefreshPositionsAsync();
        }

        private void SyncMoneyFromPips(Panel card, LivePosition? position = null)
        {
            if (_syncingAutoCloseValues) return;
            if (card.Tag is not SignalCardInfo info) return;
            var pips = card.Controls.OfType<NumericUpDown>().FirstOrDefault(c => c.Tag?.ToString() == "target-pips");
            var money = card.Controls.OfType<NumericUpDown>().FirstOrDefault(c => c.Tag?.ToString() == "target-money");
            if (pips == null || money == null) return;

            double lots = position?.Lots ?? info.LotSize;
            string symbol = position?.Symbol ?? info.Pair;
            double price = position?.CurrentPrice > 0 ? position.CurrentPrice : 1.0;
            double targetMoney = Math.Max(0, (double)pips.Value) * lots * LotCalculator.GetPipValuePerLot(symbol.ToUpperInvariant(), price);
            decimal value = Math.Min(money.Maximum, Math.Max(money.Minimum, (decimal)Math.Round(targetMoney, 2)));

            _syncingAutoCloseValues = true;
            try { money.Value = value; }
            finally { _syncingAutoCloseValues = false; }
        }

        private void SyncPipsFromMoney(Panel card, LivePosition? position = null)
        {
            if (_syncingAutoCloseValues) return;
            if (card.Tag is not SignalCardInfo info) return;
            var pips = card.Controls.OfType<NumericUpDown>().FirstOrDefault(c => c.Tag?.ToString() == "target-pips");
            var money = card.Controls.OfType<NumericUpDown>().FirstOrDefault(c => c.Tag?.ToString() == "target-money");
            if (pips == null || money == null) return;

            double lots = position?.Lots ?? info.LotSize;
            string symbol = position?.Symbol ?? info.Pair;
            double price = position?.CurrentPrice > 0 ? position.CurrentPrice : 1.0;
            double pipValue = lots * LotCalculator.GetPipValuePerLot(symbol.ToUpperInvariant(), price);
            double targetPips = pipValue > 0 ? (double)money.Value / pipValue : 0;
            decimal value = Math.Min(pips.Maximum, Math.Max(pips.Minimum, (decimal)Math.Round(targetPips, 1)));

            _syncingAutoCloseValues = true;
            try { pips.Value = value; }
            finally { _syncingAutoCloseValues = false; }
        }

        private async Task ProcessAutoCloseTargetsAsync(IReadOnlyCollection<LivePosition> positions)
        {
            if (_bridge?.IsConnected != true) return;

            foreach (var position in positions)
            {
                if (!_autoCloseTargets.TryGetValue(position.Ticket, out var target) || !target.Enabled)
                    continue;
                if (_autoCloseInProgress.Contains(position.Ticket))
                    continue;

                bool targetReached =
                    target.TargetMoney <= 0 && target.TargetPips <= 0
                        ? position.Profit > 0
                        : target.TargetMoney > 0
                            ? position.Profit >= target.TargetMoney
                            : position.ProfitPips >= target.TargetPips;

                if (!targetReached) continue;

                _autoCloseInProgress.Add(position.Ticket);
                try
                {
                    Log($"[BOT] Auto close target reached on #{position.Ticket}: ${position.Profit:F2}, {position.ProfitPips:F1} pips.", C_GREEN);
                    bool ok = await _bridge.CloseTradeAsync(position.Ticket).ConfigureAwait(false);
                    Log(ok
                            ? $"[OK] Auto closed #{position.Ticket} at profit ${position.Profit:F2}."
                            : $"[ERROR] Auto close failed for #{position.Ticket}.",
                        ok ? C_GREEN : C_RED);
                    if (ok)
                        _autoCloseTargets.Remove(position.Ticket);
                }
                finally
                {
                    _autoCloseInProgress.Remove(position.Ticket);
                }
            }
        }

        private void ApplyAutoCloseDecisionToCard(Panel card, long ticket, TradeReviewDecision review)
        {
            if (card.Tag is not SignalCardInfo info || ticket <= 0) return;
            var updated = info with { Ticket = ticket };
            card.Tag = updated;

            var chk = card.Controls.OfType<CheckBox>().FirstOrDefault(c => c.Tag?.ToString() == "auto-close");
            var pips = card.Controls.OfType<NumericUpDown>().FirstOrDefault(c => c.Tag?.ToString() == "target-pips");
            var money = card.Controls.OfType<NumericUpDown>().FirstOrDefault(c => c.Tag?.ToString() == "target-money");

            _syncingAutoCloseValues = true;
            try
            {
                if (pips != null)
                    pips.Value = Math.Min(pips.Maximum, Math.Max(pips.Minimum, (decimal)review.TargetPips));
                if (money != null)
                    money.Value = Math.Min(money.Maximum, Math.Max(money.Minimum, (decimal)review.TargetMoney));
                if (chk != null)
                    chk.Checked = review.AutoCloseEnabled;
            }
            finally
            {
                _syncingAutoCloseValues = false;
            }

            _autoCloseTargets[ticket] = new AutoCloseTarget
            {
                Enabled = review.AutoCloseEnabled,
                TargetPips = review.TargetPips,
                TargetMoney = review.TargetMoney
            };
            UpdateAutoCloseControlsState(card, updated);
        }

        private async Task<TradeReviewDecision> ShowTradeReviewDialogAsync(TradeRequest request, SignalCardInfo info)
        {
            if (_bridge == null)
                return new TradeReviewDecision(false, false, 0, 0);

            AccountInfo? account = null;
            SymbolInfo? symbol = null;
            List<LivePosition> positions = [];
            JObject? liveSnapshot = null;

            try
            {
                liveSnapshot = await _bridge.GetMarketSnapshotAsync(request, _cfg.Bot).ConfigureAwait(false);
                account = await _bridge.GetAccountInfoAsync().ConfigureAwait(false);
                symbol = await _bridge.GetSymbolInfoAsync(request.Pair).ConfigureAwait(false);
                positions = await _bridge.GetPositionsAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log($"[BOT] Could not collect live MT5 review data: {ex.Message}", C_YELLOW);
            }

            string snapshot = liveSnapshot?.ToString(Formatting.Indented)
                ?? BuildTradeReviewSnapshotJson(request, account, symbol, positions);

            if (InvokeRequired)
                return (TradeReviewDecision)Invoke(() => ShowTradeReviewDialog(request, info, snapshot, symbol))!;

            return ShowTradeReviewDialog(request, info, snapshot, symbol);
        }

        private TradeReviewDecision ShowTradeReviewDialog(
            TradeRequest request,
            SignalCardInfo info,
            string snapshotJson,
            SymbolInfo? symbol)
        {
            using var form = new Form
            {
                Text = $"Review Trade - {request.TradeType} {request.Pair}",
                Size = new Size(900, 720),
                MinimumSize = new Size(760, 560),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Color.FromArgb(13, 13, 19),
                ForeColor = Color.FromArgb(218, 218, 230),
                Font = new Font("Segoe UI", 9F)
            };
            AppIcon.ApplyTo(form);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(14),
                BackColor = form.BackColor
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            form.Controls.Add(root);

            var title = new Label
            {
                Text = $"{request.TradeType} {request.Pair} | Lots {request.LotSize:F2} | SL {request.StopLoss:F5} | TP {request.TakeProfit:F5}",
                Dock = DockStyle.Fill,
                ForeColor = C_TEXT,
                Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            root.Controls.Add(title, 0, 0);

            JObject currentSnapshot = ParseReviewSnapshot(snapshotJson);
            string latestSnapshotJson = currentSnapshot.ToString(Formatting.Indented);
            form.Tag = latestSnapshotJson;

            var bindings = new List<(string Path, Label Value, string Format)>();
            var dashboard = BuildReviewDashboard(bindings, out var liveStatus);
            root.Controls.Add(dashboard, 0, 1);
            RefreshReviewDashboard(currentSnapshot, bindings, liveStatus);

            bool refreshingSnapshot = false;
            var liveTimer = new System.Windows.Forms.Timer { Interval = 2500 };
            liveTimer.Tick += async (_, _) =>
            {
                if (refreshingSnapshot || _bridge == null || form.IsDisposed) return;
                refreshingSnapshot = true;
                try
                {
                    var updated = await _bridge.GetMarketSnapshotAsync(request, _cfg.Bot);
                    if (updated != null && !form.IsDisposed)
                    {
                        currentSnapshot = updated;
                        latestSnapshotJson = currentSnapshot.ToString(Formatting.Indented);
                        form.Tag = latestSnapshotJson;
                        RefreshReviewDashboard(currentSnapshot, bindings, liveStatus);
                    }
                }
                catch (Exception ex)
                {
                    if (!form.IsDisposed)
                        liveStatus.Text = $"Live refresh failed: {ex.Message}";
                }
                finally
                {
                    refreshingSnapshot = false;
                }
            };
            form.FormClosed += (_, _) => liveTimer.Stop();
            liveTimer.Start();

            var autoPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 10, 0, 0),
                BackColor = form.BackColor
            };
            root.Controls.Add(autoPanel, 0, 2);

            var chkAutoClose = new CheckBox
            {
                Text = "Auto close after trade opens",
                AutoSize = false,
                Size = new Size(190, 28),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(44, 50, 74),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold)
            };
            var nudPips = MakeReviewNumber(0, 10000, 0.5M, 1, 82);
            var nudMoney = MakeReviewNumber(0, 100000, 0.10M, 2, 92);
            autoPanel.Controls.Add(chkAutoClose);
            autoPanel.Controls.Add(MakeInlineLabel("Pips target"));
            autoPanel.Controls.Add(nudPips);
            autoPanel.Controls.Add(MakeInlineLabel("Money target"));
            autoPanel.Controls.Add(nudMoney);
            autoPanel.Controls.Add(MakeInlineLabel("0 means close on any profit"));

            bool syncing = false;
            double lots = request.LotSize;
            double price = symbol?.Ask > 0 ? symbol.Ask : 1.0;
            string sym = symbol?.Symbol ?? request.Pair;
            double pipValue = Math.Max(0.0001, lots * LotCalculator.GetPipValuePerLot(sym.ToUpperInvariant(), price));

            nudPips.ValueChanged += (_, _) =>
            {
                if (syncing) return;
                syncing = true;
                nudMoney.Value = Math.Min(nudMoney.Maximum, Math.Round(nudPips.Value * (decimal)pipValue, 2));
                syncing = false;
            };
            nudMoney.ValueChanged += (_, _) =>
            {
                if (syncing) return;
                syncing = true;
                nudPips.Value = Math.Min(nudPips.Maximum, Math.Round(nudMoney.Value / (decimal)pipValue, 1));
                syncing = false;
            };

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = form.BackColor
            };
            root.Controls.Add(buttons, 0, 3);

            var btnPlay = MakeDialogButton("Play / Start Trade", C_GREEN);
            var btnCancel = MakeDialogButton("Cancel", Color.FromArgb(110, 110, 130));
            buttons.Controls.Add(btnPlay);
            buttons.Controls.Add(btnCancel);

            TradeReviewDecision decision = new(false, false, 0, 0);
            btnPlay.Click += (_, _) =>
            {
                form.Tag = latestSnapshotJson;
                decision = new TradeReviewDecision(
                    true,
                    chkAutoClose.Checked,
                    (double)nudPips.Value,
                    (double)nudMoney.Value);
                form.DialogResult = DialogResult.OK;
                form.Close();
            };
            btnCancel.Click += (_, _) =>
            {
                decision = new TradeReviewDecision(false, false, 0, 0);
                form.DialogResult = DialogResult.Cancel;
                form.Close();
            };

            form.ShowDialog(this);
            return decision;
        }

        private Control BuildReviewDashboard(
            List<(string Path, Label Value, string Format)> bindings,
            out Label liveStatus)
        {
            var host = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.FromArgb(13, 13, 19)
            };
            host.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            host.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            liveStatus = new Label
            {
                Text = "Live MT5 snapshot loaded. Refreshing...",
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(150, 220, 255),
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            host.Controls.Add(liveStatus, 0, 0);

            var scroll = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(13, 13, 19)
            };
            host.Controls.Add(scroll, 0, 1);

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0, 0, 12, 12),
                BackColor = scroll.BackColor
            };
            scroll.Controls.Add(flow);

            AddReviewGroup(flow, bindings, "Account", [
                ("Balance", "account.balance", "money"),
                ("Equity", "account.equity", "money"),
                ("Free Margin", "account.free_margin", "money"),
                ("Margin Used", "account.margin_used", "money"),
                ("Margin Level", "account.margin_level", "pct"),
                ("Floating P/L", "account.floating_pnl", "money"),
                ("Daily P/L", "account.daily_pnl", "money"),
                ("Trades Today", "account.daily_trades_taken", "plain")
            ]);

            AddReviewGroup(flow, bindings, "Price", [
                ("Bid", "price.bid", "price"),
                ("Ask", "price.ask", "price"),
                ("Spread", "price.spread_pips", "pips"),
                ("Daily Open", "price.daily_open", "price"),
                ("Daily High", "price.daily_high", "price"),
                ("Daily Low", "price.daily_low", "price"),
                ("Daily Range", "price.daily_range_pips", "pips"),
                ("Prev High", "price.prev_day_high", "price")
            ]);

            AddReviewGroup(flow, bindings, "Trade Risk", [
                ("Risk $", "risk.dollar_risk", "money"),
                ("TP1 Profit", "risk.dollar_profit_tp1", "money"),
                ("TP2 Profit", "risk.dollar_profit_tp2", "money"),
                ("SL Distance", "risk.sl_distance_pips", "pips"),
                ("TP1 Distance", "risk.tp1_distance_pips", "pips"),
                ("R:R", "risk.rr_ratio", "ratio"),
                ("Max Risk", "risk.max_risk_pct", "pct"),
                ("Daily Loss Left", "risk.daily_loss_remaining", "money")
            ]);

            AddReviewGroup(flow, bindings, "Symbol", [
                ("Name", "symbol.name", "plain"),
                ("Digits", "symbol.digits", "plain"),
                ("Min Lot", "symbol.min_lot", "lots"),
                ("Max Lot", "symbol.max_lot", "lots"),
                ("Lot Step", "symbol.lot_step", "lots"),
                ("Trade Allowed", "symbol.trade_allowed", "bool"),
                ("Execution", "symbol.execution_mode", "plain"),
                ("Filling", "symbol.filling_mode", "plain")
            ]);

            AddReviewGroup(flow, bindings, "Session", [
                ("Broker Time", "session.broker_time", "plain"),
                ("Connected", "session.terminal_connected", "bool"),
                ("Market Open", "session.market_open", "bool"),
                ("London", "session.london_open", "bool"),
                ("New York", "session.newyork_open", "bool"),
                ("Overlap", "session.overlap_active", "bool"),
                ("Session", "session.session_name", "plain"),
                ("Weekend", "session.is_weekend", "bool")
            ]);

            AddReviewGroup(flow, bindings, "H1 Indicators", [
                ("RSI", "indicators.h1.rsi", "one"),
                ("RSI Signal", "indicators.h1.rsi_signal", "plain"),
                ("MACD Bias", "indicators.h1.macd_bias", "plain"),
                ("EMA 20", "indicators.h1.ema20", "price"),
                ("EMA 50", "indicators.h1.ema50", "price"),
                ("EMA 200", "indicators.h1.ema200", "price"),
                ("ADX", "indicators.h1.adx", "one"),
                ("ATR", "indicators.h1.atr", "price")
            ]);

            AddReviewGroup(flow, bindings, "Candles", [
                ("H4", "candles.h4_last.direction", "plain"),
                ("H1", "candles.h1_last.direction", "plain"),
                ("M15", "candles.m15_last.direction", "plain"),
                ("M5", "candles.m5_last.direction", "plain"),
                ("H1 Body", "candles.h1_last.body_pips", "pips"),
                ("M15 Body", "candles.m15_last.body_pips", "pips"),
                ("M5 Doji", "candles.m5_last.is_doji", "bool"),
                ("M15 Inside", "candles.m15_last.is_inside_bar", "bool")
            ]);

            AddReviewGroup(flow, bindings, "Structure", [
                ("H4 Trend", "structure.trend_h4", "plain"),
                ("H1 Trend", "structure.trend_h1", "plain"),
                ("M15 Trend", "structure.trend_m15", "plain"),
                ("M5 Trend", "structure.trend_m5", "plain"),
                ("Aligned", "structure.all_timeframes_aligned", "bool"),
                ("Regime", "structure.market_regime", "plain"),
                ("Swing High", "structure.swing_high", "price"),
                ("Swing Low", "structure.swing_low", "price")
            ]);

            AddReviewGroup(flow, bindings, "Levels", [
                ("Support 1", "levels.nearest_support_1", "price"),
                ("Support 2", "levels.nearest_support_2", "price"),
                ("Resistance 1", "levels.nearest_resistance_1", "price"),
                ("Resistance 2", "levels.nearest_resistance_2", "price"),
                ("To Support", "levels.distance_to_support_pips", "pips"),
                ("To Resistance", "levels.distance_to_resistance_pips", "pips"),
                ("Key Level", "levels.price_at_key_level", "bool"),
                ("Key Type", "levels.key_level_type", "plain")
            ]);

            AddReviewGroup(flow, bindings, "Positions", [
                ("Open Total", "positions.total_open", "plain"),
                ("Same Pair", "positions.same_pair_open", "bool"),
                ("Same Direction", "positions.same_pair_direction", "plain"),
                ("Duplicate", "positions.duplicate_trade_exists", "bool"),
                ("Opposite", "positions.opposite_trade_exists", "bool"),
                ("Last Result", "last_order.execution_result", "plain"),
                ("Ticket", "last_order.ticket", "plain"),
                ("Today Win Rate", "history.win_rate_today_pct", "pct")
            ]);

            AddReviewGroup(flow, bindings, "News", [
                ("Risk", "news.news_risk_level", "plain"),
                ("High Impact", "news.high_impact_next_60_min", "bool"),
                ("Source", "news.source", "plain")
            ]);

            return host;
        }

        private void AddReviewGroup(
            FlowLayoutPanel parent,
            List<(string Path, Label Value, string Format)> bindings,
            string title,
            IReadOnlyList<(string Label, string Path, string Format)> metrics)
        {
            var group = new GroupBox
            {
                Text = title,
                Width = 276,
                Height = 36 + metrics.Count * 26,
                ForeColor = Color.FromArgb(215, 220, 235),
                BackColor = Color.FromArgb(18, 20, 32),
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
                Padding = new Padding(10),
                Margin = new Padding(0, 0, 12, 12)
            };

            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = metrics.Count,
                BackColor = group.BackColor,
                Padding = new Padding(4, 10, 4, 4)
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            group.Controls.Add(grid);

            for (int i = 0; i < metrics.Count; i++)
            {
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));

                var name = new Label
                {
                    Text = metrics[i].Label,
                    Dock = DockStyle.Fill,
                    ForeColor = Color.FromArgb(150, 156, 175),
                    Font = new Font("Segoe UI", 8.5F),
                    TextAlign = ContentAlignment.MiddleLeft,
                    AutoEllipsis = true
                };
                var value = new Label
                {
                    Text = "--",
                    Dock = DockStyle.Fill,
                    ForeColor = Color.FromArgb(235, 238, 246),
                    Font = new Font("Segoe UI Semibold", 8.8F, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleRight,
                    AutoEllipsis = true
                };
                grid.Controls.Add(name, 0, i);
                grid.Controls.Add(value, 1, i);
                bindings.Add((metrics[i].Path, value, metrics[i].Format));
            }

            parent.Controls.Add(group);
        }

        private static JObject ParseReviewSnapshot(string snapshotJson)
        {
            try
            {
                return JObject.Parse(snapshotJson);
            }
            catch
            {
                return new JObject
                {
                    ["status"] = "Snapshot JSON could not be parsed"
                };
            }
        }

        private void RefreshReviewDashboard(
            JObject snapshot,
            IReadOnlyList<(string Path, Label Value, string Format)> bindings,
            Label liveStatus)
        {
            foreach (var binding in bindings)
            {
                var token = snapshot.SelectToken(binding.Path);
                binding.Value.Text = FormatReviewValue(token, binding.Format);
                binding.Value.ForeColor = ReviewValueColor(binding.Path, token);
            }

            string time = FormatReviewValue(snapshot.SelectToken("collected_at_pkt"), "plain");
            string symbol = FormatReviewValue(snapshot.SelectToken("symbol.name"), "plain");
            liveStatus.Text = $"Live MT5 values | {symbol} | updated {time}";
        }

        private static string FormatReviewValue(JToken? token, string format)
        {
            if (token == null || token.Type == JTokenType.Null)
                return "--";

            if (format == "bool")
                return token.Type == JTokenType.Boolean && token.Value<bool>() ? "Yes" : "No";

            if (format == "plain")
                return token.ToString(Formatting.None).Trim('"');

            if (!double.TryParse(token.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double value))
                return token.ToString(Formatting.None).Trim('"');

            return format switch
            {
                "money" => value.ToString("0.00", CultureInfo.InvariantCulture),
                "price" => value.ToString("0.00000", CultureInfo.InvariantCulture),
                "pips" => value.ToString("0.0", CultureInfo.InvariantCulture),
                "pct" => value.ToString("0.0", CultureInfo.InvariantCulture),
                "ratio" => value.ToString("0.00", CultureInfo.InvariantCulture),
                "lots" => value.ToString("0.00", CultureInfo.InvariantCulture),
                "one" => value.ToString("0.0", CultureInfo.InvariantCulture),
                _ => value.ToString("0.#####", CultureInfo.InvariantCulture)
            };
        }

        private static Color ReviewValueColor(string path, JToken? token)
        {
            if (token != null &&
                double.TryParse(token.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double value) &&
                (path.Contains("pnl", StringComparison.OrdinalIgnoreCase) ||
                 path.Contains("profit", StringComparison.OrdinalIgnoreCase)))
            {
                return value >= 0
                    ? Color.FromArgb(79, 209, 139)
                    : Color.FromArgb(255, 126, 126);
            }

            if (path.Contains("spread", StringComparison.OrdinalIgnoreCase))
                return Color.FromArgb(250, 199, 117);

            return Color.FromArgb(235, 238, 246);
        }

        private NumericUpDown MakeReviewNumber(decimal min, decimal max, decimal step, int decimals, int width) =>
            new()
            {
                Minimum = min,
                Maximum = max,
                Increment = step,
                DecimalPlaces = decimals,
                Width = width,
                Height = 28,
                BackColor = Color.FromArgb(18, 20, 32),
                ForeColor = Color.FromArgb(230, 235, 245),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9F),
                Margin = new Padding(6, 4, 12, 0)
            };

        private Label MakeInlineLabel(string text) =>
            new()
            {
                Text = text,
                AutoSize = true,
                ForeColor = Color.FromArgb(190, 195, 210),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(10, 8, 0, 0)
            };

        private Button MakeDialogButton(string text, Color color)
        {
            var button = new Button
            {
                Text = text,
                Width = 150,
                Height = 34,
                BackColor = color,
                ForeColor = Color.FromArgb(10, 10, 20),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(8, 8, 0, 0)
            };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        private string BuildTradeReviewSnapshotJson(
            TradeRequest request,
            AccountInfo? account,
            SymbolInfo? symbol,
            IReadOnlyCollection<LivePosition> positions)
        {
            DateTime utc = DateTime.UtcNow;
            DateTime local = DateTime.Now;
            double entry = request.EntryPrice > 0
                ? request.EntryPrice
                : symbol != null
                    ? request.TradeType == TradeType.BUY ? symbol.Ask : symbol.Bid
                    : 0;
            double pipSize = LotCalculator.GetPipSize((symbol?.Symbol ?? request.Pair).ToUpperInvariant());
            double slPips = entry > 0 ? Math.Abs(entry - request.StopLoss) / pipSize : 0;
            double tpPips = entry > 0 ? Math.Abs(request.TakeProfit - entry) / pipSize : 0;
            double rr = slPips > 0 ? tpPips / slPips : 0;
            double dollarRisk = entry > 0 ? LotCalculator.DollarRisk(request.LotSize, entry, request.StopLoss, request.Pair) : 0;
            double dollarProfit = entry > 0 ? LotCalculator.DollarProfit(request.LotSize, entry, request.TakeProfit, request.Pair) : 0;
            var samePair = positions
                .Where(p => p.Symbol.StartsWith(request.Pair, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var snapshot = new JObject
            {
                ["collected_at_utc"] = utc.ToString("yyyy-MM-dd HH:mm:ss"),
                ["collected_at_pkt"] = local.ToString("yyyy-MM-dd HH:mm:ss"),
                ["account"] = account == null ? Unavailable("GET_ACCOUNT failed") : new JObject
                {
                    ["balance"] = account.Balance,
                    ["equity"] = account.Equity,
                    ["free_margin"] = account.FreeMargin,
                    ["margin_used"] = account.Margin,
                    ["margin_level"] = account.MarginLevel,
                    ["currency"] = account.Currency,
                    ["leverage"] = account.Leverage,
                    ["floating_pnl"] = account.Profit,
                    ["daily_pnl"] = null,
                    ["daily_trades_taken"] = null,
                    ["consecutive_losses"] = null,
                    ["win_rate_today_pct"] = null,
                    ["daily_loss_limit_reached"] = false
                },
                ["session"] = new JObject
                {
                    ["broker_time"] = utc.ToString("yyyy-MM-dd HH:mm:ss"),
                    ["terminal_time"] = local.ToString("yyyy-MM-dd HH:mm:ss"),
                    ["current_hour_utc"] = utc.Hour,
                    ["terminal_connected"] = _bridge?.IsConnected == true,
                    ["market_open"] = utc.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday,
                    ["london_open"] = utc.Hour >= 7 && utc.Hour < 16,
                    ["newyork_open"] = utc.Hour >= 12 && utc.Hour < 21,
                    ["overlap_active"] = utc.Hour >= 12 && utc.Hour < 16,
                    ["session_name"] = utc.Hour >= 12 && utc.Hour < 16 ? "London+NY Overlap" : "Live Session",
                    ["is_weekend"] = utc.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
                },
                ["symbol"] = symbol == null ? Unavailable("GET_SYMBOL_INFO failed") : new JObject
                {
                    ["name"] = symbol.Symbol,
                    ["digits"] = symbol.Digits,
                    ["point_size"] = Math.Pow(10, -symbol.Digits),
                    ["pip_size"] = pipSize,
                    ["min_lot"] = symbol.MinLot,
                    ["max_lot"] = symbol.MaxLot,
                    ["spread_pips"] = symbol.SpreadPips,
                    ["trade_allowed"] = true,
                    ["execution_mode"] = "MARKET"
                },
                ["price"] = symbol == null ? Unavailable("GET_SYMBOL_INFO failed") : new JObject
                {
                    ["bid"] = symbol.Bid,
                    ["ask"] = symbol.Ask,
                    ["spread_pips"] = symbol.SpreadPips,
                    ["spread_normal"] = symbol.SpreadPips <= _cfg.Bot.MaxSpreadPips,
                    ["daily_open"] = null,
                    ["daily_high"] = null,
                    ["daily_low"] = null,
                    ["daily_range_pips"] = null
                },
                ["candles"] = Unavailable("Current EA does not expose candle snapshots yet"),
                ["indicators"] = Unavailable("Current EA does not expose RSI/MACD/EMA/ADX yet"),
                ["structure"] = Unavailable("Structure engine not wired to live EA snapshot yet"),
                ["levels"] = Unavailable("Support/resistance snapshot not exposed yet"),
                ["positions"] = new JObject
                {
                    ["total_open"] = positions.Count,
                    ["same_pair_open"] = samePair.Count > 0,
                    ["same_pair_direction"] = samePair.Count == 0 ? "NONE" : samePair[0].Type.ToString(),
                    ["duplicate_trade_exists"] = samePair.Any(p => p.Type == request.TradeType),
                    ["opposite_trade_exists"] = samePair.Any(p => p.Type != request.TradeType),
                    ["pending_orders"] = new JArray(),
                    ["open_list"] = JArray.FromObject(positions.Select(p => new
                    {
                        ticket = p.Ticket,
                        pair = p.Symbol,
                        direction = p.Type.ToString(),
                        lots = p.Lots,
                        open_price = p.OpenPrice,
                        current_price = p.CurrentPrice,
                        pnl = p.Profit,
                        pips = p.ProfitPips
                    }))
                },
                ["last_order"] = new JObject
                {
                    ["ticket"] = 0,
                    ["execution_result"] = "NONE"
                },
                ["history"] = Unavailable("Trade-history summary is not available in live review yet"),
                ["risk"] = new JObject
                {
                    ["max_risk_pct"] = _cfg.Bot.MaxRiskPercent,
                    ["max_risk_dollar"] = account == null ? 0 : Math.Round(account.Equity * _cfg.Bot.MaxRiskPercent / 100.0, 2),
                    ["min_rr_ratio"] = _cfg.Bot.MinRRRatio,
                    ["suggested_sl"] = request.StopLoss,
                    ["suggested_tp1"] = request.TakeProfit,
                    ["suggested_tp2"] = request.TakeProfit2,
                    ["sl_distance_pips"] = Math.Round(slPips, 1),
                    ["tp1_distance_pips"] = Math.Round(tpPips, 1),
                    ["rr_ratio"] = Math.Round(rr, 2),
                    ["calculated_lot"] = request.LotSize,
                    ["dollar_risk"] = dollarRisk,
                    ["dollar_profit_tp1"] = dollarProfit,
                    ["daily_loss_limit_dollar"] = account == null ? 0 : Math.Round(account.Equity * _cfg.Bot.EmergencyCloseDrawdownPct / 100.0, 2)
                },
                ["news"] = Unavailable("News module is offline in this review window")
            };

            return snapshot.ToString(Formatting.Indented);
        }

        private static JObject Unavailable(string reason) => new()
        {
            ["available"] = false,
            ["reason"] = reason
        };

        private async Task ExecuteSignalFromCardSafeAsync(Panel card)
        {
            if (card.Tag is not SignalCardInfo info) return;
            string executionKey = !string.IsNullOrWhiteSpace(info.SignalId)
                ? info.SignalId
                : info.FilePath;

            try
            {
                Log($"[BOT] Execute clicked for {info.FileName} ({info.Pair}).", C_ACCENT);

                lock (_signalExecutionLock)
                {
                    if (!_executingSignalIds.Add(executionKey))
                    {
                        Log($"[BOT] Signal {info.SignalId} is already executing. Duplicate click ignored.", C_YELLOW);
                        return;
                    }
                }

                if (_bridge?.IsConnected != true)
                {
                    Log("[WARN] Not connected to MT5.", C_YELLOW);
                    return;
                }

                if (_bot == null)
                {
                    Log("[BOT] Click 'Start Monitoring' first so the app validates settings and loads the signal row.", C_YELLOW);
                    return;
                }

                string signalPath = ResolveSignalFilePath(info);
                if (string.IsNullOrWhiteSpace(signalPath) || !File.Exists(signalPath))
                {
                    Log($"[ERROR] Cannot find signal file for {info.FileName}. It may have been moved or deleted.", C_RED);
                    UpdateCardStatusSafe(card, info with { Status = SignalCardStatus.Error, StatusText = "Signal file not found", Time = DateTime.Now });
                    return;
                }

                TradeRequest? req;
                try
                {
                    string json = await Task.Run(() => File.ReadAllText(signalPath)).ConfigureAwait(false);
                    req = JsonConvert.DeserializeObject<TradeRequest>(json);
                }
                catch (Exception ex)
                {
                    Log($"[ERROR] Cannot read signal file: {ex.Message}", C_RED);
                    UpdateCardStatusSafe(card, info with { Status = SignalCardStatus.Error, StatusText = "Cannot read file", Time = DateTime.Now });
                    return;
                }

                if (req == null)
                {
                    Log("[ERROR] Signal JSON is empty or invalid.", C_RED);
                    UpdateCardStatusSafe(card, info with { Status = SignalCardStatus.Error, StatusText = "Invalid JSON", Time = DateTime.Now });
                    return;
                }

                var review = await ShowTradeReviewDialogAsync(req, info).ConfigureAwait(false);
                if (!review.Approved)
                {
                    Log($"[BOT] Trade review cancelled for {info.FileName}.", C_YELLOW);
                    return;
                }

                var executing = info with
                {
                    Status = SignalCardStatus.Executing,
                    StatusText = "Executing...",
                    FilePath = signalPath,
                    Time = DateTime.Now
                };
                UpdateCardStatusSafe(card, executing);

                Log($"[BOT] Sending signal {req.Id} to MT5: {req.TradeType} {req.Pair} {req.LotSize:F2} lot(s).", C_ACCENT);
                var result = await _bot.ExecuteTradeWithValidationAsync(req).ConfigureAwait(false);

                string archivedPath = ArchiveExecutedSignalFile(signalPath, result.IsSuccess);
                _bot.SignalFileArchived(signalPath);

                var updated = executing with
                {
                    Status     = result.IsSuccess ? SignalCardStatus.Executed : SignalCardStatus.Rejected,
                    StatusText = result.IsSuccess ? $"#{result.Ticket}" : result.ErrorMessage,
                    Ticket     = result.IsSuccess ? result.Ticket : 0,
                    Time       = DateTime.Now,
                    FilePath   = archivedPath
                };
                UpdateCardStatusSafe(card, updated);
                if (result.IsSuccess)
                    ApplyAutoCloseDecisionToCard(card, result.Ticket, review);

                Log(result.IsSuccess
                    ? $"[OK] Trade placed: {req.Pair} ticket #{result.Ticket}"
                    : $"[ERROR] Execute failed: {result.ErrorMessage}",
                    result.IsSuccess ? C_GREEN : C_RED);

                if (result.IsSuccess)
                    await RefreshPositionsAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log($"[ERROR] Execute button failed: {ex.Message}", C_RED);
                UpdateCardStatusSafe(card, info with { Status = SignalCardStatus.Error, StatusText = ex.Message, Time = DateTime.Now });
            }
            finally
            {
                lock (_signalExecutionLock)
                    _executingSignalIds.Remove(executionKey);
            }
        }

        private string ResolveSignalFilePath(SignalCardInfo info)
        {
            if (!string.IsNullOrWhiteSpace(info.FilePath) && File.Exists(info.FilePath))
                return info.FilePath;

            string root = _cfg.Bot.WatchFolder;
            foreach (var path in new[]
            {
                Path.Combine(root, info.FileName),
                Path.Combine(root, "rejected", info.FileName),
                Path.Combine(root, "error", info.FileName),
                Path.Combine(root, "executed", info.FileName)
            })
            {
                if (File.Exists(path)) return path;
            }

            foreach (var folder in new[] { root, Path.Combine(root, "rejected"), Path.Combine(root, "error"), Path.Combine(root, "executed") })
            {
                if (!Directory.Exists(folder)) continue;
                string pattern = Path.GetFileNameWithoutExtension(info.FileName) + "_*.json";
                var match = Directory.GetFiles(folder, pattern)
                    .OrderByDescending(File.GetLastWriteTime)
                    .FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(match)) return match;
            }

            return "";
        }

        private string ArchiveExecutedSignalFile(string originalPath, bool success)
        {
            string archivedPath = originalPath;
            try
            {
                string archiveDir = success
                    ? Path.Combine(_cfg.Bot.WatchFolder, "executed")
                    : Path.Combine(_cfg.Bot.WatchFolder, "rejected");
                Directory.CreateDirectory(archiveDir);
                if (File.Exists(originalPath))
                {
                    string dest = Path.Combine(archiveDir,
                        $"{Path.GetFileNameWithoutExtension(originalPath)}_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                    File.Move(originalPath, dest, overwrite: true);
                    archivedPath = dest;
                }
            }
            catch (Exception ex)
            {
                Log($"[WARN] Could not archive signal file: {ex.Message}", C_YELLOW);
            }
            return archivedPath;
        }

        private void UpdateCardStatusSafe(Panel card, SignalCardInfo info)
        {
            if (InvokeRequired)
                Invoke(() => { UpdateCardStatus(card, info); ReorderSignalFeed(); });
            else
            {
                UpdateCardStatus(card, info);
                ReorderSignalFeed();
            }
        }

        private async Task ExecuteSignalFromCardAsync(Panel card)
        {
            await ExecuteSignalFromCardSafeAsync(card);
        }

        private async Task CloseTradeFromCardAsync(Panel card)
        {
            if (card.Tag is not SignalCardInfo info || info.Ticket <= 0) return;
            if (_bridge?.IsConnected != true) { Log("[WARN] Not connected to MT5."); return; }
            if (!Confirm($"Close position #{info.Ticket} ({info.Pair})?")) return;

            bool ok = await _bridge.CloseTradeAsync(info.Ticket).ConfigureAwait(false);
            Log(ok ? $"[OK] Closed #{info.Ticket}" : $"[ERROR] Failed to close #{info.Ticket}",
                ok ? C_GREEN : C_RED);

            if (ok)
            {
                var updated = info with { Status = SignalCardStatus.Executed, StatusText = $"#{info.Ticket} closed", Ticket = 0, Time = DateTime.Now };
                if (InvokeRequired) Invoke(() => UpdateCardStatus(card, updated));
                else UpdateCardStatus(card, updated);
            }
            await RefreshPositionsAsync();
        }

        private static (string text, Color color) GetNeutralStatusDisplay(SignalCardInfo info) =>
            info.Status switch
            {
                SignalCardStatus.Pending   => ("New / Pending", Color.FromArgb(250, 199, 117)),
                SignalCardStatus.Executing => ("Sending to MT5...", Color.FromArgb(99, 179, 237)),
                SignalCardStatus.Executed  => ($"Executed {info.StatusText}", Color.FromArgb(170, 150, 255)),
                SignalCardStatus.Rejected  => ($"Rejected {Truncate(info.StatusText, 40)}", Color.FromArgb(225, 175, 95)),
                SignalCardStatus.Error     => ($"Error {Truncate(info.StatusText, 40)}", Color.FromArgb(245, 190, 90)),
                _                          => (info.StatusText, Color.FromArgb(175, 175, 195))
            };

        private static (Color bg, Color stripe) GetNeutralStatusColors(SignalCardStatus status) =>
            status switch
            {
                SignalCardStatus.Pending   => (Color.FromArgb(18, 28, 50), Color.FromArgb(99, 179, 237)),
                SignalCardStatus.Executing => (Color.FromArgb(14, 32, 58), Color.FromArgb(60, 140, 255)),
                SignalCardStatus.Executed  => (Color.FromArgb(29, 26, 50), Color.FromArgb(170, 150, 255)),
                SignalCardStatus.Rejected  => (Color.FromArgb(42, 32, 24), Color.FromArgb(225, 175, 95)),
                SignalCardStatus.Error     => (Color.FromArgb(45, 34, 18), Color.FromArgb(245, 190, 90)),
                _                          => (Color.FromArgb(28, 29, 42), Color.FromArgb(80, 80, 100))
            };

        private static (string text, Color color) GetStatusDisplay(SignalCardInfo info) =>
            info.Status switch
            {
                SignalCardStatus.Pending   => ("⏳  Pending",                            Color.FromArgb(250, 199, 117)),
                SignalCardStatus.Executing => ("🔄  Executing...",                       Color.FromArgb(99,  179, 237)),
                SignalCardStatus.Executed  => ($"✅  {info.StatusText}",                 Color.FromArgb(72,  199, 142)),
                SignalCardStatus.Rejected  => ($"❌  {Truncate(info.StatusText, 40)}",   Color.FromArgb(252, 95,  95)),
                SignalCardStatus.Error     => ($"⚠   {Truncate(info.StatusText, 40)}",   Color.FromArgb(250, 150, 50)),
                _                          => (info.StatusText,                           Color.FromArgb(175, 175, 195))
            };

        private static (Color bg, Color stripe) GetStatusColors(SignalCardStatus status) =>
            status switch
            {
                SignalCardStatus.Pending   => (Color.FromArgb(18,  28,  50),  Color.FromArgb(99,  179, 237)),
                SignalCardStatus.Executing => (Color.FromArgb(14,  32,  58),  Color.FromArgb(60,  140, 255)),
                SignalCardStatus.Executed  => (Color.FromArgb(16,  36,  22),  Color.FromArgb(72,  199, 142)),
                SignalCardStatus.Rejected  => (Color.FromArgb(45,  18,  18),  Color.FromArgb(252, 95,  95)),
                SignalCardStatus.Error     => (Color.FromArgb(45,  26,  10),  Color.FromArgb(250, 150, 50)),
                _                          => (Color.FromArgb(28,  29,  42),  Color.FromArgb(80,  80,  100))
            };

        private static string Truncate(string s, int max) =>
            s.Length > max ? s[..(max - 3)] + "..." : s;

        private async void BtnStartClaude_Click(object? sender, EventArgs e) => await StartClaudeAsync();
        private async void BtnStopClaude_Click(object? sender, EventArgs e)  => await StopClaudeAsync();
        private void BtnResetPrompt_Click(object? sender, EventArgs e)       => _txtClaudePrompt.Text = ClaudeConfig.DefaultPrompt;

        private void BtnClearLog_Click(object? sender, EventArgs e) => _txtLog.Clear();
        private void BtnSaveLog_Click(object? sender, EventArgs e)
        {
            using var d = new SaveFileDialog { Filter = "Text|*.txt", FileName = "MT5Log" };
            if (d.ShowDialog() == DialogResult.OK) File.WriteAllText(d.FileName, _txtLog.Text);
        }
    }
}

