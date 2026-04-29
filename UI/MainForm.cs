using MT5TradingBot.Core;
using MT5TradingBot.Models;
using MT5TradingBot.Modules.BrokerIntegration;
using MT5TradingBot.Modules.MarketData;
using MT5TradingBot.Modules.PairScanner;
using MT5TradingBot.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using System.ComponentModel;
using System.Diagnostics;
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

        // -- Pair analysis feed ------------------------------------
        private readonly Dictionary<string, Panel> _pairAnalysisCards = new(StringComparer.OrdinalIgnoreCase);
        private bool _suppressPairCheckEvent;

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

            _clbAllowedPairs.ItemCheck += ClbAllowedPairs_ItemCheck;

            _btnStartBot.Click        += BtnStartBot_Click;
            _btnAnalyzePairs.Click    += BtnAnalyzePairs_Click;
            _btnOpenFolder.Click      += BtnOpenFolder_Click;
            _btnBotInstructions.Click += BtnBotInstructions_Click;

            _btnStartClaude.Click    += BtnStartClaude_Click;
            _btnStopClaude.Click     += BtnStopClaude_Click;
            _btnTestClaudeApi.Click  += BtnTestClaudeApi_Click;
            _btnTestNewsApi.Click    += BtnTestNewsApi_Click;
            _btnTestTelegram.Click   += BtnTestTelegram_Click;
            _btnResetPrompt.Click    += BtnResetPrompt_Click;

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

        private async Task AnalyzePairsAsync()
        {
            if (_bridge?.IsConnected != true)
            {
                Log("[BOT] MT5 is not connected. Cannot analyze pairs.", C_RED);
                SetBotBadge("PAIR ANALYSIS NEEDS MT5", C_RED);
                return;
            }

            var allPairs = _clbAllowedPairs.Items.Cast<string>()
                .Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            if (allPairs.Count == 0)
            {
                Log("[BOT] No pairs available in pair dropdown.", C_YELLOW);
                return;
            }

            try
            {
                _btnAnalyzePairs.Enabled = false;
                SetBotBadge("ANALYZING PAIRS...", C_ACCENT);
                Log($"[BOT] Analyze Pair clicked — scanning {allPairs.Count} pairs from dropdown...", C_ACCENT);

                _cfg.Bot = ReadBotConfigFromUI();
                await _settings.SaveAsync(_cfg);

                // Step 1: collect MT5 data for every pair in the list
                Log("[BOT] Pair list loaded — collecting MT5 data per pair...", C_ACCENT);
                var scanner     = new PairScanner(new MarketDataService(_bridge));
                var scanResults = await scanner.ScanAsync(allPairs, _cfg.Bot).ConfigureAwait(false);

                foreach (var r in scanResults)
                    Log($"[BOT] {(r.IsAvailable ? "OK" : "SKIP")} {r.Pair} | " +
                        $"Spread {r.SpreadPips:F1} pips | Score {r.Score:F0} | {r.Reason}",
                        r.IsAvailable ? C_GREEN : C_YELLOW);

                // Step 2: AI pair selection (if API is configured)
                string? selectedPair = null;
                string  aiConfidence = "-";
                string  aiDirection  = "NONE";
                string  aiReason     = "";

                bool aiReady = !string.IsNullOrWhiteSpace(_cfg.Claude?.ApiKey)
                            && !_cfg.Claude.ApiKey.StartsWith("sk-ant-..")
                            && _cfg.Claude.ApiKey.Length > 20;

                if (aiReady)
                {
                    Log("[BOT] Sending pair comparison JSON to AI...", C_ACCENT);
                    var (aiPair, conf, dir, reason, err) =
                        await RunAiPairSelectionAsync(scanResults).ConfigureAwait(false);

                    if (!string.IsNullOrEmpty(err))
                        Log($"[BOT] AI pair selection error: {err}", C_RED);
                    else if (string.IsNullOrEmpty(aiPair) || dir == "NO_TRADE")
                    {
                        Log($"[BOT] AI: No suitable pair — {reason}", C_YELLOW);
                        SetBotBadge("AI: NO SUITABLE PAIR", C_YELLOW);
                    }
                    else
                    {
                        Log($"[BOT] AI best pair response: {aiPair} ({dir}, {conf}) — {reason}", C_GREEN);
                        selectedPair = aiPair;
                        aiConfidence = conf;
                        aiDirection  = dir;
                        aiReason     = reason;
                    }
                }

                // Step 3: fallback to highest-scoring scanner result
                if (selectedPair == null)
                {
                    var best = scanResults.FirstOrDefault(r => r.IsAvailable);
                    if (best == null)
                    {
                        Log("[BOT] No available pairs found after scan.", C_YELLOW);
                        SetBotBadge("NO PAIRS AVAILABLE", C_YELLOW);
                        return;
                    }
                    selectedPair = best.Pair;
                    aiReason     = best.Reason;
                    Log($"[BOT] Using scanner best pair (AI not active): {selectedPair}", C_ACCENT);
                }

                // Step 4: map to actual dropdown entry (broker suffix handling)
                string? dropdownPair = FindDropdownPair(selectedPair);
                if (dropdownPair == null)
                {
                    Log($"[BOT] AI selected pair '{selectedPair}' is not available in current pair list.", C_RED);
                    SetBotBadge("PAIR NOT IN LIST", C_RED);
                    return;
                }

                // Step 5: create/update signal feed row
                Log($"[BOT] Dropdown selected by AI: {dropdownPair}", C_ACCENT);
                var card = EnsureSignalFeedRowForPair(dropdownPair);

                // Step 6: check the pair in dropdown (suppress ItemCheck — we handle it here)
                _suppressPairCheckEvent = true;
                ProgrammaticallyCheckPair(dropdownPair);
                _suppressPairCheckEvent = false;

                // Step 7: stamp row with AI selection data
                if (card.Tag is PairAnalysisInfo paInfo)
                {
                    paInfo.Direction   = aiDirection;
                    paInfo.Confidence  = aiConfidence;
                    paInfo.Status      = "AI Selected";
                    paInfo.ShortReason = aiReason;
                    paInfo.LastUpdated = DateTime.Now;
                    UpdatePairAnalysisCard(card, paInfo);
                }

                SetBotBadge($"AI SELECTED: {dropdownPair}", C_GREEN);

                // Step 8: run decision module for selected pair
                if (aiReady)
                {
                    Log($"[BOT] Decision module started for {dropdownPair}...", C_ACCENT);
                    await RunDecisionAnalysisForPairAsync(dropdownPair, card).ConfigureAwait(false);
                    Log($"[BOT] Decision module completed for {dropdownPair}.", C_ACCENT);
                }
            }
            catch (Exception ex)
            {
                SetBotBadge("PAIR ANALYSIS FAILED", C_RED);
                Log($"[BOT] Pair analysis failed: {ex.Message}", C_RED);
            }
            finally
            {
                _btnAnalyzePairs.Enabled = true;
            }
        }

        // ==========================================================
        //  AI API CONFIGURATION
        // ==========================================================
        private async Task StartClaudeAsync()
        {
            if (_bridge?.IsConnected != true)
            { Log("[ERROR] Connect to MT5 first.", C_RED); return; }

            _cfg.Claude = ReadClaudeConfigFromUI();
            _cfg.ApiIntegrations = ReadApiIntegrationConfigFromUI();
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

        private async Task TestClaudeApiAsync()
        {
            var cfg = ReadClaudeConfigFromUI();
            _cfg.Claude = cfg;
            _cfg.ApiIntegrations = ReadApiIntegrationConfigFromUI();
            await _settings.SaveAsync(_cfg);
            string key = cfg.ApiKey;

            if (string.IsNullOrWhiteSpace(key) || key.StartsWith("sk-ant-.."))
            {
                SetApiTestStatus("Enter a valid API key first.", C_YELLOW);
                Log("[AI] Test skipped — no API key entered.", C_YELLOW);
                return;
            }

            _btnTestClaudeApi.Enabled = false;
            SetApiTestStatus("Connecting...", C_ACCENT);
            Log($"[AI] Testing API connection (model: {cfg.Model})...", C_ACCENT);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var client = new Anthropic.AnthropicClient { ApiKey = key };
                var response = await client.Messages.Create(
                    new Anthropic.Models.Messages.MessageCreateParams
                    {
                        Model     = cfg.Model,
                        MaxTokens = 16,
                        Messages  =
                        [
                            new() { Role = Anthropic.Models.Messages.Role.User, Content = "Say OK" }
                        ]
                    }).ConfigureAwait(false);

                sw.Stop();

                string replyText = "";
                foreach (var block in response.Content)
                    if (block.TryPickText(out var tb)) { replyText = tb!.Text.Trim(); break; }

                string status = $"OK ({sw.ElapsedMilliseconds} ms)  |  model: {cfg.Model}  |  reply: {replyText}";
                SetApiTestStatus(status, C_GREEN);
                Log($"[AI] API test passed — {status}", C_GREEN);
            }
            catch (Exception ex)
            {
                sw.Stop();
                string err = CategorizeApiError(ex);
                SetApiTestStatus($"FAILED: {err}", C_RED);
                Log($"[AI] API test failed ({sw.ElapsedMilliseconds} ms): {err}", C_RED);
            }
            finally
            {
                _btnTestClaudeApi.Enabled = true;
            }
        }

        private void SetApiTestStatus(string text, Color color)
        {
            UIThread(() =>
            {
                _lblApiTestStatus.Text      = text;
                _lblApiTestStatus.ForeColor = color;
            });
        }

        private async Task TestNewsApiConfigAsync()
        {
            _cfg.ApiIntegrations = ReadApiIntegrationConfigFromUI();
            await _settings.SaveAsync(_cfg);

            bool disabled = string.Equals(_cfg.ApiIntegrations.NewsProvider, "None", StringComparison.OrdinalIgnoreCase);
            bool configured = disabled || !string.IsNullOrWhiteSpace(_cfg.ApiIntegrations.NewsApiKey);
            string message = disabled
                ? "News provider disabled."
                : configured
                    ? $"Configured for {_cfg.ApiIntegrations.NewsProvider}; live API call not wired yet."
                    : "Enter a news API key before enabling news filtering.";

            SetNewsTestStatus(message, configured ? C_GREEN : C_YELLOW);
            Log($"[AI] News API config check: {message}", configured ? C_GREEN : C_YELLOW);
            UpdateAiApiConfigStatus(_cfg.Claude);
        }

        private async Task TestTelegramConfigAsync()
        {
            _cfg.ApiIntegrations = ReadApiIntegrationConfigFromUI();
            await _settings.SaveAsync(_cfg);

            bool configured = !string.IsNullOrWhiteSpace(_cfg.ApiIntegrations.TelegramBotToken)
                && !string.IsNullOrWhiteSpace(_cfg.ApiIntegrations.TelegramChatId);
            string message = configured
                ? "Telegram configured; live send test not wired yet."
                : "Enter Telegram bot token and chat ID.";

            SetTelegramTestStatus(message, configured ? C_GREEN : C_YELLOW);
            Log($"[AI] Notification config check: {message}", configured ? C_GREEN : C_YELLOW);
            UpdateAiApiConfigStatus(_cfg.Claude);
        }

        private void SetNewsTestStatus(string text, Color color)
        {
            UIThread(() =>
            {
                _lblNewsTestStatus.Text      = text;
                _lblNewsTestStatus.ForeColor = color;
            });
        }

        private void SetTelegramTestStatus(string text, Color color)
        {
            UIThread(() =>
            {
                _lblTelegramTestStatus.Text      = text;
                _lblTelegramTestStatus.ForeColor = color;
            });
        }

        private static string CategorizeApiError(Exception ex)
        {
            string msg = ex.Message;
            if (msg.Contains("401") || msg.Contains("authentication_error") || msg.Contains("invalid_api_key"))
                return "Invalid API key (401) — check key in AI API Config tab";
            if (msg.Contains("403"))
                return "Forbidden (403) — key may lack permissions for this model";
            if (msg.Contains("429") || msg.Contains("rate_limit"))
                return "Rate limited (429) — wait and retry";
            if (msg.Contains("529") || msg.Contains("overloaded"))
                return "API overloaded (529) — retry in a few minutes";
            if (msg.Contains("model_not_found") || msg.Contains("model"))
                return $"Model not found — verify model name: {msg[..Math.Min(80, msg.Length)]}";
            if (msg.Contains("SocketException") || msg.Contains("HttpRequestException") || msg.Contains("timeout"))
                return "Network error — check internet connection";
            return msg.Length > 120 ? msg[..120] + "…" : msg;
        }

        // ── AI Trade Decision (one-shot, from Review Trade dialog) ────

        private const string AiTradeDecisionSystemPrompt = """
You are a professional forex/CFD trading decision engine.

Your job is to analyze the complete market JSON provided by my trading bot and return ONLY a valid JSON trading signal.

You must decide one of: BUY, SELL, WAIT, NO_TRADE

Important: Do not force a trade. Capital protection is more important than taking a trade.
If data is weak, missing, conflicting, or risk is not acceptable, return NO_TRADE or WAIT.

INPUT: You will receive one complete JSON object containing account, session, symbol, price, candles, indicators, structure, levels, positions, last_order, history, risk, news, sentiment, correlation, higher_timeframe, volume_analysis, volatility, liquidity, data_quality.

MAIN DECISION RULES:
1. DATA QUALITY: Reject if data_quality.score < 70 or ready_for_decision_module = false.
2. NEWS: Reject if news_risk_level = HIGH or high_impact_next_60_min = true.
3. SPREAD/EXECUTION: Reject if spread_normal = false, trade_allowed = false, market_open = false, or duplicate trade exists.
4. TREND/STRUCTURE: Prefer aligned trend across H4/H1/M15. entry_confirmed must be true for BUY/SELL.
5. INDICATORS: Use as confirmation only. Check RSI, MACD, EMA, ADX, Stochastic.
6. SENTIMENT: Contrarian indicator. Heavy retail long can support SELL and vice versa.
7. CORRELATION: Confirm via USD/base currency strength alignment.
8. VOLUME: Prefer volume_confirms_move = true.
9. VOLATILITY: Reject if trade_allowed_by_volatility = false.
10. LIQUIDITY: Avoid entries directly into nearby liquidity traps.
11. RISK: Require rr_ratio >= 1.5 and valid lot/SL/TP.

ENTRY/SL/TP RULES:
- BUY uses ask price, SELL uses bid price.
- SL based on structure + ATR buffer. TP based on nearest S/R or liquidity level.
- If RR < 1.5 for any valid TP: NO_TRADE.

CONFIDENCE SCORING (0-100):
- Data quality: 15 | News safety: 15 | Structure/trend: 20 | Entry confirmation: 15
- Risk/reward: 15 | Correlation/sentiment: 10 | Volume/volatility/liquidity: 10
- Score 0-49=LOW, 50-69=MEDIUM, 70-84=HIGH, 85-100=VERY_HIGH
- Only allow BUY/SELL if score >= 70, RR >= 1.5, no high-impact news, entry confirmed.

OUTPUT RULES: Return ONLY valid JSON, no explanation, no markdown, no comments.

Use EXACTLY this JSON format:
{
  "signal_id": "",
  "generated_at_utc": "",
  "symbol": "",
  "decision": "BUY/SELL/WAIT/NO_TRADE",
  "order_type": "MARKET/PENDING/NONE",
  "direction": "BUY/SELL/NONE",
  "confidence": "LOW/MEDIUM/HIGH/VERY_HIGH",
  "confluence_score": 0,
  "entry": {
    "entry_price": 0,
    "entry_type": "MARKET/BUY_LIMIT/SELL_LIMIT/BUY_STOP/SELL_STOP/NONE",
    "pending_entry_condition": "",
    "entry_reason": ""
  },
  "risk_plan": {
    "stop_loss": 0,
    "take_profit_1": 0,
    "take_profit_2": 0,
    "take_profit_3": 0,
    "sl_distance_pips": 0,
    "tp1_distance_pips": 0,
    "tp2_distance_pips": 0,
    "rr_ratio_tp1": 0,
    "rr_ratio_tp2": 0,
    "risk_percent": 0,
    "risk_amount": 0,
    "suggested_lot": 0
  },
  "trade_management": {
    "move_sl_to_breakeven_at": 0,
    "partial_close_tp1_percent": 0,
    "partial_close_tp2_percent": 0,
    "trailing_stop_enabled": false,
    "trailing_stop_after_pips": 0,
    "max_trade_duration_minutes": 0
  },
  "validation": {
    "data_quality_ok": false,
    "news_ok": false,
    "spread_ok": false,
    "structure_ok": false,
    "entry_confirmed": false,
    "risk_reward_ok": false,
    "correlation_ok": false,
    "sentiment_ok": false,
    "volume_ok": false,
    "volatility_ok": false,
    "liquidity_ok": false
  },
  "reason": [""],
  "warnings": [""],
  "blocking_reasons": [""],
  "modules_used": [],
  "execution_permission": {
    "allowed_to_execute": false,
    "requires_human_confirmation": true,
    "reason": ""
  }
}

SAFETY RULES:
- If NO_TRADE: entry_price=0, stop_loss=0, take_profits=0, suggested_lot=0, allowed_to_execute=false.
- If WAIT: provide pending_entry_condition, allowed_to_execute=false.
- If BUY/SELL: entry_price, stop_loss, take_profit_1 must be valid; rr_ratio_tp1 >= 1.5; allowed_to_execute=true only if ALL validation checks pass.
- Never trade only because user wants one. Never ignore news, RR, or missing data.
""";

        private async Task<(string ResponseJson, bool Allowed, string Decision, string Error)>
            RunAiTradeDecisionAsync(string snapshotJson)
        {
            string key   = _cfg.Claude.ApiKey;
            string model = string.IsNullOrWhiteSpace(_cfg.Claude.Model) ? "claude-sonnet-4-6" : _cfg.Claude.Model;

            var client = new Anthropic.AnthropicClient { ApiKey = key };
            try
            {
                var response = await client.Messages.Create(
                    new Anthropic.Models.Messages.MessageCreateParams
                    {
                        Model     = model,
                        MaxTokens = 4096,
                        System    = new List<Anthropic.Models.Messages.TextBlockParam>
                        {
                            new() { Text = AiTradeDecisionSystemPrompt }
                        },
                        Messages  =
                        [
                            new() { Role = Anthropic.Models.Messages.Role.User, Content = snapshotJson }
                        ]
                    }).ConfigureAwait(false);

                string rawText = "";
                foreach (var block in response.Content)
                    if (block.TryPickText(out var tb)) { rawText = tb!.Text; break; }

                if (string.IsNullOrWhiteSpace(rawText))
                    return ("", false, "NO_TRADE", "AI returned empty response");

                // Extract JSON from response (may have whitespace/preamble)
                int jsonStart = rawText.IndexOf('{');
                int jsonEnd   = rawText.LastIndexOf('}');
                if (jsonStart < 0 || jsonEnd <= jsonStart)
                    return (rawText, false, "NO_TRADE", "Response is not valid JSON");

                string responseJson = rawText[jsonStart..(jsonEnd + 1)];
                var jobj = JObject.Parse(responseJson);

                string decision = jobj.Value<string>("decision") ?? "NO_TRADE";
                bool allowed = jobj["execution_permission"]?.Value<bool>("allowed_to_execute") == true;

                string prettyJson = jobj.ToString(Formatting.Indented);
                return (prettyJson, allowed, decision.ToUpper(), "");
            }
            catch (Exception ex)
            {
                return ("", false, "NO_TRADE", CategorizeApiError(ex));
            }
        }

        private static TradeRequest BuildSignalFromAiDecision(TradeRequest original, string responseJson)
        {
            try
            {
                var jobj    = JObject.Parse(responseJson);
                var entry   = jobj["entry"];
                var risk    = jobj["risk_plan"];
                string dir  = (jobj.Value<string>("direction") ?? original.TradeType.ToString()).ToUpper();
                string etype = (entry?.Value<string>("entry_type") ?? "MARKET").ToUpper();

                double entryPrice = entry?.Value<double>("entry_price") ?? 0;
                double sl         = risk?.Value<double>("stop_loss")     ?? original.StopLoss;
                double tp1        = risk?.Value<double>("take_profit_1") ?? original.TakeProfit;
                double tp2        = risk?.Value<double>("take_profit_2") ?? original.TakeProfit2;
                double lot        = risk?.Value<double>("suggested_lot") ?? original.LotSize;

                var orderType = etype switch
                {
                    "BUY_LIMIT"  => OrderType.LIMIT,
                    "SELL_LIMIT" => OrderType.LIMIT,
                    "BUY_STOP"   => OrderType.STOP,
                    "SELL_STOP"  => OrderType.STOP,
                    _            => OrderType.MARKET
                };

                return new TradeRequest
                {
                    Id          = Guid.NewGuid().ToString("N")[..8].ToUpper(),
                    Pair        = jobj.Value<string>("symbol") ?? original.Pair,
                    TradeType   = dir == "SELL" ? TradeType.SELL : TradeType.BUY,
                    OrderType   = orderType,
                    EntryPrice  = entryPrice,
                    StopLoss    = sl,
                    TakeProfit  = tp1,
                    TakeProfit2 = tp2,
                    LotSize     = lot > 0 ? lot : original.LotSize,
                    Comment     = "AI_Decision",
                    MagicNumber = original.MagicNumber,
                    MoveSLToBreakevenAfterTP1 = original.MoveSLToBreakevenAfterTP1,
                    CreatedAt   = DateTime.UtcNow
                };
            }
            catch
            {
                // Fallback: return original signal with AI comment
                return new TradeRequest
                {
                    Id = original.Id, Pair = original.Pair, TradeType = original.TradeType,
                    OrderType = original.OrderType, EntryPrice = original.EntryPrice,
                    StopLoss = original.StopLoss, TakeProfit = original.TakeProfit,
                    TakeProfit2 = original.TakeProfit2, LotSize = original.LotSize,
                    Comment = "AI_Decision", MagicNumber = original.MagicNumber,
                    MoveSLToBreakevenAfterTP1 = original.MoveSLToBreakevenAfterTP1,
                    CreatedAt = DateTime.UtcNow
                };
            }
        }

        private string WriteSignalFile(TradeRequest req)
        {
            string folder = _cfg.Bot.WatchFolder.Trim();
            if (string.IsNullOrWhiteSpace(folder))
                folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MT5Bot", "signals");

            Directory.CreateDirectory(folder);
            string fileName = $"AI_{req.Pair}_{req.TradeType}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            string path     = Path.Combine(folder, fileName);
            File.WriteAllText(path, JsonConvert.SerializeObject(req, Formatting.Indented));
            Log($"[AI] Signal file written: {path}", C_GREEN);
            return path;
        }

        private static string ExtractAiBlockingReasons(string responseJson)
        {
            if (string.IsNullOrEmpty(responseJson)) return "No response";
            try
            {
                var jobj     = JObject.Parse(responseJson);
                var blocking = jobj["blocking_reasons"]?.ToObject<List<string>>() ?? [];
                var reasons  = jobj["reason"]?.ToObject<List<string>>() ?? [];
                var all      = blocking.Concat(reasons).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                return all.Count > 0 ? string.Join("; ", all.Take(3)) : "No details";
            }
            catch { return "Could not parse reasons"; }
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
            var integrations = ReadApiIntegrationConfigFromUI();
            bool claudeConfigured = !string.IsNullOrWhiteSpace(config.ApiKey)
                && !string.IsNullOrWhiteSpace(config.Model);
            bool openAiConfigured = !string.IsNullOrWhiteSpace(integrations.OpenAiApiKey)
                && !string.IsNullOrWhiteSpace(integrations.OpenAiModel);
            bool aiConfigured = integrations.AiProvider switch
            {
                "OpenAI" => openAiConfigured,
                "Both" => claudeConfigured && openAiConfigured,
                _ => claudeConfigured
            };
            bool newsConfigured = string.Equals(integrations.NewsProvider, "None", StringComparison.OrdinalIgnoreCase)
                || !string.IsNullOrWhiteSpace(integrations.NewsApiKey);
            bool notifyConfigured = !string.IsNullOrWhiteSpace(integrations.TelegramBotToken)
                && !string.IsNullOrWhiteSpace(integrations.TelegramChatId);
            UIThread(() =>
            {
                if (_claude?.IsRunning == true) return;
                _lblClaudeBadge.Text = aiConfigured
                    ? $"AI: {integrations.AiProvider} READY | NEWS: {(newsConfigured ? "READY" : "MISSING")} | TELEGRAM: {(notifyConfigured ? "READY" : "MISSING")}"
                    : $"AI: {integrations.AiProvider} MISSING";
                _lblClaudeBadge.ForeColor = aiConfigured ? C_GREEN : C_YELLOW;
            });

            if (!logResult) return;
            Log(aiConfigured
                    ? $"[AI] API configuration found for {integrations.AiProvider}. Startup check did not send a prompt or consume tokens."
                    : "[AI] API key/model missing. Configure the AI API Config tab before AI analysis.",
                aiConfigured ? C_GREEN : C_YELLOW);
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
            _cfg.ApiIntegrations ??= new ApiIntegrationConfig();
            _cmbMode.SelectedIndex   = _cfg.Mt5.Mode == ConnectionMode.NamedPipe ? 0 : 1;
            _txtPipeName.Text        = _cfg.Mt5.PipeName;
            _chkAutoConn.Checked     = _cfg.AutoConnectOnLaunch;
            _txtWatchFolder.Text     = _cfg.Bot.WatchFolder;
            _nudRisk.Value           = (decimal)_cfg.Bot.MaxRiskPercent;
            _nudMaxTrades.Value      = _cfg.Bot.MaxTradesPerDay;
            _nudPollMs.Value         = _cfg.Bot.PollIntervalMs;
            // Check saved pairs in the CheckedListBox (suppress row-creation event during bulk load)
            _suppressPairCheckEvent = true;
            for (int i = 0; i < _clbAllowedPairs.Items.Count; i++)
            {
                string item = _clbAllowedPairs.Items[i]?.ToString() ?? "";
                bool shouldCheck = _cfg.Bot.AllowedPairs.Count == 0
                    || _cfg.Bot.AllowedPairs.Any(p => string.Equals(p.Trim(), item, StringComparison.OrdinalIgnoreCase));
                _clbAllowedPairs.SetItemChecked(i, shouldCheck);
            }
            _suppressPairCheckEvent = false;
            _nudMinRR.Value          = (decimal)_cfg.Bot.MinRRRatio;
            _nudDrawdownPct.Value    = (decimal)_cfg.Bot.EmergencyCloseDrawdownPct;
            _nudRetry.Value          = _cfg.Bot.RetryCount;
            SelectComboValue(_cmbAiProvider, _cfg.ApiIntegrations.AiProvider);
            _txtClaudeApiKey.Text    = _cfg.Claude.ApiKey;
            _txtClaudeModel.Text     = _cfg.Claude.Model;
            _txtOpenAiApiKey.Text    = _cfg.ApiIntegrations.OpenAiApiKey;
            _txtOpenAiModel.Text     = _cfg.ApiIntegrations.OpenAiModel;
            _txtClaudeSymbols.Text   = string.Join(",", _cfg.Claude.WatchSymbols);
            _nudClaudePollSec.Value  = _cfg.Claude.PollIntervalSeconds;
            _nudAiConfidence.Value   = _cfg.ApiIntegrations.MinimumConfidencePercent;
            SelectComboValue(_cmbNewsProvider, _cfg.ApiIntegrations.NewsProvider);
            _txtNewsApiKey.Text      = _cfg.ApiIntegrations.NewsApiKey;
            _txtNewsCurrencies.Text  = string.Join(",", _cfg.ApiIntegrations.NewsCurrencies);
            SelectComboValue(_cmbNewsImpact, _cfg.ApiIntegrations.NewsImpactFilter);
            _nudNewsBefore.Value     = _cfg.ApiIntegrations.NewsBlackoutBeforeMinutes;
            _nudNewsAfter.Value      = _cfg.ApiIntegrations.NewsBlackoutAfterMinutes;
            _txtTelegramBotToken.Text = _cfg.ApiIntegrations.TelegramBotToken;
            _txtTelegramChatId.Text  = _cfg.ApiIntegrations.TelegramChatId;
            _chkNotifySignals.Checked = _cfg.ApiIntegrations.NotifySignals;
            _chkNotifyApproval.Checked = _cfg.ApiIntegrations.NotifyApprovalNeeded;
            _chkNotifyOpened.Checked = _cfg.ApiIntegrations.NotifyTradeOpened;
            _chkNotifyClosed.Checked = _cfg.ApiIntegrations.NotifyTradeClosed;
            _chkNotifyRisk.Checked   = _cfg.ApiIntegrations.NotifyRiskBlocked;
            _txtClaudePrompt.Text    = string.IsNullOrEmpty(_cfg.Claude.SystemPrompt)
                ? ClaudeConfig.DefaultPrompt : _cfg.Claude.SystemPrompt;
            _lblModelValue.Text      = _cfg.Claude.Model;
            _lblClaudeNote1.Text     = "Startup checks validate saved AI configuration only; no prompt is sent.";
            _lblClaudeNote2.Text     = "Tokens are used only when AI analysis/monitoring sends market data to the provider.";
            UpdateAiApiConfigStatus(_cfg.Claude);
        }

        private static void SelectComboValue(ComboBox comboBox, string value)
        {
            for (int i = 0; i < comboBox.Items.Count; i++)
            {
                if (string.Equals(comboBox.Items[i]?.ToString(), value, StringComparison.OrdinalIgnoreCase))
                {
                    comboBox.SelectedIndex = i;
                    return;
                }
            }

            if (comboBox.Items.Count > 0)
                comboBox.SelectedIndex = 0;
        }

        private ClaudeConfig ReadClaudeConfigFromUI() => new()
        {
            ApiKey              = _txtClaudeApiKey.Text.Trim(),
            WatchSymbols        = [.. _txtClaudeSymbols.Text.Split(',').Select(s => s.Trim().ToUpper()).Where(s => s.Length > 0)],
            PollIntervalSeconds = (int)_nudClaudePollSec.Value,
            SystemPrompt        = _txtClaudePrompt.Text,
            Model               = string.IsNullOrWhiteSpace(_txtClaudeModel.Text) ? "claude-opus-4-7" : _txtClaudeModel.Text.Trim()
        };

        private ApiIntegrationConfig ReadApiIntegrationConfigFromUI() => new()
        {
            AiProvider = _cmbAiProvider.SelectedItem?.ToString() ?? "Claude",
            OpenAiApiKey = _txtOpenAiApiKey.Text.Trim(),
            OpenAiModel = string.IsNullOrWhiteSpace(_txtOpenAiModel.Text) ? "gpt-5.1" : _txtOpenAiModel.Text.Trim(),
            MinimumConfidencePercent = (int)_nudAiConfidence.Value,
            NewsProvider = _cmbNewsProvider.SelectedItem?.ToString() ?? "Trading Economics",
            NewsApiKey = _txtNewsApiKey.Text.Trim(),
            NewsCurrencies = [.. _txtNewsCurrencies.Text.Split(',').Select(s => s.Trim().ToUpper()).Where(s => s.Length > 0)],
            NewsImpactFilter = _cmbNewsImpact.SelectedItem?.ToString() ?? "High only",
            NewsBlackoutBeforeMinutes = (int)_nudNewsBefore.Value,
            NewsBlackoutAfterMinutes = (int)_nudNewsAfter.Value,
            TelegramBotToken = _txtTelegramBotToken.Text.Trim(),
            TelegramChatId = _txtTelegramChatId.Text.Trim(),
            NotifySignals = _chkNotifySignals.Checked,
            NotifyApprovalNeeded = _chkNotifyApproval.Checked,
            NotifyTradeOpened = _chkNotifyOpened.Checked,
            NotifyTradeClosed = _chkNotifyClosed.Checked,
            NotifyRiskBlocked = _chkNotifyRisk.Checked
        };

        private BotConfig ReadBotConfigFromUI() => new()
        {
            Enabled                  = true,
            WatchFolder              = _txtWatchFolder.Text,
            MaxRiskPercent           = (double)_nudRisk.Value,
            MaxTradesPerDay          = (int)_nudMaxTrades.Value,
            PollIntervalMs           = (int)_nudPollMs.Value,
            AllowedPairs             = [.. _clbAllowedPairs.CheckedItems.Cast<string>()],
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
        private async void BtnAnalyzePairs_Click(object? sender, EventArgs e) => await AnalyzePairsAsync();

        private void ClbAllowedPairs_ItemCheck(object? sender, ItemCheckEventArgs e)
        {
            if (_suppressPairCheckEvent || e.NewValue != CheckState.Checked) return;
            string pair = _clbAllowedPairs.Items[e.Index]?.ToString() ?? "";
            if (!string.IsNullOrEmpty(pair))
            {
                Log($"[BOT] Manual pair selected: {pair}", C_ACCENT);
                _ = OnPairSelectionChangedAsync(pair);
            }
        }

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

            // Detail button — opens trade review dialog, does NOT immediately trade
            var btnExec = MakeCardButton("Detail", Color.FromArgb(20, 50, 30), Color.FromArgb(72, 199, 142),
                "Review — open trade details and approve before sending to MT5");
            btnExec.Size     = new Size(52, 22);
            btnExec.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
            btnExec.Location = new Point(w - 84, 8);
            btnExec.Visible  = CanExecuteSignal(info);
            btnExec.Tag      = "execute";
            btnExec.Click   += (_, _) => _ = ExecuteSignalFromCardSafeAsync(card);
            card.Controls.Add(btnExec);

            // JSON button — opens the signal file in the default text editor
            var btnJson = MakeCardButton("JSON", Color.FromArgb(20, 30, 55), Color.FromArgb(130, 170, 255),
                "Open JSON — view the raw signal file in your default editor");
            btnJson.Size     = new Size(38, 22);
            btnJson.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
            btnJson.Location = new Point(w - 130, 8);
            btnJson.Tag      = "json";
            btnJson.Click   += (_, _) =>
            {
                if (card.Tag is not SignalCardInfo ci) return;
                string path = ResolveSignalFilePath(ci);
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    Log($"[INFO] Signal file not found: {ci.FileName}", C_YELLOW);
                    return;
                }
                try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
                catch (Exception ex) { Log($"[ERROR] Cannot open file: {ex.Message}", C_RED); }
            };
            card.Controls.Add(btnJson);

            // Thin marquee progress bar — shown while async work is in progress
            var pbBusy = new ProgressBar
            {
                Style                 = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 30,
                Height                = 3,
                Location              = new Point(5, 0),
                Width                 = w - 5,
                Anchor                = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Visible               = false,
                Tag                   = "spinner"
            };
            card.Controls.Add(pbBusy);

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
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
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
            RefreshReviewDashboard(currentSnapshot, bindings);

            bool refreshingSnapshot = false;
            DateTime lastSyncTime = DateTime.Now;
            const int syncIntervalMs = 5000;

            var liveTimer = new System.Windows.Forms.Timer { Interval = syncIntervalMs };
            liveTimer.Tick += async (_, _) =>
            {
                if (refreshingSnapshot || _bridge == null || form.IsDisposed) return;
                refreshingSnapshot = true;
                try
                {
                    // Try full EA snapshot first
                    JObject? updated = await _bridge.GetMarketSnapshotAsync(request, _cfg.Bot).ConfigureAwait(false);

                    // EA doesn't implement GET_MARKET_SNAPSHOT — fetch fields individually
                    if (updated == null && !form.IsDisposed)
                    {
                        AccountInfo?       acct = null;
                        SymbolInfo?        sym  = null;
                        List<LivePosition> pos  = [];
                        try { acct = await _bridge.GetAccountInfoAsync().ConfigureAwait(false); }  catch { }
                        try { sym  = await _bridge.GetSymbolInfoAsync(request.Pair).ConfigureAwait(false); } catch { }
                        try { pos  = await _bridge.GetPositionsAsync().ConfigureAwait(false); }      catch { }
                        try { updated = JObject.Parse(BuildTradeReviewSnapshotJson(request, acct, sym, pos)); } catch { }
                    }

                    if (updated != null && !form.IsDisposed)
                    {
                        currentSnapshot    = updated;
                        latestSnapshotJson = currentSnapshot.ToString(Formatting.Indented);
                        form.Tag           = latestSnapshotJson;
                        RefreshReviewDashboard(currentSnapshot, bindings);
                    }
                }
                catch (Exception ex)
                {
                    if (!form.IsDisposed)
                        liveStatus.Text = $"  {DateTime.Now:HH:mm:ss}  |  Sync failed: {ex.Message}";
                }
                finally
                {
                    refreshingSnapshot = false;
                    lastSyncTime = DateTime.Now; // always reset countdown regardless of success
                }
            };
            form.FormClosed += (_, _) => liveTimer.Stop();
            liveTimer.Start();

            var clockTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            clockTimer.Tick += (_, _) =>
            {
                if (form.IsDisposed) return;
                int secsToNext = Math.Max(0, syncIntervalMs / 1000 - (int)(DateTime.Now - lastSyncTime).TotalSeconds);
                liveStatus.Text = $"  {DateTime.Now:HH:mm:ss}  |  Last sync: {lastSyncTime:HH:mm:ss}  |  Next sync in: {secsToNext}s";
            };
            form.FormClosed += (_, _) => clockTimer.Stop();
            clockTimer.Start();

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

            // ── Bottom section: status label + button row ──────────
            var bottomHost = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = form.BackColor
            };
            bottomHost.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            bottomHost.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.Controls.Add(bottomHost, 0, 3);

            var lblPlayStatus = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(110, 110, 130),
                Font = new Font("Segoe UI", 8.5F),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Text = ""
            };
            bottomHost.Controls.Add(lblPlayStatus, 0, 0);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = form.BackColor
            };
            bottomHost.Controls.Add(buttons, 0, 1);

            // State shared across handlers
            string aiResponseJson = "";
            TradeReviewDecision decision = new(false, false, 0, 0);

            // ── Build buttons ──────────────────────────────────────
            var btnPlay         = MakeDialogButton("Play / Start Trade", C_GREEN);
            var btnCancel       = MakeDialogButton("Cancel", Color.FromArgb(110, 110, 130));
            var btnViewJson     = MakeDialogButton("View JSON",    Color.FromArgb(28, 45, 80));
            var btnFilledValues = MakeDialogButton("Input JSON",   Color.FromArgb(28, 40, 65));
            var btnAiResponse   = MakeDialogButton("AI Response",  Color.FromArgb(40, 28, 65));

            btnViewJson.ForeColor     = Color.FromArgb(130, 180, 255);
            btnFilledValues.ForeColor = Color.FromArgb(130, 220, 180);
            btnAiResponse.ForeColor   = Color.FromArgb(210, 150, 255);
            btnAiResponse.Enabled     = false;

            buttons.Controls.Add(btnPlay);
            buttons.Controls.Add(btnCancel);
            buttons.Controls.Add(btnViewJson);
            buttons.Controls.Add(btnFilledValues);
            buttons.Controls.Add(btnAiResponse);

            // ── Helper: open a static JSON viewer form ─────────────
            void OpenJsonViewer(string title, Func<string> getJson, bool liveRefresh = false)
            {
                var jf = new Form
                {
                    Text            = title,
                    Size            = new Size(820, 640),
                    MinimumSize     = new Size(500, 380),
                    BackColor       = Color.FromArgb(18, 22, 36),
                    ForeColor       = Color.FromArgb(200, 210, 230),
                    StartPosition   = FormStartPosition.CenterScreen,
                    FormBorderStyle = FormBorderStyle.Sizable,
                    Icon            = form.Icon,
                };
                var rtb = new RichTextBox
                {
                    Dock = DockStyle.Fill, ReadOnly = true,
                    ScrollBars = RichTextBoxScrollBars.Both,
                    Font = new Font("Consolas", 9.5f),
                    BackColor = Color.FromArgb(14, 18, 30),
                    ForeColor = Color.FromArgb(180, 210, 255),
                    BorderStyle = BorderStyle.None, WordWrap = false,
                    Text = getJson()
                };
                var btnCopy = new Button
                {
                    Text = "Copy to Clipboard", Dock = DockStyle.Bottom, Height = 32,
                    FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(28, 45, 80),
                    ForeColor = Color.FromArgb(130, 180, 255),
                    Font = new Font("Segoe UI", 9f), Cursor = Cursors.Hand
                };
                btnCopy.FlatAppearance.BorderColor = Color.FromArgb(50, 80, 130);
                btnCopy.Click += (_, _) => { try { Clipboard.SetText(rtb.Text); } catch { } };
                jf.Controls.Add(rtb);
                jf.Controls.Add(btnCopy);

                if (liveRefresh)
                {
                    var t = new System.Windows.Forms.Timer { Interval = 2000 };
                    t.Tick += (_, _) => { if (jf.IsDisposed) { t.Stop(); return; } var s = getJson(); if (s != rtb.Text) rtb.Text = s; };
                    jf.Shown     += (_, _) => t.Start();
                    jf.FormClosed += (_, _) => { t.Stop(); t.Dispose(); };
                }

                jf.Show(form);
            }

            // ── Button: View JSON (live snapshot) ─────────────────
            btnViewJson.Click += (_, _) =>
                OpenJsonViewer("Market Snapshot JSON", () => latestSnapshotJson, liveRefresh: true);

            // ── Button: Input JSON (what was sent to AI) ──────────
            btnFilledValues.Click += (_, _) =>
                OpenJsonViewer("AI Input — Market Snapshot", () => latestSnapshotJson);

            // ── Button: AI Response ───────────────────────────────
            btnAiResponse.Click += (_, _) =>
                OpenJsonViewer("AI Trade Decision Response", () =>
                    string.IsNullOrEmpty(aiResponseJson) ? "{ \"status\": \"No AI response yet\" }" : aiResponseJson);

            // ── Helper: set status label ───────────────────────────
            void SetPlayStatus(string text, Color color)
            {
                lblPlayStatus.Text      = text;
                lblPlayStatus.ForeColor = color;
            }

            // ── Button: Play / Start Trade ────────────────────────
            btnPlay.Click += async (_, _) =>
            {
                btnPlay.Enabled   = false;
                btnPlay.Text      = "Analyzing...";
                string snapshot   = latestSnapshotJson;

                bool aiEnabled = !string.IsNullOrWhiteSpace(_cfg.Claude.ApiKey)
                              && !_cfg.Claude.ApiKey.StartsWith("sk-ant-..")
                              && _cfg.Claude.ApiKey.Length > 20;

                if (aiEnabled)
                {
                    try
                    {
                        SetPlayStatus("Sending to AI for analysis...", C_ACCENT);
                        Log("[AI] Running trade decision analysis on market snapshot...", C_ACCENT);

                        var (respJson, allowed, aiDecision, error) =
                            await RunAiTradeDecisionAsync(snapshot).ConfigureAwait(false);

                        aiResponseJson = respJson;
                        if (!string.IsNullOrEmpty(respJson)) btnAiResponse.Enabled = true;

                        if (!string.IsNullOrEmpty(error))
                        {
                            SetPlayStatus($"AI Error: {error}", C_RED);
                            Log($"[AI] Analysis failed: {error}", C_RED);
                            btnPlay.Text    = "Play / Start Trade";
                            btnPlay.Enabled = true;
                            return;
                        }

                        Log($"[AI] Decision: {aiDecision}", aiDecision is "BUY" or "SELL" ? C_GREEN : C_YELLOW);

                        if (!allowed || aiDecision is not "BUY" and not "SELL")
                        {
                            string reasons = ExtractAiBlockingReasons(respJson);
                            SetPlayStatus($"AI: {aiDecision} — {reasons}", C_YELLOW);
                            Log($"[AI] Trade not approved. Decision: {aiDecision} | {reasons}", C_YELLOW);
                            btnPlay.Text    = "Play / Start Trade";
                            btnPlay.Enabled = true;
                            return;
                        }

                        // AI approved — build signal from response and write to watch folder
                        var signalReq = BuildSignalFromAiDecision(request, respJson);
                        string signalPath = WriteSignalFile(signalReq);
                        Log($"[AI] APPROVED — {aiDecision} | Signal: {Path.GetFileName(signalPath)}", C_GREEN);
                        SetPlayStatus($"AI approved {aiDecision}. Signal: {Path.GetFileName(signalPath)}", C_GREEN);
                    }
                    catch (Exception ex)
                    {
                        SetPlayStatus($"Error: {ex.Message}", C_RED);
                        Log($"[AI] Exception during analysis: {ex.Message}", C_RED);
                        btnPlay.Text    = "Play / Start Trade";
                        btnPlay.Enabled = true;
                        return;
                    }
                }
                else
                {
                    Log("[AI] AI not configured — executing trade from signal values directly.", C_YELLOW);
                    SetPlayStatus("AI not configured — executing directly.", C_YELLOW);
                }

                form.Tag = snapshot;
                decision  = new TradeReviewDecision(
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
                Text = $"  {DateTime.Now:HH:mm:ss}  |  Last sync: —  |  Next sync in: 5s",
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
            const int rowH    = 27;
            const int maxRows = 7;
            const int headerH = 24;
            const int padV    = 10;

            bool needsScroll = metrics.Count > maxRows;
            int  visRows     = Math.Min(metrics.Count, maxRows);
            int  groupH      = headerH + padV + visRows * rowH + padV;

            var bg = Color.FromArgb(14, 16, 26);
            var group = new GroupBox
            {
                Text      = title,
                Width     = 284,
                Height    = groupH,
                ForeColor = Color.FromArgb(160, 170, 200),
                BackColor = bg,
                Font      = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
                Padding   = new Padding(6, 4, 6, 6),
                Margin    = new Padding(0, 0, 12, 12)
            };

            var scroll = new Panel
            {
                Dock        = DockStyle.Fill,
                AutoScroll  = needsScroll,
                BackColor   = bg
            };
            group.Controls.Add(scroll);

            var grid = new TableLayoutPanel
            {
                Dock        = needsScroll ? DockStyle.Top : DockStyle.Fill,
                AutoSize    = needsScroll,
                ColumnCount = 2,
                RowCount    = metrics.Count,
                BackColor   = bg,
                Padding     = new Padding(2, 4, 2, 4)
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));
            scroll.Controls.Add(grid);

            for (int i = 0; i < metrics.Count; i++)
            {
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, rowH));

                var name = new Label
                {
                    Text      = metrics[i].Label,
                    Dock      = DockStyle.Fill,
                    ForeColor = Color.FromArgb(115, 124, 152),
                    Font      = new Font("Segoe UI", 8.2F),
                    TextAlign = ContentAlignment.MiddleLeft,
                    AutoEllipsis = true,
                    Padding   = new Padding(4, 0, 0, 0)
                };

                var (initFg, initBg) = ReviewValueStyle(metrics[i].Path, null);
                var value = new Label
                {
                    Text      = "--",
                    Dock      = DockStyle.Fill,
                    ForeColor = initFg,
                    BackColor = initBg,
                    Font      = new Font("Consolas", 8.5F, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleRight,
                    AutoEllipsis = true,
                    Padding   = new Padding(0, 2, 6, 2)
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
            IReadOnlyList<(string Path, Label Value, string Format)> bindings)
        {
            foreach (var binding in bindings)
            {
                var token = snapshot.SelectToken(binding.Path);
                binding.Value.Text = FormatReviewValue(token, binding.Format);
                var (fg, bg) = ReviewValueStyle(binding.Path, token);
                binding.Value.ForeColor = fg;
                binding.Value.BackColor = bg;
            }
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

        private static (Color Fg, Color Bg) ReviewValueStyle(string path, JToken? token)
        {
            var critFg = Color.FromArgb(255, 95,  95);  var critBg = Color.FromArgb(72,  16, 16);
            var warnFg = Color.FromArgb(255, 200, 60);  var warnBg = Color.FromArgb(66,  50, 10);
            var goodFg = Color.FromArgb(72,  218, 128); var goodBg = Color.FromArgb(14,  56, 36);
            var infoFg = Color.FromArgb(110, 185, 255); var infoBg = Color.FromArgb(16,  36, 58);
            var normFg = Color.FromArgb(200, 210, 235); var normBg = Color.FromArgb(20,  24, 40);
            var dimFg  = Color.FromArgb(88,  96,  120); var dimBg  = Color.FromArgb(15,  17, 25);

            // Ignorable — metadata / static config
            if (path is "symbol.digits" or "symbol.min_lot" or "symbol.max_lot" or "symbol.lot_step"
                     or "symbol.execution_mode" or "symbol.filling_mode"
                     or "last_order.ticket" or "news.source" or "levels.key_level_type"
                     or "account.daily_trades_taken" or "session.session_name")
                return (dimFg, dimBg);

            // Info-only — price levels, indicators, candle data
            if (path is "price.bid" or "price.ask" or "price.daily_open" or "price.daily_high"
                     or "price.daily_low" or "price.daily_range_pips" or "price.prev_day_high"
                     or "structure.swing_high" or "structure.swing_low"
                     or "levels.nearest_support_1" or "levels.nearest_support_2"
                     or "levels.nearest_resistance_1" or "levels.nearest_resistance_2"
                     or "indicators.h1.ema20" or "indicators.h1.ema50" or "indicators.h1.ema200"
                     or "indicators.h1.atr" or "indicators.h1.macd_bias"
                     or "symbol.name" or "last_order.ticket")
                return (infoFg, infoBg);

            if (path.StartsWith("candles.") || path.StartsWith("structure.trend_"))
                return (infoFg, infoBg);

            if (token == null || token.Type == JTokenType.Null)
                return (normFg, normBg);

            string raw     = token.ToString(Formatting.None).Trim('"');
            bool   boolVal = token.Type == JTokenType.Boolean && token.Value<bool>();
            bool   isNum   = double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out double num);

            // ── Boolean fields ────────────────────────────────────────
            switch (path)
            {
                case "session.terminal_connected":
                case "session.market_open":
                case "symbol.trade_allowed":
                    return boolVal ? (goodFg, goodBg) : (critFg, critBg);

                case "session.is_weekend":
                    return boolVal ? (critFg, critBg) : (dimFg, dimBg);

                case "session.london_open":
                case "session.newyork_open":
                    return boolVal ? (goodFg, goodBg) : (dimFg, dimBg);

                case "session.overlap_active":
                    return boolVal ? (goodFg, goodBg) : (normFg, normBg);

                case "positions.duplicate_trade_exists":
                    return boolVal ? (critFg, critBg) : (goodFg, goodBg);

                case "positions.opposite_trade_exists":
                case "positions.same_pair_open":
                    return boolVal ? (warnFg, warnBg) : (normFg, normBg);

                case "news.high_impact_next_60_min":
                    return boolVal ? (critFg, critBg) : (goodFg, goodBg);

                case "structure.all_timeframes_aligned":
                    return boolVal ? (goodFg, goodBg) : (warnFg, warnBg);

                case "levels.price_at_key_level":
                    return boolVal ? (goodFg, goodBg) : (normFg, normBg);
            }

            // ── Numeric fields ────────────────────────────────────────
            if (isNum)
            {
                if (path.Contains("pnl", StringComparison.OrdinalIgnoreCase) ||
                    path.Contains("profit", StringComparison.OrdinalIgnoreCase) ||
                    path == "risk.daily_loss_remaining")
                    return num >= 0 ? (goodFg, goodBg) : (critFg, critBg);

                if (path.Contains("spread", StringComparison.OrdinalIgnoreCase))
                {
                    if (num > 3.0) return (critFg, critBg);
                    if (num > 1.5) return (warnFg, warnBg);
                    return (goodFg, goodBg);
                }

                if (path == "risk.rr_ratio")
                {
                    if (num >= 2.0) return (goodFg, goodBg);
                    if (num >= 1.5) return (normFg, normBg);
                    if (num >= 1.0) return (warnFg, warnBg);
                    return (critFg, critBg);
                }

                if (path == "account.margin_level")
                {
                    if (num > 500) return (goodFg, goodBg);
                    if (num > 200) return (normFg, normBg);
                    if (num > 150) return (warnFg, warnBg);
                    return (critFg, critBg);
                }

                if (path == "indicators.h1.rsi")
                {
                    if (num >= 70 || num <= 30) return (warnFg, warnBg);
                    if (num >= 40 && num <= 60) return (goodFg, goodBg);
                    return (normFg, normBg);
                }

                if (path == "indicators.h1.adx")
                {
                    if (num >= 25) return (goodFg, goodBg);
                    if (num >= 20) return (normFg, normBg);
                    return (dimFg, dimBg);
                }

                if (path.Contains("distance", StringComparison.OrdinalIgnoreCase))
                    return (infoFg, infoBg);

                if (path == "positions.total_open")
                    return num == 0 ? (dimFg, dimBg) : (normFg, normBg);
            }

            // ── String fields ─────────────────────────────────────────
            if (path == "news.news_risk_level")
                return raw.ToUpperInvariant() switch
                {
                    "HIGH"   => (critFg, critBg),
                    "MEDIUM" => (warnFg, warnBg),
                    "LOW"    => (goodFg, goodBg),
                    _        => (dimFg,  dimBg)
                };

            if (path == "indicators.h1.rsi_signal")
                return (raw is "Overbought" or "Oversold") ? (warnFg, warnBg) : (normFg, normBg);

            if (path == "structure.market_regime")
                return raw switch
                {
                    "Trending" => (goodFg, goodBg),
                    "Choppy"   => (warnFg, warnBg),
                    _          => (normFg, normBg)
                };

            if (path == "last_order.execution_result")
                return raw switch
                {
                    "Filled"             => (goodFg, goodBg),
                    "Rejected" or "Error" => (critFg, critBg),
                    _                    => (normFg, normBg)
                };

            return (normFg, normBg);
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
            double dollarRisk    = entry > 0 ? LotCalculator.DollarRisk(request.LotSize, entry, request.StopLoss, request.Pair) : 0;
            double dollarProfit  = entry > 0 ? LotCalculator.DollarProfit(request.LotSize, entry, request.TakeProfit, request.Pair) : 0;
            double dollarProfit2 = entry > 0 && request.TakeProfit2 > 0
                ? LotCalculator.DollarProfit(request.LotSize, entry, request.TakeProfit2, request.Pair) : 0;
            double tp2Pips       = entry > 0 && request.TakeProfit2 > 0
                ? Math.Abs(request.TakeProfit2 - entry) / pipSize : 0;
            double maxLossDollar = account != null
                ? Math.Round(account.Equity * _cfg.Bot.EmergencyCloseDrawdownPct / 100.0, 2) : 0;
            double dailyLossRemaining = account != null
                ? Math.Round(maxLossDollar + Math.Min(0, account.Profit), 2) : 0;
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
                    ["name"]           = symbol.Symbol,
                    ["digits"]         = symbol.Digits,
                    ["point_size"]     = Math.Pow(10, -symbol.Digits),
                    ["pip_size"]       = pipSize,
                    ["min_lot"]        = symbol.MinLot,
                    ["max_lot"]        = symbol.MaxLot,
                    ["lot_step"]       = 0.01,
                    ["spread_pips"]    = symbol.SpreadPips,
                    ["trade_allowed"]  = true,
                    ["execution_mode"] = "MARKET",
                    ["filling_mode"]   = "FOK"
                },
                ["price"] = symbol == null ? Unavailable("GET_SYMBOL_INFO failed") : new JObject
                {
                    ["bid"]              = symbol.Bid,
                    ["ask"]              = symbol.Ask,
                    ["spread_pips"]      = symbol.SpreadPips,
                    ["spread_normal"]    = symbol.SpreadPips <= _cfg.Bot.MaxSpreadPips,
                    ["daily_open"]       = null,
                    ["daily_high"]       = null,
                    ["daily_low"]        = null,
                    ["daily_range_pips"] = null,
                    ["prev_day_high"]    = null
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
                    ["max_risk_pct"]          = _cfg.Bot.MaxRiskPercent,
                    ["max_risk_dollar"]       = account == null ? 0 : Math.Round(account.Equity * _cfg.Bot.MaxRiskPercent / 100.0, 2),
                    ["min_rr_ratio"]          = _cfg.Bot.MinRRRatio,
                    ["suggested_sl"]          = request.StopLoss,
                    ["suggested_tp1"]         = request.TakeProfit,
                    ["suggested_tp2"]         = request.TakeProfit2,
                    ["sl_distance_pips"]      = Math.Round(slPips, 1),
                    ["tp1_distance_pips"]     = Math.Round(tpPips, 1),
                    ["tp2_distance_pips"]     = Math.Round(tp2Pips, 1),
                    ["rr_ratio"]              = Math.Round(rr, 2),
                    ["calculated_lot"]        = request.LotSize,
                    ["dollar_risk"]           = Math.Round(dollarRisk, 2),
                    ["dollar_profit_tp1"]     = Math.Round(dollarProfit, 2),
                    ["dollar_profit_tp2"]     = Math.Round(dollarProfit2, 2),
                    ["daily_loss_remaining"]  = dailyLossRemaining,
                    ["daily_loss_limit_dollar"] = maxLossDollar
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

        private void SetCardBusy(Panel card, bool busy)
        {
            if (card.IsDisposed) return;
            void Apply()
            {
                // Progress bar
                var pb = card.Controls.OfType<ProgressBar>()
                    .FirstOrDefault(c => c.Tag?.ToString() == "spinner");
                if (pb != null) pb.Visible = busy;

                if (busy)
                {
                    foreach (var btn in card.Controls.OfType<Button>())
                        btn.Enabled = false;
                }
                else
                {
                    // Restore each button to its correct state
                    if (card.Tag is SignalCardInfo info)
                        foreach (var btn in card.Controls.OfType<Button>())
                            switch (btn.Tag?.ToString())
                            {
                                case "delete":
                                    btn.Enabled = info.Status != SignalCardStatus.Executing;
                                    break;
                                case "close":
                                    btn.Visible = info.Status == SignalCardStatus.Executed && info.Ticket > 0;
                                    btn.Enabled = true;
                                    break;
                                case "execute":
                                    btn.Visible = CanExecuteSignal(info);
                                    btn.Enabled = true;
                                    break;
                            }
                }
            }
            if (card.InvokeRequired) card.Invoke(Apply); else Apply();
        }

        private async Task ExecuteSignalFromCardSafeAsync(Panel card)
        {
            if (card.Tag is not SignalCardInfo info) return;
            string executionKey = !string.IsNullOrWhiteSpace(info.SignalId)
                ? info.SignalId
                : info.FilePath;

            SetCardBusy(card, true);
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
                SetCardBusy(card, false);
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

            SetCardBusy(card, true);
            try
            {
                bool ok = await _bridge.CloseTradeAsync(info.Ticket).ConfigureAwait(false);
                Log(ok ? $"[OK] Closed #{info.Ticket}" : $"[ERROR] Failed to close #{info.Ticket}",
                    ok ? C_GREEN : C_RED);

                if (ok)
                {
                    var updated = info with { Status = SignalCardStatus.Executed, StatusText = $"#{info.Ticket} closed", Ticket = 0, Time = DateTime.Now };
                    if (InvokeRequired) Invoke(() => UpdateCardStatus(card, updated));
                    else UpdateCardStatus(card, updated);
                }
                await RefreshPositionsAsync().ConfigureAwait(false);
            }
            finally
            {
                SetCardBusy(card, false);
            }
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

        private async void BtnStartClaude_Click(object? sender, EventArgs e)   => await StartClaudeAsync();
        private async void BtnStopClaude_Click(object? sender, EventArgs e)    => await StopClaudeAsync();
        private async void BtnTestClaudeApi_Click(object? sender, EventArgs e) => await TestClaudeApiAsync();
        private async void BtnTestNewsApi_Click(object? sender, EventArgs e)   => await TestNewsApiConfigAsync();
        private async void BtnTestTelegram_Click(object? sender, EventArgs e)  => await TestTelegramConfigAsync();
        private void BtnResetPrompt_Click(object? sender, EventArgs e)         => _txtClaudePrompt.Text = ClaudeConfig.DefaultPrompt;

        private void BtnClearLog_Click(object? sender, EventArgs e) => _txtLog.Clear();
        private void BtnSaveLog_Click(object? sender, EventArgs e)
        {
            using var d = new SaveFileDialog { Filter = "Text|*.txt", FileName = "MT5Log" };
            if (d.ShowDialog() == DialogResult.OK) File.WriteAllText(d.FileName, _txtLog.Text);
        }

        // ==========================================================
        //  PAIR SELECTION — shared flow for manual & AI selection
        // ==========================================================

        private async Task OnPairSelectionChangedAsync(string pair)
        {
            var card = EnsureSignalFeedRowForPair(pair);

            bool aiReady = !string.IsNullOrWhiteSpace(_cfg.Claude?.ApiKey)
                        && !_cfg.Claude.ApiKey.StartsWith("sk-ant-..")
                        && _cfg.Claude.ApiKey.Length > 20;

            if (aiReady)
                await RunDecisionAnalysisForPairAsync(pair, card).ConfigureAwait(false);
        }

        private Panel EnsureSignalFeedRowForPair(string pair)
        {
            if (InvokeRequired)
                return (Panel)Invoke(() => EnsureSignalFeedRowForPair(pair))!;

            if (_pairAnalysisCards.TryGetValue(pair, out var existing))
            {
                if (existing.Tag is PairAnalysisInfo info)
                {
                    info.Status      = "Selected";
                    info.ShortReason = "Pair selected";
                    info.LastUpdated = DateTime.Now;
                    UpdatePairAnalysisCard(existing, info);
                }
                _flpSignals.ScrollControlIntoView(existing);
                Log($"[BOT] Signal row updated for {pair}", C_ACCENT);
                return existing;
            }

            var newInfo = new PairAnalysisInfo
            {
                Pair        = pair,
                Direction   = "NONE",
                Confidence  = "-",
                Status      = "Waiting for Analysis",
                LastUpdated = DateTime.Now,
                ShortReason = "Pair selected"
            };

            var card = BuildPairAnalysisCard(newInfo);
            _pairAnalysisCards[pair] = card;

            _flpSignals.SuspendLayout();
            _flpSignals.Controls.Add(card);
            _flpSignals.ResumeLayout(true);

            Log($"[BOT] Signal row created for {pair}", C_ACCENT);
            return card;
        }

        private Panel BuildPairAnalysisCard(PairAnalysisInfo info)
        {
            int w = Math.Max(200, _flpSignals.ClientSize.Width - _flpSignals.Padding.Horizontal - 4);

            var card = new Panel
            {
                Width     = w,
                Height    = 130,
                BackColor = Color.FromArgb(16, 18, 34),
                Margin    = new Padding(0, 0, 0, 5),
                Tag       = info
            };

            // Left purple stripe — distinguishes pair analysis rows from file-based signal cards
            card.Controls.Add(new Panel { Width = 5, Dock = DockStyle.Left, BackColor = Color.FromArgb(130, 100, 255) });

            card.Controls.Add(new Label
            {
                Name      = "lblPairHeader",
                Text      = $"📊  {info.Pair}",
                Font      = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(180, 160, 255),
                Location  = new Point(14, 8),
                AutoSize  = true
            });

            var btnRemove = MakeCardButton("✕", Color.FromArgb(80, 30, 30), Color.FromArgb(252, 95, 95), "Remove this row");
            btnRemove.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
            btnRemove.Location = new Point(w - 28, 8);
            btnRemove.Click   += (_, _) =>
            {
                _pairAnalysisCards.Remove(info.Pair);
                _flpSignals.Controls.Remove(card);
            };
            card.Controls.Add(btnRemove);

            card.Controls.Add(new Label
            {
                Name      = "lblDirConf",
                Text      = $"Direction: {info.Direction}   Confidence: {info.Confidence}",
                Font      = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(160, 200, 255),
                Location  = new Point(14, 33),
                AutoSize  = true
            });

            card.Controls.Add(new Label
            {
                Name      = "lblPrices",
                Text      = FormatPairPrices(info),
                Font      = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(140, 160, 200),
                Location  = new Point(14, 55),
                AutoSize  = true
            });

            card.Controls.Add(new Label
            {
                Name      = "lblPairStatus",
                Text      = info.Status,
                Font      = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
                ForeColor = GetPairStatusColor(info.Status),
                Location  = new Point(14, 77),
                AutoSize  = true
            });

            card.Controls.Add(new Label
            {
                Name      = "lblPairMeta",
                Text      = $"{info.LastUpdated:HH:mm:ss}  {info.ShortReason}",
                Font      = new Font("Segoe UI", 8F),
                ForeColor = Color.FromArgb(90, 100, 130),
                Location  = new Point(14, 99),
                AutoSize  = true
            });

            return card;
        }

        private void UpdatePairAnalysisCard(Panel card, PairAnalysisInfo info)
        {
            if (InvokeRequired) { Invoke(() => UpdatePairAnalysisCard(card, info)); return; }

            card.Tag = info;
            foreach (Control c in card.Controls)
            {
                switch (c.Name)
                {
                    case "lblPairHeader":
                        string icon = info.Direction switch { "BUY" => "▲", "SELL" => "▼", _ => "📊" };
                        c.Text      = $"{icon}  {info.Pair}";
                        c.ForeColor = info.Direction switch
                        {
                            "BUY"  => Color.FromArgb(99,  200, 140),
                            "SELL" => Color.FromArgb(220, 140, 255),
                            _      => Color.FromArgb(180, 160, 255)
                        };
                        break;
                    case "lblDirConf":
                        c.Text = $"Direction: {info.Direction}   Confidence: {info.Confidence}";
                        break;
                    case "lblPrices":
                        c.Text = FormatPairPrices(info);
                        break;
                    case "lblPairStatus":
                        c.Text      = info.Status;
                        c.ForeColor = GetPairStatusColor(info.Status);
                        break;
                    case "lblPairMeta":
                        c.Text = $"{info.LastUpdated:HH:mm:ss}  {info.ShortReason}";
                        break;
                }
            }
        }

        private static string FormatPairPrices(PairAnalysisInfo info) =>
            info.Entry == 0
                ? "Entry: -   SL: -   TP: -   RR: -"
                : $"Entry: {info.Entry:F5}   SL: {info.StopLoss:F5}   TP: {info.TakeProfit:F5}   RR: {info.RR:F2}";

        private static Color GetPairStatusColor(string status) => status switch
        {
            "Waiting for Analysis" => Color.FromArgb(150, 140, 80),
            "Selected"             => Color.FromArgb(130, 100, 255),
            "AI Selected"          => Color.FromArgb(100, 180, 255),
            "Analyzing"            => Color.FromArgb(80,  160, 255),
            "BUY"                  => Color.FromArgb(72,  199, 142),
            "SELL"                 => Color.FromArgb(214, 164, 255),
            "WAIT"                 => Color.FromArgb(200, 180,  80),
            "No Trade"             => Color.FromArgb(160, 100, 100),
            "Analysis Error" or "No suitable pair found" => Color.FromArgb(220, 80, 80),
            _                      => Color.FromArgb(150, 155, 185)
        };

        private string? FindDropdownPair(string aiPair)
        {
            if (string.IsNullOrWhiteSpace(aiPair)) return null;
            var items = _clbAllowedPairs.Items.Cast<string>().ToList();

            // 1. Exact match (case-insensitive)
            var exact = items.FirstOrDefault(i => string.Equals(i, aiPair, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;

            // 2. Dropdown item starts with aiPair (broker suffix appended, e.g. GBPUSDm)
            var withSuffix = items.FirstOrDefault(i =>
                i.StartsWith(aiPair, StringComparison.OrdinalIgnoreCase));
            if (withSuffix != null) return withSuffix;

            // 3. aiPair starts with a dropdown item (AI returned a longer normalised name)
            return items.FirstOrDefault(i =>
                aiPair.StartsWith(i, StringComparison.OrdinalIgnoreCase));
        }

        private void ProgrammaticallyCheckPair(string pair)
        {
            for (int i = 0; i < _clbAllowedPairs.Items.Count; i++)
            {
                if (string.Equals(_clbAllowedPairs.Items[i]?.ToString(), pair, StringComparison.OrdinalIgnoreCase))
                {
                    _clbAllowedPairs.SetItemChecked(i, true);
                    break;
                }
            }
        }

        private async Task<(string Pair, string Confidence, string Direction, string Reason, string Error)>
            RunAiPairSelectionAsync(IReadOnlyList<PairScanResult> scanResults)
        {
            try
            {
                var pairsPayload = scanResults.Select(r => new
                {
                    pair        = r.Pair,
                    available   = r.IsAvailable,
                    spread_pips = Math.Round(r.SpreadPips, 2),
                    score       = Math.Round(r.Score, 1),
                    reason      = r.Reason
                });

                string comparisonJson = JsonConvert.SerializeObject(new
                {
                    timestamp       = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC"),
                    available_pairs = scanResults.Where(r => r.IsAvailable).Select(r => r.Pair).ToList(),
                    pairs           = pairsPayload
                }, Formatting.Indented);

                var client = new Anthropic.AnthropicClient { ApiKey = _cfg.Claude.ApiKey };
                var response = await client.Messages.Create(
                    new Anthropic.Models.Messages.MessageCreateParams
                    {
                        Model     = _cfg.Claude.Model,
                        MaxTokens = 1024,
                        System    = new List<Anthropic.Models.Messages.TextBlockParam>
                        {
                            new() { Text         = AiPairSelectionSystemPrompt,
                                    CacheControl = new Anthropic.Models.Messages.CacheControlEphemeral() }
                        },
                        Messages  =
                        [
                            new() { Role = Anthropic.Models.Messages.Role.User, Content = comparisonJson }
                        ]
                    }).ConfigureAwait(false);

                string? text = null;
                foreach (var block in response.Content)
                    if (block.TryPickText(out var tb)) { text = tb!.Text; break; }

                if (string.IsNullOrWhiteSpace(text))
                    return ("", "-", "NO_TRADE", "", "AI returned no text");

                int start = text.IndexOf('{'), end = text.LastIndexOf('}');
                if (start < 0 || end <= start)
                    return ("", "-", "NO_TRADE", "", "AI returned invalid pair-selection JSON.");

                var sig = JsonConvert.DeserializeObject<AiPairSelectionResult>(text[start..(end + 1)]);
                if (sig == null)
                    return ("", "-", "NO_TRADE", "", "AI returned invalid pair-selection JSON.");

                if (string.IsNullOrEmpty(sig.SelectedPair) || sig.RecommendedDirection == "NO_TRADE")
                    return ("", sig.Confidence ?? "-", "NO_TRADE",
                            sig.Reason ?? "No suitable pair found", "");

                return (sig.SelectedPair,
                        sig.Confidence ?? "-",
                        sig.RecommendedDirection ?? "NONE",
                        sig.Reason ?? "",
                        "");
            }
            catch (Exception ex)
            {
                return ("", "-", "NO_TRADE", "", CategorizeApiError(ex));
            }
        }

        private async Task RunDecisionAnalysisForPairAsync(string pair, Panel card)
        {
            if (_bridge?.IsConnected != true || card.Tag is not PairAnalysisInfo info) return;

            info.Status      = "Analyzing";
            info.LastUpdated = DateTime.Now;
            UpdatePairAnalysisCard(card, info);

            try
            {
                var account   = await _bridge.GetAccountInfoAsync().ConfigureAwait(false);
                var symInfo   = await _bridge.GetSymbolInfoAsync(pair).ConfigureAwait(false);
                var positions = await _bridge.GetPositionsAsync().ConfigureAwait(false);

                if (account == null || symInfo == null)
                {
                    info.Status      = "Data Unavailable";
                    info.ShortReason = "Cannot fetch MT5 data";
                    info.LastUpdated = DateTime.Now;
                    UpdatePairAnalysisCard(card, info);
                    return;
                }

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"=== SINGLE PAIR ANALYSIS — {pair} — {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC ===");
                sb.AppendLine($"Account: Balance=${account.Balance:F2}  Equity=${account.Equity:F2}  Free Margin=${account.FreeMargin:F2}");
                sb.AppendLine($"Symbol:  Ask={symInfo.Ask:F5}  Bid={symInfo.Bid:F5}  Spread={symInfo.SpreadPips:F1} pips");
                sb.AppendLine($"Open Positions: {positions.Count}");
                string pairBase = pair.Length >= 6 ? pair[..6] : pair;
                foreach (var p in positions.Where(p =>
                    p.Symbol.StartsWith(pairBase, StringComparison.OrdinalIgnoreCase)))
                    sb.AppendLine($"  #{p.Ticket} {p.Type} {p.Lots:F2}L @ {p.OpenPrice:F5}  P&L=${p.Profit:F2}");
                sb.AppendLine("Provide your trading decision as a JSON object for this specific pair.");

                var client = new Anthropic.AnthropicClient { ApiKey = _cfg.Claude.ApiKey };
                var response = await client.Messages.Create(
                    new Anthropic.Models.Messages.MessageCreateParams
                    {
                        Model     = _cfg.Claude.Model,
                        MaxTokens = 4096,
                        System    = new List<Anthropic.Models.Messages.TextBlockParam>
                        {
                            new() { Text         = _cfg.Claude.SystemPrompt,
                                    CacheControl = new Anthropic.Models.Messages.CacheControlEphemeral() }
                        },
                        Messages  =
                        [
                            new() { Role = Anthropic.Models.Messages.Role.User, Content = sb.ToString() }
                        ]
                    }).ConfigureAwait(false);

                string? text = null;
                foreach (var block in response.Content)
                    if (block.TryPickText(out var tb)) { text = tb!.Text; break; }

                if (string.IsNullOrWhiteSpace(text))
                {
                    info.Status = "No Decision"; info.ShortReason = "AI returned no text";
                    info.LastUpdated = DateTime.Now;
                    UpdatePairAnalysisCard(card, info);
                    return;
                }

                int s = text.IndexOf('{'), e = text.LastIndexOf('}');
                if (s < 0 || e <= s)
                {
                    info.Status = "Invalid Response"; info.ShortReason = "No JSON in AI response";
                    info.LastUpdated = DateTime.Now;
                    UpdatePairAnalysisCard(card, info);
                    return;
                }

                var jo     = JObject.Parse(text[s..(e + 1)]);
                string action = (jo["action"]?.ToString() ?? "").ToUpperInvariant();

                if (action == "NO_TRADE")
                {
                    info.Direction   = "NONE";
                    info.Status      = "No Trade";
                    info.ShortReason = jo["reason"]?.ToString() ?? "AI: no trade";
                    info.LastUpdated = DateTime.Now;
                }
                else if (action == "TRADE")
                {
                    string dir  = (jo["trade_type"]?.ToString() ?? "NONE").ToUpperInvariant();
                    double entry = jo["entry_price"]?.Value<double>() ?? 0;
                    double sl    = jo["stop_loss"]?.Value<double>() ?? 0;
                    double tp    = jo["take_profit"]?.Value<double>() ?? 0;
                    double mid   = entry > 0 ? entry : (symInfo.Ask + symInfo.Bid) / 2.0;
                    double rr    = sl > 0 && tp > 0 && mid > 0
                                   ? Math.Round(Math.Abs(tp - mid) / Math.Abs(sl - mid), 2) : 0;

                    info.Direction   = dir;
                    info.Entry       = entry;
                    info.StopLoss    = sl;
                    info.TakeProfit  = tp;
                    info.RR          = rr;
                    info.Status      = dir;
                    info.ShortReason = jo["comment"]?.ToString() ?? "AI signal";
                    info.LastUpdated = DateTime.Now;
                }
                else
                {
                    info.Status      = "WAIT";
                    info.Direction   = "NONE";
                    info.ShortReason = jo["reason"]?.ToString() ?? "AI: wait";
                    info.LastUpdated = DateTime.Now;
                }

                UpdatePairAnalysisCard(card, info);
                Log($"[BOT] Decision for {pair}: {info.Status} | {info.ShortReason}", C_GREEN);
            }
            catch (Exception ex)
            {
                if (card.Tag is PairAnalysisInfo i)
                {
                    i.Status      = "Analysis Error";
                    i.ShortReason = ex.Message.Length > 60 ? ex.Message[..60] : ex.Message;
                    i.LastUpdated = DateTime.Now;
                    UpdatePairAnalysisCard(card, i);
                }
                Log($"[BOT] Decision analysis failed for {pair}: {ex.Message}", C_RED);
            }
        }

        private const string AiPairSelectionSystemPrompt = """
            You are an FX pair selector. Given live spread and score data for multiple symbols, select
            the single best pair to trade right now. Only pick from pairs where available = true.
            Return ONLY a valid JSON object — no markdown, no explanatory text outside the JSON.

            Output format:
            {
              "selected_pair": "GBPUSD",
              "confidence": "HIGH",
              "selection_score": 85,
              "recommended_direction": "BUY",
              "reason": "Tight spread, strong momentum",
              "warnings": [],
              "ranked_pairs": [
                {"pair": "GBPUSD", "score": 85, "recommended_direction": "BUY", "reason": "..."}
              ]
            }

            If no suitable pair exists:
            {
              "selected_pair": "",
              "confidence": "LOW",
              "selection_score": 0,
              "recommended_direction": "NO_TRADE",
              "reason": "No suitable pair found",
              "warnings": [],
              "ranked_pairs": []
            }

            Rules:
            - confidence must be one of: LOW, MEDIUM, HIGH, VERY_HIGH
            - recommended_direction must be one of: BUY, SELL, WAIT, NO_TRADE
            - selected_pair must exactly match one entry from the available_pairs list
            """;

        // ── Inner data classes ────────────────────────────────────────────

        internal sealed class PairAnalysisInfo
        {
            public string   Pair        { get; set; } = "";
            public string   Direction   { get; set; } = "NONE";
            public string   Confidence  { get; set; } = "-";
            public double   Entry       { get; set; } = 0;
            public double   StopLoss    { get; set; } = 0;
            public double   TakeProfit  { get; set; } = 0;
            public double   RR          { get; set; } = 0;
            public string   Status      { get; set; } = "Waiting for Analysis";
            public DateTime LastUpdated { get; set; } = DateTime.Now;
            public string   ShortReason { get; set; } = "Pair selected";
        }

        internal sealed class AiPairSelectionResult
        {
            [JsonProperty("selected_pair")]        public string? SelectedPair        { get; set; }
            [JsonProperty("confidence")]           public string? Confidence          { get; set; }
            [JsonProperty("selection_score")]      public double  SelectionScore      { get; set; }
            [JsonProperty("recommended_direction")] public string? RecommendedDirection { get; set; }
            [JsonProperty("reason")]               public string? Reason              { get; set; }
        }
    }
}

