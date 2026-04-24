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

        // ── Timers ────────────────────────────────────────────────
        private System.Windows.Forms.Timer _clockTimer;

        // ── Header ────────────────────────────────────────────────
        private Panel _pnlHeader;
        private Label _lblTitle;
        private Panel _pnlDot;
        private Label _lblConnStatus;
        private Label _lblTime;

        // ── Connection bar ────────────────────────────────────────
        private Panel _pnlConnBar;
        private ComboBox _cmbMode;
        private Label _lblPipeLabel;
        private TextBox _txtPipeName;
        private Button _btnConnect;
        private Button _btnDisconnect;
        private CheckBox _chkAutoConn;

        // ── Account bar ───────────────────────────────────────────
        private Panel _pnlAccountBar;
        private Label _lblAccNum;
        private Label _lblBalance;
        private Label _lblEquity;
        private Label _lblFreeMargin;
        private Label _lblPnl;
        private Label _lblMarginLvl;

        // ── Tabs ──────────────────────────────────────────────────
        private TabControl _tabControl;
        private TabPage _tabTrade;
        private TabPage _tabPositions;
        private TabPage _tabHistory;
        private TabPage _tabBot;
        private TabPage _tabClaude;
        private TabPage _tabLog;

        // ── Trade tab ─────────────────────────────────────────────
        private Panel _pnlTradeLeft;
        private Label _lblTradeHeader;
        private Label _lblPairLabel;
        private ComboBox _cmbPair;
        private Label _lblDirLabel;
        private ComboBox _cmbDir;
        private Label _lblOrderTypeLabel;
        private ComboBox _cmbOrderType;
        private Label _lblEntryLabel;
        private TextBox _txtEntry;
        private Label _lblSLLabel;
        private TextBox _txtSL;
        private Label _lblTPLabel;
        private TextBox _txtTP;
        private Label _lblTP2Label;
        private TextBox _txtTP2;
        private CheckBox _chkAutoLot;
        private Label _lblLotLabel;
        private TextBox _txtLot;
        private CheckBox _chkMoveSLBE;
        private Panel _pnlRR;
        private Label _lblRR;
        private Label _lblDollarRisk;
        private Label _lblDollarProfit;
        private Button _btnBuy;
        private Button _btnSell;
        private Panel _pnlTradeRight;
        private Label _lblJsonHeader;
        private RichTextBox _txtJson;
        private Button _btnJsonLoad;
        private Button _btnJsonExec;
        private Button _btnJsonFmt;
        private Button _btnJsonSample;

        // ── Positions tab ─────────────────────────────────────────
        private DataGridView _gridPos;
        private Button _btnClosePos;
        private Button _btnCloseAllPos;
        private Button _btnRefreshPos;

        // ── History tab ───────────────────────────────────────────
        private DataGridView _gridHistory;
        private Button _btnImportHistory;
        private Button _btnClearHistory;

        // ── Bot tab ───────────────────────────────────────────────
        private Label _lblBotBadge;
        private Panel _pnlBotCard;
        private Label _lblBotCardHeader;
        private Label _lblWatchFolderLabel;
        private TextBox _txtWatchFolder;
        private Label _lblRiskLabel;
        private NumericUpDown _nudRisk;
        private Label _lblMinRRLabel;
        private NumericUpDown _nudMinRR;
        private Label _lblMaxTradesLabel;
        private NumericUpDown _nudMaxTrades;
        private Label _lblPollMsLabel;
        private NumericUpDown _nudPollMs;
        private Label _lblRetryLabel;
        private NumericUpDown _nudRetry;
        private Label _lblAllowedPairsLabel;
        private TextBox _txtAllowedPairs;
        private Label _lblDrawdownLabel;
        private NumericUpDown _nudDrawdownPct;
        private CheckBox _chkAutoLotBot;
        private CheckBox _chkEnforceRR;
        private CheckBox _chkDrawdown;
        private CheckBox _chkAutoStart;
        private Button _btnStartBot;
        private Button _btnStopBot;
        private Panel _pnlBotInfo;
        private Label _lblBotInfoHeader;
        private RichTextBox _rtbBotHelp;
        private Button _btnOpenFolder;

        // ── Claude AI tab ─────────────────────────────────────────
        private Label _lblClaudeBadge;
        private Panel _pnlClaudeCard;
        private Label _lblClaudeCardHeader;
        private Label _lblApiKeyLabel;
        private TextBox _txtClaudeApiKey;
        private Label _lblModelLabel;
        private Label _lblModelValue;
        private Label _lblSymbolsLabel;
        private TextBox _txtClaudeSymbols;
        private Label _lblPollSecLabel;
        private NumericUpDown _nudClaudePollSec;
        private Label _lblClaudeNote1;
        private Label _lblClaudeNote2;
        private Button _btnStartClaude;
        private Button _btnStopClaude;
        private Panel _pnlClaudePromptCard;
        private Label _lblPromptHeader;
        private RichTextBox _txtClaudePrompt;
        private Button _btnResetPrompt;

        // ── Log tab ───────────────────────────────────────────────
        private RichTextBox _txtLog;
        private Button _btnClearLog;
        private Button _btnSaveLog;

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            SuspendLayout();

            // ── Clock timer ───────────────────────────────────────
            _clockTimer = new System.Windows.Forms.Timer(components) { Interval = 1000 };

            // ═════════════════════════════════════════════════════
            //  HEADER
            // ═════════════════════════════════════════════════════
            _pnlHeader = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = C_SURFACE };
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

            // ═════════════════════════════════════════════════════
            //  CONNECTION BAR
            // ═════════════════════════════════════════════════════
            _pnlConnBar = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = C_CARD };
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
            _btnConnect    = MkBtn("⚡ Connect",  502, 7, 110, C_GREEN);
            _btnDisconnect = MkBtn("Disconnect",  622, 7, 100, C_RED);
            _btnDisconnect.Enabled = false;
            _chkAutoConn = new CheckBox
            {
                Text = "Auto-connect on launch", ForeColor = C_MUTED,
                Location = new Point(734, 10), AutoSize = true
            };
            _pnlConnBar.Controls.AddRange(new Control[] {
                _cmbMode, _lblPipeLabel, _txtPipeName, _btnConnect, _btnDisconnect, _chkAutoConn });

            // ═════════════════════════════════════════════════════
            //  ACCOUNT BAR
            // ═════════════════════════════════════════════════════
            _pnlAccountBar = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = C_SURFACE };
            _lblAccNum     = MkLbl("Account: —",     8,   12, C_MUTED);
            _lblBalance    = MkLbl("Balance: —",     170, 12, C_TEXT);
            _lblEquity     = MkLbl("Equity: —",      300, 12, C_TEXT);
            _lblFreeMargin = MkLbl("Free Margin: —", 430, 12, C_TEXT);
            _lblPnl        = MkLbl("P&L: —",         580, 12, C_TEXT);
            _lblMarginLvl  = MkLbl("Margin Lvl: —",  680, 12, C_MUTED);
            _pnlAccountBar.Controls.AddRange(new Control[] {
                _lblAccNum, _lblBalance, _lblEquity, _lblFreeMargin, _lblPnl, _lblMarginLvl });

            // ═════════════════════════════════════════════════════
            //  TAB CONTROL
            // ═════════════════════════════════════════════════════
            _tabControl = new TabControl
            {
                Dock = DockStyle.Fill, DrawMode = TabDrawMode.OwnerDrawFixed,
                Padding = new Point(14, 6), BackColor = C_BG
            };

            // ─── TRADE TAB ────────────────────────────────────────
            _tabTrade     = new TabPage("  📈 Trade  ")    { BackColor = Color.FromArgb(18, 18, 26) };
            _pnlTradeLeft = new Panel
            {
                Location = new Point(12, 8), Size = new Size(370, 680),
                BackColor = Color.FromArgb(24, 25, 38), BorderStyle = BorderStyle.FixedSingle
            };
            _lblTradeHeader    = MkCardH("Manual Trade Entry", 14, 14);
            _lblPairLabel      = MkLbl("Pair",        14, 50,  C_MUTED);
            _cmbPair           = MkCmb(new[] { "GBPUSD", "EURUSD", "USDJPY", "XAUUSD", "USDCAD", "AUDUSD", "EURGBP" }, 130, 48, 210);
            _lblDirLabel       = MkLbl("Direction",   14, 82,  C_MUTED);
            _cmbDir            = MkCmb(new[] { "BUY", "SELL" }, 130, 80, 210);
            _lblOrderTypeLabel = MkLbl("Order Type",  14, 114, C_MUTED);
            _cmbOrderType      = MkCmb(new[] { "MARKET", "LIMIT", "STOP" }, 130, 112, 210);
            _lblEntryLabel     = MkLbl("Entry Price", 14, 146, C_MUTED);
            _txtEntry          = MkTxt("0 (market)",  130, 144, 210);
            _txtEntry.Enabled  = false;
            _lblSLLabel        = MkLbl("Stop Loss ✱",   14, 178, C_RED);
            _txtSL             = MkTxt("e.g. 1.34750",  130, 176, 210);
            _lblTPLabel        = MkLbl("Take Profit ✱", 14, 210, C_GREEN);
            _txtTP             = MkTxt("e.g. 1.35200",  130, 208, 210);
            _lblTP2Label       = MkLbl("Take Profit 2", 14, 242, C_ACCENT);
            _txtTP2            = MkTxt("0 (optional)",  130, 240, 210);
            _chkAutoLot = new CheckBox
            {
                Text = "Auto lot size (1% risk)", ForeColor = C_YELLOW, Checked = true,
                Location = new Point(14, 274), AutoSize = true
            };
            _lblLotLabel   = MkLbl("Lot Size", 14, 302, C_MUTED);
            _txtLot        = MkTxt("0.01",     130, 300, 100);
            _txtLot.Enabled = false;
            _chkMoveSLBE = new CheckBox
            {
                Text = "Move SL → Breakeven after TP1", ForeColor = C_ACCENT,
                Location = new Point(14, 334), AutoSize = true, Checked = true
            };
            _pnlRR = new Panel
            {
                Location = new Point(14, 364), Size = new Size(326, 52),
                BackColor = Color.FromArgb(20, 99, 179, 237)
            };
            _lblRR           = new Label { Text = "R:R  —",     Location = new Point(10, 8),   AutoSize = true, Font = new Font("Consolas", 9f),   ForeColor = C_ACCENT };
            _lblDollarRisk   = new Label { Text = "Risk  $—",   Location = new Point(10, 26),  AutoSize = true, Font = new Font("Consolas", 8.5f), ForeColor = C_RED };
            _lblDollarProfit = new Label { Text = "Profit  $—", Location = new Point(170, 26), AutoSize = true, Font = new Font("Consolas", 8.5f), ForeColor = C_GREEN };
            _pnlRR.Controls.AddRange(new Control[] { _lblRR, _lblDollarRisk, _lblDollarProfit });
            _btnBuy  = MkBtn("▲  BUY",  14,  424, 152, C_GREEN);
            _btnBuy.Height = 44;
            _btnBuy.Font   = new Font("Segoe UI Semibold", 11f);
            _btnSell = MkBtn("▼  SELL", 174, 424, 152, C_RED);
            _btnSell.Height = 44;
            _btnSell.Font   = new Font("Segoe UI Semibold", 11f);
            _pnlTradeLeft.Controls.AddRange(new Control[] {
                _lblTradeHeader,
                _lblPairLabel, _cmbPair, _lblDirLabel, _cmbDir,
                _lblOrderTypeLabel, _cmbOrderType, _lblEntryLabel, _txtEntry,
                _lblSLLabel, _txtSL, _lblTPLabel, _txtTP, _lblTP2Label, _txtTP2,
                _chkAutoLot, _lblLotLabel, _txtLot, _chkMoveSLBE,
                _pnlRR, _btnBuy, _btnSell });

            _pnlTradeRight = new Panel
            {
                Location = new Point(392, 8), Size = new Size(838, 680),
                BackColor = Color.FromArgb(24, 25, 38), BorderStyle = BorderStyle.FixedSingle
            };
            _lblJsonHeader = MkCardH("JSON Signal Input  (paste, load file, or drop)", 14, 14);
            _txtJson = new RichTextBox
            {
                Location = new Point(14, 50), Size = new Size(808, 540),
                BackColor = Color.FromArgb(14, 14, 22), ForeColor = Color.FromArgb(200, 220, 180),
                Font = new Font("Consolas", 9.5f), BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.Vertical, AllowDrop = true
            };
            _btnJsonLoad   = MkBtn("📂 Load File",    14,  600, 130, C_ACCENT);
            _btnJsonExec   = MkBtn("⚡ Execute JSON", 154, 600, 140, C_GREEN);
            _btnJsonFmt    = MkBtn("🔧 Format",       304, 600, 100, C_YELLOW);
            _btnJsonSample = MkBtn("📋 Sample",       414, 600, 100, C_MUTED);
            _pnlTradeRight.Controls.AddRange(new Control[] {
                _lblJsonHeader, _txtJson, _btnJsonLoad, _btnJsonExec, _btnJsonFmt, _btnJsonSample });
            _tabTrade.Controls.AddRange(new Control[] { _pnlTradeLeft, _pnlTradeRight });

            // ─── POSITIONS TAB ────────────────────────────────────
            _tabPositions = new TabPage("  📊 Positions  ") { BackColor = Color.FromArgb(18, 18, 26) };
            _gridPos = MkGrid();
            _gridPos.Location = new Point(12, 8);
            _gridPos.Size     = new Size(1220, 580);
            _gridPos.Columns.Add("Ticket",   "Ticket");
            _gridPos.Columns.Add("Symbol",   "Symbol");
            _gridPos.Columns.Add("Type",     "Type");
            _gridPos.Columns.Add("Lots",     "Lots");
            _gridPos.Columns.Add("Open",     "Open");
            _gridPos.Columns.Add("Current",  "Current");
            _gridPos.Columns.Add("SL",       "SL");
            _gridPos.Columns.Add("TP",       "TP");
            _gridPos.Columns.Add("PnL",      "P&L ($)");
            _gridPos.Columns.Add("Pips",     "Pips");
            _gridPos.Columns.Add("Time",     "Time");
            _gridPos.Columns.Add("Comment",  "Comment");
            _btnClosePos    = MkBtn("Close Selected", 12,  600, 150, C_RED);
            _btnCloseAllPos = MkBtn("Close ALL",      172, 600, 130, Color.FromArgb(160, 50, 50));
            _btnRefreshPos  = MkBtn("🔄 Refresh",     312, 600, 110, C_ACCENT);
            _tabPositions.Controls.AddRange(new Control[] { _gridPos, _btnClosePos, _btnCloseAllPos, _btnRefreshPos });

            // ─── HISTORY TAB ──────────────────────────────────────
            _tabHistory = new TabPage("  🗂 History  ") { BackColor = Color.FromArgb(18, 18, 26) };
            _gridHistory = MkGrid();
            _gridHistory.Location = new Point(12, 8);
            _gridHistory.Size     = new Size(1220, 580);
            _gridHistory.Columns.Add("Time",       "Time");
            _gridHistory.Columns.Add("Id",         "Id");
            _gridHistory.Columns.Add("Pair",       "Pair");
            _gridHistory.Columns.Add("Dir",        "Dir");
            _gridHistory.Columns.Add("Lots",       "Lots");
            _gridHistory.Columns.Add("Entry",      "Entry");
            _gridHistory.Columns.Add("SL",         "SL");
            _gridHistory.Columns.Add("TP",         "TP");
            _gridHistory.Columns.Add("Ticket",     "Ticket");
            _gridHistory.Columns.Add("Status",     "Status");
            _gridHistory.Columns.Add("Exec_Price", "Exec Price");
            _gridHistory.Columns.Add("Error",      "Error");
            _btnImportHistory = MkBtn("📂 Load CSV Log", 12,  600, 150, C_ACCENT);
            _btnClearHistory  = MkBtn("Clear",           172, 600, 80,  C_MUTED);
            _tabHistory.Controls.AddRange(new Control[] { _gridHistory, _btnImportHistory, _btnClearHistory });

            // ─── BOT TAB ──────────────────────────────────────────
            _tabBot = new TabPage("  🤖 Auto Bot  ") { BackColor = Color.FromArgb(18, 18, 26) };
            _lblBotBadge = new Label
            {
                Text = "● BOT STOPPED", ForeColor = C_RED,
                Font = new Font("Segoe UI Semibold", 12f), Location = new Point(14, 14), AutoSize = true
            };
            _pnlBotCard       = new Panel { Location = new Point(14, 50), Size = new Size(560, 620), BackColor = Color.FromArgb(24, 25, 38), BorderStyle = BorderStyle.FixedSingle };
            _lblBotCardHeader    = MkCardH("Bot Configuration", 14, 14);
            _lblWatchFolderLabel = MkLbl("Watch Folder",     14, 50,  C_MUTED);
            _txtWatchFolder      = MkTxt(@"C:\MT5Bot\signals", 200, 48, 330);
            _lblRiskLabel        = MkLbl("Max Risk %",       14, 82,  C_MUTED);
            _nudRisk             = MkNud(0.1m, 10m,   1m,   1, 0.1m, 100, 200, 80);
            _lblMinRRLabel       = MkLbl("Min R:R Ratio",    14, 114, C_MUTED);
            _nudMinRR            = MkNud(0.5m, 10m,   1.5m, 1, 0.1m, 100, 200, 112);
            _lblMaxTradesLabel   = MkLbl("Max Trades/Day",   14, 146, C_MUTED);
            _nudMaxTrades        = MkNud(1,    100,   5,    0, 1,    100, 200, 144);
            _lblPollMsLabel      = MkLbl("Poll Interval ms", 14, 178, C_MUTED);
            _nudPollMs           = MkNud(500,  30000, 2000, 0, 500,  120, 200, 176);
            _lblRetryLabel       = MkLbl("Retry Count",      14, 210, C_MUTED);
            _nudRetry            = MkNud(0,    10,    3,    0, 1,    100, 200, 208);
            _lblAllowedPairsLabel = MkLbl("Allowed Pairs",   14, 242, C_MUTED);
            _txtAllowedPairs     = MkTxt("GBPUSD,EURUSD,USDJPY", 200, 240, 330);
            _lblDrawdownLabel    = MkLbl("Drawdown Stop %",  14, 274, C_MUTED);
            _nudDrawdownPct      = MkNud(1,    50,    10,   1, 1,    100, 200, 272);
            _chkAutoLotBot = new CheckBox { Text = "Auto lot calculation from equity",      ForeColor = C_MUTED, Checked = true,  Location = new Point(14, 314), AutoSize = true };
            _chkEnforceRR  = new CheckBox { Text = "Enforce minimum R:R (reject if below)", ForeColor = C_MUTED, Checked = true,  Location = new Point(14, 340), AutoSize = true };
            _chkDrawdown   = new CheckBox { Text = "Drawdown protection (emergency close)", ForeColor = C_MUTED, Checked = true,  Location = new Point(14, 366), AutoSize = true };
            _chkAutoStart  = new CheckBox { Text = "Auto-start bot on app launch",          ForeColor = C_MUTED, Checked = false, Location = new Point(14, 392), AutoSize = true };
            _btnStartBot = MkBtn("▶  Start Bot", 14,  430, 160, C_GREEN);
            _btnStartBot.Height = 42;
            _btnStartBot.Font   = new Font("Segoe UI Semibold", 10f);
            _btnStopBot  = MkBtn("■  Stop Bot",  184, 430, 160, C_RED);
            _btnStopBot.Height  = 42;
            _btnStopBot.Font    = new Font("Segoe UI Semibold", 10f);
            _btnStopBot.Enabled = false;
            _pnlBotCard.Controls.AddRange(new Control[] {
                _lblBotCardHeader,
                _lblWatchFolderLabel, _txtWatchFolder,
                _lblRiskLabel, _nudRisk, _lblMinRRLabel, _nudMinRR,
                _lblMaxTradesLabel, _nudMaxTrades, _lblPollMsLabel, _nudPollMs,
                _lblRetryLabel, _nudRetry, _lblAllowedPairsLabel, _txtAllowedPairs,
                _lblDrawdownLabel, _nudDrawdownPct,
                _chkAutoLotBot, _chkEnforceRR, _chkDrawdown, _chkAutoStart,
                _btnStartBot, _btnStopBot });
            _pnlBotInfo       = new Panel { Location = new Point(590, 50), Size = new Size(640, 620), BackColor = Color.FromArgb(24, 25, 38), BorderStyle = BorderStyle.FixedSingle };
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
            _tabBot.Controls.AddRange(new Control[] { _lblBotBadge, _pnlBotCard, _pnlBotInfo, _btnOpenFolder });

            // ─── CLAUDE AI TAB ────────────────────────────────────
            _tabClaude = new TabPage("  🧠 Claude AI  ") { BackColor = Color.FromArgb(18, 18, 26) };
            _lblClaudeBadge = new Label
            {
                Text = "● CLAUDE STOPPED", ForeColor = C_RED,
                Font = new Font("Segoe UI Semibold", 12f), Location = new Point(14, 14), AutoSize = true
            };
            _pnlClaudeCard       = new Panel { Location = new Point(14, 50), Size = new Size(560, 580), BackColor = Color.FromArgb(24, 25, 38), BorderStyle = BorderStyle.FixedSingle };
            _lblClaudeCardHeader = MkCardH("Claude AI Settings", 14, 14);
            _lblApiKeyLabel      = MkLbl("API Key",              14, 50,  C_MUTED);
            _txtClaudeApiKey     = MkTxt("sk-ant-...",           210, 48, 320);
            _txtClaudeApiKey.UseSystemPasswordChar = true;
            _lblModelLabel   = MkLbl("Model",                14, 82,  C_MUTED);
            _lblModelValue   = new Label { Text = "claude-opus-4-7", Location = new Point(210, 80), AutoSize = true, ForeColor = C_ACCENT, Font = new Font("Consolas", 9f) };
            _lblSymbolsLabel = MkLbl("Watch Symbols",        14, 114, C_MUTED);
            _txtClaudeSymbols = MkTxt("GBPUSD,EURUSD,USDJPY", 210, 112, 320);
            _lblPollSecLabel  = MkLbl("Poll Interval (sec)", 14, 146, C_MUTED);
            _nudClaudePollSec = MkNud(10, 3600, 60, 0, 10, 120, 210, 144);
            _lblClaudeNote1   = new Label { Text = "⚡ Start the Bot tab first for full validation pipeline.", Location = new Point(14, 194), AutoSize = true, ForeColor = C_MUTED,  Font = new Font("Segoe UI", 8f) };
            _lblClaudeNote2   = new Label { Text = "🔐 API key is saved to settings.json on disk.",           Location = new Point(14, 214), AutoSize = true, ForeColor = C_YELLOW, Font = new Font("Segoe UI", 8f) };
            _btnStartClaude = MkBtn("▶  Start Claude", 14,  244, 180, C_GREEN);
            _btnStartClaude.Height = 42;
            _btnStartClaude.Font   = new Font("Segoe UI Semibold", 10f);
            _btnStopClaude  = MkBtn("■  Stop Claude",  204, 244, 180, C_RED);
            _btnStopClaude.Height  = 42;
            _btnStopClaude.Font    = new Font("Segoe UI Semibold", 10f);
            _btnStopClaude.Enabled = false;
            _pnlClaudeCard.Controls.AddRange(new Control[] {
                _lblClaudeCardHeader,
                _lblApiKeyLabel, _txtClaudeApiKey,
                _lblModelLabel, _lblModelValue,
                _lblSymbolsLabel, _txtClaudeSymbols,
                _lblPollSecLabel, _nudClaudePollSec,
                _lblClaudeNote1, _lblClaudeNote2,
                _btnStartClaude, _btnStopClaude });
            _pnlClaudePromptCard = new Panel { Location = new Point(590, 50), Size = new Size(640, 580), BackColor = Color.FromArgb(24, 25, 38), BorderStyle = BorderStyle.FixedSingle };
            _lblPromptHeader     = MkCardH("System Prompt  (stable — cached by Claude API)", 14, 14);
            _txtClaudePrompt = new RichTextBox
            {
                Location = new Point(14, 48), Size = new Size(608, 486),
                BackColor = Color.FromArgb(14, 14, 22), ForeColor = Color.FromArgb(200, 220, 180),
                Font = new Font("Consolas", 9f), BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                Text = ClaudeConfig.DefaultPrompt
            };
            _btnResetPrompt = MkBtn("↺ Reset Default", 14, 542, 150, C_MUTED);
            _pnlClaudePromptCard.Controls.AddRange(new Control[] { _lblPromptHeader, _txtClaudePrompt, _btnResetPrompt });
            _tabClaude.Controls.AddRange(new Control[] { _lblClaudeBadge, _pnlClaudeCard, _pnlClaudePromptCard });

            // ─── LOG TAB ──────────────────────────────────────────
            _tabLog = new TabPage("  📋 Log  ") { BackColor = Color.FromArgb(18, 18, 26) };
            _txtLog = new RichTextBox
            {
                Location = new Point(12, 8), Size = new Size(1222, 640),
                BackColor = Color.FromArgb(12, 12, 18), ForeColor = C_TEXT,
                Font = new Font("Consolas", 9f), ReadOnly = true, BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.Vertical
            };
            _btnClearLog = MkBtn("Clear",    12,  658, 100, C_MUTED);
            _btnSaveLog  = MkBtn("Save Log", 122, 658, 100, C_ACCENT);
            _tabLog.Controls.AddRange(new Control[] { _txtLog, _btnClearLog, _btnSaveLog });

            // ── Assemble tabs ─────────────────────────────────────
            _tabControl.TabPages.AddRange(new TabPage[] {
                _tabTrade, _tabPositions, _tabHistory, _tabBot, _tabClaude, _tabLog });

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

            // Add in reverse order: Fill first, then Top panels stack correctly
            this.Controls.Add(_tabControl);
            this.Controls.Add(_pnlAccountBar);
            this.Controls.Add(_pnlConnBar);
            this.Controls.Add(_pnlHeader);

            ResumeLayout(false);
        }

        // ── Factory helpers (Designer.cs only) ────────────────────

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
