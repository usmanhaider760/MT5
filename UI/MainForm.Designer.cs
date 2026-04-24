using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace MT5TradingBot.UI
{
    partial class MainForm
    {
        private IContainer components = null;

        // ───────────────── HEADER ─────────────────
        private Panel _pnlHeader;
        private Label _lblTitle;
        private Panel _pnlDot;
        private Label _lblConnStatus;
        private Label _lblTime;
        private Label _lblBotBadge;

        // ───────────────── CONNECTION ─────────────────
        private Panel _pnlConnBar;
        private ComboBox _cmbMode;
        private TextBox _txtPipeName;
        private Button _btnConnect;
        private Button _btnDisconnect;
        private CheckBox _chkAutoConn;

        // ───────────────── ACCOUNT ─────────────────
        private Panel _pnlAccountBar;
        private Label _lblAccNum;
        private Label _lblBalance;
        private Label _lblEquity;
        private Label _lblFreeMargin;
        private Label _lblPnl;
        private Label _lblMarginLvl;

        // ───────────────── TABS ─────────────────
        private TabControl _tabControl;
        private TabPage _tabTrade;
        private TabPage _tabPositions;
        private TabPage _tabHistory;
        private TabPage _tabBot;
        private TabPage _tabClaude;
        private TabPage _tabLog;

        // ───────────────── TRADE TAB ─────────────────
        private ComboBox _cmbPair;
        private ComboBox _cmbDir;
        private ComboBox _cmbOrderType;
        private TextBox _txtEntry;
        private TextBox _txtSL;
        private TextBox _txtTP;
        private TextBox _txtTP2;
        private TextBox _txtLot;
        private CheckBox _chkAutoLot;
        private CheckBox _chkMoveSLBE;
        private Button _btnBuy;
        private Button _btnSell;
        private RichTextBox _txtJson;

        private Button _btnJsonLoad;
        private Button _btnJsonExec;
        private Button _btnJsonFmt;
        private Button _btnJsonSample;

        // ───────────────── POSITIONS ─────────────────
        private DataGridView _gridPos;
        private Button _btnClosePos;
        private Button _btnCloseAllPos;
        private Button _btnRefreshPos;

        // ───────────────── HISTORY ─────────────────
        private DataGridView _gridHistory;
        private Button _btnImportHistory;
        private Button _btnClearHistory;

        // ───────────────── BOT ─────────────────
        private TextBox _txtWatchFolder;
        private NumericUpDown _nudRisk;
        private NumericUpDown _nudMinRR;
        private NumericUpDown _nudMaxTrades;
        private NumericUpDown _nudPollMs;
        private NumericUpDown _nudRetry;
        private TextBox _txtAllowedPairs;
        private NumericUpDown _nudDrawdownPct;

        private CheckBox _chkAutoLotBot;
        private CheckBox _chkEnforceRR;
        private CheckBox _chkDrawdown;
        private CheckBox _chkAutoStart;

        private Button _btnStartBot;
        private Button _btnStopBot;
        private Button _btnOpenFolder;

        // ───────────────── CLAUDE ─────────────────
        private TextBox _txtClaudeApiKey;
        private TextBox _txtClaudeSymbols;
        private NumericUpDown _nudClaudePollSec;
        private RichTextBox _txtClaudePrompt;
        private Button _btnStartClaude;
        private Button _btnStopClaude;
        private Button _btnResetPrompt;

        // ───────────────── LOG ─────────────────
        private RichTextBox _txtLog;
        private Button _btnClearLog;
        private Button _btnSaveLog;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            components = new Container();

            this.SuspendLayout();

            // ───────── FORM ─────────
            this.Text = "MT5 Trading Bot — Professional";
            this.Size = new Size(1280, 860);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(13, 13, 19);
            this.ForeColor = Color.White;

            // ───────── HEADER ─────────
            _pnlHeader = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = Color.FromArgb(22, 22, 32) };
            _lblTitle = new Label { Text = "MT5 Trading Bot", Location = new Point(15, 15), AutoSize = true };
            _lblConnStatus = new Label { Text = "Disconnected", Location = new Point(250, 15), AutoSize = true };
            _pnlDot = new Panel { Size = new Size(10, 10), Location = new Point(220, 18), BackColor = Color.Red };
            _lblTime = new Label { Text = "Time", Location = new Point(1000, 15), AutoSize = true };

            _pnlHeader.Controls.AddRange(new Control[] { _lblTitle, _lblConnStatus, _pnlDot, _lblTime });

            // ───────── CONNECTION ─────────
            _pnlConnBar = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Color.FromArgb(28, 28, 40) };

            _cmbMode = new ComboBox { Location = new Point(10, 8), Width = 200 };
            _cmbMode.Items.AddRange(new[] { "Named Pipe", "Socket" });

            _txtPipeName = new TextBox { Location = new Point(220, 8), Width = 200, Text = "MT5TradingBotPipe" };
            _btnConnect = new Button { Text = "Connect", Location = new Point(430, 6) };
            _btnDisconnect = new Button { Text = "Disconnect", Location = new Point(520, 6) };
            _chkAutoConn = new CheckBox { Text = "Auto Connect", Location = new Point(630, 10) };

            _pnlConnBar.Controls.AddRange(new Control[] {
                _cmbMode, _txtPipeName, _btnConnect, _btnDisconnect, _chkAutoConn
            });

            // ───────── ACCOUNT ─────────
            _pnlAccountBar = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Color.FromArgb(22, 22, 32) };

            _lblAccNum = new Label { Text = "Account", Location = new Point(10, 10), AutoSize = true };
            _lblBalance = new Label { Text = "Balance", Location = new Point(150, 10), AutoSize = true };
            _lblEquity = new Label { Text = "Equity", Location = new Point(300, 10), AutoSize = true };
            _lblFreeMargin = new Label { Text = "Free", Location = new Point(450, 10), AutoSize = true };
            _lblPnl = new Label { Text = "PnL", Location = new Point(600, 10), AutoSize = true };
            _lblMarginLvl = new Label { Text = "Margin", Location = new Point(700, 10), AutoSize = true };

            _pnlAccountBar.Controls.AddRange(new Control[]
            {
                _lblAccNum, _lblBalance, _lblEquity, _lblFreeMargin, _lblPnl, _lblMarginLvl
            });

            // ───────── TABS ─────────
            _tabControl = new TabControl { Dock = DockStyle.Fill };

            _tabTrade = new TabPage("Trade");
            _tabPositions = new TabPage("Positions");
            _tabHistory = new TabPage("History");
            _tabBot = new TabPage("Bot");
            _tabClaude = new TabPage("Claude");
            _tabLog = new TabPage("Log");

            _tabControl.TabPages.AddRange(new[]
            {
                _tabTrade, _tabPositions, _tabHistory, _tabBot, _tabClaude, _tabLog
            });

            _lblBotBadge = new Label
            {
                Text = "● BOT STOPPED",
                Location = new Point(20, 20),
                AutoSize = true,
                ForeColor = Color.Red
            };

            _tabBot.Controls.Add(_lblBotBadge);

            // (Trade / Bot / Claude / Log controls omitted here for brevity of message —
            // I will continue in next message to complete FULL remaining tabs)

            // ───────── FORM ADD ─────────
            this.Controls.Add(_tabControl);
            this.Controls.Add(_pnlAccountBar);
            this.Controls.Add(_pnlConnBar);
            this.Controls.Add(_pnlHeader);

            this.ResumeLayout(false);

            // ================= TRADE TAB =================
            var pnlTradeLeft = new Panel { Location = new Point(10, 10), Size = new Size(350, 650) };
            var pnlTradeRight = new Panel { Location = new Point(370, 10), Size = new Size(850, 650) };

            // Inputs
            _cmbPair = new ComboBox { Location = new Point(120, 20), Width = 200 };
            _cmbPair.Items.AddRange(new[] { "GBPUSD", "EURUSD", "USDJPY", "XAUUSD" });

            _cmbDir = new ComboBox { Location = new Point(120, 60), Width = 200 };
            _cmbDir.Items.AddRange(new[] { "BUY", "SELL" });

            _cmbOrderType = new ComboBox { Location = new Point(120, 100), Width = 200 };
            _cmbOrderType.Items.AddRange(new[] { "MARKET", "LIMIT", "STOP" });

            _txtEntry = new TextBox { Location = new Point(120, 140), Width = 200 };
            _txtSL = new TextBox { Location = new Point(120, 180), Width = 200 };
            _txtTP = new TextBox { Location = new Point(120, 220), Width = 200 };
            _txtTP2 = new TextBox { Location = new Point(120, 260), Width = 200 };

            _chkAutoLot = new CheckBox { Text = "Auto Lot", Location = new Point(10, 300) };

            _txtLot = new TextBox { Location = new Point(120, 330), Width = 100 };

            _chkMoveSLBE = new CheckBox
            {
                Text = "Move SL to BE",
                Location = new Point(10, 360)
            };

            _btnBuy = new Button { Text = "BUY", Location = new Point(10, 420), Width = 150 };
            _btnSell = new Button { Text = "SELL", Location = new Point(170, 420), Width = 150 };

            // JSON panel
            _txtJson = new RichTextBox
            {
                Location = new Point(10, 10),
                Size = new Size(820, 550)
            };

            _btnJsonLoad = new Button { Text = "Load", Location = new Point(10, 570) };
            _btnJsonExec = new Button { Text = "Execute", Location = new Point(100, 570) };
            _btnJsonFmt = new Button { Text = "Format", Location = new Point(200, 570) };
            _btnJsonSample = new Button { Text = "Sample", Location = new Point(300, 570) };

            pnlTradeLeft.Controls.AddRange(new Control[]
            {
    _cmbPair, _cmbDir, _cmbOrderType,
    _txtEntry, _txtSL, _txtTP, _txtTP2,
    _chkAutoLot, _txtLot, _chkMoveSLBE,
    _btnBuy, _btnSell
            });

            pnlTradeRight.Controls.AddRange(new Control[]
            {
    _txtJson, _btnJsonLoad, _btnJsonExec, _btnJsonFmt, _btnJsonSample
            });

            _tabTrade.Controls.AddRange(new Control[] { pnlTradeLeft, pnlTradeRight });


            // ================= POSITIONS TAB =================
            _gridPos = new DataGridView { Dock = DockStyle.Top, Height = 550 };
            _btnClosePos = new Button { Text = "Close Selected", Location = new Point(10, 560) };
            _btnCloseAllPos = new Button { Text = "Close All", Location = new Point(150, 560) };
            _btnRefreshPos = new Button { Text = "Refresh", Location = new Point(260, 560) };

            _tabPositions.Controls.AddRange(new Control[]
            {
    _gridPos, _btnClosePos, _btnCloseAllPos, _btnRefreshPos
            });


            // ================= HISTORY TAB =================
            _gridHistory = new DataGridView { Dock = DockStyle.Top, Height = 550 };
            _btnImportHistory = new Button { Text = "Load CSV", Location = new Point(10, 560) };
            _btnClearHistory = new Button { Text = "Clear", Location = new Point(120, 560) };

            _tabHistory.Controls.AddRange(new Control[]
            {
    _gridHistory, _btnImportHistory, _btnClearHistory
            });


            // ================= BOT TAB =================
            _txtWatchFolder = new TextBox { Location = new Point(20, 40), Width = 300 };

            _nudRisk = new NumericUpDown { Location = new Point(20, 80), DecimalPlaces = 1, Value = 1 };
            _nudMinRR = new NumericUpDown { Location = new Point(20, 120), DecimalPlaces = 1, Value = 1.5M };
            _nudMaxTrades = new NumericUpDown { Location = new Point(20, 160), Value = 5 };
            _nudPollMs = new NumericUpDown { Location = new Point(20, 200), Value = 2000 };
            _nudRetry = new NumericUpDown { Location = new Point(20, 240), Value = 3 };

            _txtAllowedPairs = new TextBox { Location = new Point(20, 280), Width = 300 };
            _nudDrawdownPct = new NumericUpDown { Location = new Point(20, 320), Value = 10 };

            _chkAutoLotBot = new CheckBox { Text = "Auto Lot", Location = new Point(20, 360) };
            _chkEnforceRR = new CheckBox { Text = "Enforce RR", Location = new Point(20, 390) };
            _chkDrawdown = new CheckBox { Text = "Drawdown Protection", Location = new Point(20, 420) };
            _chkAutoStart = new CheckBox { Text = "Auto Start", Location = new Point(20, 450) };

            _btnStartBot = new Button { Text = "Start Bot", Location = new Point(20, 500) };
            _btnStopBot = new Button { Text = "Stop Bot", Location = new Point(140, 500) };
            _btnOpenFolder = new Button { Text = "Open Folder", Location = new Point(260, 500) };

            _tabBot.Controls.AddRange(new Control[]
            {
    _txtWatchFolder, _nudRisk, _nudMinRR, _nudMaxTrades,
    _nudPollMs, _nudRetry, _txtAllowedPairs, _nudDrawdownPct,
    _chkAutoLotBot, _chkEnforceRR, _chkDrawdown, _chkAutoStart,
    _btnStartBot, _btnStopBot, _btnOpenFolder
            });


            // ================= CLAUDE TAB =================
            _txtClaudeApiKey = new TextBox { Location = new Point(20, 40), Width = 300 };
            _txtClaudeSymbols = new TextBox { Location = new Point(20, 80), Width = 300 };
            _nudClaudePollSec = new NumericUpDown { Location = new Point(20, 120), Value = 60 };

            _btnStartClaude = new Button { Text = "Start Claude", Location = new Point(20, 160) };
            _btnStopClaude = new Button { Text = "Stop Claude", Location = new Point(160, 160) };

            _txtClaudePrompt = new RichTextBox
            {
                Location = new Point(350, 40),
                Size = new Size(500, 500)
            };

            _btnResetPrompt = new Button { Text = "Reset Prompt", Location = new Point(350, 550) };

            _tabClaude.Controls.AddRange(new Control[]
            {
    _txtClaudeApiKey, _txtClaudeSymbols, _nudClaudePollSec,
    _btnStartClaude, _btnStopClaude,
    _txtClaudePrompt, _btnResetPrompt
            });


            // ================= LOG TAB =================
            _txtLog = new RichTextBox
            {
                Dock = DockStyle.Top,
                Height = 600,
                ReadOnly = true
            };

            _btnClearLog = new Button { Text = "Clear", Location = new Point(10, 610) };
            _btnSaveLog = new Button { Text = "Save", Location = new Point(100, 610) };

            _tabLog.Controls.AddRange(new Control[]
            {
    _txtLog, _btnClearLog, _btnSaveLog
            });
        }
    }
}