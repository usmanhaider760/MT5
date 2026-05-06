using Microsoft.Extensions.DependencyInjection;
using MT5TradingBot.Core;
using MT5TradingBot.Data;
using MT5TradingBot.Models;
using MT5TradingBot.Modules.Backtesting;
using MT5TradingBot.Modules.BrokerIntegration;
using MT5TradingBot.Modules.MarketData;
using MT5TradingBot.Modules.PairScanner;
using MT5TradingBot.Modules.PairSettings;
using MT5TradingBot.Modules.NewsFilter;
using MT5TradingBot.Modules.NormalTrading;
using MT5TradingBot.Modules.RiskManagement;
using MT5TradingBot.Modules.Scalping;
using MT5TradingBot.Modules.TradeRules;
using MT5TradingBot.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace MT5TradingBot.UI
{
    public sealed partial class MainForm : Form
    {
        // -- Services ----------------------------------------------
        private MT5Bridge? _bridge;
        private AutoBotService? _bot;
        private ClaudeSignalService? _claude;
        private readonly IServiceProvider _services;
        private readonly SettingsManager _settings;
        private PairSettingsService? _pairSettings;
        private readonly INewsCalendarService _newsCalendar;
        private readonly ITradeRepository? _tradeDb;
        private AppSettings _cfg = new();
        private bool _warnedZeroAccountValues;
        private bool _shownEaDeployNotice;
        private DateTime _lastEaStatusBadgeRefreshUtc = DateTime.MinValue;
        private readonly ToolTip _cardTooltip = new() { InitialDelay = 400, ShowAlways = true };
        private readonly object _signalExecutionLock = new();
        private readonly HashSet<string> _executingSignalIds = [];
        private readonly Dictionary<long, AutoCloseTarget> _autoCloseTargets = [];
        private readonly HashSet<long> _autoCloseInProgress = [];
        private bool _syncingAutoCloseValues;
        private IScalpingSessionService? _scalping;
        private readonly ScalpingTradeManager _scalpingTradeManager = new();
        private readonly NormalTradeManager _normalTradeManager = new();
        private readonly Button _btnStopScalping = new();
        private readonly Button _btnScalpingRules = new();
        private readonly Button _btnNormalRules = new();
        private const int MaxScreenLogLines = 500;
        private const int MaxScreenLogChars = 180;
        private readonly List<string> _screenLogFullMessages = [];
        private int _lastLogContextLineIndex = -1;
        private readonly Dictionary<string, IReadOnlyList<TradeRuleAuditSnapshot>> _tradeRuleAuditsByRequestId = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<long, IReadOnlyList<TradeRuleAuditSnapshot>> _tradeRuleAuditsByTicket = [];
        private readonly Dictionary<long, IReadOnlyList<TradeLifecycleAuditRecord>> _tradeLifecycleAuditsByTicket = [];

        // -- Pair analysis feed ------------------------------------
        private readonly Dictionary<string, Panel> _pairAnalysisCards = new(StringComparer.OrdinalIgnoreCase);
        private bool _suppressPairSelectionEvent;
        private string _activeWatchFolder = "";
        private FileSystemWatcher? _signalFeedWatcher;
        private readonly System.Windows.Forms.Timer _signalFeedPollTimer = new() { Interval = 2500 };
        private Action<TradeRequest>? _reviewSignalPush;

        // -- Timers ------------------------------------------------
        private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = 2500 };
        private MarketDataAutoSyncService? _marketDataSync;
        private MT5Bridge? _marketDataBridge;
        private readonly Panel _pnlMarketDataSync = new();
        private readonly Label _lblMarketDataSync = new();
        private readonly ProgressBar _pbMarketDataSync = new();

        // -- Pair Settings tab ------------------------------------
        private readonly TabPage _tabPairSettings = new() { Text = "  Pair Settings  ", Name = "_tabPairSettings" };
        private readonly DataGridView _gridPairSettings = new();
        private readonly Button _btnPairAdd = new();
        private readonly Button _btnPairEdit = new();
        private readonly Button _btnPairDelete = new();
        private readonly Button _btnPairImport = new();

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
            double TargetMoney,
            double LotSize   = 0,
            int    Leverage  = 100,
            TradeRequest? FinalRequest = null,
            bool AutoScalpingEnabled = false,
            ScalpingConfig? ScalpingConfig = null,
            bool NormalTradingEnabled = false,
            NormalTradingSettings? NormalTradingSettings = null,
            CommonTradingSettings? CommonTradingSettings = null);

        private sealed record TradeHistoryDetail(
            Mt5TradeHistoryItem Trade,
            IReadOnlyList<TradeRuleAuditSnapshot> RuleAudit,
            IReadOnlyList<TradeLifecycleAuditRecord> LifecycleAudit);

        // ==========================================================
        // Keep for WinForms designer compatibility
        public MainForm() : this(BuildFallbackProvider()) { }

        // Primary constructor used at runtime via DI
        public MainForm(IServiceProvider services)
        {
            _services = services;
            _settings = services.GetRequiredService<SettingsManager>();
            _newsCalendar = services.GetRequiredService<INewsCalendarService>();
            _tradeDb = services.GetRequiredService<ITradeRepository>();

            InitializeComponent();
            AppIcon.ApplyTo(this);
            ApplyStableLayout();
            EnsureMarketDataSyncProgressArea();

            if (!IsDesignerHosted())
            {
                _tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
                _txtClaudePrompt.Text = ClaudeConfig.DefaultPrompt;
                EnsurePairSettingsTab();
                EnsureBacktestTab();
                EnsurePerformanceTab();
                WireEvents();
                _settings.StartWatching();
                _settings.SettingsReloaded += OnSettingsHotReloaded;
                _clockTimer.Start();
                _ = InitAsync();
            }
        }

        // Fallback: builds a minimal provider for design-time / unit-test use
        private static IServiceProvider BuildFallbackProvider()
        {
            var sc = new ServiceCollection();
            sc.AddSingleton<SettingsManager>();
            sc.AddSingleton<INewsCalendarService, FmpNewsCalendarService>();
            sc.AddSingleton<IRiskManager, RiskManager>();
            sc.AddSingleton<IAiContextManager, AiContextManager>();
            sc.AddSingleton<ITradeRepository>(_ =>
            {
                return new SqliteTradeRepository(AppPaths.PrepareTradesDatabaseFile());
            });
            return sc.BuildServiceProvider();
        }

        private bool IsDesignerHosted() =>
            DesignMode || LicenseManager.UsageMode == LicenseUsageMode.Designtime;

        private async Task InitAsync()
        {
            await _settings.LoadAsync();
            _cfg = _settings.Current;
            _pairSettings = new PairSettingsService(_settings, _cfg);
            ApplySettingsToUI();

            if (_cfg.AutoConnectOnLaunch)
                await ConnectAsync();

            Log(_bridge?.IsConnected == true
                ? "MT5 Trading Bot ready. MT5 is connected."
                : "MT5 Trading Bot ready. Connect to MT5 to begin.", C_ACCENT);
            UpdateEaDeploymentStatusBadge();
            ShowEaDeployNoticeIfNeeded();
            StartMarketDataAutoSyncIfEnabled();
            await RefreshSignalFeedAsync();
            await EnsureAutoWatcherAsync("form load");
        }

        private AutoBotService CreateBot()
        {
            var bridge = _bridge ?? throw new InvalidOperationException("MT5 bridge is not connected.");
            var bot = new AutoBotService(
                bridge,
                _cfg.Bot,
                _pairSettings,
                _newsCalendar,
                _cfg.ApiIntegrations,
                riskManager:     new RiskManager(_pairSettings),
                tradeRepository: _tradeDb);

            bot.OnLog += msg => Log(msg);
            bot.OnTradeExecuted += r =>
            {
                CaptureExecutionRuleAudit(null, r);
                Log(r.IsSuccess
                        ? $"[BOT] Trade | Rule=EXEC-TRADE-ACCEPTED Trade Accepted | {r}"
                        : $"[BOT] Rejected | MainRule={ResolveRejectedRuleForLog(r.ErrorCode, r.ErrorMessage)} | Reason={r.ErrorMessage}",
                    r.IsSuccess ? C_GREEN : C_RED);
                _ = RefreshBotTradeStatusAsync(r);
            };
            bot.OnBotStatusChanged += on => UpdateBotBadge(on);
            bot.OnEdgeStatusChanged += status => UIThread(() =>
            {
                if (_bot?.IsRunning != true) return;
                _lblBotBadge.Text = status.IsDegraded
                    ? $"BOT PAUSED (EDGE) - WR {status.WinRatePct:F1}%"
                    : $"BOT MONITORING - WR {status.WinRatePct:F1}%";
                _lblBotBadge.ForeColor = status.IsDegraded ? Color.Orange : C_ACCENT;
            });
            bot.OnSignalUpdate += info => AddOrUpdateSignalCard(info);

            return bot;
        }

        private static string ResolveRejectedRuleForLog(string? errorCode, string? reason)
        {
            string code = errorCode ?? "";
            string text = $"{errorCode} {reason}";

            if (text.Contains("NEWS", StringComparison.OrdinalIgnoreCase))
                return "SAFETY-NEWS-BLACKOUT News Blackout Filter";
            if (text.Contains("BROKER_STOP", StringComparison.OrdinalIgnoreCase) || text.Contains("stop-level", StringComparison.OrdinalIgnoreCase))
                return "BROKER-STOP-LEVEL Broker Stop Level";
            if (text.Contains("BROKER_FREEZE", StringComparison.OrdinalIgnoreCase) || text.Contains("freeze", StringComparison.OrdinalIgnoreCase))
                return "BROKER-FREEZE-LEVEL Broker Freeze Level";
            if (text.Contains("BROKER_LOT", StringComparison.OrdinalIgnoreCase) || text.Contains("lot", StringComparison.OrdinalIgnoreCase))
                return "BROKER-LOT-SIZE Broker Lot Size";
            if (text.Contains("ORDER_CHECK", StringComparison.OrdinalIgnoreCase))
                return "BROKER-ORDER-CHECK Broker OrderCheck";
            if (text.Contains("DAILY", StringComparison.OrdinalIgnoreCase))
                return "ACCOUNT-DAILY-LOSS Daily Loss Limit";
            if (text.Contains("WEEKLY", StringComparison.OrdinalIgnoreCase))
                return "ACCOUNT-WEEKLY-LOSS Weekly Loss Limit";
            if (text.Contains("MARGIN", StringComparison.OrdinalIgnoreCase))
                return "ACCOUNT-MARGIN Projected Margin Validation";
            if (text.Contains("SYMBOL_EXPOSURE", StringComparison.OrdinalIgnoreCase))
                return "ACCOUNT-SYMBOL-EXPOSURE Same Symbol Exposure";
            if (text.Contains("NO_TRADE_WINDOW", StringComparison.OrdinalIgnoreCase) || text.Contains("ROLLOVER", StringComparison.OrdinalIgnoreCase))
                return "SAFETY-NO-TRADE-WINDOW No-Trade Window";

            return string.IsNullOrWhiteSpace(code)
                ? "EXEC-TRADE-REJECTED Trade Rejected"
                : $"EXEC-TRADE-REJECTED Trade Rejected ({code})";
        }

        private ClaudeSignalService CreateClaude()
        {
            var bridge = _bridge ?? throw new InvalidOperationException("MT5 bridge is not connected.");
            var svc = new ClaudeSignalService(
                bridge,
                _cfg.Claude,
                _cfg.Bot,
                ExecuteClaudeTradeWithAuditAsync,
                contextManager: _services.GetRequiredService<IAiContextManager>());

            svc.OnLog += msg => Log($"[AI] {msg}");
            svc.OnSignalGenerated += req =>
            {
                Log($"[AI] Signal: {req}", C_ACCENT);
                _reviewSignalPush?.Invoke(req);
            };
            svc.OnStatusChanged += on => UpdateClaudeBadge(on);

            return svc;
        }

        private async Task<TradeResult> ExecuteClaudeTradeWithAuditAsync(TradeRequest req)
        {
            if (_bot == null)
                return RejectWithoutExecutionGate(req, "AI execution gate is not ready.");

            TradeResult result = await _bot.ExecuteTradeWithValidationAsync(req).ConfigureAwait(false);
            CaptureExecutionRuleAudit(req, result);
            return result;
        }

        // ==========================================================
        //  WIRE EVENTS - named handlers only, no lambdas
        // ==========================================================
        private void WireEvents()
        {
            _clockTimer.Tick    += ClockTimer_Tick;
            _refreshTimer.Tick  += RefreshTimer_Tick;
            _signalFeedPollTimer.Tick += SignalFeedPollTimer_Tick;
            this.FormClosing    += OnFormClosingAsync;
            _tabControl.DrawItem += DrawTabItem;
            _tabControl.SelectedIndexChanged += TabControl_SelectedIndexChanged;

            _btnConnect.Click           += BtnConnect_Click;
            _btnDisconnect.Click        += BtnDisconnect_Click;
            _chkAutoConn.CheckedChanged += ChkAutoConn_CheckedChanged;
            _lblEaStatus.Click          += async (_, _) => await RefreshEaStatusButtonAsync(logResult: true);

            _btnClosePos.Click    += BtnClosePos_Click;
            _btnCloseAllPos.Click += BtnCloseAllPos_Click;
            _btnRefreshPos.Click  += BtnRefreshPos_Click;
            _gridPos.CellMouseDown += GridPos_CellMouseDown;

            _btnImportHistory.Click += BtnImportHistory_Click;
            _btnClearHistory.Click  += BtnClearHistory_Click;
            _gridHistory.CellDoubleClick += GridHistory_CellDoubleClick;

            _cmbAllowedPair.SelectedIndexChanged += CmbAllowedPair_SelectedIndexChanged;

            _btnStopBot.Click         += BtnStopBot_Click;
            _btnStopScalping.Click    += BtnStopScalping_Click;
            _btnScalpingRules.Click   += (_, _) => OpenRulesMonitor(BuildPanelRulesContext(TradeRulesStrategy.Scalping, "ScalpingPanel"));
            _btnNormalRules.Click     += (_, _) => OpenRulesMonitor(BuildPanelRulesContext(TradeRulesStrategy.Normal, "NormalPanel"));
            _btnBotSettings.Click     += BtnBotSettings_Click;
            _btnAnalyzePairs.Click    += BtnAnalyzePairs_Click;
            _btnOpenFolder.Click      += BtnOpenFolder_Click;
            _btnBotInstructions.Click += BtnBotInstructions_Click;

            _btnStartClaude.Click    += BtnStartClaude_Click;
            _btnStopClaude.Click     += BtnStopClaude_Click;
            _btnTestClaudeApi.Click  += BtnTestClaudeApi_Click;
            _btnTestNewsApi.Click    += BtnTestNewsApi_Click;
            _btnTestTelegram.Click   += BtnTestTelegram_Click;

            _btnClearLog.Click += BtnClearLog_Click;
            _btnLogDetails.Click += BtnLogDetails_Click;
            _btnSaveLog.Click  += BtnSaveLog_Click;
            _btnOpenLogFile.Click += BtnOpenLogFile_Click;
            _btnOpenTradeLogFile.Click += BtnOpenTradeLogFile_Click;
            _btnDeleteLogs.Click += BtnDeleteLogs_Click;
            _txtLog.MouseDown += TxtLog_MouseDown;
            _txtLog.DoubleClick += TxtLog_DoubleClick;
            ConfigureRulesMonitorContextMenus();
            _cardTooltip.SetToolTip(_btnLogDetails, "Select or double-click a log line to see why the bot traded, waited, or blocked it.");
            _cardTooltip.SetToolTip(_btnOpenLogFile, "Open the full regular bot diagnostic log for this app session.");
            _cardTooltip.SetToolTip(_btnOpenTradeLogFile, "Open the focused trade log with placed, rejected, closed, and execution-quality events.");

            _btnPairAdd.Click += BtnPairAdd_Click;
            _btnPairEdit.Click += BtnPairEdit_Click;
            _btnPairDelete.Click += BtnPairDelete_Click;
            _btnPairImport.Click += BtnPairImport_Click;
            _gridPairSettings.CellDoubleClick += GridPairSettings_CellDoubleClick;
        }

        private void GridPos_CellMouseDown(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right && e.RowIndex >= 0)
            {
                _gridPos.ClearSelection();
                _gridPos.Rows[e.RowIndex].Selected = true;
                if (e.ColumnIndex >= 0)
                    _gridPos.CurrentCell = _gridPos.Rows[e.RowIndex].Cells[e.ColumnIndex];
            }
        }

        private void TxtLog_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right || _txtLog.TextLength == 0)
                return;

            int charIndex = _txtLog.GetCharIndexFromPosition(e.Location);
            _txtLog.SelectionStart = Math.Clamp(charIndex, 0, _txtLog.TextLength);
            _txtLog.SelectionLength = 0;
            _lastLogContextLineIndex = ResolveNearestLogLineIndex(_txtLog.GetLineFromCharIndex(_txtLog.SelectionStart));
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
                UpdateEaDeploymentStatusBadge(force: true);
                ShowEaDeployNoticeIfNeeded();
                await EnsureAutoWatcherAsync("MT5 connected");
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

            string pair = _cmbPair.SelectedItem?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(pair))
            {
                Log("[ERROR] No trading pair configured. Add a pair in the Pair Settings tab first.", C_RED);
                _tabControl.SelectedTab = _tabPairSettings;
                return;
            }

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
                Pair      = pair,
                TradeType = dir,
                OrderType = _cmbOrderType.SelectedIndex switch
                { 1 => OrderType.LIMIT, 2 => OrderType.STOP, _ => OrderType.MARKET },
                EntryPrice  = entry,
                StopLoss    = sl,
                TakeProfit  = tp,
                TakeProfit2 = tp2,
                LotSize     = lot,
                MoveSLToBreakevenAfterTP1 = _chkMoveSLBE.Checked,
                MagicNumber = _cfg.Bot.MagicNumber,
                Comment     = "Manual"
            };

            TradeResult result = await ExecuteThroughCentralGateAsync(req);

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

                TradeResult result = await ExecuteThroughCentralGateAsync(req);

                Log(result.IsSuccess ? $"[OK] {result}" : $"[ERROR] {result}", result.IsSuccess ? C_GREEN : C_RED);
                AddHistoryRow(req, result);
            }
            catch (JsonException ex) { Log($"[ERROR] JSON parse error: {ex.Message}", C_RED); }
        }

        private async Task<TradeResult> ExecuteThroughCentralGateAsync(TradeRequest req)
        {
            if (_bridge?.IsConnected != true)
                return RejectWithoutExecutionGate(req, "MT5 bridge is not connected.");

            _cfg.Bot = ReadBotConfigFromUISafe();
            _cfg.ApiIntegrations = ReadApiIntegrationConfigFromUI();

            _bot ??= CreateBot();
            _bot.UpdateConfig(_cfg.Bot);
            _bot.UpdateApiConfig(_cfg.ApiIntegrations);

            TradeResult result = await _bot.ExecuteTradeWithValidationAsync(req);
            CaptureExecutionRuleAudit(req, result);
            return result;
        }

        private static TradeResult RejectWithoutExecutionGate(TradeRequest req, string reason) => new()
        {
            RequestId = req.Id,
            Status = TradeStatus.Rejected,
            ErrorCode = "EXECUTION_GATE_UNAVAILABLE",
            ErrorMessage = reason,
            ExecutedAt = DateTime.UtcNow
        };

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
        private async Task EnsureAutoWatcherAsync(string reason)
        {
            try
            {
                _cfg.Bot = ReadBotConfigFromUI();
                string watchFolder = _cfg.Bot.WatchFolder.Trim();

                if (string.IsNullOrWhiteSpace(watchFolder))
                {
                    SetBotBadge("WATCH FOLDER NOT SET", C_YELLOW);
                    return;
                }

                Directory.CreateDirectory(watchFolder);
                EnsureSignalFeedWatcher(watchFolder);
                await _settings.SaveAsync(_cfg);
                await RefreshSignalFeedAsync();

                if (_bridge?.IsConnected != true)
                {
                    SetBotBadge($"WATCH FOLDER READY: {watchFolder}", C_YELLOW);
                    Log($"[BOT] Watch folder ready ({reason}), but MT5 is disconnected. Live watcher will start after MT5 connects.", C_YELLOW);
                    return;
                }

                if (_bot?.IsRunning == true &&
                    string.Equals(_activeWatchFolder, watchFolder, StringComparison.OrdinalIgnoreCase))
                {
                    SetBotBadge($"WATCHING: {watchFolder}", C_ACCENT);
                    return;
                }

                await (_bot?.DisposeAsync() ?? ValueTask.CompletedTask);
                _bot = CreateBot();

                await _bot.StartAsync();
                _activeWatchFolder = watchFolder;
                SetBotBadge($"WATCHING: {watchFolder}", C_ACCENT);
                Log($"[BOT] Auto watcher active ({reason}). New signal files appear immediately; trades still require row Detail/Play approval.", C_GREEN);
            }
            catch (Exception ex)
            {
                SetBotBadge("WATCHER START FAILED", C_RED);
                Log($"[BOT] Watcher start failed: {ex.Message}", C_RED);
            }
        }

        private async Task StartBotAsync()
        {
            try
            {
                SetBotBadge("CHECKING...", C_ACCENT);
                Log("[BOT] Checking requirements before monitoring...", C_ACCENT);
                bool allOk = true;

                // â"€â"€ 1. MT5 connection â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€
                if (_bridge?.IsConnected != true)
                {
                    Log("[BOT] [X] MT5 is not connected. Click Connect first.", C_RED);
                    SetBotBadge("BOT STOPPED - MT5 NOT CONNECTED", C_RED);
                    return;
                }
                Log("[BOT] [OK] MT5 is connected.", C_GREEN);

                // â"€â"€ 2. Watch folder â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€
                _cfg.Claude = ReadClaudeConfigFromUI();
                _cfg.ApiIntegrations = ReadApiIntegrationConfigFromUI();
                UpdateAiApiConfigStatus(_cfg.Claude, logResult: true);

                _cfg.Bot = ReadBotConfigFromUI();
                string watchFolder = _cfg.Bot.WatchFolder.Trim();
                if (string.IsNullOrWhiteSpace(watchFolder))
                {
                    Log("[BOT] [X] Watch folder is empty. Set a folder path first.", C_RED);
                    SetBotBadge("BOT STOPPED - WATCH FOLDER EMPTY", C_RED);
                    return;
                }
                Directory.CreateDirectory(watchFolder);
                Log($"[BOT] [OK] Watch folder: {watchFolder}", C_GREEN);

                // â"€â"€ 3. Account info â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€
                var account = await _bridge.GetAccountInfoAsync();
                if (account != null)
                {
                    Log($"[BOT] [OK] Account #{account.AccountNumber} {account.Server} | Balance ${account.Balance:F2} | Equity ${account.Equity:F2}", C_GREEN);
                    if (account.Balance == 0 && account.Equity == 0)
                    {
                        Log("[BOT] [!] Balance and Equity are 0. Ensure your MT5 account has funds.", C_YELLOW);
                        allOk = false;
                    }
                }
                else
                {
                    Log("[BOT] [!] Could not fetch account info from MT5.", C_YELLOW);
                    allOk = false;
                }

                // â"€â"€ 4. Open positions â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€
                var positions = await _bridge.GetPositionsAsync();
                Log($"[BOT] [OK] MT5 has {positions.Count} open position(s).", C_GREEN);

                // â"€â"€ 5. Pending signals â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€
                var pendingFiles = Directory.GetFiles(watchFolder, "*.json");
                if (pendingFiles.Length == 0)
                    Log("[BOT] [OK] Watch folder is empty - ready to receive signals.", C_GREEN);
                else
                    Log($"[BOT] [OK] {pendingFiles.Length} pending signal file(s) in folder.", C_ACCENT);

                // â"€â"€ 6. Config summary â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€
                Log($"[BOT] Settings -> Risk: {_cfg.Bot.MaxRiskPercent:F1}% | Scalping max trades: {_cfg.Bot.Scalping.MaxTrades} | Normal max trades: {_cfg.Bot.NormalTrading.MaxTrades}", C_ACCENT);
                int pairCount = _cfg.Bot.AllowedPairs.Count;
                string pairSummary = pairCount == 0 ? "All pairs"
                    : pairCount <= 5 ? string.Join(", ", _cfg.Bot.AllowedPairs)
                    : string.Join(", ", _cfg.Bot.AllowedPairs.Take(5)) + $" +{pairCount - 5} more";
                Log($"[BOT] Allowed pairs: {pairSummary}", C_ACCENT);

                if (!allOk)
                    Log("[BOT] [!] Some checks have warnings. Review above before trading.", C_YELLOW);

                // â"€â"€ 7. Start monitoring â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€
                Log("[BOT] Monitoring only: trades will NOT start from this button.", C_ACCENT);
                Log("[BOT] To place a trade, click the Play button on the signal row.", C_ACCENT);

                await _settings.SaveAsync(_cfg);
                await (_bot?.DisposeAsync() ?? ValueTask.CompletedTask);

                _bot = CreateBot();

                await _bot.StartAsync();
                Log("[BOT] Monitoring started - new signal files will appear in the feed below.", C_GREEN);
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
            if (_scalping != null)
            {
                await _scalping.StopAsync();
                _scalping = null;
                _scalpingTradeManager.Stop();
                UIThread(() => _btnStopScalping.Enabled = false);
            }
            if (_bot == null) return;
            await _bot.DisposeAsync();
            _bot = null;
            _activeWatchFolder = "";
            UpdateBotBadge(false);
        }

        private async Task StopScalpingAsync()
        {
            if (_scalping?.IsRunning != true)
            {
                _scalping = null;
                _scalpingTradeManager.Stop();
                Log("[SCALP] No scalping session is running.", C_YELLOW);
                UIThread(() => _btnStopScalping.Enabled = false);
                return;
            }

            await _scalping.StopAsync().ConfigureAwait(false);
            _scalping = null;
            _scalpingTradeManager.Stop();
            UIThread(() =>
            {
                _btnStopScalping.Enabled = false;
                if (_bot?.IsRunning == true)
                    UpdateBotBadge(true);
                else
                    SetBotBadge("SCALPING STOPPED", C_YELLOW);
            });
        }

        private async Task AnalyzePairsAsync()
        {
            if (_bridge?.IsConnected != true)
            {
                Log("[BOT] MT5 is not connected. Cannot analyze pairs.", C_RED);
                SetBotBadge("PAIR ANALYSIS NEEDS MT5", C_RED);
                return;
            }

            var allPairs = _cmbAllowedPair.Items.Cast<string>()
                .Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            if (allPairs.Count == 0)
            {
                Log("[BOT] No pairs configured. Add pairs in the Pair Settings tab first.", C_YELLOW);
                _tabControl.SelectedTab = _tabPairSettings;
                return;
            }

            try
            {
                _btnAnalyzePairs.Enabled = false;
                SetBotBadge("ANALYZING PAIRS...", C_ACCENT);
                Log($"[BOT] Analyze Pair clicked - scanning {allPairs.Count} pairs from Pair Settings...", C_ACCENT);

                _cfg.Bot = ReadBotConfigFromUI();
                await _settings.SaveAsync(_cfg);

                // Step 1: collect MT5 data for every pair in the list
                Log("[BOT] Pair list loaded - collecting MT5 data per pair...", C_ACCENT);
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
                        Log($"[BOT] AI: No suitable pair - {reason}", C_YELLOW);
                        SetBotBadge("AI: NO SUITABLE PAIR", C_YELLOW);
                    }
                    else
                    {
                        Log($"[BOT] AI best pair response: {aiPair} ({dir}, {conf}) - {reason}", C_GREEN);
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

                // Step 6: select the pair in dropdown (suppress event because we update the row below)
                _suppressPairSelectionEvent = true;
                ProgrammaticallySelectPair(dropdownPair);
                _suppressPairSelectionEvent = false;

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
            _bot?.UpdateApiConfig(_cfg.ApiIntegrations);
            await _settings.SaveAsync(_cfg);

            if (_claude != null) { await _claude.DisposeAsync(); _claude = null; }

            _claude = CreateClaude();

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
            _bot?.UpdateApiConfig(_cfg.ApiIntegrations);
            await _settings.SaveAsync(_cfg);
            string key = cfg.ApiKey;

            if (string.IsNullOrWhiteSpace(key) || key.StartsWith("sk-ant-.."))
            {
                SetApiTestStatus("Enter a valid API key first.", C_YELLOW);
                Log("[AI] Test skipped - no API key entered.", C_YELLOW);
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
                Log($"[AI] API test passed - {status}", C_GREEN);
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
            _bot?.UpdateApiConfig(_cfg.ApiIntegrations);
            await _settings.SaveAsync(_cfg);

            bool disabled = string.Equals(_cfg.ApiIntegrations.NewsProvider, "None", StringComparison.OrdinalIgnoreCase);
            bool configured = disabled || !string.IsNullOrWhiteSpace(_cfg.ApiIntegrations.NewsApiKey);
            string message;
            Color color;
            if (disabled)
            {
                message = "News provider disabled.";
                color = C_GREEN;
            }
            else if (!configured)
            {
                message = "Enter a news API key before enabling news filtering.";
                color = C_YELLOW;
            }
            else
            {
                var pair = _cmbAllowedPair.SelectedItem?.ToString() ?? _cfg.Bot.AllowedPairs.FirstOrDefault() ?? "XAUUSD";
                var risk = await _newsCalendar.GetRiskSnapshotAsync(pair, _cfg.ApiIntegrations);
                message = risk.IsConfigured
                    ? $"{risk.Source}: {risk.RiskLevel} for {pair} - {risk.Reason}"
                    : risk.Reason;
                color = risk.IsConfigured ? C_GREEN : C_YELLOW;
            }

            SetNewsTestStatus(message, color);
            Log($"[AI] News API config check: {message}", color);
            UpdateAiApiConfigStatus(_cfg.Claude);
        }

        private async Task TestTelegramConfigAsync()
        {
            string token  = _txtTelegramBotToken.Text.Trim();
            string chatId = _txtTelegramChatId.Text.Trim();

            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(chatId))
            {
                _lblTelegramTestStatus.Text      = "Enter bot token and chat ID first.";
                _lblTelegramTestStatus.ForeColor = Color.OrangeRed;
                return;
            }

            _btnTestTelegram.Enabled         = false;
            _lblTelegramTestStatus.Text      = "Sending...";
            _lblTelegramTestStatus.ForeColor = Color.Gray;

            var tempCfg = new ApiIntegrationConfig
            {
                TelegramBotToken = token,
                TelegramChatId   = chatId
            };

            try
            {
                var svc = new MT5TradingBot.Services.TelegramService(tempCfg);
                await svc.SendTestMessageAsync().ConfigureAwait(true);
                _lblTelegramTestStatus.Text      = "Test message sent successfully.";
                _lblTelegramTestStatus.ForeColor = Color.SeaGreen;
            }
            catch (Exception ex)
            {
                _lblTelegramTestStatus.Text      = $"Failed: {ex.Message}";
                _lblTelegramTestStatus.ForeColor = Color.OrangeRed;
            }
            finally
            {
                _btnTestTelegram.Enabled = true;
            }
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
                return "Invalid API key (401) - check key in AI API Config tab";
            if (msg.Contains("403"))
                return "Forbidden (403) - key may lack permissions for this model";
            if (msg.Contains("429") || msg.Contains("rate_limit"))
                return "Rate limited (429) - wait and retry";
            if (msg.Contains("529") || msg.Contains("overloaded"))
                return "API overloaded (529) - retry in a few minutes";
            if (msg.Contains("model_not_found") || msg.Contains("model"))
                return $"Model not found - verify model name: {msg[..Math.Min(80, msg.Length)]}";
            if (msg.Contains("SocketException") || msg.Contains("HttpRequestException") || msg.Contains("timeout"))
                return "Network error - check internet connection";
            return msg.Length > 120 ? msg[..120] + "..." : msg;
        }

        // â"€â"€ AI Trade Decision (one-shot, from Review Trade dialog) â"€â"€â"€â"€

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
                            new() { Text = "Follow the user's trading-analysis instructions exactly. Return only one valid JSON object and no markdown." }
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

                string action = (jobj.Value<string>("action") ?? "").ToUpperInvariant();
                string decision = (jobj.Value<string>("decision") ?? jobj.Value<string>("trade_type") ?? action).ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(decision))
                    decision = "NO_TRADE";

                bool allowed = action == "TRADE"
                            || decision is "BUY" or "SELL"
                            || jobj["execution_permission"]?.Value<bool>("allowed_to_execute") == true;

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
                string dir  = (jobj.Value<string>("trade_type") ?? jobj.Value<string>("direction") ?? original.TradeType.ToString()).ToUpper();
                string etype = (jobj.Value<string>("order_type") ?? entry?.Value<string>("entry_type") ?? "MARKET").ToUpper();

                double entryPrice = jobj.Value<double?>("entry_price")   ?? entry?.Value<double>("entry_price") ?? 0;
                double sl         = jobj.Value<double?>("stop_loss")     ?? risk?.Value<double>("stop_loss")     ?? original.StopLoss;
                double tp1        = jobj.Value<double?>("take_profit")   ?? risk?.Value<double>("take_profit_1") ?? original.TakeProfit;
                double tp2        = jobj.Value<double?>("take_profit_2") ?? risk?.Value<double>("take_profit_2") ?? original.TakeProfit2;
                double lot        = jobj.Value<double?>("lot_size")      ?? risk?.Value<double>("suggested_lot") ?? original.LotSize;

                var orderType = etype switch
                {
                    "BUY_LIMIT"  => OrderType.LIMIT,
                    "SELL_LIMIT" => OrderType.LIMIT,
                    "BUY_STOP"   => OrderType.STOP,
                    "SELL_STOP"  => OrderType.STOP,
                    _            => OrderType.MARKET
                };

                string responsePair = jobj.Value<string>("pair") ?? jobj.Value<string>("symbol") ?? original.Pair;
                string finalPair = responsePair.StartsWith(original.Pair, StringComparison.OrdinalIgnoreCase)
                    ? original.Pair
                    : responsePair;

                return new TradeRequest
                {
                    Id          = Guid.NewGuid().ToString("N")[..8].ToUpper(),
                    Pair        = finalPair,
                    TradeType   = dir == "SELL" ? TradeType.SELL : TradeType.BUY,
                    OrderType   = orderType,
                    EntryPrice  = entryPrice,
                    StopLoss    = sl,
                    TakeProfit  = tp1,
                    TakeProfit2 = tp2,
                    LotSize     = lot > 0 ? lot : original.LotSize,
                    Comment     = jobj.Value<string>("comment") ?? "AI_Decision",
                    MagicNumber = jobj.Value<int?>("magic_number") ?? original.MagicNumber,
                    MoveSLToBreakevenAfterTP1 = jobj.Value<bool?>("move_sl_to_be_after_tp1") ?? original.MoveSLToBreakevenAfterTP1,
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
                var reasons  = jobj["reason"]?.Type == JTokenType.Array
                    ? jobj["reason"]!.ToObject<List<string>>() ?? []
                    : [jobj.Value<string>("reason") ?? ""];
                var newReasons = jobj["reasons"]?.ToObject<List<string>>() ?? [];
                var risks      = jobj["risks"]?.ToObject<List<string>>() ?? [];
                var all        = blocking.Concat(reasons).Concat(newReasons).Concat(risks).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
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
            List<LivePosition> positions;
            try
            {
                positions = await _bridge.GetPositionsAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log($"[POSITIONS] Live MT5 position sync failed: {ex.Message}", C_RED);
                return;
            }

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
                _ = PersistLifecycleAuditAsync(new TradeLifecycleAuditRecord
                {
                    CreatedAtUtc = DateTime.UtcNow,
                    EventType = ok ? "CLOSE_REQUESTED" : "CLOSE_FAILED",
                    Ticket = t,
                    PositionId = t,
                    Actor = "User",
                    Reason = ok ? "User closed selected position from Positions tab." : "User close selected position failed.",
                    DetailsJson = JsonConvert.SerializeObject(new { source = "PositionsTab.CloseSelected" }, Formatting.None)
                });
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
            {
                bool ok = await _bridge.CloseTradeAsync(p.Ticket);
                if (ok) count++;
                _ = PersistLifecycleAuditAsync(new TradeLifecycleAuditRecord
                {
                    CreatedAtUtc = DateTime.UtcNow,
                    EventType = ok ? "CLOSE_REQUESTED" : "CLOSE_FAILED",
                    Ticket = p.Ticket,
                    PositionId = p.Ticket,
                    Pair = p.Symbol,
                    Direction = p.Type.ToString(),
                    Actor = "User",
                    Reason = ok ? "User requested close-all from Positions tab." : "User close-all failed for this position.",
                    Price = p.CurrentPrice,
                    ProfitUsd = p.Profit,
                    DetailsJson = JsonConvert.SerializeObject(new { source = "PositionsTab.CloseAll" }, Formatting.None)
                });
            }
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
                double.TryParse(_txtLot.Text, out lots);

                string sym    = _cmbPair.SelectedItem?.ToString() ?? "";
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
                else
                {
                    if (!_refreshTimer.Enabled)
                        _refreshTimer.Start();
                    _ = RefreshPositionsAsync();
                }
                UpdateEaDeploymentStatusBadge(force: true);
                if (connected)
                    _ = RefreshEaStatusButtonAsync(logResult: false);
            });
        }

        private void UpdateEaDeploymentStatusBadge(bool force = false)
        {
            if (!force && DateTime.UtcNow - _lastEaStatusBadgeRefreshUtc < TimeSpan.FromSeconds(15))
                return;

            _lastEaStatusBadgeRefreshUtc = DateTime.UtcNow;

            try
            {
                var status = ReadEaDeploymentBadgeStatus();
                UIThread(() =>
                {
                    _lblEaStatus.Text = status.Text;
                    _lblEaStatus.ForeColor = status.ForeColor;
                    _lblEaStatus.BackColor = status.BackColor;
                    _lblEaStatus.FlatAppearance.BorderColor = C_BORDER;
                    _cardTooltip.SetToolTip(_lblEaStatus, status.Tooltip);
                });
            }
            catch (Exception ex)
            {
                UIThread(() =>
                {
                    _lblEaStatus.Text = "[?] EA Unknown";
                    _lblEaStatus.ForeColor = C_YELLOW;
                    _lblEaStatus.BackColor = Color.FromArgb(40, 32, 18);
                    _lblEaStatus.FlatAppearance.BorderColor = C_BORDER;
                    _cardTooltip.SetToolTip(_lblEaStatus, $"Could not read EA deployment status: {ex.Message}");
                });
            }
        }

        private async Task RefreshEaStatusButtonAsync(bool logResult)
        {
            UIThread(() =>
            {
                _lblEaStatus.Enabled = false;
                _lblEaStatus.Text = "[...] EA Check";
                _lblEaStatus.ForeColor = C_ACCENT;
                _lblEaStatus.BackColor = Color.FromArgb(18, 28, 42);
                _cardTooltip.SetToolTip(_lblEaStatus, "Checking live EA health from MT5...");
            });

            try
            {
                if (_bridge?.IsConnected != true)
                {
                    var fileStatus = ReadEaDeploymentBadgeStatus();
                    UIThread(() =>
                    {
                        _lblEaStatus.Text = fileStatus.Text;
                        _lblEaStatus.ForeColor = fileStatus.ForeColor;
                        _lblEaStatus.BackColor = fileStatus.BackColor;
                        _cardTooltip.SetToolTip(_lblEaStatus, fileStatus.Tooltip + "\n\nMT5 is not connected. Connect first, then click to check live EA health.");
                    });
                    if (logResult)
                        Log("[EA] Live status check skipped: MT5 is not connected.", C_YELLOW);
                    return;
                }

                var (success, health, error) = await _bridge.TryGetEaHealthAsync().ConfigureAwait(false);
                if (success && health?.IsAlive == true)
                {
                    MarkEaReloadSatisfiedIfNeeded();
                    string tooltip =
                        $"EA is live and responding from MT5.\n" +
                        $"Version: {health.Version}\n" +
                        $"Build: {health.BuildIdentifier}\n" +
                        $"Terminal: {health.TerminalName}\n" +
                        $"Server: {health.Server}\n" +
                        $"Checked: {DateTime.Now:HH:mm:ss}";
                    UIThread(() =>
                    {
                        _lblEaStatus.Text = "[OK] EA Live";
                        _lblEaStatus.ForeColor = C_GREEN;
                        _lblEaStatus.BackColor = Color.FromArgb(16, 42, 28);
                        _cardTooltip.SetToolTip(_lblEaStatus, tooltip);
                    });
                    if (logResult)
                        Log("[EA] Live health check passed. EA is attached and responding.", C_GREEN);
                    return;
                }

                UIThread(() =>
                {
                    _lblEaStatus.Text = "[X] EA No Reply";
                    _lblEaStatus.ForeColor = C_RED;
                    _lblEaStatus.BackColor = Color.FromArgb(45, 18, 24);
                    _cardTooltip.SetToolTip(_lblEaStatus, $"MT5 is connected, but GET_EA_HEALTH did not return a live EA response.\n{error}");
                });
                if (logResult)
                    Log($"[EA] Live health check failed: {error}", C_RED);
            }
            catch (Exception ex)
            {
                UIThread(() =>
                {
                    _lblEaStatus.Text = "[X] EA Check";
                    _lblEaStatus.ForeColor = C_RED;
                    _lblEaStatus.BackColor = Color.FromArgb(45, 18, 24);
                    _cardTooltip.SetToolTip(_lblEaStatus, $"Could not check live EA health: {ex.Message}");
                });
                if (logResult)
                    Log($"[EA] Live health check failed: {ex.Message}", C_RED);
            }
            finally
            {
                UIThread(() => _lblEaStatus.Enabled = true);
            }
        }

        private static void MarkEaReloadSatisfiedIfNeeded()
        {
            foreach (string statusPath in GetEaDeploymentStatusPaths())
                MarkEaReloadSatisfiedIfNeeded(statusPath);
        }

        private static void MarkEaReloadSatisfiedIfNeeded(string statusPath)
        {
            if (string.IsNullOrWhiteSpace(statusPath) || !File.Exists(statusPath))
                return;

            try
            {
                var status = JObject.Parse(File.ReadAllText(statusPath));
                status["needs_mt5_reload"] = false;
                status["message"] = "EA live health check passed after reattach.";
                status["live_health_checked_at"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
                File.WriteAllText(statusPath, status.ToString(Formatting.Indented));
            }
            catch
            {
                // Badge refresh is advisory; do not interrupt trading status updates for a bad status file.
            }
        }

        private static IEnumerable<string> GetEaDeploymentStatusPaths()
        {
            string preparedPath = AppPaths.PrepareEaDeployStatusFile();
            yield return preparedPath;

            string legacyPath = AppPaths.LegacyEaDeployStatusFile;
            if (!string.Equals(preparedPath, legacyPath, StringComparison.OrdinalIgnoreCase))
                yield return legacyPath;
        }

        private (string Text, Color ForeColor, Color BackColor, string Tooltip) ReadEaDeploymentBadgeStatus()
        {
            string statusPath = AppPaths.PrepareEaDeployStatusFile();
            if (!File.Exists(statusPath))
            {
                return (
                    "[?] EA Unknown",
                    C_YELLOW,
                    Color.FromArgb(40, 32, 18),
                    "EA deployment status file was not found yet. Restart the bot to run the startup EA copy check.");
            }

            var status = JObject.Parse(File.ReadAllText(statusPath));
            string mq5Path = status.Value<string>("mq5_path") ?? "";
            string ex5Path = status.Value<string>("ex5_path") ?? "";
            string sourceMq5 = status.Value<string>("source") ?? "";
            string sourceEx5 = status.Value<string>("source_ex5") ?? "";
            bool needsReload = status.Value<bool?>("needs_mt5_reload") == true;
            string message = status.Value<string>("message") ?? "";

            if (!File.Exists(mq5Path) || !File.Exists(ex5Path))
            {
                return (
                    "[X] EA Missing",
                    C_RED,
                    Color.FromArgb(45, 18, 24),
                    $"TradingBotEA is not present in the configured MT5 Experts folder.\nMQ5: {mq5Path}\nEX5: {ex5Path}");
            }

            bool sourceMq5Available = File.Exists(sourceMq5);
            bool mq5Matches = sourceMq5Available && FilesMatch(sourceMq5, mq5Path);
            bool sourceEx5Available = File.Exists(sourceEx5);
            bool sourceEx5Matches = sourceEx5Available && FilesMatch(sourceEx5, ex5Path);
            bool deployedEx5Fresh = IsFileAtLeastAsNew(ex5Path, mq5Path);

            if (!mq5Matches)
            {
                return (
                    "[X] EA Outdated",
                    C_RED,
                    Color.FromArgb(45, 18, 24),
                    $"MT5 Experts MQ5 does not match the repository EA source. Deploy the EA again, then re-attach it in MT5.\nSource: {sourceMq5}\nMT5: {mq5Path}");
            }

            if (!sourceEx5Matches && !deployedEx5Fresh)
            {
                return (
                    "[X] EA Outdated",
                    C_RED,
                    Color.FromArgb(45, 18, 24),
                    $"TradingBotEA.ex5 is older than the deployed MQ5 source. Compile/deploy the EA again, then re-attach it in MT5.\nMQ5: {mq5Path}\nEX5: {ex5Path}");
            }

            if (needsReload)
            {
                return (
                    "[!] EA Reload",
                    C_YELLOW,
                    Color.FromArgb(42, 33, 16),
                    $"EA files were copied successfully, but MT5 must reload them. Remove and re-attach TradingBotEA on the chart, or restart MT5.\n{message}\nEX5: {ex5Path}");
            }

            return (
                "[OK] EA File",
                C_GREEN,
                Color.FromArgb(16, 42, 28),
                $"TradingBotEA source matches the repository and the compiled EX5 is fresh in the MT5 Experts folder.\nEX5: {ex5Path}");
        }

        private static bool FilesMatch(string leftPath, string rightPath)
        {
            var left = new FileInfo(leftPath);
            var right = new FileInfo(rightPath);
            if (left.Length != right.Length) return false;

            using var leftStream = File.OpenRead(leftPath);
            using var rightStream = File.OpenRead(rightPath);
            byte[] leftHash = SHA256.HashData(leftStream);
            byte[] rightHash = SHA256.HashData(rightStream);
            return leftHash.SequenceEqual(rightHash);
        }

        private static bool IsFileAtLeastAsNew(string candidatePath, string referencePath)
        {
            if (!File.Exists(candidatePath) || !File.Exists(referencePath))
                return false;

            return File.GetLastWriteTimeUtc(candidatePath) >= File.GetLastWriteTimeUtc(referencePath).AddSeconds(-5);
        }

        private void UpdateBotBadge(bool running)
        {
            UIThread(() =>
            {
                string modeSuffix = running ? _bot?.TradingMode switch
                {
                    TradingControlMode.Auto          => " [AUTO]",
                    TradingControlMode.ManualApproval => " [MANUAL]",
                    TradingControlMode.PaperTrading  => " [PAPER]",
                    _                                => ""
                } ?? "" : "";
                _lblBotBadge.Text =
                    running && _bot?.IsEdgePaused == true ? "BOT PAUSED (EDGE)" + modeSuffix :
                    running                               ? "BOT MONITORING" + modeSuffix     :
                                                            "BOT STOPPED";
                _lblBotBadge.ForeColor =
                    running && _bot?.IsEdgePaused == true       ? Color.Orange :
                    running && _bot?.IsPaperTrading == true      ? Color.Gold   :
                    running                                      ? C_ACCENT     :
                                                                   C_RED;
                _btnStopBot.Enabled = running;
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
                string statusPath = AppPaths.PrepareEaDeployStatusFile();

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
            {
                _gridHistory.Rows.Insert(0,
                    DateTime.Now.ToString("HH:mm:ss"), req.Id, req.Pair,
                    req.TradeType.ToString(), $"{req.LotSize:F2}",
                    $"{req.EntryPrice:F5}", $"{req.StopLoss:F5}", $"{req.TakeProfit:F5}",
                    result.Ticket, result.Status, $"{result.ExecutedPrice:F5}",
                    result.ErrorMessage);
                _gridHistory.Rows[0].Tag = new TradeHistoryDetail(
                    new Mt5TradeHistoryItem
                    {
                        DealTicket = result.Ticket,
                        OrderTicket = result.Ticket,
                        PositionId = result.Ticket,
                        TimeUtc = DateTime.UtcNow,
                        EntryTimeUtc = DateTime.UtcNow,
                        Symbol = req.Pair,
                        Direction = req.TradeType.ToString(),
                        EntryType = result.IsSuccess ? "IN" : "REJECTED",
                        Lots = req.LotSize,
                        Price = result.ExecutedPrice > 0 ? result.ExecutedPrice : req.EntryPrice,
                        EntryPrice = result.ExecutedPrice > 0 ? result.ExecutedPrice : req.EntryPrice,
                        StopLoss = req.StopLoss,
                        TakeProfit = req.TakeProfit,
                        MagicNumber = req.MagicNumber,
                        Comment = string.IsNullOrWhiteSpace(req.Comment) ? result.ErrorMessage : req.Comment
                    },
                    FindRuleAuditForRequest(req.Id, result),
                    result.Ticket > 0 && _tradeLifecycleAuditsByTicket.TryGetValue(result.Ticket, out var lifecycle) ? lifecycle : []);
            });
        }

        private void CaptureExecutionRuleAudit(TradeRequest? request, TradeResult result)
        {
            var audit = AutoBotService.LastExecutionAuditSnapshot
                .Where(a =>
                    (request != null && string.Equals(a.RequestId, request.Id, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(result.RequestId) && string.Equals(a.RequestId, result.RequestId, StringComparison.OrdinalIgnoreCase)) ||
                    (request != null && string.Equals(a.Pair, request.Pair, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(a => a.Order)
                .ToList();

            if (audit.Count == 0)
                return;

            string requestId = request?.Id ?? result.RequestId;
            if (!string.IsNullOrWhiteSpace(requestId))
                _tradeRuleAuditsByRequestId[requestId] = audit;
            if (result.Ticket > 0)
                _tradeRuleAuditsByTicket[result.Ticket] = audit;

            _ = PersistLifecycleAuditAsync(new TradeLifecycleAuditRecord
            {
                CreatedAtUtc = DateTime.UtcNow,
                EventType = result.IsSuccess ? "EXEC_RULES_PASS" : "EXEC_RULES_REJECT",
                RequestId = requestId,
                Ticket = result.Ticket,
                PositionId = result.Ticket,
                Pair = request?.Pair ?? audit.FirstOrDefault()?.Pair ?? "",
                Direction = request?.TradeType.ToString() ?? "",
                Actor = "DesktopApp",
                Reason = result.IsSuccess ? "Execution rules passed before broker send." : result.ErrorMessage,
                Price = result.ExecutedPrice,
                ProfitUsd = 0,
                DetailsJson = JsonConvert.SerializeObject(audit, Formatting.None)
            });

            if (result.IsSuccess && request != null)
            {
                _ = PersistLifecycleAuditAsync(new TradeLifecycleAuditRecord
                {
                    CreatedAtUtc = result.ExecutedAt == default ? DateTime.UtcNow : result.ExecutedAt,
                    EventType = "OPEN_ACCEPTED",
                    RequestId = request.Id,
                    Ticket = result.Ticket,
                    PositionId = result.Ticket,
                    Pair = request.Pair,
                    Direction = request.TradeType.ToString(),
                    Actor = "MT5",
                    Reason = "Broker accepted order after desktop execution gate.",
                    Price = result.ExecutedPrice,
                    DetailsJson = JsonConvert.SerializeObject(new
                    {
                        request.OrderType,
                        request.LotSize,
                        request.StopLoss,
                        request.TakeProfit,
                        request.Comment,
                        result.ExecutedLots,
                        result.EstimatedSlippagePips,
                        result.EstimatedCommission,
                        result.BrokerRetcode,
                        result.BrokerComment
                    }, Formatting.None)
                });
            }
        }

        private async Task PersistLifecycleAuditAsync(TradeLifecycleAuditRecord record)
        {
            if (_tradeDb == null) return;
            await _tradeDb.InsertLifecycleAuditAsync(record).ConfigureAwait(false);
            IndexLifecycleAudits([record]);
        }

        private void IndexLifecycleAudits(IReadOnlyList<TradeLifecycleAuditRecord> records)
        {
            foreach (var group in records
                         .SelectMany(r => LifecycleAuditKeys(r).Select(key => new { key, record = r }))
                         .GroupBy(x => x.key))
            {
                var existing = _tradeLifecycleAuditsByTicket.TryGetValue(group.Key, out var current)
                    ? current
                    : [];
                _tradeLifecycleAuditsByTicket[group.Key] = existing
                    .Concat(group.Select(x => x.record))
                    .GroupBy(r => $"{r.EventType}|{r.CreatedAtUtc:O}|{r.Reason}", StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .OrderBy(r => r.CreatedAtUtc)
                    .ToList();
            }
        }

        private static IEnumerable<long> LifecycleAuditKeys(TradeLifecycleAuditRecord record)
        {
            foreach (long key in new[] { record.Ticket, record.PositionId, record.OrderTicket, record.DealTicket })
                if (key > 0) yield return key;
        }

        private IReadOnlyList<TradeRuleAuditSnapshot> FindRuleAuditForRequest(string requestId, TradeResult result)
        {
            if (!string.IsNullOrWhiteSpace(requestId) && _tradeRuleAuditsByRequestId.TryGetValue(requestId, out var byRequest))
                return byRequest;
            if (!string.IsNullOrWhiteSpace(result.RequestId) && _tradeRuleAuditsByRequestId.TryGetValue(result.RequestId, out byRequest))
                return byRequest;
            if (result.Ticket > 0 && _tradeRuleAuditsByTicket.TryGetValue(result.Ticket, out var byTicket))
                return byTicket;
            return [];
        }

        private IReadOnlyList<TradeLifecycleAuditRecord> FindLifecycleAuditForTrade(Mt5TradeHistoryItem trade)
        {
            return new[] { trade.PositionId, trade.EntryOrderTicket, trade.ExitOrderTicket, trade.EntryDealTicket, trade.ExitDealTicket, trade.DealTicket }
                .Where(v => v > 0)
                .SelectMany(key => _tradeLifecycleAuditsByTicket.TryGetValue(key, out var audits) ? audits : [])
                .GroupBy(r => $"{r.EventType}|{r.CreatedAtUtc:O}|{r.Reason}", StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(r => r.CreatedAtUtc)
                .ToList();
        }

        private IReadOnlyList<TradeRuleAuditSnapshot> FindRuleAuditForTrade(Mt5TradeHistoryItem trade)
        {
            foreach (long key in new[] { trade.PositionId, trade.OrderTicket, trade.DealTicket }.Where(v => v > 0))
            {
                if (_tradeRuleAuditsByTicket.TryGetValue(key, out var audit))
                    return audit;
            }

            foreach (var record in FindLifecycleAuditForTrade(trade)
                         .Where(r => r.EventType.StartsWith("EXEC_RULES", StringComparison.OrdinalIgnoreCase)))
            {
                var audit = TryReadRuleAudit(record.DetailsJson);
                if (audit.Count > 0)
                    return audit;
            }

            return [];
        }

        private static IReadOnlyList<TradeRuleAuditSnapshot> TryReadRuleAudit(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return [];
            try
            {
                return JsonConvert.DeserializeObject<List<TradeRuleAuditSnapshot>>(json) ?? [];
            }
            catch
            {
                return [];
            }
        }

        private async Task RefreshHistoryFromMt5Async()
        {
            if (_bridge?.IsConnected != true)
            {
                Log("[HISTORY] MT5 is not connected. Connect first, then refresh history.", C_YELLOW);
                return;
            }

            DateTime toUtc = DateTime.UtcNow;
            DateTime fromUtc = toUtc.AddDays(-7);
            var result = await _bridge.TryGetTradeHistoryAsync(fromUtc, toUtc, 200).ConfigureAwait(false);
            if (!result.Success)
            {
                Log($"[HISTORY] MT5 history refresh failed: {result.Error}", C_RED);
                return;
            }

            if (_tradeDb != null)
            {
                var lifecycle = await _tradeDb.GetLifecycleAuditsByDateRangeAsync(fromUtc.AddDays(-1), toUtc.AddDays(1)).ConfigureAwait(false);
                IndexLifecycleAudits(lifecycle);
            }

            UIThread(() =>
            {
                _gridHistory.Rows.Clear();
                foreach (var trade in BuildTradeHistoryRows(result.Trades))
                {
                    bool isClosed = IsClosedTradeHistoryRow(trade);
                    string status = isClosed
                            ? $"CLOSED P/L {trade.Profit:F2}"
                            : "OPENED";

                    int rowIndex = _gridHistory.Rows.Add(
                        trade.TimeUtc.ToLocalTime().ToString("MM-dd HH:mm"),
                        trade.DealTicket,
                        trade.Symbol,
                        trade.Direction,
                        $"{trade.Lots:F2}",
                        $"{(trade.EntryPrice > 0 ? trade.EntryPrice : trade.Price):F5}",
                        trade.StopLoss > 0 ? $"{trade.StopLoss:F5}" : "",
                        trade.TakeProfit > 0 ? $"{trade.TakeProfit:F5}" : "",
                        trade.ExitOrderTicket > 0 ? trade.ExitOrderTicket : trade.EntryOrderTicket > 0 ? trade.EntryOrderTicket : trade.OrderTicket,
                        status,
                        trade.ExitPrice > 0 ? $"{trade.ExitPrice:F5}" : $"{trade.Price:F5}",
                        trade.Comment);
                    var row = _gridHistory.Rows[rowIndex];
                    row.Tag = new TradeHistoryDetail(
                        trade,
                        FindRuleAuditForTrade(trade),
                        FindLifecycleAuditForTrade(trade));
                    if (isClosed)
                        row.DefaultCellStyle.ForeColor = trade.Profit >= 0 ? C_GREEN : C_RED;
                }
            });

            Log($"[HISTORY] Loaded {result.Trades.Count} MT5 history rows from the last 7 days.", C_GREEN);
        }

        private static IReadOnlyList<Mt5TradeHistoryItem> BuildTradeHistoryRows(IReadOnlyList<Mt5TradeHistoryItem> deals)
        {
            return deals
                .GroupBy(d => d.PositionId > 0 ? d.PositionId : d.DealTicket)
                .Select(BuildTradeHistoryRow)
                .OrderByDescending(t => t.ExitTimeUtc ?? t.EntryTimeUtc ?? t.TimeUtc)
                .ToList();
        }

        private static Mt5TradeHistoryItem BuildTradeHistoryRow(IGrouping<long, Mt5TradeHistoryItem> group)
        {
            var ordered = group
                .OrderBy(d => d.TimeUtc)
                .ThenBy(d => d.DealTicket)
                .ToList();

            var entry = ordered.FirstOrDefault(IsEntryDeal) ?? ordered.First();
            var exit = ordered.LastOrDefault(IsExitDeal);
            var detailSource = exit ?? entry;
            double totalProfit = ordered
                .Where(IsExitDeal)
                .Sum(d => d.Profit);
            if (exit == null)
                totalProfit = ordered.Sum(d => d.Profit);
            string direction = FirstNonBlank(entry.Direction, detailSource.Direction);
            double entryPrice = entry.EntryPrice > 0 ? entry.EntryPrice : entry.Price;
            double exitPrice = exit == null ? 0 : exit.ExitPrice > 0 ? exit.ExitPrice : exit.Price;
            double closePips = exit?.ClosePips ?? 0;
            double maxProfitPips = exit?.MaxProfitPips ?? 0;
            double maxLossPips = exit?.MaxLossPips ?? 0;
            double highestPrice = exit?.HighestPrice > 0 ? exit.HighestPrice : entry.HighestPrice;
            double lowestPrice = exit?.LowestPrice > 0 ? exit.LowestPrice : entry.LowestPrice;
            ApplyTradePriceFallbacks(direction, entryPrice, exitPrice, ref closePips, ref maxProfitPips, ref maxLossPips, ref highestPrice, ref lowestPrice);

            return new Mt5TradeHistoryItem
            {
                DealTicket = detailSource.DealTicket,
                EntryDealTicket = entry.DealTicket,
                ExitDealTicket = exit?.DealTicket ?? 0,
                OrderTicket = detailSource.OrderTicket,
                EntryOrderTicket = entry.OrderTicket,
                ExitOrderTicket = exit?.OrderTicket ?? 0,
                PositionId = group.Key,
                TimeUtc = detailSource.TimeUtc,
                EntryTimeUtc = entry.EntryTimeUtc ?? entry.TimeUtc,
                ExitTimeUtc = exit?.ExitTimeUtc ?? exit?.TimeUtc,
                Symbol = FirstNonBlank(entry.Symbol, detailSource.Symbol),
                Direction = direction,
                EntryType = exit != null ? "TRADE_CLOSED" : "TRADE_OPEN",
                Lots = entry.Lots > 0 ? entry.Lots : detailSource.Lots,
                Price = detailSource.Price,
                EntryPrice = entryPrice,
                ExitPrice = exitPrice,
                Profit = totalProfit,
                ClosePips = closePips,
                MaxProfitPips = maxProfitPips,
                MaxLossPips = maxLossPips,
                HighestPrice = highestPrice,
                LowestPrice = lowestPrice,
                DurationMinutes = exit?.DurationMinutes ?? 0,
                StopLoss = entry.StopLoss > 0 ? entry.StopLoss : detailSource.StopLoss,
                TakeProfit = entry.TakeProfit > 0 ? entry.TakeProfit : detailSource.TakeProfit,
                MagicNumber = entry.MagicNumber != 0 ? entry.MagicNumber : detailSource.MagicNumber,
                Comment = FirstNonBlank(entry.Comment, detailSource.Comment),
                CloseReason = exit?.CloseReason ?? ""
            };
        }

        private static void ApplyTradePriceFallbacks(
            string direction,
            double entryPrice,
            double exitPrice,
            ref double closePips,
            ref double maxProfitPips,
            ref double maxLossPips,
            ref double highestPrice,
            ref double lowestPrice)
        {
            if (entryPrice <= 0 || exitPrice <= 0)
                return;

            if (highestPrice <= 0) highestPrice = Math.Max(entryPrice, exitPrice);
            if (lowestPrice <= 0) lowestPrice = Math.Min(entryPrice, exitPrice);

            double pipSize = InferDisplayPipSize(entryPrice, exitPrice);
            if (Math.Abs(closePips) < 0.000001)
            {
                closePips = string.Equals(direction, "SELL", StringComparison.OrdinalIgnoreCase)
                    ? (entryPrice - exitPrice) / pipSize
                    : (exitPrice - entryPrice) / pipSize;
            }

            if (maxProfitPips <= 0 && maxLossPips <= 0)
            {
                if (string.Equals(direction, "SELL", StringComparison.OrdinalIgnoreCase))
                {
                    maxProfitPips = Math.Max(0, (entryPrice - lowestPrice) / pipSize);
                    maxLossPips = Math.Max(0, (highestPrice - entryPrice) / pipSize);
                }
                else
                {
                    maxProfitPips = Math.Max(0, (highestPrice - entryPrice) / pipSize);
                    maxLossPips = Math.Max(0, (entryPrice - lowestPrice) / pipSize);
                }
            }
        }

        private static double InferDisplayPipSize(double entryPrice, double exitPrice)
        {
            double scale = Math.Max(Math.Abs(entryPrice), Math.Abs(exitPrice));
            return scale >= 100 ? 0.01 : 0.0001;
        }

        private static bool IsEntryDeal(Mt5TradeHistoryItem deal) =>
            string.Equals(deal.EntryType, "IN", StringComparison.OrdinalIgnoreCase);

        private static bool IsExitDeal(Mt5TradeHistoryItem deal) =>
            string.Equals(deal.EntryType, "OUT", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(deal.EntryType, "INOUT", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(deal.EntryType, "OUT_BY", StringComparison.OrdinalIgnoreCase);

        private static bool IsClosedTradeHistoryRow(Mt5TradeHistoryItem trade) =>
            trade.ExitDealTicket > 0 ||
            trade.ExitTimeUtc.HasValue ||
            trade.ExitPrice > 0 ||
            IsExitDeal(trade);

        private static string FirstNonBlank(params string[] values) =>
            values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "";

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

        private void GridHistory_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _gridHistory.Rows.Count) return;
            if (_gridHistory.Rows[e.RowIndex].Tag is not TradeHistoryDetail detail)
            {
                AppMessageBox.Info(this, "This row does not have MT5 detail data. Click Refresh MT5, then double-click the row again.");
                return;
            }

            AppLogDetailBox.Show(this, BuildTradeHistoryPostmortem(detail.Trade, detail.RuleAudit, detail.LifecycleAudit));
        }

        private AppLogDetail BuildTradeHistoryPostmortem(
            Mt5TradeHistoryItem trade,
            IReadOnlyList<TradeRuleAuditSnapshot> ruleAudit,
            IReadOnlyList<TradeLifecycleAuditRecord> lifecycleAudit)
        {
            bool isClosed = IsClosedTradeHistoryRow(trade);
            bool isProfit = trade.Profit >= 0;
            string result = isClosed
                ? isProfit ? "PROFIT" : "LOSS"
                : "OPENED";

            string original = BuildTradeSummaryText(trade, result);
            string meaning = isClosed
                ? $"This MT5 history row is a closed {trade.Direction} trade on {trade.Symbol}. It closed with {FormatMoney(trade.Profit)} and {trade.ClosePips:F1} pips."
                : $"This MT5 history row is an opening deal for {trade.Direction} {trade.Symbol}. Close/max runup values are available after MT5 records the closing deal.";

            string values = BuildTradeValuesText(trade, ruleAudit, lifecycleAudit);
            string formula = BuildTradeFormulaText(trade);
            string outcome = isClosed
                ? $"Status: {result}. Close reason: {BlankIfMissing(trade.CloseReason)}. The row is shown {(isProfit ? "green" : "red")} in the history grid."
                : "Status: opened. No final profit/loss is available on this row yet.";
            string expectedPl = BuildTradeExpectedPlText(trade);
            string next = isClosed
                ? "Postmortem: compare max profit pips, max adverse pips, SL/TP placement, close reason, and duration. If max profit was strong but final P/L was weak, review exit management and trailing/breakeven behavior."
                : "Wait for the close row, then double-click that row for a complete profit/loss postmortem.";

            return new AppLogDetail(
                original,
                meaning,
                values,
                formula,
                outcome,
                expectedPl,
                next,
                "Trade Postmortem",
                "Copyable MT5 deal evidence: entry, exit, max favorable/adverse move, SL/TP, result, and close reason.",
                "MT5 BOT TRADE POSTMORTEM",
                "Trade time");
        }

        private static string BuildTradeSummaryText(Mt5TradeHistoryItem trade, string result)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[{FormatLocalTime(trade.ExitTimeUtc ?? trade.EntryTimeUtc ?? trade.TimeUtc)}] {result}: {trade.Direction} {trade.Symbol} lot {trade.Lots:F2}");
            sb.AppendLine($"Position: {trade.PositionId}");
            sb.AppendLine($"Entry deal/order: {trade.EntryDealTicket}/{trade.EntryOrderTicket}");
            if (trade.ExitDealTicket > 0 || trade.ExitOrderTicket > 0)
                sb.AppendLine($"Exit deal/order: {trade.ExitDealTicket}/{trade.ExitOrderTicket}");
            sb.AppendLine($"Entry: {FormatLocalTime(trade.EntryTimeUtc ?? trade.TimeUtc)} @ {FormatPrice(trade.EntryPrice > 0 ? trade.EntryPrice : trade.Price)}");
            if (IsClosedTradeHistoryRow(trade))
                sb.AppendLine($"Exit: {FormatLocalTime(trade.ExitTimeUtc ?? trade.TimeUtc)} @ {FormatPrice(trade.ExitPrice > 0 ? trade.ExitPrice : trade.Price)}");
            sb.AppendLine($"P/L: {FormatMoney(trade.Profit)} | Close pips: {trade.ClosePips:F1}");
            return sb.ToString();
        }

        private static string BuildTradeValuesText(
            Mt5TradeHistoryItem trade,
            IReadOnlyList<TradeRuleAuditSnapshot> ruleAudit,
            IReadOnlyList<TradeLifecycleAuditRecord> lifecycleAudit)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Symbol: {trade.Symbol}");
            sb.AppendLine($"Direction: {trade.Direction}");
            sb.AppendLine($"Status type: {trade.EntryType}");
            sb.AppendLine($"Position id: {trade.PositionId}");
            sb.AppendLine($"Entry deal/order: {trade.EntryDealTicket}/{trade.EntryOrderTicket}");
            sb.AppendLine($"Exit deal/order: {(trade.ExitDealTicket > 0 ? trade.ExitDealTicket.ToString(CultureInfo.InvariantCulture) : "not closed")}/{(trade.ExitOrderTicket > 0 ? trade.ExitOrderTicket.ToString(CultureInfo.InvariantCulture) : "not closed")}");
            sb.AppendLine($"Lot size: {trade.Lots:F2}");
            sb.AppendLine($"Entry price: {FormatPrice(trade.EntryPrice > 0 ? trade.EntryPrice : trade.Price)}");
            sb.AppendLine($"Exit price: {(trade.ExitPrice > 0 ? FormatPrice(trade.ExitPrice) : "not closed")}");
            sb.AppendLine($"Stop loss: {(trade.StopLoss > 0 ? FormatPrice(trade.StopLoss) : "not available")}");
            sb.AppendLine($"Take profit: {(trade.TakeProfit > 0 ? FormatPrice(trade.TakeProfit) : "not available")}");
            sb.AppendLine($"Highest price during trade: {(trade.HighestPrice > 0 ? FormatPrice(trade.HighestPrice) : "not available")}");
            sb.AppendLine($"Lowest price during trade: {(trade.LowestPrice > 0 ? FormatPrice(trade.LowestPrice) : "not available")}");
            sb.AppendLine($"Max favorable move: {trade.MaxProfitPips:F1} pips");
            sb.AppendLine($"Max adverse move: {trade.MaxLossPips:F1} pips");
            sb.AppendLine($"Final move: {trade.ClosePips:F1} pips");
            sb.AppendLine($"Duration: {trade.DurationMinutes} minutes");
            sb.AppendLine($"Close reason: {BlankIfMissing(trade.CloseReason)}");
            sb.AppendLine($"Magic number: {trade.MagicNumber}");
            sb.AppendLine($"Comment: {BlankIfMissing(trade.Comment)}");
            sb.AppendLine();
            sb.AppendLine(BuildRuleAuditText(ruleAudit));
            sb.AppendLine();
            sb.AppendLine(BuildLifecycleAuditText(lifecycleAudit));
            return sb.ToString();
        }

        private static string BuildLifecycleAuditText(IReadOnlyList<TradeLifecycleAuditRecord> lifecycleAudit)
        {
            if (lifecycleAudit.Count == 0)
                return "TRADE LIFECYCLE AUDIT\nNo persisted lifecycle events were found for this position/ticket yet.";

            var sb = new StringBuilder();
            sb.AppendLine("TRADE LIFECYCLE AUDIT");
            foreach (var record in lifecycleAudit.OrderBy(r => r.CreatedAtUtc))
            {
                sb.AppendLine($"{FormatLocalTime(record.CreatedAtUtc)} | {record.EventType} | {record.Actor} | {record.Reason}");
                if (record.Price > 0 || Math.Abs(record.SpreadPips) > 0.000001 || Math.Abs(record.ProfitUsd) > 0.000001)
                    sb.AppendLine($"  price={FormatPrice(record.Price)} spread={record.SpreadPips:0.0} pips pnl={FormatMoney(record.ProfitUsd)}");
            }

            return sb.ToString();
        }

        private static string BuildRuleAuditText(IReadOnlyList<TradeRuleAuditSnapshot> ruleAudit)
        {
            if (ruleAudit.Count == 0)
                return "RULES PASSED AT EXECUTION\nNot available for this row. This usually means the trade was placed directly in MT5, the app was restarted after execution, or this history deal could not be matched to the app execution ticket.";

            var passed = ruleAudit
                .Where(r => string.Equals(r.Result, TradeRuleResults.Pass, StringComparison.OrdinalIgnoreCase))
                .OrderBy(r => r.Order)
                .ToList();
            var blocked = ruleAudit
                .Where(r => string.Equals(r.Result, TradeRuleResults.Block, StringComparison.OrdinalIgnoreCase))
                .OrderBy(r => r.Order)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine("RULES PASSED AT EXECUTION");
            sb.AppendLine($"Passed: {passed.Count}/{ruleAudit.Count}");
            foreach (var rule in passed)
                sb.AppendLine($"PASS {rule.RuleCode} {rule.RuleName} - {rule.Reason}");

            if (blocked.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("RULES BLOCKED/WARNED AT EXECUTION");
                foreach (var rule in blocked)
                    sb.AppendLine($"{rule.Result} {rule.RuleCode} {rule.RuleName} - {rule.Reason}");
            }

            return sb.ToString();
        }

        private static string BuildTradeFormulaText(Mt5TradeHistoryItem trade)
        {
            string closeFormula = string.Equals(trade.Direction, "BUY", StringComparison.OrdinalIgnoreCase)
                ? "BUY close pips = (exit price - entry price) / pip size."
                : "SELL close pips = (entry price - exit price) / pip size.";
            string mfeFormula = string.Equals(trade.Direction, "BUY", StringComparison.OrdinalIgnoreCase)
                ? "BUY max favorable = highest price during trade - entry. Max adverse = entry - lowest price."
                : "SELL max favorable = entry - lowest price during trade. Max adverse = highest price - entry.";

            return $"{closeFormula}{Environment.NewLine}{mfeFormula}{Environment.NewLine}Profit is broker deal profit + swap + commission reported by MT5.";
        }

        private static string BuildTradeExpectedPlText(Mt5TradeHistoryItem trade)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Actual broker P/L: {FormatMoney(trade.Profit)}");
            sb.AppendLine($"Actual pips: {trade.ClosePips:F1}");
            sb.AppendLine($"Best available runup: +{trade.MaxProfitPips:F1} pips");
            sb.AppendLine($"Worst adverse move: -{trade.MaxLossPips:F1} pips");
            sb.AppendLine("Dollar SL/TP expectation is not reconstructed here unless MT5 provides the original pip-value snapshot; use max pips plus broker P/L for postmortem.");
            return sb.ToString();
        }

        private static string FormatPrice(double value) => value > 0 ? value.ToString("F5", CultureInfo.InvariantCulture) : "not available";
        private static string FormatMoney(double value) => value.ToString("$0.00;-$0.00;$0.00", CultureInfo.InvariantCulture);
        private static string FormatLocalTime(DateTime value) => value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        private static string BlankIfMissing(string value) => string.IsNullOrWhiteSpace(value) ? "not available" : value;

        private void ApplySettingsToUI()
        {
            _cfg.ApiIntegrations ??= new ApiIntegrationConfig();
            _cfg.PairSettings ??= new Dictionary<string, PairTradingSettings>(StringComparer.OrdinalIgnoreCase);
            SyncPairDropdownsFromPairSettings();
            _cmbMode.SelectedIndex   = _cfg.Mt5.Mode == ConnectionMode.NamedPipe ? 0 : 1;
            _txtPipeName.Text        = _cfg.Mt5.PipeName;
            _chkAutoConn.Checked     = _cfg.AutoConnectOnLaunch;
            _txtWatchFolder.Text     = _cfg.Bot.WatchFolder;
            _suppressPairSelectionEvent = true;
            SelectAllowedPair(_cfg.Bot.AllowedPairs.FirstOrDefault());
            _suppressPairSelectionEvent = false;
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
            _txtClaudePrompt.Text    = ClaudeConfig.DefaultPrompt;
            _lblModelValue.Text      = _cfg.Claude.Model;
            _lblClaudeNote1.Text     = "Startup checks validate saved AI configuration only; no prompt is sent.";
            _lblClaudeNote2.Text     = "Tokens are used only when AI analysis/monitoring sends market data to the provider.";
            UpdateAiApiConfigStatus(_cfg.Claude);
            RefreshPairSettingsGrid();
        }

        private void SyncPairDropdownsFromPairSettings()
        {
            var pairs = _pairSettings?.GetAll()
                .Select(p => p.Pair)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList() ?? [];

            string currentBot = _cmbAllowedPair.SelectedItem?.ToString() ?? _cfg.Bot.AllowedPairs.FirstOrDefault() ?? "";

            bool previousSuppressPairSelectionEvent = _suppressPairSelectionEvent;
            _suppressPairSelectionEvent = true;
            try
            {
                _cmbAllowedPair.Items.Clear();
                foreach (string pair in pairs)
                {
                    _cmbAllowedPair.Items.Add(pair);
                }

                SelectComboPair(_cmbAllowedPair, currentBot);
            }
            finally
            {
                _suppressPairSelectionEvent = previousSuppressPairSelectionEvent;
            }

            _cfg.Bot.AllowedPairs = SelectedAllowedPairList();
            _cfg.Claude.WatchSymbols = pairs;
            _txtClaudeSymbols.Text = string.Join(",", pairs);
        }

        private static void SelectComboPair(ComboBox comboBox, string? preferred)
        {
            if (!string.IsNullOrWhiteSpace(preferred))
            {
                for (int i = 0; i < comboBox.Items.Count; i++)
                {
                    if (string.Equals(comboBox.Items[i]?.ToString(), preferred, StringComparison.OrdinalIgnoreCase))
                    {
                        comboBox.SelectedIndex = i;
                        return;
                    }
                }
            }

            comboBox.SelectedIndex = comboBox.Items.Count > 0 ? 0 : -1;
        }

        private void EnsurePairSettingsTab()
        {
            if (_tabControl.TabPages.Contains(_tabPairSettings))
                return;

            _gridPairSettings.AllowUserToAddRows = false;
            _gridPairSettings.AllowUserToDeleteRows = false;
            _gridPairSettings.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _gridPairSettings.Dock = DockStyle.Fill;
            _gridPairSettings.MultiSelect = false;
            _gridPairSettings.ReadOnly = true;
            _gridPairSettings.RowHeadersVisible = false;
            _gridPairSettings.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _gridPairSettings.Columns.Add("Pair", "Pair");
            _gridPairSettings.Columns.Add("PipSize", "Pip size");
            _gridPairSettings.Columns.Add("AtrM5", "ATR M5");
            _gridPairSettings.Columns.Add("AtrM15", "ATR M15");
            _gridPairSettings.Columns.Add("KeyLevelDistance", "Key level dist");
            _gridPairSettings.Columns.Add("Trailing", "Trailing");
            _gridPairSettings.Columns.Add("MaxSlippage", "Slippage");
            _gridPairSettings.Columns.Add("RecommendedSessions", "Recommended sessions");
            _gridPairSettings.Columns.Add("AvoidSessions", "Avoid sessions");
            StyleDataGrid(_gridPairSettings);

            ConfigurePairButton(_btnPairAdd, "Add Pair", C_GREEN);
            ConfigurePairButton(_btnPairEdit, "Edit Pair", C_ACCENT);
            ConfigurePairButton(_btnPairDelete, "Delete Pair", C_RED);
            ConfigurePairButton(_btnPairImport, "Import JSON", C_YELLOW);

            var buttonRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0),
                Margin = Padding.Empty
            };
            buttonRow.Controls.AddRange([_btnPairAdd, _btnPairEdit, _btnPairDelete, _btnPairImport]);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                ColumnCount = 1,
                RowCount = 2
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            layout.Controls.Add(_gridPairSettings, 0, 0);
            layout.Controls.Add(buttonRow, 0, 1);

            _tabPairSettings.BackColor = C_BG;
            _tabPairSettings.Controls.Add(layout);

            int insertAt = _tabControl.TabPages.IndexOf(_tabClaude);
            if (insertAt < 0)
                insertAt = _tabControl.TabPages.Count;
            _tabControl.TabPages.Insert(insertAt, _tabPairSettings);
        }

        private static void ConfigurePairButton(Button button, string text, Color color)
        {
            button.Text = text;
            button.Size = new Size(112, 34);
            button.BackColor = color;
            button.ForeColor = Color.FromArgb(10, 10, 20);
            button.FlatStyle = FlatStyle.Flat;
            button.Font = new Font("Segoe UI Semibold", 9F);
            button.Cursor = Cursors.Hand;
            button.Margin = new Padding(0, 6, 8, 0);
            button.FlatAppearance.BorderSize = 0;
        }

        private void RefreshPairSettingsGrid()
        {
            if (_pairSettings == null || _gridPairSettings.IsDisposed)
                return;

            _gridPairSettings.Rows.Clear();
            foreach (var settings in _pairSettings.GetAll())
            {
                int row = _gridPairSettings.Rows.Add(
                    settings.Pair,
                    settings.PipSize.ToString("0.#####", CultureInfo.InvariantCulture),
                    $"{settings.MinAtrPipsM5:0.##}-{settings.MaxAtrPipsM5:0.##}",
                    $"{settings.MinAtrPipsM15:0.##}-{settings.MaxAtrPipsM15:0.##}",
                    settings.MinimumDistanceFromKeyLevelPips.ToString("0.##", CultureInfo.InvariantCulture),
                    $"{settings.TrailingStartPips:0.##}/{settings.TrailingStepPips:0.##}",
                    settings.MaxSlippagePips.ToString("0.##", CultureInfo.InvariantCulture),
                    string.Join(",", settings.RecommendedSessions),
                    string.Join(",", settings.AvoidSessions));
                _gridPairSettings.Rows[row].Tag = settings;
            }
        }

        private PairTradingSettings? SelectedPairSettings() =>
            _gridPairSettings.CurrentRow?.Tag as PairTradingSettings;

        private void RebindPairSettingsAfterSave()
        {
            _cfg = _settings.Current;
            _pairSettings = new PairSettingsService(_settings, _cfg);
        }

        private void BtnPairAdd_Click(object? sender, EventArgs e)
        {
            using var form = new PairSettingsEditForm();
            if (form.ShowDialog(this) != DialogResult.OK || _pairSettings == null)
                return;

            try
            {
                _pairSettings.Upsert(form.Settings);
                RebindPairSettingsAfterSave();
                SyncPairDropdownsFromPairSettings();
                _settings.SaveAsync(_cfg).GetAwaiter().GetResult();
                RefreshPairSettingsGrid();
                Log($"[PAIR] Saved settings for {form.Settings.Pair}.", C_GREEN);
            }
            catch (Exception ex)
            {
                AppMessageBox.Warning(this, ex.Message, "Pair Settings");
            }
        }

        private void BtnPairEdit_Click(object? sender, EventArgs e) => EditSelectedPairSettings();

        private void GridPairSettings_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
                EditSelectedPairSettings();
        }

        private void EditSelectedPairSettings()
        {
            var selected = SelectedPairSettings();
            if (selected == null || _pairSettings == null)
                return;

            using var form = new PairSettingsEditForm(selected);
            if (form.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                if (!string.Equals(selected.Pair, form.Settings.Pair, StringComparison.OrdinalIgnoreCase))
                    _pairSettings.Delete(selected.Pair);
                _pairSettings.Upsert(form.Settings);
                RebindPairSettingsAfterSave();
                SyncPairDropdownsFromPairSettings();
                _settings.SaveAsync(_cfg).GetAwaiter().GetResult();
                RefreshPairSettingsGrid();
                Log($"[PAIR] Updated settings for {form.Settings.Pair}.", C_GREEN);
            }
            catch (Exception ex)
            {
                AppMessageBox.Warning(this, ex.Message, "Pair Settings");
            }
        }

        private void BtnPairDelete_Click(object? sender, EventArgs e)
        {
            var selected = SelectedPairSettings();
            if (selected == null || _pairSettings == null)
                return;

            var result = AppMessageBox.Show(
                this,
                $"Delete pair settings for {selected.Pair}?",
                "Pair Settings",
                MessageBoxIcon.Warning,
                MessageBoxButtons.YesNo);
            if (result != DialogResult.Yes)
                return;

            if (_pairSettings.Delete(selected.Pair))
            {
                RebindPairSettingsAfterSave();
                RefreshPairSettingsGrid();
                SyncPairDropdownsFromPairSettings();
                _settings.SaveAsync(_cfg).GetAwaiter().GetResult();
                Log($"[PAIR] Deleted settings for {selected.Pair}.", C_YELLOW);
            }
        }

        private void BtnPairImport_Click(object? sender, EventArgs e)
        {
            if (_pairSettings == null)
                return;

            using var form = new PairSettingsJsonForm(DefaultPairSettingsJson());
            if (form.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                int count = _pairSettings.ImportJson(form.JsonText);
                RebindPairSettingsAfterSave();
                SyncPairDropdownsFromPairSettings();
                _settings.SaveAsync(_cfg).GetAwaiter().GetResult();
                RefreshPairSettingsGrid();
                Log($"[PAIR] Imported {count} pair setting(s) from JSON.", C_GREEN);
            }
            catch (Exception ex)
            {
                AppMessageBox.Warning(this, ex.Message, "Pair Settings JSON");
            }
        }

        private static string DefaultPairSettingsJson() => """
        {
          "pair_settings": {
            "GBPUSD": {
              "pip_size": 0.0001,
              "min_atr_pips_m5": 3,
              "max_atr_pips_m5": 30,
              "min_atr_pips_m15": 6,
              "max_atr_pips_m15": 60,
              "minimum_distance_from_key_level_pips": 5,
              "trailing_start_pips": 15,
              "trailing_step_pips": 5,
              "max_slippage_pips": 3,
              "recommended_sessions": ["London", "NewYork", "London_NewYork_Overlap"],
              "avoid_sessions": ["Rollover"]
            }
          }
        }
        """;

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

        private void SelectAllowedPair(string? pair)
        {
            if (!string.IsNullOrWhiteSpace(pair))
            {
                for (int i = 0; i < _cmbAllowedPair.Items.Count; i++)
                {
                    if (string.Equals(_cmbAllowedPair.Items[i]?.ToString(), pair.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        _cmbAllowedPair.SelectedIndex = i;
                        return;
                    }
                }
            }

            if (_cmbAllowedPair.Items.Count > 0)
                _cmbAllowedPair.SelectedIndex = 0;
        }

        private List<string> SelectedAllowedPairList()
        {
            string selected = _cmbAllowedPair.SelectedItem?.ToString()?.Trim() ?? "";
            return string.IsNullOrWhiteSpace(selected) ? [] : [selected];
        }

        private ClaudeConfig ReadClaudeConfigFromUI() => new()
        {
            ApiKey              = _txtClaudeApiKey.Text.Trim(),
            WatchSymbols        = [.. _txtClaudeSymbols.Text.Split(',').Select(s => s.Trim().ToUpper()).Where(s => s.Length > 0)],
            PollIntervalSeconds = (int)_nudClaudePollSec.Value,
            SystemPrompt        = ClaudeConfig.DefaultPrompt,
            Model               = string.IsNullOrWhiteSpace(_txtClaudeModel.Text) ? "claude-opus-4-7" : _txtClaudeModel.Text.Trim()
        };

        private ApiIntegrationConfig ReadApiIntegrationConfigFromUI() => new()
        {
            AiProvider = _cmbAiProvider.SelectedItem?.ToString() ?? "Claude",
            OpenAiApiKey = _txtOpenAiApiKey.Text.Trim(),
            OpenAiModel = string.IsNullOrWhiteSpace(_txtOpenAiModel.Text) ? "gpt-5.1" : _txtOpenAiModel.Text.Trim(),
            MinimumConfidencePercent = (int)_nudAiConfidence.Value,
            NewsProvider = _cmbNewsProvider.SelectedItem?.ToString() ?? "Financial Modeling Prep",
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
            // UI-bound fields
            Enabled      = true,
            WatchFolder  = _txtWatchFolder.Text,
            AllowedPairs = SelectedAllowedPairList(),
            // Settings managed by ReviewTradeForm (persisted in _cfg.Bot)
            MaxRiskPercent            = _cfg.Bot.MaxRiskPercent,
            PollIntervalMs            = _cfg.Bot.PollIntervalMs,
            DrawdownProtectionEnabled = _cfg.Bot.DrawdownProtectionEnabled,
            EmergencyCloseDrawdownPct = _cfg.Bot.EmergencyCloseDrawdownPct,
            RetryOnFail               = true,
            RetryCount                = _cfg.Bot.RetryCount,
            RetryDelayMs              = 1000,
            AutoStartOnLaunch         = _cfg.Bot.AutoStartOnLaunch,
            MagicNumber               = 999001,
            SymbolSuffix              = _cfg.Bot.SymbolSuffix,
            Scalping                  = CloneScalpingSettings(_cfg.Bot.Scalping),
            ScalpingByPair            = new Dictionary<string, ScalpingConfig>(
                (_cfg.Bot.ScalpingByPair ?? new Dictionary<string, ScalpingConfig>())
                    .ToDictionary(kv => kv.Key, kv => CloneScalpingConfig(kv.Value)),
                StringComparer.OrdinalIgnoreCase),
            CommonTrading             = CloneCommonTradingSettings(_cfg.Bot.CommonTrading),
            NormalTrading             = CloneNormalTradingSettings(_cfg.Bot.NormalTrading),
            NormalTradingByPair       = new Dictionary<string, NormalTradingSettings>(
                (_cfg.Bot.NormalTradingByPair ?? new Dictionary<string, NormalTradingSettings>())
                    .ToDictionary(kv => kv.Key, kv => CloneNormalTradingSettings(kv.Value)),
                StringComparer.OrdinalIgnoreCase)
        };

        private BotConfig ReadBotConfigFromUISafe()
        {
            if (!InvokeRequired)
                return ReadBotConfigFromUI();

            return (BotConfig)Invoke(() => ReadBotConfigFromUI())!;
        }

        // -- Log ---------------------------------------------------
        public void Log(string msg, Color? color = null)
        {
            if (InvokeRequired) { Invoke(() => Log(msg, color)); return; }
            Serilog.Log.Information("{msg}", msg);
            string fullMessage = CollapseWhitespace(msg);
            WriteTradeLogIfNeeded(fullMessage);
            string screenMessage = BuildScreenLogMessage(msg);
            if (screenMessage.Length == 0) return;

            string line = $"[{DateTime.Now:HH:mm:ss}] {screenMessage}\n";
            _txtLog.SuspendLayout();
            int start = _txtLog.TextLength;
            _txtLog.AppendText(line);
            _txtLog.Select(start, line.Length);
            _txtLog.SelectionColor = color ?? C_TEXT;
            _txtLog.Select(_txtLog.TextLength, 0);
            _screenLogFullMessages.Add($"[{DateTime.Now:HH:mm:ss}] {fullMessage}");
            TrimScreenLog();
            _txtLog.ResumeLayout();
            _txtLog.ScrollToCaret();
        }

        private static void WriteTradeLogIfNeeded(string fullMessage)
        {
            if (!IsTradeLifecycleMessage(fullMessage)) return;

            try
            {
                AppLogFiles.WriteTrade(fullMessage);
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Could not write trade lifecycle log");
            }
        }

        private static bool IsTradeLifecycleMessage(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;

            string[] markers =
            [
                "Sending trade to MT5",
                "Sending signal",
                "MT5 accepted ticket",
                "MT5 rejected",
                "Trade placed",
                "Trade was not opened",
                "Execute failed",
                "Pair row execute failed",
                "Order attempt",
                "Final order result",
                "[PAPER] Simulated",
                "[PAPER] Estimated commission",
                "[PAPER] Estimated slippage",
                "[PAPER] #",
                "closed at",
                "Closed #",
                "Closed:",
                "Close failed",
                "Failed to close",
                "Emergency close",
                "Position #",
                "extreme slippage",
                "HIGH SLIPPAGE",
                "Slippage:"
            ];

            return markers.Any(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase));
        }

        private static string BuildScreenLogMessage(string msg)
        {
            string text = CollapseWhitespace(msg);
            string marker = text.StartsWith("[SCALP]", StringComparison.OrdinalIgnoreCase)
                ? text[7..].Trim()
                : text;
            if (text.Length == 0 || (marker.Length > 0 && marker.All(ch => ch == '-')))
                return "";

            int detailIndex = text.IndexOf(" | ", StringComparison.Ordinal);
            if (detailIndex > 0)
            {
                int secondDetailIndex = text.IndexOf(" | ", detailIndex + 3, StringComparison.Ordinal);
                if (secondDetailIndex > 0)
                    text = text[..secondDetailIndex];
            }

            return Truncate(text, MaxScreenLogChars);
        }

        private static string CollapseWhitespace(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";

            var parts = value
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Replace('\t', ' ')
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);

            return string.Join(' ', parts);
        }

        private void TrimScreenLog()
        {
            string[] lines = _txtLog.Lines;
            if (lines.Length <= MaxScreenLogLines) return;

            int removeCount = lines.Length - MaxScreenLogLines;
            _txtLog.Lines = lines.Skip(removeCount).ToArray();
            if (removeCount > 0 && _screenLogFullMessages.Count > 0)
                _screenLogFullMessages.RemoveRange(0, Math.Min(removeCount, _screenLogFullMessages.Count));
            _txtLog.Select(_txtLog.TextLength, 0);
        }

        // -- Utility -----------------------------------------------
        private void UIThread(Action a) { if (InvokeRequired) Invoke(a); else a(); }

        private bool AssertConnected()
        {
            if (_bridge?.IsConnected == true) return true;
            Log("[ERROR] Not connected to MT5. Click Connect first.", C_RED);
            return false;
        }

        private bool Confirm(string msg) =>
            AppMessageBox.Confirm(this, msg);

        private static void SetBtnState(Button btn, bool enabled)
        {
            if (btn.InvokeRequired) btn.Invoke(() => btn.Enabled = enabled);
            else btn.Enabled = enabled;
        }

        private void EnsureMarketDataSyncProgressArea()
        {
            if (_pnlMarketDataSync.Parent != null) return;

            _pnlMarketDataSync.Height = 30;
            _pnlMarketDataSync.Dock = DockStyle.Fill;
            _pnlMarketDataSync.BackColor = Color.FromArgb(24, 28, 36);
            _pnlMarketDataSync.Padding = new Padding(10, 5, 10, 5);

            _pbMarketDataSync.Dock = DockStyle.Right;
            _pbMarketDataSync.Width = 180;
            _pbMarketDataSync.Minimum = 0;
            _pbMarketDataSync.Maximum = 100;
            _pbMarketDataSync.Value = 0;

            _lblMarketDataSync.Dock = DockStyle.Fill;
            _lblMarketDataSync.TextAlign = ContentAlignment.MiddleLeft;
            _lblMarketDataSync.ForeColor = C_MUTED;
            _lblMarketDataSync.Text = "Market data sync: idle";

            _pnlMarketDataSync.Controls.Add(_lblMarketDataSync);
            _pnlMarketDataSync.Controls.Add(_pbMarketDataSync);

            _layoutRoot.RowCount = 5;
            if (_layoutRoot.RowStyles.Count < 5)
                _layoutRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            _layoutRoot.Controls.Add(_pnlMarketDataSync, 0, 4);
        }

        private void StartMarketDataAutoSyncIfEnabled()
        {
            if (!_cfg.Bot.EnableMarketDataAutoUpdate)
            {
                Log(MarketDataSyncStatusText.Disabled, C_MUTED);
                UpdateMarketDataProgress(new HistoricalMarketDataSyncProgress
                {
                    Status = HistoricalMarketDataSyncStatus.Skipped,
                    Message = MarketDataSyncStatusText.Disabled
                });
                return;
            }

            bool runOnStartup = _cfg.Bot.UpdateMarketDataOnStartup || _cfg.Bot.UpdateOnStartup;
            if (!runOnStartup)
            {
                Log(MarketDataSyncStatusText.Disabled, C_MUTED);
                UpdateMarketDataProgress(new HistoricalMarketDataSyncProgress
                {
                    Status = HistoricalMarketDataSyncStatus.Skipped,
                    DataType = _cfg.Bot.PreferredMarketDataType,
                    Message = MarketDataSyncStatusText.Disabled
                });
            }
            else
            {
                UpdateMarketDataProgress(new HistoricalMarketDataSyncProgress
                {
                    Status = HistoricalMarketDataSyncStatus.Syncing,
                    DataType = _cfg.Bot.PreferredMarketDataType,
                    Message = MarketDataSyncStatusText.Starting
                });
            }

            _marketDataSync ??= new MarketDataAutoSyncService(
                CreateMarketDataUpdater,
                () => HistoricalMarketDataUpdater.FromConfig(_cfg.Bot),
                TimeSpan.FromMinutes(Math.Max(1, _cfg.Bot.MarketDataSyncIntervalMinutes)),
                _cfg.Bot.AllowSyncDuringTrading,
                IsCriticalTradeExecutionInProgress,
                CheckMarketDataMt5AvailableAsync);

            _marketDataSync.ProgressChanged += p => UIThread(() => UpdateMarketDataProgress(p));
            _marketDataSync.Start(runOnStartup);
        }

        private async Task RestartMarketDataAutoSyncAsync()
        {
            if (_marketDataSync != null)
            {
                await _marketDataSync.DisposeAsync();
                _marketDataSync = null;
            }

            UIThread(StartMarketDataAutoSyncIfEnabled);
        }

        private HistoricalMarketDataUpdater CreateMarketDataUpdater()
        {
            MT5Bridge bridge = _bridge?.IsConnected == true
                ? _bridge
                : (_marketDataBridge ??= new MT5Bridge(_cfg.Mt5));

            return new HistoricalMarketDataUpdater(new Mt5HistoricalMarketDataProvider(bridge));
        }

        private async Task<bool> CheckMarketDataMt5AvailableAsync()
        {
            MT5Bridge bridge = _bridge?.IsConnected == true
                ? _bridge
                : (_marketDataBridge ??= new MT5Bridge(_cfg.Mt5));

            return await bridge.PingAsync().ConfigureAwait(false);
        }

        private bool IsCriticalTradeExecutionInProgress()
        {
            lock (_signalExecutionLock)
                return _executingSignalIds.Count > 0;
        }

        private void UpdateMarketDataProgress(HistoricalMarketDataSyncProgress progress)
        {
            _pbMarketDataSync.Value = Math.Clamp(progress.Percent, 0, 100);
            _lblMarketDataSync.Text = MarketDataSyncStatusText.Format(progress);

            _lblMarketDataSync.ForeColor = progress.Status switch
            {
                HistoricalMarketDataSyncStatus.Failed => C_RED,
                HistoricalMarketDataSyncStatus.Cancelled => C_YELLOW,
                HistoricalMarketDataSyncStatus.Completed => C_GREEN,
                _ => C_MUTED
            };
        }

        private async void OnFormClosingAsync(object? sender, FormClosingEventArgs e)
        {
            _refreshTimer.Stop();
            _signalFeedPollTimer.Stop();
            _signalFeedWatcher?.Dispose();
            if (_marketDataSync != null)
                await _marketDataSync.DisposeAsync();
            _settings.StopWatching();
            if (_scalping != null)
            {
                await _scalping.StopAsync();
                _scalping = null;
                _scalpingTradeManager.Stop();
            }
            await StopClaudeAsync();
            await StopBotAsync();
            await _settings.SaveAsync(_cfg);
            if (!ReferenceEquals(_marketDataBridge, _bridge))
                _marketDataBridge?.Dispose();
            _bridge?.Dispose();
        }

        private void OnSettingsHotReloaded(AppSettings s)
        {
            _cfg = s;
            _pairSettings = new PairSettingsService(_settings, _cfg);
            _bot?.UpdateConfig(s.Bot);
            _claude?.UpdateConfig(s.Claude);
            _ = RestartMarketDataAutoSyncAsync();
            UIThread(() =>
            {
                SyncPairDropdownsFromPairSettings();
                RefreshPairSettingsGrid();
                Log("[CFG] Settings hot-reloaded from disk.");
            });
        }

        [System.Runtime.InteropServices.DllImport("Gdi32.dll")]
        private static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int cx, int cy);

        // ==========================================================
        //  STATIC DATA
        // ==========================================================
        private static string DefaultJsonSample() =>
            JsonConvert.SerializeObject(new TradeRequest
            {
                Pair       = "", TradeType = TradeType.BUY,
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
            2. The app automatically watches your selected signal folder
            3. Drop a .json file into the folder
            4. Click Detail/Play on a signal row to review and start trade

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
              - Checks SL -> breakeven (Trade Page BE trigger)
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
        {
            _lblTime.Text = $"UTC {DateTime.UtcNow:HH:mm:ss}  |  Local {DateTime.Now:HH:mm:ss}";
            UpdateEaDeploymentStatusBadge();
        }

        private async void RefreshTimer_Tick(object? sender, EventArgs e)  => await OnRefreshTickAsync();
        private async void BtnConnect_Click(object? sender, EventArgs e)    => await ConnectAsync();
        private async void BtnDisconnect_Click(object? sender, EventArgs e) => await DisconnectAsync();
        private void ChkAutoConn_CheckedChanged(object? sender, EventArgs e) => _cfg.AutoConnectOnLaunch = _chkAutoConn.Checked;

        private void TabControl_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_tabControl.SelectedTab == _tabPerformance)
                _ = RefreshPerformanceAsync();
            else if (_tabControl.SelectedTab == _tabPositions)
                _ = RefreshPositionsAsync();
            else if (_tabControl.SelectedTab == _tabHistory)
                _ = RefreshHistoryFromMt5Async();
        }

        private void CmbPair_SelectedIndexChanged(object? sender, EventArgs e)      => RecalcRR();
        private void CmbDir_SelectedIndexChanged(object? sender, EventArgs e)       { UpdateBuySellColors(); RecalcRR(); }
        private void CmbOrderType_SelectedIndexChanged(object? sender, EventArgs e) => _txtEntry.Enabled = _cmbOrderType.SelectedIndex != 0;
        private void TxtEntry_TextChanged(object? sender, EventArgs e) => RecalcRR();
        private void TxtSL_TextChanged(object? sender, EventArgs e)    => RecalcRR();
        private void TxtTP_TextChanged(object? sender, EventArgs e)    => RecalcRR();
        private void TxtLot_TextChanged(object? sender, EventArgs e)   => RecalcRR();

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

        private async void BtnImportHistory_Click(object? sender, EventArgs e) => await RefreshHistoryFromMt5Async();
        private void BtnClearHistory_Click(object? sender, EventArgs e)  => _gridHistory.Rows.Clear();

        private async void BtnStartBot_Click(object? sender, EventArgs e) => await StartBotAsync();
        private async void BtnStopBot_Click(object? sender, EventArgs e)    => await StopBotAsync();
        private async void BtnStopScalping_Click(object? sender, EventArgs e) => await StopScalpingAsync();

        private void BtnBotSettings_Click(object? sender, EventArgs e)
        {
            using var dlg = new ReviewTradeForm(_cfg.Bot);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _ = _settings.SaveAsync(_cfg);
                Log("[BOT] Trade settings saved.", C_ACCENT);
            }
        }

        private async void BtnAnalyzePairs_Click(object? sender, EventArgs e) => await AnalyzePairsAsync();

        private void CmbAllowedPair_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_suppressPairSelectionEvent) return;
            string pair = _cmbAllowedPair.SelectedItem?.ToString() ?? "";
            if (!string.IsNullOrEmpty(pair))
            {
                EnsureSignalFeedRowForPair(pair);
                _cfg.Bot = ReadBotConfigFromUI();
                _bot?.UpdateConfig(_cfg.Bot);
                _bot?.UpdateApiConfig(_cfg.ApiIntegrations);
                _ = _settings.SaveAsync(_cfg);
                Log($"[BOT] Manual pair selected: {pair}", C_ACCENT);
            }
        }

        private void BtnOpenFolder_Click(object? sender, EventArgs e)
        {
            string current = _txtWatchFolder.Text.Trim();
            string initial = Directory.Exists(current)
                ? current
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            using var dialog = new FolderBrowserDialog
            {
                Description = "Select the signal watch folder",
                InitialDirectory = initial,
                UseDescriptionForTitle = true,
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog(this) != DialogResult.OK ||
                string.IsNullOrWhiteSpace(dialog.SelectedPath))
            {
                return;
            }

            _txtWatchFolder.Text = dialog.SelectedPath;
            Directory.CreateDirectory(dialog.SelectedPath);
            Log($"[BOT] Watch folder selected: {dialog.SelectedPath}", C_ACCENT);
            _ = EnsureAutoWatcherAsync("watch folder changed");
        }

        private void BtnBotInstructions_Click(object? sender, EventArgs e)
        {
            using var dlg = new Form
            {
                Text            = "How It Works - Auto Bot",
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

        // â"€â"€ Signal Feed â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€

        private async Task RefreshSignalFeedAsync()
        {
            string root = _cfg.Bot.WatchFolder;
            if (!Directory.Exists(root)) return;

            await LoadSignalFolderToFeedAsync(Path.Combine(root, "error"),    SignalCardStatus.Error);
            await LoadSignalFolderToFeedAsync(Path.Combine(root, "rejected"), SignalCardStatus.Rejected);
            await LoadSignalFolderToFeedAsync(Path.Combine(root, "executed"), SignalCardStatus.Executed);
            await LoadSignalFolderToFeedAsync(root,                           SignalCardStatus.Pending);
            PruneMissingSignalCards(root);
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
                        RawJson    = json,
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

        private void EnsureSignalFeedWatcher(string folder)
        {
            if (_signalFeedWatcher != null &&
                string.Equals(_signalFeedWatcher.Path, folder, StringComparison.OrdinalIgnoreCase))
            {
                if (!_signalFeedPollTimer.Enabled)
                    _signalFeedPollTimer.Start();
                return;
            }

            _signalFeedPollTimer.Stop();
            _signalFeedWatcher?.Dispose();
            _signalFeedWatcher = null;

            if (!Directory.Exists(folder)) return;

            _signalFeedWatcher = new FileSystemWatcher(folder, "*.json")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                IncludeSubdirectories = false,
                EnableRaisingEvents = true
            };
            _signalFeedWatcher.Created += SignalFeedWatcherChanged;
            _signalFeedWatcher.Changed += SignalFeedWatcherChanged;
            _signalFeedWatcher.Deleted += SignalFeedWatcherChanged;
            _signalFeedWatcher.Renamed += SignalFeedWatcherChanged;
            _signalFeedWatcher.Error += (_, ex) =>
                Log($"[BOT] Signal feed watcher warning: {ex.GetException().Message}. Polling will continue.", C_YELLOW);

            _signalFeedPollTimer.Start();
        }

        private void SignalFeedWatcherChanged(object sender, FileSystemEventArgs e)
        {
            _ = RefreshSignalFeedAsync();
        }

        private void PruneMissingSignalCards(string root)
        {
            if (InvokeRequired) { Invoke(() => PruneMissingSignalCards(root)); return; }

            var cards = _flpSignals.Controls.OfType<Panel>().ToList();
            foreach (var card in cards)
            {
                if (card.Tag is not SignalCardInfo info) continue;
                if (info.Ticket > 0) continue;
                if (string.IsNullOrWhiteSpace(info.FilePath)) continue;
                if (!info.FilePath.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;
                if (File.Exists(info.FilePath)) continue;
                _flpSignals.Controls.Remove(card);
                card.Dispose();
            }
        }

        private async void SignalFeedPollTimer_Tick(object? sender, EventArgs e)
        {
            await RefreshSignalFeedAsync();
        }

        private void AddOrUpdateSignalCard(SignalCardInfo info)
        {
            if (InvokeRequired) { Invoke(() => AddOrUpdateSignalCard(info)); return; }

            var existing = _flpSignals.Controls.OfType<Panel>()
                .FirstOrDefault(p =>
                {
                    var ci = p.Tag as SignalCardInfo;
                    if (ci == null) return false;
                    if (ci.SignalId == info.SignalId) return true;
                    return !string.IsNullOrEmpty(info.FileName) && ci.FileName == info.FileName;
                });

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

            // â"€â"€ Row 1: direction+pair  |  action buttons â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€
            card.Controls.Add(new Label
            {
                Text      = $"{(isBuy ? "BUY" : "SELL")}  {info.Pair}",
                Font      = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold),
                ForeColor = dirColor,
                Location  = new Point(14, 8),
                AutoSize  = true
            });

            // X Delete button (always visible, disabled while Executing)
            var btnDel = MakeCardButton("X", Color.FromArgb(80, 30, 30), Color.FromArgb(252, 95, 95),
                "Delete - remove this signal card and file");
            btnDel.Anchor  = AnchorStyles.Top | AnchorStyles.Right;
            btnDel.Location = new Point(w - 28, 8);
            btnDel.Enabled  = info.Status != SignalCardStatus.Executing;
            btnDel.Tag      = "delete";
            btnDel.Click   += (_, _) => DeleteSignalCard(card);
            card.Controls.Add(btnDel);

            // Cls Close position button (only meaningful for Executed with ticket)
            var btnClose = MakeCardButton("Cls", Color.FromArgb(60, 20, 20), Color.FromArgb(252, 95, 95),
                "Close Position - close this trade on MT5");
            btnClose.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
            btnClose.Location = new Point(w - 56, 8);
            btnClose.Enabled  = info.Status == SignalCardStatus.Executed && info.Ticket > 0;
            btnClose.Tag      = "close";
            btnClose.Click   += (_, _) => _ = CloseTradeFromCardAsync(card);
            card.Controls.Add(btnClose);

            // Detail button - opens trade review dialog, does NOT immediately trade
            var btnExec = MakeCardButton("Detail", Color.FromArgb(20, 50, 30), Color.FromArgb(72, 199, 142),
                "Review - open trade details and approve before sending to MT5");
            btnExec.Size     = new Size(52, 22);
            btnExec.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
            btnExec.Location = new Point(w - 84, 8);
            btnExec.Enabled  = CanExecuteSignal(info);
            btnExec.Tag      = "execute";
            btnExec.Click   += (_, _) => _ = ExecuteSignalFromCardSafeAsync(card);
            card.Controls.Add(btnExec);

            // JSON button - opens the signal file in the default text editor
            var btnJson = MakeCardButton("JSON", Color.FromArgb(20, 30, 55), Color.FromArgb(130, 170, 255),
                "Open JSON - view the raw signal file in your default editor");
            btnJson.Size     = new Size(38, 22);
            btnJson.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
            btnJson.Location = new Point(w - 130, 8);
            btnJson.Tag      = "json";
            btnJson.Click   += (_, _) =>
            {
                if (card.Tag is not SignalCardInfo ci) return;
                string path = ResolveSignalFilePath(ci);
                string raw  = "";
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    try { raw = File.ReadAllText(path); }
                    catch (Exception ex) { Log($"[ERROR] Cannot read file: {ex.Message}", C_RED); return; }
                }
                else if (!string.IsNullOrWhiteSpace(ci.RawJson))
                {
                    raw = ci.RawJson;
                }
                else
                {
                    Log($"[INFO] Signal file not found: {ci.FileName}", C_YELLOW);
                    return;
                }
                using var dlg = new JsonViewForm(path, raw);
                dlg.ShowDialog(this);
            };
            card.Controls.Add(btnJson);

            var btnRules = MakeCardButton("Rules", Color.FromArgb(45, 45, 70), Color.FromArgb(210, 220, 255),
                "Open Rules Monitor - inspect decision rules for this signal");
            btnRules.Size     = new Size(44, 22);
            btnRules.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
            btnRules.Location = new Point(w - 180, 8);
            btnRules.Tag      = "rules";
            btnRules.Click   += (_, _) =>
            {
                if (card.Tag is SignalCardInfo ci)
                    OpenRulesMonitor(BuildSignalRulesContext(ci));
            };
            card.Controls.Add(btnRules);

            // Thin marquee progress bar - shown while async work is in progress
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

            // â"€â"€ Row 2: status label â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€
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

            // â"€â"€ Row 3: SL / TP / Lots â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€
            card.Controls.Add(new Label
            {
                Text      = $"SL: {info.StopLoss:F5}   TP: {info.TakeProfit:F5}   Lots: {info.LotSize:F2}",
                Font      = new Font("Consolas", 8.5F),
                ForeColor = Color.FromArgb(175, 175, 195),
                Location  = new Point(14, 54),
                AutoSize  = true
            });

            // â"€â"€ Row 4: timestamps â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€
            string genPart  = info.CreatedAt > DateTime.MinValue
                ? $"Gen: {info.CreatedAt:dd MMM HH:mm:ss}"
                : $"File: {info.Time:dd MMM HH:mm:ss}";
            string donePart = info.Status is SignalCardStatus.Executed
                                           or SignalCardStatus.Rejected
                                           or SignalCardStatus.Error
                ? $"   ->   Done: {info.Time:HH:mm:ss}"
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

            // â"€â"€ Row 5: filename + ID â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€
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
                    ? $"   ->   Done: {info.Time:HH:mm:ss}"
                    : "";
                lblTs.Text = genPart + donePart;
            }

            // Button visibility + enabled state
            foreach (var btn in card.Controls.OfType<Button>())
            {
                switch (btn.Tag?.ToString())
                {
                    case "json":
                        btn.Enabled = true;
                        break;
                    case "delete":
                        btn.Enabled = info.Status != SignalCardStatus.Executing;
                        break;
                    case "close":
                        btn.Enabled = info.Status == SignalCardStatus.Executed && info.Ticket > 0;
                        break;
                    case "execute":
                        btn.Enabled = CanExecuteSignal(info);
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
                    _ = PersistLifecycleAuditAsync(new TradeLifecycleAuditRecord
                    {
                        CreatedAtUtc = DateTime.UtcNow,
                        EventType = ok ? "CLOSE_REQUESTED" : "CLOSE_FAILED",
                        Ticket = position.Ticket,
                        PositionId = position.Ticket,
                        Pair = position.Symbol,
                        Direction = position.Type.ToString(),
                        Actor = "DesktopAutoClose",
                        Reason = ok
                            ? "Auto-close target reached."
                            : "Auto-close target reached but close request failed.",
                        Price = position.CurrentPrice,
                        ProfitUsd = position.Profit,
                        DetailsJson = JsonConvert.SerializeObject(new
                        {
                            position.ProfitPips,
                            position.Profit,
                            target.TargetPips,
                            target.TargetMoney,
                            trigger = target.TargetMoney > 0 ? "money" : target.TargetPips > 0 ? "pips" : "any_profit"
                        }, Formatting.None)
                    });
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
                liveSnapshot = await AwaitOrDefaultAsync(
                    _bridge.GetMarketSnapshotAsync(request, BuildReviewSnapshotBotConfig(request)),
                    "market snapshot").ConfigureAwait(false);
                account = await AwaitOrDefaultAsync(
                    _bridge.GetAccountInfoAsync(),
                    "account info").ConfigureAwait(false);
                symbol = await AwaitOrDefaultAsync(
                    _bridge.GetSymbolInfoAsync(request.Pair),
                    "symbol info").ConfigureAwait(false);
                positions = await AwaitOrDefaultAsync(
                    _bridge.GetPositionsAsync(),
                    "positions").ConfigureAwait(false) ?? [];
            }
            catch (Exception ex)
            {
                Log($"[BOT] Could not collect live MT5 review data: {ex.Message}", C_YELLOW);
            }

            JObject reviewSnapshot = liveSnapshot
                ?? JObject.Parse(BuildTradeReviewSnapshotJson(request, account, symbol, positions));
            if (liveSnapshot == null)
            {
                Log("[BOT] Review market snapshot unavailable; using fallback account/symbol data.", C_YELLOW);
                await TryEnrichReviewFallbackSnapshotAsync(reviewSnapshot, request, symbol).ConfigureAwait(false);
            }

            try
            {
                var news = await _newsCalendar.GetRiskSnapshotAsync(request.Pair, _cfg.ApiIntegrations).ConfigureAwait(false);
                reviewSnapshot["news"] = BuildReviewNewsJson(news);
            }
            catch (Exception ex)
            {
                Log($"[BOT] News snapshot unavailable for review: {ex.Message}", C_YELLOW);
            }

            string snapshot = reviewSnapshot.ToString(Formatting.Indented);

            if (InvokeRequired)
            {
                var completion = new TaskCompletionSource<TradeReviewDecision>();
                BeginInvoke(async () =>
                {
                    try
                    {
                        completion.SetResult(await ShowTradeReviewDialog(request, info, snapshot, symbol, account, positions));
                    }
                    catch (Exception ex)
                    {
                        completion.SetException(ex);
                    }
                });
                return await completion.Task.ConfigureAwait(false);
            }

            return await ShowTradeReviewDialog(request, info, snapshot, symbol, account, positions);

            async Task<T?> AwaitOrDefaultAsync<T>(Task<T> task, string label, int timeoutMs = 0)
            {
                if (timeoutMs <= 0)
                    timeoutMs = Math.Clamp(_cfg.Mt5.TimeoutMs + 1500, 4000, 10000);

                var completed = await Task.WhenAny(task, Task.Delay(timeoutMs)).ConfigureAwait(false);
                if (completed != task)
                {
                    Log($"[BOT] Review {label} timed out; opening detail with available data.", C_YELLOW);
                    return default;
                }

                return await task.ConfigureAwait(false);
            }
        }

        private async Task<TradeReviewDecision> ShowTradeReviewDialog(
            TradeRequest request,
            SignalCardInfo info,
            string snapshotJson,
            SymbolInfo? symbol,
            AccountInfo? account = null,
            IReadOnlyCollection<LivePosition>? reviewPositions = null)
        {
            TradeRequest activeRequest = request;
            var completion = new TaskCompletionSource<TradeReviewDecision>();
            var decisionCompleted = false;
            var form = new Form
            {
                Text = $"Trade Window - {request.TradeType} {request.Pair}",
                Size = new Size(1280, 900),
                MinimumSize = new Size(1080, 760),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Color.FromArgb(13, 13, 19),
                ForeColor = Color.FromArgb(218, 218, 230),
                Font = new Font("Segoe UI", 9F),
                Opacity = 0
            };
            AppIcon.ApplyTo(form);
            form.SuspendLayout();

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(14),
                BackColor = form.BackColor
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
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
            IReadOnlyCollection<LivePosition> latestPositions = reviewPositions ?? [];
            Func<double> getCurrentReviewLotSize = () => Math.Max(0.01, activeRequest.LotSize);

            var scrollHost = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = form.BackColor
            };
            root.Controls.Add(scrollHost, 0, 1);

            var contentStack = new FlowLayoutPanel
            {
                Dock = DockStyle.None,
                Location = new Point(0, 0),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(0, 0, 6, 16),
                BackColor = form.BackColor
            };
            scrollHost.Controls.Add(contentStack);
            EnableReviewDoubleBuffering(scrollHost);
            EnableReviewDoubleBuffering(contentStack);

            Action requestReviewContentResize = () => { };

            Panel MakeReviewExpander(string text, Control body, bool expanded = true)
            {
                bool isExpanded = expanded;
                var expander = new Panel
                {
                    Width = 900,
                    Height = expanded ? 260 : 36,
                    BackColor = Color.FromArgb(13, 13, 19),
                    Margin = new Padding(0, 0, 0, 10)
                };

                var header = new Button
                {
                    Location = new Point(0, 0),
                    Height = 34,
                    TextAlign = ContentAlignment.MiddleLeft,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(18, 22, 34),
                    ForeColor = Color.FromArgb(180, 220, 255),
                    Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                header.FlatAppearance.BorderColor = Color.FromArgb(48, 56, 72);
                header.FlatAppearance.BorderSize = 1;

                var bodyHost = new Panel
                {
                    Location = new Point(0, 42),
                    BackColor = Color.FromArgb(13, 13, 19),
                    Padding = new Padding(0),
                    Visible = expanded
                };
                body.Location = new Point(0, 0);
                bodyHost.Controls.Add(body);

                void LayoutExpander()
                {
                    int width = Math.Max(320, expander.Width);
                    header.SetBounds(0, 0, width, 34);
                    header.Text = isExpanded ? $"[-] {text}" : $"[+] {text}";

                    body.Width = width;
                    int bodyHeight = Math.Max(1, body.Height + 8);
                    bodyHost.SetBounds(0, 42, width, isExpanded ? bodyHeight : 0);
                    bodyHost.Visible = isExpanded;
                    expander.Height = isExpanded ? bodyHost.Bottom + 8 : 36;
                }

                header.Click += (_, _) =>
                {
                    isExpanded = !isExpanded;
                    LayoutExpander();
                    requestReviewContentResize();
                };
                expander.Resize += (_, _) => LayoutExpander();
                expander.Controls.Add(header);
                expander.Controls.Add(bodyHost);
                LayoutExpander();
                return expander;
            }

            var bindings = new List<(string Path, Label Value, string Format)>();
            var dashboard = BuildReviewDashboard(bindings, out var liveStatus, out var dashboardFlow, useInternalScroll: false);
            var dataExpander = MakeReviewExpander("Live Market Data Groups", dashboard, expanded: true);
            UpdateReviewExecutionBarrierSnapshot(currentSnapshot, request, request.LotSize, latestPositions, scalpingStrategy: false);
            RefreshReviewDashboard(currentSnapshot, bindings);

            bool fastRefreshing = false;
            bool contextRefreshing = false;
            bool slowRefreshing = false;
            DateTime lastFastSync = DateTime.MinValue;
            DateTime lastContextSync = DateTime.MinValue;
            DateTime lastSlowSync = DateTime.MinValue;

            void ReloadReviewConfig()
            {
                try
                {
                    _cfg.Bot = ReadBotConfigFromUISafe();
                    _pairSettings ??= new PairSettingsService(_settings, _cfg);
                }
                catch { }
            }

            void CommitReviewSnapshot(string lane)
            {
                UpdateReviewExecutionBarrierSnapshot(currentSnapshot, activeRequest, getCurrentReviewLotSize(), latestPositions, scalpingStrategy: false);
                latestSnapshotJson = currentSnapshot.ToString(Formatting.Indented);
                form.Tag = latestSnapshotJson;
                RefreshReviewDashboard(currentSnapshot, bindings);

                DateTime now = DateTime.Now;
                if (lane == "Fast") lastFastSync = now;
                else if (lane == "Context") lastContextSync = now;
                else if (lane == "Slow") lastSlowSync = now;
            }

            async Task RefreshReviewFastAsync()
            {
                if (fastRefreshing || _bridge == null || form.IsDisposed) return;
                fastRefreshing = true;
                try
                {
                    ReloadReviewConfig();
                    AccountInfo? acct = null;
                    SymbolInfo? sym = null;
                    List<LivePosition> pos = [];
                    try { acct = await _bridge.GetAccountInfoAsync(); } catch { }
                    try { sym = await _bridge.GetSymbolInfoAsync(activeRequest.Pair); } catch { }
                    try { pos = await _bridge.GetPositionsAsync(); } catch { }
                    latestPositions = pos;

                    var fastSnapshot = JObject.Parse(BuildTradeReviewSnapshotJson(activeRequest, acct, sym, pos));
                    MergeReviewSnapshotSections(currentSnapshot, fastSnapshot,
                        "collected_at_utc", "collected_at_pkt", "account", "price", "positions", "risk", "last_order");
                    PatchSnapshotSignalFields(currentSnapshot, activeRequest);
                    CommitReviewSnapshot("Fast");
                }
                catch (Exception ex)
                {
                    if (!form.IsDisposed)
                        liveStatus.Text = $"  {DateTime.Now:HH:mm:ss}  |  Fast refresh failed: {ex.Message}";
                }
                finally
                {
                    fastRefreshing = false;
                }
            }

            async Task RefreshReviewContextAsync()
            {
                if (contextRefreshing || _bridge == null || form.IsDisposed) return;
                contextRefreshing = true;
                try
                {
                    ReloadReviewConfig();
                    JObject? contextSnapshot = await _bridge.GetMarketSnapshotAsync(activeRequest, BuildReviewSnapshotBotConfig(activeRequest));
                    if (contextSnapshot != null && !form.IsDisposed)
                    {
                        MergeReviewSnapshotSections(currentSnapshot, contextSnapshot,
                            "collected_at_utc", "collected_at_pkt", "account", "price", "positions", "session", "candles", "indicators", "structure", "levels", "risk");
                        PatchSnapshotSignalFields(currentSnapshot, activeRequest);
                        CommitReviewSnapshot("Context");
                    }
                    else
                    {
                        await TryEnrichReviewFallbackSnapshotAsync(currentSnapshot, activeRequest, symbol).ConfigureAwait(false);
                        if (!form.IsDisposed)
                        {
                            PatchSnapshotSignalFields(currentSnapshot, activeRequest);
                            CommitReviewSnapshot("Context");
                        }
                        lastContextSync = DateTime.Now;
                    }
                }
                catch (Exception ex)
                {
                    if (!form.IsDisposed)
                        liveStatus.Text = $"  {DateTime.Now:HH:mm:ss}  |  Context refresh failed: {ex.Message}";
                }
                finally
                {
                    contextRefreshing = false;
                }
            }

            async Task RefreshReviewSlowAsync()
            {
                if (slowRefreshing || _bridge == null || form.IsDisposed) return;
                slowRefreshing = true;
                try
                {
                    ReloadReviewConfig();
                    JObject? slowSnapshot = await _bridge.GetMarketSnapshotAsync(activeRequest, BuildReviewSnapshotBotConfig(activeRequest));
                    if (slowSnapshot != null && !form.IsDisposed)
                    {
                        MergeReviewSnapshotSections(currentSnapshot, slowSnapshot,
                            "account", "symbol", "price", "positions", "news", "history", "pair_rules", "risk");
                    }
                    else
                    {
                        SymbolInfo? sym = null;
                        try { sym = await _bridge.GetSymbolInfoAsync(activeRequest.Pair); } catch { }
                        var fallback = JObject.Parse(BuildTradeReviewSnapshotJson(activeRequest, null, sym, latestPositions));
                        MergeReviewSnapshotSections(currentSnapshot, fallback, "symbol", "news", "history", "pair_rules");
                    }

                    var news = await _newsCalendar.GetRiskSnapshotAsync(activeRequest.Pair, _cfg.ApiIntegrations);
                    currentSnapshot["news"] = BuildReviewNewsJson(news);

                    PatchSnapshotSignalFields(currentSnapshot, activeRequest);
                    CommitReviewSnapshot("Slow");
                }
                catch (Exception ex)
                {
                    if (!form.IsDisposed)
                        liveStatus.Text = $"  {DateTime.Now:HH:mm:ss}  |  Slow refresh failed: {ex.Message}";
                }
                finally
                {
                    slowRefreshing = false;
                }
            }

            var fastTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            fastTimer.Tick += async (_, _) => await RefreshReviewFastAsync();
            form.FormClosed += (_, _) => fastTimer.Stop();

            var contextTimer = new System.Windows.Forms.Timer { Interval = 5000 };
            contextTimer.Tick += async (_, _) => await RefreshReviewContextAsync();
            form.FormClosed += (_, _) => contextTimer.Stop();

            var slowTimer = new System.Windows.Forms.Timer { Interval = 60000 };
            slowTimer.Tick += async (_, _) => await RefreshReviewSlowAsync();
            form.FormClosed += (_, _) => slowTimer.Stop();

            var clockTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            clockTimer.Tick += (_, _) =>
            {
                if (form.IsDisposed) return;
                liveStatus.Text =
                    $"  {DateTime.Now:HH:mm:ss}  |  Fast: {FormatReviewSyncAge(lastFastSync)}  |  Context: {FormatReviewSyncAge(lastContextSync)}  |  Slow: {FormatReviewSyncAge(lastSlowSync)}";
            };
            form.FormClosed += (_, _) => clockTimer.Stop();

            // â"€â"€ Row 2: two-row host (lot/leverage + auto-close) â"€â"€â"€â"€
            var row2Host = new TableLayoutPanel
            {
                ColumnCount = 2,
                RowCount = 2,
                BackColor = form.BackColor,
                Padding = new Padding(0),
                Margin = new Padding(0, 0, 0, 10)
            };
            row2Host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            row2Host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            row2Host.RowStyles.Add(new RowStyle(SizeType.Absolute, 124));
            row2Host.RowStyles.Add(new RowStyle(SizeType.Absolute, 300));
            contentStack.Controls.Add(row2Host);

            FlowLayoutPanel lotPanel = null!;
            FlowLayoutPanel scalpPanel = null!;
            FlowLayoutPanel normalPanel = null!;

            void ResizeReviewContent()
            {
                int width = Math.Max(980, scrollHost.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 10);
                contentStack.Width = width;
                dataExpander.Width = width;
                dashboard.Width = width;
                dashboardFlow.Width = width;
                ResizeReviewGroups(dashboardFlow);
                dashboardFlow.PerformLayout();
                int dashboardGroupsHeight = MeasureFlowPanelHeight(dashboardFlow, 220);
                dashboardFlow.Height = dashboardGroupsHeight;
                dashboard.Height = dashboardGroupsHeight + 38;
                if (dataExpander.Controls.Count > 1 && dataExpander.Controls[1] is Panel dataBody && dataBody.Visible)
                {
                    dataBody.SetBounds(0, 42, width, dashboard.Height + 8);
                    dataExpander.Height = dataBody.Bottom + 8;
                }
                else
                {
                    dataExpander.Height = 36;
                }
                row2Host.Width = width;
                int strategyColumnWidth = Math.Max(420, (width - 16) / 2);
                if (lotPanel == null || scalpPanel == null || normalPanel == null)
                    return;

                lotPanel.Width = Math.Max(1, width - 8);
                scalpPanel.Width = strategyColumnWidth;
                normalPanel.Width = strategyColumnWidth;

                lotPanel.PerformLayout();
                scalpPanel.PerformLayout();
                normalPanel.PerformLayout();

                int commonHeight = MeasureFlowPanelHeight(lotPanel, 94) + 8;
                int strategyHeight = Math.Max(
                    172,
                    Math.Max(
                        MeasureFlowPanelHeight(scalpPanel, 154),
                        MeasureFlowPanelHeight(normalPanel, 154)) + 12);

                row2Host.RowStyles[0].Height = commonHeight;
                row2Host.RowStyles[1].Height = strategyHeight;
                row2Host.Height = commonHeight + strategyHeight + 12;
                contentStack.PerformLayout();
                scrollHost.AutoScrollMinSize = new Size(width, MeasureFlowPanelHeight(contentStack, contentStack.Height) + 18);
            }
            requestReviewContentResize = ResizeReviewContent;

            scrollHost.Resize += (_, _) => ResizeReviewContent();
            form.Load += (_, _) => ResizeReviewContent();
            bool reviewRefreshStarted = false;
            form.Shown += (_, _) =>
            {
                ResizeReviewContent();
                form.BeginInvoke(new Action(async () =>
                {
                    ResizeReviewContent();
                    form.Opacity = 1;
                    if (!reviewRefreshStarted)
                    {
                        reviewRefreshStarted = true;
                        await Task.Delay(120).ConfigureAwait(true);
                        if (form.IsDisposed)
                            return;

                        fastTimer.Start();
                        contextTimer.Start();
                        slowTimer.Start();
                        clockTimer.Start();
                        _ = RefreshReviewFastAsync();
                        _ = RefreshReviewContextAsync();
                        _ = RefreshReviewSlowAsync();
                    }
                }));
            };

            void PaintSettingsPanel(Panel panel, Color accent, PaintEventArgs e)
            {
                using var border = new Pen(Color.FromArgb(48, 56, 72));
                using var accentPen = new Pen(accent, 2);
                e.Graphics.DrawRectangle(border, 0, 0, panel.Width - 1, panel.Height - 1);
                e.Graphics.DrawLine(accentPen, 0, 0, panel.Width - 1, 0);
            }

            const int StrategyRowWidth = 408;
            const int StrategyLabelWidth = 84;
            const int StrategyInputWidth = 104;
            const int StrategyFieldWidth = 198;

            Label MakeCompactLabel(string text, int width = StrategyLabelWidth) => new()
            {
                Text = text,
                AutoSize = false,
                Size = new Size(width, 28),
                ForeColor = Color.FromArgb(190, 195, 210),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 2, 6, 0)
            };

            FlowLayoutPanel MakeSettingField(
                string label,
                Control control,
                int labelWidth = StrategyLabelWidth,
                int fieldWidth = StrategyFieldWidth,
                int inputWidth = StrategyInputWidth)
            {
                control.Width = inputWidth;
                control.Margin = new Padding(0, 2, 0, 0);
                var field = new FlowLayoutPanel
                {
                    AutoSize = false,
                    Size = new Size(fieldWidth, 32),
                    FlowDirection = FlowDirection.LeftToRight,
                    WrapContents = false,
                    Margin = new Padding(0, 0, 8, 2),
                    BackColor = Color.Transparent
                };
                field.Controls.Add(MakeCompactLabel(label, labelWidth));
                field.Controls.Add(control);
                return field;
            }

            FlowLayoutPanel MakeSettingRow(params Control[] controls)
            {
                var row = new FlowLayoutPanel
                {
                    AutoSize = false,
                    Size = new Size(StrategyRowWidth, 36),
                    FlowDirection = FlowDirection.LeftToRight,
                    WrapContents = false,
                    Margin = new Padding(0, 0, 0, 3),
                    BackColor = Color.Transparent
                };
                row.Controls.AddRange(controls);
                return row;
            }

            FlowLayoutPanel MakeSettingGroup(params Control[] rows)
            {
                var group = new FlowLayoutPanel
                {
                    AutoSize = false,
                    Size = new Size(StrategyRowWidth, rows.Length * 36 + 2),
                    FlowDirection = FlowDirection.TopDown,
                    WrapContents = false,
                    Margin = new Padding(0, 2, 0, 5),
                    Padding = new Padding(0),
                    BackColor = Color.FromArgb(22, 24, 34)
                };
                group.Controls.AddRange(rows);
                return group;
            }

            Label MakeStrategyTitle(string text, Color color) => new()
            {
                Text = text,
                AutoSize = false,
                Size = new Size(StrategyFieldWidth, 30),
                ForeColor = color,
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 0, 8, 3)
            };

            // â"€â"€ Row 2a: Lot size + Leverage â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€
            lotPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true, Padding = new Padding(10),
                BackColor = Color.FromArgb(17, 19, 28),
                Margin = new Padding(4)
            };
            lotPanel.Paint += (_, e) => PaintSettingsPanel(lotPanel, Color.FromArgb(120, 170, 220), e);
            row2Host.Controls.Add(lotPanel, 0, 0);
            row2Host.SetColumnSpan(lotPanel, 2);

            var lotOptions = BuildReviewLotOptions(symbol?.Symbol ?? request.Pair);

            var cmbLotSize = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 320,
                Height = 28,
                BackColor = Color.FromArgb(18, 20, 32),
                ForeColor = Color.FromArgb(218, 218, 230),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(6, 4, 0, 0)
            };
            cmbLotSize.Items.AddRange(lotOptions);
            cmbLotSize.SelectedItem = lotOptions
                .Where(o => !o.IsAutoFromRisk)
                .OrderBy(o => Math.Abs(o.Size - Math.Max(0.01, request.LotSize)))
                .FirstOrDefault() ?? lotOptions.First();

            var leverageOptions = new[] { "1:50", "1:100", "1:200", "1:500", "1:1000" };
            var cmbLeverage = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 88, Height = 28,
                BackColor = Color.FromArgb(18, 20, 32),
                ForeColor = Color.FromArgb(218, 218, 230),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(6, 4, 0, 0)
            };

            int acctLev = account?.Leverage > 0 ? account.Leverage : 100;
            string acctLevItem = $"1:{acctLev}";
            cmbLeverage.Items.AddRange(leverageOptions);
            if (!leverageOptions.Contains(acctLevItem))
                cmbLeverage.Items.Insert(0, acctLevItem);
            cmbLeverage.SelectedItem = acctLevItem;
            if (cmbLeverage.SelectedIndex < 0) cmbLeverage.SelectedIndex = 0;

            lotPanel.Controls.Add(MakeInlineLabel("Common Trade Settings"));
            lotPanel.Controls.Add(MakeInlineLabel("Lot size"));
            lotPanel.Controls.Add(cmbLotSize);
            lotPanel.Controls.Add(MakeInlineLabel("   Leverage"));
            lotPanel.Controls.Add(cmbLeverage);

            double entryForCalc = symbol != null
                ? (request.TradeType == TradeType.BUY ? symbol.Ask : symbol.Bid)
                : 0;
            double equityForCalc = account?.Equity ?? 0;

            cmbLeverage.SelectedIndexChanged += (_, _) =>
            {
                string levStr = cmbLeverage.SelectedItem?.ToString() ?? "1:100";
                int colon = levStr.IndexOf(':');
                if (colon < 0 || !int.TryParse(levStr[(colon + 1)..], out int lev)) lev = 100;
                if (equityForCalc <= 0 || entryForCalc <= 0 || activeRequest.StopLoss <= 0) return;
                double baseLots = LotCalculator.Calculate(equityForCalc, _cfg.Bot.MaxRiskPercent, entryForCalc, activeRequest.StopLoss, activeRequest.Pair);
                double scaledLots = BrokerLotSizeValidator.Normalize(baseLots * lev / 100.0, symbol);
                cmbLotSize.SelectedItem = lotOptions.Where(o => !o.IsAutoFromRisk).OrderBy(o => Math.Abs(o.Size - scaledLots)).First();
            };

            // â"€â"€ Row 2b: Auto-close â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€
            var autoPanel = lotPanel;

            var cmbTradingMode = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 130,
                Height = 28,
                BackColor = Color.FromArgb(18, 20, 32),
                ForeColor = Color.FromArgb(218, 218, 230),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F),
                Margin = new Padding(6, 4, 0, 0)
            };
            cmbTradingMode.Items.AddRange(new object[] { "Auto", "Manual Approval", "Paper Trading" });
            cmbTradingMode.SelectedIndex = _cfg.Bot.CommonTrading.TradingMode switch
            {
                TradingControlMode.Auto          => 0,
                TradingControlMode.ManualApproval => 1,
                TradingControlMode.PaperTrading  => 2,
                _                                => 1
            };
            var chkCommonAi = new CheckBox
            {
                Text = "AI confirm",
                AutoSize = false,
                Size = new Size(92, 28),
                ForeColor = Color.FromArgb(210, 150, 255),
                BackColor = form.BackColor,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F)
            };
            chkCommonAi.Checked = _cfg.Bot.CommonTrading.UseAiConfirmation;
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
            chkAutoClose.Checked = _cfg.Bot.CommonTrading.AutoCloseAfterOpen;
            var nudPips = MakeReviewNumber(0, 10000, 0.5M, 1, 82);
            var nudMoney = MakeReviewNumber(0, 100000, 0.10M, 2, 92);
            var nudBeTrigger = MakeReviewNumber(0.10M, 1.00M, 0.05M, 2, 64);
            nudPips.Value = Math.Min(nudPips.Maximum, Math.Max(nudPips.Minimum, (decimal)_cfg.Bot.CommonTrading.ProfitTargetPips));
            nudMoney.Value = Math.Min(nudMoney.Maximum, Math.Max(nudMoney.Minimum, (decimal)_cfg.Bot.CommonTrading.ProfitTargetUsd));
            nudBeTrigger.Value = Math.Min(nudBeTrigger.Maximum, Math.Max(nudBeTrigger.Minimum, (decimal)_cfg.Bot.CommonTrading.BeTriggerPercentOfTp));
            var btnCommonReset = new Button
            {
                Text = "Reset",
                AutoSize = false,
                Size = new Size(58, 24),
                BackColor = Color.FromArgb(34, 46, 58),
                ForeColor = Color.FromArgb(140, 210, 255),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 8F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(4, 5, 10, 0)
            };
            btnCommonReset.FlatAppearance.BorderSize = 0;
            _cardTooltip.SetToolTip(nudPips, "Global profit target in pips.");
            _cardTooltip.SetToolTip(nudMoney, "0 = disabled. If auto-close is enabled and both targets are 0, close on any profit.");
            _cardTooltip.SetToolTip(nudBeTrigger, "Move SL to break-even after this fraction of the selected Trade Page TP pips.");
            _cardTooltip.SetToolTip(btnCommonReset, "Reset shared trade controls and refresh strategy defaults.");
            autoPanel.Controls.Add(MakeInlineLabel("Trading Mode"));
            autoPanel.Controls.Add(cmbTradingMode);
            autoPanel.Controls.Add(chkCommonAi);
            autoPanel.Controls.Add(chkAutoClose);
            autoPanel.Controls.Add(MakeInlineLabel("Pips target"));
            autoPanel.Controls.Add(nudPips);
            autoPanel.Controls.Add(MakeInlineLabel("Money target"));
            autoPanel.Controls.Add(nudMoney);
            autoPanel.Controls.Add(MakeInlineLabel("BE Trigger % of TP"));
            autoPanel.Controls.Add(nudBeTrigger);
            autoPanel.Controls.Add(MakeInlineLabel("0 = disabled"));
            autoPanel.Controls.Add(btnCommonReset);

            scalpPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
                WrapContents = false, Padding = new Padding(10),
                BackColor = Color.FromArgb(24, 20, 12),
                Margin = new Padding(4)
            };
            scalpPanel.Paint += (_, e) => PaintSettingsPanel(scalpPanel, Color.FromArgb(235, 170, 55), e);
            row2Host.Controls.Add(scalpPanel, 0, 1);

            var chkAutoScalp = new CheckBox
            {
                Text = "Enable Scalping",
                AutoSize = false,
                Size = new Size(126, 28),
                ForeColor = Color.Gold,
                BackColor = Color.FromArgb(50, 42, 20),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold)
            };
            chkAutoScalp.Checked = _cfg.Bot.Scalping.Enabled;
            var cmbScalpMode = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 104,
                Height = 28,
                BackColor = Color.FromArgb(18, 20, 32),
                ForeColor = Color.FromArgb(218, 218, 230),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F),
                Margin = new Padding(6, 4, 0, 0)
            };
            cmbScalpMode.Items.AddRange(new object[] { "Auto", "Signal", "Buy only", "Sell only" });
            cmbScalpMode.SelectedIndex = _cfg.Bot.Scalping.DirectionMode switch
            {
                ScalpingDirectionMode.SignalDirection => 1,
                ScalpingDirectionMode.BuyOnly => 2,
                ScalpingDirectionMode.SellOnly => 3,
                _ => 0
            };
            var nudScalpTrades = MakeReviewNumber(1, 50, 1, 0, 52);
            var nudScalpMinutes = MakeReviewNumber(1, 240, 1, 0, 52);
            var nudScalpSl = MakeReviewNumber(1, 500, 0.5M, 1, 58);
            var nudScalpTp = MakeReviewNumber(1, 500, 0.5M, 1, 58);
            var nudScalpSlMoney = MakeReviewNumber(0, 100000, 0.10M, 2, 78);
            var nudScalpTpMoney = MakeReviewNumber(0, 100000, 0.10M, 2, 78);
            var nudScalpRr = MakeReviewNumber(0.1M, 20, 0.1M, 1, 58);
            var nudScalpSpread = MakeReviewNumber(0.1M, 100, 0.1M, 1, 58);
            var btnScalpReset = new Button
            {
                Text = "Reset",
                AutoSize = false,
                Size = new Size(58, 24),
                BackColor = Color.FromArgb(34, 46, 58),
                ForeColor = Color.FromArgb(140, 210, 255),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 8F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(4, 5, 10, 0)
            };
            btnScalpReset.FlatAppearance.BorderSize = 0;
            _cardTooltip.SetToolTip(btnScalpReset, "Reset scalping values to bot suggestions for this pair.");
            var btnStartScalping = MakeDialogButton(_scalping?.IsRunning == true ? "Stop Scalping" : "Start Scalping", Color.FromArgb(150, 88, 18));
            btnStartScalping.ForeColor = Color.FromArgb(255, 220, 140);
            var lblScalpActualRr = new Label
            {
                Text = "-",
                ForeColor = Color.FromArgb(170, 220, 170),
                AutoSize = false,
                Size = new Size(StrategyInputWidth, 28),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 2, 0, 0)
            };
            var lblScalpingStatus = new Label
            {
                Text = $"Scalping Status: {(_scalping?.IsRunning == true ? "Running" : "Stopped")}",
                ForeColor = _scalping?.IsRunning == true ? Color.Gold : Color.FromArgb(145, 150, 165),
                AutoSize = false,
                Size = new Size(190, 28),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(6, 4, 0, 0)
            };

            void RefreshScalpingReviewRunState()
            {
                bool running = _scalping?.IsRunning == true;
                btnStartScalping.Text = running ? "Stop Scalping" : "Start Scalping";
                lblScalpingStatus.Text = $"Scalping Status: {(running ? "Running" : "Stopped")}";
                lblScalpingStatus.ForeColor = running ? Color.Gold : Color.FromArgb(145, 150, 165);
                _btnStopScalping.Enabled = running;
                if (running)
                    chkAutoScalp.Checked = true;
            }

            RefreshScalpingReviewRunState();
            decimal Clamp(NumericUpDown nud, decimal value) =>
                Math.Min(nud.Maximum, Math.Max(nud.Minimum, value));

            void ApplyScalpingConfigToControls(ScalpingConfig config)
            {
                cmbScalpMode.SelectedIndex = config.DirectionMode switch
                {
                    ScalpingDirectionMode.SignalDirection => 1,
                    ScalpingDirectionMode.BuyOnly => 2,
                    ScalpingDirectionMode.SellOnly => 3,
                    _ => 0
                };
                nudScalpTrades.Value = Clamp(nudScalpTrades, config.MaxTrades);
                nudScalpMinutes.Value = Clamp(nudScalpMinutes, config.MaxMinutes);
                nudScalpSl.Value = Clamp(nudScalpSl, (decimal)config.StopLossPips);
                nudScalpTp.Value = Clamp(nudScalpTp, (decimal)config.TakeProfitPips);
                nudScalpRr.Value = Clamp(nudScalpRr, (decimal)Math.Max(0.1, config.RiskRewardRatio));
                nudScalpSpread.Value = Clamp(nudScalpSpread, (decimal)config.MaxSpreadPips);
            }

            var savedScalping = GetSavedScalpingConfigForPair(request.Pair);
            var suggestedScalping = BuildSuggestedScalpingConfigForPair(request.Pair, symbol);
            var initialScalping = savedScalping == null
                ? suggestedScalping
                : MergeSavedScalpingPreferences(savedScalping, suggestedScalping);
            ApplyScalpingConfigToControls(initialScalping);
            if (savedScalping == null)
            {
                Log(
                    $"[SCALP] Bot suggested values for {NormalizePairKey(request.Pair)}: " +
                    $"SL {initialScalping.StopLossPips:F1} pips, TP {initialScalping.TakeProfitPips:F1} pips, " +
                    $"max spread {initialScalping.MaxSpreadPips:F1} pips.",
                    C_ACCENT);
            }
            else
            {
                Log(
                    $"[SCALP] Refreshed live scalping values for {NormalizePairKey(request.Pair)}: " +
                    $"SL {initialScalping.StopLossPips:F1} pips, TP {initialScalping.TakeProfitPips:F1} pips, " +
                    $"max spread {initialScalping.MaxSpreadPips:F1} pips. Session preferences came from saved settings.",
                    C_ACCENT);
            }

            chkAutoScalp.Margin = new Padding(0, 1, 8, 3);
            btnScalpReset.Margin = new Padding(0, 2, 8, 0);
            btnStartScalping.Margin = new Padding(0, 0, 10, 0);
            lblScalpingStatus.Margin = new Padding(0, 3, 0, 0);
            scalpPanel.Controls.Add(MakeSettingRow(MakeStrategyTitle("Scalping Strategy", Color.Gold), chkAutoScalp));
            scalpPanel.Controls.Add(MakeSettingRow(
                MakeSettingField("Mode", cmbScalpMode),
                MakeSettingField("Max trades", nudScalpTrades)));
            scalpPanel.Controls.Add(MakeSettingRow(
                MakeSettingField("Duration", nudScalpMinutes)));
            scalpPanel.Controls.Add(MakeSettingRow(
                MakeSettingField("TP pips", nudScalpTp),
                MakeSettingField("TP $", nudScalpTpMoney)));
            scalpPanel.Controls.Add(MakeSettingRow(
                MakeSettingField("SL pips", nudScalpSl),
                MakeSettingField("SL $", nudScalpSlMoney)));
            scalpPanel.Controls.Add(MakeSettingGroup(
                MakeSettingRow(
                    MakeSettingField("Spread", nudScalpSpread),
                    MakeSettingField("Min R:R", nudScalpRr)),
                MakeSettingRow(MakeSettingField("Actual R:R", lblScalpActualRr))));
            scalpPanel.Controls.Add(MakeSettingRow(lblScalpingStatus));
            scalpPanel.Controls.Add(MakeSettingRow(btnStartScalping, btnScalpReset));

            normalPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(10),
                BackColor = Color.FromArgb(13, 17, 26),
                Margin = new Padding(4)
            };
            normalPanel.Paint += (_, e) => PaintSettingsPanel(normalPanel, Color.FromArgb(90, 160, 245), e);
            row2Host.Controls.Add(normalPanel, 1, 1);

            var chkNormalTrading = new CheckBox
            {
                Text = "Enable Normal Trading",
                AutoSize = false,
                Size = new Size(154, 28),
                ForeColor = Color.FromArgb(135, 190, 255),
                BackColor = Color.FromArgb(24, 36, 54),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold)
            };
            var nudNormalTrades = MakeReviewNumber(1, 50, 1, 0, 52);
            var nudNormalExpiry = MakeReviewNumber(1, 10080, 1, 0, 62);
            var nudNormalSl = MakeReviewNumber(1, 5000, 0.5M, 1, 58);
            var nudNormalTp = MakeReviewNumber(1, 10000, 0.5M, 1, 58);
            var nudNormalSlMoney = MakeReviewNumber(0, 100000, 0.10M, 2, 78);
            var nudNormalTpMoney = MakeReviewNumber(0, 100000, 0.10M, 2, 78);
            var nudNormalSpread = MakeReviewNumber(0.1M, 500, 0.1M, 1, 58);
            var nudNormalRr = MakeReviewNumber(0.1M, 20, 0.1M, 1, 58);
            var lblNormalActualRr = new Label
            {
                Text = "-",
                ForeColor = Color.FromArgb(170, 220, 170),
                AutoSize = false,
                Size = new Size(StrategyInputWidth, 28),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 2, 0, 0)
            };
            var btnStartNormal = MakeDialogButton(_normalTradeManager.IsRunning ? "Stop Normal Trading" : "Start Normal Trading", Color.FromArgb(24, 82, 150));
            btnStartNormal.ForeColor = Color.FromArgb(170, 215, 255);
            var lblNormalStatus = new Label
            {
                Text = $"Normal Trading Status: {(_normalTradeManager.IsRunning ? "Running" : "Stopped")}",
                ForeColor = _normalTradeManager.IsRunning ? Color.FromArgb(130, 190, 255) : Color.FromArgb(145, 150, 165),
                AutoSize = false,
                Size = new Size(220, 28),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(6, 4, 0, 0)
            };

            void ApplyNormalTradingSettingsToControls(NormalTradingSettings settings)
            {
                chkNormalTrading.Checked = settings.Enabled;
                nudNormalTrades.Value = Clamp(nudNormalTrades, settings.MaxTrades);
                nudNormalExpiry.Value = Clamp(nudNormalExpiry, settings.ExpiryMinutes);
                nudNormalSl.Value = Clamp(nudNormalSl, (decimal)settings.StopLossPips);
                nudNormalTp.Value = Clamp(nudNormalTp, (decimal)settings.TakeProfitPips);
                nudNormalSpread.Value = Clamp(nudNormalSpread, (decimal)settings.MaxSpreadPips);
                nudNormalRr.Value = Clamp(nudNormalRr, (decimal)Math.Max(0.1, settings.RiskRewardRatio));
            }

            ApplyNormalTradingSettingsToControls(GetSavedNormalTradingSettingsForPair(request.Pair) ?? CloneNormalTradingSettings(_cfg.Bot.NormalTrading));
            chkNormalTrading.Margin = new Padding(0, 1, 8, 3);
            btnStartNormal.Margin = new Padding(0, 0, 10, 0);
            lblNormalStatus.Margin = new Padding(0, 3, 0, 0);
            normalPanel.Controls.Add(MakeSettingRow(MakeStrategyTitle("Normal Trading Strategy", Color.FromArgb(135, 190, 255)), chkNormalTrading));
            normalPanel.Controls.Add(MakeSettingRow(
                MakeSettingField("Max trades", nudNormalTrades),
                MakeSettingField("Expiry", nudNormalExpiry)));
            normalPanel.Controls.Add(MakeSettingRow(
                MakeSettingField("TP pips", nudNormalTp),
                MakeSettingField("TP $", nudNormalTpMoney)));
            normalPanel.Controls.Add(MakeSettingRow(
                MakeSettingField("SL pips", nudNormalSl),
                MakeSettingField("SL $", nudNormalSlMoney)));
            normalPanel.Controls.Add(MakeSettingGroup(
                MakeSettingRow(
                    MakeSettingField("Spread", nudNormalSpread),
                    MakeSettingField("Min R:R", nudNormalRr)),
                MakeSettingRow(MakeSettingField("Actual R:R", lblNormalActualRr))));
            normalPanel.Controls.Add(MakeSettingRow(btnStartNormal));
            normalPanel.Controls.Add(MakeSettingRow(lblNormalStatus));
            contentStack.Controls.Add(dataExpander);

            bool syncing = false;
            bool scalpSyncing = false;
            bool normalSyncing = false;
            double price = symbol?.Ask > 0 ? symbol.Ask : 1.0;
            string sym = symbol?.Symbol ?? request.Pair;
            double PipValue() => Math.Max(0.0001, GetSelectedReviewLotSize() * LotCalculator.GetPipValuePerLot(sym.ToUpperInvariant(), price));

            void SyncScalpMoneyFromPips()
            {
                decimal pipValue = (decimal)PipValue();
                nudScalpSlMoney.Value = Math.Min(nudScalpSlMoney.Maximum, Math.Round(nudScalpSl.Value * pipValue, 2));
                nudScalpTpMoney.Value = Math.Min(nudScalpTpMoney.Maximum, Math.Round(nudScalpTp.Value * pipValue, 2));
                UpdateScalpActualRrLabel();
            }

            void SyncNormalMoneyFromPips()
            {
                decimal pipValue = (decimal)PipValue();
                nudNormalSlMoney.Value = Math.Min(nudNormalSlMoney.Maximum, Math.Round(nudNormalSl.Value * pipValue, 2));
                nudNormalTpMoney.Value = Math.Min(nudNormalTpMoney.Maximum, Math.Round(nudNormalTp.Value * pipValue, 2));
                UpdateNormalActualRrLabel();
            }

            decimal MinScalpTp() => Math.Min(nudScalpTp.Maximum, Math.Max(nudScalpTp.Minimum, Math.Round(nudScalpSl.Value * nudScalpRr.Value, 1)));
            decimal MinNormalTp() => Math.Min(nudNormalTp.Maximum, Math.Max(nudNormalTp.Minimum, Math.Round(nudNormalSl.Value * nudNormalRr.Value, 1)));

            void EnsureScalpTpAtMinimum()
            {
                decimal minTp = MinScalpTp();
                if (nudScalpTp.Value < minTp)
                    nudScalpTp.Value = minTp;
            }

            void EnsureNormalTpAtMinimum()
            {
                decimal minTp = MinNormalTp();
                if (nudNormalTp.Value < minTp)
                    nudNormalTp.Value = minTp;
            }

            void UpdateScalpActualRrLabel()
            {
                decimal actual = nudScalpSl.Value > 0 ? nudScalpTp.Value / nudScalpSl.Value : 0;
                lblScalpActualRr.Text = $"{actual:0.00}";
                lblScalpActualRr.ForeColor = actual >= nudScalpRr.Value * 1.15M
                    ? Color.FromArgb(144, 238, 170)
                    : actual >= nudScalpRr.Value
                        ? Color.FromArgb(250, 199, 117)
                        : Color.FromArgb(252, 95, 95);
            }

            void UpdateNormalActualRrLabel()
            {
                decimal actual = nudNormalSl.Value > 0 ? nudNormalTp.Value / nudNormalSl.Value : 0;
                lblNormalActualRr.Text = $"{actual:0.00}";
                lblNormalActualRr.ForeColor = actual >= nudNormalRr.Value * 1.15M
                    ? Color.FromArgb(144, 238, 170)
                    : actual >= nudNormalRr.Value
                        ? Color.FromArgb(250, 199, 117)
                        : Color.FromArgb(252, 95, 95);
            }

            scalpSyncing = true;
            EnsureScalpTpAtMinimum();
            SyncScalpMoneyFromPips();
            scalpSyncing = false;
            normalSyncing = true;
            EnsureNormalTpAtMinimum();
            SyncNormalMoneyFromPips();
            normalSyncing = false;

            nudPips.ValueChanged += (_, _) =>
            {
                if (syncing) return;
                syncing = true;
                nudMoney.Value = Math.Min(nudMoney.Maximum, Math.Round(nudPips.Value * (decimal)PipValue(), 2));
                syncing = false;
            };
            nudMoney.ValueChanged += (_, _) =>
            {
                if (syncing) return;
                syncing = true;
                nudPips.Value = Math.Min(nudPips.Maximum, Math.Round(nudMoney.Value / (decimal)PipValue(), 1));
                syncing = false;
            };
            cmbLotSize.SelectedIndexChanged += (_, _) =>
            {
                if (syncing) return;
                syncing = true;
                nudMoney.Value = Math.Min(nudMoney.Maximum, Math.Round(nudPips.Value * (decimal)PipValue(), 2));
                syncing = false;
                if (!scalpSyncing)
                {
                    scalpSyncing = true;
                    SyncScalpMoneyFromPips();
                    scalpSyncing = false;
                }
                if (!normalSyncing)
                {
                    normalSyncing = true;
                    SyncNormalMoneyFromPips();
                    normalSyncing = false;
                }
                UpdateReviewExecutionBarrierSnapshot(currentSnapshot, activeRequest, GetSelectedReviewLotSize(), latestPositions, chkAutoScalp.Checked);
                latestSnapshotJson = currentSnapshot.ToString(Formatting.Indented);
                form.Tag = latestSnapshotJson;
                RefreshReviewDashboard(currentSnapshot, bindings);
            };

            nudScalpSl.ValueChanged += (_, _) =>
            {
                if (scalpSyncing) return;
                scalpSyncing = true;
                nudScalpSlMoney.Value = Math.Min(nudScalpSlMoney.Maximum, Math.Round(nudScalpSl.Value * (decimal)PipValue(), 2));
                EnsureScalpTpAtMinimum();
                nudScalpTpMoney.Value = Math.Min(nudScalpTpMoney.Maximum, Math.Round(nudScalpTp.Value * (decimal)PipValue(), 2));
                UpdateScalpActualRrLabel();
                scalpSyncing = false;
            };
            nudScalpTp.ValueChanged += (_, _) =>
            {
                if (scalpSyncing) return;
                scalpSyncing = true;
                EnsureScalpTpAtMinimum();
                nudScalpTpMoney.Value = Math.Min(nudScalpTpMoney.Maximum, Math.Round(nudScalpTp.Value * (decimal)PipValue(), 2));
                UpdateScalpActualRrLabel();
                scalpSyncing = false;
            };
            nudScalpRr.ValueChanged += (_, _) =>
            {
                if (scalpSyncing) return;
                scalpSyncing = true;
                EnsureScalpTpAtMinimum();
                nudScalpTpMoney.Value = Math.Min(nudScalpTpMoney.Maximum, Math.Round(nudScalpTp.Value * (decimal)PipValue(), 2));
                UpdateScalpActualRrLabel();
                scalpSyncing = false;
            };
            nudScalpSlMoney.ValueChanged += (_, _) =>
            {
                if (scalpSyncing) return;
                scalpSyncing = true;
                decimal pipValue = (decimal)PipValue();
                if (pipValue > 0)
                    nudScalpSl.Value = Math.Min(nudScalpSl.Maximum, Math.Max(nudScalpSl.Minimum, Math.Round(nudScalpSlMoney.Value / pipValue, 1)));
                EnsureScalpTpAtMinimum();
                UpdateScalpActualRrLabel();
                scalpSyncing = false;
            };
            nudScalpTpMoney.ValueChanged += (_, _) =>
            {
                if (scalpSyncing) return;
                scalpSyncing = true;
                decimal pipValue = (decimal)PipValue();
                if (pipValue > 0)
                    nudScalpTp.Value = Math.Min(nudScalpTp.Maximum, Math.Max(nudScalpTp.Minimum, Math.Round(nudScalpTpMoney.Value / pipValue, 1)));
                EnsureScalpTpAtMinimum();
                nudScalpTpMoney.Value = Math.Min(nudScalpTpMoney.Maximum, Math.Round(nudScalpTp.Value * pipValue, 2));
                UpdateScalpActualRrLabel();
                scalpSyncing = false;
            };
            nudNormalSl.ValueChanged += (_, _) =>
            {
                if (normalSyncing) return;
                normalSyncing = true;
                nudNormalSlMoney.Value = Math.Min(nudNormalSlMoney.Maximum, Math.Round(nudNormalSl.Value * (decimal)PipValue(), 2));
                EnsureNormalTpAtMinimum();
                nudNormalTpMoney.Value = Math.Min(nudNormalTpMoney.Maximum, Math.Round(nudNormalTp.Value * (decimal)PipValue(), 2));
                UpdateNormalActualRrLabel();
                normalSyncing = false;
            };
            nudNormalTp.ValueChanged += (_, _) =>
            {
                if (normalSyncing) return;
                normalSyncing = true;
                EnsureNormalTpAtMinimum();
                nudNormalTpMoney.Value = Math.Min(nudNormalTpMoney.Maximum, Math.Round(nudNormalTp.Value * (decimal)PipValue(), 2));
                UpdateNormalActualRrLabel();
                normalSyncing = false;
            };
            nudNormalRr.ValueChanged += (_, _) =>
            {
                if (normalSyncing) return;
                normalSyncing = true;
                EnsureNormalTpAtMinimum();
                nudNormalTpMoney.Value = Math.Min(nudNormalTpMoney.Maximum, Math.Round(nudNormalTp.Value * (decimal)PipValue(), 2));
                UpdateNormalActualRrLabel();
                normalSyncing = false;
            };
            nudNormalSlMoney.ValueChanged += (_, _) =>
            {
                if (normalSyncing) return;
                normalSyncing = true;
                decimal pipValue = (decimal)PipValue();
                if (pipValue > 0)
                    nudNormalSl.Value = Math.Min(nudNormalSl.Maximum, Math.Max(nudNormalSl.Minimum, Math.Round(nudNormalSlMoney.Value / pipValue, 1)));
                EnsureNormalTpAtMinimum();
                UpdateNormalActualRrLabel();
                normalSyncing = false;
            };
            nudNormalTpMoney.ValueChanged += (_, _) =>
            {
                if (normalSyncing) return;
                normalSyncing = true;
                decimal pipValue = (decimal)PipValue();
                if (pipValue > 0)
                    nudNormalTp.Value = Math.Min(nudNormalTp.Maximum, Math.Max(nudNormalTp.Minimum, Math.Round(nudNormalTpMoney.Value / pipValue, 1)));
                EnsureNormalTpAtMinimum();
                nudNormalTpMoney.Value = Math.Min(nudNormalTpMoney.Maximum, Math.Round(nudNormalTp.Value * pipValue, 2));
                UpdateNormalActualRrLabel();
                normalSyncing = false;
            };
            btnScalpReset.Click += (_, _) =>
            {
                scalpSyncing = true;
                var suggested = BuildSuggestedScalpingConfigForPair(activeRequest.Pair, symbol);
                ApplyScalpingConfigToControls(suggested);
                EnsureScalpTpAtMinimum();
                scalpSyncing = false;
                SyncScalpMoneyFromPips();
                Log(
                    $"[SCALP] Reset review values to bot suggestion for {NormalizePairKey(activeRequest.Pair)}: " +
                    $"SL {suggested.StopLossPips:F1} pips, TP {suggested.TakeProfitPips:F1} pips, " +
                    $"max spread {suggested.MaxSpreadPips:F1} pips.",
                    C_ACCENT);
            };
            btnCommonReset.Click += (_, _) =>
            {
                cmbTradingMode.SelectedIndex = 1;
                chkCommonAi.Checked = false;
                chkAutoClose.Checked = false;
                nudPips.Value = 0;
                nudMoney.Value = 0;
                var suggested = BuildSuggestedScalpingConfigForPair(activeRequest.Pair, symbol);
                ApplyScalpingConfigToControls(suggested);
                EnsureScalpTpAtMinimum();
                SyncScalpMoneyFromPips();
                ApplyNormalTradingSettingsToControls(new NormalTradingSettings
                {
                    Enabled = true,
                    StopLossPips = Math.Max(1, suggested.StopLossPips * 3),
                    TakeProfitPips = Math.Max(1, suggested.TakeProfitPips * 3),
                    MaxSpreadPips = Math.Max(suggested.MaxSpreadPips, 30),
                    RiskRewardRatio = 2.0
                });
                EnsureNormalTpAtMinimum();
                SyncNormalMoneyFromPips();
            };
            EnsureScalpTpAtMinimum();
            SyncScalpMoneyFromPips();
            EnsureNormalTpAtMinimum();
            SyncNormalMoneyFromPips();

            double GetSelectedReviewLotSize()
            {
                double selectedLot = cmbLotSize.SelectedItem is LotSizeOption selected && selected.IsAutoFromRisk
                    ? CalculateReviewLotFromRisk(activeRequest, account, symbol)
                    : cmbLotSize.SelectedItem is LotSizeOption manual
                        ? manual.Size
                        : Math.Max(0.01, activeRequest.LotSize);

                return BrokerLotSizeValidator.Normalize(selectedLot, symbol);
            }
            getCurrentReviewLotSize = GetSelectedReviewLotSize;

            int GetSelectedReviewLeverage()
            {
                string levStr = cmbLeverage.SelectedItem?.ToString() ?? "1:100";
                int colon = levStr.IndexOf(':');
                return colon >= 0 && int.TryParse(levStr[(colon + 1)..], out int lev) ? lev : 100;
            }

            string BuildCurrentAiInputPrompt() =>
                BuildFilledAiInputPrompt(BuildCurrentValuesJson());

            string BuildCurrentValuesJson()
            {
                UpdateReviewExecutionBarrierSnapshot(
                    currentSnapshot,
                    activeRequest,
                    GetSelectedReviewLotSize(),
                    latestPositions,
                    chkAutoScalp.Checked);
                if (currentSnapshot["account"] is JObject accountJson)
                    accountJson["leverage"] = GetSelectedReviewLeverage();
                latestSnapshotJson = currentSnapshot.ToString(Formatting.Indented);
                form.Tag = latestSnapshotJson;
                return latestSnapshotJson;
            }

            UpdateReviewExecutionBarrierSnapshot(currentSnapshot, request, GetSelectedReviewLotSize(), latestPositions, chkAutoScalp.Checked);
            latestSnapshotJson = currentSnapshot.ToString(Formatting.Indented);
            form.Tag = latestSnapshotJson;
            RefreshReviewDashboard(currentSnapshot, bindings);

            // â"€â"€ Bottom section: status label + button row â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€
            var bottomHost = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = form.BackColor
            };
            bottomHost.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            bottomHost.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.Controls.Add(bottomHost, 0, 2);

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
            TradeRequest? aiCompletedRequest = null;
            TradeReviewDecision decision = new(false, false, 0, 0);

            // â"€â"€ Build buttons â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€
            var btnPlay         = MakeDialogButton("Start Normal Trading", C_GREEN);
            var btnCancel       = MakeDialogButton("Cancel", Color.FromArgb(110, 110, 130));
            var btnSignalJson   = MakeDialogButton("Signal",      Color.FromArgb(20, 38, 68));
            var btnViewJson     = MakeDialogButton("Values JSON", Color.FromArgb(28, 45, 80));
            var btnFilledValues = MakeDialogButton("Prompt",      Color.FromArgb(28, 40, 65));
            var btnAiResponse   = MakeDialogButton("AI Response",  Color.FromArgb(40, 28, 65));

            btnSignalJson.ForeColor   = Color.FromArgb(180, 220, 255);
            btnViewJson.ForeColor     = Color.FromArgb(130, 180, 255);
            btnFilledValues.ForeColor = Color.FromArgb(130, 220, 180);
            btnAiResponse.ForeColor   = Color.FromArgb(210, 150, 255);
            btnAiResponse.Enabled     = false;

            buttons.Controls.Add(btnCancel);
            buttons.Controls.Add(btnSignalJson);
            buttons.Controls.Add(btnViewJson);
            buttons.Controls.Add(btnFilledValues);
            buttons.Controls.Add(btnAiResponse);

            // â"€â"€ Helper: open a static JSON viewer form â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€
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

            // -- Button: Signal JSON (view + edit the actual signal file)
            btnSignalJson.Click += (_, _) =>
            {
                string signalPath = ResolveSignalFilePath(info);
                string sigJson    = "";
                if (!string.IsNullOrWhiteSpace(signalPath) && File.Exists(signalPath))
                    try { sigJson = File.ReadAllText(signalPath); } catch { }
                if (string.IsNullOrWhiteSpace(sigJson)) sigJson = info.RawJson ?? "";
                if (string.IsNullOrWhiteSpace(sigJson))
                {
                    AppMessageBox.Info(form, "Signal JSON not available.", "Signal JSON");
                    return;
                }
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(sigJson);
                    sigJson = System.Text.Json.JsonSerializer.Serialize(doc.RootElement,
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                }
                catch { }

                var jf = new Form
                {
                    Text            = $"Signal JSON - {info.Pair}",
                    Size            = new Size(700, 580),
                    MinimumSize     = new Size(480, 380),
                    BackColor       = Color.FromArgb(13, 18, 30),
                    ForeColor       = Color.FromArgb(218, 218, 230),
                    StartPosition   = FormStartPosition.CenterParent,
                    FormBorderStyle = FormBorderStyle.Sizable,
                };
                AppIcon.ApplyTo(jf);

                var rtbSig = new RichTextBox
                {
                    Dock        = DockStyle.Fill,
                    ReadOnly    = false,
                    ScrollBars  = RichTextBoxScrollBars.Both,
                    Font        = new Font("Consolas", 10F),
                    BackColor   = Color.FromArgb(18, 22, 36),
                    ForeColor   = Color.FromArgb(180, 220, 255),
                    BorderStyle = BorderStyle.None,
                    WordWrap    = false,
                    Text        = sigJson
                };

                bool hasFile  = !string.IsNullOrWhiteSpace(signalPath) && File.Exists(signalPath);

                var btnBar = new FlowLayoutPanel
                {
                    Dock          = DockStyle.Bottom,
                    Height        = 46,
                    BackColor     = Color.FromArgb(18, 22, 36),
                    FlowDirection = FlowDirection.LeftToRight,
                    Padding       = new Padding(6, 6, 6, 0),
                    WrapContents  = false,
                };

                var btnSave = new Button
                {
                    Text      = "Save to File",
                    Size      = new Size(110, 32),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(30, 80, 50),
                    ForeColor = Color.FromArgb(120, 230, 160),
                    Font      = new Font("Segoe UI Semibold", 9F),
                    Cursor    = Cursors.Hand,
                    Enabled   = hasFile
                };
                btnSave.FlatAppearance.BorderSize = 0;

                var btnCopy = new Button
                {
                    Text      = "Copy",
                    Size      = new Size(70, 32),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(28, 45, 80),
                    ForeColor = Color.FromArgb(130, 180, 255),
                    Font      = new Font("Segoe UI Semibold", 9F),
                    Cursor    = Cursors.Hand
                };
                btnCopy.FlatAppearance.BorderSize = 0;

                var btnCloseJ = new Button
                {
                    Text      = "Close",
                    Size      = new Size(70, 32),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(50, 52, 68),
                    ForeColor = Color.FromArgb(200, 200, 220),
                    Font      = new Font("Segoe UI Semibold", 9F),
                    Cursor    = Cursors.Hand
                };
                btnCloseJ.FlatAppearance.BorderSize = 0;

                var lblPath = new Label
                {
                    Text      = hasFile ? signalPath : "(no file - read only)",
                    ForeColor = Color.FromArgb(90, 100, 130),
                    Font      = new Font("Segoe UI", 8F),
                    AutoSize  = false,
                    Size      = new Size(380, 32),
                    TextAlign = ContentAlignment.MiddleLeft,
                };

                btnSave.Click += (_, _) =>
                {
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(rtbSig.Text);
                        string fmt = System.Text.Json.JsonSerializer.Serialize(doc.RootElement,
                            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                        File.WriteAllText(signalPath, fmt);
                        rtbSig.Text  = fmt;
                        btnSave.Text = "Saved!";
                        var t = new System.Windows.Forms.Timer { Interval = 1500 };
                        t.Tick += (_, _) => { btnSave.Text = "Save to File"; t.Stop(); t.Dispose(); };
                        t.Start();
                    }
                    catch (Exception ex)
                    {
                        AppMessageBox.Warning(jf, $"Cannot save: {ex.Message}", "Save Error");
                    }
                };
                btnCopy.Click += (_, _) =>
                {
                    try { Clipboard.SetText(rtbSig.Text); } catch { }
                    btnCopy.Text = "Copied!";
                    var t = new System.Windows.Forms.Timer { Interval = 1400 };
                    t.Tick += (_, _) => { btnCopy.Text = "Copy"; t.Stop(); t.Dispose(); };
                    t.Start();
                };
                btnCloseJ.Click += (_, _) => jf.Close();

                btnBar.Controls.AddRange(new Control[] { btnSave, btnCopy, btnCloseJ, lblPath });
                jf.Controls.Add(rtbSig);
                jf.Controls.Add(btnBar);
                jf.Show(form);
            };

            // -- Button: View JSON (live snapshot)
            btnViewJson.Click += (_, _) =>
                OpenJsonViewer("Market Snapshot JSON", BuildCurrentValuesJson, liveRefresh: true);

            // -- Button: Input Prompt (what is sent to AI)
            btnFilledValues.Click += (_, _) =>
                OpenJsonViewer("AI Input Prompt", BuildCurrentAiInputPrompt, liveRefresh: true);

            // â"€â"€ Button: AI Response â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€
            btnAiResponse.Click += (_, _) =>
                OpenJsonViewer("AI Trade Decision Response", () =>
                    string.IsNullOrEmpty(aiResponseJson) ? "{ \"status\": \"No AI response yet\" }" : aiResponseJson);

            // â"€â"€ Helper: set status label â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€
            void SetPlayStatus(string text, Color color)
            {
                lblPlayStatus.Text      = text;
                lblPlayStatus.ForeColor = color;
            }

            // â"€â"€ Button: Play / Start Trade â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€
            btnStartScalping.Click += async (_, _) =>
            {
                if (_scalping?.IsRunning == true)
                {
                    btnStartScalping.Enabled = false;
                    lblScalpingStatus.Text = "Scalping Status: Stopping";
                    await StopScalpingAsync().ConfigureAwait(true);
                    RefreshScalpingReviewRunState();
                    btnStartScalping.Enabled = true;
                    return;
                }

                RefreshScalpingReviewRunState();
                chkAutoScalp.Checked = true;
                btnPlay.PerformClick();
            };

            btnStartNormal.Click += (_, _) =>
            {
                if (_normalTradeManager.IsRunning)
                {
                    _normalTradeManager.Stop();
                    lblNormalStatus.Text = "Normal Trading Status: Stopped";
                    lblNormalStatus.ForeColor = Color.FromArgb(145, 150, 165);
                    btnStartNormal.Text = "Start Normal Trading";
                    Log("[BOT] Normal trading manager stopped.", C_YELLOW);
                    return;
                }

                chkAutoScalp.Checked = false;
                btnPlay.PerformClick();
            };

            btnPlay.Click += async (_, _) =>
            {
                btnPlay.Enabled   = false;
                btnPlay.Text      = "Analyzing...";

                bool aiEnabled = chkCommonAi.Checked
                              && !string.IsNullOrWhiteSpace(_cfg.Claude.ApiKey)
                              && !_cfg.Claude.ApiKey.StartsWith("sk-ant-..")
                              && _cfg.Claude.ApiKey.Length > 20;
                bool autoScalpingRequested = chkAutoScalp.Checked;
                var scalpingConfig = BuildScalpingConfigFromReview(
                    activeRequest.Pair,
                    cmbScalpMode,
                    nudScalpTrades,
                    nudScalpMinutes,
                    nudScalpSl,
                    nudScalpTp,
                    nudScalpRr,
                    nudScalpSpread,
                    chkCommonAi);
                scalpingConfig.UseAiConfirmation = chkCommonAi.Checked;
                scalpingConfig.Enabled = chkAutoScalp.Checked;
                var normalTradingSettings = BuildNormalTradingSettingsFromReview(
                    chkNormalTrading,
                    nudNormalTrades,
                    nudNormalExpiry,
                    nudNormalSl,
                    nudNormalTp,
                    nudNormalSpread,
                    nudNormalRr);
                var commonTradingSettings = BuildCommonTradingSettingsFromReview(
                    cmbTradingMode,
                    chkCommonAi,
                    chkAutoClose,
                    nudPips,
                    nudMoney,
                    nudBeTrigger);

                _cfg.Bot.CommonTrading = commonTradingSettings;
                await SaveScalpingConfigForPairAsync(activeRequest.Pair, scalpingConfig);
                await SaveNormalTradingSettingsForPairAsync(activeRequest.Pair, normalTradingSettings);
                if (!autoScalpingRequested)
                {
                    if (!normalTradingSettings.Enabled)
                    {
                        SetPlayStatus("Normal trading is disabled.", C_YELLOW);
                        Log("[BOT] Normal trading start blocked because Enable Normal Trading is unchecked.", C_YELLOW);
                        btnPlay.Text = "Start Normal Trading";
                        btnPlay.Enabled = true;
                        return;
                    }

                    activeRequest = ApplyNormalTradingSettingsToRequest(activeRequest, normalTradingSettings, symbol);
                    PatchSnapshotSignalFields(currentSnapshot, activeRequest);
                }
                else
                {
                    activeRequest = ApplyScalpingSettingsToRequest(activeRequest, scalpingConfig, symbol);
                    PatchSnapshotSignalFields(currentSnapshot, activeRequest);
                }

                UpdateReviewExecutionBarrierSnapshot(currentSnapshot, activeRequest, GetSelectedReviewLotSize(), latestPositions, autoScalpingRequested);
                latestSnapshotJson = currentSnapshot.ToString(Formatting.Indented);
                form.Tag = latestSnapshotJson;
                RefreshReviewDashboard(currentSnapshot, bindings);

                var failedRules = GetFailedReviewBarrierMessages(
                    currentSnapshot,
                    allowAiCompletion: aiEnabled,
                    allowAutoScalping: autoScalpingRequested);
                if (failedRules.Count > 0)
                {
                    if (!autoScalpingRequested &&
                        failedRules.Any(rule =>
                            rule.Contains("StopLoss cannot be 0", StringComparison.OrdinalIgnoreCase) ||
                            rule.Contains("TakeProfit cannot be 0", StringComparison.OrdinalIgnoreCase)))
                    {
                        failedRules.Insert(
                            0,
                            "Auto scalping is not selected. Tick Auto scalping to use SL/TP pips, or enter normal SL/TP price levels for a one-shot trade.");
                    }

                    string message =
                        "These required trade rules are not fulfilled:\n\n" +
                        string.Join("\n", failedRules.Select(rule => "- " + rule)) +
                        "\n\nThe trade cannot be started until these hard safety rules are fixed.";

                    AppMessageBox.Warning(form, message, "Trade Blocked By Safety Rules");

                    SetPlayStatus("Trade blocked because one or more required rules are not fulfilled.", C_RED);
                    Log("[BOT] Trade blocked by review safety rules: " + string.Join(" | ", failedRules), C_RED);
                    btnPlay.Text    = "Start Normal Trading";
                    btnPlay.Enabled = true;
                    return;
                }

                var warningItems = BuildReviewWarningItems(
                    currentSnapshot,
                    activeRequest,
                    aiEnabled,
                    autoScalpingRequested,
                    GetSelectedReviewLotSize(),
                    GetSelectedReviewLeverage());
                if (warningItems.Count > 0)
                {
                    var warningForm = new TradeWarningForm(warningItems);
                    if (await warningForm.ShowModelessAsync(form).ConfigureAwait(true) != DialogResult.OK)
                    {
                        SetPlayStatus("Trade cancelled after warning review.", C_YELLOW);
                        Log("[BOT] Trade cancelled after review warnings.", C_YELLOW);
                        btnPlay.Text = "Start Normal Trading";
                        btnPlay.Enabled = true;
                        return;
                    }

                    Log("[BOT] User confirmed review warnings: " + string.Join(" | ", warningItems.Select(w => w.Title)), C_YELLOW);
                }

                string aiInputPrompt = BuildCurrentAiInputPrompt();

                if (autoScalpingRequested)
                {
                    Log("[SCALP] Auto scalping selected - skipping one-shot signal AI/SL/TP validation.", C_ACCENT);
                    SetPlayStatus("Starting auto scalping session...", Color.Gold);
                }
                else if (aiEnabled)
                {
                    try
                    {
                        SetPlayStatus("Sending to AI for analysis...", C_ACCENT);
                        Log("[AI] Running trade decision analysis on market snapshot...", C_ACCENT);

                        var (respJson, allowed, aiDecision, error) =
                            await RunAiTradeDecisionAsync(aiInputPrompt).ConfigureAwait(false);

                        aiResponseJson = respJson;
                        if (!string.IsNullOrEmpty(respJson)) btnAiResponse.Enabled = true;

                        if (!string.IsNullOrEmpty(error))
                        {
                            SetPlayStatus($"AI Error: {error}", C_RED);
                            Log($"[AI] Analysis failed: {error}", C_RED);
                            btnPlay.Text    = "Start Normal Trading";
                            btnPlay.Enabled = true;
                            return;
                        }

                        Log($"[AI] Decision: {aiDecision}", aiDecision is "BUY" or "SELL" ? C_GREEN : C_YELLOW);

                        if (!allowed || aiDecision is not "BUY" and not "SELL")
                        {
                            string reasons = ExtractAiBlockingReasons(respJson);
                            SetPlayStatus($"AI: {aiDecision} - {reasons}", C_YELLOW);
                            Log($"[AI] Trade not approved. Decision: {aiDecision} | {reasons}", C_YELLOW);
                            btnPlay.Text    = "Start Normal Trading";
                            btnPlay.Enabled = true;
                            return;
                        }

                        // AI approved - build signal from response and write to watch folder
                        var signalReq = BuildSignalFromAiDecision(activeRequest, respJson);
                        var (aiSignalValid, aiSignalError) = signalReq.Validate();
                        if (!aiSignalValid)
                        {
                            SetPlayStatus($"AI response invalid: {aiSignalError}", C_RED);
                            Log($"[AI] Approved response rejected by local validation: {aiSignalError}", C_RED);
                            btnPlay.Text    = "Start Normal Trading";
                            btnPlay.Enabled = true;
                            return;
                        }

                        aiCompletedRequest = signalReq;
                        string signalPath = WriteSignalFile(signalReq);
                        Log($"[AI] APPROVED - {aiDecision} | Signal: {Path.GetFileName(signalPath)}", C_GREEN);
                        SetPlayStatus($"AI approved {aiDecision}. Signal: {Path.GetFileName(signalPath)}", C_GREEN);
                    }
                    catch (Exception ex)
                    {
                        SetPlayStatus($"Error: {ex.Message}", C_RED);
                        Log($"[AI] Exception during analysis: {ex.Message}", C_RED);
                        btnPlay.Text    = "Start Normal Trading";
                        btnPlay.Enabled = true;
                        return;
                    }
                }
                else
                {
                    Log("[AI] AI not configured - executing trade from signal values directly.", C_YELLOW);
                    SetPlayStatus("AI not configured - executing directly.", C_YELLOW);
                }

                double finalReviewLotSize = GetSelectedReviewLotSize();
                if (aiCompletedRequest != null)
                    aiCompletedRequest.LotSize = finalReviewLotSize;
                activeRequest.LotSize = finalReviewLotSize;

                form.Tag = latestSnapshotJson;
                if (!chkAutoScalp.Checked && chkNormalTrading.Checked)
                    _normalTradeManager.Start(normalTradingSettings);
                decision  = new TradeReviewDecision(
                    true,
                    chkAutoClose.Checked,
                    (double)nudPips.Value,
                    (double)nudMoney.Value,
                    finalReviewLotSize,
                    GetSelectedReviewLeverage(),
                    aiCompletedRequest,
                    chkAutoScalp.Checked,
                    scalpingConfig,
                    !chkAutoScalp.Checked && chkNormalTrading.Checked,
                    normalTradingSettings,
                    commonTradingSettings);
                decisionCompleted = true;
                completion.TrySetResult(decision);
                form.DialogResult = DialogResult.OK;
                form.Close();
            };

            btnCancel.Click += (_, _) =>
            {
                decision = new TradeReviewDecision(false, false, 0, 0);
                decisionCompleted = true;
                completion.TrySetResult(decision);
                form.DialogResult = DialogResult.Cancel;
                form.Close();
            };

            // ── Live signal push: update all signal-derived data when a new signal arrives
            _reviewSignalPush = newReq =>
            {
                if (form.IsDisposed) return;
                void Apply()
                {
                    activeRequest = newReq;
                    title.Text = $"{newReq.TradeType} {newReq.Pair} | Lots {newReq.LotSize:F2} | SL {newReq.StopLoss:F5} | TP {newReq.TakeProfit:F5}";
                    PatchSnapshotSignalFields(currentSnapshot, activeRequest);
                    UpdateReviewExecutionBarrierSnapshot(currentSnapshot, activeRequest, getCurrentReviewLotSize(), latestPositions, scalpingStrategy: false);
                    latestSnapshotJson = currentSnapshot.ToString(Formatting.Indented);
                    form.Tag = latestSnapshotJson;
                    RefreshReviewDashboard(currentSnapshot, bindings);
                }
                if (form.InvokeRequired) form.BeginInvoke(Apply);
                else Apply();
            };

            // ── Watch the signal file itself for on-disk edits
            form.Activated += (_, _) => RefreshScalpingReviewRunState();

            FileSystemWatcher? sigFileWatcher = null;
            string sigFilePath = ResolveSignalFilePath(info) ?? "";
            if (!string.IsNullOrWhiteSpace(sigFilePath) && File.Exists(sigFilePath))
            {
                sigFileWatcher = new FileSystemWatcher(
                    Path.GetDirectoryName(sigFilePath)!,
                    Path.GetFileName(sigFilePath))
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };
                sigFileWatcher.Changed += async (_, _) =>
                {
                    try
                    {
                        await Task.Delay(120).ConfigureAwait(false); // let writer finish
                        string newJson = await Task.Run(() => File.ReadAllText(sigFilePath)).ConfigureAwait(false);
                        var newReq = JsonConvert.DeserializeObject<TradeRequest>(newJson);
                        if (newReq != null) _reviewSignalPush?.Invoke(newReq);
                    }
                    catch { }
                };
            }

            form.FormClosed += (_, _) =>
            {
                _reviewSignalPush = null;
                sigFileWatcher?.Dispose();
                if (!decisionCompleted)
                {
                    decision = new TradeReviewDecision(false, false, 0, 0);
                    completion.TrySetResult(decision);
                }
            };

            EnableReviewDoubleBuffering(form);
            ResizeReviewContent();
            form.ResumeLayout(true);
            CopyablePopupText.Enable(form);
            form.Show(this);
            return await completion.Task.ConfigureAwait(true);
        }

        private Control BuildReviewDashboard(
            List<(string Path, Label Value, string Format)> bindings,
            out Label liveStatus,
            out FlowLayoutPanel groupsFlow,
            bool useInternalScroll = true)
        {
            var host = new TableLayoutPanel
            {
                Dock = useInternalScroll ? DockStyle.Fill : DockStyle.Top,
                AutoSize = !useInternalScroll,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.FromArgb(13, 13, 19)
            };
            host.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            host.RowStyles.Add(useInternalScroll
                ? new RowStyle(SizeType.Percent, 100)
                : new RowStyle(SizeType.AutoSize));

            liveStatus = new Label
            {
                Text = $"  {DateTime.Now:HH:mm:ss}  |  Last sync: -  |  Next sync in: 5s",
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(150, 220, 255),
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            host.Controls.Add(liveStatus, 0, 0);

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0, 0, 12, 12),
                BackColor = host.BackColor
            };
            groupsFlow = flow;
            if (useInternalScroll)
            {
                var scroll = new Panel
                {
                    Dock = DockStyle.Fill,
                    AutoScroll = true,
                    BackColor = Color.FromArgb(13, 13, 19)
                };
                host.Controls.Add(scroll, 0, 1);
                scroll.Controls.Add(flow);
            }
            else
            {
                host.Controls.Add(flow, 0, 1);
            }

            var reviewTips = new ToolTip
            {
                AutoPopDelay = 22000,
                InitialDelay = 350,
                ReshowDelay = 100,
                ShowAlways = true
            };
            host.Tag = reviewTips;
            reviewTips.SetToolTip(liveStatus, "Shows when the Review Trade data last refreshed from MT5 and when the next refresh will run.");

            AddReviewGroup(flow, bindings, reviewTips, "Pre-Trade Safety Checks", [
                ("Final order fields valid", "execution_barriers.signal_valid_detail", "barrier:execution_barriers.signal_valid"),
                ("Signal is not expired", "execution_barriers.signal_fresh_detail", "barrier:execution_barriers.signal_fresh"),
                ("Pair is allowed", "execution_barriers.pair_allowed_detail", "barrier:execution_barriers.pair_allowed"),
                ("Daily trade limit", "execution_barriers.daily_limit_detail", "barrier:execution_barriers.daily_limit_ok"),
                ("Account data available", "execution_barriers.account_detail", "barrier:execution_barriers.account_ok"),
                ("Risk/reward rule", "execution_barriers.rr_detail", "barrier:execution_barriers.rr_ok"),
                ("Free margin available", "execution_barriers.free_margin_detail", "barrier:execution_barriers.free_margin_ok"),
                ("Total account risk cap", "execution_barriers.portfolio_risk_detail", "barrier:execution_barriers.portfolio_risk_ok"),
                ("Spread within limit", "execution_barriers.spread_detail", "barrier:execution_barriers.spread_ok"),
                ("News blackout clear", "execution_barriers.news_detail", "barrier:execution_barriers.news_ok")
            ]);

            AddReviewGroup(flow, bindings, reviewTips, "Account Health", [
                ("Account balance", "account.balance", "money"),
                ("Live account equity", "account.equity", "money"),
                ("Free margin available", "account.free_margin", "money"),
                ("Margin currently used", "account.margin_used", "money"),
                ("Margin level percent", "account.margin_level", "pct"),
                ("Open trade profit/loss", "account.floating_pnl", "money"),
                ("Profit/loss today", "account.daily_pnl", "money"),
                ("Trades opened today", "account.daily_trades_taken", "plain")
            ]);

            AddReviewGroup(flow, bindings, reviewTips, "Live Price And Spread", [
                ("Bid price", "price.bid", "price"),
                ("Ask price", "price.ask", "price"),
                ("Current spread", "price.spread_pips", "pips"),
                ("Today open price", "price.daily_open", "price"),
                ("Today high price", "price.daily_high", "price"),
                ("Today low price", "price.daily_low", "price"),
                ("Today range", "price.daily_range_pips", "pips"),
                ("Previous day high", "price.prev_day_high", "price")
            ]);

            AddReviewGroup(flow, bindings, reviewTips, "Trade Risk Preview", [
                ("Trading Mode", "effective_trade_settings.trading_mode", "plain"),
                ("Money at risk", "risk.dollar_risk", "money"),
                ("Profit at TP1", "risk.dollar_profit_tp1", "money"),
                ("Profit at TP2", "risk.dollar_profit_tp2", "money"),
                ("Trade Page SL", "effective_trade_settings.stop_loss_pips", "pips"),
                ("Trade Page TP", "effective_trade_settings.take_profit_pips", "pips"),
                ("Trade Page BE trigger", "effective_trade_settings.be_trigger_pips", "pips"),
                ("Trade Page max trades", "effective_trade_settings.max_trades", "plain"),
                ("Risk/reward ratio", "risk.rr_ratio", "ratio"),
                ("Max risk per trade", "risk.max_risk_pct", "pct"),
                ("Daily loss room left", "risk.daily_loss_remaining", "money")
            ]);

            AddReviewGroup(flow, bindings, reviewTips, "Broker Symbol Rules", [
                ("Broker symbol name", "symbol.name", "plain"),
                ("Price decimals", "symbol.digits", "plain"),
                ("Minimum lot size", "symbol.min_lot", "lots"),
                ("Maximum lot size", "symbol.max_lot", "lots"),
                ("Lot size step", "symbol.lot_step", "lots"),
                ("Trading allowed now", "symbol.trade_allowed", "bool"),
                ("Broker execution rule", "symbol.execution_mode", "plain"),
                ("Order fill rule", "symbol.filling_mode", "plain")
            ]);

            AddReviewGroup(flow, bindings, reviewTips, "Market Session", [
                ("Broker server time", "session.broker_time", "plain"),
                ("MT5 terminal connected", "session.terminal_connected", "bool"),
                ("Market is open", "session.market_open", "bool"),
                ("London session open", "session.london_open", "bool"),
                ("New York session open", "session.newyork_open", "bool"),
                ("London/New York overlap", "session.overlap_active", "bool"),
                ("Current session name", "session.session_name", "plain"),
                ("Weekend market status", "session.is_weekend", "bool")
            ]);

            AddReviewGroup(flow, bindings, reviewTips, "H1 Indicator Signals", [
                ("Momentum score (RSI)", "indicators.h1.rsi", "one"),
                ("Momentum meaning", "indicators.h1.rsi_signal", "plain"),
                ("Direction bias (MACD)", "indicators.h1.macd_bias", "plain"),
                ("Fast average price (EMA 20)", "indicators.h1.ema20", "price"),
                ("Medium average price (EMA 50)", "indicators.h1.ema50", "price"),
                ("Long trend price (EMA 200)", "indicators.h1.ema200", "price"),
                ("Trend strength (ADX)", "indicators.h1.adx", "one"),
                ("Volatility size (ATR)", "indicators.h1.atr", "price")
            ]);

            AddReviewGroup(flow, bindings, reviewTips, "Recent Candle Behavior", [
                ("H4 candle direction", "candles.h4_last.direction", "plain"),
                ("H1 candle direction", "candles.h1_last.direction", "plain"),
                ("M15 candle direction", "candles.m15_last.direction", "plain"),
                ("M5 candle direction", "candles.m5_last.direction", "plain"),
                ("H1 candle body size", "candles.h1_last.body_pips", "pips"),
                ("M15 candle body size", "candles.m15_last.body_pips", "pips"),
                ("M5 candle is doji", "candles.m5_last.is_doji", "bool"),
                ("M15 candle is inside bar", "candles.m15_last.is_inside_bar", "bool")
            ]);

            AddReviewGroup(flow, bindings, reviewTips, "Market Structure", [
                ("H4 trend direction", "structure.trend_h4", "plain"),
                ("H1 trend direction", "structure.trend_h1", "plain"),
                ("M15 trend direction", "structure.trend_m15", "plain"),
                ("M5 trend direction", "structure.trend_m5", "plain"),
                ("All timeframes agree", "structure.all_timeframes_aligned", "bool"),
                ("Market condition", "structure.market_regime", "plain"),
                ("Nearest swing high", "structure.swing_high", "price"),
                ("Nearest swing low", "structure.swing_low", "price")
            ]);

            AddReviewGroup(flow, bindings, reviewTips, "Support And Resistance", [
                ("Nearest support level", "levels.nearest_support_1", "price"),
                ("Second support level", "levels.nearest_support_2", "price"),
                ("Nearest resistance level", "levels.nearest_resistance_1", "price"),
                ("Second resistance level", "levels.nearest_resistance_2", "price"),
                ("Distance to support", "levels.distance_to_support_pips", "pips"),
                ("Distance to resistance", "levels.distance_to_resistance_pips", "pips"),
                ("Price near key level", "levels.price_at_key_level", "bool"),
                ("Nearest key level type", "levels.key_level_type", "plain")
            ]);

            AddReviewGroup(flow, bindings, reviewTips, "Open Position Check", [
                ("Total open positions", "positions.total_open", "plain"),
                ("Same pair already open", "positions.same_pair_open", "bool"),
                ("Existing trade direction", "positions.same_pair_direction", "plain"),
                ("Duplicate trade exists", "positions.duplicate_trade_exists", "bool"),
                ("Opposite trade exists", "positions.opposite_trade_exists", "bool"),
                ("Last order result", "last_order.execution_result", "plain"),
                ("Last order ticket", "last_order.ticket", "plain"),
                ("Today win rate", "history.win_rate_today_pct", "pct")
            ]);

            AddReviewGroup(flow, bindings, reviewTips, "News Risk", [
                ("News risk level", "news.news_risk_level", "plain"),
                ("High impact within 60 min", "news.high_impact_next_60_min", "bool"),
                ("Blackout active now", "news.blackout_active", "bool"),
                ("Next relevant event", "news.next_event", "plain"),
                ("Why this news status", "news.reason", "plain"),
                ("Events checked", "news.relevant_event_count", "plain"),
                ("News data source", "news.source", "plain")
            ]);

            ResizeReviewGroups(flow);
            flow.Resize += (_, _) => ResizeReviewGroups(flow);

            return host;
        }

        private void UpdateReviewExecutionBarrierSnapshot(
            JObject snapshot,
            TradeRequest request,
            double selectedLotSize,
            IReadOnlyCollection<LivePosition> positions,
            bool scalpingStrategy)
        {
            var reviewRequest = CloneReviewRequest(request);
            reviewRequest.LotSize = Math.Max(0.01, selectedLotSize);

            bool signalFresh = true;
            string freshnessDetail = $"Current: no expiry | Base: expiry > 0 enables age check";
            if (reviewRequest.ExpiryMinutes > 0)
            {
                double ageMinutes = (DateTime.UtcNow - reviewRequest.CreatedAt).TotalMinutes;
                signalFresh = ageMinutes <= reviewRequest.ExpiryMinutes;
                freshnessDetail = $"Current: {ageMinutes:F0} min old | Base: <= {reviewRequest.ExpiryMinutes} min";
            }

            string pair = reviewRequest.Pair.ToUpperInvariant();
            var pairRules = _pairSettings?.GetForPair(pair);
            var effective = EffectiveTradeSettings.Resolve(
                _cfg.Bot, scalpingStrategy ? "Scalping" : "Normal", reviewRequest.LotSize);
            var metrics = BuildReviewRiskMetrics(snapshot, reviewRequest, effective, reviewRequest.LotSize);
            if (metrics.Entry > 0)
                reviewRequest.EntryPrice = metrics.Entry;
            reviewRequest.StopLoss = metrics.SuggestedSl;
            reviewRequest.TakeProfit = metrics.SuggestedTp1;
            reviewRequest.TakeProfit2 = metrics.SuggestedTp2;
            reviewRequest.Strategy = effective.Strategy;

            var (signalValid, signalError) = reviewRequest.Validate();
            double requiredRr = effective.RiskRewardRatio;
            double requiredSlPips = effective.SlPips;
            double requiredTpPips = effective.TpPips;
            double beTriggerPips = requiredTpPips * effective.BeTriggerPercentOfTp;
            string strategyName = effective.Strategy;
            double maxSpreadPips = effective.MaxSpreadPips;
            bool pairAllowed = _cfg.Bot.AllowedPairs.Count == 0 || _cfg.Bot.AllowedPairs.Contains(pair);

            var dailyStats = GetTodayReviewStats(pair, strategyName, ReadReviewNumber(snapshot, "account.floating_pnl"));
            ApplyReviewDailySummary(snapshot, dailyStats, pair, strategyName);

            double tradesToday = dailyStats.Known
                ? dailyStats.TradesToday
                : ReadReviewNumber(snapshot, "account.daily_trades_taken");
            int maxTrades = effective.MaxTrades;
            bool dailyLimitKnown = !double.IsNaN(tradesToday);
            bool dailyLimitOk = !dailyLimitKnown || tradesToday < maxTrades;

            double balance = ReadReviewNumber(snapshot, "account.balance");
            double equity = ReadReviewNumber(snapshot, "account.equity");
            double freeMargin = ReadReviewNumber(snapshot, "account.free_margin");
            bool accountOk = !double.IsNaN(equity) && equity > 0 && !double.IsNaN(freeMargin);
            bool freeMarginOk = accountOk && (double.IsNaN(balance) || balance <= 0 || freeMargin >= balance * 0.05);

            double rr = metrics.ActualRiskRewardRatio;
            bool rrOk = rr >= requiredRr;

            double entry = metrics.Entry;
            double newTradeRisk = metrics.DollarRisk;

            double openRisk = positions
                .Where(p => p.StopLoss > 0)
                .Sum(p => LotCalculator.DollarRisk(p.Lots, p.OpenPrice, p.StopLoss, p.Symbol));
            double totalRiskPct = equity > 0 ? (openRisk + newTradeRisk) / equity * 100.0 : double.NaN;
            bool portfolioRiskOk = _cfg.Bot.MaxTotalRiskPercent <= 0
                || (!double.IsNaN(totalRiskPct) && totalRiskPct <= _cfg.Bot.MaxTotalRiskPercent);

            double spread = ReadReviewNumber(snapshot, "price.spread_pips");
            bool spreadOk = maxSpreadPips <= 0
                || (!double.IsNaN(spread) && spread <= maxSpreadPips);
            ApplyReviewSpreadSummary(snapshot, spread, maxSpreadPips, spreadOk);

            string newsRisk = snapshot.SelectToken("news.news_risk_level")?.ToString() ?? "UNAVAILABLE";
            bool newsConfigured = snapshot.SelectToken("news.configured")?.Value<bool?>() == true;
            bool newsBlackout = snapshot.SelectToken("news.blackout_active")?.Value<bool?>() == true;
            bool highImpactNext60 = snapshot.SelectToken("news.high_impact_next_60_min")?.Value<bool?>() == true;
            string newsReason = snapshot.SelectToken("news.reason")?.ToString() ?? "News data unavailable.";
            bool newsUnavailableBlocks = _cfg.ApiIntegrations.BlockTradesWhenNewsUnavailable && !newsConfigured;
            bool highImpactBlocks = _cfg.ApiIntegrations.BlockTradesOnHighImpactNews && (newsBlackout || highImpactNext60);
            bool newsOk = string.Equals(_cfg.ApiIntegrations.NewsProvider, "None", StringComparison.OrdinalIgnoreCase)
                || (!newsUnavailableBlocks && !highImpactBlocks);

            snapshot["execution_barriers"] = new JObject
            {
                ["signal_valid"] = signalValid,
                ["signal_valid_detail"] = signalValid
                    ? "Current: final order fields valid | Base: pair, SL, TP, lot, direction after Trade Page SL/TP generation"
                    : $"Current: {signalError} | Base: final order must have pair, SL, TP, lot, direction after Trade Page SL/TP generation",
                ["signal_fresh"] = signalFresh,
                ["signal_fresh_detail"] = freshnessDetail,
                ["pair_allowed"] = pairAllowed,
                ["pair_allowed_detail"] = _cfg.Bot.AllowedPairs.Count == 0
                    ? $"Current: {pair} | Base: all pairs allowed"
                    : $"Current: {pair} | Base: [{string.Join(", ", _cfg.Bot.AllowedPairs)}]",
                ["daily_limit_ok"] = dailyLimitOk,
                ["daily_limit_detail"] = dailyLimitKnown
                    ? $"Current: {tradesToday:0} {strategyName} trades today for {pair} | Base: < {maxTrades} ({strategyName} trade page)"
                    : $"Current: unknown until runtime | Base: < {maxTrades} ({strategyName} trade page)",
                ["account_ok"] = accountOk,
                ["account_detail"] = accountOk
                    ? $"Current: equity {equity:0.00}, free {freeMargin:0.00} | Base: equity > 0 and margin known"
                    : "Current: unavailable | Base: equity > 0 and margin known",
                ["rr_ok"] = rrOk,
                ["rr_detail"] = $"Current: {rr:0.00} | Base: >= {requiredRr:0.00} ({strategyName} trade page)",
                ["free_margin_ok"] = freeMarginOk,
                ["free_margin_detail"] = accountOk
                    ? $"Current: {freeMargin:0.00} | Base: >= {(Math.Max(0, balance) * 0.05):0.00}"
                    : "Current: unavailable | Base: >= 5% of balance",
                ["portfolio_risk_ok"] = portfolioRiskOk,
                ["portfolio_risk_detail"] = _cfg.Bot.MaxTotalRiskPercent <= 0
                    ? "Current: not checked | Base: disabled"
                    : double.IsNaN(totalRiskPct)
                        ? $"Current: unavailable | Base: <= {_cfg.Bot.MaxTotalRiskPercent:0.0}%"
                        : $"Current: {totalRiskPct:0.0}% | Base: <= {_cfg.Bot.MaxTotalRiskPercent:0.0}%",
                ["spread_ok"] = spreadOk,
                ["spread_detail"] = maxSpreadPips <= 0
                    ? "Current: not checked | Base: disabled"
                    : double.IsNaN(spread)
                        ? $"Current: unavailable | Base: <= {maxSpreadPips:0.0} pips"
                        : $"Current: {spread:0.0} pips | Base: <= {maxSpreadPips:0.0} pips ({strategyName} trade page)",
                ["news_ok"] = newsOk,
                ["news_detail"] = string.Equals(_cfg.ApiIntegrations.NewsProvider, "None", StringComparison.OrdinalIgnoreCase)
                    ? "Current: disabled | Base: news filter disabled"
                    : highImpactBlocks
                        ? $"Current: {newsRisk} blackout active | Base: no high-impact news within {_cfg.ApiIntegrations.NewsBlackoutBeforeMinutes}m before / {_cfg.ApiIntegrations.NewsBlackoutAfterMinutes}m after"
                        : newsUnavailableBlocks
                            ? $"Current: unavailable | Base: news data required ({newsReason})"
                            : $"Current: {newsRisk}{(highImpactNext60 ? ", high impact <= 60m" : "")} | Base: no active blackout"
            };

            UpsertReviewNumber(snapshot, "risk.calculated_lot", reviewRequest.LotSize);
            snapshot["effective_trade_settings"] = new JObject
            {
                ["trading_mode"] = _cfg.Bot.CommonTrading.TradingMode.ToString(),
                ["strategy"] = strategyName,
                ["stop_loss_pips"] = requiredSlPips,
                ["take_profit_pips"] = requiredTpPips,
                ["be_trigger_percent_of_tp"] = effective.BeTriggerPercentOfTp,
                ["be_trigger_pips"] = Math.Round(beTriggerPips, 1),
                ["required_risk_reward_ratio"] = requiredRr,
                ["actual_risk_reward_ratio"] = Math.Round(rr, 2),
                ["max_trades"] = maxTrades,
                ["trades_taken_today"] = dailyLimitKnown ? tradesToday : null,
                ["max_spread_pips"] = maxSpreadPips,
                ["current_spread_pips"] = double.IsNaN(spread) ? null : Math.Round(spread, 1)
            };
            UpsertReviewNumber(snapshot, "risk.required_rr_ratio", requiredRr);
            PatchReviewRiskSnapshot(snapshot, reviewRequest, effective, metrics);

            double dailyLossLimit = ReadReviewNumber(snapshot, "risk.daily_loss_limit_dollar");
            if (double.IsNaN(dailyLossLimit) || dailyLossLimit < 0)
                dailyLossLimit = equity > 0 ? Math.Round(equity * _cfg.Bot.EmergencyCloseDrawdownPct / 100.0, 2) : 0;
            ApplyReviewDailyLossRemaining(snapshot, dailyLossLimit, dailyStats);
            ApplyReviewDataSourceSummary(snapshot);
        }

        private readonly record struct ReviewRiskMetrics(
            double Entry,
            double SuggestedSl,
            double SuggestedTp1,
            double SuggestedTp2,
            double SlDistancePips,
            double Tp1DistancePips,
            double Tp2DistancePips,
            double ActualRiskRewardRatio,
            double DollarRisk,
            double DollarProfitTp1,
            double DollarProfitTp2);

        private ReviewRiskMetrics BuildReviewRiskMetrics(
            JObject snapshot,
            TradeRequest request,
            EffectiveTradeSettings effective,
            double selectedLotSize)
        {
            string pair = request.Pair.ToUpperInvariant();
            var pairRules = _pairSettings?.GetForPair(pair);
            double pipSize = pairRules?.PipSize > 0
                ? pairRules.PipSize
                : LotCalculator.GetPipSize(pair);

            double ask = snapshot["price"]?["ask"]?.Value<double>() ?? 0;
            double bid = snapshot["price"]?["bid"]?.Value<double>() ?? 0;
            double entry = request.TradeType == TradeType.BUY ? ask : bid;
            if (entry <= 0)
                entry = request.EntryPrice > 0 ? request.EntryPrice : GetReviewReferenceEntry(snapshot, request);

            double sl = 0;
            double tp1 = 0;
            if (entry > 0 && pipSize > 0)
            {
                if (request.TradeType == TradeType.BUY)
                {
                    sl = entry - effective.SlPips * pipSize;
                    tp1 = entry + effective.TpPips * pipSize;
                }
                else
                {
                    sl = entry + effective.SlPips * pipSize;
                    tp1 = entry - effective.TpPips * pipSize;
                }
            }

            double tp2 = request.TakeProfit2;
            bool tp2Valid = IsReviewTakeProfitValid(request.TradeType, entry, tp2);
            double tp2Pips = tp2Valid && pipSize > 0 ? Math.Abs(tp2 - entry) / pipSize : 0;
            double actualRr = effective.SlPips > 0 ? effective.TpPips / effective.SlPips : 0;
            double lots = Math.Max(0.01, selectedLotSize);
            double pipValuePerLot = LotCalculator.GetPipValuePerLot(pair, entry > 0 ? entry : 1.0);
            double dollarRisk = Math.Round(lots * effective.SlPips * pipValuePerLot, 2);
            double dollarProfit = Math.Round(lots * effective.TpPips * pipValuePerLot, 2);
            double dollarProfit2 = Math.Round(lots * tp2Pips * pipValuePerLot, 2);

            return new ReviewRiskMetrics(
                entry,
                sl,
                tp1,
                tp2Valid ? tp2 : 0,
                effective.SlPips,
                effective.TpPips,
                tp2Pips,
                actualRr,
                dollarRisk,
                dollarProfit,
                dollarProfit2);
        }

        private static void PatchReviewRiskSnapshot(
            JObject snapshot,
            TradeRequest request,
            EffectiveTradeSettings effective,
            ReviewRiskMetrics metrics)
        {
            if (snapshot["risk"] is not JObject risk)
                return;

            risk["required_rr_ratio"] = effective.RiskRewardRatio;
            risk["suggested_sl"] = metrics.SuggestedSl;
            risk["suggested_tp1"] = metrics.SuggestedTp1;
            risk["suggested_tp2"] = metrics.SuggestedTp2;
            risk["sl_distance_pips"] = Math.Round(metrics.SlDistancePips, 1);
            risk["tp1_distance_pips"] = Math.Round(metrics.Tp1DistancePips, 1);
            risk["tp2_distance_pips"] = Math.Round(metrics.Tp2DistancePips, 1);
            risk["rr_ratio"] = Math.Round(metrics.ActualRiskRewardRatio, 2);
            risk["calculated_lot"] = request.LotSize;
            risk["dollar_risk"] = metrics.DollarRisk;
            risk["dollar_profit_tp1"] = metrics.DollarProfitTp1;
            risk["dollar_profit_tp2"] = metrics.DollarProfitTp2;

            if (snapshot["effective_trade_settings"] is JObject effectiveJson)
                effectiveJson["actual_risk_reward_ratio"] = Math.Round(metrics.ActualRiskRewardRatio, 2);

            snapshot["risk_summary"] = new JObject
            {
                ["source"] = "EffectiveTradeSettings",
                ["strategy"] = effective.Strategy,
                ["lot_size"] = request.LotSize,
                ["entry_price"] = metrics.Entry,
                ["suggested_sl"] = metrics.SuggestedSl,
                ["suggested_tp1"] = metrics.SuggestedTp1,
                ["suggested_tp2"] = metrics.SuggestedTp2,
                ["sl_distance_pips"] = Math.Round(metrics.SlDistancePips, 1),
                ["tp1_distance_pips"] = Math.Round(metrics.Tp1DistancePips, 1),
                ["tp2_distance_pips"] = Math.Round(metrics.Tp2DistancePips, 1),
                ["actual_risk_reward_ratio"] = Math.Round(metrics.ActualRiskRewardRatio, 2),
                ["required_risk_reward_ratio"] = effective.RiskRewardRatio,
                ["dollar_risk"] = metrics.DollarRisk,
                ["dollar_profit_tp1"] = metrics.DollarProfitTp1,
                ["dollar_profit_tp2"] = metrics.DollarProfitTp2
            };
        }

        private static void ApplyReviewDailySummary(
            JObject snapshot,
            ReviewDailyStats stats,
            string pair,
            string strategy)
        {
            JArray lastTrades = BuildReviewLastTradesJson(stats);

            snapshot["account_daily_summary"] = new JObject
            {
                ["source"] = "TradeHistoryByPairStrategyDate",
                ["pair"] = pair,
                ["strategy"] = strategy,
                ["date_utc"] = DateTime.UtcNow.Date.ToString("yyyy-MM-dd"),
                ["known"] = stats.Known,
                ["daily_pnl"] = stats.Known ? Math.Round(stats.TodayPnl, 2) : JValue.CreateNull(),
                ["daily_trades_taken"] = stats.Known ? stats.TradesToday : JValue.CreateNull(),
                ["total_pnl_today"] = stats.Known ? Math.Round(stats.TodayPnl, 2) : JValue.CreateNull(),
                ["total_trades_today"] = stats.Known ? stats.TradesToday : JValue.CreateNull(),
                ["last_5_trades"] = stats.Known ? lastTrades : JValue.CreateNull()
            };

            if (snapshot["account"] is JObject account)
            {
                account["daily_pnl"] = stats.Known ? Math.Round(stats.TodayPnl, 2) : JValue.CreateNull();
                account["daily_trades_taken"] = stats.Known ? stats.TradesToday : JValue.CreateNull();
            }

            var history = snapshot["history"] as JObject ?? new JObject();
            history["available"] = stats.Known;
            history["source"] = "TradeHistoryByPairStrategyDate";
            history["pair"] = pair;
            history["strategy"] = strategy;
            history["total_trades_today"] = stats.Known ? stats.TradesToday : JValue.CreateNull();
            history["total_pnl_today"] = stats.Known ? Math.Round(stats.TodayPnl, 2) : JValue.CreateNull();
            history["last_5_trades"] = stats.Known ? lastTrades.DeepClone() : JValue.CreateNull();
            snapshot["history"] = history;
        }

        private static JArray BuildReviewLastTradesJson(ReviewDailyStats stats)
        {
            var trades = new JArray();
            foreach (var trade in stats.LastTrades)
            {
                trades.Add(new JObject
                {
                    ["pair"] = trade.Pair,
                    ["strategy"] = trade.Strategy,
                    ["direction"] = trade.Direction,
                    ["result"] = trade.Result,
                    ["pips"] = trade.Pips.HasValue ? Math.Round(trade.Pips.Value, 1) : JValue.CreateNull(),
                    ["pnl"] = Math.Round(trade.Pnl, 2)
                });
            }

            return trades;
        }

        private static void ApplyReviewDailyLossRemaining(
            JObject snapshot,
            double dailyLossLimit,
            ReviewDailyStats stats)
        {
            double todayLossOnly = stats.Known ? Math.Max(0, -stats.TodayPnl) : 0;
            double remaining = Math.Round(Math.Max(0, dailyLossLimit - todayLossOnly), 2);

            if (snapshot["risk"] is JObject risk)
            {
                risk["daily_loss_limit_dollar"] = dailyLossLimit;
                risk["today_loss_only"] = Math.Round(todayLossOnly, 2);
                risk["daily_loss_remaining"] = remaining;
            }

            if (snapshot["account_daily_summary"] is JObject daily)
            {
                daily["daily_loss_limit_dollar"] = dailyLossLimit;
                daily["today_loss_only"] = Math.Round(todayLossOnly, 2);
                daily["daily_loss_remaining"] = remaining;
            }
        }

        private static void ApplyReviewSpreadSummary(
            JObject snapshot,
            double currentSpreadPips,
            double maxSpreadPips,
            bool spreadOk)
        {
            snapshot["spread_summary"] = new JObject
            {
                ["source"] = "EffectiveTradeSettings",
                ["current_spread_pips"] = double.IsNaN(currentSpreadPips) ? JValue.CreateNull() : Math.Round(currentSpreadPips, 1),
                ["max_spread_pips"] = maxSpreadPips,
                ["spread_ok"] = spreadOk,
                ["formula"] = "currentSpreadPips <= maxSpreadPips"
            };

            if (snapshot["price"] is JObject price)
                price["spread_normal"] = spreadOk;

            if (snapshot["effective_trade_settings"] is JObject effectiveJson)
            {
                effectiveJson["max_spread_pips"] = maxSpreadPips;
                effectiveJson["current_spread_pips"] = double.IsNaN(currentSpreadPips) ? JValue.CreateNull() : Math.Round(currentSpreadPips, 1);
            }
        }

        private static void ApplyReviewDataSourceSummary(JObject snapshot)
        {
            snapshot["data_sources"] = new JObject
            {
                ["mt5_direct"] = new JArray
                {
                    "account.balance",
                    "account.equity",
                    "account.free_margin",
                    "account.margin_used",
                    "account.margin_level",
                    "account.leverage",
                    "account.floating_pnl",
                    "symbol",
                    "price",
                    "candles",
                    "indicators",
                    "structure",
                    "levels",
                    "positions"
                },
                ["external_services"] = new JArray
                {
                    "news"
                },
                ["sqlite_trade_history"] = new JArray
                {
                    "account_daily_summary.daily_pnl",
                    "account_daily_summary.daily_trades_taken",
                    "account_daily_summary.total_pnl_today",
                    "account_daily_summary.total_trades_today",
                    "history.last_5_trades"
                },
                ["trade_page_effective_settings"] = new JArray
                {
                    "effective_trade_settings.stop_loss_pips",
                    "effective_trade_settings.take_profit_pips",
                    "effective_trade_settings.required_risk_reward_ratio",
                    "effective_trade_settings.max_trades",
                    "effective_trade_settings.max_spread_pips",
                    "effective_trade_settings.be_trigger_percent_of_tp"
                },
                ["app_computed"] = new JArray
                {
                    "risk_summary",
                    "spread_summary",
                    "execution_barriers",
                    "account_daily_summary.daily_loss_remaining",
                    "effective_trade_settings.actual_risk_reward_ratio"
                }
            };
        }

        private static TradeRequest CloneReviewRequest(TradeRequest request) => new()
        {
            Id = request.Id,
            Pair = request.Pair,
            TradeType = request.TradeType,
            OrderType = request.OrderType,
            EntryPrice = request.EntryPrice,
            StopLoss = request.StopLoss,
            TakeProfit = request.TakeProfit,
            TakeProfit2 = request.TakeProfit2,
            LotSize = request.LotSize,
            MaxSpreadPips = request.MaxSpreadPips,
            Comment = request.Comment,
            Strategy = request.Strategy,
            MagicNumber = request.MagicNumber,
            ExpiryMinutes = request.ExpiryMinutes,
            MoveSLToBreakevenAfterTP1 = request.MoveSLToBreakevenAfterTP1,
            CreatedAt = request.CreatedAt
        };

        private BotConfig BuildReviewSnapshotBotConfig(TradeRequest request)
        {
            double maxSpreadPips = EffectiveTradeSettings.Resolve(
                _cfg.Bot, request.Strategy, request.LotSize).MaxSpreadPips;

            return new BotConfig
            {
                MaxRiskPercent = _cfg.Bot.MaxRiskPercent,
                EmergencyCloseDrawdownPct = _cfg.Bot.EmergencyCloseDrawdownPct,
                MaxSpreadPips = maxSpreadPips,
                Scalping = CloneScalpingSettings(_cfg.Bot.Scalping),
                CommonTrading = CloneCommonTradingSettings(_cfg.Bot.CommonTrading),
                NormalTrading = CloneNormalTradingSettings(_cfg.Bot.NormalTrading)
            };
        }

        private static double ReadReviewNumber(JObject snapshot, string path)
        {
            var token = snapshot.SelectToken(path);
            if (token == null || token.Type == JTokenType.Null)
                return double.NaN;

            return double.TryParse(token.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double value)
                ? value
                : double.NaN;
        }

        private static double GetReviewReferenceEntry(JObject snapshot, TradeRequest request)
        {
            if (request.EntryPrice > 0)
                return request.EntryPrice;

            string pricePath = request.TradeType == TradeType.BUY ? "price.ask" : "price.bid";
            double livePrice = ReadReviewNumber(snapshot, pricePath);
            if (!double.IsNaN(livePrice) && livePrice > 0)
                return livePrice;

            return request.TradeType == TradeType.BUY
                ? request.StopLoss * 1.002
                : request.StopLoss * 0.998;
        }

        private static double CalculateReviewRiskReward(JObject snapshot, TradeRequest request)
        {
            double entry = GetReviewReferenceEntry(snapshot, request);
            return entry > 0
                && IsReviewStopLossValid(request.TradeType, entry, request.StopLoss)
                && IsReviewTakeProfitValid(request.TradeType, entry, request.TakeProfit)
                ? LotCalculator.RiskRewardRatio(entry, request.StopLoss, request.TakeProfit)
                : 0;
        }

        private static bool IsReviewStopLossValid(TradeType type, double entry, double stopLoss) =>
            entry > 0 && stopLoss > 0 && (type == TradeType.BUY ? stopLoss < entry : stopLoss > entry);

        private static bool IsReviewTakeProfitValid(TradeType type, double entry, double takeProfit) =>
            entry > 0 && takeProfit > 0 && (type == TradeType.BUY ? takeProfit > entry : takeProfit < entry);

        private static void UpsertReviewNumber(JObject snapshot, string path, double value)
        {
            var parts = path.Split('.');
            if (parts.Length == 0)
                return;

            JObject node = snapshot;
            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (node[parts[i]] is not JObject child)
                {
                    child = new JObject();
                    node[parts[i]] = child;
                }
                node = child;
            }

            node[parts[^1]] = Math.Round(value, 4);
        }

        private static void MergeReviewSnapshotSections(JObject target, JObject source, params string[] sectionNames)
        {
            foreach (string section in sectionNames)
            {
                if (!source.TryGetValue(section, out JToken? value))
                    continue;

                // Field-level merge for objects: null source values never overwrite real target values.
                // This preserves data (e.g. daily OHLC) that came from a richer snapshot and is absent
                // in the lightweight fast-refresh snapshot.
                if (value is JObject sourceObj && target[section] is JObject targetObj)
                {
                    foreach (var prop in sourceObj.Properties())
                    {
                        if (prop.Value.Type != JTokenType.Null)
                            targetObj[prop.Name] = prop.Value.DeepClone();
                    }
                }
                else
                {
                    target[section] = value.DeepClone();
                }
            }
        }

        private static string FormatReviewSyncAge(DateTime syncTime)
        {
            if (syncTime == DateTime.MinValue)
                return "-";

            double seconds = Math.Max(0, (DateTime.Now - syncTime).TotalSeconds);
            return seconds < 1 ? "now" : $"{seconds:0}s ago";
        }

        private void AddReviewGroup(
            FlowLayoutPanel parent,
            List<(string Path, Label Value, string Format)> bindings,
            ToolTip toolTip,
            string title,
            IReadOnlyList<(string Label, string Path, string Format)> metrics)
        {
            bool isBarrierGroup = metrics.Any(m => m.Format.StartsWith("barrier:", StringComparison.OrdinalIgnoreCase));
            int rowH          = isBarrierGroup ? 30 : 27;
            const int headerH = 24;
            const int padV    = 10;

            int groupH = headerH + padV + metrics.Count * rowH + padV;

            var bg = Color.FromArgb(14, 16, 26);
            var group = new GroupBox
            {
                Text      = title,
                Width     = isBarrierGroup ? 580 : 284,
                Height    = groupH,
                ForeColor = Color.FromArgb(160, 170, 200),
                BackColor = bg,
                Font      = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
                Padding   = new Padding(6, 4, 6, 6),
                Margin    = new Padding(0, 0, 12, 12)
            };
            group.Tag = isBarrierGroup ? "review-barrier" : "review-card";
            toolTip.SetToolTip(group, GetReviewGroupTooltip(title));

            var scroll = new Panel
            {
                Dock        = DockStyle.Fill,
                AutoScroll  = false,
                BackColor   = bg
            };
            group.Controls.Add(scroll);

            var grid = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                AutoSize    = false,
                ColumnCount = 2,
                RowCount    = metrics.Count,
                BackColor   = bg,
                Padding     = new Padding(2, 4, 2, 4),
                CellBorderStyle = TableLayoutPanelCellBorderStyle.Single
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));
            if (isBarrierGroup)
            {
                grid.ColumnStyles.Clear();
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 72));
            }
            scroll.Controls.Add(grid);

            for (int i = 0; i < metrics.Count; i++)
            {
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, rowH));

                var name = new Label
                {
                    Text      = metrics[i].Label,
                    Dock      = DockStyle.Fill,
                    ForeColor = Color.FromArgb(115, 124, 152),
                    BackColor = i % 2 == 0 ? Color.FromArgb(16, 19, 31) : Color.FromArgb(20, 23, 37),
                    Font      = new Font("Segoe UI", 8.2F),
                    TextAlign = ContentAlignment.MiddleLeft,
                    AutoEllipsis = true,
                    Padding   = new Padding(6, 0, 0, 0),
                    Cursor = Cursors.Help
                };

                var (initFg, initBg) = ReviewValueStyle(metrics[i].Path, null);
                var value = new Label
                {
                    Text      = "--",
                    Dock      = DockStyle.Fill,
                    ForeColor = initFg,
                    BackColor = initBg,
                    Font      = new Font("Consolas", 8.5F, FontStyle.Bold),
                    TextAlign = isBarrierGroup ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight,
                    AutoEllipsis = true,
                    Padding   = isBarrierGroup ? new Padding(8, 2, 8, 2) : new Padding(0, 2, 8, 2),
                    Cursor = Cursors.Help
                };

                string tip = GetReviewMetricTooltip(title, metrics[i].Label, metrics[i].Path, metrics[i].Format);
                toolTip.SetToolTip(name, tip);
                toolTip.SetToolTip(value, tip);

                grid.Controls.Add(name, 0, i);
                grid.Controls.Add(value, 1, i);
                bindings.Add((metrics[i].Path, value, metrics[i].Format));
            }

            parent.Controls.Add(group);
        }

        private static void ResizeReviewGroups(FlowLayoutPanel flow)
        {
            int available = Math.Max(320, flow.ClientSize.Width - 28);
            int columns = available >= 1180 ? 4
                : available >= 880 ? 3
                : available >= 620 ? 2
                : 1;

            int cardWidth = Math.Max(284, (available - (columns - 1) * 12) / columns);
            int barrierWidth = columns == 1
                ? cardWidth
                : Math.Min(available, cardWidth * Math.Min(columns, 2) + 12);

            foreach (Control control in flow.Controls)
            {
                if (control is not GroupBox group)
                    continue;

                bool isBarrier = string.Equals(group.Tag?.ToString(), "review-barrier", StringComparison.OrdinalIgnoreCase);
                group.Width = isBarrier ? barrierWidth : cardWidth;

                foreach (Control child in group.Controls)
                {
                    if (child is Panel panel)
                    {
                        foreach (Control inner in panel.Controls)
                        {
                            if (inner is TableLayoutPanel grid)
                                grid.Width = Math.Max(1, panel.ClientSize.Width);
                        }
                    }
                }
            }
        }

        private static int MeasureFlowPanelHeight(FlowLayoutPanel flow, int minimumHeight)
        {
            flow.PerformLayout();

            int height = flow.Padding.Top + flow.Padding.Bottom;
            foreach (Control child in flow.Controls)
            {
                if (!child.Visible)
                    continue;

                height = Math.Max(height, child.Bottom + child.Margin.Bottom + flow.Padding.Bottom);
            }

            return Math.Max(minimumHeight, height);
        }

        private static void EnableReviewDoubleBuffering(Control control)
        {
            try
            {
                typeof(Control)
                    .GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(control, true, null);
            }
            catch
            {
                // Rendering optimization only; ignore controls that do not expose this property.
            }

            foreach (Control child in control.Controls)
                EnableReviewDoubleBuffering(child);
        }

        private static string GetReviewGroupTooltip(string title)
        {
            string intro = title switch
            {
                "Pre-Trade Safety Checks" => "Hard safety checks that should pass before the trade can be started. These rows compare live/calculated values against configured limits.",
                "Account Health" => "Live account money and margin information from MT5. Use this to confirm the account can safely support another position.",
                "Live Price And Spread" => "Current bid, ask, spread, and daily price range for the selected pair. These values affect entry price and trading cost.",
                "Trade Risk Preview" => "Estimated money risk, profit targets, distances in pips, and risk/reward for this trade using the selected lot size.",
                "Broker Symbol Rules" => "Broker limits for this symbol, such as lot size range, lot step, execution mode, and whether trading is currently allowed.",
                "Market Session" => "Connection, market-open status, broker time, and active trading sessions. Session quality can affect liquidity and spread.",
                "H1 Indicator Signals" => "One-hour indicator readings used as market context. These values support analysis but do not execute trades by themselves.",
                "Recent Candle Behavior" => "Recent candle direction and candle-pattern clues across timeframes. Useful for timing and momentum checks.",
                "Market Structure" => "Trend and structure context across timeframes, including whether the market is aligned or choppy.",
                "Support And Resistance" => "Nearby support and resistance levels and how close price is to important levels.",
                "Open Position Check" => "Existing positions and recent execution context. Use this to avoid duplicate or conflicting trades.",
                "News Risk" => "Upcoming news-event risk. High-impact news can cause spread spikes and fast price movement.",
                _ => "Review information for this trade before starting it."
            };

            return $"{title}\n\n{intro}\n\nColor guide:\nGreen = safe/pass/favorable.\nYellow = caution or near a limit.\nRed = unsafe/fail/outside limit.\nBlue = information only.\nDim gray = static metadata, missing data, or not a direct safety signal.";
        }

        private static string GetReviewMetricTooltip(string groupTitle, string label, string path, string format)
        {
            string units = format switch
            {
                "money" => "Value is shown as account currency.",
                "price" => "Value is shown as a market price.",
                "pips" => "Value is shown in pips.",
                "pct" => "Value is shown as a percent.",
                "ratio" => "Value is shown as a ratio.",
                "lots" => "Value is shown in lots.",
                "bool" => "Yes means true or currently active. No means false or not active.",
                _ when format.StartsWith("barrier:", StringComparison.OrdinalIgnoreCase) => "This row shows current value versus the required safety rule.",
                _ => "Value is shown as reported by MT5 or the review snapshot."
            };

            string meaning = path switch
            {
                "execution_barriers.signal_valid_detail" => "Checks that the final generated order has a pair, direction, lot size, stop loss, and take profit.",
                "execution_barriers.signal_fresh_detail" => "Checks whether the signal is still inside its expiry window.",
                "execution_barriers.pair_allowed_detail" => "Checks whether this pair is allowed by the bot configuration.",
                "execution_barriers.daily_limit_detail" => "Checks whether today's trade count is still below the configured maximum.",
                "execution_barriers.account_detail" => "Checks whether MT5 returned usable equity and margin data.",
                "execution_barriers.rr_detail" => "Checks whether expected reward is high enough compared with the stop-loss risk.",
                "execution_barriers.free_margin_detail" => "Checks whether the account has enough available margin before opening another trade.",
                "execution_barriers.portfolio_risk_detail" => "Checks whether total open risk plus this trade stays within the account risk cap.",
                "execution_barriers.spread_detail" => "Checks whether current spread is inside the Trade Page max spread limit.",
                "execution_barriers.news_detail" => "Checks whether high-impact news is inside the configured blackout window.",
                "account.balance" => "Closed account value before current floating profit or loss.",
                "account.equity" => "Live account value including open trade profit or loss.",
                "account.free_margin" => "Margin still available for new trades.",
                "account.margin_used" => "Margin currently locked by open positions.",
                "account.margin_level" => "Equity divided by used margin. Lower values mean less margin safety.",
                "account.floating_pnl" => "Current unrealized profit or loss from open positions.",
                "account.daily_pnl" => "Profit or loss recorded for today's trading activity.",
                "account.daily_trades_taken" => "Number of trades opened today according to the snapshot.",
                "price.bid" => "Price used when selling or closing a buy position.",
                "price.ask" => "Price used when buying or closing a sell position.",
                "price.spread_pips" => "Trading cost gap between ask and bid. Lower spread is usually better.",
                "price.daily_open" => "Price at the start of the broker's trading day.",
                "price.daily_high" => "Highest price reached today.",
                "price.daily_low" => "Lowest price reached today.",
                "price.daily_range_pips" => "Distance between today's high and low.",
                "price.prev_day_high" => "Previous trading day's high, often watched as resistance.",
                "risk.dollar_risk" => "Estimated loss if stop loss is hit with the selected lot size.",
                "risk.dollar_profit_tp1" => "Estimated profit if the first take-profit target is hit.",
                "risk.dollar_profit_tp2" => "Estimated profit if the second take-profit target is hit.",
                "effective_trade_settings.trading_mode" => "Execution mode from the Trade Page: Auto executes after validation, Manual Approval shows a dialog, Paper Trading simulates without sending to MT5.",
                "effective_trade_settings.stop_loss_pips" => "Stop-loss pips from the selected Trade Page strategy.",
                "effective_trade_settings.take_profit_pips" => "Take-profit pips from the selected Trade Page strategy.",
                "effective_trade_settings.be_trigger_pips" => "Break-even trigger from Trade Page TP pips multiplied by the Common Trade Settings BE Trigger % of TP.",
                "risk.rr_ratio" => "Reward compared with risk. 1.5 means target profit is 1.5 times the stop-loss risk.",
                "risk.max_risk_pct" => "Maximum account percentage allowed for one trade by configuration.",
                "risk.daily_loss_remaining" => "Estimated loss room left before the daily loss protection limit is reached.",
                "symbol.name" => "Exact broker symbol that MT5 is using for this pair.",
                "symbol.digits" => "Number of decimal places used in this symbol's price.",
                "symbol.min_lot" => "Smallest lot size the broker allows for this symbol.",
                "symbol.max_lot" => "Largest lot size the broker allows for this symbol.",
                "symbol.lot_step" => "Smallest allowed lot-size increment.",
                "symbol.trade_allowed" => "Whether MT5 and the broker currently allow trading this symbol.",
                "symbol.execution_mode" => "How the broker executes orders for this symbol.",
                "symbol.filling_mode" => "Allowed order filling behavior for this symbol.",
                "session.broker_time" => "Current server time reported by the broker.",
                "session.terminal_connected" => "Whether the MT5 terminal connection is active.",
                "session.market_open" => "Whether the market appears open for trading.",
                "session.london_open" => "Whether London session conditions are active.",
                "session.newyork_open" => "Whether New York session conditions are active.",
                "session.overlap_active" => "Whether London and New York sessions overlap, often a more liquid period.",
                "session.session_name" => "Current detected trading session name.",
                "session.is_weekend" => "Whether the market is in weekend status.",
                "indicators.h1.rsi" => "Relative Strength Index on H1. High can mean overbought, low can mean oversold.",
                "indicators.h1.rsi_signal" => "Plain-language interpretation of the H1 RSI value.",
                "indicators.h1.macd_bias" => "MACD directional bias on H1.",
                "indicators.h1.ema20" => "Shorter-term moving average on H1.",
                "indicators.h1.ema50" => "Medium-term moving average on H1.",
                "indicators.h1.ema200" => "Long-term trend moving average on H1.",
                "indicators.h1.adx" => "Trend-strength reading. Higher values usually mean stronger trend.",
                "indicators.h1.atr" => "Average True Range on H1, used as volatility context.",
                "candles.h4_last.direction" => "Direction of the latest H4 candle.",
                "candles.h1_last.direction" => "Direction of the latest H1 candle.",
                "candles.m15_last.direction" => "Direction of the latest M15 candle.",
                "candles.m5_last.direction" => "Direction of the latest M5 candle.",
                "candles.h1_last.body_pips" => "Body size of the latest H1 candle, excluding wicks.",
                "candles.m15_last.body_pips" => "Body size of the latest M15 candle, excluding wicks.",
                "candles.m5_last.is_doji" => "Doji candles can show hesitation or indecision.",
                "candles.m15_last.is_inside_bar" => "Inside bars can show compression before a move.",
                "structure.trend_h4" => "Detected trend direction on H4.",
                "structure.trend_h1" => "Detected trend direction on H1.",
                "structure.trend_m15" => "Detected trend direction on M15.",
                "structure.trend_m5" => "Detected trend direction on M5.",
                "structure.all_timeframes_aligned" => "Whether the checked timeframes point in the same direction.",
                "structure.market_regime" => "Detected market condition, such as trend or range.",
                "structure.swing_high" => "Nearby recent high used as structure reference.",
                "structure.swing_low" => "Nearby recent low used as structure reference.",
                "levels.nearest_support_1" => "Closest lower price area where buyers may appear.",
                "levels.nearest_support_2" => "Second lower support area.",
                "levels.nearest_resistance_1" => "Closest upper price area where sellers may appear.",
                "levels.nearest_resistance_2" => "Second upper resistance area.",
                "levels.distance_to_support_pips" => "How far current price is from nearest support.",
                "levels.distance_to_resistance_pips" => "How far current price is from nearest resistance.",
                "levels.price_at_key_level" => "Whether price is close to a detected support or resistance area.",
                "levels.key_level_type" => "Type of key level closest to price.",
                "positions.total_open" => "Total open positions currently reported by MT5.",
                "positions.same_pair_open" => "Whether this pair already has an open position.",
                "positions.same_pair_direction" => "Direction of any existing position on this pair.",
                "positions.duplicate_trade_exists" => "Whether a same-pair, same-direction trade already exists.",
                "positions.opposite_trade_exists" => "Whether an opposite-direction trade exists for this pair.",
                "last_order.execution_result" => "Most recent order result reported in the review snapshot.",
                "last_order.ticket" => "Broker ticket number for the last order if available.",
                "history.win_rate_today_pct" => "Today's approximate win rate from available history.",
                "news.news_risk_level" => "Detected news risk level for this pair or market.",
                "news.high_impact_next_60_min" => "Whether high-impact news is expected within the next hour.",
                "news.blackout_active" => "Whether a high-impact event is inside the configured no-trade window right now.",
                "news.next_event" => "Next relevant economic calendar event for either currency in the pair.",
                "news.reason" => "Plain-language explanation for the current news risk color and barrier result.",
                "news.relevant_event_count" => "Number of matching news events found for the pair currencies in the next review window.",
                "news.source" => "Source used for the news risk data.",
                _ => $"Shows {label.ToLowerInvariant()} for this trade review."
            };

            string source = GetReviewMetricSource(path, format);
            string color = GetReviewMetricColorExplanation(path, format);

            return $"{label}\n\nWhat it means:\n{meaning}\n\nWhere it comes from:\n{source}\n\nHow to read the color:\n{color}\n\nFormat:\n{units}";
        }

        private static string GetReviewMetricSource(string path, string format)
        {
            if (format.StartsWith("barrier:", StringComparison.OrdinalIgnoreCase))
            {
                return path switch
                {
                    "execution_barriers.signal_valid_detail" => "Calculated inside the Review Trade window after applying Trade Page SL/TP pips to the current Bid/Ask.",
                    "execution_barriers.signal_fresh_detail" => "Calculated inside the Review Trade window from signal created_at and expiry_minutes.",
                    "execution_barriers.pair_allowed_detail" => "Calculated from the signal pair and the Bot Configuration allowed-pair list, which is synced from Pair Settings.",
                    "execution_barriers.daily_limit_detail" => "Uses today's strategy-specific trade count from the review snapshot and compares it with the selected Trade Page max trades setting.",
                    "execution_barriers.account_detail" => "Uses live account equity and free margin returned by MT5 through the EA/bridge snapshot.",
                    "execution_barriers.rr_detail" => "Calculated from entry, stop loss, take profit, and the Trade Page R:R setting.",
                    "execution_barriers.free_margin_detail" => "Uses live MT5 free margin and balance from the account snapshot.",
                    "execution_barriers.portfolio_risk_detail" => "Calculated from live open positions, their stop losses, this trade's selected lot size, and Bot Configuration max total risk.",
                    "execution_barriers.spread_detail" => "Uses live MT5 spread and compares it with the Trade Page max spread setting for the selected strategy.",
                    "execution_barriers.news_detail" => "Uses cached Financial Modeling Prep economic-calendar events and compares them with configured news blackout minutes.",
                    _ => "Calculated inside the Review Trade window from the current snapshot and bot safety settings."
                };
            }

            if (path.StartsWith("account.", StringComparison.OrdinalIgnoreCase))
                return "Live account data from MT5 through the EA/bridge snapshot. If MT5 cannot provide it, the value may show as unavailable.";
            if (path.StartsWith("price.", StringComparison.OrdinalIgnoreCase))
                return "Live symbol price data from MT5 through the EA/bridge snapshot. Spread is broker bid/ask difference converted to pips.";
            if (path.StartsWith("risk.", StringComparison.OrdinalIgnoreCase))
                return path switch
                {
                    "risk.max_risk_pct" => "Read from Bot Configuration max risk percent.",
                    "risk.daily_loss_remaining" => "Calculated from account/history snapshot data and the configured daily loss limit when available.",
                    _ => "Calculated in the Review Trade window from the signal entry/SL/TP, selected lot size, pip size, and account settings."
                };
            if (path.StartsWith("symbol.", StringComparison.OrdinalIgnoreCase))
                return "Broker symbol rules from MT5 for the selected pair, such as lot limits and whether trading is allowed.";
            if (path.StartsWith("session.", StringComparison.OrdinalIgnoreCase))
                return "Session and connection data from MT5/server time in the EA/bridge snapshot.";
            if (path.StartsWith("indicators.", StringComparison.OrdinalIgnoreCase))
                return "Indicator values calculated by the MT5 Expert Advisor snapshot from live chart data.";
            if (path.StartsWith("candles.", StringComparison.OrdinalIgnoreCase))
                return "Recent candle data calculated by the MT5 Expert Advisor snapshot from live chart candles.";
            if (path.StartsWith("structure.", StringComparison.OrdinalIgnoreCase))
                return "Market structure estimate calculated by the MT5 Expert Advisor snapshot from recent price action.";
            if (path.StartsWith("levels.", StringComparison.OrdinalIgnoreCase))
                return "Support/resistance estimate calculated by the MT5 Expert Advisor snapshot from recent highs and lows.";
            if (path.StartsWith("positions.", StringComparison.OrdinalIgnoreCase))
                return "Live open-position data from MT5, filtered for this pair and direction where applicable.";
            if (path.StartsWith("last_order.", StringComparison.OrdinalIgnoreCase))
                return "Most recent order result stored in the review snapshot after an execution attempt, if one exists.";
            if (path.StartsWith("history.", StringComparison.OrdinalIgnoreCase))
                return "Trade-history summary from the MT5/review snapshot when available.";
            if (path.StartsWith("news.", StringComparison.OrdinalIgnoreCase))
                return "Live/cached Financial Modeling Prep economic-calendar data filtered by the pair currencies, configured impact level, and blackout minutes. If no API key is set, it shows unavailable.";

            return "Read from the current Review Trade snapshot.";
        }

        private static string GetReviewMetricColorExplanation(string path, string format)
        {
            if (format.StartsWith("barrier:", StringComparison.OrdinalIgnoreCase))
                return "Green means this safety check passed. Red means this rule failed and can block or warn before trade start. The text shows current value versus the required base rule.";

            if (path is "symbol.digits" or "symbol.min_lot" or "symbol.max_lot" or "symbol.lot_step"
                     or "symbol.execution_mode" or "symbol.filling_mode"
                     or "last_order.ticket" or "news.source" or "levels.key_level_type"
                     or "account.daily_trades_taken" or "session.session_name")
                return "Dim gray/blue means informational metadata. It usually does not mean good or bad by itself.";

            if (path is "price.spread_pips")
                return "Green means spread is within the allowed limit. Yellow/red means spread is expensive or above the configured limit.";

            if (path.StartsWith("price.", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("indicators.", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("candles.", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("structure.trend_", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("levels.nearest_", StringComparison.OrdinalIgnoreCase))
                return "Blue means context-only market information. Use it for analysis, but it is not a direct pass/fail safety check.";

            if (path is "risk.rr_ratio")
                return "Green means risk/reward meets or beats the configured minimum. Red means the reward is too small for the risk.";

            if (path is "risk.dollar_risk" or "risk.max_risk_pct" or "account.daily_pnl" or "risk.daily_loss_remaining")
                return "Green is healthy or within limit. Yellow warns the value is getting close to a limit. Red means the value is risky or outside the safety threshold.";

            if (format == "bool")
                return "Green generally means Yes is safe/active/available. Red generally means No is unsafe/unavailable. Some context-only booleans may be blue or dim.";

            return "Green means favorable or within a configured safety limit. Yellow means caution. Red means unsafe or outside a limit. Blue means information only. Dim means static metadata or unavailable context.";
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
            NormalizeReviewSnapshotForDisplay(snapshot);

            foreach (var binding in bindings)
            {
                var token = ResolveReviewDisplayToken(snapshot, binding.Path);
                binding.Value.Text = FormatReviewValue(token, binding.Format);
                string stylePath = binding.Path;
                JToken? styleToken = token;
                if (TryGetReviewStylePath(binding.Format, out string barrierPath))
                {
                    stylePath = barrierPath;
                    styleToken = snapshot.SelectToken(barrierPath);
                }

                var (fg, bg) = ReviewValueStyle(stylePath, styleToken);
                binding.Value.ForeColor = fg;
                binding.Value.BackColor = bg;
            }
        }

        private static JToken? ResolveReviewDisplayToken(JObject snapshot, string path)
        {
            var token = snapshot.SelectToken(path);
            if (token != null && token.Type != JTokenType.Null)
                return token;

            string rootName = path.Split('.', 2)[0];
            if (snapshot[rootName] is JObject root &&
                root.Value<bool?>("available") == false)
            {
                string reason = root.Value<string>("reason") ?? "Data source did not return this section.";
                return new JValue($"Unavailable: {reason}");
            }

            return token;
        }

        private static void NormalizeReviewSnapshotForDisplay(JObject snapshot)
        {
            double marginUsed = ReadReviewNumber(snapshot, "account.margin_used");
            double marginLevel = ReadReviewNumber(snapshot, "account.margin_level");
            if (!double.IsNaN(marginUsed) && marginUsed <= 0 &&
                !double.IsNaN(marginLevel) && marginLevel <= 0 &&
                snapshot["account"] is JObject account)
            {
                account["margin_level"] = null;
            }
        }

        private static string FormatReviewValue(JToken? token, string format)
        {
            if (token == null || token.Type == JTokenType.Null)
                return "--";

            if (format.StartsWith("barrier:", StringComparison.OrdinalIgnoreCase))
                return token.ToString(Formatting.None).Trim('"');

            if (format == "bool")
            {
                if (token.Type == JTokenType.Boolean)
                    return token.Value<bool>() ? "Yes" : "No";

                return token.ToString(Formatting.None).Trim('"');
            }

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

        private static bool TryGetReviewStylePath(string format, out string path)
        {
            const string prefix = "barrier:";
            if (format.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                path = format[prefix.Length..];
                return !string.IsNullOrWhiteSpace(path);
            }

            path = "";
            return false;
        }

        private static List<string> GetFailedReviewBarrierMessages(
            JObject snapshot,
            bool allowAiCompletion = false,
            bool allowAutoScalping = false)
        {
            var barriers = new (string Label, string FlagPath, string DetailPath)[]
            {
                ("Final order fields valid", "execution_barriers.signal_valid", "execution_barriers.signal_valid_detail"),
                ("Signal is not expired", "execution_barriers.signal_fresh", "execution_barriers.signal_fresh_detail"),
                ("Pair is allowed", "execution_barriers.pair_allowed", "execution_barriers.pair_allowed_detail"),
                ("Daily trade limit", "execution_barriers.daily_limit_ok", "execution_barriers.daily_limit_detail"),
                ("Account data available", "execution_barriers.account_ok", "execution_barriers.account_detail"),
                ("Risk/reward rule", "execution_barriers.rr_ok", "execution_barriers.rr_detail"),
                ("Free margin available", "execution_barriers.free_margin_ok", "execution_barriers.free_margin_detail"),
                ("Total account risk cap", "execution_barriers.portfolio_risk_ok", "execution_barriers.portfolio_risk_detail"),
                ("Spread within limit", "execution_barriers.spread_ok", "execution_barriers.spread_detail"),
                ("News blackout clear", "execution_barriers.news_ok", "execution_barriers.news_detail")
            };

            var failed = new List<string>();
            foreach (var barrier in barriers)
            {
                bool ok = snapshot.SelectToken(barrier.FlagPath)?.Value<bool?>() == true;
                if (ok)
                    continue;

                string detail = snapshot.SelectToken(barrier.DetailPath)?.ToString(Formatting.None).Trim('"') ?? "No detail available";
                if (allowAutoScalping && IsAutoScalpingCompletableReviewBarrier(barrier.FlagPath))
                    continue;
                if (allowAiCompletion && IsAiCompletableReviewBarrier(barrier.FlagPath, detail))
                    continue;

                failed.Add($"{barrier.Label}: {detail}");
            }

            return failed;
        }

        private List<TradeWarningItem> BuildReviewWarningItems(
            JObject snapshot,
            TradeRequest request,
            bool aiEnabled,
            bool autoScalpingRequested,
            double selectedLotSize,
            int selectedLeverage)
        {
            var warnings = new List<TradeWarningItem>();
            string pair = request.Pair.ToUpperInvariant();
            var pairRules = _pairSettings?.GetForPair(pair);
            var effective = EffectiveTradeSettings.Resolve(
                _cfg.Bot, autoScalpingRequested ? "Scalping" : "Normal", selectedLotSize);
            double requiredRr = effective.RiskRewardRatio;
            string strategyName = effective.Strategy;
            double maxSpread = effective.MaxSpreadPips;

            if (!autoScalpingRequested && !aiEnabled)
            {
                warnings.Add(new TradeWarningItem(
                    "AI analysis will be skipped",
                    "This trade will use the visible signal values directly, without AI re-analysis before creating the order.",
                    "AI API key/model is not configured for review approval",
                    "AI API Config tab: API key and model fields",
                    "Configured AI provider for pre-trade analysis",
                    "AI API Config tab: configured provider settings",
                    "Manual/direct execution can continue only after you confirm this warning."));
            }

            double rr = ReadReviewNumber(snapshot, "effective_trade_settings.actual_risk_reward_ratio");
            if (double.IsNaN(rr))
                rr = ReadReviewNumber(snapshot, "risk.rr_ratio");
            if (double.IsNaN(rr))
                rr = CalculateReviewRiskReward(snapshot, request);
            if (!double.IsNaN(rr) && requiredRr > 0)
            {
                bool rrWarn = rr >= requiredRr && rr < requiredRr * 1.15;
                if (rrWarn)
                {
                    warnings.Add(new TradeWarningItem(
                        "Risk/reward is close to the required ratio",
                        "The reward compared with stop-loss risk is weak for this pair. A small spread or entry movement can reduce the effective R:R further.",
                        $"{rr:0.00} R:R",
                        "Actual TP pips / SL pips after applying visible inputs",
                        $"{requiredRr:0.00} minimum R:R",
                        $"{strategyName} trade page Min R:R setting",
                        Math.Abs(rr - requiredRr) < 0.005
                            ? "Current value exactly equals the required value, so there is no buffer for spread, slippage, or entry movement."
                            : "Current value passes, but is less than 15% above the required value."));
                }
            }

            double spread = ReadReviewNumber(snapshot, "price.spread_pips");
            if (autoScalpingRequested && !double.IsNaN(spread) && spread > 0)
            {
                double maxSpreadPercentOfTp = Math.Max(0.1, _cfg.Bot.Scalping.MaxSpreadPercentOfTp);
                double requiredTpFromLiveRules = Math.Max(
                    effective.SlPips * requiredRr,
                    spread * (100.0 / maxSpreadPercentOfTp));
                if (requiredTpFromLiveRules > effective.TpPips + 1e-9)
                {
                    warnings.Add(new TradeWarningItem(
                        "Scalping TP is below live spread/R:R requirement",
                        "The scalping session can start, but it will wait until live conditions improve or the Trade Page TP is large enough for the current spread and R:R guardrails.",
                        $"{effective.TpPips:0.0} TP pips",
                        "Scalping trade page TP pips after applying visible inputs",
                        $">= {requiredTpFromLiveRules:0.0} TP pips",
                        $"Live rule: max(SL {effective.SlPips:0.0} x R:R {requiredRr:0.00}, spread {spread:0.0} / {maxSpreadPercentOfTp:0.#}%)",
                        $"Current TP is short by {(requiredTpFromLiveRules - effective.TpPips):0.0} pips for the current live spread/R:R rule."));
                }
            }

            if (maxSpread > 0 && !double.IsNaN(spread) && spread <= maxSpread && spread >= maxSpread * 0.75)
            {
                warnings.Add(new TradeWarningItem(
                    "Spread is near the maximum allowed",
                    "The broker spread is high relative to the configured limit, so entry cost is already elevated before the trade starts.",
                    $"{spread:0.0} pips",
                    "Live MT5 symbol snapshot: price.spread_pips",
                    $"<= {maxSpread:0.0} pips",
                    $"{strategyName} trade page Max Spread Pips",
                    "Current spread is at least 75% of the base limit."));
            }

            double equity = ReadReviewNumber(snapshot, "account.equity");
            double balance = ReadReviewNumber(snapshot, "account.balance");
            double freeMargin = ReadReviewNumber(snapshot, "account.free_margin");
            double dollarRisk = ReadReviewNumber(snapshot, "risk.dollar_risk");
            if (equity > 0 && dollarRisk > 0 && _cfg.Bot.MaxRiskPercent > 0)
            {
                double riskPct = dollarRisk / equity * 100.0;
                if (riskPct <= _cfg.Bot.MaxRiskPercent && riskPct >= _cfg.Bot.MaxRiskPercent * 0.80)
                {
                    warnings.Add(new TradeWarningItem(
                        "Trade risk is close to the per-trade limit",
                        "This trade uses a large portion of the allowed risk for one position.",
                        $"{riskPct:0.00}% (${dollarRisk:0.00})",
                        "Review Trade risk preview: selected lot size, entry, stop loss, live equity",
                        $"<= {_cfg.Bot.MaxRiskPercent:0.00}%",
                        "Bot Configuration: Max Risk %",
                        "Current risk is at least 80% of the configured max risk per trade."));
                }
            }

            if (balance > 0 && freeMargin > 0 && freeMargin >= balance * 0.05 && freeMargin < balance * 0.10)
            {
                warnings.Add(new TradeWarningItem(
                    "Free margin is getting low",
                    "The account passes the hard margin check, but free margin is near the minimum safety floor.",
                    $"{freeMargin:0.00}",
                    "Live MT5 account snapshot: account.free_margin",
                    $">= {(balance * 0.05):0.00} hard floor; preferred >= {(balance * 0.10):0.00}",
                    "Review safety rule: 5% of account.balance hard floor, 10% caution level",
                    "Current value is below the 10% caution level but above the 5% hard block."));
            }

            double totalRiskPct = ReadReviewBarrierCurrentPercent(snapshot, "execution_barriers.portfolio_risk_detail");
            if (_cfg.Bot.MaxTotalRiskPercent > 0 && !double.IsNaN(totalRiskPct)
                && totalRiskPct <= _cfg.Bot.MaxTotalRiskPercent
                && totalRiskPct >= _cfg.Bot.MaxTotalRiskPercent * 0.80)
            {
                warnings.Add(new TradeWarningItem(
                    "Total portfolio risk is close to the cap",
                    "Open trade risk plus this new trade is near the configured total account risk limit.",
                    $"{totalRiskPct:0.0}%",
                    "Review Trade portfolio risk check: open positions plus this selected lot size",
                    $"<= {_cfg.Bot.MaxTotalRiskPercent:0.0}%",
                    "Bot Configuration: Max Total Risk %",
                    "Current total risk is at least 80% of the configured cap."));
            }

            double tradesToday = ReadReviewNumber(snapshot, "account.daily_trades_taken");
            int maxTrades = effective.MaxTrades;
            if (!double.IsNaN(tradesToday) && maxTrades > 1
                && tradesToday < maxTrades
                && tradesToday >= maxTrades - 1)
            {
                warnings.Add(new TradeWarningItem(
                    $"{strategyName} trade limit is almost reached",
                    "Starting this trade may leave little or no room for another approved trade today.",
                    $"{tradesToday:0} {strategyName} trades today",
                    "Review Trade account snapshot: account.daily_trades_taken",
                    $"< {maxTrades} trades per day",
                    $"{strategyName} Trade Page: Max Trades",
                    "Current value is within one trade of the daily limit."));
            }

            string newsProvider = _cfg.ApiIntegrations.NewsProvider;
            string newsRisk = snapshot.SelectToken("news.news_risk_level")?.ToString() ?? "UNAVAILABLE";
            string newsReason = snapshot.SelectToken("news.reason")?.ToString() ?? "No news detail available.";
            bool newsConfigured = snapshot.SelectToken("news.configured")?.Value<bool?>() == true;
            bool newsDisabled = string.Equals(newsProvider, "None", StringComparison.OrdinalIgnoreCase);
            if (!newsDisabled && (!newsConfigured || newsRisk is "MEDIUM" or "UNAVAILABLE"))
            {
                warnings.Add(new TradeWarningItem(
                    newsConfigured ? "News risk is not low" : "News data is unavailable",
                    newsReason,
                    newsConfigured ? newsRisk : "Provider/API data unavailable",
                    "News Filter snapshot: configured economic-calendar provider response",
                    "LOW risk or disabled news filter",
                    "AI API Config: news provider, impact filter, blackout settings",
                    "Current news state is not a hard block with the current settings, but it can affect spread and volatility."));
            }

            if (selectedLeverage >= 500)
            {
                warnings.Add(new TradeWarningItem(
                    "Selected leverage is high",
                    "High leverage can make small price movement consume margin quickly if lot size is too large.",
                    $"{selectedLeverage}:1",
                    "Review Trade window: leverage selector",
                    "Lower leverage when possible for safer margin usage",
                    "Operational safety guideline shown by Review Trade warning rules",
                    "Current leverage is 500:1 or higher."));
            }

            if (Math.Abs(selectedLotSize - request.LotSize) >= 0.001)
            {
                warnings.Add(new TradeWarningItem(
                    "Lot size was changed in review",
                    "The trade will use the lot size selected in the Review Trade window, not the original signal lot size.",
                    $"{selectedLotSize:0.##} lots",
                    "Review Trade window: selected lot size control",
                    $"{request.LotSize:0.##} lots from signal",
                    "Signal JSON: lot_size field",
                    "Selected review value overrides the original signal value."));
            }

            return warnings;
        }

        private static double ReadReviewBarrierCurrentPercent(JObject snapshot, string detailPath)
        {
            string detail = snapshot.SelectToken(detailPath)?.ToString() ?? "";
            const string prefix = "Current:";
            int start = detail.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
            if (start < 0) return double.NaN;
            start += prefix.Length;
            int percent = detail.IndexOf('%', start);
            if (percent < 0) return double.NaN;

            string number = detail[start..percent].Trim();
            return double.TryParse(number, NumberStyles.Any, CultureInfo.InvariantCulture, out double value)
                ? value
                : double.NaN;
        }

        private static bool IsAiCompletableReviewBarrier(string flagPath, string detail)
        {
            if (flagPath == "execution_barriers.signal_fresh")
                return true;

            if (flagPath != "execution_barriers.signal_valid")
                return false;

            return detail.Contains("StopLoss cannot be 0", StringComparison.OrdinalIgnoreCase)
                || detail.Contains("TakeProfit cannot be 0", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAutoScalpingCompletableReviewBarrier(string flagPath) =>
            flagPath is "execution_barriers.signal_valid"
                     or "execution_barriers.signal_fresh"
                     or "execution_barriers.rr_ok";

        private static (Color Fg, Color Bg) ReviewValueStyle(string path, JToken? token)
        {
            var critFg = Color.FromArgb(255, 95,  95);  var critBg = Color.FromArgb(72,  16, 16);
            var warnFg = Color.FromArgb(255, 200, 60);  var warnBg = Color.FromArgb(66,  50, 10);
            var goodFg = Color.FromArgb(72,  218, 128); var goodBg = Color.FromArgb(14,  56, 36);
            var infoFg = Color.FromArgb(110, 185, 255); var infoBg = Color.FromArgb(16,  36, 58);
            var normFg = Color.FromArgb(200, 210, 235); var normBg = Color.FromArgb(20,  24, 40);
            var dimFg  = Color.FromArgb(88,  96,  120); var dimBg  = Color.FromArgb(15,  17, 25);

            // Ignorable - metadata / static config
            if (path is "symbol.digits" or "symbol.min_lot" or "symbol.max_lot" or "symbol.lot_step"
                     or "symbol.execution_mode" or "symbol.filling_mode"
                     or "last_order.ticket" or "news.source" or "levels.key_level_type"
                     or "account.daily_trades_taken" or "session.session_name")
                return (dimFg, dimBg);

            // Info-only - price levels, indicators, candle data
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

            if (raw.StartsWith("Unavailable:", StringComparison.OrdinalIgnoreCase))
                return (dimFg, dimBg);

            if (path.StartsWith("execution_barriers.", StringComparison.OrdinalIgnoreCase) &&
                token.Type == JTokenType.Boolean)
            {
                return boolVal ? (goodFg, goodBg) : (critFg, critBg);
            }

            // â"€â"€ Boolean fields â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€
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
                case "news.blackout_active":
                    return boolVal ? (critFg, critBg) : (goodFg, goodBg);

                case "structure.all_timeframes_aligned":
                    return boolVal ? (goodFg, goodBg) : (warnFg, warnBg);

                case "levels.price_at_key_level":
                    return boolVal ? (goodFg, goodBg) : (normFg, normBg);
            }

            // â"€â"€ Numeric fields â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€
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

            // â"€â"€ String fields â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€
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

        private static JObject BuildReviewNewsJson(NewsRiskSnapshot news)
        {
            var next = news.RelevantEvents
                .Where(e => e.EventTimeUtc >= DateTime.UtcNow)
                .OrderBy(e => e.EventTimeUtc)
                .FirstOrDefault();

            return new JObject
            {
                ["news_risk_level"] = news.RiskLevel,
                ["risk_level"] = news.RiskLevel,
                ["high_impact_next_60_min"] = news.HighImpactNext60Minutes,
                ["has_high_impact_event_next_60_minutes"] = news.HighImpactNext60Minutes,
                ["blackout_active"] = news.IsBlackoutActive,
                ["is_blackout_active"] = news.IsBlackoutActive,
                ["configured"] = news.IsConfigured,
                ["is_configured"] = news.IsConfigured,
                ["source"] = news.Source,
                ["data_source"] = news.Source,
                ["reason"] = news.Reason,
                ["status_reason"] = news.Reason,
                ["checked_at_utc"] = news.CheckedAtUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                ["cache_updated_at_utc"] = news.CacheUpdatedAtUtc == DateTime.MinValue
                    ? null
                    : news.CacheUpdatedAtUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                ["next_event"] = next == null
                    ? "None in next 24h"
                    : $"{next.EventTimeUtc:HH:mm} UTC {next.Currency} {next.Impact}: {next.Title}",
                ["next_relevant_event_summary"] = next == null
                    ? "None in next 24h"
                    : $"{next.EventTimeUtc:HH:mm} UTC {next.Currency} {next.Impact}: {next.Title}",
                ["next_event_time_utc"] = next?.EventTimeUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                ["blocking_event_count"] = news.BlockingEvents.Count,
                ["blackout_event_count"] = news.BlockingEvents.Count,
                ["relevant_event_count"] = news.RelevantEvents.Count,
                ["events"] = JArray.FromObject(news.RelevantEvents.Select(e => new
                {
                    time_utc = e.EventTimeUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                    e.Currency,
                    e.Country,
                    e.Impact,
                    e.Title,
                    e.Previous,
                    e.Forecast,
                    e.Actual,
                    e.Source
                })),
                ["blackout_events"] = JArray.FromObject(news.BlockingEvents.Select(e => new
                {
                    time_utc = e.EventTimeUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                    e.Currency,
                    e.Country,
                    e.Impact,
                    e.Title,
                    e.Previous,
                    e.Forecast,
                    e.Actual,
                    e.Source
                }))
            };
        }

        private async Task TryEnrichReviewFallbackSnapshotAsync(
            JObject snapshot,
            TradeRequest request,
            SymbolInfo? symbol)
        {
            if (_bridge == null) return;

            try
            {
                DateTime to = DateTime.UtcNow;
                var m5 = await GetReviewRatesAsync(request.Pair, "M5", to.AddHours(-12), to, 180).ConfigureAwait(false);
                var m15 = await GetReviewRatesAsync(request.Pair, "M15", to.AddDays(-2), to, 220).ConfigureAwait(false);
                var h1 = await GetReviewRatesAsync(request.Pair, "H1", to.AddDays(-12), to, 320).ConfigureAwait(false);

                if (m5.Count == 0 && m15.Count == 0 && h1.Count == 0)
                    return;

                double pipSize = ReviewPipSize(request, symbol);
                double bid = snapshot["price"]?["bid"]?.Value<double?>()
                    ?? symbol?.Bid
                    ?? h1.LastOrDefault()?.Close
                    ?? m15.LastOrDefault()?.Close
                    ?? m5.LastOrDefault()?.Close
                    ?? 0;

                var h4 = AggregateReviewCandles(h1, 4);
                snapshot["candles"] = new JObject
                {
                    ["h4_last"] = BuildReviewCandleJson(h4, h1.Count >= 8 ? AggregateReviewCandles(h1.Take(h1.Count - 4).ToList(), 4) : null, pipSize),
                    ["h1_last"] = BuildReviewCandleJson(LastOrNull(h1), PreviousOrNull(h1), pipSize),
                    ["m15_last"] = BuildReviewCandleJson(LastOrNull(m15), PreviousOrNull(m15), pipSize),
                    ["m5_last"] = BuildReviewCandleJson(LastOrNull(m5), PreviousOrNull(m5), pipSize)
                };

                var h1Indicators = BuildReviewIndicatorJson(h1, pipSize);
                snapshot["indicators"] = new JObject
                {
                    ["h1"] = h1Indicators,
                    ["m15"] = BuildReviewIndicatorJson(m15, pipSize),
                    ["m5"] = BuildReviewIndicatorJson(m5, pipSize)
                };

                string h4Trend = ReviewTrend(h1.TakeLast(Math.Min(h1.Count, 120)).ToList(), aggregateSize: 4);
                string h1Trend = ReviewTrend(h1);
                string m15Trend = ReviewTrend(m15);
                string m5Trend = ReviewTrend(m5);
                double swingHigh = h1.Count > 0 ? h1.TakeLast(Math.Min(20, h1.Count)).Max(c => c.High) : 0;
                double swingLow = h1.Count > 0 ? h1.TakeLast(Math.Min(20, h1.Count)).Min(c => c.Low) : 0;
                snapshot["structure"] = new JObject
                {
                    ["trend_h4"] = h4Trend,
                    ["trend_h1"] = h1Trend,
                    ["trend_m15"] = m15Trend,
                    ["trend_m5"] = m5Trend,
                    ["all_timeframes_aligned"] = h4Trend == h1Trend && h1Trend == m15Trend && m15Trend == m5Trend,
                    ["market_regime"] = h1Indicators.Value<double?>("adx") >= 25 ? "TRENDING" : "RANGING",
                    ["swing_high"] = swingHigh,
                    ["swing_low"] = swingLow
                };

                snapshot["levels"] = BuildReviewLevelsJson(h1, bid, pipSize);
                Log("[BOT] Review fallback enriched from MT5 OHLC rates because GET_MARKET_SNAPSHOT was unavailable.", C_ACCENT);
            }
            catch (Exception ex)
            {
                Log($"[BOT] Review fallback enrichment failed: {ex.Message}", C_YELLOW);
            }
        }

        private async Task<List<BacktestOhlcCandle>> GetReviewRatesAsync(
            string pair,
            string timeframe,
            DateTime fromUtc,
            DateTime toUtc,
            int maxRows)
        {
            if (_bridge == null) return [];

            var result = await _bridge.TryGetHistoricalRatesAsync(pair, timeframe, fromUtc, toUtc, maxRows).ConfigureAwait(false);
            return result.Success
                ? result.Candles.OrderBy(c => c.TimestampUtc).ToList()
                : [];
        }

        private double ReviewPipSize(TradeRequest request, SymbolInfo? symbol)
        {
            var pairRules = _pairSettings?.GetForPair(request.Pair);
            if (pairRules?.PipSize > 0) return pairRules.PipSize;
            return LotCalculator.GetPipSize((symbol?.Symbol ?? request.Pair).ToUpperInvariant());
        }

        private static BacktestOhlcCandle? LastOrNull(IReadOnlyList<BacktestOhlcCandle> candles) =>
            candles.Count > 0 ? candles[^1] : null;

        private static BacktestOhlcCandle? PreviousOrNull(IReadOnlyList<BacktestOhlcCandle> candles) =>
            candles.Count > 1 ? candles[^2] : null;

        private static BacktestOhlcCandle? AggregateReviewCandles(IReadOnlyList<BacktestOhlcCandle> candles, int count)
        {
            if (candles.Count < count) return LastOrNull(candles);

            var selected = candles.Skip(candles.Count - count).ToList();
            return new BacktestOhlcCandle
            {
                TimestampUtc = selected[0].TimestampUtc,
                Symbol = selected[0].Symbol,
                Timeframe = $"H{count}",
                Open = selected[0].Open,
                High = selected.Max(c => c.High),
                Low = selected.Min(c => c.Low),
                Close = selected[^1].Close,
                Volume = selected.Sum(c => c.Volume ?? 0)
            };
        }

        private static JObject BuildReviewCandleJson(
            BacktestOhlcCandle? candle,
            BacktestOhlcCandle? previous,
            double pipSize)
        {
            if (candle == null) return Unavailable("MT5 OHLC fallback did not return enough candle data.");

            double body = Math.Abs(candle.Close - candle.Open);
            double upper = candle.High - Math.Max(candle.Open, candle.Close);
            double lower = Math.Min(candle.Open, candle.Close) - candle.Low;
            bool inside = previous != null && candle.High < previous.High && candle.Low > previous.Low;
            bool engulfing = previous != null &&
                ((candle.Close > candle.Open && previous.Close < previous.Open && candle.Close >= previous.Open && candle.Open <= previous.Close) ||
                 (candle.Close < candle.Open && previous.Close > previous.Open && candle.Open >= previous.Close && candle.Close <= previous.Open));

            return new JObject
            {
                ["time"] = candle.TimestampUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                ["open"] = candle.Open,
                ["high"] = candle.High,
                ["low"] = candle.Low,
                ["close"] = candle.Close,
                ["volume"] = candle.Volume ?? 0,
                ["body_pips"] = pipSize > 0 ? Math.Round(body / pipSize, 1) : 0,
                ["direction"] = candle.Close > candle.Open ? "BULLISH" : candle.Close < candle.Open ? "BEARISH" : "DOJI",
                ["is_engulfing"] = engulfing,
                ["is_pin_bar"] = body > 0 && (upper >= body * 2.0 || lower >= body * 2.0),
                ["is_inside_bar"] = inside,
                ["is_doji"] = pipSize > 0 && body / pipSize <= 1.5
            };
        }

        private static JObject BuildReviewIndicatorJson(IReadOnlyList<BacktestOhlcCandle> candles, double pipSize)
        {
            if (candles.Count == 0) return Unavailable("MT5 OHLC fallback did not return enough indicator data.");

            var closes = candles.Select(c => c.Close).ToList();
            double close = closes[^1];
            double ema20 = Ema(closes, 20);
            double ema50 = Ema(closes, 50);
            double ema200 = Ema(closes, 200);
            double rsi = Rsi(closes, 14);
            double macd = Ema(closes, 12) - Ema(closes, 26);
            double macdSignal = Ema(MacdSeries(closes), 9);
            double atr = Atr(candles, 14);
            double adx = AdxApprox(candles, 14);

            return new JObject
            {
                ["rsi"] = Math.Round(rsi, 1),
                ["rsi_signal"] = rsi >= 70 ? "OVERBOUGHT" : rsi <= 30 ? "OVERSOLD" : "NEUTRAL",
                ["macd_value"] = macd,
                ["macd_signal_line"] = macdSignal,
                ["macd_histogram"] = macd - macdSignal,
                ["macd_bias"] = macd >= macdSignal ? "BULLISH" : "BEARISH",
                ["ema20"] = ema20,
                ["ema50"] = ema50,
                ["ema200"] = ema200,
                ["price_vs_ema20"] = close >= ema20 ? "ABOVE" : "BELOW",
                ["price_vs_ema50"] = close >= ema50 ? "ABOVE" : "BELOW",
                ["price_vs_ema200"] = close >= ema200 ? "ABOVE" : "BELOW",
                ["adx"] = Math.Round(adx, 1),
                ["adx_signal"] = adx >= 25 ? "STRONG_TREND" : adx >= 18 ? "WEAK_TREND" : "NO_TREND",
                ["atr"] = atr,
                ["atr_pips"] = pipSize > 0 ? Math.Round(atr / pipSize, 1) : 0
            };
        }

        private static JObject BuildReviewLevelsJson(IReadOnlyList<BacktestOhlcCandle> h1, double bid, double pipSize)
        {
            if (h1.Count == 0 || bid <= 0) return Unavailable("MT5 OHLC fallback did not return enough support/resistance data.");

            var recent = h1.TakeLast(Math.Min(h1.Count, 96)).ToList();
            var lows = recent.Select(c => c.Low).OrderBy(x => Math.Abs(bid - x)).ToList();
            var highs = recent.Select(c => c.High).OrderBy(x => Math.Abs(x - bid)).ToList();
            double support1 = lows.Where(x => x <= bid).DefaultIfEmpty(recent.Min(c => c.Low)).OrderByDescending(x => x).First();
            double support2 = lows.Where(x => x < support1).DefaultIfEmpty(support1).OrderByDescending(x => x).First();
            double resistance1 = highs.Where(x => x >= bid).DefaultIfEmpty(recent.Max(c => c.High)).OrderBy(x => x).First();
            double resistance2 = highs.Where(x => x > resistance1).DefaultIfEmpty(resistance1).OrderBy(x => x).First();
            double supportDistance = pipSize > 0 ? Math.Abs(bid - support1) / pipSize : 0;
            double resistanceDistance = pipSize > 0 ? Math.Abs(resistance1 - bid) / pipSize : 0;

            return new JObject
            {
                ["nearest_support_1"] = support1,
                ["nearest_support_2"] = support2,
                ["nearest_resistance_1"] = resistance1,
                ["nearest_resistance_2"] = resistance2,
                ["distance_to_support_pips"] = Math.Round(supportDistance, 1),
                ["distance_to_resistance_pips"] = Math.Round(resistanceDistance, 1),
                ["price_at_key_level"] = Math.Min(supportDistance, resistanceDistance) <= 5.0,
                ["key_level_type"] = supportDistance <= resistanceDistance ? "SUPPORT" : "RESISTANCE"
            };
        }

        private static string ReviewTrend(IReadOnlyList<BacktestOhlcCandle> candles, int aggregateSize = 1)
        {
            if (aggregateSize > 1)
            {
                var aggregated = new List<BacktestOhlcCandle>();
                for (int i = 0; i + aggregateSize <= candles.Count; i += aggregateSize)
                {
                    var chunk = candles.Skip(i).Take(aggregateSize).ToList();
                    aggregated.Add(new BacktestOhlcCandle
                    {
                        TimestampUtc = chunk[0].TimestampUtc,
                        Open = chunk[0].Open,
                        High = chunk.Max(c => c.High),
                        Low = chunk.Min(c => c.Low),
                        Close = chunk[^1].Close
                    });
                }
                candles = aggregated;
            }

            if (candles.Count == 0) return "UNKNOWN";

            var closes = candles.Select(c => c.Close).ToList();
            double close = closes[^1];
            double ema20 = Ema(closes, 20);
            double ema50 = Ema(closes, 50);
            if (close >= ema20 && ema20 >= ema50) return "BULLISH";
            if (close <= ema20 && ema20 <= ema50) return "BEARISH";
            return "RANGING";
        }

        private static double Ema(IReadOnlyList<double> values, int period)
        {
            if (values.Count == 0) return 0;
            double k = 2.0 / (period + 1);
            double ema = values[0];
            foreach (double value in values.Skip(1))
                ema = value * k + ema * (1 - k);
            return ema;
        }

        private static double Rsi(IReadOnlyList<double> closes, int period)
        {
            if (closes.Count <= 1) return 50;
            int start = Math.Max(1, closes.Count - period);
            double gains = 0;
            double losses = 0;
            for (int i = start; i < closes.Count; i++)
            {
                double change = closes[i] - closes[i - 1];
                if (change >= 0) gains += change;
                else losses -= change;
            }
            if (losses <= 0) return 100;
            double rs = gains / losses;
            return 100.0 - (100.0 / (1.0 + rs));
        }

        private static List<double> MacdSeries(IReadOnlyList<double> closes)
        {
            var series = new List<double>(closes.Count);
            for (int i = 1; i <= closes.Count; i++)
            {
                var slice = closes.Take(i).ToList();
                series.Add(Ema(slice, 12) - Ema(slice, 26));
            }
            return series;
        }

        private static double Atr(IReadOnlyList<BacktestOhlcCandle> candles, int period)
        {
            if (candles.Count == 0) return 0;
            int start = Math.Max(0, candles.Count - period);
            double total = 0;
            int count = 0;
            for (int i = start; i < candles.Count; i++)
            {
                double prevClose = i > 0 ? candles[i - 1].Close : candles[i].Close;
                total += Math.Max(candles[i].High - candles[i].Low, Math.Max(Math.Abs(candles[i].High - prevClose), Math.Abs(candles[i].Low - prevClose)));
                count++;
            }
            return count > 0 ? total / count : 0;
        }

        private static double AdxApprox(IReadOnlyList<BacktestOhlcCandle> candles, int period)
        {
            if (candles.Count < 2) return 0;
            int start = Math.Max(1, candles.Count - period);
            double directionalMove = 0;
            double trueRange = 0;
            for (int i = start; i < candles.Count; i++)
            {
                directionalMove += Math.Abs(candles[i].Close - candles[i - 1].Close);
                trueRange += candles[i].High - candles[i].Low;
            }
            return trueRange > 0 ? Math.Min(60, directionalMove / trueRange * 35.0) : 0;
        }

        private sealed record ReviewHistoryItem(
            string Pair,
            string Strategy,
            string Direction,
            string Result,
            double? Pips,
            double Pnl);

        private readonly record struct ReviewDailyStats(
            bool Known,
            double TodayPnl,
            int TradesToday,
            IReadOnlyList<ReviewHistoryItem> LastTrades);

        private ReviewDailyStats GetTodayReviewStats(string pair, string strategy, double fallbackFloatingPnl)
        {
            DateTime dayStartUtc = DateTime.UtcNow.Date;
            DateTime dayEndUtc = dayStartUtc.AddDays(1);

            try
            {
                if (_tradeDb == null)
                    return double.IsNaN(fallbackFloatingPnl)
                        ? UnknownReviewDailyStats()
                        : new ReviewDailyStats(true, fallbackFloatingPnl, 0, []);

                var openedToday = _tradeDb.GetByDateRangeAsync(dayStartUtc, dayEndUtc)
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();
                var closedToday = _tradeDb.GetClosedByCloseDateRangeAsync(dayStartUtc, dayEndUtc)
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();

                var openedForScope = openedToday
                    .Where(t =>
                        IsSameReviewPair(t.Pair, pair) &&
                        string.Equals(ResolveTradeRecordStrategy(t), strategy, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var closedForScope = closedToday
                    .Where(t =>
                        IsSameReviewPair(t.Pair, pair) &&
                        string.Equals(ResolveTradeRecordStrategy(t), strategy, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                int tradesToday = openedForScope.Count;
                double todayPnl = closedForScope.Sum(t => t.ProfitUsd);
                var lastTrades = closedForScope
                    .OrderByDescending(t => t.ClosedAt ?? t.ExecutedAt)
                    .Take(5)
                    .Select(BuildReviewHistoryItem)
                    .ToList();

                return new ReviewDailyStats(true, todayPnl, tradesToday, lastTrades);
            }
            catch
            {
                return double.IsNaN(fallbackFloatingPnl)
                    ? UnknownReviewDailyStats()
                    : new ReviewDailyStats(true, fallbackFloatingPnl, 0, []);
            }
        }

        private static ReviewDailyStats UnknownReviewDailyStats() => new(false, 0, 0, []);

        private static ReviewHistoryItem BuildReviewHistoryItem(TradeRecord record)
        {
            string strategy = ResolveTradeRecordStrategy(record);
            return new ReviewHistoryItem(
                record.Pair,
                strategy,
                record.Direction,
                record.ProfitUsd >= 0 ? "WIN" : "LOSS",
                EstimateReviewTradePips(record),
                record.ProfitUsd);
        }

        private static double? EstimateReviewTradePips(TradeRecord record)
        {
            double lots = record.ExecutedLots > 0 ? record.ExecutedLots : record.LotSize;
            double entry = record.ExecutedPrice > 0 ? record.ExecutedPrice : record.EntryPrice;
            if (lots <= 0 || entry <= 0 || string.IsNullOrWhiteSpace(record.Pair))
                return null;

            double pipValuePerLot = LotCalculator.GetPipValuePerLot(record.Pair, entry);
            if (pipValuePerLot <= 0 || double.IsNaN(pipValuePerLot) || double.IsInfinity(pipValuePerLot))
                return null;

            return record.ProfitUsd / (pipValuePerLot * lots);
        }

        private static bool IsSameReviewPair(string storedPair, string pair) =>
            storedPair.StartsWith(pair, StringComparison.OrdinalIgnoreCase) ||
            pair.StartsWith(storedPair, StringComparison.OrdinalIgnoreCase);

        private static string ResolveTradeRecordStrategy(TradeRecord record)
        {
            if (!string.IsNullOrWhiteSpace(record.Strategy))
                return record.Strategy;

            return record.Comment.Contains("Scalp", StringComparison.OrdinalIgnoreCase)
                || record.Comment.Contains("Scalping", StringComparison.OrdinalIgnoreCase)
                    ? "Scalping"
                    : "Normal";
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
            var pairRules = _pairSettings?.GetForPair(request.Pair);
            double pipSize = pairRules?.PipSize > 0
                ? pairRules.PipSize
                : LotCalculator.GetPipSize((symbol?.Symbol ?? request.Pair).ToUpperInvariant());
            var effective = EffectiveTradeSettings.Resolve(_cfg.Bot, request.Strategy, request.LotSize);
            double maxSpreadPips = effective.MaxSpreadPips;
            double marginRequired = account?.Leverage > 0 && symbol?.ContractSize > 0 && request.LotSize > 0 && entry > 0
                ? Math.Round(symbol.ContractSize.Value * request.LotSize * entry / account.Leverage, 2)
                : 0;
            double maxLossDollar = account != null
                ? Math.Round(account.Equity * _cfg.Bot.EmergencyCloseDrawdownPct / 100.0, 2) : 0;
            var dailyStats = GetTodayReviewStats(request.Pair, effective.Strategy, account?.Profit ?? double.NaN);
            double todayLossOnly = Math.Max(0, -dailyStats.TodayPnl);
            double dailyLossRemaining = Math.Round(Math.Max(0, maxLossDollar - todayLossOnly), 2);
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
                    ["daily_pnl"] = dailyStats.Known ? Math.Round(dailyStats.TodayPnl, 2) : (double?)null,
                    ["daily_trades_taken"] = dailyStats.Known ? dailyStats.TradesToday : (int?)null,
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
                    ["spread_normal"]    = maxSpreadPips <= 0 || symbol.SpreadPips <= maxSpreadPips,
                    ["daily_open"]       = null,
                    ["daily_high"]       = null,
                    ["daily_low"]        = null,
                    ["daily_range_pips"] = null,
                    ["prev_day_high"]    = null
                },
                ["candles"] = Unavailable("GET_MARKET_SNAPSHOT did not return candle data. Reload/re-attach the latest TradingBotEA in MT5 or wait for the context refresh."),
                ["indicators"] = Unavailable("GET_MARKET_SNAPSHOT did not return indicator data. Reload/re-attach the latest TradingBotEA in MT5 or wait for the context refresh."),
                ["structure"] = Unavailable("GET_MARKET_SNAPSHOT did not return market-structure data. Reload/re-attach the latest TradingBotEA in MT5 or wait for the context refresh."),
                ["levels"] = Unavailable("GET_MARKET_SNAPSHOT did not return support/resistance data. Reload/re-attach the latest TradingBotEA in MT5 or wait for the context refresh."),
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
                    ["required_rr_ratio"]     = effective.RiskRewardRatio,
                    ["suggested_sl"]          = 0,
                    ["suggested_tp1"]         = 0,
                    ["suggested_tp2"]         = 0,
                    ["sl_distance_pips"]      = Math.Round(effective.SlPips, 1),
                    ["tp1_distance_pips"]     = Math.Round(effective.TpPips, 1),
                    ["tp2_distance_pips"]     = 0,
                    ["rr_ratio"]              = Math.Round(effective.SlPips > 0 ? effective.TpPips / effective.SlPips : 0, 2),
                    ["calculated_lot"]        = request.LotSize,
                    ["dollar_risk"]           = 0,
                    ["dollar_profit_tp1"]     = 0,
                    ["dollar_profit_tp2"]     = 0,
                    ["margin_required"]       = marginRequired,
                    ["daily_loss_remaining"]  = dailyLossRemaining,
                    ["daily_loss_limit_dollar"] = maxLossDollar
                },
                ["news"] = Unavailable("News module is offline in this review window")
            };

            if (pairRules != null)
            {
                snapshot["pair_rules"] = JObject.FromObject(new
                {
                    pair = pairRules.Pair,
                    pip_size = pairRules.PipSize,
                    min_atr_pips_m5 = pairRules.MinAtrPipsM5,
                    max_atr_pips_m5 = pairRules.MaxAtrPipsM5,
                    min_atr_pips_m15 = pairRules.MinAtrPipsM15,
                    max_atr_pips_m15 = pairRules.MaxAtrPipsM15,
                    minimum_distance_from_key_level_pips = pairRules.MinimumDistanceFromKeyLevelPips,
                    trailing_start_pips = pairRules.TrailingStartPips,
                    trailing_step_pips = pairRules.TrailingStepPips,
                    max_slippage_pips = pairRules.MaxSlippagePips,
                    recommended_sessions = pairRules.RecommendedSessions,
                    avoid_sessions = pairRules.AvoidSessions
                });
            }

            ApplyReviewDailySummary(snapshot, dailyStats, request.Pair.ToUpperInvariant(), effective.Strategy);
            ApplyReviewDailyLossRemaining(snapshot, maxLossDollar, dailyStats);
            ApplyReviewSpreadSummary(snapshot, symbol?.SpreadPips ?? double.NaN, maxSpreadPips,
                maxSpreadPips <= 0 || (symbol != null && symbol.SpreadPips <= maxSpreadPips));
            var metrics = BuildReviewRiskMetrics(snapshot, request, effective, request.LotSize);
            PatchReviewRiskSnapshot(snapshot, request, effective, metrics);
            ApplyReviewDataSourceSummary(snapshot);

            return snapshot.ToString(Formatting.Indented);
        }

        private void PatchSnapshotSignalFields(JObject snapshot, TradeRequest req)
        {
            var effective = EffectiveTradeSettings.Resolve(_cfg.Bot, req.Strategy, req.LotSize, req.MaxSpreadPips);
            var metrics = BuildReviewRiskMetrics(snapshot, req, effective, req.LotSize);
            PatchReviewRiskSnapshot(snapshot, req, effective, metrics);
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
                                case "json":
                                    btn.Enabled = true;
                                    break;
                                case "delete":
                                    btn.Enabled = info.Status != SignalCardStatus.Executing;
                                    break;
                                case "close":
                                    btn.Enabled = info.Status == SignalCardStatus.Executed && info.Ticket > 0;
                                    break;
                                case "execute":
                                    btn.Enabled = CanExecuteSignal(info);
                                    break;
                            }
                    else if (card.Tag is PairAnalysisInfo)
                        foreach (var btn in card.Controls.OfType<Button>())
                            btn.Enabled = true;
                }
            }
            if (card.InvokeRequired) card.Invoke(Apply); else Apply();
        }

        private async Task<bool> StartAutoScalpingFromReviewAsync(TradeRequest req, TradeReviewDecision review)
        {
            if (!review.AutoScalpingEnabled || review.ScalpingConfig == null)
                return false;

            if (_bridge?.IsConnected != true)
            {
                Log("[SCALP] Cannot start: MT5 is not connected.", C_RED);
                return true;
            }

            bool approved = _cfg.Bot.PaperTrading || (InvokeRequired
                ? (bool)Invoke(() => Confirm(
                    "Start LIVE auto scalping for this pair?\n\nThe bot will place multiple trades inside the configured session limits and existing risk controls."))
                : Confirm("Start LIVE auto scalping for this pair?\n\nThe bot will place multiple trades inside the configured session limits and existing risk controls."));
            if (!approved)
            {
                _scalpingTradeManager.Stop();
                Log("[SCALP] User cancelled auto scalping start.", C_YELLOW);
                return true;
            }

            await SaveScalpingConfigForPairAsync(req.Pair, review.ScalpingConfig).ConfigureAwait(false);
            _cfg.Bot.Scalping = CloneScalpingSettings(review.ScalpingConfig);
            _bot ??= CreateBot();
            _bot.UpdateConfig(_cfg.Bot);
            _bot.UpdateApiConfig(_cfg.ApiIntegrations);

            if (_scalping?.IsRunning == true)
            {
                await _scalping.StopAsync().ConfigureAwait(false);
                _scalpingTradeManager.Stop();
            }

            _scalping = new ScalpingSessionService(_bridge, _newsCalendar, _cfg.ApiIntegrations);
            _scalping.OnLog += msg => Log(msg, msg.Contains("Blocked", StringComparison.OrdinalIgnoreCase) ? C_YELLOW : C_ACCENT);
            _scalping.OnStatusChanged += status => UIThread(() =>
            {
                bool running = string.Equals(status, "Running", StringComparison.OrdinalIgnoreCase);
                _btnStopScalping.Enabled = running;
                if (!running)
                    _scalpingTradeManager.Stop();
                SetBotBadge($"SCALPING {status.ToUpperInvariant()}", running ? Color.Gold : C_ACCENT);
            });

            var startReq = new ScalpingSessionRequest(
                req.Pair,
                req.TradeType,
                review.LotSize > 0 ? review.LotSize : req.LotSize,
                _cfg.Bot.MagicNumber,
                review.ScalpingConfig,
                ExecuteScalpingTradeFromReviewAsync,
                async fromUtc =>
                {
                    if (_tradeDb == null) return 0;
                    var closed = await _tradeDb.GetByDateRangeAsync(fromUtc, DateTime.UtcNow).ConfigureAwait(false);
                    return closed
                        .Where(t => t.Comment.Contains("AutoScalp", StringComparison.OrdinalIgnoreCase))
                        .Sum(t => t.ProfitUsd);
                },
                review.ScalpingConfig.UseAiConfirmation
                    ? async (snapshot, direction) =>
                    {
                        string prompt =
                            "You are confirming an already rule-filtered auto-scalping opportunity. " +
                            "Return JSON only: {\"approved\":true|false,\"reason\":\"short reason\"}.\n\n" +
                            $"Direction: {direction}\nSnapshot:\n{snapshot.ToString(Formatting.None)}";
                        var (json, allowed, decision, error) = await RunAiTradeDecisionAsync(prompt).ConfigureAwait(false);
                        if (!string.IsNullOrWhiteSpace(error))
                            return new ScalpingAiConfirmation(false, error);
                        var jo = JObject.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
                        bool approved = jo.Value<bool?>("approved") == true ||
                                        allowed ||
                                        string.Equals(decision, direction.ToString(), StringComparison.OrdinalIgnoreCase);
                        string reason = jo.Value<string>("reason") ?? decision;
                        return new ScalpingAiConfirmation(approved, reason);
                    }
                    : null);

            await _scalping.StartAsync(startReq).ConfigureAwait(false);
            _scalpingTradeManager.Start(_cfg.Bot.Scalping);
            await _settings.SaveAsync(_cfg).ConfigureAwait(false);
            Log($"[SCALP] Auto scalping session armed for {req.Pair}.", C_GREEN);
            return true;
        }

        private async Task<TradeResult> ExecuteScalpingTradeFromReviewAsync(TradeRequest req)
        {
            _bot ??= CreateBot();
            TradeResult result = await _bot.ExecuteTradeWithValidationAsync(req).ConfigureAwait(false);
            CaptureExecutionRuleAudit(req, result);

            AddHistoryRow(req, result);

            if (result.IsSuccess)
            {
                await Task.Delay(500).ConfigureAwait(false);
                await RefreshPositionsAsync().ConfigureAwait(false);

                if (_bridge?.IsConnected == true)
                {
                    var positions = await _bridge.GetPositionsAsync().ConfigureAwait(false);
                    bool found = result.Ticket > 0 && positions.Any(p => p.Ticket == result.Ticket);
                    Log(found
                            ? $"[SCALP] Confirmed open position #{result.Ticket} in MT5 positions."
                            : $"[SCALP] MT5 accepted ticket #{result.Ticket}, but it was not found in the refreshed open-position list. It may have closed immediately, or MT5 returned an order/deal ticket instead of the live position ticket.",
                        found ? C_GREEN : C_YELLOW);
                }
            }
            else
            {
                await RefreshPositionsAsync().ConfigureAwait(false);
            }

            return result;
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
                    Log("[BOT] Auto watcher is not ready yet. Connect MT5 and confirm the watch folder path.", C_YELLOW);
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

                _cfg.Bot = ReadBotConfigFromUISafe();
                _cfg.ApiIntegrations = ReadApiIntegrationConfigFromUI();
                _bot.UpdateConfig(_cfg.Bot);
                _bot.UpdateApiConfig(_cfg.ApiIntegrations);
                await _settings.SaveAsync(_cfg).ConfigureAwait(false);

                var review = await ShowTradeReviewDialogAsync(req, info).ConfigureAwait(false);
                if (!review.Approved)
                {
                    Log($"[BOT] Trade review cancelled for {info.FileName}.", C_YELLOW);
                    return;
                }

                if (review.LotSize > 0)
                    req.LotSize = review.LotSize;
                if (review.FinalRequest != null)
                    req = review.FinalRequest;

                if (await StartAutoScalpingFromReviewAsync(req, review).ConfigureAwait(false))
                {
                    UpdateCardStatusSafe(card, info with
                    {
                        Status = SignalCardStatus.Executing,
                        StatusText = "Auto scalping",
                        Time = DateTime.Now
                    });
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
                CaptureExecutionRuleAudit(req, result);

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
                _normalTradeManager.Stop();

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
                _ = PersistLifecycleAuditAsync(new TradeLifecycleAuditRecord
                {
                    CreatedAtUtc = DateTime.UtcNow,
                    EventType = ok ? "CLOSE_REQUESTED" : "CLOSE_FAILED",
                    Ticket = info.Ticket,
                    PositionId = info.Ticket,
                    Pair = info.Pair,
                    Direction = info.TradeType.ToString(),
                    Actor = "User",
                    Reason = ok ? "User closed position from signal card." : "User signal-card close failed.",
                    DetailsJson = JsonConvert.SerializeObject(new { source = "SignalCard.CloseButton", info.FileName, info.SignalId }, Formatting.None)
                });
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
                SignalCardStatus.Pending   => ("Pending",                            Color.FromArgb(250, 199, 117)),
                SignalCardStatus.Executing => ("Executing...",                       Color.FromArgb(99,  179, 237)),
                SignalCardStatus.Executed  => ($"  {info.StatusText}",                 Color.FromArgb(72,  199, 142)),
                SignalCardStatus.Rejected  => ($"[X]  {Truncate(info.StatusText, 40)}",   Color.FromArgb(252, 95,  95)),
                SignalCardStatus.Error     => ($"[!]   {Truncate(info.StatusText, 40)}",   Color.FromArgb(250, 150, 50)),
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

        private void BtnClearLog_Click(object? sender, EventArgs e)
        {
            _txtLog.Clear();
            _screenLogFullMessages.Clear();
        }

        private void BtnLogDetails_Click(object? sender, EventArgs e) => ShowSelectedLogDetail();

        private void TxtLog_DoubleClick(object? sender, EventArgs e) => ShowSelectedLogDetail();

        private void ConfigureRulesMonitorContextMenus()
        {
            var logMenu = new ContextMenuStrip();
            logMenu.Items.Add("Open Log Details", null, (_, _) => ShowSelectedLogDetail());
            logMenu.Items.Add("Open Rules Monitor", null, (_, _) =>
            {
                string line = GetFullLogLineForDetails(GetSelectedLogLineIndex());
                if (string.IsNullOrWhiteSpace(line))
                    line = GetSelectedLogLine();

                if (!IsRulesMonitorEligibleLog(line))
                {
                    AppMessageBox.Info(this, "Rules Monitor opens for trade-decision log rows only.");
                    return;
                }

                OpenRulesMonitor(BuildLogRulesContext(line));
            });
            logMenu.Items.Add("Copy Log", null, (_, _) =>
            {
                string line = GetFullLogLineForDetails(GetSelectedLogLineIndex());
                if (!string.IsNullOrWhiteSpace(line))
                    Clipboard.SetText(line);
            });
            logMenu.Items.Add("Copy Decision Audit", null, (_, _) =>
            {
                string line = GetFullLogLineForDetails(GetSelectedLogLineIndex());
                if (IsRulesMonitorEligibleLog(line))
                    Clipboard.SetText(line);
            });
            _txtLog.ContextMenuStrip = logMenu;

            var positionMenu = new ContextMenuStrip();
            positionMenu.Items.Add("Open Rules Monitor", null, (_, _) => OpenRulesMonitor(BuildPositionRulesContext()));
            _gridPos.ContextMenuStrip = positionMenu;
        }

        private void OpenRulesMonitor(TradeRulesContext context)
        {
            var snapshotService = new TradeRulesRuntimeSnapshotService(
                _cfg,
                new TradeRuleCatalog(),
                _bridge,
                _pairSettings,
                _scalping,
                _normalTradeManager,
                _newsCalendar,
                _cfg.ApiIntegrations);
            var controlService = new TradeRulesRuntimeControlService(_cfg, _settings);

            Log($"[RULES_MONITOR] Opened | Source={context.OpenedFrom} | Pair={context.Pair} | Strategy={context.Strategy} | Ticket={context.Ticket?.ToString() ?? "-"}", C_ACCENT);
            using var form = new LiveTradeRulesMonitorControlForm(context, snapshotService, controlService, message => Log(message, C_ACCENT));
            form.ShowDialog(this);
        }

        private TradeRulesContext BuildPanelRulesContext(TradeRulesStrategy strategy, string openedFrom)
        {
            string pair = strategy == TradeRulesStrategy.Scalping
                ? (_cmbAllowedPair.Text.DefaultIfBlank(_cmbPair.Text))
                : _cmbPair.Text;

            return new TradeRulesContext
            {
                Pair = pair.Trim().ToUpperInvariant(),
                Strategy = strategy,
                IsRunningTrade = strategy == TradeRulesStrategy.Scalping
                    ? _scalping?.IsRunning == true
                    : _normalTradeManager.IsRunning,
                OpenedFrom = openedFrom
            };
        }

        private TradeRulesContext BuildPositionRulesContext()
        {
            if (_gridPos.SelectedRows.Count == 0)
                return new TradeRulesContext { OpenedFrom = "RunningTrade", IsRunningTrade = true };

            var row = _gridPos.SelectedRows[0];
            _ = long.TryParse(row.Cells[0].Value?.ToString(), out long ticket);
            string pair = row.Cells[1].Value?.ToString() ?? "";
            string typeText = row.Cells[2].Value?.ToString() ?? "";
            string comment = row.Cells[11].Value?.ToString() ?? "";

            return new TradeRulesContext
            {
                Pair = pair,
                Strategy = ResolveStrategy(comment),
                Ticket = ticket > 0 ? ticket : null,
                TradeType = Enum.TryParse<TradeType>(typeText, true, out var type) ? type : null,
                IsRunningTrade = true,
                OpenedFrom = "RunningTrade"
            };
        }

        private TradeRulesContext BuildSignalRulesContext(SignalCardInfo info) => new()
        {
            Pair = info.Pair,
            Strategy = ResolveStrategy(info.StatusText),
            Ticket = info.Ticket > 0 ? info.Ticket : null,
            TradeType = Enum.TryParse<TradeType>(info.TradeType, true, out var type) ? type : null,
            RequestId = info.SignalId,
            IsRunningTrade = info.Ticket > 0,
            OpenedFrom = "SignalCard"
        };

        private TradeRulesContext BuildPairAnalysisRulesContext(PairAnalysisInfo info) => new()
        {
            Pair = info.Pair,
            Strategy = string.Equals(info.Status, "Scalping", StringComparison.OrdinalIgnoreCase)
                ? TradeRulesStrategy.Scalping
                : TradeRulesStrategy.Unknown,
            TradeType = Enum.TryParse<TradeType>(info.Direction, true, out var type) ? type : null,
            OpenedFrom = "SignalCard"
        };

        private TradeRulesContext BuildLogRulesContext(string line) => new()
        {
            Pair = ExtractKnownPair(line),
            Strategy = ResolveStrategy(line),
            Ticket = ExtractLongAfter(line, "Ticket #") ?? ExtractLongAfter(line, "#"),
            RequestId = ExtractTokenAfter(line, "RequestId="),
            TradeType = line.Contains(" BUY ", StringComparison.OrdinalIgnoreCase) || line.Contains(" BUY", StringComparison.OrdinalIgnoreCase)
                ? TradeType.BUY
                : line.Contains(" SELL ", StringComparison.OrdinalIgnoreCase) || line.Contains(" SELL", StringComparison.OrdinalIgnoreCase)
                    ? TradeType.SELL
                    : null,
            IsRunningTrade = false,
            OpenedFrom = "LogScreen",
            RawLogLine = line,
            OpenedLogTimestamp = ExtractLogTimestamp(line)
        };

        private static string? ExtractLogTimestamp(string line)
        {
            string text = line.TrimStart();
            if (text.Length < 10 || text[0] != '[')
                return null;

            int end = text.IndexOf(']');
            return end > 1 ? text[1..end] : null;
        }

        private static bool IsRulesMonitorEligibleLog(string line) =>
            !string.IsNullOrWhiteSpace(line) &&
            (line.Contains("[SCALP]", StringComparison.OrdinalIgnoreCase) ||
             line.Contains("[SCALP_DECISION]", StringComparison.OrdinalIgnoreCase) ||
             line.Contains("[TRADE_AUDIT_FULL]", StringComparison.OrdinalIgnoreCase) ||
             line.Contains("TRADE_AUDIT_FULL", StringComparison.OrdinalIgnoreCase) ||
             line.Contains("[EXEC_AUDIT]", StringComparison.OrdinalIgnoreCase) ||
             line.Contains("[BOT] Trade", StringComparison.OrdinalIgnoreCase) ||
             line.Contains("[BOT] Rejected", StringComparison.OrdinalIgnoreCase) ||
             line.Contains("[AI] Signal", StringComparison.OrdinalIgnoreCase));

        private static TradeRulesStrategy ResolveStrategy(string text)
        {
            if (text.Contains("Scalping", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("[SCALP]", StringComparison.OrdinalIgnoreCase))
                return TradeRulesStrategy.Scalping;

            if (text.Contains("Normal", StringComparison.OrdinalIgnoreCase))
                return TradeRulesStrategy.Normal;

            return TradeRulesStrategy.Unknown;
        }

        private string ExtractKnownPair(string text)
        {
            var candidates = _cfg.Bot.AllowedPairs
                .Concat(_cfg.PairSettings.Keys)
                .Concat(_cfg.Bot.ScalpingByPair.Keys)
                .Concat(_cfg.Bot.NormalTradingByPair.Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase);

            return candidates.FirstOrDefault(pair => text.Contains(pair, StringComparison.OrdinalIgnoreCase)) ?? "";
        }

        private static long? ExtractLongAfter(string text, string marker)
        {
            int index = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0) return null;

            string tail = text[(index + marker.Length)..];
            string digits = new(tail.SkipWhile(c => !char.IsDigit(c)).TakeWhile(char.IsDigit).ToArray());
            return long.TryParse(digits, out long value) ? value : null;
        }

        private static string? ExtractTokenAfter(string text, string marker)
        {
            int index = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0) return null;

            string tail = text[(index + marker.Length)..].Trim();
            string token = new(tail.TakeWhile(c => !char.IsWhiteSpace(c) && c != '|').ToArray());
            return string.IsNullOrWhiteSpace(token) ? null : token;
        }

        private void ShowSelectedLogDetail()
        {
            int lineIndex = GetSelectedLogLineIndex();
            string line = GetFullLogLineForDetails(lineIndex);
            if (string.IsNullOrWhiteSpace(line))
            {
                AppMessageBox.Info(this, "Select a log line first, then click Details.");
                return;
            }

            AppLogDetailBox.Show(this, LogLineExplainer.Explain(line));
        }

        private string GetFullLogLineForDetails(int lineIndex)
        {
            lineIndex = ResolveNearestLogLineIndex(lineIndex);
            if (lineIndex >= 0 && lineIndex < _screenLogFullMessages.Count)
                return _screenLogFullMessages[lineIndex];

            return GetSelectedLogLine();
        }

        private int GetSelectedLogLineIndex()
        {
            if (_txtLog.TextLength == 0) return -1;

            int caret = Math.Clamp(_txtLog.SelectionStart, 0, Math.Max(0, _txtLog.TextLength - 1));
            int line = _txtLog.GetLineFromCharIndex(caret);
            return ResolveNearestLogLineIndex(line);
        }

        private string GetSelectedLogLine()
        {
            if (_txtLog.TextLength == 0) return "";

            string selected = _txtLog.SelectedText;
            if (!string.IsNullOrWhiteSpace(selected))
            {
                string selectedLine = selected
                    .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault(static x => !string.IsNullOrWhiteSpace(x))
                    ?.Trim() ?? "";
                if (selectedLine.Length > 0)
                    return selectedLine;
            }

            string[] lines = _txtLog.Lines;
            if (lines.Length == 0)
                return "";

            int caret = Math.Clamp(_txtLog.SelectionStart, 0, Math.Max(0, _txtLog.TextLength - 1));
            int lineIndex = ResolveNearestLogLineIndex(_txtLog.GetLineFromCharIndex(caret));
            if (lineIndex < 0 || lineIndex >= lines.Length)
                return "";

            return lines[lineIndex].Trim();
        }

        private int ResolveNearestLogLineIndex(int lineIndex)
        {
            string[] lines = _txtLog.Lines;
            if (lines.Length == 0)
                return -1;

            int start = Math.Clamp(lineIndex >= 0 ? lineIndex : _lastLogContextLineIndex, 0, lines.Length - 1);
            if (!string.IsNullOrWhiteSpace(lines[start]))
                return start;

            for (int i = start - 1; i >= 0; i--)
            {
                if (!string.IsNullOrWhiteSpace(lines[i]))
                    return i;
            }

            for (int i = start + 1; i < lines.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(lines[i]))
                    return i;
            }

            return -1;
        }

        private void BtnOpenLogFile_Click(object? sender, EventArgs e)
        {
            try
            {
                string path = AppLogFiles.CurrentLogFile;
                if (string.IsNullOrWhiteSpace(path))
                    throw new InvalidOperationException("Log file is not ready yet.");

                Serilog.Log.Information("Opening log file: {Path}", path);
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Log($"[LOG] Could not open log file: {ex.Message}", C_RED);
            }
        }

        private void BtnOpenTradeLogFile_Click(object? sender, EventArgs e)
        {
            try
            {
                string path = AppLogFiles.CurrentTradeLogFile;
                if (string.IsNullOrWhiteSpace(path))
                    throw new InvalidOperationException("Trade log file is not ready yet.");

                AppLogFiles.WriteTrade("Trade log opened by user.");
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Log($"[LOG] Could not open trade log file: {ex.Message}", C_RED);
            }
        }

        private void BtnSaveLog_Click(object? sender, EventArgs e)
        {
            using var d = new SaveFileDialog { Filter = "Text|*.txt", FileName = "MT5Log" };
            if (d.ShowDialog() == DialogResult.OK) File.WriteAllText(d.FileName, _txtLog.Text);
        }

        private void BtnDeleteLogs_Click(object? sender, EventArgs e)
        {
            if (!Confirm("Delete all log files and clear the on-screen log?")) return;

            try
            {
                AppLogFiles.Close();
                if (Directory.Exists(AppLogFiles.LogDirectory))
                {
                    foreach (string path in Directory.EnumerateFiles(AppLogFiles.LogDirectory, "*.log"))
                        File.Delete(path);
                }

                AppLogFiles.RecreateCurrentFile();
                _txtLog.Clear();
                _screenLogFullMessages.Clear();
                Log("[LOG] All log files deleted. New session log started.", C_YELLOW);
            }
            catch (Exception ex)
            {
                AppLogFiles.RecreateCurrentFile();
                Log($"[LOG] Delete logs failed: {ex.Message}", C_RED);
            }
        }

        // ==========================================================
        //  PAIR SELECTION - shared flow for manual & AI selection
        // ==========================================================

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
                Status      = "Selected",
                LastUpdated = DateTime.Now,
                ShortReason = "Manual pair selected"
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

            // Left purple stripe - distinguishes pair analysis rows from file-based signal cards
            card.Controls.Add(new Panel { Width = 5, Dock = DockStyle.Left, BackColor = Color.FromArgb(130, 100, 255) });

            card.Controls.Add(new ProgressBar
            {
                Style                 = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 30,
                Height                = 3,
                Location              = new Point(5, 0),
                Width                 = w - 5,
                Anchor                = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Visible               = false,
                Tag                   = "spinner"
            });

            card.Controls.Add(new Label
            {
                Name      = "lblPairHeader",
                Text      = $"  {info.Pair}",
                Font      = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(180, 160, 255),
                Location  = new Point(14, 8),
                AutoSize  = true
            });

            var btnRemove = MakeCardButton("X", Color.FromArgb(80, 30, 30), Color.FromArgb(252, 95, 95), "Remove this row");
            btnRemove.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
            btnRemove.Location = new Point(w - 28, 8);
            btnRemove.Click   += (_, _) =>
            {
                _pairAnalysisCards.Remove(info.Pair);
                _flpSignals.Controls.Remove(card);
            };
            card.Controls.Add(btnRemove);

            var btnDetail = MakeCardButton("Detail", Color.FromArgb(20, 50, 30), Color.FromArgb(72, 199, 142),
                "Review - open trade details after analysis creates BUY/SELL levels");
            btnDetail.Size     = new Size(52, 22);
            btnDetail.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
            btnDetail.Location = new Point(w - 84, 8);
            btnDetail.Tag      = "detail";
            btnDetail.Click   += (_, _) => _ = OpenPairAnalysisDetailAsync(card);
            card.Controls.Add(btnDetail);

            var btnJson = MakeCardButton("JSON", Color.FromArgb(20, 30, 55), Color.FromArgb(130, 170, 255),
                "Open JSON - view the generated signal data for this row");
            btnJson.Size     = new Size(38, 22);
            btnJson.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
            btnJson.Location = new Point(w - 130, 8);
            btnJson.Tag      = "json";
            btnJson.Click   += (_, _) => ShowPairAnalysisJson(card);
            card.Controls.Add(btnJson);

            var btnRules = MakeCardButton("Rules", Color.FromArgb(45, 45, 70), Color.FromArgb(210, 220, 255),
                "Open Rules Monitor - inspect rules for this pair analysis row");
            btnRules.Size     = new Size(44, 22);
            btnRules.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
            btnRules.Location = new Point(w - 180, 8);
            btnRules.Tag      = "rules";
            btnRules.Click   += (_, _) =>
            {
                if (card.Tag is PairAnalysisInfo pa)
                    OpenRulesMonitor(BuildPairAnalysisRulesContext(pa));
            };
            card.Controls.Add(btnRules);

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
                        string icon = info.Direction switch { "BUY" => "BUY", "SELL" => "SELL", _ => "" };
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
            "Executing"            => Color.FromArgb(99,  179, 237),
            "Scalping"             => Color.Gold,
            "Executed"             => Color.FromArgb(72,  199, 142),
            "Rejected"             => Color.FromArgb(252, 95,  95),
            "BUY"                  => Color.FromArgb(72,  199, 142),
            "SELL"                 => Color.FromArgb(214, 164, 255),
            "WAIT"                 => Color.FromArgb(200, 180,  80),
            "No Trade"             => Color.FromArgb(160, 100, 100),
            "Analysis Error" or "No suitable pair found" => Color.FromArgb(220, 80, 80),
            _                      => Color.FromArgb(150, 155, 185)
        };

        private void ShowPairAnalysisJson(Panel card)
        {
            if (card.Tag is not PairAnalysisInfo info) return;

            var payload = new
            {
                pair = info.Pair,
                trade_type = info.Direction,
                order_type = OrderType.MARKET.ToString(),
                entry_price = info.Entry,
                stop_loss = info.StopLoss,
                take_profit = info.TakeProfit,
                lot_size = 0.01,
                comment = "PairAnalysis",
                magic_number = _cfg.Bot.MagicNumber,
                status = info.Status,
                confidence = info.Confidence,
                reason = info.ShortReason,
                rr = info.RR,
                generated_at = info.LastUpdated.ToString("O")
            };

            string json = JsonConvert.SerializeObject(payload, Formatting.Indented);
            using var dlg = new JsonViewForm($"{info.Pair}-pair-analysis.json", json);
            dlg.ShowDialog(this);
        }

        private async Task OpenPairAnalysisDetailAsync(Panel card)
        {
            if (card.Tag is not PairAnalysisInfo info) return;

            SetCardBusy(card, true);
            try
            {
                Log($"[BOT] Opening detail review for {info.Pair}...", C_ACCENT);

                if (_bridge?.IsConnected != true)
                {
                    Log("[WARN] Connect to MT5 before opening trade details.", C_YELLOW);
                    return;
                }

                bool rowHasTrade = Enum.TryParse<TradeType>(info.Direction, true, out var tradeType) &&
                    info.StopLoss > 0 &&
                    info.TakeProfit > 0;

                if (!rowHasTrade)
                {
                    tradeType = string.Equals(_cmbDir.SelectedItem?.ToString(), "SELL", StringComparison.OrdinalIgnoreCase)
                        ? TradeType.SELL
                        : TradeType.BUY;
                }

                double entry = rowHasTrade ? info.Entry : 0;
                double sl = rowHasTrade ? info.StopLoss : 0;
                double tp = rowHasTrade ? info.TakeProfit : 0;
                double tp2 = 0;

                if (!rowHasTrade)
                {
                    double.TryParse(_txtEntry.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out entry);
                    double.TryParse(_txtSL.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out sl);
                    double.TryParse(_txtTP.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out tp);
                    double.TryParse(_txtTP2.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out tp2);
                }

                if (sl <= 0 || tp <= 0)
                    Log($"[BOT] Opening detail for {info.Pair} without final SL/TP. Enter or adjust levels in the review window before approving.", C_YELLOW);

                double lot = 0.01;
                if (double.TryParse(_txtLot.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var uiLot) &&
                    uiLot >= 0.01)
                {
                    lot = uiLot;
                }

                var req = new TradeRequest
                {
                    Pair       = info.Pair,
                    TradeType  = tradeType,
                    OrderType  = _cmbOrderType.SelectedIndex switch
                    { 1 => OrderType.LIMIT, 2 => OrderType.STOP, _ => OrderType.MARKET },
                    EntryPrice = entry,
                    StopLoss   = sl,
                    TakeProfit = tp,
                    TakeProfit2 = tp2,
                    LotSize    = lot,
                    Comment    = rowHasTrade ? "PairAnalysis" : "ManualPairReview",
                    MagicNumber = _cfg.Bot.MagicNumber,
                    CreatedAt  = DateTime.UtcNow
                };

                var signalInfo = new SignalCardInfo
                {
                    SignalId   = req.Id,
                    FileName   = $"{info.Pair}-pair-analysis",
                    Pair       = info.Pair,
                    TradeType  = tradeType.ToString(),
                    StopLoss   = sl,
                    TakeProfit = tp,
                    LotSize    = lot,
                    Status     = SignalCardStatus.Pending,
                    StatusText = "Pair analysis",
                    CreatedAt  = DateTime.UtcNow,
                    Time       = DateTime.Now
                };

                _cfg.Bot = ReadBotConfigFromUISafe();
                _cfg.ApiIntegrations = ReadApiIntegrationConfigFromUI();
                _bot?.UpdateConfig(_cfg.Bot);
                _bot?.UpdateApiConfig(_cfg.ApiIntegrations);
                await _settings.SaveAsync(_cfg).ConfigureAwait(false);

                var review = await ShowTradeReviewDialogAsync(req, signalInfo).ConfigureAwait(false);
                if (!review.Approved)
                {
                    Log($"[BOT] Trade review cancelled for {info.Pair}.", C_YELLOW);
                    return;
                }

                if (review.LotSize > 0)
                    req.LotSize = review.LotSize;
                if (review.FinalRequest != null)
                    req = review.FinalRequest;

                if (await StartAutoScalpingFromReviewAsync(req, review).ConfigureAwait(false))
                {
                    info.Status = "Scalping";
                    info.ShortReason = "Auto scalping running";
                    info.LastUpdated = DateTime.Now;
                    UpdatePairAnalysisCard(card, info);
                    return;
                }

                info.Status = "Executing";
                info.ShortReason = "Sending to MT5";
                info.LastUpdated = DateTime.Now;
                UpdatePairAnalysisCard(card, info);

                _bot ??= CreateBot();
                _bot.UpdateConfig(_cfg.Bot);
                _bot.UpdateApiConfig(_cfg.ApiIntegrations);

                var result = await _bot.ExecuteTradeWithValidationAsync(req).ConfigureAwait(false);
                CaptureExecutionRuleAudit(req, result);
                _normalTradeManager.Stop();

                info.Status = result.IsSuccess ? "Executed" : "Rejected";
                info.ShortReason = result.IsSuccess ? $"Ticket #{result.Ticket}" : result.ErrorMessage;
                info.LastUpdated = DateTime.Now;
                UpdatePairAnalysisCard(card, info);

                Log(result.IsSuccess
                        ? $"[OK] Trade placed from pair row: {req.Pair} ticket #{result.Ticket}"
                        : $"[ERROR] Pair row execute failed: {result.ErrorMessage}",
                    result.IsSuccess ? C_GREEN : C_RED);

                if (result.IsSuccess)
                    await RefreshPositionsAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log($"[ERROR] Detail button failed for {info.Pair}: {ex.Message}", C_RED);
            }
            finally
            {
                SetCardBusy(card, false);
            }
        }

        private string? FindDropdownPair(string aiPair)
        {
            if (string.IsNullOrWhiteSpace(aiPair)) return null;
            var items = _cmbAllowedPair.Items.Cast<string>().ToList();

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

        private void ProgrammaticallySelectPair(string pair)
        {
            for (int i = 0; i < _cmbAllowedPair.Items.Count; i++)
            {
                if (string.Equals(_cmbAllowedPair.Items[i]?.ToString(), pair, StringComparison.OrdinalIgnoreCase))
                {
                    _cmbAllowedPair.SelectedIndex = i;
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
                sb.AppendLine($"=== SINGLE PAIR ANALYSIS - {pair} - {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC ===");
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
                string action = (jo["action"]?.ToString() ?? "").Trim().ToUpperInvariant();
                string tradeTypeText = (jo["trade_type"]?.ToString() ?? "").Trim().ToUpperInvariant();
                string reasonText = jo["reason"]?.ToString()
                    ?? jo["comment"]?.ToString()
                    ?? "AI: no trade";
                if (string.IsNullOrWhiteSpace(action) && tradeTypeText == "NO_TRADE")
                    action = "NO_TRADE";

                if (action == "NO_TRADE")
                {
                    info.Direction   = "NONE";
                    info.Status      = "No Trade";
                    info.ShortReason = reasonText;
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
            Return ONLY a valid JSON object - no markdown, no explanatory text outside the JSON.

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

        // â"€â"€ Inner data classes â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€

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

        private double CalculateReviewLotFromRisk(TradeRequest request, AccountInfo? account, SymbolInfo? symbol)
        {
            double entry = request.EntryPrice > 0
                ? request.EntryPrice
                : symbol != null
                    ? request.TradeType == TradeType.BUY ? symbol.Ask : symbol.Bid
                    : 0;

            double equity = account?.Equity ?? 0;
            double lotSize = LotCalculator.Calculate(
                equity,
                _cfg.Bot.MaxRiskPercent,
                entry,
                request.StopLoss,
                request.Pair);

            return BrokerLotSizeValidator.Normalize(lotSize, symbol);
        }

        private sealed class LotSizeOption(string name, double size, string units, string pipValue, bool isAutoFromRisk = false)
        {
            public string Name          { get; } = name;
            public double Size          { get; } = size;
            public string Units         { get; } = units;
            public string PipValue      { get; } = pipValue;
            public bool IsAutoFromRisk  { get; } = isAutoFromRisk;

            public override string ToString() =>
                IsAutoFromRisk ? Name : $"{Name}  {Size:F2}  |  {Units}  |  {PipValue}";
        }

        private static LotSizeOption[] BuildReviewLotOptions(string symbol)
        {
            string sym = symbol.ToUpperInvariant().Replace("/", "").Replace("_", "");
            double standardPipValue = LotCalculator.GetPipValuePerLot(sym);

            if (sym.Contains("XAU") || sym.Contains("GOLD"))
            {
                return
                [
                    new LotSizeOption("Auto From Risk %", 0, "risk", "Bot Max Risk %", true),
                    new LotSizeOption("Micro Lot",    0.01, "1 oz",   $"{symbol} approx ${standardPipValue * 0.01:0.00}/pip"),
                    new LotSizeOption("Mini Lot",     0.10, "10 oz",  $"{symbol} approx ${standardPipValue * 0.10:0.00}/pip"),
                    new LotSizeOption("Standard Lot", 1.00, "100 oz", $"{symbol} approx ${standardPipValue:0.00}/pip")
                ];
            }

            if (sym.Contains("XAG"))
            {
                return
                [
                    new LotSizeOption("Auto From Risk %", 0, "risk", "Bot Max Risk %", true),
                    new LotSizeOption("Micro Lot",    0.01, "50 oz",    $"{symbol} approx ${standardPipValue * 0.01:0.00}/pip"),
                    new LotSizeOption("Mini Lot",     0.10, "500 oz",   $"{symbol} approx ${standardPipValue * 0.10:0.00}/pip"),
                    new LotSizeOption("Standard Lot", 1.00, "5,000 oz", $"{symbol} approx ${standardPipValue:0.00}/pip")
                ];
            }

            return
            [
                new LotSizeOption("Auto From Risk %", 0, "risk", "Bot Max Risk %", true),
                new LotSizeOption("Micro Lot",    0.01, "1,000 units",   $"{symbol} approx ${standardPipValue * 0.01:0.00}/pip"),
                new LotSizeOption("Mini Lot",     0.10, "10,000 units",  $"{symbol} approx ${standardPipValue * 0.10:0.00}/pip"),
                new LotSizeOption("Standard Lot", 1.00, "100,000 units", $"{symbol} approx ${standardPipValue:0.00}/pip")
            ];
        }

        private ScalpingConfig? GetSavedScalpingConfigForPair(string pair)
        {
            string key = NormalizePairKey(pair);
            _cfg.Bot.ScalpingByPair ??= new Dictionary<string, ScalpingConfig>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(key) &&
                _cfg.Bot.ScalpingByPair.TryGetValue(key, out var saved))
            {
                Log($"[SCALP] Loaded saved scalping settings for {key}.", C_ACCENT);
                return CloneScalpingConfig(saved);
            }

            Log($"[SCALP] No saved scalping settings for {key}; using bot suggested scalping values.", C_ACCENT);
            return null;
        }

        private async Task SaveScalpingConfigForPairAsync(string pair, ScalpingConfig config)
        {
            string key = NormalizePairKey(pair);
            if (string.IsNullOrWhiteSpace(key))
                return;

            _cfg.Bot.Scalping = CloneScalpingSettings(config);
            _cfg.Bot.ScalpingByPair ??= new Dictionary<string, ScalpingConfig>(StringComparer.OrdinalIgnoreCase);
            _cfg.Bot.ScalpingByPair[key] = CloneScalpingConfig(config);
            await _settings.SaveAsync(_cfg).ConfigureAwait(false);
            Log($"[SCALP] Saved scalping settings for {key}.", C_ACCENT);
        }

        private NormalTradingSettings? GetSavedNormalTradingSettingsForPair(string pair)
        {
            string key = NormalizePairKey(pair);
            _cfg.Bot.NormalTradingByPair ??= new Dictionary<string, NormalTradingSettings>(StringComparer.OrdinalIgnoreCase);
            return !string.IsNullOrWhiteSpace(key) &&
                   _cfg.Bot.NormalTradingByPair.TryGetValue(key, out var saved)
                ? CloneNormalTradingSettings(saved)
                : null;
        }

        private async Task SaveNormalTradingSettingsForPairAsync(string pair, NormalTradingSettings settings)
        {
            string key = NormalizePairKey(pair);
            if (string.IsNullOrWhiteSpace(key))
                return;

            _cfg.Bot.NormalTrading = CloneNormalTradingSettings(settings);
            _cfg.Bot.NormalTradingByPair ??= new Dictionary<string, NormalTradingSettings>(StringComparer.OrdinalIgnoreCase);
            _cfg.Bot.NormalTradingByPair[key] = CloneNormalTradingSettings(settings);
            await _settings.SaveAsync(_cfg).ConfigureAwait(false);
            Log($"[BOT] Saved normal trading settings for {key}.", C_ACCENT);
        }

        private ScalpingConfig BuildSuggestedScalpingConfigForPair(string pair, SymbolInfo? symbol)
        {
            var pairRules = _pairSettings?.GetForPair(pair);
            double liveSpread = symbol?.SpreadPips > 0 ? symbol.SpreadPips : 0;
            double configuredMaxSpread = _cfg.Bot.Scalping.MaxSpreadPips > 0
                ? _cfg.Bot.Scalping.MaxSpreadPips
                : 3;
            double spreadForCalc = liveSpread > 0
                ? liveSpread
                : configuredMaxSpread * 0.75;

            double slPips = Math.Max(1, _cfg.Bot.Scalping.StopLossPips);
            double requiredRr = Math.Max(0.1, _cfg.Bot.Scalping.RiskRewardRatio);
            double tpPips = Math.Max(1, _cfg.Bot.Scalping.TakeProfitPips);
            double maxSpreadPips = RoundHalfPip(Math.Clamp(
                liveSpread > 0 ? Math.Max(0.1, liveSpread * 1.15) : configuredMaxSpread,
                0.1,
                Math.Min(tpPips * 0.20, Math.Min(100, Math.Max(0.1, configuredMaxSpread)))));

            return new ScalpingConfig
            {
                MaxTrades = 3,
                MaxMinutes = 60,
                MaxSessionLossUsd = Math.Max(20, _cfg.Bot.Scalping.MaxSessionLossUsd),
                ProfitTargetUsd = Math.Max(20, _cfg.Bot.Scalping.ProfitTargetUsd),
                StopLossPips = slPips,
                TakeProfitPips = tpPips,
                RiskRewardRatio = requiredRr,
                MaxSpreadPips = maxSpreadPips,
                DynamicValuesEnabled = true,
                PollIntervalMs = _cfg.Bot.Scalping.PollIntervalMs,
                CooldownSeconds = _cfg.Bot.Scalping.CooldownSeconds,
                DirectionMode = ScalpingDirectionMode.Auto,
                AllowPyramiding = false,
                RequireSnapshotConfirmation = true,
                MinDecisionScore = Math.Max(6, _cfg.Bot.Scalping.MinDecisionScore),
                UseAiConfirmation = false
            };
        }

        private ScalpingSettings BuildScalpingConfigFromReview(
            string pair,
            ComboBox mode,
            NumericUpDown trades,
            NumericUpDown minutes,
            NumericUpDown sl,
            NumericUpDown tp,
            NumericUpDown rr,
            NumericUpDown spread,
            CheckBox aiConfirm)
        {
            var cfg = CloneScalpingSettings(_cfg.Bot.Scalping);
            cfg.MaxTrades = Math.Clamp((int)trades.Value, 1, 6);
            cfg.MaxMinutes = Math.Clamp((int)minutes.Value, 1, 90);
            cfg.StopLossPips = (double)sl.Value;
            cfg.RiskRewardRatio = Math.Max(0.1, (double)rr.Value);
            cfg.TakeProfitPips = Math.Max((double)tp.Value, cfg.StopLossPips * cfg.RiskRewardRatio);
            cfg.MaxSpreadPips = (double)spread.Value;
            cfg.DynamicValuesEnabled = true;
            cfg.RequireSnapshotConfirmation = true;
            cfg.AllowPyramiding = false;
            cfg.MinDecisionScore = Math.Max(6, cfg.MinDecisionScore);
            cfg.UseAiConfirmation = aiConfirm.Checked;
            cfg.DirectionMode = mode.SelectedIndex switch
            {
                1 => ScalpingDirectionMode.SignalDirection,
                2 => ScalpingDirectionMode.BuyOnly,
                3 => ScalpingDirectionMode.SellOnly,
                _ => ScalpingDirectionMode.Auto
            };
            return cfg;
        }

        private static CommonTradingSettings BuildCommonTradingSettingsFromReview(
            ComboBox mode,
            CheckBox aiConfirm,
            CheckBox autoClose,
            NumericUpDown pipsTarget,
            NumericUpDown moneyTarget,
            NumericUpDown beTrigger) => new()
            {
                TradingMode = mode.SelectedIndex switch
                {
                    0 => TradingControlMode.Auto,
                    2 => TradingControlMode.PaperTrading,
                    _ => TradingControlMode.ManualApproval
                },
                UseAiConfirmation = aiConfirm.Checked,
                AutoCloseAfterOpen = autoClose.Checked,
                ProfitTargetPips = (double)pipsTarget.Value,
                ProfitTargetUsd = (double)moneyTarget.Value,
                BeTriggerPercentOfTp = (double)beTrigger.Value
            };

        private static NormalTradingSettings BuildNormalTradingSettingsFromReview(
            CheckBox enabled,
            NumericUpDown trades,
            NumericUpDown expiry,
            NumericUpDown sl,
            NumericUpDown tp,
            NumericUpDown spread,
            NumericUpDown rr) => new()
            {
                Enabled = enabled.Checked,
                MaxTrades = Math.Clamp((int)trades.Value, 1, 50),
                ExpiryMinutes = Math.Clamp((int)expiry.Value, 1, 10080),
                StopLossPips = (double)sl.Value,
                TakeProfitPips = Math.Max((double)tp.Value, (double)sl.Value * Math.Max(0.1, (double)rr.Value)),
                MaxSpreadPips = (double)spread.Value,
                RiskRewardRatio = Math.Max(0.1, (double)rr.Value)
            };

        private static TradeRequest ApplyNormalTradingSettingsToRequest(
            TradeRequest request,
            NormalTradingSettings settings,
            SymbolInfo? symbol)
        {
            double entry = request.EntryPrice > 0
                ? request.EntryPrice
                : symbol != null
                    ? request.TradeType == TradeType.BUY ? symbol.Ask : symbol.Bid
                    : 0;
            if (entry <= 0)
                return request;

            double pipSize = request.Pair.Contains("JPY", StringComparison.OrdinalIgnoreCase) ? 0.01 : 0.0001;
            if (request.Pair.Contains("XAU", StringComparison.OrdinalIgnoreCase))
                pipSize = 0.01;

            double slDistance = settings.StopLossPips * pipSize;
            double tpDistance = Math.Max(settings.TakeProfitPips, settings.StopLossPips * settings.RiskRewardRatio) * pipSize;
            request.EntryPrice = entry;
            request.Strategy = "Normal";
            request.MaxSpreadPips = settings.MaxSpreadPips;
            if (request.TradeType == TradeType.BUY)
            {
                request.StopLoss = entry - slDistance;
                request.TakeProfit = entry + tpDistance;
            }
            else
            {
                request.StopLoss = entry + slDistance;
                request.TakeProfit = entry - tpDistance;
            }

            return request;
        }

        private static TradeRequest ApplyScalpingSettingsToRequest(
            TradeRequest request,
            ScalpingConfig settings,
            SymbolInfo? symbol)
        {
            double entry = request.EntryPrice > 0
                ? request.EntryPrice
                : symbol != null
                    ? request.TradeType == TradeType.BUY ? symbol.Ask : symbol.Bid
                    : 0;
            if (entry <= 0)
                return request;

            double pipSize = request.Pair.Contains("JPY", StringComparison.OrdinalIgnoreCase) ? 0.01 : 0.0001;
            if (request.Pair.Contains("XAU", StringComparison.OrdinalIgnoreCase))
                pipSize = 0.01;

            double slDistance = settings.StopLossPips * pipSize;
            double tpDistance = Math.Max(settings.TakeProfitPips, settings.StopLossPips * settings.RiskRewardRatio) * pipSize;
            request.EntryPrice = entry;
            request.Strategy = "Scalping";
            request.MaxSpreadPips = settings.MaxSpreadPips;
            if (request.TradeType == TradeType.BUY)
            {
                request.StopLoss = entry - slDistance;
                request.TakeProfit = entry + tpDistance;
            }
            else
            {
                request.StopLoss = entry + slDistance;
                request.TakeProfit = entry - tpDistance;
            }

            return request;
        }

        private static ScalpingConfig MergeSavedScalpingPreferences(ScalpingConfig saved, ScalpingConfig suggested)
        {
            var cfg = CloneScalpingConfig(suggested);
            cfg.MaxTrades = Math.Clamp(saved.MaxTrades, 1, 6);
            cfg.MaxMinutes = Math.Clamp(saved.MaxMinutes, 1, 90);
            cfg.MaxSessionLossUsd = saved.MaxSessionLossUsd;
            cfg.ProfitTargetUsd = saved.ProfitTargetUsd;
            cfg.PollIntervalMs = saved.PollIntervalMs;
            cfg.CooldownSeconds = saved.CooldownSeconds;
            cfg.DirectionMode = saved.DirectionMode;
            cfg.AllowPyramiding = false;
            cfg.RequireSnapshotConfirmation = true;
            cfg.MinDecisionScore = Math.Max(6, saved.MinDecisionScore);
            cfg.UseAiConfirmation = saved.UseAiConfirmation;
            return cfg;
        }

        private static ScalpingConfig CloneScalpingConfig(ScalpingConfig config) => new()
        {
            MaxTrades = config.MaxTrades,
            MaxMinutes = config.MaxMinutes,
            MaxSessionLossUsd = config.MaxSessionLossUsd,
            ProfitTargetUsd = config.ProfitTargetUsd,
            StopLossPips = config.StopLossPips,
            TakeProfitPips = config.TakeProfitPips,
            RiskRewardRatio = config.RiskRewardRatio,
            MaxSpreadPips = config.MaxSpreadPips,
            MaxSpreadPercentOfTp = config.MaxSpreadPercentOfTp,
            DynamicValuesEnabled = config.DynamicValuesEnabled,
            PollIntervalMs = config.PollIntervalMs,
            CooldownSeconds = config.CooldownSeconds,
            DirectionMode = config.DirectionMode,
            AllowPyramiding = config.AllowPyramiding,
            RequireSnapshotConfirmation = config.RequireSnapshotConfirmation,
            MinDecisionScore = config.MinDecisionScore,
            UseAiConfirmation = config.UseAiConfirmation
        };

        private static ScalpingSettings CloneScalpingSettings(ScalpingConfig config) => new()
        {
            Enabled = config is ScalpingSettings settings && settings.Enabled,
            MaxTrades = config.MaxTrades,
            MaxMinutes = config.MaxMinutes,
            MaxSessionLossUsd = config.MaxSessionLossUsd,
            ProfitTargetUsd = config.ProfitTargetUsd,
            StopLossPips = config.StopLossPips,
            TakeProfitPips = config.TakeProfitPips,
            RiskRewardRatio = config.RiskRewardRatio,
            MaxSpreadPips = config.MaxSpreadPips,
            MaxSpreadPercentOfTp = config.MaxSpreadPercentOfTp,
            DynamicValuesEnabled = config.DynamicValuesEnabled,
            PollIntervalMs = config.PollIntervalMs,
            CooldownSeconds = config.CooldownSeconds,
            DirectionMode = config.DirectionMode,
            AllowPyramiding = config.AllowPyramiding,
            RequireSnapshotConfirmation = config.RequireSnapshotConfirmation,
            MinDecisionScore = config.MinDecisionScore,
            UseAiConfirmation = config.UseAiConfirmation
        };

        private static CommonTradingSettings CloneCommonTradingSettings(CommonTradingSettings settings) => new()
        {
            TradingMode = settings.TradingMode,
            UseAiConfirmation = settings.UseAiConfirmation,
            AutoCloseAfterOpen = settings.AutoCloseAfterOpen,
            ProfitTargetPips = settings.ProfitTargetPips,
            ProfitTargetUsd = settings.ProfitTargetUsd,
            BeTriggerPercentOfTp = settings.BeTriggerPercentOfTp
        };

        private static NormalTradingSettings CloneNormalTradingSettings(NormalTradingSettings settings) => new()
        {
            Enabled = settings.Enabled,
            MaxTrades = settings.MaxTrades,
            ExpiryMinutes = settings.ExpiryMinutes,
            StopLossPips = settings.StopLossPips,
            TakeProfitPips = settings.TakeProfitPips,
            MaxSpreadPips = settings.MaxSpreadPips,
            RiskRewardRatio = settings.RiskRewardRatio
        };

        private static string NormalizePairKey(string pair) =>
            (pair ?? "").Trim().ToUpperInvariant().Replace("/", "").Replace("_", "");

        private static double RoundHalfPip(double value) =>
            Math.Round(value * 2.0, MidpointRounding.AwayFromZero) / 2.0;

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

