using MT5TradingBot.Core;
using MT5TradingBot.Models;
using MT5TradingBot.Services;
using Newtonsoft.Json;
using Serilog;

namespace MT5TradingBot.UI
{
    public sealed partial class MainForm : Form
    {
        // ── Services ──────────────────────────────────────────────
        private MT5Bridge? _bridge;
        private AutoBotService? _bot;
        private readonly SettingsManager _settings = new();
        private AppSettings _cfg = new();

        // ── Timers ────────────────────────────────────────────────
        private readonly System.Windows.Forms.Timer _refreshTimer;

        // ── Theme ─────────────────────────────────────────────────
        private static readonly Color C_BG      = Color.FromArgb(13, 13, 19);
        private static readonly Color C_SURFACE = Color.FromArgb(22, 22, 32);
        private static readonly Color C_CARD    = Color.FromArgb(28, 29, 42);
        private static readonly Color C_ACCENT  = Color.FromArgb(99, 179, 237);
        private static readonly Color C_GREEN   = Color.FromArgb(72, 199, 142);
        private static readonly Color C_RED     = Color.FromArgb(252, 95, 95);
        private static readonly Color C_YELLOW  = Color.FromArgb(250, 199, 117);
        private static readonly Color C_TEXT    = Color.FromArgb(218, 218, 230);
        private static readonly Color C_MUTED   = Color.FromArgb(110, 110, 130);
        private static readonly Color C_BORDER  = Color.FromArgb(45, 45, 65);

        // ── Control references (set in Build*) ────────────────────

        // Header
        private Panel _pnlDot = null!;
        private Label _lblConnStatus = null!;
        private Label _lblTime = null!;

        // Account bar
        private Label _lblAccNum = null!, _lblBalance = null!, _lblEquity = null!;
        private Label _lblFreeMargin = null!, _lblPnl = null!, _lblMarginLvl = null!;

        // Connection
        private ComboBox _cmbMode = null!;
        private TextBox _txtPipeName = null!;
        private Button _btnConnect = null!;
        private Button _btnDisconnect = null!;

        // Trade tab
        private ComboBox _cmbPair = null!, _cmbDir = null!, _cmbOrderType = null!;
        private TextBox _txtEntry = null!, _txtSL = null!, _txtTP = null!;
        private TextBox _txtTP2 = null!, _txtLot = null!;
        private CheckBox _chkAutoLot = null!, _chkMoveSLBE = null!;
        private Label _lblRR = null!, _lblDollarRisk = null!, _lblDollarProfit = null!;
        private Button _btnBuy = null!, _btnSell = null!;
        private RichTextBox _txtJson = null!;

        // Positions tab
        private DataGridView _gridPos = null!;

        // History tab
        private DataGridView _gridHistory = null!;

        // Bot tab
        private TextBox _txtWatchFolder = null!;
        private NumericUpDown _nudRisk = null!, _nudMaxTrades = null!, _nudPollMs = null!;
        private TextBox _txtAllowedPairs = null!;
        private CheckBox _chkAutoLotBot = null!, _chkEnforceRR = null!;
        private CheckBox _chkDrawdown = null!, _chkAutoStart = null!;
        private NumericUpDown _nudMinRR = null!, _nudDrawdownPct = null!;
        private NumericUpDown _nudRetry = null!;
        private Button _btnStartBot = null!, _btnStopBot = null!;
        private Label _lblBotBadge = null!;

        // Log
        private RichTextBox _txtLog = null!;

        // ═══════════════════════════════════════════════════════════
        public MainForm()
        {
            _refreshTimer = new System.Windows.Forms.Timer { Interval = 2500 };
            _refreshTimer.Tick += async (_, _) => await OnRefreshTickAsync();

            BuildUI();
            _ = InitAsync();
        }

        private async Task InitAsync()
        {
            await _settings.LoadAsync();
            _cfg = _settings.Current;
            ApplySettingsToUI();

            if (_cfg.AutoConnectOnLaunch)
                await ConnectAsync();

            if (_cfg.Bot.AutoStartOnLaunch && _bridge?.IsConnected == true)
                await StartBotAsync();

            Log("MT5 Trading Bot ready. Connect to MT5 to begin.", C_ACCENT);
        }

        // ══════════════════════════════════════════════════════════
        //  BUILD UI
        // ══════════════════════════════════════════════════════════

