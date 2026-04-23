using MT5TradingBot.Models;

namespace MT5TradingBot.UI
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
                components.Dispose();
            base.Dispose(disposing);
        }

        // ── Header ────────────────────────────────────────────────
        private Panel _pnlHeader = null!;
        private Label _lblTitle = null!;
        private Panel _pnlDot = null!;
        private Label _lblConnStatus = null!;
        private Label _lblTime = null!;
        private System.Windows.Forms.Timer _clockTimer = null!;

        // ── Connection bar ────────────────────────────────────────
        private Panel _pnlConnBar = null!;
        private ComboBox _cmbMode = null!;
        private Label _lblPipeLabel = null!;
        private TextBox _txtPipeName = null!;
        private Button _btnConnect = null!;
        private Button _btnDisconnect = null!;
        private CheckBox _chkAutoConn = null!;

        // ── Account bar ───────────────────────────────────────────
        private Panel _pnlAccountBar = null!;
        private Label _lblAccNum = null!;
        private Label _lblBalance = null!;
        private Label _lblEquity = null!;
        private Label _lblFreeMargin = null!;
        private Label _lblPnl = null!;
        private Label _lblMarginLvl = null!;

        // ── Layout + tabs ─────────────────────────────────────────
        private TableLayoutPanel _mainLayout = null!;
        private TabControl _tabControl = null!;
        private TabPage _tabTrade = null!;
        private TabPage _tabPositions = null!;
        private TabPage _tabHistory = null!;
        private TabPage _tabBot = null!;
        private TabPage _tabClaude = null!;
        private TabPage _tabLog = null!;

        // ── Trade tab ─────────────────────────────────────────────
        private Panel _pnlTradeLeft = null!;
        private Label _lblTradeHeader = null!;
        private Label _lblPairLabel = null!;
        private ComboBox _cmbPair = null!;
        private Label _lblDirLabel = null!;
        private ComboBox _cmbDir = null!;
        private Label _lblOrderTypeLabel = null!;
        private ComboBox _cmbOrderType = null!;
        private Label _lblEntryLabel = null!;
        private TextBox _txtEntry = null!;
        private Label _lblSLLabel = null!;
        private TextBox _txtSL = null!;
        private Label _lblTPLabel = null!;
        private TextBox _txtTP = null!;
        private Label _lblTP2Label = null!;
        private TextBox _txtTP2 = null!;
        private CheckBox _chkAutoLot = null!;
        private Label _lblLotLabel = null!;
        private TextBox _txtLot = null!;
        private CheckBox _chkMoveSLBE = null!;
        private Panel _pnlRR = null!;
        private Label _lblRR = null!;
        private Label _lblDollarRisk = null!;
        private Label _lblDollarProfit = null!;
        private Button _btnBuy = null!;
        private Button _btnSell = null!;
        private Panel _pnlTradeRight = null!;
        private Label _lblJsonHeader = null!;
        private RichTextBox _txtJson = null!;
        private Button _btnJsonLoad = null!;
        private Button _btnJsonExec = null!;
        private Button _btnJsonFmt = null!;
        private Button _btnJsonSample = null!;

        // ── Positions tab ─────────────────────────────────────────
        private DataGridView _gridPos = null!;
        private Button _btnClosePos = null!;
        private Button _btnCloseAllPos = null!;
        private Button _btnRefreshPos = null!;

        // ── History tab ───────────────────────────────────────────
        private DataGridView _gridHistory = null!;
        private Button _btnImportHistory = null!;
        private Button _btnClearHistory = null!;

        // ── Bot tab ───────────────────────────────────────────────
        private Label _lblBotBadge = null!;
        private Panel _pnlBotCard = null!;
        private Label _lblBotCardHeader = null!;
        private Label _lblWatchFolderLabel = null!;
        private TextBox _txtWatchFolder = null!;
        private Label _lblRiskLabel = null!;
        private NumericUpDown _nudRisk = null!;
        private Label _lblMinRRLabel = null!;
        private NumericUpDown _nudMinRR = null!;
        private Label _lblMaxTradesLabel = null!;
        private NumericUpDown _nudMaxTrades = null!;
        private Label _lblPollMsLabel = null!;
        private NumericUpDown _nudPollMs = null!;
        private Label _lblRetryLabel = null!;
        private NumericUpDown _nudRetry = null!;
        private Label _lblAllowedPairsLabel = null!;
        private TextBox _txtAllowedPairs = null!;
        private Label _lblDrawdownLabel = null!;
        private NumericUpDown _nudDrawdownPct = null!;
        private CheckBox _chkAutoLotBot = null!;
        private CheckBox _chkEnforceRR = null!;
        private CheckBox _chkDrawdown = null!;
        private CheckBox _chkAutoStart = null!;
        private Button _btnStartBot = null!;
        private Button _btnStopBot = null!;
        private Panel _pnlBotInfo = null!;
        private Label _lblBotInfoHeader = null!;
        private RichTextBox _rtbBotHelp = null!;
        private Button _btnOpenFolder = null!;

        // ── Claude AI tab ─────────────────────────────────────────
        private Label _lblClaudeBadge = null!;
        private Panel _pnlClaudeCard = null!;
        private Label _lblClaudeCardHeader = null!;
        private Label _lblApiKeyLabel = null!;
        private TextBox _txtClaudeApiKey = null!;
        private Label _lblModelLabel = null!;
        private Label _lblModelValue = null!;
        private Label _lblSymbolsLabel = null!;
        private TextBox _txtClaudeSymbols = null!;
        private Label _lblPollSecLabel = null!;
        private NumericUpDown _nudClaudePollSec = null!;
        private Label _lblClaudeNote1 = null!;
        private Label _lblClaudeNote2 = null!;
        private Button _btnStartClaude = null!;
        private Button _btnStopClaude = null!;
        private Panel _pnlClaudePromptCard = null!;
        private Label _lblPromptHeader = null!;
        private RichTextBox _txtClaudePrompt = null!;
        private Button _btnResetPrompt = null!;

        // ── Log tab ───────────────────────────────────────────────
        private RichTextBox _txtLog = null!;
        private Button _btnClearLog = null!;
        private Button _btnSaveLog = null!;

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            SuspendLayout();

            // ── Clock timer ───────────────────────────────────────
            _clockTimer = new System.Windows.Forms.Timer(components) { Interval = 1000 };
            _clockTimer.Tick += (_, _) =>
                _lblTime.Text = $"UTC {DateTime.UtcNow:HH:mm:ss}  |  Local {DateTime.Now:HH:mm:ss}";

            // ── Header ────────────────────────────────────────────
            _pnlHeader = new Panel { Dock = DockStyle.Fill, BackColor = C_SURFACE };

            _lblTitle = new Label
            {
                Text = "⚡  MT5 Trading Bot", Location = new Point(14, 14), AutoSize = true,
                Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold), ForeColor = C_ACCENT
            };

            _pnlDot = new Panel { Size = new Size(10, 10), BackColor = C_RED, Location = new Point(230, 21) };
            _pnlDot.Region = System.Drawing.Region.FromHrgn(
                CreateRoundRectRgn(0, 0, _pnlDot.Width, _pnlDot.Height, _pnlDot.Width, _pnlDot.Height));

            _lblConnStatus = new Label
            {
                Text = "Disconnected", Location = new Point(246, 18), AutoSize = true,
                ForeColor = C_RED, Font = new Font("Segoe UI", 9f)
            };
            _lblTime = new Label
            {
                Text = "", Location = new Point(900, 18), AutoSize = true,
                Font = new Font("Consolas", 8.5f), ForeColor = C_MUTED
            };
            _pnlHeader.Controls.AddRange(new Control[] { _lblTitle, _pnlDot, _lblConnStatus, _lblTime });

            // ── Connection bar ────────────────────────────────────
            _pnlConnBar = new Panel { Dock = DockStyle.Fill, BackColor = C_CARD };

            _cmbMode = new ComboBox
            {
                Location = new Point(10, 8), Width = 180,
                DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat,
                BackColor = C_SURFACE, ForeColor = C_TEXT, Font = new Font("Segoe UI", 9f)
            };
            _cmbMode.Items.AddRange(new object[] { "Named Pipe (Local MT5)", "TCP Socket (Remote)" });
            _cmbMode.SelectedIndex = 0;

            _lblPipeLabel = new Label
            {
                Text = "Pipe/Host:", Location = new Point(200, 12), AutoSize = true,
                ForeColor = C_MUTED, Font = new Font("Segoe UI", 9f)
            };
            _txtPipeName = new TextBox
            {
                Text = "MT5TradingBotPipe", Location = new Point(270, 7), Width = 220,
                BackColor = C_SURFACE, ForeColor = C_TEXT,
                BorderStyle = BorderStyle.FixedSingle, Font = new Font("Consolas", 9f)
            };

            _btnConnect = MkBtn("⚡ Connect", 502, 7, 110, C_GREEN);
            _btnConnect.Click += async (_, _) => await ConnectAsync();

            _btnDisconnect = MkBtn("Disconnect", 622, 7, 100, C_RED);
            _btnDisconnect.Enabled = false;
            _btnDisconnect.Click += async (_, _) => await DisconnectAsync();

            _chkAutoConn = new CheckBox
            {
                Text = "Auto-connect on launch", ForeColor = C_MUTED,
                Location = new Point(734, 10), AutoSize = true
            };
            _chkAutoConn.CheckedChanged += (_, _) => _cfg.AutoConnectOnLaunch = _chkAutoConn.Checked;

            _pnlConnBar.Controls.AddRange(new Control[] {
                _cmbMode, _lblPipeLabel, _txtPipeName, _btnConnect, _btnDisconnect, _chkAutoConn });

            // ── Account bar ───────────────────────────────────────
            _pnlAccountBar = new Panel { Dock = DockStyle.Fill, BackColor = C_SURFACE };
            _lblAccNum     = MkLbl("Account: —",    8,   12, C_MUTED);
            _lblBalance    = MkLbl("Balance: —",    170, 12, C_TEXT);
            _lblEquity     = MkLbl("Equity: —",     300, 12, C_TEXT);
            _lblFreeMargin = MkLbl("Free Margin: —",430, 12, C_TEXT);
            _lblPnl        = MkLbl("P&L: —",        580, 12, C_TEXT);
            _lblMarginLvl  = MkLbl("Margin Lvl: —", 680, 12, C_MUTED);
            _pnlAccountBar.Controls.AddRange(new Control[] {
                _lblAccNum, _lblBalance, _lblEquity, _lblFreeMargin, _lblPnl, _lblMarginLvl });

            // ── Tab control ───────────────────────────────────────
            _tabControl = new TabControl
            {
                Dock = DockStyle.Fill, DrawMode = TabDrawMode.OwnerDrawFixed,
                Padding = new Point(14, 6), BackColor = C_BG
            };
            _tabControl.DrawItem += DrawTabItem;

            // ═════════════════════════════════════════════════════
            //  TRADE TAB
            // ═════════════════════════════════════════════════════
            _tabTrade = MkTab("  📈 Trade  ");

            _pnlTradeLeft = MkCard(12, 8, 370, 680);
            _lblTradeHeader = MkCardH("Manual Trade Entry", 14, 14);
            _lblPairLabel      = MkLbl("Pair",        14, 50,  C_MUTED);
            _cmbPair = MkCmb(new[] { "GBPUSD","EURUSD","USDJPY","XAUUSD","USDCAD","AUDUSD","EURGBP" }, 130, 48, 210);
            _cmbPair.SelectedIndexChanged += (_, _) => RecalcRR();

            _lblDirLabel       = MkLbl("Direction",   14, 82,  C_MUTED);
            _cmbDir = MkCmb(new[] { "BUY", "SELL" }, 130, 80, 210);
            _cmbDir.SelectedIndexChanged += (_, _) => { UpdateBuySellColors(); RecalcRR(); };

            _lblOrderTypeLabel = MkLbl("Order Type",  14, 114, C_MUTED);
            _cmbOrderType = MkCmb(new[] { "MARKET", "LIMIT", "STOP" }, 130, 112, 210);
            _cmbOrderType.SelectedIndexChanged += (_, _) =>
                _txtEntry.Enabled = _cmbOrderType.SelectedIndex != 0;

            _lblEntryLabel = MkLbl("Entry Price",  14, 146, C_MUTED);
            _txtEntry = MkTxt("0 (market)", 130, 144, 210); _txtEntry.Enabled = false;
            _txtEntry.TextChanged += (_, _) => RecalcRR();

            _lblSLLabel = MkLbl("Stop Loss ✱",   14, 178, C_RED);
            _txtSL = MkTxt("e.g. 1.34750", 130, 176, 210);
            _txtSL.TextChanged += (_, _) => RecalcRR();

            _lblTPLabel = MkLbl("Take Profit ✱", 14, 210, C_GREEN);
            _txtTP = MkTxt("e.g. 1.35200", 130, 208, 210);
            _txtTP.TextChanged += (_, _) => RecalcRR();

            _lblTP2Label = MkLbl("Take Profit 2", 14, 242, C_ACCENT);
            _txtTP2 = MkTxt("0 (optional)", 130, 240, 210);

            _chkAutoLot = new CheckBox
            {
                Text = "Auto lot size (1% risk)", ForeColor = C_YELLOW, Checked = true,
                Location = new Point(14, 274), AutoSize = true
            };
            _chkAutoLot.CheckedChanged += (_, _) => { _txtLot.Enabled = !_chkAutoLot.Checked; RecalcRR(); };

            _lblLotLabel = MkLbl("Lot Size", 14, 302, C_MUTED);
            _txtLot = MkTxt("0.01", 130, 300, 100); _txtLot.Enabled = false;
            _txtLot.TextChanged += (_, _) => RecalcRR();

            _chkMoveSLBE = new CheckBox
            {
                Text = "Move SL → Breakeven after TP1", ForeColor = C_ACCENT,
                Location = new Point(14, 334), AutoSize = true, Checked = true
            };

            _pnlRR = new Panel
            {
                Location = new Point(14, 364), Size = new Size(326, 52),
                BackColor = Color.FromArgb(20, 99, 179, 237), BorderStyle = BorderStyle.None
            };
            _lblRR           = new Label { Text = "R:R  —",     Location = new Point(10, 8),  AutoSize = true, Font = new Font("Consolas", 9f),   ForeColor = C_ACCENT };
            _lblDollarRisk   = new Label { Text = "Risk  $—",   Location = new Point(10, 26), AutoSize = true, Font = new Font("Consolas", 8.5f), ForeColor = C_RED };
            _lblDollarProfit = new Label { Text = "Profit  $—", Location = new Point(170,26), AutoSize = true, Font = new Font("Consolas", 8.5f), ForeColor = C_GREEN };
            _pnlRR.Controls.AddRange(new Control[] { _lblRR, _lblDollarRisk, _lblDollarProfit });

            _btnBuy  = MkBtn("▲  BUY",  14,  424, 152, C_GREEN); _btnBuy.Height  = 44; _btnBuy.Font  = new Font("Segoe UI Semibold", 11f);
            _btnSell = MkBtn("▼  SELL", 174, 424, 152, C_RED);   _btnSell.Height = 44; _btnSell.Font = new Font("Segoe UI Semibold", 11f);
            _btnBuy.Click  += async (_, _) => await SubmitTradeAsync(TradeType.BUY);
            _btnSell.Click += async (_, _) => await SubmitTradeAsync(TradeType.SELL);

            _pnlTradeLeft.Controls.AddRange(new Control[] {
                _lblTradeHeader,
                _lblPairLabel, _cmbPair, _lblDirLabel, _cmbDir,
                _lblOrderTypeLabel, _cmbOrderType, _lblEntryLabel, _txtEntry,
                _lblSLLabel, _txtSL, _lblTPLabel, _txtTP, _lblTP2Label, _txtTP2,
                _chkAutoLot, _lblLotLabel, _txtLot, _chkMoveSLBE,
                _pnlRR, _btnBuy, _btnSell });

            _pnlTradeRight = MkCard(392, 8, 838, 680);
            _lblJsonHeader = MkCardH("JSON Signal Input  (paste, load file, or drop)", 14, 14);
            _txtJson = new RichTextBox
            {
                Location = new Point(14, 50), Size = new Size(808, 540),
                BackColor = Color.FromArgb(14, 14, 22), ForeColor = Color.FromArgb(200, 220, 180),
                Font = new Font("Consolas", 9.5f), BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.Vertical, AllowDrop = true
            };
            _txtJson.DragEnter += (_, e) =>
                e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true
                    ? DragDropEffects.Copy : DragDropEffects.None;
            _txtJson.DragDrop += (_, e) =>
            {
                if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                    _txtJson.Text = File.ReadAllText(files[0]);
            };

            _btnJsonLoad   = MkBtn("📂 Load File",    14,  600, 130, C_ACCENT);
            _btnJsonExec   = MkBtn("⚡ Execute JSON", 154, 600, 140, C_GREEN);
            _btnJsonFmt    = MkBtn("🔧 Format",       304, 600, 100, C_YELLOW);
            _btnJsonSample = MkBtn("📋 Sample",       414, 600, 100, C_MUTED);
            _btnJsonLoad.Click   += (_, _) => LoadJsonFile();
            _btnJsonExec.Click   += async (_, _) => await ExecuteJsonAsync();
            _btnJsonFmt.Click    += (_, _) => FormatJson();
            _btnJsonSample.Click += (_, _) => _txtJson.Text = DefaultJsonSample();

            _pnlTradeRight.Controls.AddRange(new Control[] {
                _lblJsonHeader, _txtJson,
                _btnJsonLoad, _btnJsonExec, _btnJsonFmt, _btnJsonSample });

            _tabTrade.Controls.AddRange(new Control[] { _pnlTradeLeft, _pnlTradeRight });

            // ═════════════════════════════════════════════════════
            //  POSITIONS TAB
            // ═════════════════════════════════════════════════════
            _tabPositions = MkTab("  📊 Positions  ");
            _gridPos = MkGrid();
            _gridPos.Location = new Point(12, 8);
            _gridPos.Size = new Size(1220, 580);
            foreach (var c in new[] { "Ticket","Symbol","Type","Lots","Open","Current","SL","TP","P&L ($)","Pips","Time","Comment" })
                _gridPos.Columns.Add(c.Replace(" ","_"), c);

            _btnClosePos    = MkBtn("Close Selected", 12,  600, 150, C_RED);
            _btnCloseAllPos = MkBtn("Close ALL",      172, 600, 130, Color.FromArgb(160,50,50));
            _btnRefreshPos  = MkBtn("🔄 Refresh",     312, 600, 110, C_ACCENT);
            _btnClosePos.Click    += async (_, _) => await CloseSelectedAsync();
            _btnCloseAllPos.Click += async (_, _) => await CloseAllAsync();
            _btnRefreshPos.Click  += async (_, _) => await RefreshPositionsAsync();
            _tabPositions.Controls.AddRange(new Control[] { _gridPos, _btnClosePos, _btnCloseAllPos, _btnRefreshPos });

            // ═════════════════════════════════════════════════════
            //  HISTORY TAB
            // ═════════════════════════════════════════════════════
            _tabHistory = MkTab("  🗂 History  ");
            _gridHistory = MkGrid();
            _gridHistory.Location = new Point(12, 8);
            _gridHistory.Size = new Size(1220, 580);
            foreach (var c in new[] { "Time","Id","Pair","Dir","Lots","Entry","SL","TP","Ticket","Status","Exec Price","Error" })
                _gridHistory.Columns.Add(c.Replace(" ","_"), c);

            _btnImportHistory = MkBtn("📂 Load CSV Log", 12,  600, 150, C_ACCENT);
            _btnClearHistory  = MkBtn("Clear",           172, 600, 80,  C_MUTED);
            _btnImportHistory.Click += (_, _) => LoadHistoryFromCsv();
            _btnClearHistory.Click  += (_, _) => _gridHistory.Rows.Clear();
            _tabHistory.Controls.AddRange(new Control[] { _gridHistory, _btnImportHistory, _btnClearHistory });

            // ═════════════════════════════════════════════════════
            //  BOT TAB
            // ═════════════════════════════════════════════════════
            _tabBot = MkTab("  🤖 Auto Bot  ");

            _lblBotBadge = new Label
            {
                Text = "● BOT STOPPED", ForeColor = C_RED,
                Font = new Font("Segoe UI Semibold", 12f), Location = new Point(14, 14), AutoSize = true
            };

            _pnlBotCard = MkCard(14, 50, 560, 620);
            _lblBotCardHeader    = MkCardH("Bot Configuration", 14, 14);
            _lblWatchFolderLabel = MkLbl("Watch Folder",     14, 50,  C_MUTED);
            _txtWatchFolder = MkTxt(@"C:\MT5Bot\signals",   200, 48,  330);
            _lblRiskLabel        = MkLbl("Max Risk %",       14, 82,  C_MUTED);
            _nudRisk        = MkNud(0.1m, 10m,    1m,   1, 0.1m, 100, 200, 80);
            _lblMinRRLabel       = MkLbl("Min R:R Ratio",    14, 114, C_MUTED);
            _nudMinRR       = MkNud(0.5m, 10m,    1.5m, 1, 0.1m, 100, 200, 112);
            _lblMaxTradesLabel   = MkLbl("Max Trades/Day",   14, 146, C_MUTED);
            _nudMaxTrades   = MkNud(1,    100,    5,    0, 1,    100, 200, 144);
            _lblPollMsLabel      = MkLbl("Poll Interval ms", 14, 178, C_MUTED);
            _nudPollMs      = MkNud(500,  30000,  2000, 0, 500,  120, 200, 176);
            _lblRetryLabel       = MkLbl("Retry Count",      14, 210, C_MUTED);
            _nudRetry       = MkNud(0,    10,     3,    0, 1,    100, 200, 208);
            _lblAllowedPairsLabel= MkLbl("Allowed Pairs",    14, 242, C_MUTED);
            _txtAllowedPairs= MkTxt("GBPUSD,EURUSD,USDJPY", 200, 240, 330);
            _lblDrawdownLabel    = MkLbl("Drawdown Stop %",  14, 274, C_MUTED);
            _nudDrawdownPct = MkNud(1,    50,     10,   1, 1,    100, 200, 272);

            _chkAutoLotBot = new CheckBox { Text = "Auto lot calculation from equity",         ForeColor = C_MUTED, Checked = true,  Location = new Point(14, 314), AutoSize = true };
            _chkEnforceRR  = new CheckBox { Text = "Enforce minimum R:R (reject if below)",    ForeColor = C_MUTED, Checked = true,  Location = new Point(14, 340), AutoSize = true };
            _chkDrawdown   = new CheckBox { Text = "Drawdown protection (emergency close)",     ForeColor = C_MUTED, Checked = true,  Location = new Point(14, 366), AutoSize = true };
            _chkAutoStart  = new CheckBox { Text = "Auto-start bot on app launch",             ForeColor = C_MUTED, Checked = false, Location = new Point(14, 392), AutoSize = true };

            _btnStartBot = MkBtn("▶  Start Bot", 14,  430, 160, C_GREEN); _btnStartBot.Height = 42; _btnStartBot.Font = new Font("Segoe UI Semibold", 10f);
            _btnStopBot  = MkBtn("■  Stop Bot",  184, 430, 160, C_RED);   _btnStopBot.Height  = 42; _btnStopBot.Font  = new Font("Segoe UI Semibold", 10f);
            _btnStopBot.Enabled = false;
            _btnStartBot.Click += async (_, _) => await StartBotAsync();
            _btnStopBot.Click  += async (_, _) => await StopBotAsync();

            _pnlBotCard.Controls.AddRange(new Control[] {
                _lblBotCardHeader,
                _lblWatchFolderLabel, _txtWatchFolder,
                _lblRiskLabel, _nudRisk, _lblMinRRLabel, _nudMinRR,
                _lblMaxTradesLabel, _nudMaxTrades, _lblPollMsLabel, _nudPollMs,
                _lblRetryLabel, _nudRetry, _lblAllowedPairsLabel, _txtAllowedPairs,
                _lblDrawdownLabel, _nudDrawdownPct,
                _chkAutoLotBot, _chkEnforceRR, _chkDrawdown, _chkAutoStart,
                _btnStartBot, _btnStopBot });

            _pnlBotInfo = MkCard(590, 50, 640, 620);
            _lblBotInfoHeader = MkCardH("How It Works", 14, 14);
            _rtbBotHelp = new RichTextBox
            {
                Location = new Point(14, 50), Size = new Size(608, 550),
                BackColor = C_CARD, ForeColor = C_MUTED, BorderStyle = BorderStyle.None,
                ReadOnly = true, Font = new Font("Consolas", 9f),
                Text = BotHelpText()
            };
            _pnlBotInfo.Controls.AddRange(new Control[] { _lblBotInfoHeader, _rtbBotHelp });

            _btnOpenFolder = MkBtn("📁 Open Folder", 14, 680, 140, C_ACCENT);
            _btnOpenFolder.Click += (_, _) =>
            {
                Directory.CreateDirectory(_txtWatchFolder.Text);
                System.Diagnostics.Process.Start("explorer.exe", _txtWatchFolder.Text);
            };

            _tabBot.Controls.AddRange(new Control[] { _lblBotBadge, _pnlBotCard, _pnlBotInfo, _btnOpenFolder });

            // ═════════════════════════════════════════════════════
            //  CLAUDE AI TAB
            // ═════════════════════════════════════════════════════
            _tabClaude = MkTab("  🧠 Claude AI  ");

            _lblClaudeBadge = new Label
            {
                Text = "● CLAUDE STOPPED", ForeColor = C_RED,
                Font = new Font("Segoe UI Semibold", 12f), Location = new Point(14, 14), AutoSize = true
            };

            _pnlClaudeCard = MkCard(14, 50, 560, 580);
            _lblClaudeCardHeader = MkCardH("Claude AI Settings", 14, 14);
            _lblApiKeyLabel  = MkLbl("API Key",            14, 50,  C_MUTED);
            _txtClaudeApiKey = MkTxt("sk-ant-...",         210, 48,  320); _txtClaudeApiKey.UseSystemPasswordChar = true;
            _lblModelLabel   = MkLbl("Model",              14, 82,  C_MUTED);
            _lblModelValue   = new Label { Text = "claude-opus-4-7", Location = new Point(210, 80), AutoSize = true, ForeColor = C_ACCENT, Font = new Font("Consolas", 9f) };
            _lblSymbolsLabel = MkLbl("Watch Symbols",      14, 114, C_MUTED);
            _txtClaudeSymbols= MkTxt("GBPUSD,EURUSD,USDJPY", 210, 112, 320);
            _lblPollSecLabel = MkLbl("Poll Interval (sec)", 14, 146, C_MUTED);
            _nudClaudePollSec= MkNud(10, 3600, 60, 0, 10, 120, 210, 144);
            _lblClaudeNote1  = new Label { Text = "⚡ Start the Bot tab first for full validation pipeline.", Location = new Point(14, 194), AutoSize = true, ForeColor = C_MUTED,  Font = new Font("Segoe UI", 8f) };
            _lblClaudeNote2  = new Label { Text = "🔐 API key is saved to settings.json on disk.",           Location = new Point(14, 214), AutoSize = true, ForeColor = C_YELLOW, Font = new Font("Segoe UI", 8f) };

            _btnStartClaude = MkBtn("▶  Start Claude", 14,  244, 180, C_GREEN); _btnStartClaude.Height = 42; _btnStartClaude.Font = new Font("Segoe UI Semibold", 10f);
            _btnStopClaude  = MkBtn("■  Stop Claude",  204, 244, 180, C_RED);   _btnStopClaude.Height  = 42; _btnStopClaude.Font  = new Font("Segoe UI Semibold", 10f);
            _btnStopClaude.Enabled = false;
            _btnStartClaude.Click += async (_, _) => await StartClaudeAsync();
            _btnStopClaude.Click  += async (_, _) => await StopClaudeAsync();

            _pnlClaudeCard.Controls.AddRange(new Control[] {
                _lblClaudeCardHeader,
                _lblApiKeyLabel, _txtClaudeApiKey,
                _lblModelLabel, _lblModelValue,
                _lblSymbolsLabel, _txtClaudeSymbols,
                _lblPollSecLabel, _nudClaudePollSec,
                _lblClaudeNote1, _lblClaudeNote2,
                _btnStartClaude, _btnStopClaude });

            _pnlClaudePromptCard = MkCard(590, 50, 640, 580);
            _lblPromptHeader = MkCardH("System Prompt  (stable — cached by Claude API)", 14, 14);
            _txtClaudePrompt = new RichTextBox
            {
                Location = new Point(14, 48), Size = new Size(608, 486),
                BackColor = Color.FromArgb(14, 14, 22), ForeColor = Color.FromArgb(200, 220, 180),
                Font = new Font("Consolas", 9f), BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                Text = ClaudeConfig.DefaultPrompt
            };
            _btnResetPrompt = MkBtn("↺ Reset Default", 14, 542, 150, C_MUTED);
            _btnResetPrompt.Click += (_, _) => _txtClaudePrompt.Text = ClaudeConfig.DefaultPrompt;
            _pnlClaudePromptCard.Controls.AddRange(new Control[] { _lblPromptHeader, _txtClaudePrompt, _btnResetPrompt });

            _tabClaude.Controls.AddRange(new Control[] { _lblClaudeBadge, _pnlClaudeCard, _pnlClaudePromptCard });

            // ═════════════════════════════════════════════════════
            //  LOG TAB
            // ═════════════════════════════════════════════════════
            _tabLog = MkTab("  📋 Log  ");
            _txtLog = new RichTextBox
            {
                Location = new Point(12, 8), Size = new Size(1222, 640),
                BackColor = Color.FromArgb(12, 12, 18), ForeColor = C_TEXT,
                Font = new Font("Consolas", 9f), ReadOnly = true, BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.Vertical
            };
            _btnClearLog = MkBtn("Clear",    12,  658, 100, C_MUTED);
            _btnSaveLog  = MkBtn("Save Log", 122, 658, 100, C_ACCENT);
            _btnClearLog.Click += (_, _) => _txtLog.Clear();
            _btnSaveLog.Click  += (_, _) =>
            {
                using var d = new SaveFileDialog { Filter = "Text|*.txt", FileName = "MT5Log" };
                if (d.ShowDialog() == DialogResult.OK) File.WriteAllText(d.FileName, _txtLog.Text);
            };
            _tabLog.Controls.AddRange(new Control[] { _txtLog, _btnClearLog, _btnSaveLog });

            // ── Assemble ──────────────────────────────────────────
            _tabControl.TabPages.AddRange(new TabPage[] {
                _tabTrade, _tabPositions, _tabHistory, _tabBot, _tabClaude, _tabLog });

            _mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1 };
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _mainLayout.Controls.Add(_pnlHeader,     0, 0);
            _mainLayout.Controls.Add(_pnlConnBar,    0, 1);
            _mainLayout.Controls.Add(_pnlAccountBar, 0, 2);
            _mainLayout.Controls.Add(_tabControl,    0, 3);

            // ── Form ──────────────────────────────────────────────
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Text = "MT5 Trading Bot — Professional";
            this.Size = new Size(1280, 860);
            this.MinimumSize = new Size(1100, 720);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = C_BG;
            this.ForeColor = C_TEXT;
            this.Font = new Font("Segoe UI", 9f);
            this.DoubleBuffered = true;
            this.FormClosing += OnFormClosingAsync;
            this.Controls.Add(_mainLayout);

            ResumeLayout(false);
        }

        // ── Designer-local factories ──────────────────────────────

        private static TabPage MkTab(string title) =>
            new(title) { BackColor = Color.FromArgb(18, 18, 26) };

        private static Panel MkCard(int x, int y, int w, int h) =>
            new() { Location = new Point(x, y), Size = new Size(w, h),
                BackColor = Color.FromArgb(24, 25, 38),
                BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(8) };

        private static Label MkCardH(string text, int x, int y) =>
            new() { Text = text, Location = new Point(x, y), AutoSize = true,
                Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(200, 210, 240) };

        private static Label MkLbl(string text, int x, int y, Color fg) =>
            new() { Text = text, Location = new Point(x, y), AutoSize = true,
                ForeColor = fg, Font = new Font("Segoe UI", 9f) };

        private static TextBox MkTxt(string text, int x, int y, int w) =>
            new() { Text = text, Location = new Point(x, y), Width = w,
                BackColor = Color.FromArgb(22, 22, 32), ForeColor = Color.FromArgb(218, 218, 230),
                BorderStyle = BorderStyle.FixedSingle, Font = new Font("Consolas", 9f) };

        private static ComboBox MkCmb(string[] items, int x, int y, int w)
        {
            var c = new ComboBox
            {
                Location = new Point(x, y), Width = w,
                DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(22, 22, 32), ForeColor = Color.FromArgb(218, 218, 230),
                Font = new Font("Segoe UI", 9f)
            };
            c.Items.AddRange(items);
            if (items.Length > 0) c.SelectedIndex = 0;
            return c;
        }

        private static Button MkBtn(string text, int x, int y, int w, Color bg)
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

        private static NumericUpDown MkNud(decimal min, decimal max, decimal val,
            int decimals, decimal inc, int w, int x, int y) =>
            new()
            {
                Location = new Point(x, y), Width = w,
                Minimum = min, Maximum = max, Value = val,
                DecimalPlaces = decimals, Increment = inc,
                BackColor = Color.FromArgb(22, 22, 32), ForeColor = Color.FromArgb(218, 218, 230),
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
    }
}
