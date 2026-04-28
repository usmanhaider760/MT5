using MT5TradingBot.Core;
using MT5TradingBot.Models;
using MT5TradingBot.Modules.BrokerIntegration;
using MT5TradingBot.Services;
using Newtonsoft.Json;
using Serilog;
using System.ComponentModel;

namespace MT5TradingBot.UI
{
    public sealed partial class MainForm : Form
    {
        // â”€â”€ Services â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private MT5Bridge? _bridge;
        private AutoBotService? _bot;
        private ClaudeSignalService? _claude;
        private readonly SettingsManager _settings = new();
        private AppSettings _cfg = new();
        private bool _warnedZeroAccountValues;

        // â”€â”€ Timers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = 2500 };

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        public MainForm()
        {
            InitializeComponent();
            ApplyStableLayout();

            if (!IsDesignerHosted())
            {
                _tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
                _rtbBotHelp.Text = BotHelpText();
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
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  WIRE EVENTS â€” named handlers only, no lambdas
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
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

            _btnStartBot.Click   += BtnStartBot_Click;
            _btnStopBot.Click    += BtnStopBot_Click;
            _btnOpenFolder.Click += BtnOpenFolder_Click;

            _btnStartClaude.Click  += BtnStartClaude_Click;
            _btnStopClaude.Click   += BtnStopClaude_Click;
            _btnResetPrompt.Click  += BtnResetPrompt_Click;

            _btnClearLog.Click += BtnClearLog_Click;
            _btnSaveLog.Click  += BtnSaveLog_Click;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  CONNECT / DISCONNECT
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
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
                Log("âœ… Connected to MT5 EA", C_GREEN);
                await RefreshAsync();
            }
            else
            {
                Log("âŒ Cannot connect. Ensure:\n" +
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

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  TRADE EXECUTION
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private async Task SubmitTradeAsync(TradeType dir)
        {
            if (!AssertConnected()) return;

            if (!double.TryParse(_txtSL.Text, out double sl) || sl == 0)
            { Log("âŒ Invalid Stop Loss", C_RED); return; }
            if (!double.TryParse(_txtTP.Text, out double tp) || tp == 0)
            { Log("âŒ Invalid Take Profit", C_RED); return; }

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

            Log(result.IsSuccess ? $"âœ… {result}" : $"âŒ {result}", result.IsSuccess ? C_GREEN : C_RED);
            AddHistoryRow(req, result);
        }

        private async Task ExecuteJsonAsync()
        {
            if (!AssertConnected()) return;
            try
            {
                var req = JsonConvert.DeserializeObject<TradeRequest>(_txtJson.Text);
                if (req == null) { Log("âŒ Invalid JSON structure", C_RED); return; }

                var (valid, err) = req.Validate();
                if (!valid) { Log($"âŒ Validation: {err}", C_RED); return; }

                TradeResult result = _bot != null
                    ? await _bot.ExecuteTradeWithValidationAsync(req)
                    : await _bridge!.OpenTradeAsync(req);

                Log(result.IsSuccess ? $"âœ… {result}" : $"âŒ {result}", result.IsSuccess ? C_GREEN : C_RED);
                AddHistoryRow(req, result);
            }
            catch (JsonException ex) { Log($"âŒ JSON parse error: {ex.Message}", C_RED); }
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
            catch { Log("âŒ Cannot format â€” invalid JSON", C_RED); }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  AUTO BOT
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private async Task StartBotAsync()
        {
            if (!AssertConnected()) return;

            _cfg.Bot = ReadBotConfigFromUI();
            await _settings.SaveAsync(_cfg);
            await (_bot?.DisposeAsync() ?? ValueTask.CompletedTask);

            _bot = new AutoBotService(_bridge!, _cfg.Bot);
            _bot.OnLog += msg => Log(msg);
            _bot.OnTradeExecuted += r =>
                Log(r.IsSuccess ? $"ðŸ¤– Bot trade: {r}" : $"ðŸ¤– Bot rejected: {r.ErrorMessage}",
                    r.IsSuccess ? C_GREEN : C_RED);
            _bot.OnBotStatusChanged += on => UpdateBotBadge(on);

            await _bot.StartAsync();
        }

        private async Task StopBotAsync()
        {
            if (_bot == null) return;
            await _bot.DisposeAsync();
            _bot = null;
            UpdateBotBadge(false);
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  CLAUDE AI
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private async Task StartClaudeAsync()
        {
            if (_bridge?.IsConnected != true)
            { Log("âŒ Connect to MT5 first.", C_RED); return; }

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

            _claude.OnLog            += msg => Log($"[Claude] {msg}");
            _claude.OnSignalGenerated += req => Log($"ðŸ§  Signal: {req}", C_ACCENT);
            _claude.OnStatusChanged  += on => UpdateClaudeBadge(on);

            try { await _claude.StartAsync(); }
            catch (Exception ex)
            {
                Log($"âŒ Claude start failed: {ex.Message}", C_RED);
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
                _lblClaudeBadge.Text      = running ? "â— CLAUDE RUNNING" : "â— CLAUDE STOPPED";
                _lblClaudeBadge.ForeColor = running ? C_GREEN : C_RED;
                _btnStartClaude.Enabled   = !running;
                _btnStopClaude.Enabled    = running;
            });
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  POSITIONS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
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
            });
        }

        private async Task CloseSelectedAsync()
        {
            if (_bridge == null || _gridPos.SelectedRows.Count == 0) return;
            if (!long.TryParse(_gridPos.SelectedRows[0].Cells[0].Value?.ToString(), out long t)) return;
            if (Confirm($"Close ticket #{t}?"))
            {
                bool ok = await _bridge.CloseTradeAsync(t);
                Log(ok ? $"âœ… Closed #{t}" : $"âŒ Failed to close #{t}", ok ? C_GREEN : C_RED);
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

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  REFRESH
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
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

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  R:R CALCULATOR
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
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

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  UI HELPERS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
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
                _lblBotBadge.Text      = running ? "â— BOT RUNNING" : "â— BOT STOPPED";
                _lblBotBadge.ForeColor = running ? C_GREEN : C_RED;
                _btnStartBot.Enabled   = !running;
                _btnStopBot.Enabled    = running;
            });
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
        }

        private ClaudeConfig ReadClaudeConfigFromUI() => new()
        {
            ApiKey              = _txtClaudeApiKey.Text.Trim(),
            WatchSymbols        = [.. _txtClaudeSymbols.Text.Split(',').Select(s => s.Trim().ToUpper()).Where(s => s.Length > 0)],
            PollIntervalSeconds = (int)_nudClaudePollSec.Value,
            SystemPrompt        = _txtClaudePrompt.Text,
            Model               = "claude-opus-4-7"
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

        // â”€â”€ Log â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

        // â”€â”€ Utility â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private void UIThread(Action a) { if (InvokeRequired) Invoke(a); else a(); }

        private bool AssertConnected()
        {
            if (_bridge?.IsConnected == true) return true;
            Log("âŒ Not connected to MT5. Click Connect first.", C_RED);
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

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  STATIC DATA
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
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
            TOTAL AUTOMATION â€” HOW IT WORKS
            â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

            1. Connect the app to MT5 (Named Pipe)
            2. Start the bot â†’ it watches your folder
            3. Drop a .json file into the folder

            The bot then:
              âœ“ Reads and validates the JSON
              âœ“ Checks: pair allowed, daily limit,
                R:R ratio, free margin, equity
              âœ“ Auto-calculates lot size from risk %
              âœ“ Sends trade to MT5 via named pipe
              âœ“ Retries on failure (configurable)
              âœ“ Moves file to /executed or /rejected
              âœ“ Logs to trade_history.csv

            Every 2 seconds the bot also:
              âœ“ Checks SL â†’ breakeven (at 60% TP)
              âœ“ Monitors drawdown â†’ emergency close
              âœ“ Polls folder (watcher backup)

            SIGNAL FOLDERS:
            â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            signals/              â† drop files here
            signals/executed/     â† success
            signals/rejected/     â† validation fail
            signals/error/        â† bad JSON
            signals/trade_history.csv â† full log

            SAMPLE JSON FILE:
            â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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
            â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            â€¢ MT5 running with TradingBotEA.ex5
            â€¢ AutoTrading ON (green button in MT5)
            â€¢ Pipe name matches in both apps
            """;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  NAMED EVENT HANDLERS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
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