        private void BuildUI()
        {
            Text = "MT5 Trading Bot — Professional";
            Size = new Size(1280, 860);
            MinimumSize = new Size(1100, 720);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = C_BG;
            ForeColor = C_TEXT;
            Font = new Font("Segoe UI", 9f);
            DoubleBuffered = true;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 4, ColumnCount = 1
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));  // header
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));  // conn bar
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));  // account bar
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // tabs

            layout.Controls.Add(BuildHeader(),     0, 0);
            layout.Controls.Add(BuildConnBar(),    0, 1);
            layout.Controls.Add(BuildAccountBar(), 0, 2);
            layout.Controls.Add(BuildTabs(),       0, 3);

            Controls.Add(layout);
            FormClosing += OnFormClosingAsync;
        }

        // ── Header ────────────────────────────────────────────────
        private Control BuildHeader()
        {
            var p = new Panel { Dock = DockStyle.Fill, BackColor = C_SURFACE };

            var title = Lbl("⚡  MT5 Trading Bot", 16, 14,
                new Font("Segoe UI Semibold", 14f, FontStyle.Bold), C_ACCENT);

            _pnlDot = new Panel
            {
                Size = new Size(10, 10), BackColor = C_RED,
                Location = new Point(230, 21)
            };
            RoundPanel(_pnlDot);

            _lblConnStatus = Lbl("Disconnected", 246, 18, null, C_RED);

            _lblTime = Lbl("", 900, 18, new Font("Consolas", 8.5f), C_MUTED);

            var clockTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            clockTimer.Tick += (_, _) =>
                _lblTime.Text = $"UTC {DateTime.UtcNow:HH:mm:ss}  |  Local {DateTime.Now:HH:mm:ss}";
            clockTimer.Start();

            p.Controls.AddRange([title, _pnlDot, _lblConnStatus, _lblTime]);
            return p;
        }

        // ── Connection bar ────────────────────────────────────────
        private Control BuildConnBar()
        {
            var p = new Panel { Dock = DockStyle.Fill, BackColor = C_CARD };

            _cmbMode = Cmb(["Named Pipe (Local MT5)", "TCP Socket (Remote)"], 10, 8, 180);
            var lblPipe = Lbl("Pipe/Host:", 200, 12, null, C_MUTED);
            _txtPipeName = Txt("MT5TradingBotPipe", 270, 7, 220);

            _btnConnect = Btn("⚡ Connect", 502, 7, 110, C_GREEN);
            _btnDisconnect = Btn("Disconnect", 622, 7, 100, C_RED);
            _btnDisconnect.Enabled = false;

            var chkAutoConn = new CheckBox
            {
                Text = "Auto-connect on launch", ForeColor = C_MUTED,
                Location = new Point(734, 10), AutoSize = true
            };
            chkAutoConn.CheckedChanged += (_, _) =>
                _cfg.AutoConnectOnLaunch = chkAutoConn.Checked;

            _btnConnect.Click    += async (_, _) => await ConnectAsync();
            _btnDisconnect.Click += async (_, _) => await DisconnectAsync();

            p.Controls.AddRange([_cmbMode, lblPipe, _txtPipeName,
                _btnConnect, _btnDisconnect, chkAutoConn]);
            return p;
        }

        // ── Account bar ───────────────────────────────────────────
        private Control BuildAccountBar()
        {
            var p = new Panel { Dock = DockStyle.Fill, BackColor = C_SURFACE };

            _lblAccNum     = Lbl("Account: —",          8,  12, null, C_MUTED);
            _lblBalance    = Lbl("Balance: —",          170, 12, null, C_TEXT);
            _lblEquity     = Lbl("Equity: —",           300, 12, null, C_TEXT);
            _lblFreeMargin = Lbl("Free Margin: —",      430, 12, null, C_TEXT);
            _lblPnl        = Lbl("P&L: —",              580, 12, null, C_TEXT);
            _lblMarginLvl  = Lbl("Margin Lvl: —",       680, 12, null, C_MUTED);

            p.Controls.AddRange([_lblAccNum, _lblBalance, _lblEquity,
                _lblFreeMargin, _lblPnl, _lblMarginLvl]);
            return p;
        }

        // ── Tabs ──────────────────────────────────────────────────
        private Control BuildTabs()
        {
            var tc = new TabControl
            {
                Dock = DockStyle.Fill, DrawMode = TabDrawMode.OwnerDrawFixed,
                Padding = new Point(14, 6)
            };
            tc.DrawItem += DrawTabItem;
            tc.BackColor = C_BG;

            tc.TabPages.Add(BuildTradeTab());
            tc.TabPages.Add(BuildPositionsTab());
            tc.TabPages.Add(BuildHistoryTab());
            tc.TabPages.Add(BuildBotTab());
            tc.TabPages.Add(BuildLogTab());

            return tc;
        }

        // ── TRADE TAB ─────────────────────────────────────────────
        private TabPage BuildTradeTab()
        {
            var tab = Tab("  📈 Trade  ");

            // Left card: form inputs
            var left = Card(12, 8, 370, 680);

            int y = 14;
            left.Controls.Add(CardH("Manual Trade Entry", 14, y)); y += 36;

            left.Controls.Add(Lbl("Pair", 14, y, null, C_MUTED));
            _cmbPair = Cmb(["GBPUSD","EURUSD","USDJPY","XAUUSD","USDCAD","AUDUSD","EURGBP"],
                130, y - 2, 210);
            _cmbPair.SelectedIndexChanged += (_, _) => RecalcRR();
            left.Controls.Add(_cmbPair); y += 32;

            left.Controls.Add(Lbl("Direction", 14, y, null, C_MUTED));
            _cmbDir = Cmb(["BUY", "SELL"], 130, y - 2, 210);
            _cmbDir.SelectedIndexChanged += (_, _) => { UpdateBuySellColors(); RecalcRR(); };
            left.Controls.Add(_cmbDir); y += 32;

            left.Controls.Add(Lbl("Order Type", 14, y, null, C_MUTED));
            _cmbOrderType = Cmb(["MARKET", "LIMIT", "STOP"], 130, y - 2, 210);
            _cmbOrderType.SelectedIndexChanged += (_, _) =>
                _txtEntry.Enabled = _cmbOrderType.SelectedIndex != 0;
            left.Controls.Add(_cmbOrderType); y += 32;

            left.Controls.Add(Lbl("Entry Price", 14, y, null, C_MUTED));
            _txtEntry = Txt("0 (market)", 130, y - 2, 210);
            _txtEntry.Enabled = false;
            _txtEntry.TextChanged += (_, _) => RecalcRR();
            left.Controls.Add(_txtEntry); y += 32;

            left.Controls.Add(MkRedLbl("Stop Loss ✱", 14, y));
            _txtSL = Txt("e.g. 1.34750", 130, y - 2, 210);
            _txtSL.TextChanged += (_, _) => RecalcRR();
            left.Controls.Add(_txtSL); y += 32;

            left.Controls.Add(MkGreenLbl("Take Profit ✱", 14, y));
            _txtTP = Txt("e.g. 1.35200", 130, y - 2, 210);
            _txtTP.TextChanged += (_, _) => RecalcRR();
            left.Controls.Add(_txtTP); y += 32;

            left.Controls.Add(Lbl("Take Profit 2", 14, y, null, C_ACCENT));
            _txtTP2 = Txt("0 (optional)", 130, y - 2, 210);
            left.Controls.Add(_txtTP2); y += 32;

            _chkAutoLot = new CheckBox
            {
                Text = "Auto lot size (1% risk)", ForeColor = C_YELLOW, Checked = true,
                Location = new Point(14, y), AutoSize = true
            };
            _chkAutoLot.CheckedChanged += (_, _) =>
            { _txtLot.Enabled = !_chkAutoLot.Checked; RecalcRR(); };
            left.Controls.Add(_chkAutoLot); y += 28;

            left.Controls.Add(Lbl("Lot Size", 14, y, null, C_MUTED));
            _txtLot = Txt("0.01", 130, y - 2, 100);
            _txtLot.Enabled = false;
            _txtLot.TextChanged += (_, _) => RecalcRR();
            left.Controls.Add(_txtLot); y += 32;

            _chkMoveSLBE = new CheckBox
            {
                Text = "Move SL → Breakeven after TP1", ForeColor = C_ACCENT,
                Location = new Point(14, y), AutoSize = true, Checked = true
            };
            left.Controls.Add(_chkMoveSLBE); y += 30;

            // R:R display
            var rrPanel = new Panel
            {
                Location = new Point(14, y), Size = new Size(326, 52),
                BackColor = Color.FromArgb(20, 99, 179, 237), BorderStyle = BorderStyle.None
            };
            _lblRR         = Lbl("R:R  —", 10, 8, new Font("Consolas", 9f), C_ACCENT);
            _lblDollarRisk = Lbl("Risk  $—", 10, 26, new Font("Consolas", 8.5f), C_RED);
            _lblDollarProfit = Lbl("Profit  $—", 170, 26, new Font("Consolas", 8.5f), C_GREEN);
            rrPanel.Controls.AddRange([_lblRR, _lblDollarRisk, _lblDollarProfit]);
            left.Controls.Add(rrPanel);
            y += 60;

            // BUY / SELL buttons
            _btnBuy = Btn("▲  BUY", 14, y, 152, C_GREEN);
            _btnSell = Btn("▼  SELL", 174, y, 152, C_RED);
            _btnBuy.Font = _btnSell.Font = new Font("Segoe UI Semibold", 11f);
            _btnBuy.Height = _btnSell.Height = 44;
            _btnBuy.Click  += async (_, _) => await SubmitTradeAsync(TradeType.BUY);
            _btnSell.Click += async (_, _) => await SubmitTradeAsync(TradeType.SELL);
            left.Controls.AddRange([_btnBuy, _btnSell]);

            // Right card: JSON
            var right = Card(392, 8, 838, 680);
            right.Controls.Add(CardH("JSON Signal Input  (paste, load file, or drop)", 14, 14));

            _txtJson = new RichTextBox
            {
                Location = new Point(14, 50), Size = new Size(808, 540),
                BackColor = Color.FromArgb(14, 14, 22),
                ForeColor = Color.FromArgb(200, 220, 180),
                Font = new Font("Consolas", 9.5f),
                BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.Vertical
            };
            _txtJson.Text = DefaultJsonSample();
            _txtJson.AllowDrop = true;
            _txtJson.DragEnter += (_, e) =>
                e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true
                    ? DragDropEffects.Copy : DragDropEffects.None;
            _txtJson.DragDrop += (_, e) =>
            {
                string[]? files = e.Data?.GetData(DataFormats.FileDrop) as string[];
                if (files?.Length > 0) _txtJson.Text = File.ReadAllText(files[0]);
            };
            right.Controls.Add(_txtJson);

            var btnLoad   = Btn("📂 Load File",     14,  600, 130, C_ACCENT);
            var btnExec   = Btn("⚡ Execute JSON",  154,  600, 140, C_GREEN);
            var btnFmt    = Btn("🔧 Format",        304,  600, 100, C_YELLOW);
            var btnSample = Btn("📋 Sample",        414,  600, 100, C_MUTED);
            btnLoad.Click   += (_, _) => LoadJsonFile();
            btnExec.Click   += async (_, _) => await ExecuteJsonAsync();
            btnFmt.Click    += (_, _) => FormatJson();
            btnSample.Click += (_, _) => _txtJson.Text = DefaultJsonSample();
            right.Controls.AddRange([btnLoad, btnExec, btnFmt, btnSample]);

            tab.Controls.AddRange([left, right]);
            return tab;
        }

        // ── POSITIONS TAB ─────────────────────────────────────────
        private TabPage BuildPositionsTab()
        {
            var tab = Tab("  📊 Positions  ");

            _gridPos = MkGrid();
            _gridPos.Location = new Point(12, 8);
            _gridPos.Size = new Size(1220, 580);

            foreach (var c in new[]
            {
                "Ticket","Symbol","Type","Lots","Open","Current","SL","TP","P&L ($)","Pips","Time","Comment"
            })
            _gridPos.Columns.Add(c.Replace(" ","_"), c);

            var btnClose    = Btn("Close Selected",    12, 600, 150, C_RED);
            var btnCloseAll = Btn("Close ALL",        172, 600, 130, Color.FromArgb(160,50,50));
            var btnRef      = Btn("🔄 Refresh",       312, 600, 110, C_ACCENT);
            btnClose.Click    += async (_, _) => await CloseSelectedAsync();
            btnCloseAll.Click += async (_, _) => await CloseAllAsync();
            btnRef.Click      += async (_, _) => await RefreshPositionsAsync();

            tab.Controls.AddRange([_gridPos, btnClose, btnCloseAll, btnRef]);
            return tab;
        }

        // ── HISTORY TAB ───────────────────────────────────────────
        private TabPage BuildHistoryTab()
        {
            var tab = Tab("  🗂 History  ");

            _gridHistory = MkGrid();
            _gridHistory.Location = new Point(12, 8);
            _gridHistory.Size = new Size(1220, 580);

            foreach (var c in new[]
            {
                "Time","Id","Pair","Dir","Lots","Entry","SL","TP","Ticket","Status","Exec Price","Error"
            })
            _gridHistory.Columns.Add(c.Replace(" ","_"), c);

            var btnImport = Btn("📂 Load CSV Log", 12, 600, 150, C_ACCENT);
            var btnClear  = Btn("Clear",           172, 600, 80, C_MUTED);
            btnImport.Click += (_, _) => LoadHistoryFromCsv();
            btnClear.Click  += (_, _) => _gridHistory.Rows.Clear();

            tab.Controls.AddRange([_gridHistory, btnImport, btnClear]);
            return tab;
        }

        // ── BOT TAB ───────────────────────────────────────────────
        private TabPage BuildBotTab()
        {
            var tab = Tab("  🤖 Auto Bot  ");

            // Status badge
            _lblBotBadge = new Label
            {
                Text = "● BOT STOPPED", ForeColor = C_RED,
                Font = new Font("Segoe UI Semibold", 12f),
                Location = new Point(14, 14), AutoSize = true
            };
            tab.Controls.Add(_lblBotBadge);

            // Settings card
            var card = Card(14, 50, 560, 620);

            int y = 14;
            card.Controls.Add(CardH("Bot Configuration", 14, y)); y += 36;

            void Row(string label, Control ctrl)
            {
                card.Controls.Add(Lbl(label, 14, y, null, C_MUTED));
                ctrl.Location = new Point(200, y - 2);
                card.Controls.Add(ctrl);
                y += 32;
            }

            _txtWatchFolder = Txt(@"C:\MT5Bot\signals", 0, 0, 330);
            Row("Watch Folder", _txtWatchFolder);

            _nudRisk = Nud(0.1m, 10m, 1m, 1, 0.1m, 100);
            Row("Max Risk %", _nudRisk);

            _nudMinRR = Nud(0.5m, 10m, 1.5m, 1, 0.1m, 100);
            Row("Min R:R Ratio", _nudMinRR);

            _nudMaxTrades = Nud(1, 100, 5, 0, 1, 100);
            Row("Max Trades/Day", _nudMaxTrades);

            _nudPollMs = Nud(500, 30000, 2000, 0, 500, 120);
            Row("Poll Interval ms", _nudPollMs);

            _nudRetry = Nud(0, 10, 3, 0, 1, 100);
            Row("Retry Count", _nudRetry);

            _txtAllowedPairs = Txt("GBPUSD,EURUSD,USDJPY", 0, 0, 330);
            Row("Allowed Pairs", _txtAllowedPairs);

            _nudDrawdownPct = Nud(1, 50, 10, 1, 1, 100);
            Row("Drawdown Stop %", _nudDrawdownPct);

            y += 8;
            void Ck(ref CheckBox cb, string text, bool def)
            {
                cb = new CheckBox { Text = text, ForeColor = C_MUTED, Checked = def,
                    Location = new Point(14, y), AutoSize = true };
                card.Controls.Add(cb);
                y += 26;
            }
            Ck(ref _chkAutoLotBot, "Auto lot calculation from equity", true);
            Ck(ref _chkEnforceRR, "Enforce minimum R:R (reject if below)", true);
            Ck(ref _chkDrawdown, "Drawdown protection (emergency close)", true);
            Ck(ref _chkAutoStart, "Auto-start bot on app launch", false);
            y += 12;

            _btnStartBot = Btn("▶  Start Bot",  14, y, 160, C_GREEN);
            _btnStopBot  = Btn("■  Stop Bot",  184, y, 160, C_RED);
            _btnStartBot.Font = _btnStopBot.Font = new Font("Segoe UI Semibold", 10f);
            _btnStartBot.Height = _btnStopBot.Height = 42;
            _btnStartBot.Click += async (_, _) => await StartBotAsync();
            _btnStopBot.Click  += async (_, _) => await StopBotAsync();
            _btnStopBot.Enabled = false;
            card.Controls.AddRange([_btnStartBot, _btnStopBot]);

            // Info card
            var info = Card(590, 50, 640, 620);
            info.Controls.Add(CardH("How It Works", 14, 14));
            var rtb = new RichTextBox
            {
                Location = new Point(14, 50), Size = new Size(608, 550),
                BackColor = C_CARD, ForeColor = C_MUTED, BorderStyle = BorderStyle.None,
                ReadOnly = true, Font = new Font("Consolas", 9f),
                Text = BotHelpText()
            };
            info.Controls.Add(rtb);

            var btnFolder = Btn("📁 Open Folder", 14, 680, 140, C_ACCENT);
            btnFolder.Click += (_, _) =>
            {
                Directory.CreateDirectory(_txtWatchFolder.Text);
                System.Diagnostics.Process.Start("explorer.exe", _txtWatchFolder.Text);
            };

            tab.Controls.AddRange([card, info, btnFolder]);
            return tab;
        }

        // ── LOG TAB ───────────────────────────────────────────────
        private TabPage BuildLogTab()
        {
            var tab = Tab("  📋 Log  ");

            _txtLog = new RichTextBox
            {
                Location = new Point(12, 8), Size = new Size(1222, 640),
                BackColor = Color.FromArgb(12, 12, 18), ForeColor = C_TEXT,
                Font = new Font("Consolas", 9f), ReadOnly = true, BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.Vertical
            };

            var btnClear = Btn("Clear",     12, 658, 100, C_MUTED);
            var btnSave  = Btn("Save Log", 122, 658, 100, C_ACCENT);
            btnClear.Click += (_, _) => _txtLog.Clear();
            btnSave.Click  += (_, _) =>
            {
                using var d = new SaveFileDialog { Filter = "Text|*.txt", FileName = "MT5Log" };
                if (d.ShowDialog() == DialogResult.OK) File.WriteAllText(d.FileName, _txtLog.Text);
            };

            tab.Controls.AddRange([_txtLog, btnClear, btnSave]);
            return tab;
        }

        // ══════════════════════════════════════════════════════════
        //  CONNECT / DISCONNECT
        // ══════════════════════════════════════════════════════════

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
                Log("✅ Connected to MT5 EA", C_GREEN);
                await RefreshAsync();
            }
            else
            {
                Log("❌ Cannot connect. Ensure:\n" +
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

        // ══════════════════════════════════════════════════════════
        //  TRADE EXECUTION
        // ══════════════════════════════════════════════════════════

        private async Task SubmitTradeAsync(TradeType dir)
        {
            if (!AssertConnected()) return;

            if (!double.TryParse(_txtSL.Text, out double sl) || sl == 0)
            { Log("❌ Invalid Stop Loss", C_RED); return; }
            if (!double.TryParse(_txtTP.Text, out double tp) || tp == 0)
            { Log("❌ Invalid Take Profit", C_RED); return; }

            double.TryParse(_txtEntry.Text, out double entry);
            double.TryParse(_txtTP2.Text, out double tp2);
            double.TryParse(_txtLot.Text, out double lot);
            if (lot < 0.01) lot = 0.01;

            var req = new TradeRequest
            {
                Pair = _cmbPair.SelectedItem?.ToString() ?? "GBPUSD",
                TradeType = dir,
                OrderType = _cmbOrderType.SelectedIndex switch
                { 1 => OrderType.LIMIT, 2 => OrderType.STOP, _ => OrderType.MARKET },
                EntryPrice = entry,
                StopLoss = sl,
                TakeProfit = tp,
                TakeProfit2 = tp2,
                LotSize = _chkAutoLot.Checked ? 0.01 : lot,
                MoveSLToBreakevenAfterTP1 = _chkMoveSLBE.Checked,
                MagicNumber = _cfg.Bot.MagicNumber,
                Comment = "Manual"
            };

            // Use bot validation pipeline if bot is available, else direct bridge
            TradeResult result;
            if (_bot != null)
                result = await _bot.ExecuteTradeWithValidationAsync(req);
            else
                result = await _bridge!.OpenTradeAsync(req);

            Log(result.IsSuccess ? $"✅ {result}" : $"❌ {result}", result.IsSuccess ? C_GREEN : C_RED);
            AddHistoryRow(req, result);
        }

        private async Task ExecuteJsonAsync()
        {
            if (!AssertConnected()) return;
            try
            {
                var req = JsonConvert.DeserializeObject<TradeRequest>(_txtJson.Text);
                if (req == null) { Log("❌ Invalid JSON structure", C_RED); return; }

                var (valid, err) = req.Validate();
                if (!valid) { Log($"❌ Validation: {err}", C_RED); return; }

                TradeResult result;
                if (_bot != null)
                    result = await _bot.ExecuteTradeWithValidationAsync(req);
                else
                    result = await _bridge!.OpenTradeAsync(req);

                Log(result.IsSuccess ? $"✅ {result}" : $"❌ {result}", result.IsSuccess ? C_GREEN : C_RED);
                AddHistoryRow(req, result);
            }
            catch (JsonException ex)
            {
                Log($"❌ JSON parse error: {ex.Message}", C_RED);
            }
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
            catch { Log("❌ Cannot format — invalid JSON", C_RED); }
        }

        // ══════════════════════════════════════════════════════════
        //  AUTO BOT
        // ══════════════════════════════════════════════════════════

        private async Task StartBotAsync()
        {
            if (!AssertConnected()) return;

            _cfg.Bot = ReadBotConfigFromUI();
            await _settings.SaveAsync(_cfg);

            await (_bot?.DisposeAsync() ?? ValueTask.CompletedTask);

            _bot = new AutoBotService(_bridge!, _cfg.Bot);
            _bot.OnLog += msg => Log(msg);
            _bot.OnTradeExecuted += r =>
            {
                Log(r.IsSuccess ? $"🤖 Bot trade: {r}" : $"🤖 Bot rejected: {r.ErrorMessage}",
                    r.IsSuccess ? C_GREEN : C_RED);
            };
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

        // ══════════════════════════════════════════════════════════
        //  POSITIONS
        // ══════════════════════════════════════════════════════════

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
                    _gridPos.Rows[i].DefaultCellStyle.ForeColor =
                        p.Profit >= 0 ? C_GREEN : C_RED;
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
                Log(ok ? $"✅ Closed #{t}" : $"❌ Failed to close #{t}", ok ? C_GREEN : C_RED);
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
            }
            Log($"Closed {count}/{positions.Count} positions.", C_YELLOW);
            await RefreshPositionsAsync();
        }

        // ══════════════════════════════════════════════════════════
        //  REFRESH (timer)
        // ══════════════════════════════════════════════════════════

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

        // ══════════════════════════════════════════════════════════
        //  R:R CALCULATOR (live)
        // ══════════════════════════════════════════════════════════

        private void RecalcRR()
        {
            try
            {
                if (!double.TryParse(_txtSL.Text, out double sl) || sl == 0) return;
                if (!double.TryParse(_txtTP.Text, out double tp) || tp == 0) return;

                double entry = 0;
                double.TryParse(_txtEntry.Text, out entry);

                // Use a rough estimate for market orders
                if (entry == 0)
                    entry = (sl + tp) / 2.0;

                double rr = LotCalculator.RiskRewardRatio(entry, sl, tp);

                double lots = 0.01;
                if (!_chkAutoLot.Checked)
                    double.TryParse(_txtLot.Text, out lots);
                else
                    lots = 0.01; // placeholder

                string sym = _cmbPair.SelectedItem?.ToString() ?? "GBPUSD";
                double risk   = LotCalculator.DollarRisk(lots, entry, sl, sym);
                double profit = LotCalculator.DollarProfit(lots, entry, tp, sym);

                Color rrColor = rr >= 1.5 ? C_GREEN : rr >= 1.0 ? C_YELLOW : C_RED;
                _lblRR.Text = $"R:R  1 : {rr:F2}";
                _lblRR.ForeColor = rrColor;
                _lblDollarRisk.Text   = $"Risk  ${risk:F2}";
                _lblDollarProfit.Text = $"Profit  ${profit:F2}";
            }
            catch { /* parsing incomplete */ }
        }

        // ══════════════════════════════════════════════════════════
        //  UI HELPERS
        // ══════════════════════════════════════════════════════════

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
            });
        }

        private void SetConnectedUI(bool connected)
        {
            UIThread(() =>
            {
                _pnlDot.BackColor = connected ? C_GREEN : C_RED;
                _lblConnStatus.Text = connected ? "Connected" : "Disconnected";
                _lblConnStatus.ForeColor = connected ? C_GREEN : C_RED;
                _btnDisconnect.Enabled = connected;
                if (!connected) _refreshTimer.Stop();
            });
        }

        private void UpdateBotBadge(bool running)
        {
            UIThread(() =>
            {
                _lblBotBadge.Text = running ? "● BOT RUNNING" : "● BOT STOPPED";
                _lblBotBadge.ForeColor = running ? C_GREEN : C_RED;
                _btnStartBot.Enabled = !running;
                _btnStopBot.Enabled = running;
            });
        }

        private void UpdateBuySellColors()
        {
            bool buy = _cmbDir.SelectedItem?.ToString() == "BUY";
            _btnBuy.BackColor  = buy ? C_GREEN : Color.FromArgb(45, 45, 60);
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
            });
        }

        private void LoadHistoryFromCsv()
        {
            using var d = new OpenFileDialog { Filter = "CSV files|*.csv|All|*.*" };
            if (d.ShowDialog() != DialogResult.OK) return;

            _gridHistory.Rows.Clear();
            foreach (var line in File.ReadLines(d.FileName).Skip(1))
            {
                var parts = line.Split(',');
                if (parts.Length >= 12)
                    _gridHistory.Rows.Add(parts[0], parts[1], parts[2], parts[3],
                        parts[4], parts[5], parts[6], parts[7],
                        parts[8], parts[9], parts[10], parts[11]);
            }
        }

        private void ApplySettingsToUI()
        {
            _cmbMode.SelectedIndex = _cfg.Mt5.Mode == ConnectionMode.NamedPipe ? 0 : 1;
            _txtPipeName.Text = _cfg.Mt5.PipeName;
            _txtWatchFolder.Text = _cfg.Bot.WatchFolder;
            _nudRisk.Value = (decimal)_cfg.Bot.MaxRiskPercent;
            _nudMaxTrades.Value = _cfg.Bot.MaxTradesPerDay;
            _nudPollMs.Value = _cfg.Bot.PollIntervalMs;
            _txtAllowedPairs.Text = string.Join(",", _cfg.Bot.AllowedPairs);
            _nudMinRR.Value = (decimal)_cfg.Bot.MinRRRatio;
            _nudDrawdownPct.Value = (decimal)_cfg.Bot.EmergencyCloseDrawdownPct;
            _nudRetry.Value = _cfg.Bot.RetryCount;
        }

        private BotConfig ReadBotConfigFromUI() => new()
        {
            Enabled = true,
            WatchFolder = _txtWatchFolder.Text,
            MaxRiskPercent = (double)_nudRisk.Value,
            MaxTradesPerDay = (int)_nudMaxTrades.Value,
            PollIntervalMs = (int)_nudPollMs.Value,
            AllowedPairs = [.. _txtAllowedPairs.Text.Split(',').Select(p => p.Trim().ToUpper())],
            AutoLotCalculation = _chkAutoLotBot.Checked,
            MinRRRatio = (double)_nudMinRR.Value,
            EnforceRR = _chkEnforceRR.Checked,
            DrawdownProtectionEnabled = _chkDrawdown.Checked,
            EmergencyCloseDrawdownPct = (double)_nudDrawdownPct.Value,
            RetryOnFail = true,
            RetryCount = (int)_nudRetry.Value,
            RetryDelayMs = 1000,
            AutoStartOnLaunch = _chkAutoStart.Checked,
            MagicNumber = 999001
        };

        // ── Log ───────────────────────────────────────────────────

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

        // ── Util ──────────────────────────────────────────────────

        private void UIThread(Action a)
        {
            if (InvokeRequired) Invoke(a);
            else a();
        }

        private bool AssertConnected()
        {
            if (_bridge?.IsConnected == true) return true;
            Log("❌ Not connected to MT5. Click Connect first.", C_RED);
            return false;
        }

        private static bool Confirm(string msg) =>
            MessageBox.Show(msg, "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
                == DialogResult.Yes;

        private static void SetBtnState(Button btn, bool enabled)
        {
            if (btn.InvokeRequired) btn.Invoke(() => btn.Enabled = enabled);
            else btn.Enabled = enabled;
        }

        private async void OnFormClosingAsync(object? sender, FormClosingEventArgs e)
        {
            _refreshTimer.Stop();
            await StopBotAsync();
            await _settings.SaveAsync(_cfg);
            _bridge?.Dispose();
            //Log.CloseAndFlush();
        }

        // ══════════════════════════════════════════════════════════
        //  FACTORY CONTROLS  (keeps BuildXxx clean)
        // ══════════════════════════════════════════════════════════

        private static TabPage Tab(string title)
            => new(title) { BackColor = Color.FromArgb(18, 18, 26) };

        private static Panel Card(int x, int y, int w, int h)
        {
            var p = new Panel
            {
                Location = new Point(x, y), Size = new Size(w, h),
                BackColor = Color.FromArgb(24, 25, 38),
                BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(8)
            };
            return p;
        }

        private static Label CardH(string text, int x, int y) =>
            new() { Text = text, Location = new Point(x, y), AutoSize = true,
                Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(200, 210, 240) };

        private static Label Lbl(string text, int x, int y,
            Font? font = null, Color? fg = null)
            => new() { Text = text, Location = new Point(x, y), AutoSize = true,
                Font = font ?? new Font("Segoe UI", 9f),
                ForeColor = fg ?? Color.FromArgb(218, 218, 230) };

        private Label MkRedLbl(string t, int x, int y) =>
            new() { Text = t, Location = new Point(x, y), AutoSize = true,
                ForeColor = C_RED, Font = new Font("Segoe UI", 9f) };

        private Label MkGreenLbl(string t, int x, int y) =>
            new() { Text = t, Location = new Point(x, y), AutoSize = true,
                ForeColor = C_GREEN, Font = new Font("Segoe UI", 9f) };

        private TextBox Txt(string text, int x, int y, int w) =>
            new() { Text = text, Location = new Point(x, y), Width = w,
                BackColor = C_SURFACE, ForeColor = C_TEXT,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9f) };

        private ComboBox Cmb(string[] items, int x, int y, int w)
        {
            var c = new ComboBox
            {
                Location = new Point(x, y), Width = w,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = C_SURFACE, ForeColor = C_TEXT,
                Font = new Font("Segoe UI", 9f)
            };
            c.Items.AddRange(items);
            if (items.Length > 0) c.SelectedIndex = 0;
            return c;
        }

        private static Button Btn(string text, int x, int y, int w, Color bg)
        {
            var b = new Button
            {
                Text = text, Location = new Point(x, y), Width = w, Height = 30,
                BackColor = bg, ForeColor = Color.FromArgb(10, 10, 20),
                FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
                Font = new Font("Segoe UI Semibold", 9f)
            };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }

        private NumericUpDown Nud(decimal min, decimal max, decimal val,
            int decimals, decimal inc, int w)
            => new()
            {
                Minimum = min, Maximum = max, Value = val,
                DecimalPlaces = decimals, Increment = inc, Width = w,
                BackColor = C_SURFACE, ForeColor = C_TEXT,
                BorderStyle = BorderStyle.None
            };

        private static DataGridView MkGrid()
        {
            var g = new DataGridView
            {
                BackgroundColor = Color.FromArgb(24, 25, 38),
                GridColor = Color.FromArgb(45, 45, 65),
                ForeColor = Color.FromArgb(218, 218, 230),
                Font = new Font("Consolas", 8.5f),
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false, ReadOnly = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false, AllowUserToAddRows = false,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None
            };
            g.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(18, 18, 28);
            g.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(110, 110, 140);
            g.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            g.DefaultCellStyle.BackColor = Color.FromArgb(24, 25, 38);
            g.DefaultCellStyle.SelectionBackColor = Color.FromArgb(40, 99, 179, 237);
            g.DefaultCellStyle.SelectionForeColor = Color.FromArgb(218, 218, 230);
            g.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(20, 20, 30);
            return g;
        }

        private static void RoundPanel(Panel p) =>
            p.Region = System.Drawing.Region.FromHrgn(
                CreateRoundRectRgn(0, 0, p.Width, p.Height, p.Width, p.Height));

        private void DrawTabItem(object? sender, DrawItemEventArgs e)
        {
            if (sender is not TabControl tc) return;
            var tab = tc.TabPages[e.Index];
            bool sel = e.Index == tc.SelectedIndex;
            using var brush = new SolidBrush(sel ? C_CARD : C_SURFACE);
            e.Graphics.FillRectangle(brush, e.Bounds);
            using var tb = new SolidBrush(sel ? C_ACCENT : C_MUTED);
            e.Graphics.DrawString(tab.Text, tc.Font, tb,
                e.Bounds.Left + 4, e.Bounds.Top + 6);
        }

        [System.Runtime.InteropServices.DllImport("Gdi32.dll")]
        private static extern IntPtr CreateRoundRectRgn(int x1,int y1,int x2,int y2,int cx,int cy);

        // ══════════════════════════════════════════════════════════
        //  SAMPLE DATA
        // ══════════════════════════════════════════════════════════

        private static string DefaultJsonSample() =>
            JsonConvert.SerializeObject(new TradeRequest
            {
                Pair = "GBPUSD", TradeType = TradeType.BUY,
                OrderType = OrderType.MARKET, EntryPrice = 0,
                StopLoss = 1.34750, TakeProfit = 1.35200,
                TakeProfit2 = 1.35500, LotSize = 0.01,
                Comment = "BotSignal", MagicNumber = 999001,
                MoveSLToBreakevenAfterTP1 = true
            }, Formatting.Indented);

        private static string BotHelpText() => """
            TOTAL AUTOMATION — HOW IT WORKS
            ─────────────────────────────────────

            1. Connect the app to MT5 (Named Pipe)
            2. Start the bot → it watches your folder
            3. Drop a .json file into the folder

            The bot then:
              ✓ Reads and validates the JSON
              ✓ Checks: pair allowed, daily limit,
                R:R ratio, free margin, equity
              ✓ Auto-calculates lot size from risk %
              ✓ Sends trade to MT5 via named pipe
              ✓ Retries on failure (configurable)
              ✓ Moves file to /executed or /rejected
              ✓ Logs to trade_history.csv

            Every 2 seconds the bot also:
              ✓ Checks SL → breakeven (at 60% TP)
              ✓ Monitors drawdown → emergency close
              ✓ Polls folder (watcher backup)

            SIGNAL FOLDERS:
            ─────────────────────────────────────
            signals/              ← drop files here
            signals/executed/     ← success
            signals/rejected/     ← validation fail
            signals/error/        ← bad JSON
            signals/trade_history.csv ← full log

            SAMPLE JSON FILE:
            ─────────────────────────────────────
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
            ─────────────────────────────────────
            • MT5 running with TradingBotEA.ex5
            • AutoTrading ON (green button in MT5)
            • Pipe name matches in both apps
            """;
    }
}
