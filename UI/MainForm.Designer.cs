
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
        private TableLayoutPanel _layoutRoot;

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
        private ComboBox _cmbAllowedPair;
        private Label _lblDrawdownLabel;
        private NumericUpDown _nudDrawdownPct;
        private CheckBox _chkAutoLotBot;
        private CheckBox _chkEnforceRR;
        private CheckBox _chkDrawdown;
        private CheckBox _chkAutoStart;
        private Button _btnStartBot;
        private Button _btnStopBot;
        private Button _btnBotSettings;
        private Button _btnAnalyzePairs;
        private Button _btnOpenFolder;
        private Button _btnBotInstructions;
        private Panel _pnlSignalFeed;
        private Label _lblSignalFeedHeader;
        private FlowLayoutPanel _flpSignals;

        // ── Claude AI tab ─────────────────────────────────────────
        private Label _lblClaudeBadge;
        private Panel _pnlClaudeCard;
        private Label _lblClaudeCardHeader;
        private Label _lblAiProviderLabel;
        private ComboBox _cmbAiProvider;
        private Label _lblApiKeyLabel;
        private TextBox _txtClaudeApiKey;
        private Label _lblModelLabel;
        private Label _lblModelValue;
        private TextBox _txtClaudeModel;
        private Label _lblOpenAiKeyLabel;
        private TextBox _txtOpenAiApiKey;
        private Label _lblOpenAiModelLabel;
        private TextBox _txtOpenAiModel;
        private Label _lblSymbolsLabel;
        private TextBox _txtClaudeSymbols;
        private Label _lblPollSecLabel;
        private NumericUpDown _nudClaudePollSec;
        private Label _lblConfidenceLabel;
        private NumericUpDown _nudAiConfidence;
        private Label _lblNewsHeader;
        private Label _lblNewsProviderLabel;
        private ComboBox _cmbNewsProvider;
        private Label _lblNewsApiKeyLabel;
        private TextBox _txtNewsApiKey;
        private Label _lblNewsCurrenciesLabel;
        private TextBox _txtNewsCurrencies;
        private Label _lblNewsImpactLabel;
        private ComboBox _cmbNewsImpact;
        private Label _lblNewsBlackoutLabel;
        private NumericUpDown _nudNewsBefore;
        private NumericUpDown _nudNewsAfter;
        private Button _btnTestNewsApi;
        private Label _lblNewsTestStatus;
        private Label _lblNotifyHeader;
        private Label _lblTelegramTokenLabel;
        private TextBox _txtTelegramBotToken;
        private Label _lblTelegramChatLabel;
        private TextBox _txtTelegramChatId;
        private CheckBox _chkNotifySignals;
        private CheckBox _chkNotifyApproval;
        private CheckBox _chkNotifyOpened;
        private CheckBox _chkNotifyClosed;
        private CheckBox _chkNotifyRisk;
        private Button _btnTestTelegram;
        private Label _lblTelegramTestStatus;
        private Label _lblClaudeNote1;
        private Label _lblClaudeNote2;
        private Button _btnStartClaude;
        private Button _btnStopClaude;
        private Button _btnTestClaudeApi;
        private Label  _lblApiTestStatus;
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
            DataGridViewCellStyle dataGridViewCellStyle1 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle2 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle3 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle4 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle5 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle6 = new DataGridViewCellStyle();
            _clockTimer = new System.Windows.Forms.Timer(components);
            _layoutRoot = new TableLayoutPanel();
            _pnlHeader = new Panel();
            _lblTitle = new Label();
            _pnlDot = new Panel();
            _lblConnStatus = new Label();
            _lblTime = new Label();
            _pnlConnBar = new Panel();
            _cmbMode = new ComboBox();
            _lblPipeLabel = new Label();
            _txtPipeName = new TextBox();
            _btnConnect = new Button();
            _btnDisconnect = new Button();
            _chkAutoConn = new CheckBox();
            _pnlAccountBar = new Panel();
            _lblAccNum = new Label();
            _lblBalance = new Label();
            _lblEquity = new Label();
            _lblFreeMargin = new Label();
            _lblPnl = new Label();
            _lblMarginLvl = new Label();
            _tabControl = new TabControl();
            _tabTrade = new TabPage();
            _pnlTradeLeft = new Panel();
            _lblTradeHeader = new Label();
            _lblPairLabel = new Label();
            _cmbPair = new ComboBox();
            _lblDirLabel = new Label();
            _cmbDir = new ComboBox();
            _lblOrderTypeLabel = new Label();
            _cmbOrderType = new ComboBox();
            _lblEntryLabel = new Label();
            _txtEntry = new TextBox();
            _lblSLLabel = new Label();
            _txtSL = new TextBox();
            _lblTPLabel = new Label();
            _txtTP = new TextBox();
            _lblTP2Label = new Label();
            _txtTP2 = new TextBox();
            _chkAutoLot = new CheckBox();
            _lblLotLabel = new Label();
            _txtLot = new TextBox();
            _chkMoveSLBE = new CheckBox();
            _pnlRR = new Panel();
            _lblRR = new Label();
            _lblDollarRisk = new Label();
            _lblDollarProfit = new Label();
            _btnBuy = new Button();
            _btnSell = new Button();
            _pnlTradeRight = new Panel();
            _lblJsonHeader = new Label();
            _txtJson = new RichTextBox();
            _btnJsonLoad = new Button();
            _btnJsonExec = new Button();
            _btnJsonFmt = new Button();
            _btnJsonSample = new Button();
            _tabPositions = new TabPage();
            _gridPos = new DataGridView();
            _btnClosePos = new Button();
            _btnCloseAllPos = new Button();
            _btnRefreshPos = new Button();
            _tabHistory = new TabPage();
            _gridHistory = new DataGridView();
            _btnImportHistory = new Button();
            _btnClearHistory = new Button();
            _tabBot = new TabPage();
            _lblBotBadge = new Label();
            _pnlBotCard = new Panel();
            _lblBotCardHeader = new Label();
            _lblWatchFolderLabel = new Label();
            _txtWatchFolder = new TextBox();
            _lblRiskLabel = new Label();
            _nudRisk = new NumericUpDown();
            _lblMinRRLabel = new Label();
            _nudMinRR = new NumericUpDown();
            _lblMaxTradesLabel = new Label();
            _nudMaxTrades = new NumericUpDown();
            _lblPollMsLabel = new Label();
            _nudPollMs = new NumericUpDown();
            _lblRetryLabel = new Label();
            _nudRetry = new NumericUpDown();
            _lblAllowedPairsLabel = new Label();
            _cmbAllowedPair = new ComboBox();
            _lblDrawdownLabel = new Label();
            _nudDrawdownPct = new NumericUpDown();
            _chkAutoLotBot = new CheckBox();
            _chkEnforceRR = new CheckBox();
            _chkDrawdown = new CheckBox();
            _chkAutoStart = new CheckBox();
            _btnStartBot = new Button();
            _btnStopBot = new Button();
            _btnBotSettings = new Button();
            _btnAnalyzePairs = new Button();
            _btnOpenFolder = new Button();
            _btnBotInstructions = new Button();
            _pnlSignalFeed = new Panel();
            _lblSignalFeedHeader = new Label();
            _flpSignals = new FlowLayoutPanel();
            _tabClaude = new TabPage();
            _lblClaudeBadge = new Label();
            _pnlClaudeCard = new Panel();
            _lblClaudeCardHeader = new Label();
            _lblAiProviderLabel = new Label();
            _cmbAiProvider = new ComboBox();
            _lblApiKeyLabel = new Label();
            _txtClaudeApiKey = new TextBox();
            _lblModelLabel = new Label();
            _lblModelValue = new Label();
            _txtClaudeModel = new TextBox();
            _lblOpenAiKeyLabel = new Label();
            _txtOpenAiApiKey = new TextBox();
            _lblOpenAiModelLabel = new Label();
            _txtOpenAiModel = new TextBox();
            _lblSymbolsLabel = new Label();
            _txtClaudeSymbols = new TextBox();
            _lblPollSecLabel = new Label();
            _nudClaudePollSec = new NumericUpDown();
            _lblConfidenceLabel = new Label();
            _nudAiConfidence = new NumericUpDown();
            _lblNewsHeader = new Label();
            _lblNewsProviderLabel = new Label();
            _cmbNewsProvider = new ComboBox();
            _lblNewsApiKeyLabel = new Label();
            _txtNewsApiKey = new TextBox();
            _lblNewsCurrenciesLabel = new Label();
            _txtNewsCurrencies = new TextBox();
            _lblNewsImpactLabel = new Label();
            _cmbNewsImpact = new ComboBox();
            _lblNewsBlackoutLabel = new Label();
            _nudNewsBefore = new NumericUpDown();
            _nudNewsAfter = new NumericUpDown();
            _btnTestNewsApi = new Button();
            _lblNewsTestStatus = new Label();
            _lblNotifyHeader = new Label();
            _lblTelegramTokenLabel = new Label();
            _txtTelegramBotToken = new TextBox();
            _lblTelegramChatLabel = new Label();
            _txtTelegramChatId = new TextBox();
            _chkNotifySignals = new CheckBox();
            _chkNotifyApproval = new CheckBox();
            _chkNotifyOpened = new CheckBox();
            _chkNotifyClosed = new CheckBox();
            _chkNotifyRisk = new CheckBox();
            _btnTestTelegram = new Button();
            _lblTelegramTestStatus = new Label();
            _lblClaudeNote1 = new Label();
            _lblClaudeNote2 = new Label();
            _btnStartClaude = new Button();
            _btnStopClaude = new Button();
            _btnTestClaudeApi = new Button();
            _lblApiTestStatus = new Label();
            _pnlClaudePromptCard = new Panel();
            _lblPromptHeader = new Label();
            _txtClaudePrompt = new RichTextBox();
            _btnResetPrompt = new Button();
            _tabLog = new TabPage();
            _txtLog = new RichTextBox();
            _btnClearLog = new Button();
            _btnSaveLog = new Button();
            dataGridViewTextBoxColumn1 = new DataGridViewTextBoxColumn();
            dataGridViewTextBoxColumn2 = new DataGridViewTextBoxColumn();
            dataGridViewTextBoxColumn3 = new DataGridViewTextBoxColumn();
            dataGridViewTextBoxColumn4 = new DataGridViewTextBoxColumn();
            dataGridViewTextBoxColumn5 = new DataGridViewTextBoxColumn();
            dataGridViewTextBoxColumn6 = new DataGridViewTextBoxColumn();
            dataGridViewTextBoxColumn7 = new DataGridViewTextBoxColumn();
            dataGridViewTextBoxColumn8 = new DataGridViewTextBoxColumn();
            dataGridViewTextBoxColumn9 = new DataGridViewTextBoxColumn();
            dataGridViewTextBoxColumn10 = new DataGridViewTextBoxColumn();
            dataGridViewTextBoxColumn11 = new DataGridViewTextBoxColumn();
            dataGridViewTextBoxColumn12 = new DataGridViewTextBoxColumn();
            dataGridViewTextBoxColumn13 = new DataGridViewTextBoxColumn();
            dataGridViewTextBoxColumn14 = new DataGridViewTextBoxColumn();
            dataGridViewTextBoxColumn15 = new DataGridViewTextBoxColumn();
            dataGridViewTextBoxColumn16 = new DataGridViewTextBoxColumn();
            dataGridViewTextBoxColumn17 = new DataGridViewTextBoxColumn();
            dataGridViewTextBoxColumn18 = new DataGridViewTextBoxColumn();
            dataGridViewTextBoxColumn19 = new DataGridViewTextBoxColumn();
            dataGridViewTextBoxColumn20 = new DataGridViewTextBoxColumn();
            dataGridViewTextBoxColumn21 = new DataGridViewTextBoxColumn();
            dataGridViewTextBoxColumn22 = new DataGridViewTextBoxColumn();
            dataGridViewTextBoxColumn23 = new DataGridViewTextBoxColumn();
            dataGridViewTextBoxColumn24 = new DataGridViewTextBoxColumn();
            _layoutRoot.SuspendLayout();
            _pnlHeader.SuspendLayout();
            _pnlConnBar.SuspendLayout();
            _pnlAccountBar.SuspendLayout();
            _tabControl.SuspendLayout();
            _tabTrade.SuspendLayout();
            _pnlTradeLeft.SuspendLayout();
            _pnlRR.SuspendLayout();
            _pnlTradeRight.SuspendLayout();
            _tabPositions.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)_gridPos).BeginInit();
            _tabHistory.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)_gridHistory).BeginInit();
            _tabBot.SuspendLayout();
            _pnlBotCard.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)_nudRisk).BeginInit();
            ((System.ComponentModel.ISupportInitialize)_nudMinRR).BeginInit();
            ((System.ComponentModel.ISupportInitialize)_nudMaxTrades).BeginInit();
            ((System.ComponentModel.ISupportInitialize)_nudPollMs).BeginInit();
            ((System.ComponentModel.ISupportInitialize)_nudRetry).BeginInit();
            ((System.ComponentModel.ISupportInitialize)_nudDrawdownPct).BeginInit();
            _tabClaude.SuspendLayout();
            _pnlClaudeCard.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)_nudClaudePollSec).BeginInit();
            _pnlClaudePromptCard.SuspendLayout();
            _tabLog.SuspendLayout();
            SuspendLayout();
            // 
            // _layoutRoot
            // 
            _layoutRoot.ColumnCount = 1;
            _layoutRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _layoutRoot.Controls.Add(_pnlHeader, 0, 0);
            _layoutRoot.Controls.Add(_pnlConnBar, 0, 1);
            _layoutRoot.Controls.Add(_pnlAccountBar, 0, 2);
            _layoutRoot.Controls.Add(_tabControl, 0, 3);
            _layoutRoot.Dock = DockStyle.Fill;
            _layoutRoot.Location = new Point(0, 0);
            _layoutRoot.Name = "_layoutRoot";
            _layoutRoot.RowCount = 4;
            _layoutRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            _layoutRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            _layoutRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
            _layoutRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            _layoutRoot.Size = new Size(1264, 890);
            _layoutRoot.TabIndex = 0;
            // 
            // _pnlHeader
            // 
            _pnlHeader.Controls.Add(_lblTitle);
            _pnlHeader.Controls.Add(_pnlDot);
            _pnlHeader.Controls.Add(_lblConnStatus);
            _pnlHeader.Controls.Add(_lblTime);
            _pnlHeader.Location = new Point(3, 3);
            _pnlHeader.Name = "_pnlHeader";
            _pnlHeader.Size = new Size(200, 46);
            _pnlHeader.TabIndex = 0;
            // 
            // _lblTitle
            // 
            _lblTitle.Location = new Point(0, 0);
            _lblTitle.Name = "_lblTitle";
            _lblTitle.Size = new Size(100, 23);
            _lblTitle.TabIndex = 0;
            // 
            // _pnlDot
            // 
            _pnlDot.Location = new Point(0, 0);
            _pnlDot.Name = "_pnlDot";
            _pnlDot.Size = new Size(200, 100);
            _pnlDot.TabIndex = 1;
            // 
            // _lblConnStatus
            // 
            _lblConnStatus.Location = new Point(0, 0);
            _lblConnStatus.Name = "_lblConnStatus";
            _lblConnStatus.Size = new Size(100, 23);
            _lblConnStatus.TabIndex = 2;
            // 
            // _lblTime
            // 
            _lblTime.Location = new Point(0, 0);
            _lblTime.Name = "_lblTime";
            _lblTime.Size = new Size(100, 23);
            _lblTime.TabIndex = 3;
            // 
            // _pnlConnBar
            // 
            _pnlConnBar.Controls.Add(_cmbMode);
            _pnlConnBar.Controls.Add(_lblPipeLabel);
            _pnlConnBar.Controls.Add(_txtPipeName);
            _pnlConnBar.Controls.Add(_btnConnect);
            _pnlConnBar.Controls.Add(_btnDisconnect);
            _pnlConnBar.Controls.Add(_chkAutoConn);
            _pnlConnBar.Location = new Point(3, 55);
            _pnlConnBar.Name = "_pnlConnBar";
            _pnlConnBar.Size = new Size(200, 34);
            _pnlConnBar.TabIndex = 1;
            // 
            // _cmbMode
            // 
            _cmbMode.Items.AddRange(new object[] { "Named Pipe (Local MT5)", "TCP Socket (Remote)" });
            _cmbMode.Location = new Point(0, 0);
            _cmbMode.Name = "_cmbMode";
            _cmbMode.Size = new Size(121, 23);
            _cmbMode.TabIndex = 0;
            // 
            // _lblPipeLabel
            // 
            _lblPipeLabel.Location = new Point(0, 0);
            _lblPipeLabel.Name = "_lblPipeLabel";
            _lblPipeLabel.Size = new Size(100, 23);
            _lblPipeLabel.TabIndex = 1;
            // 
            // _txtPipeName
            // 
            _txtPipeName.Location = new Point(0, 0);
            _txtPipeName.Name = "_txtPipeName";
            _txtPipeName.Size = new Size(100, 23);
            _txtPipeName.TabIndex = 2;
            // 
            // _btnConnect
            // 
            _btnConnect.BackColor = Color.FromArgb(72, 199, 142);
            _btnConnect.Cursor = Cursors.Hand;
            _btnConnect.FlatAppearance.BorderSize = 0;
            _btnConnect.FlatStyle = FlatStyle.Flat;
            _btnConnect.Font = new Font("Segoe UI Semibold", 9F);
            _btnConnect.ForeColor = Color.FromArgb(10, 10, 20);
            _btnConnect.Location = new Point(502, 7);
            _btnConnect.Name = "_btnConnect";
            _btnConnect.Size = new Size(110, 30);
            _btnConnect.TabIndex = 3;
            _btnConnect.Text = "⚡ Connect";
            _btnConnect.UseVisualStyleBackColor = false;
            // 
            // _btnDisconnect
            // 
            _btnDisconnect.BackColor = Color.FromArgb(252, 95, 95);
            _btnDisconnect.Cursor = Cursors.Hand;
            _btnDisconnect.Enabled = false;
            _btnDisconnect.FlatAppearance.BorderSize = 0;
            _btnDisconnect.FlatStyle = FlatStyle.Flat;
            _btnDisconnect.Font = new Font("Segoe UI Semibold", 9F);
            _btnDisconnect.ForeColor = Color.FromArgb(10, 10, 20);
            _btnDisconnect.Location = new Point(622, 7);
            _btnDisconnect.Name = "_btnDisconnect";
            _btnDisconnect.Size = new Size(100, 30);
            _btnDisconnect.TabIndex = 4;
            _btnDisconnect.Text = "Disconnect";
            _btnDisconnect.UseVisualStyleBackColor = false;
            // 
            // _chkAutoConn
            // 
            _chkAutoConn.Location = new Point(0, 0);
            _chkAutoConn.Name = "_chkAutoConn";
            _chkAutoConn.Size = new Size(104, 24);
            _chkAutoConn.TabIndex = 5;
            // 
            // _pnlAccountBar
            // 
            _pnlAccountBar.Controls.Add(_lblAccNum);
            _pnlAccountBar.Controls.Add(_lblBalance);
            _pnlAccountBar.Controls.Add(_lblEquity);
            _pnlAccountBar.Controls.Add(_lblFreeMargin);
            _pnlAccountBar.Controls.Add(_lblPnl);
            _pnlAccountBar.Controls.Add(_lblMarginLvl);
            _pnlAccountBar.Location = new Point(3, 95);
            _pnlAccountBar.Name = "_pnlAccountBar";
            _pnlAccountBar.Size = new Size(200, 32);
            _pnlAccountBar.TabIndex = 2;
            // 
            // _lblAccNum
            // 
            _lblAccNum.AutoEllipsis = true;
            _lblAccNum.Font = new Font("Segoe UI", 9F);
            _lblAccNum.ForeColor = Color.FromArgb(110, 110, 130);
            _lblAccNum.Location = new Point(8, 12);
            _lblAccNum.Name = "_lblAccNum";
            _lblAccNum.Size = new Size(300, 20);
            _lblAccNum.TabIndex = 0;
            _lblAccNum.Text = "Account: —";
            // 
            // _lblBalance
            // 
            _lblBalance.AutoEllipsis = true;
            _lblBalance.Font = new Font("Segoe UI", 9F);
            _lblBalance.ForeColor = Color.FromArgb(218, 218, 230);
            _lblBalance.Location = new Point(330, 12);
            _lblBalance.Name = "_lblBalance";
            _lblBalance.Size = new Size(130, 20);
            _lblBalance.TabIndex = 1;
            _lblBalance.Text = "Balance: —";
            // 
            // _lblEquity
            // 
            _lblEquity.AutoEllipsis = true;
            _lblEquity.Font = new Font("Segoe UI", 9F);
            _lblEquity.ForeColor = Color.FromArgb(218, 218, 230);
            _lblEquity.Location = new Point(470, 12);
            _lblEquity.Name = "_lblEquity";
            _lblEquity.Size = new Size(130, 20);
            _lblEquity.TabIndex = 2;
            _lblEquity.Text = "Equity: —";
            // 
            // _lblFreeMargin
            // 
            _lblFreeMargin.AutoEllipsis = true;
            _lblFreeMargin.Font = new Font("Segoe UI", 9F);
            _lblFreeMargin.ForeColor = Color.FromArgb(218, 218, 230);
            _lblFreeMargin.Location = new Point(610, 12);
            _lblFreeMargin.Name = "_lblFreeMargin";
            _lblFreeMargin.Size = new Size(150, 20);
            _lblFreeMargin.TabIndex = 3;
            _lblFreeMargin.Text = "Free Margin: —";
            // 
            // _lblPnl
            // 
            _lblPnl.AutoEllipsis = true;
            _lblPnl.Font = new Font("Segoe UI", 9F);
            _lblPnl.ForeColor = Color.FromArgb(218, 218, 230);
            _lblPnl.Location = new Point(770, 12);
            _lblPnl.Name = "_lblPnl";
            _lblPnl.Size = new Size(120, 20);
            _lblPnl.TabIndex = 4;
            _lblPnl.Text = "P&L: —";
            // 
            // _lblMarginLvl
            // 
            _lblMarginLvl.AutoEllipsis = true;
            _lblMarginLvl.Font = new Font("Segoe UI", 9F);
            _lblMarginLvl.ForeColor = Color.FromArgb(110, 110, 130);
            _lblMarginLvl.Location = new Point(900, 12);
            _lblMarginLvl.Name = "_lblMarginLvl";
            _lblMarginLvl.Size = new Size(120, 20);
            _lblMarginLvl.TabIndex = 5;
            _lblMarginLvl.Text = "Margin Lvl: —";
            // 
            // _tabControl
            // 
            _tabControl.Controls.Add(_tabTrade);
            _tabControl.Controls.Add(_tabPositions);
            _tabControl.Controls.Add(_tabHistory);
            _tabControl.Controls.Add(_tabBot);
            _tabControl.Controls.Add(_tabClaude);
            _tabControl.Controls.Add(_tabLog);
            _tabControl.Location = new Point(3, 133);
            _tabControl.Name = "_tabControl";
            _tabControl.SelectedIndex = 0;
            _tabControl.Size = new Size(200, 100);
            _tabControl.TabIndex = 3;
            // 
            // _tabTrade
            // 
            _tabTrade.Controls.Add(_pnlTradeLeft);
            _tabTrade.Controls.Add(_pnlTradeRight);
            _tabTrade.Location = new Point(4, 24);
            _tabTrade.Name = "_tabTrade";
            _tabTrade.Size = new Size(192, 72);
            _tabTrade.TabIndex = 0;
            _tabTrade.Text = "  📈 Trade  ";
            // 
            // _pnlTradeLeft
            // 
            _pnlTradeLeft.Controls.Add(_lblTradeHeader);
            _pnlTradeLeft.Controls.Add(_lblPairLabel);
            _pnlTradeLeft.Controls.Add(_cmbPair);
            _pnlTradeLeft.Controls.Add(_lblDirLabel);
            _pnlTradeLeft.Controls.Add(_cmbDir);
            _pnlTradeLeft.Controls.Add(_lblOrderTypeLabel);
            _pnlTradeLeft.Controls.Add(_cmbOrderType);
            _pnlTradeLeft.Controls.Add(_lblEntryLabel);
            _pnlTradeLeft.Controls.Add(_txtEntry);
            _pnlTradeLeft.Controls.Add(_lblSLLabel);
            _pnlTradeLeft.Controls.Add(_txtSL);
            _pnlTradeLeft.Controls.Add(_lblTPLabel);
            _pnlTradeLeft.Controls.Add(_txtTP);
            _pnlTradeLeft.Controls.Add(_lblTP2Label);
            _pnlTradeLeft.Controls.Add(_txtTP2);
            _pnlTradeLeft.Controls.Add(_chkAutoLot);
            _pnlTradeLeft.Controls.Add(_lblLotLabel);
            _pnlTradeLeft.Controls.Add(_txtLot);
            _pnlTradeLeft.Controls.Add(_chkMoveSLBE);
            _pnlTradeLeft.Controls.Add(_pnlRR);
            _pnlTradeLeft.Controls.Add(_btnBuy);
            _pnlTradeLeft.Controls.Add(_btnSell);
            _pnlTradeLeft.Location = new Point(0, 0);
            _pnlTradeLeft.Name = "_pnlTradeLeft";
            _pnlTradeLeft.Size = new Size(200, 100);
            _pnlTradeLeft.TabIndex = 0;
            // 
            // _lblTradeHeader
            // 
            _lblTradeHeader.AutoSize = true;
            _lblTradeHeader.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            _lblTradeHeader.ForeColor = Color.FromArgb(200, 210, 240);
            _lblTradeHeader.Location = new Point(14, 14);
            _lblTradeHeader.Name = "_lblTradeHeader";
            _lblTradeHeader.Size = new Size(131, 19);
            _lblTradeHeader.TabIndex = 0;
            _lblTradeHeader.Text = "Manual Trade Entry";
            // 
            // _lblPairLabel
            // 
            _lblPairLabel.AutoSize = true;
            _lblPairLabel.Font = new Font("Segoe UI", 9F);
            _lblPairLabel.ForeColor = Color.FromArgb(110, 110, 130);
            _lblPairLabel.Location = new Point(14, 50);
            _lblPairLabel.Name = "_lblPairLabel";
            _lblPairLabel.Size = new Size(27, 15);
            _lblPairLabel.TabIndex = 1;
            _lblPairLabel.Text = "Pair";
            // 
            // _cmbPair
            // 
            _cmbPair.BackColor = Color.FromArgb(22, 22, 32);
            _cmbPair.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbPair.FlatStyle = FlatStyle.Flat;
            _cmbPair.Font = new Font("Segoe UI", 9F);
            _cmbPair.ForeColor = Color.FromArgb(218, 218, 230);
            _cmbPair.Location = new Point(130, 48);
            _cmbPair.Name = "_cmbPair";
            _cmbPair.Size = new Size(210, 23);
            _cmbPair.TabIndex = 2;
            // 
            // _lblDirLabel
            // 
            _lblDirLabel.AutoSize = true;
            _lblDirLabel.Font = new Font("Segoe UI", 9F);
            _lblDirLabel.ForeColor = Color.FromArgb(110, 110, 130);
            _lblDirLabel.Location = new Point(14, 82);
            _lblDirLabel.Name = "_lblDirLabel";
            _lblDirLabel.Size = new Size(55, 15);
            _lblDirLabel.TabIndex = 3;
            _lblDirLabel.Text = "Direction";
            // 
            // _cmbDir
            // 
            _cmbDir.BackColor = Color.FromArgb(22, 22, 32);
            _cmbDir.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbDir.FlatStyle = FlatStyle.Flat;
            _cmbDir.Font = new Font("Segoe UI", 9F);
            _cmbDir.ForeColor = Color.FromArgb(218, 218, 230);
            _cmbDir.Items.AddRange(new object[] { "BUY", "SELL" });
            _cmbDir.Location = new Point(130, 80);
            _cmbDir.Name = "_cmbDir";
            _cmbDir.Size = new Size(210, 23);
            _cmbDir.TabIndex = 4;
            // 
            // _lblOrderTypeLabel
            // 
            _lblOrderTypeLabel.AutoSize = true;
            _lblOrderTypeLabel.Font = new Font("Segoe UI", 9F);
            _lblOrderTypeLabel.ForeColor = Color.FromArgb(110, 110, 130);
            _lblOrderTypeLabel.Location = new Point(14, 114);
            _lblOrderTypeLabel.Name = "_lblOrderTypeLabel";
            _lblOrderTypeLabel.Size = new Size(65, 15);
            _lblOrderTypeLabel.TabIndex = 5;
            _lblOrderTypeLabel.Text = "Order Type";
            // 
            // _cmbOrderType
            // 
            _cmbOrderType.BackColor = Color.FromArgb(22, 22, 32);
            _cmbOrderType.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbOrderType.FlatStyle = FlatStyle.Flat;
            _cmbOrderType.Font = new Font("Segoe UI", 9F);
            _cmbOrderType.ForeColor = Color.FromArgb(218, 218, 230);
            _cmbOrderType.Items.AddRange(new object[] { "MARKET", "LIMIT", "STOP" });
            _cmbOrderType.Location = new Point(130, 112);
            _cmbOrderType.Name = "_cmbOrderType";
            _cmbOrderType.Size = new Size(210, 23);
            _cmbOrderType.TabIndex = 6;
            // 
            // _lblEntryLabel
            // 
            _lblEntryLabel.AutoSize = true;
            _lblEntryLabel.Font = new Font("Segoe UI", 9F);
            _lblEntryLabel.ForeColor = Color.FromArgb(110, 110, 130);
            _lblEntryLabel.Location = new Point(14, 146);
            _lblEntryLabel.Name = "_lblEntryLabel";
            _lblEntryLabel.Size = new Size(63, 15);
            _lblEntryLabel.TabIndex = 7;
            _lblEntryLabel.Text = "Entry Price";
            // 
            // _txtEntry
            // 
            _txtEntry.BackColor = Color.FromArgb(22, 22, 32);
            _txtEntry.BorderStyle = BorderStyle.FixedSingle;
            _txtEntry.Enabled = false;
            _txtEntry.Font = new Font("Consolas", 9F);
            _txtEntry.ForeColor = Color.FromArgb(218, 218, 230);
            _txtEntry.Location = new Point(130, 144);
            _txtEntry.Name = "_txtEntry";
            _txtEntry.Size = new Size(210, 22);
            _txtEntry.TabIndex = 8;
            _txtEntry.Text = "0 (market)";
            // 
            // _lblSLLabel
            // 
            _lblSLLabel.AutoSize = true;
            _lblSLLabel.Font = new Font("Segoe UI", 9F);
            _lblSLLabel.ForeColor = Color.FromArgb(252, 95, 95);
            _lblSLLabel.Location = new Point(14, 178);
            _lblSLLabel.Name = "_lblSLLabel";
            _lblSLLabel.Size = new Size(71, 15);
            _lblSLLabel.TabIndex = 9;
            _lblSLLabel.Text = "Stop Loss ✱";
            // 
            // _txtSL
            // 
            _txtSL.BackColor = Color.FromArgb(22, 22, 32);
            _txtSL.BorderStyle = BorderStyle.FixedSingle;
            _txtSL.Font = new Font("Consolas", 9F);
            _txtSL.ForeColor = Color.FromArgb(218, 218, 230);
            _txtSL.Location = new Point(130, 176);
            _txtSL.Name = "_txtSL";
            _txtSL.Size = new Size(210, 22);
            _txtSL.TabIndex = 10;
            _txtSL.Text = "e.g. 1.34750";
            // 
            // _lblTPLabel
            // 
            _lblTPLabel.AutoSize = true;
            _lblTPLabel.Font = new Font("Segoe UI", 9F);
            _lblTPLabel.ForeColor = Color.FromArgb(72, 199, 142);
            _lblTPLabel.Location = new Point(14, 210);
            _lblTPLabel.Name = "_lblTPLabel";
            _lblTPLabel.Size = new Size(77, 15);
            _lblTPLabel.TabIndex = 11;
            _lblTPLabel.Text = "Take Profit ✱";
            // 
            // _txtTP
            // 
            _txtTP.BackColor = Color.FromArgb(22, 22, 32);
            _txtTP.BorderStyle = BorderStyle.FixedSingle;
            _txtTP.Font = new Font("Consolas", 9F);
            _txtTP.ForeColor = Color.FromArgb(218, 218, 230);
            _txtTP.Location = new Point(130, 208);
            _txtTP.Name = "_txtTP";
            _txtTP.Size = new Size(210, 22);
            _txtTP.TabIndex = 12;
            _txtTP.Text = "e.g. 1.35200";
            // 
            // _lblTP2Label
            // 
            _lblTP2Label.AutoSize = true;
            _lblTP2Label.Font = new Font("Segoe UI", 9F);
            _lblTP2Label.ForeColor = Color.FromArgb(99, 179, 237);
            _lblTP2Label.Location = new Point(14, 242);
            _lblTP2Label.Name = "_lblTP2Label";
            _lblTP2Label.Size = new Size(72, 15);
            _lblTP2Label.TabIndex = 13;
            _lblTP2Label.Text = "Take Profit 2";
            // 
            // _txtTP2
            // 
            _txtTP2.BackColor = Color.FromArgb(22, 22, 32);
            _txtTP2.BorderStyle = BorderStyle.FixedSingle;
            _txtTP2.Font = new Font("Consolas", 9F);
            _txtTP2.ForeColor = Color.FromArgb(218, 218, 230);
            _txtTP2.Location = new Point(130, 240);
            _txtTP2.Name = "_txtTP2";
            _txtTP2.Size = new Size(210, 22);
            _txtTP2.TabIndex = 14;
            _txtTP2.Text = "0 (optional)";
            // 
            // _chkAutoLot
            // 
            _chkAutoLot.Location = new Point(0, 0);
            _chkAutoLot.Name = "_chkAutoLot";
            _chkAutoLot.Size = new Size(104, 24);
            _chkAutoLot.TabIndex = 15;
            // 
            // _lblLotLabel
            // 
            _lblLotLabel.AutoSize = true;
            _lblLotLabel.Font = new Font("Segoe UI", 9F);
            _lblLotLabel.ForeColor = Color.FromArgb(110, 110, 130);
            _lblLotLabel.Location = new Point(14, 302);
            _lblLotLabel.Name = "_lblLotLabel";
            _lblLotLabel.Size = new Size(47, 15);
            _lblLotLabel.TabIndex = 16;
            _lblLotLabel.Text = "Lot Size";
            // 
            // _txtLot
            // 
            _txtLot.BackColor = Color.FromArgb(22, 22, 32);
            _txtLot.BorderStyle = BorderStyle.FixedSingle;
            _txtLot.Enabled = false;
            _txtLot.Font = new Font("Consolas", 9F);
            _txtLot.ForeColor = Color.FromArgb(218, 218, 230);
            _txtLot.Location = new Point(130, 300);
            _txtLot.Name = "_txtLot";
            _txtLot.Size = new Size(100, 22);
            _txtLot.TabIndex = 17;
            _txtLot.Text = "0.01";
            // 
            // _chkMoveSLBE
            // 
            _chkMoveSLBE.Location = new Point(0, 0);
            _chkMoveSLBE.Name = "_chkMoveSLBE";
            _chkMoveSLBE.Size = new Size(104, 24);
            _chkMoveSLBE.TabIndex = 18;
            // 
            // _pnlRR
            // 
            _pnlRR.Controls.Add(_lblRR);
            _pnlRR.Controls.Add(_lblDollarRisk);
            _pnlRR.Controls.Add(_lblDollarProfit);
            _pnlRR.Location = new Point(0, 0);
            _pnlRR.Name = "_pnlRR";
            _pnlRR.Size = new Size(200, 100);
            _pnlRR.TabIndex = 19;
            // 
            // _lblRR
            // 
            _lblRR.Location = new Point(0, 0);
            _lblRR.Name = "_lblRR";
            _lblRR.Size = new Size(100, 23);
            _lblRR.TabIndex = 0;
            // 
            // _lblDollarRisk
            // 
            _lblDollarRisk.Location = new Point(0, 0);
            _lblDollarRisk.Name = "_lblDollarRisk";
            _lblDollarRisk.Size = new Size(100, 23);
            _lblDollarRisk.TabIndex = 1;
            // 
            // _lblDollarProfit
            // 
            _lblDollarProfit.Location = new Point(0, 0);
            _lblDollarProfit.Name = "_lblDollarProfit";
            _lblDollarProfit.Size = new Size(100, 23);
            _lblDollarProfit.TabIndex = 2;
            // 
            // _btnBuy
            // 
            _btnBuy.BackColor = Color.FromArgb(72, 199, 142);
            _btnBuy.Cursor = Cursors.Hand;
            _btnBuy.FlatAppearance.BorderSize = 0;
            _btnBuy.FlatStyle = FlatStyle.Flat;
            _btnBuy.Font = new Font("Segoe UI Semibold", 11F);
            _btnBuy.ForeColor = Color.FromArgb(10, 10, 20);
            _btnBuy.Location = new Point(14, 424);
            _btnBuy.Name = "_btnBuy";
            _btnBuy.Size = new Size(152, 44);
            _btnBuy.TabIndex = 20;
            _btnBuy.Text = "▲  BUY";
            _btnBuy.UseVisualStyleBackColor = false;
            // 
            // _btnSell
            // 
            _btnSell.BackColor = Color.FromArgb(252, 95, 95);
            _btnSell.Cursor = Cursors.Hand;
            _btnSell.FlatAppearance.BorderSize = 0;
            _btnSell.FlatStyle = FlatStyle.Flat;
            _btnSell.Font = new Font("Segoe UI Semibold", 11F);
            _btnSell.ForeColor = Color.FromArgb(10, 10, 20);
            _btnSell.Location = new Point(174, 424);
            _btnSell.Name = "_btnSell";
            _btnSell.Size = new Size(152, 44);
            _btnSell.TabIndex = 21;
            _btnSell.Text = "▼  SELL";
            _btnSell.UseVisualStyleBackColor = false;
            // 
            // _pnlTradeRight
            // 
            _pnlTradeRight.Controls.Add(_lblJsonHeader);
            _pnlTradeRight.Controls.Add(_txtJson);
            _pnlTradeRight.Controls.Add(_btnJsonLoad);
            _pnlTradeRight.Controls.Add(_btnJsonExec);
            _pnlTradeRight.Controls.Add(_btnJsonFmt);
            _pnlTradeRight.Controls.Add(_btnJsonSample);
            _pnlTradeRight.Location = new Point(0, 0);
            _pnlTradeRight.Name = "_pnlTradeRight";
            _pnlTradeRight.Size = new Size(200, 100);
            _pnlTradeRight.TabIndex = 1;
            // 
            // _lblJsonHeader
            // 
            _lblJsonHeader.AutoSize = true;
            _lblJsonHeader.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            _lblJsonHeader.ForeColor = Color.FromArgb(200, 210, 240);
            _lblJsonHeader.Location = new Point(14, 14);
            _lblJsonHeader.Name = "_lblJsonHeader";
            _lblJsonHeader.Size = new Size(287, 19);
            _lblJsonHeader.TabIndex = 0;
            _lblJsonHeader.Text = "JSON Signal Input  (paste, load file, or drop)";
            // 
            // _txtJson
            // 
            _txtJson.Location = new Point(0, 0);
            _txtJson.Name = "_txtJson";
            _txtJson.Size = new Size(100, 96);
            _txtJson.TabIndex = 1;
            _txtJson.Text = "";
            // 
            // _btnJsonLoad
            // 
            _btnJsonLoad.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            _btnJsonLoad.BackColor = Color.FromArgb(99, 179, 237);
            _btnJsonLoad.Cursor = Cursors.Hand;
            _btnJsonLoad.FlatAppearance.BorderSize = 0;
            _btnJsonLoad.FlatStyle = FlatStyle.Flat;
            _btnJsonLoad.Font = new Font("Segoe UI Semibold", 9F);
            _btnJsonLoad.ForeColor = Color.FromArgb(10, 10, 20);
            _btnJsonLoad.Location = new Point(14, 600);
            _btnJsonLoad.Name = "_btnJsonLoad";
            _btnJsonLoad.Size = new Size(130, 30);
            _btnJsonLoad.TabIndex = 2;
            _btnJsonLoad.Text = "📂 Load File";
            _btnJsonLoad.UseVisualStyleBackColor = false;
            // 
            // _btnJsonExec
            // 
            _btnJsonExec.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            _btnJsonExec.BackColor = Color.FromArgb(72, 199, 142);
            _btnJsonExec.Cursor = Cursors.Hand;
            _btnJsonExec.FlatAppearance.BorderSize = 0;
            _btnJsonExec.FlatStyle = FlatStyle.Flat;
            _btnJsonExec.Font = new Font("Segoe UI Semibold", 9F);
            _btnJsonExec.ForeColor = Color.FromArgb(10, 10, 20);
            _btnJsonExec.Location = new Point(154, 600);
            _btnJsonExec.Name = "_btnJsonExec";
            _btnJsonExec.Size = new Size(140, 30);
            _btnJsonExec.TabIndex = 3;
            _btnJsonExec.Text = "⚡ Execute JSON";
            _btnJsonExec.UseVisualStyleBackColor = false;
            // 
            // _btnJsonFmt
            // 
            _btnJsonFmt.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            _btnJsonFmt.BackColor = Color.FromArgb(250, 199, 117);
            _btnJsonFmt.Cursor = Cursors.Hand;
            _btnJsonFmt.FlatAppearance.BorderSize = 0;
            _btnJsonFmt.FlatStyle = FlatStyle.Flat;
            _btnJsonFmt.Font = new Font("Segoe UI Semibold", 9F);
            _btnJsonFmt.ForeColor = Color.FromArgb(10, 10, 20);
            _btnJsonFmt.Location = new Point(304, 600);
            _btnJsonFmt.Name = "_btnJsonFmt";
            _btnJsonFmt.Size = new Size(100, 30);
            _btnJsonFmt.TabIndex = 4;
            _btnJsonFmt.Text = "🔧 Format";
            _btnJsonFmt.UseVisualStyleBackColor = false;
            // 
            // _btnJsonSample
            // 
            _btnJsonSample.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            _btnJsonSample.BackColor = Color.FromArgb(110, 110, 130);
            _btnJsonSample.Cursor = Cursors.Hand;
            _btnJsonSample.FlatAppearance.BorderSize = 0;
            _btnJsonSample.FlatStyle = FlatStyle.Flat;
            _btnJsonSample.Font = new Font("Segoe UI Semibold", 9F);
            _btnJsonSample.ForeColor = Color.FromArgb(10, 10, 20);
            _btnJsonSample.Location = new Point(414, 600);
            _btnJsonSample.Name = "_btnJsonSample";
            _btnJsonSample.Size = new Size(100, 30);
            _btnJsonSample.TabIndex = 5;
            _btnJsonSample.Text = "📋 Sample";
            _btnJsonSample.UseVisualStyleBackColor = false;
            // 
            // _tabPositions
            // 
            _tabPositions.Controls.Add(_gridPos);
            _tabPositions.Controls.Add(_btnClosePos);
            _tabPositions.Controls.Add(_btnCloseAllPos);
            _tabPositions.Controls.Add(_btnRefreshPos);
            _tabPositions.Location = new Point(4, 24);
            _tabPositions.Name = "_tabPositions";
            _tabPositions.Size = new Size(192, 72);
            _tabPositions.TabIndex = 1;
            _tabPositions.Text = "  📊 Positions  ";
            // 
            // _gridPos
            // 
            _gridPos.AllowUserToAddRows = false;
            dataGridViewCellStyle1.BackColor = Color.FromArgb(20, 20, 30);
            _gridPos.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle1;
            _gridPos.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _gridPos.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _gridPos.BackgroundColor = Color.FromArgb(24, 25, 38);
            _gridPos.BorderStyle = BorderStyle.None;
            _gridPos.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            _gridPos.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            dataGridViewCellStyle2.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = Color.FromArgb(18, 18, 28);
            dataGridViewCellStyle2.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            dataGridViewCellStyle2.ForeColor = Color.FromArgb(110, 110, 140);
            dataGridViewCellStyle2.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle2.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = DataGridViewTriState.True;
            _gridPos.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle2;
            _gridPos.Columns.AddRange(new DataGridViewColumn[] { dataGridViewTextBoxColumn1, dataGridViewTextBoxColumn2, dataGridViewTextBoxColumn3, dataGridViewTextBoxColumn4, dataGridViewTextBoxColumn5, dataGridViewTextBoxColumn6, dataGridViewTextBoxColumn7, dataGridViewTextBoxColumn8, dataGridViewTextBoxColumn9, dataGridViewTextBoxColumn10, dataGridViewTextBoxColumn11, dataGridViewTextBoxColumn12 });
            dataGridViewCellStyle3.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = Color.FromArgb(24, 25, 38);
            dataGridViewCellStyle3.Font = new Font("Consolas", 8.5F);
            dataGridViewCellStyle3.ForeColor = Color.FromArgb(218, 218, 230);
            dataGridViewCellStyle3.SelectionBackColor = Color.FromArgb(40, 99, 179, 237);
            dataGridViewCellStyle3.SelectionForeColor = Color.FromArgb(218, 218, 230);
            dataGridViewCellStyle3.WrapMode = DataGridViewTriState.False;
            _gridPos.DefaultCellStyle = dataGridViewCellStyle3;
            _gridPos.Font = new Font("Consolas", 8.5F);
            _gridPos.GridColor = Color.FromArgb(45, 45, 65);
            _gridPos.Location = new Point(12, 8);
            _gridPos.MultiSelect = false;
            _gridPos.Name = "_gridPos";
            _gridPos.ReadOnly = true;
            _gridPos.RowHeadersVisible = false;
            _gridPos.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _gridPos.Size = new Size(1220, 580);
            _gridPos.TabIndex = 0;
            // 
            // _btnClosePos
            // 
            _btnClosePos.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            _btnClosePos.BackColor = Color.FromArgb(252, 95, 95);
            _btnClosePos.Cursor = Cursors.Hand;
            _btnClosePos.FlatAppearance.BorderSize = 0;
            _btnClosePos.FlatStyle = FlatStyle.Flat;
            _btnClosePos.Font = new Font("Segoe UI Semibold", 9F);
            _btnClosePos.ForeColor = Color.FromArgb(10, 10, 20);
            _btnClosePos.Location = new Point(12, 600);
            _btnClosePos.Name = "_btnClosePos";
            _btnClosePos.Size = new Size(150, 30);
            _btnClosePos.TabIndex = 1;
            _btnClosePos.Text = "Close Selected";
            _btnClosePos.UseVisualStyleBackColor = false;
            // 
            // _btnCloseAllPos
            // 
            _btnCloseAllPos.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            _btnCloseAllPos.BackColor = Color.FromArgb(160, 50, 50);
            _btnCloseAllPos.Cursor = Cursors.Hand;
            _btnCloseAllPos.FlatAppearance.BorderSize = 0;
            _btnCloseAllPos.FlatStyle = FlatStyle.Flat;
            _btnCloseAllPos.Font = new Font("Segoe UI Semibold", 9F);
            _btnCloseAllPos.ForeColor = Color.FromArgb(10, 10, 20);
            _btnCloseAllPos.Location = new Point(172, 600);
            _btnCloseAllPos.Name = "_btnCloseAllPos";
            _btnCloseAllPos.Size = new Size(130, 30);
            _btnCloseAllPos.TabIndex = 2;
            _btnCloseAllPos.Text = "Close ALL";
            _btnCloseAllPos.UseVisualStyleBackColor = false;
            // 
            // _btnRefreshPos
            // 
            _btnRefreshPos.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            _btnRefreshPos.BackColor = Color.FromArgb(99, 179, 237);
            _btnRefreshPos.Cursor = Cursors.Hand;
            _btnRefreshPos.FlatAppearance.BorderSize = 0;
            _btnRefreshPos.FlatStyle = FlatStyle.Flat;
            _btnRefreshPos.Font = new Font("Segoe UI Semibold", 9F);
            _btnRefreshPos.ForeColor = Color.FromArgb(10, 10, 20);
            _btnRefreshPos.Location = new Point(312, 600);
            _btnRefreshPos.Name = "_btnRefreshPos";
            _btnRefreshPos.Size = new Size(110, 30);
            _btnRefreshPos.TabIndex = 3;
            _btnRefreshPos.Text = "🔄 Refresh";
            _btnRefreshPos.UseVisualStyleBackColor = false;
            // 
            // _tabHistory
            // 
            _tabHistory.Controls.Add(_gridHistory);
            _tabHistory.Controls.Add(_btnImportHistory);
            _tabHistory.Controls.Add(_btnClearHistory);
            _tabHistory.Location = new Point(4, 24);
            _tabHistory.Name = "_tabHistory";
            _tabHistory.Size = new Size(192, 72);
            _tabHistory.TabIndex = 2;
            _tabHistory.Text = "  🗂 History  ";
            // 
            // _gridHistory
            // 
            _gridHistory.AllowUserToAddRows = false;
            dataGridViewCellStyle4.BackColor = Color.FromArgb(20, 20, 30);
            _gridHistory.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle4;
            _gridHistory.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _gridHistory.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _gridHistory.BackgroundColor = Color.FromArgb(24, 25, 38);
            _gridHistory.BorderStyle = BorderStyle.None;
            _gridHistory.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            _gridHistory.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            dataGridViewCellStyle5.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle5.BackColor = Color.FromArgb(18, 18, 28);
            dataGridViewCellStyle5.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            dataGridViewCellStyle5.ForeColor = Color.FromArgb(110, 110, 140);
            dataGridViewCellStyle5.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle5.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle5.WrapMode = DataGridViewTriState.True;
            _gridHistory.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle5;
            _gridHistory.Columns.AddRange(new DataGridViewColumn[] { dataGridViewTextBoxColumn13, dataGridViewTextBoxColumn14, dataGridViewTextBoxColumn15, dataGridViewTextBoxColumn16, dataGridViewTextBoxColumn17, dataGridViewTextBoxColumn18, dataGridViewTextBoxColumn19, dataGridViewTextBoxColumn20, dataGridViewTextBoxColumn21, dataGridViewTextBoxColumn22, dataGridViewTextBoxColumn23, dataGridViewTextBoxColumn24 });
            dataGridViewCellStyle6.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle6.BackColor = Color.FromArgb(24, 25, 38);
            dataGridViewCellStyle6.Font = new Font("Consolas", 8.5F);
            dataGridViewCellStyle6.ForeColor = Color.FromArgb(218, 218, 230);
            dataGridViewCellStyle6.SelectionBackColor = Color.FromArgb(40, 99, 179, 237);
            dataGridViewCellStyle6.SelectionForeColor = Color.FromArgb(218, 218, 230);
            dataGridViewCellStyle6.WrapMode = DataGridViewTriState.False;
            _gridHistory.DefaultCellStyle = dataGridViewCellStyle6;
            _gridHistory.Font = new Font("Consolas", 8.5F);
            _gridHistory.GridColor = Color.FromArgb(45, 45, 65);
            _gridHistory.Location = new Point(12, 8);
            _gridHistory.MultiSelect = false;
            _gridHistory.Name = "_gridHistory";
            _gridHistory.ReadOnly = true;
            _gridHistory.RowHeadersVisible = false;
            _gridHistory.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _gridHistory.Size = new Size(1220, 580);
            _gridHistory.TabIndex = 0;
            // 
            // _btnImportHistory
            // 
            _btnImportHistory.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            _btnImportHistory.BackColor = Color.FromArgb(99, 179, 237);
            _btnImportHistory.Cursor = Cursors.Hand;
            _btnImportHistory.FlatAppearance.BorderSize = 0;
            _btnImportHistory.FlatStyle = FlatStyle.Flat;
            _btnImportHistory.Font = new Font("Segoe UI Semibold", 9F);
            _btnImportHistory.ForeColor = Color.FromArgb(10, 10, 20);
            _btnImportHistory.Location = new Point(12, 600);
            _btnImportHistory.Name = "_btnImportHistory";
            _btnImportHistory.Size = new Size(150, 30);
            _btnImportHistory.TabIndex = 1;
            _btnImportHistory.Text = "📂 Load CSV Log";
            _btnImportHistory.UseVisualStyleBackColor = false;
            // 
            // _btnClearHistory
            // 
            _btnClearHistory.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            _btnClearHistory.BackColor = Color.FromArgb(110, 110, 130);
            _btnClearHistory.Cursor = Cursors.Hand;
            _btnClearHistory.FlatAppearance.BorderSize = 0;
            _btnClearHistory.FlatStyle = FlatStyle.Flat;
            _btnClearHistory.Font = new Font("Segoe UI Semibold", 9F);
            _btnClearHistory.ForeColor = Color.FromArgb(10, 10, 20);
            _btnClearHistory.Location = new Point(172, 600);
            _btnClearHistory.Name = "_btnClearHistory";
            _btnClearHistory.Size = new Size(80, 30);
            _btnClearHistory.TabIndex = 2;
            _btnClearHistory.Text = "Clear";
            _btnClearHistory.UseVisualStyleBackColor = false;
            // 
            // _tabBot
            // 
            _tabBot.Controls.Add(_lblBotBadge);
            _tabBot.Controls.Add(_pnlBotCard);
            _tabBot.Controls.Add(_btnOpenFolder);
            _tabBot.Controls.Add(_btnBotInstructions);
            _tabBot.Location = new Point(4, 24);
            _tabBot.Name = "_tabBot";
            _tabBot.Size = new Size(192, 72);
            _tabBot.TabIndex = 3;
            _tabBot.Text = "  🤖 Auto Bot  ";
            // 
            // _lblBotBadge
            // 
            _lblBotBadge.Location = new Point(0, 0);
            _lblBotBadge.Name = "_lblBotBadge";
            _lblBotBadge.Size = new Size(100, 23);
            _lblBotBadge.TabIndex = 0;
            // 
            // _pnlBotCard
            // 
            _pnlBotCard.Controls.Add(_lblBotCardHeader);
            _pnlBotCard.Controls.Add(_lblWatchFolderLabel);
            _pnlBotCard.Controls.Add(_txtWatchFolder);
            _pnlBotCard.Controls.Add(_lblRiskLabel);
            _pnlBotCard.Controls.Add(_nudRisk);
            _pnlBotCard.Controls.Add(_lblMinRRLabel);
            _pnlBotCard.Controls.Add(_nudMinRR);
            _pnlBotCard.Controls.Add(_lblMaxTradesLabel);
            _pnlBotCard.Controls.Add(_nudMaxTrades);
            _pnlBotCard.Controls.Add(_lblPollMsLabel);
            _pnlBotCard.Controls.Add(_nudPollMs);
            _pnlBotCard.Controls.Add(_lblRetryLabel);
            _pnlBotCard.Controls.Add(_nudRetry);
            _pnlBotCard.Controls.Add(_lblAllowedPairsLabel);
            _pnlBotCard.Controls.Add(_cmbAllowedPair);
            _pnlBotCard.Controls.Add(_lblDrawdownLabel);
            _pnlBotCard.Controls.Add(_nudDrawdownPct);
            _pnlBotCard.Controls.Add(_chkAutoLotBot);
            _pnlBotCard.Controls.Add(_chkEnforceRR);
            _pnlBotCard.Controls.Add(_chkDrawdown);
            _pnlBotCard.Controls.Add(_chkAutoStart);
            _pnlBotCard.Controls.Add(_btnStartBot);
            _pnlBotCard.Location = new Point(0, 0);
            _pnlBotCard.Name = "_pnlBotCard";
            _pnlBotCard.Size = new Size(200, 100);
            _pnlBotCard.TabIndex = 1;
            // 
            // _lblBotCardHeader
            // 
            _lblBotCardHeader.AutoSize = true;
            _lblBotCardHeader.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            _lblBotCardHeader.ForeColor = Color.FromArgb(200, 210, 240);
            _lblBotCardHeader.Location = new Point(14, 14);
            _lblBotCardHeader.Name = "_lblBotCardHeader";
            _lblBotCardHeader.Size = new Size(121, 19);
            _lblBotCardHeader.TabIndex = 0;
            _lblBotCardHeader.Text = "Bot Configuration";
            // 
            // _lblWatchFolderLabel
            // 
            _lblWatchFolderLabel.AutoSize = true;
            _lblWatchFolderLabel.Font = new Font("Segoe UI", 9F);
            _lblWatchFolderLabel.ForeColor = Color.FromArgb(110, 110, 130);
            _lblWatchFolderLabel.Location = new Point(14, 50);
            _lblWatchFolderLabel.Name = "_lblWatchFolderLabel";
            _lblWatchFolderLabel.Size = new Size(77, 15);
            _lblWatchFolderLabel.TabIndex = 1;
            _lblWatchFolderLabel.Text = "Watch Folder";
            // 
            // _txtWatchFolder
            // 
            _txtWatchFolder.BackColor = Color.FromArgb(22, 22, 32);
            _txtWatchFolder.BorderStyle = BorderStyle.FixedSingle;
            _txtWatchFolder.Font = new Font("Consolas", 9F);
            _txtWatchFolder.ForeColor = Color.FromArgb(218, 218, 230);
            _txtWatchFolder.Location = new Point(200, 48);
            _txtWatchFolder.Name = "_txtWatchFolder";
            _txtWatchFolder.Size = new Size(330, 22);
            _txtWatchFolder.TabIndex = 2;
            _txtWatchFolder.Text = "C:\\MT5Bot\\signals";
            // 
            // _lblRiskLabel
            // 
            _lblRiskLabel.AutoSize = true;
            _lblRiskLabel.Font = new Font("Segoe UI", 9F);
            _lblRiskLabel.ForeColor = Color.FromArgb(110, 110, 130);
            _lblRiskLabel.Location = new Point(14, 82);
            _lblRiskLabel.Name = "_lblRiskLabel";
            _lblRiskLabel.Size = new Size(66, 15);
            _lblRiskLabel.TabIndex = 3;
            _lblRiskLabel.Text = "Max Risk %";
            // 
            // _nudRisk
            // 
            _nudRisk.BackColor = Color.FromArgb(22, 22, 32);
            _nudRisk.BorderStyle = BorderStyle.None;
            _nudRisk.DecimalPlaces = 1;
            _nudRisk.ForeColor = Color.FromArgb(218, 218, 230);
            _nudRisk.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
            _nudRisk.Location = new Point(200, 80);
            _nudRisk.Maximum = new decimal(new int[] { 10, 0, 0, 0 });
            _nudRisk.Minimum = new decimal(new int[] { 1, 0, 0, 65536 });
            _nudRisk.Name = "_nudRisk";
            _nudRisk.Size = new Size(100, 19);
            _nudRisk.TabIndex = 4;
            _nudRisk.Value = new decimal(new int[] { 1, 0, 0, 0 });
            // 
            // _lblMinRRLabel
            // 
            _lblMinRRLabel.AutoSize = true;
            _lblMinRRLabel.Font = new Font("Segoe UI", 9F);
            _lblMinRRLabel.ForeColor = Color.FromArgb(110, 110, 130);
            _lblMinRRLabel.Location = new Point(14, 114);
            _lblMinRRLabel.Name = "_lblMinRRLabel";
            _lblMinRRLabel.Size = new Size(78, 15);
            _lblMinRRLabel.TabIndex = 5;
            _lblMinRRLabel.Text = "Min R:R Ratio";
            // 
            // _nudMinRR
            // 
            _nudMinRR.BackColor = Color.FromArgb(22, 22, 32);
            _nudMinRR.BorderStyle = BorderStyle.None;
            _nudMinRR.DecimalPlaces = 1;
            _nudMinRR.ForeColor = Color.FromArgb(218, 218, 230);
            _nudMinRR.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
            _nudMinRR.Location = new Point(200, 112);
            _nudMinRR.Maximum = new decimal(new int[] { 10, 0, 0, 0 });
            _nudMinRR.Minimum = new decimal(new int[] { 5, 0, 0, 65536 });
            _nudMinRR.Name = "_nudMinRR";
            _nudMinRR.Size = new Size(100, 19);
            _nudMinRR.TabIndex = 6;
            _nudMinRR.Value = new decimal(new int[] { 15, 0, 0, 65536 });
            // 
            // _lblMaxTradesLabel
            // 
            _lblMaxTradesLabel.AutoSize = true;
            _lblMaxTradesLabel.Font = new Font("Segoe UI", 9F);
            _lblMaxTradesLabel.ForeColor = Color.FromArgb(110, 110, 130);
            _lblMaxTradesLabel.Location = new Point(14, 146);
            _lblMaxTradesLabel.Name = "_lblMaxTradesLabel";
            _lblMaxTradesLabel.Size = new Size(91, 15);
            _lblMaxTradesLabel.TabIndex = 7;
            _lblMaxTradesLabel.Text = "Max Trades/Day";
            // 
            // _nudMaxTrades
            // 
            _nudMaxTrades.BackColor = Color.FromArgb(22, 22, 32);
            _nudMaxTrades.BorderStyle = BorderStyle.None;
            _nudMaxTrades.ForeColor = Color.FromArgb(218, 218, 230);
            _nudMaxTrades.Location = new Point(200, 144);
            _nudMaxTrades.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            _nudMaxTrades.Name = "_nudMaxTrades";
            _nudMaxTrades.Size = new Size(100, 19);
            _nudMaxTrades.TabIndex = 8;
            _nudMaxTrades.Value = new decimal(new int[] { 5, 0, 0, 0 });
            // 
            // _lblPollMsLabel
            // 
            _lblPollMsLabel.AutoSize = true;
            _lblPollMsLabel.Font = new Font("Segoe UI", 9F);
            _lblPollMsLabel.ForeColor = Color.FromArgb(110, 110, 130);
            _lblPollMsLabel.Location = new Point(14, 178);
            _lblPollMsLabel.Name = "_lblPollMsLabel";
            _lblPollMsLabel.Size = new Size(88, 15);
            _lblPollMsLabel.TabIndex = 9;
            _lblPollMsLabel.Text = "Poll Interval ms";
            // 
            // _nudPollMs
            // 
            _nudPollMs.BackColor = Color.FromArgb(22, 22, 32);
            _nudPollMs.BorderStyle = BorderStyle.None;
            _nudPollMs.ForeColor = Color.FromArgb(218, 218, 230);
            _nudPollMs.Increment = new decimal(new int[] { 500, 0, 0, 0 });
            _nudPollMs.Location = new Point(200, 176);
            _nudPollMs.Maximum = new decimal(new int[] { 30000, 0, 0, 0 });
            _nudPollMs.Minimum = new decimal(new int[] { 500, 0, 0, 0 });
            _nudPollMs.Name = "_nudPollMs";
            _nudPollMs.Size = new Size(120, 19);
            _nudPollMs.TabIndex = 10;
            _nudPollMs.Value = new decimal(new int[] { 2000, 0, 0, 0 });
            // 
            // _lblRetryLabel
            // 
            _lblRetryLabel.AutoSize = true;
            _lblRetryLabel.Font = new Font("Segoe UI", 9F);
            _lblRetryLabel.ForeColor = Color.FromArgb(110, 110, 130);
            _lblRetryLabel.Location = new Point(14, 210);
            _lblRetryLabel.Name = "_lblRetryLabel";
            _lblRetryLabel.Size = new Size(70, 15);
            _lblRetryLabel.TabIndex = 11;
            _lblRetryLabel.Text = "Retry Count";
            // 
            // _nudRetry
            // 
            _nudRetry.BackColor = Color.FromArgb(22, 22, 32);
            _nudRetry.BorderStyle = BorderStyle.None;
            _nudRetry.ForeColor = Color.FromArgb(218, 218, 230);
            _nudRetry.Location = new Point(200, 208);
            _nudRetry.Maximum = new decimal(new int[] { 10, 0, 0, 0 });
            _nudRetry.Name = "_nudRetry";
            _nudRetry.Size = new Size(100, 19);
            _nudRetry.TabIndex = 12;
            _nudRetry.Value = new decimal(new int[] { 3, 0, 0, 0 });
            // 
            // _lblAllowedPairsLabel
            // 
            _lblAllowedPairsLabel.AutoSize = true;
            _lblAllowedPairsLabel.Font = new Font("Segoe UI", 9F);
            _lblAllowedPairsLabel.ForeColor = Color.FromArgb(110, 110, 130);
            _lblAllowedPairsLabel.Location = new Point(14, 242);
            _lblAllowedPairsLabel.Name = "_lblAllowedPairsLabel";
            _lblAllowedPairsLabel.Size = new Size(73, 15);
            _lblAllowedPairsLabel.TabIndex = 13;
            _lblAllowedPairsLabel.Text = "Allowed Pair";
            // 
            // _cmbAllowedPair
            //
            _cmbAllowedPair.BackColor = Color.FromArgb(22, 22, 32);
            _cmbAllowedPair.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbAllowedPair.FlatStyle = FlatStyle.Flat;
            _cmbAllowedPair.Font = new Font("Consolas", 9F);
            _cmbAllowedPair.ForeColor = Color.FromArgb(218, 218, 230);
            _cmbAllowedPair.Location = new Point(176, 240);
            _cmbAllowedPair.Name = "_cmbAllowedPair";
            _cmbAllowedPair.Size = new Size(340, 23);
            _cmbAllowedPair.TabIndex = 14;
            // 
            // _lblDrawdownLabel
            // 
            _lblDrawdownLabel.AutoSize = true;
            _lblDrawdownLabel.Font = new Font("Segoe UI", 9F);
            _lblDrawdownLabel.ForeColor = Color.FromArgb(110, 110, 130);
            _lblDrawdownLabel.Location = new Point(14, 274);
            _lblDrawdownLabel.Name = "_lblDrawdownLabel";
            _lblDrawdownLabel.Size = new Size(104, 15);
            _lblDrawdownLabel.TabIndex = 15;
            _lblDrawdownLabel.Text = "Drawdown Stop %";
            // 
            // _nudDrawdownPct
            // 
            _nudDrawdownPct.BackColor = Color.FromArgb(22, 22, 32);
            _nudDrawdownPct.BorderStyle = BorderStyle.None;
            _nudDrawdownPct.DecimalPlaces = 1;
            _nudDrawdownPct.ForeColor = Color.FromArgb(218, 218, 230);
            _nudDrawdownPct.Location = new Point(200, 272);
            _nudDrawdownPct.Maximum = new decimal(new int[] { 50, 0, 0, 0 });
            _nudDrawdownPct.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            _nudDrawdownPct.Name = "_nudDrawdownPct";
            _nudDrawdownPct.Size = new Size(100, 19);
            _nudDrawdownPct.TabIndex = 16;
            _nudDrawdownPct.Value = new decimal(new int[] { 10, 0, 0, 0 });
            // 
            // _chkAutoLotBot
            // 
            _chkAutoLotBot.Location = new Point(0, 0);
            _chkAutoLotBot.Name = "_chkAutoLotBot";
            _chkAutoLotBot.Size = new Size(104, 24);
            _chkAutoLotBot.TabIndex = 17;
            _chkAutoLotBot.Text = "Auto calculate lot size";
            _chkAutoLotBot.UseVisualStyleBackColor = false;
            // 
            // _chkEnforceRR
            // 
            _chkEnforceRR.Location = new Point(0, 0);
            _chkEnforceRR.Name = "_chkEnforceRR";
            _chkEnforceRR.Size = new Size(104, 24);
            _chkEnforceRR.TabIndex = 18;
            _chkEnforceRR.Text = "Enforce minimum R:R";
            _chkEnforceRR.UseVisualStyleBackColor = false;
            // 
            // _chkDrawdown
            // 
            _chkDrawdown.Location = new Point(0, 0);
            _chkDrawdown.Name = "_chkDrawdown";
            _chkDrawdown.Size = new Size(104, 24);
            _chkDrawdown.TabIndex = 19;
            _chkDrawdown.Text = "Enable drawdown protection";
            _chkDrawdown.UseVisualStyleBackColor = false;
            // 
            // _chkAutoStart
            // 
            _chkAutoStart.Location = new Point(0, 0);
            _chkAutoStart.Name = "_chkAutoStart";
            _chkAutoStart.Size = new Size(104, 24);
            _chkAutoStart.TabIndex = 20;
            _chkAutoStart.Text = "Auto start on launch";
            _chkAutoStart.UseVisualStyleBackColor = false;
            // 
            // _btnStartBot
            // 
            _btnStartBot.BackColor = Color.FromArgb(72, 199, 142);
            _btnStartBot.Cursor = Cursors.Hand;
            _btnStartBot.FlatAppearance.BorderSize = 0;
            _btnStartBot.FlatStyle = FlatStyle.Flat;
            _btnStartBot.Font = new Font("Segoe UI Semibold", 10F);
            _btnStartBot.ForeColor = Color.FromArgb(10, 10, 20);
            _btnStartBot.Location = new Point(14, 430);
            _btnStartBot.Name = "_btnStartBot";
            _btnStartBot.Size = new Size(160, 42);
            _btnStartBot.TabIndex = 21;
            _btnStartBot.Text = "Start Monitoring";
            _btnStartBot.UseVisualStyleBackColor = false;
            //
            // _btnStopBot
            //
            _btnStopBot.BackColor = Color.FromArgb(252, 95, 95);
            _btnStopBot.Cursor = Cursors.Hand;
            _btnStopBot.Enabled = false;
            _btnStopBot.FlatAppearance.BorderSize = 0;
            _btnStopBot.FlatStyle = FlatStyle.Flat;
            _btnStopBot.Font = new Font("Segoe UI Semibold", 10F);
            _btnStopBot.ForeColor = Color.FromArgb(10, 10, 20);
            _btnStopBot.Name = "_btnStopBot";
            _btnStopBot.Size = new Size(148, 42);
            _btnStopBot.TabIndex = 23;
            _btnStopBot.Text = "■  Stop Bot";
            _btnStopBot.UseVisualStyleBackColor = false;
            //
            // _btnBotSettings
            //
            _btnBotSettings.BackColor = Color.FromArgb(99, 179, 237);
            _btnBotSettings.Cursor = Cursors.Hand;
            _btnBotSettings.FlatAppearance.BorderSize = 0;
            _btnBotSettings.FlatStyle = FlatStyle.Flat;
            _btnBotSettings.Font = new Font("Segoe UI Semibold", 10F);
            _btnBotSettings.ForeColor = Color.FromArgb(10, 10, 20);
            _btnBotSettings.Name = "_btnBotSettings";
            _btnBotSettings.Size = new Size(138, 42);
            _btnBotSettings.TabIndex = 24;
            _btnBotSettings.Text = "⚙  Settings";
            _btnBotSettings.UseVisualStyleBackColor = false;
            //
            // _btnAnalyzePairs
            //
            _btnAnalyzePairs.BackColor = Color.FromArgb(99, 179, 237);
            _btnAnalyzePairs.Cursor = Cursors.Hand;
            _btnAnalyzePairs.FlatAppearance.BorderSize = 0;
            _btnAnalyzePairs.FlatStyle = FlatStyle.Flat;
            _btnAnalyzePairs.Font = new Font("Segoe UI Semibold", 10F);
            _btnAnalyzePairs.ForeColor = Color.FromArgb(10, 10, 20);
            _btnAnalyzePairs.Location = new Point(184, 430);
            _btnAnalyzePairs.Name = "_btnAnalyzePairs";
            _btnAnalyzePairs.Size = new Size(160, 42);
            _btnAnalyzePairs.TabIndex = 22;
            _btnAnalyzePairs.Text = "Analysys pairs";
            _btnAnalyzePairs.UseVisualStyleBackColor = false;
            // 
            // _btnOpenFolder
            // 
            _btnOpenFolder.BackColor = Color.FromArgb(99, 179, 237);
            _btnOpenFolder.Cursor = Cursors.Hand;
            _btnOpenFolder.FlatAppearance.BorderSize = 0;
            _btnOpenFolder.FlatStyle = FlatStyle.Flat;
            _btnOpenFolder.Font = new Font("Segoe UI Semibold", 9F);
            _btnOpenFolder.ForeColor = Color.FromArgb(10, 10, 20);
            _btnOpenFolder.Location = new Point(14, 680);
            _btnOpenFolder.Name = "_btnOpenFolder";
            _btnOpenFolder.Size = new Size(118, 30);
            _btnOpenFolder.TabIndex = 3;
            _btnOpenFolder.Text = "Select Folder";
            _btnOpenFolder.UseVisualStyleBackColor = false;
            //
            // _btnBotInstructions
            //
            _btnBotInstructions.BackColor = Color.FromArgb(100, 160, 220);
            _btnBotInstructions.Cursor = Cursors.Hand;
            _btnBotInstructions.FlatAppearance.BorderSize = 0;
            _btnBotInstructions.FlatStyle = FlatStyle.Flat;
            _btnBotInstructions.Font = new Font("Segoe UI Semibold", 9F);
            _btnBotInstructions.ForeColor = Color.FromArgb(10, 10, 20);
            _btnBotInstructions.Location = new Point(164, 680);
            _btnBotInstructions.Name = "_btnBotInstructions";
            _btnBotInstructions.Size = new Size(160, 30);
            _btnBotInstructions.TabIndex = 4;
            _btnBotInstructions.Text = "📋 How It Works";
            _btnBotInstructions.UseVisualStyleBackColor = false;
            //
            // _tabClaude
            // 
            _tabClaude.Controls.Add(_lblClaudeBadge);
            _tabClaude.Controls.Add(_pnlClaudeCard);
            _tabClaude.Controls.Add(_pnlClaudePromptCard);
            _tabClaude.Location = new Point(4, 24);
            _tabClaude.Name = "_tabClaude";
            _tabClaude.Size = new Size(192, 72);
            _tabClaude.TabIndex = 4;
            _tabClaude.Text = "  AI API Config  ";
            // 
            // _lblClaudeBadge
            // 
            _lblClaudeBadge.Location = new Point(0, 0);
            _lblClaudeBadge.Name = "_lblClaudeBadge";
            _lblClaudeBadge.Size = new Size(100, 23);
            _lblClaudeBadge.TabIndex = 0;
            // 
            // _pnlClaudeCard
            // 
            _pnlClaudeCard.Controls.Add(_lblClaudeCardHeader);
            _pnlClaudeCard.Controls.Add(_lblAiProviderLabel);
            _pnlClaudeCard.Controls.Add(_cmbAiProvider);
            _pnlClaudeCard.Controls.Add(_lblApiKeyLabel);
            _pnlClaudeCard.Controls.Add(_txtClaudeApiKey);
            _pnlClaudeCard.Controls.Add(_lblModelLabel);
            _pnlClaudeCard.Controls.Add(_txtClaudeModel);
            _pnlClaudeCard.Controls.Add(_lblOpenAiKeyLabel);
            _pnlClaudeCard.Controls.Add(_txtOpenAiApiKey);
            _pnlClaudeCard.Controls.Add(_lblOpenAiModelLabel);
            _pnlClaudeCard.Controls.Add(_txtOpenAiModel);
            _pnlClaudeCard.Controls.Add(_lblSymbolsLabel);
            _pnlClaudeCard.Controls.Add(_txtClaudeSymbols);
            _pnlClaudeCard.Controls.Add(_lblPollSecLabel);
            _pnlClaudeCard.Controls.Add(_nudClaudePollSec);
            _pnlClaudeCard.Controls.Add(_lblConfidenceLabel);
            _pnlClaudeCard.Controls.Add(_nudAiConfidence);
            _pnlClaudeCard.Controls.Add(_lblNewsHeader);
            _pnlClaudeCard.Controls.Add(_lblNewsProviderLabel);
            _pnlClaudeCard.Controls.Add(_cmbNewsProvider);
            _pnlClaudeCard.Controls.Add(_lblNewsApiKeyLabel);
            _pnlClaudeCard.Controls.Add(_txtNewsApiKey);
            _pnlClaudeCard.Controls.Add(_lblNewsCurrenciesLabel);
            _pnlClaudeCard.Controls.Add(_txtNewsCurrencies);
            _pnlClaudeCard.Controls.Add(_lblNewsImpactLabel);
            _pnlClaudeCard.Controls.Add(_cmbNewsImpact);
            _pnlClaudeCard.Controls.Add(_lblNewsBlackoutLabel);
            _pnlClaudeCard.Controls.Add(_nudNewsBefore);
            _pnlClaudeCard.Controls.Add(_nudNewsAfter);
            _pnlClaudeCard.Controls.Add(_btnTestNewsApi);
            _pnlClaudeCard.Controls.Add(_lblNewsTestStatus);
            _pnlClaudeCard.Controls.Add(_lblNotifyHeader);
            _pnlClaudeCard.Controls.Add(_lblTelegramTokenLabel);
            _pnlClaudeCard.Controls.Add(_txtTelegramBotToken);
            _pnlClaudeCard.Controls.Add(_lblTelegramChatLabel);
            _pnlClaudeCard.Controls.Add(_txtTelegramChatId);
            _pnlClaudeCard.Controls.Add(_chkNotifySignals);
            _pnlClaudeCard.Controls.Add(_chkNotifyApproval);
            _pnlClaudeCard.Controls.Add(_chkNotifyOpened);
            _pnlClaudeCard.Controls.Add(_chkNotifyClosed);
            _pnlClaudeCard.Controls.Add(_chkNotifyRisk);
            _pnlClaudeCard.Controls.Add(_btnTestTelegram);
            _pnlClaudeCard.Controls.Add(_lblTelegramTestStatus);
            _pnlClaudeCard.Controls.Add(_lblClaudeNote1);
            _pnlClaudeCard.Controls.Add(_lblClaudeNote2);
            _pnlClaudeCard.Controls.Add(_btnStartClaude);
            _pnlClaudeCard.Controls.Add(_btnStopClaude);
            _pnlClaudeCard.Controls.Add(_btnTestClaudeApi);
            _pnlClaudeCard.Controls.Add(_lblApiTestStatus);
            _pnlClaudeCard.Location = new Point(0, 0);
            _pnlClaudeCard.Name = "_pnlClaudeCard";
            _pnlClaudeCard.Size = new Size(200, 100);
            _pnlClaudeCard.TabIndex = 1;
            // 
            // _lblClaudeCardHeader
            // 
            _lblClaudeCardHeader.AutoSize = true;
            _lblClaudeCardHeader.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            _lblClaudeCardHeader.ForeColor = Color.FromArgb(200, 210, 240);
            _lblClaudeCardHeader.Location = new Point(14, 14);
            _lblClaudeCardHeader.Name = "_lblClaudeCardHeader";
            _lblClaudeCardHeader.Size = new Size(124, 19);
            _lblClaudeCardHeader.TabIndex = 0;
            _lblClaudeCardHeader.Text = "AI API Configuration";
            // 
            // _lblAiProviderLabel
            // 
            _lblAiProviderLabel.AutoSize = true;
            _lblAiProviderLabel.Location = new Point(14, 50);
            _lblAiProviderLabel.Name = "_lblAiProviderLabel";
            _lblAiProviderLabel.Size = new Size(61, 15);
            _lblAiProviderLabel.TabIndex = 1;
            _lblAiProviderLabel.Text = "AI Provider";
            // 
            // _cmbAiProvider
            // 
            _cmbAiProvider.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbAiProvider.Items.AddRange(new object[] { "Claude", "OpenAI", "Both" });
            _cmbAiProvider.Location = new Point(210, 48);
            _cmbAiProvider.Name = "_cmbAiProvider";
            _cmbAiProvider.Size = new Size(160, 23);
            _cmbAiProvider.TabIndex = 2;
            // 
            // _lblApiKeyLabel
            // 
            _lblApiKeyLabel.AutoSize = true;
            _lblApiKeyLabel.Font = new Font("Segoe UI", 9F);
            _lblApiKeyLabel.ForeColor = Color.FromArgb(110, 110, 130);
            _lblApiKeyLabel.Location = new Point(14, 50);
            _lblApiKeyLabel.Name = "_lblApiKeyLabel";
            _lblApiKeyLabel.Size = new Size(47, 15);
            _lblApiKeyLabel.TabIndex = 1;
            _lblApiKeyLabel.Text = "API Key";
            // 
            // _txtClaudeApiKey
            // 
            _txtClaudeApiKey.BackColor = Color.FromArgb(22, 22, 32);
            _txtClaudeApiKey.BorderStyle = BorderStyle.FixedSingle;
            _txtClaudeApiKey.Font = new Font("Consolas", 9F);
            _txtClaudeApiKey.ForeColor = Color.FromArgb(218, 218, 230);
            _txtClaudeApiKey.Location = new Point(210, 48);
            _txtClaudeApiKey.Name = "_txtClaudeApiKey";
            _txtClaudeApiKey.Size = new Size(320, 22);
            _txtClaudeApiKey.TabIndex = 2;
            _txtClaudeApiKey.Text = "sk-ant-...";
            _txtClaudeApiKey.UseSystemPasswordChar = true;
            // 
            // _lblModelLabel
            // 
            _lblModelLabel.AutoSize = true;
            _lblModelLabel.Font = new Font("Segoe UI", 9F);
            _lblModelLabel.ForeColor = Color.FromArgb(110, 110, 130);
            _lblModelLabel.Location = new Point(14, 82);
            _lblModelLabel.Name = "_lblModelLabel";
            _lblModelLabel.Size = new Size(41, 15);
            _lblModelLabel.TabIndex = 3;
            _lblModelLabel.Text = "Model";
            // 
            // _lblModelValue
            // 
            _lblModelValue.Location = new Point(0, 0);
            _lblModelValue.Name = "_lblModelValue";
            _lblModelValue.Size = new Size(100, 23);
            _lblModelValue.TabIndex = 4;
            // 
            // _txtClaudeModel
            // 
            _txtClaudeModel.BackColor = Color.FromArgb(22, 22, 32);
            _txtClaudeModel.BorderStyle = BorderStyle.FixedSingle;
            _txtClaudeModel.Font = new Font("Consolas", 9F);
            _txtClaudeModel.ForeColor = Color.FromArgb(218, 218, 230);
            _txtClaudeModel.Location = new Point(210, 80);
            _txtClaudeModel.Name = "_txtClaudeModel";
            _txtClaudeModel.Size = new Size(320, 22);
            _txtClaudeModel.TabIndex = 4;
            _txtClaudeModel.Text = "claude-opus-4-7";
            // 
            // _lblOpenAiKeyLabel
            // 
            _lblOpenAiKeyLabel.AutoSize = true;
            _lblOpenAiKeyLabel.Location = new Point(14, 114);
            _lblOpenAiKeyLabel.Name = "_lblOpenAiKeyLabel";
            _lblOpenAiKeyLabel.Size = new Size(85, 15);
            _lblOpenAiKeyLabel.TabIndex = 5;
            _lblOpenAiKeyLabel.Text = "OpenAI API Key";
            // 
            // _txtOpenAiApiKey
            // 
            _txtOpenAiApiKey.BackColor = Color.FromArgb(22, 22, 32);
            _txtOpenAiApiKey.BorderStyle = BorderStyle.FixedSingle;
            _txtOpenAiApiKey.Font = new Font("Consolas", 9F);
            _txtOpenAiApiKey.ForeColor = Color.FromArgb(218, 218, 230);
            _txtOpenAiApiKey.Location = new Point(210, 112);
            _txtOpenAiApiKey.Name = "_txtOpenAiApiKey";
            _txtOpenAiApiKey.Size = new Size(320, 22);
            _txtOpenAiApiKey.TabIndex = 6;
            _txtOpenAiApiKey.UseSystemPasswordChar = true;
            // 
            // _lblOpenAiModelLabel
            // 
            _lblOpenAiModelLabel.AutoSize = true;
            _lblOpenAiModelLabel.Location = new Point(14, 146);
            _lblOpenAiModelLabel.Name = "_lblOpenAiModelLabel";
            _lblOpenAiModelLabel.Size = new Size(82, 15);
            _lblOpenAiModelLabel.TabIndex = 7;
            _lblOpenAiModelLabel.Text = "OpenAI Model";
            // 
            // _txtOpenAiModel
            // 
            _txtOpenAiModel.BackColor = Color.FromArgb(22, 22, 32);
            _txtOpenAiModel.BorderStyle = BorderStyle.FixedSingle;
            _txtOpenAiModel.Font = new Font("Consolas", 9F);
            _txtOpenAiModel.ForeColor = Color.FromArgb(218, 218, 230);
            _txtOpenAiModel.Location = new Point(210, 144);
            _txtOpenAiModel.Name = "_txtOpenAiModel";
            _txtOpenAiModel.Size = new Size(180, 22);
            _txtOpenAiModel.TabIndex = 8;
            _txtOpenAiModel.Text = "gpt-5.1";
            // 
            // _lblSymbolsLabel
            // 
            _lblSymbolsLabel.AutoSize = true;
            _lblSymbolsLabel.Font = new Font("Segoe UI", 9F);
            _lblSymbolsLabel.ForeColor = Color.FromArgb(110, 110, 130);
            _lblSymbolsLabel.Location = new Point(14, 114);
            _lblSymbolsLabel.Name = "_lblSymbolsLabel";
            _lblSymbolsLabel.Size = new Size(89, 15);
            _lblSymbolsLabel.TabIndex = 5;
            _lblSymbolsLabel.Text = "Watch Symbols";
            // 
            // _txtClaudeSymbols
            // 
            _txtClaudeSymbols.BackColor = Color.FromArgb(22, 22, 32);
            _txtClaudeSymbols.BorderStyle = BorderStyle.FixedSingle;
            _txtClaudeSymbols.Font = new Font("Consolas", 9F);
            _txtClaudeSymbols.ForeColor = Color.FromArgb(218, 218, 230);
            _txtClaudeSymbols.Location = new Point(210, 112);
            _txtClaudeSymbols.Name = "_txtClaudeSymbols";
            _txtClaudeSymbols.Size = new Size(320, 22);
            _txtClaudeSymbols.TabIndex = 6;
            // 
            // _lblPollSecLabel
            // 
            _lblPollSecLabel.AutoSize = true;
            _lblPollSecLabel.Font = new Font("Segoe UI", 9F);
            _lblPollSecLabel.ForeColor = Color.FromArgb(110, 110, 130);
            _lblPollSecLabel.Location = new Point(14, 146);
            _lblPollSecLabel.Name = "_lblPollSecLabel";
            _lblPollSecLabel.Size = new Size(97, 15);
            _lblPollSecLabel.TabIndex = 7;
            _lblPollSecLabel.Text = "Poll Interval (sec)";
            // 
            // _nudClaudePollSec
            // 
            _nudClaudePollSec.BackColor = Color.FromArgb(22, 22, 32);
            _nudClaudePollSec.BorderStyle = BorderStyle.None;
            _nudClaudePollSec.ForeColor = Color.FromArgb(218, 218, 230);
            _nudClaudePollSec.Increment = new decimal(new int[] { 10, 0, 0, 0 });
            _nudClaudePollSec.Location = new Point(210, 144);
            _nudClaudePollSec.Maximum = new decimal(new int[] { 3600, 0, 0, 0 });
            _nudClaudePollSec.Minimum = new decimal(new int[] { 10, 0, 0, 0 });
            _nudClaudePollSec.Name = "_nudClaudePollSec";
            _nudClaudePollSec.Size = new Size(120, 19);
            _nudClaudePollSec.TabIndex = 8;
            _nudClaudePollSec.Value = new decimal(new int[] { 60, 0, 0, 0 });
            // 
            // _lblConfidenceLabel
            // 
            _lblConfidenceLabel.AutoSize = true;
            _lblConfidenceLabel.Location = new Point(14, 210);
            _lblConfidenceLabel.Name = "_lblConfidenceLabel";
            _lblConfidenceLabel.Size = new Size(124, 15);
            _lblConfidenceLabel.TabIndex = 15;
            _lblConfidenceLabel.Text = "Minimum Confidence";
            // 
            // _nudAiConfidence
            // 
            _nudAiConfidence.BackColor = Color.FromArgb(22, 22, 32);
            _nudAiConfidence.BorderStyle = BorderStyle.None;
            _nudAiConfidence.ForeColor = Color.FromArgb(218, 218, 230);
            _nudAiConfidence.Location = new Point(210, 208);
            _nudAiConfidence.Maximum = new decimal(new int[] { 100, 0, 0, 0 });
            _nudAiConfidence.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            _nudAiConfidence.Name = "_nudAiConfidence";
            _nudAiConfidence.Size = new Size(80, 19);
            _nudAiConfidence.TabIndex = 16;
            _nudAiConfidence.Value = new decimal(new int[] { 70, 0, 0, 0 });
            // 
            // _lblNewsHeader
            // 
            _lblNewsHeader.AutoSize = true;
            _lblNewsHeader.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            _lblNewsHeader.ForeColor = Color.FromArgb(200, 210, 240);
            _lblNewsHeader.Location = new Point(14, 296);
            _lblNewsHeader.Name = "_lblNewsHeader";
            _lblNewsHeader.Size = new Size(137, 19);
            _lblNewsHeader.TabIndex = 17;
            _lblNewsHeader.Text = "Market / News Data";
            // 
            // _lblNewsProviderLabel
            // 
            _lblNewsProviderLabel.AutoSize = true;
            _lblNewsProviderLabel.Location = new Point(14, 330);
            _lblNewsProviderLabel.Name = "_lblNewsProviderLabel";
            _lblNewsProviderLabel.Size = new Size(80, 15);
            _lblNewsProviderLabel.TabIndex = 18;
            _lblNewsProviderLabel.Text = "News Provider";
            // 
            // _cmbNewsProvider
            // 
            _cmbNewsProvider.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbNewsProvider.Items.AddRange(new object[] { "Financial Modeling Prep", "Trading Economics", "None" });
            _cmbNewsProvider.Location = new Point(210, 328);
            _cmbNewsProvider.Name = "_cmbNewsProvider";
            _cmbNewsProvider.Size = new Size(180, 23);
            _cmbNewsProvider.TabIndex = 19;
            // 
            // _lblNewsApiKeyLabel
            // 
            _lblNewsApiKeyLabel.AutoSize = true;
            _lblNewsApiKeyLabel.Location = new Point(14, 362);
            _lblNewsApiKeyLabel.Name = "_lblNewsApiKeyLabel";
            _lblNewsApiKeyLabel.Size = new Size(74, 15);
            _lblNewsApiKeyLabel.TabIndex = 20;
            _lblNewsApiKeyLabel.Text = "News API Key";
            // 
            // _txtNewsApiKey
            // 
            _txtNewsApiKey.BackColor = Color.FromArgb(22, 22, 32);
            _txtNewsApiKey.BorderStyle = BorderStyle.FixedSingle;
            _txtNewsApiKey.Font = new Font("Consolas", 9F);
            _txtNewsApiKey.ForeColor = Color.FromArgb(218, 218, 230);
            _txtNewsApiKey.Location = new Point(210, 360);
            _txtNewsApiKey.Name = "_txtNewsApiKey";
            _txtNewsApiKey.Size = new Size(320, 22);
            _txtNewsApiKey.TabIndex = 21;
            _txtNewsApiKey.UseSystemPasswordChar = true;
            // 
            // _lblNewsCurrenciesLabel
            // 
            _lblNewsCurrenciesLabel.AutoSize = true;
            _lblNewsCurrenciesLabel.Location = new Point(14, 394);
            _lblNewsCurrenciesLabel.Name = "_lblNewsCurrenciesLabel";
            _lblNewsCurrenciesLabel.Size = new Size(111, 15);
            _lblNewsCurrenciesLabel.TabIndex = 22;
            _lblNewsCurrenciesLabel.Text = "Currencies/Countries";
            // 
            // _txtNewsCurrencies
            // 
            _txtNewsCurrencies.BackColor = Color.FromArgb(22, 22, 32);
            _txtNewsCurrencies.BorderStyle = BorderStyle.FixedSingle;
            _txtNewsCurrencies.Font = new Font("Consolas", 9F);
            _txtNewsCurrencies.ForeColor = Color.FromArgb(218, 218, 230);
            _txtNewsCurrencies.Location = new Point(210, 392);
            _txtNewsCurrencies.Name = "_txtNewsCurrencies";
            _txtNewsCurrencies.Size = new Size(320, 22);
            _txtNewsCurrencies.TabIndex = 23;
            _txtNewsCurrencies.Text = "USD,GBP,EUR,JPY";
            // 
            // _lblNewsImpactLabel
            // 
            _lblNewsImpactLabel.AutoSize = true;
            _lblNewsImpactLabel.Location = new Point(14, 426);
            _lblNewsImpactLabel.Name = "_lblNewsImpactLabel";
            _lblNewsImpactLabel.Size = new Size(73, 15);
            _lblNewsImpactLabel.TabIndex = 24;
            _lblNewsImpactLabel.Text = "Impact Filter";
            // 
            // _cmbNewsImpact
            // 
            _cmbNewsImpact.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbNewsImpact.Items.AddRange(new object[] { "High only", "Medium + High", "All" });
            _cmbNewsImpact.Location = new Point(210, 424);
            _cmbNewsImpact.Name = "_cmbNewsImpact";
            _cmbNewsImpact.Size = new Size(160, 23);
            _cmbNewsImpact.TabIndex = 25;
            // 
            // _lblNewsBlackoutLabel
            // 
            _lblNewsBlackoutLabel.AutoSize = true;
            _lblNewsBlackoutLabel.Location = new Point(14, 458);
            _lblNewsBlackoutLabel.Name = "_lblNewsBlackoutLabel";
            _lblNewsBlackoutLabel.Size = new Size(143, 15);
            _lblNewsBlackoutLabel.TabIndex = 26;
            _lblNewsBlackoutLabel.Text = "Blackout Before / After min";
            // 
            // _nudNewsBefore
            // 
            _nudNewsBefore.BackColor = Color.FromArgb(22, 22, 32);
            _nudNewsBefore.BorderStyle = BorderStyle.None;
            _nudNewsBefore.ForeColor = Color.FromArgb(218, 218, 230);
            _nudNewsBefore.Location = new Point(210, 456);
            _nudNewsBefore.Maximum = new decimal(new int[] { 240, 0, 0, 0 });
            _nudNewsBefore.Name = "_nudNewsBefore";
            _nudNewsBefore.Size = new Size(70, 19);
            _nudNewsBefore.TabIndex = 27;
            _nudNewsBefore.Value = new decimal(new int[] { 30, 0, 0, 0 });
            // 
            // _nudNewsAfter
            // 
            _nudNewsAfter.BackColor = Color.FromArgb(22, 22, 32);
            _nudNewsAfter.BorderStyle = BorderStyle.None;
            _nudNewsAfter.ForeColor = Color.FromArgb(218, 218, 230);
            _nudNewsAfter.Location = new Point(292, 456);
            _nudNewsAfter.Maximum = new decimal(new int[] { 240, 0, 0, 0 });
            _nudNewsAfter.Name = "_nudNewsAfter";
            _nudNewsAfter.Size = new Size(70, 19);
            _nudNewsAfter.TabIndex = 28;
            _nudNewsAfter.Value = new decimal(new int[] { 15, 0, 0, 0 });
            // 
            // _btnTestNewsApi
            // 
            _btnTestNewsApi.Location = new Point(14, 492);
            _btnTestNewsApi.Name = "_btnTestNewsApi";
            _btnTestNewsApi.Size = new Size(178, 34);
            _btnTestNewsApi.TabIndex = 29;
            _btnTestNewsApi.Text = "Test News API";
            _btnTestNewsApi.UseVisualStyleBackColor = false;
            // 
            // _lblNewsTestStatus
            // 
            _lblNewsTestStatus.AutoEllipsis = true;
            _lblNewsTestStatus.Location = new Point(210, 500);
            _lblNewsTestStatus.Name = "_lblNewsTestStatus";
            _lblNewsTestStatus.Size = new Size(320, 18);
            _lblNewsTestStatus.TabIndex = 30;
            _lblNewsTestStatus.Text = "Not tested";
            // 
            // _lblNotifyHeader
            // 
            _lblNotifyHeader.AutoSize = true;
            _lblNotifyHeader.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            _lblNotifyHeader.ForeColor = Color.FromArgb(200, 210, 240);
            _lblNotifyHeader.Location = new Point(14, 552);
            _lblNotifyHeader.Name = "_lblNotifyHeader";
            _lblNotifyHeader.Size = new Size(91, 19);
            _lblNotifyHeader.TabIndex = 31;
            _lblNotifyHeader.Text = "Notifications";
            // 
            // _lblTelegramTokenLabel
            // 
            _lblTelegramTokenLabel.AutoSize = true;
            _lblTelegramTokenLabel.Location = new Point(14, 586);
            _lblTelegramTokenLabel.Name = "_lblTelegramTokenLabel";
            _lblTelegramTokenLabel.Size = new Size(57, 15);
            _lblTelegramTokenLabel.TabIndex = 32;
            _lblTelegramTokenLabel.Text = "Bot Token";
            // 
            // _txtTelegramBotToken
            // 
            _txtTelegramBotToken.BackColor = Color.FromArgb(22, 22, 32);
            _txtTelegramBotToken.BorderStyle = BorderStyle.FixedSingle;
            _txtTelegramBotToken.Font = new Font("Consolas", 9F);
            _txtTelegramBotToken.ForeColor = Color.FromArgb(218, 218, 230);
            _txtTelegramBotToken.Location = new Point(210, 584);
            _txtTelegramBotToken.Name = "_txtTelegramBotToken";
            _txtTelegramBotToken.Size = new Size(320, 22);
            _txtTelegramBotToken.TabIndex = 33;
            _txtTelegramBotToken.UseSystemPasswordChar = true;
            // 
            // _lblTelegramChatLabel
            // 
            _lblTelegramChatLabel.AutoSize = true;
            _lblTelegramChatLabel.Location = new Point(14, 618);
            _lblTelegramChatLabel.Name = "_lblTelegramChatLabel";
            _lblTelegramChatLabel.Size = new Size(43, 15);
            _lblTelegramChatLabel.TabIndex = 34;
            _lblTelegramChatLabel.Text = "Chat ID";
            // 
            // _txtTelegramChatId
            // 
            _txtTelegramChatId.BackColor = Color.FromArgb(22, 22, 32);
            _txtTelegramChatId.BorderStyle = BorderStyle.FixedSingle;
            _txtTelegramChatId.Font = new Font("Consolas", 9F);
            _txtTelegramChatId.ForeColor = Color.FromArgb(218, 218, 230);
            _txtTelegramChatId.Location = new Point(210, 616);
            _txtTelegramChatId.Name = "_txtTelegramChatId";
            _txtTelegramChatId.Size = new Size(180, 22);
            _txtTelegramChatId.TabIndex = 35;
            // 
            // _chkNotifySignals
            // 
            _chkNotifySignals.Location = new Point(210, 650);
            _chkNotifySignals.Name = "_chkNotifySignals";
            _chkNotifySignals.Size = new Size(120, 24);
            _chkNotifySignals.TabIndex = 36;
            _chkNotifySignals.Text = "Signals";
            _chkNotifySignals.UseVisualStyleBackColor = false;
            // 
            // _chkNotifyApproval
            // 
            _chkNotifyApproval.Location = new Point(340, 650);
            _chkNotifyApproval.Name = "_chkNotifyApproval";
            _chkNotifyApproval.Size = new Size(140, 24);
            _chkNotifyApproval.TabIndex = 37;
            _chkNotifyApproval.Text = "Approval Needed";
            _chkNotifyApproval.UseVisualStyleBackColor = false;
            // 
            // _chkNotifyOpened
            // 
            _chkNotifyOpened.Location = new Point(210, 678);
            _chkNotifyOpened.Name = "_chkNotifyOpened";
            _chkNotifyOpened.Size = new Size(120, 24);
            _chkNotifyOpened.TabIndex = 38;
            _chkNotifyOpened.Text = "Trade Opened";
            _chkNotifyOpened.UseVisualStyleBackColor = false;
            // 
            // _chkNotifyClosed
            // 
            _chkNotifyClosed.Location = new Point(340, 678);
            _chkNotifyClosed.Name = "_chkNotifyClosed";
            _chkNotifyClosed.Size = new Size(120, 24);
            _chkNotifyClosed.TabIndex = 39;
            _chkNotifyClosed.Text = "Trade Closed";
            _chkNotifyClosed.UseVisualStyleBackColor = false;
            // 
            // _chkNotifyRisk
            // 
            _chkNotifyRisk.Location = new Point(210, 706);
            _chkNotifyRisk.Name = "_chkNotifyRisk";
            _chkNotifyRisk.Size = new Size(120, 24);
            _chkNotifyRisk.TabIndex = 40;
            _chkNotifyRisk.Text = "Risk Blocked";
            _chkNotifyRisk.UseVisualStyleBackColor = false;
            // 
            // _btnTestTelegram
            // 
            _btnTestTelegram.Location = new Point(14, 746);
            _btnTestTelegram.Name = "_btnTestTelegram";
            _btnTestTelegram.Size = new Size(178, 34);
            _btnTestTelegram.TabIndex = 41;
            _btnTestTelegram.Text = "Test Notification";
            _btnTestTelegram.UseVisualStyleBackColor = false;
            // 
            // _lblTelegramTestStatus
            // 
            _lblTelegramTestStatus.AutoEllipsis = true;
            _lblTelegramTestStatus.Location = new Point(210, 754);
            _lblTelegramTestStatus.Name = "_lblTelegramTestStatus";
            _lblTelegramTestStatus.Size = new Size(320, 18);
            _lblTelegramTestStatus.TabIndex = 42;
            _lblTelegramTestStatus.Text = "Not tested";
            // 
            // _lblClaudeNote1
            // 
            _lblClaudeNote1.Location = new Point(0, 0);
            _lblClaudeNote1.Name = "_lblClaudeNote1";
            _lblClaudeNote1.Size = new Size(100, 23);
            _lblClaudeNote1.TabIndex = 9;
            // 
            // _lblClaudeNote2
            // 
            _lblClaudeNote2.Location = new Point(0, 0);
            _lblClaudeNote2.Name = "_lblClaudeNote2";
            _lblClaudeNote2.Size = new Size(100, 23);
            _lblClaudeNote2.TabIndex = 10;
            // 
            // _btnStartClaude
            // 
            _btnStartClaude.BackColor = Color.FromArgb(72, 199, 142);
            _btnStartClaude.Cursor = Cursors.Hand;
            _btnStartClaude.FlatAppearance.BorderSize = 0;
            _btnStartClaude.FlatStyle = FlatStyle.Flat;
            _btnStartClaude.Font = new Font("Segoe UI Semibold", 10F);
            _btnStartClaude.ForeColor = Color.FromArgb(10, 10, 20);
            _btnStartClaude.Location = new Point(14, 244);
            _btnStartClaude.Name = "_btnStartClaude";
            _btnStartClaude.Size = new Size(180, 42);
            _btnStartClaude.TabIndex = 11;
            _btnStartClaude.Text = "Start AI Monitor";
            _btnStartClaude.UseVisualStyleBackColor = false;
            // 
            // _btnStopClaude
            // 
            _btnStopClaude.BackColor = Color.FromArgb(252, 95, 95);
            _btnStopClaude.Cursor = Cursors.Hand;
            _btnStopClaude.Enabled = false;
            _btnStopClaude.FlatAppearance.BorderSize = 0;
            _btnStopClaude.FlatStyle = FlatStyle.Flat;
            _btnStopClaude.Font = new Font("Segoe UI Semibold", 10F);
            _btnStopClaude.ForeColor = Color.FromArgb(10, 10, 20);
            _btnStopClaude.Location = new Point(204, 244);
            _btnStopClaude.Name = "_btnStopClaude";
            _btnStopClaude.Size = new Size(180, 42);
            _btnStopClaude.TabIndex = 12;
            _btnStopClaude.Text = "Stop AI Monitor";
            _btnStopClaude.UseVisualStyleBackColor = false;
            //
            // _btnTestClaudeApi
            //
            _btnTestClaudeApi.BackColor = Color.FromArgb(28, 45, 80);
            _btnTestClaudeApi.Cursor = Cursors.Hand;
            _btnTestClaudeApi.FlatAppearance.BorderColor = Color.FromArgb(50, 80, 140);
            _btnTestClaudeApi.FlatAppearance.BorderSize = 1;
            _btnTestClaudeApi.FlatStyle = FlatStyle.Flat;
            _btnTestClaudeApi.Font = new Font("Segoe UI Semibold", 10F);
            _btnTestClaudeApi.ForeColor = Color.FromArgb(130, 180, 255);
            _btnTestClaudeApi.Location = new Point(14, 296);
            _btnTestClaudeApi.Name = "_btnTestClaudeApi";
            _btnTestClaudeApi.Size = new Size(180, 36);
            _btnTestClaudeApi.TabIndex = 13;
            _btnTestClaudeApi.Text = "Test API Connection";
            _btnTestClaudeApi.UseVisualStyleBackColor = false;
            //
            // _lblApiTestStatus
            //
            _lblApiTestStatus.AutoEllipsis = true;
            _lblApiTestStatus.Font = new Font("Consolas", 8.5F);
            _lblApiTestStatus.ForeColor = Color.FromArgb(110, 110, 130);
            _lblApiTestStatus.Location = new Point(14, 340);
            _lblApiTestStatus.Name = "_lblApiTestStatus";
            _lblApiTestStatus.Size = new Size(520, 18);
            _lblApiTestStatus.TabIndex = 14;
            _lblApiTestStatus.Text = "Not tested";
            //
            // _pnlClaudePromptCard
            //
            _pnlClaudePromptCard.Controls.Add(_lblPromptHeader);
            _pnlClaudePromptCard.Controls.Add(_txtClaudePrompt);
            _pnlClaudePromptCard.Controls.Add(_btnResetPrompt);
            _pnlClaudePromptCard.Location = new Point(0, 0);
            _pnlClaudePromptCard.Name = "_pnlClaudePromptCard";
            _pnlClaudePromptCard.Size = new Size(200, 100);
            _pnlClaudePromptCard.TabIndex = 2;
            // 
            // _lblPromptHeader
            // 
            _lblPromptHeader.AutoSize = true;
            _lblPromptHeader.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            _lblPromptHeader.ForeColor = Color.FromArgb(200, 210, 240);
            _lblPromptHeader.Location = new Point(14, 14);
            _lblPromptHeader.Name = "_lblPromptHeader";
            _lblPromptHeader.Size = new Size(316, 19);
            _lblPromptHeader.TabIndex = 0;
            _lblPromptHeader.Text = "System Prompt  (used only when AI analysis runs)";
            // 
            // _txtClaudePrompt
            // 
            _txtClaudePrompt.Location = new Point(0, 0);
            _txtClaudePrompt.Name = "_txtClaudePrompt";
            _txtClaudePrompt.Size = new Size(100, 96);
            _txtClaudePrompt.TabIndex = 1;
            _txtClaudePrompt.Text = "";
            // 
            // _btnResetPrompt
            // 
            _btnResetPrompt.BackColor = Color.FromArgb(110, 110, 130);
            _btnResetPrompt.Cursor = Cursors.Hand;
            _btnResetPrompt.FlatAppearance.BorderSize = 0;
            _btnResetPrompt.FlatStyle = FlatStyle.Flat;
            _btnResetPrompt.Font = new Font("Segoe UI Semibold", 9F);
            _btnResetPrompt.ForeColor = Color.FromArgb(10, 10, 20);
            _btnResetPrompt.Location = new Point(14, 542);
            _btnResetPrompt.Name = "_btnResetPrompt";
            _btnResetPrompt.Size = new Size(150, 30);
            _btnResetPrompt.TabIndex = 2;
            _btnResetPrompt.Text = "↺ Reset Default";
            _btnResetPrompt.UseVisualStyleBackColor = false;
            // 
            // _tabLog
            // 
            _tabLog.Controls.Add(_txtLog);
            _tabLog.Controls.Add(_btnClearLog);
            _tabLog.Controls.Add(_btnSaveLog);
            _tabLog.Location = new Point(4, 24);
            _tabLog.Name = "_tabLog";
            _tabLog.Size = new Size(192, 72);
            _tabLog.TabIndex = 5;
            _tabLog.Text = "  📋 Log  ";
            // 
            // _txtLog
            // 
            _txtLog.Location = new Point(0, 0);
            _txtLog.Name = "_txtLog";
            _txtLog.Size = new Size(100, 96);
            _txtLog.TabIndex = 0;
            _txtLog.Text = "";
            // 
            // _btnClearLog
            // 
            _btnClearLog.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            _btnClearLog.BackColor = Color.FromArgb(110, 110, 130);
            _btnClearLog.Cursor = Cursors.Hand;
            _btnClearLog.FlatAppearance.BorderSize = 0;
            _btnClearLog.FlatStyle = FlatStyle.Flat;
            _btnClearLog.Font = new Font("Segoe UI Semibold", 9F);
            _btnClearLog.ForeColor = Color.FromArgb(10, 10, 20);
            _btnClearLog.Location = new Point(12, 658);
            _btnClearLog.Name = "_btnClearLog";
            _btnClearLog.Size = new Size(100, 30);
            _btnClearLog.TabIndex = 1;
            _btnClearLog.Text = "Clear";
            _btnClearLog.UseVisualStyleBackColor = false;
            // 
            // _btnSaveLog
            // 
            _btnSaveLog.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            _btnSaveLog.BackColor = Color.FromArgb(99, 179, 237);
            _btnSaveLog.Cursor = Cursors.Hand;
            _btnSaveLog.FlatAppearance.BorderSize = 0;
            _btnSaveLog.FlatStyle = FlatStyle.Flat;
            _btnSaveLog.Font = new Font("Segoe UI Semibold", 9F);
            _btnSaveLog.ForeColor = Color.FromArgb(10, 10, 20);
            _btnSaveLog.Location = new Point(122, 658);
            _btnSaveLog.Name = "_btnSaveLog";
            _btnSaveLog.Size = new Size(100, 30);
            _btnSaveLog.TabIndex = 2;
            _btnSaveLog.Text = "Save Log";
            _btnSaveLog.UseVisualStyleBackColor = false;
            // 
            // dataGridViewTextBoxColumn1
            // 
            dataGridViewTextBoxColumn1.HeaderText = "Ticket";
            dataGridViewTextBoxColumn1.Name = "dataGridViewTextBoxColumn1";
            dataGridViewTextBoxColumn1.ReadOnly = true;
            // 
            // dataGridViewTextBoxColumn2
            // 
            dataGridViewTextBoxColumn2.HeaderText = "Symbol";
            dataGridViewTextBoxColumn2.Name = "dataGridViewTextBoxColumn2";
            dataGridViewTextBoxColumn2.ReadOnly = true;
            // 
            // dataGridViewTextBoxColumn3
            // 
            dataGridViewTextBoxColumn3.HeaderText = "Type";
            dataGridViewTextBoxColumn3.Name = "dataGridViewTextBoxColumn3";
            dataGridViewTextBoxColumn3.ReadOnly = true;
            // 
            // dataGridViewTextBoxColumn4
            // 
            dataGridViewTextBoxColumn4.HeaderText = "Lots";
            dataGridViewTextBoxColumn4.Name = "dataGridViewTextBoxColumn4";
            dataGridViewTextBoxColumn4.ReadOnly = true;
            // 
            // dataGridViewTextBoxColumn5
            // 
            dataGridViewTextBoxColumn5.HeaderText = "Open";
            dataGridViewTextBoxColumn5.Name = "dataGridViewTextBoxColumn5";
            dataGridViewTextBoxColumn5.ReadOnly = true;
            // 
            // dataGridViewTextBoxColumn6
            // 
            dataGridViewTextBoxColumn6.HeaderText = "Current";
            dataGridViewTextBoxColumn6.Name = "dataGridViewTextBoxColumn6";
            dataGridViewTextBoxColumn6.ReadOnly = true;
            // 
            // dataGridViewTextBoxColumn7
            // 
            dataGridViewTextBoxColumn7.HeaderText = "SL";
            dataGridViewTextBoxColumn7.Name = "dataGridViewTextBoxColumn7";
            dataGridViewTextBoxColumn7.ReadOnly = true;
            // 
            // dataGridViewTextBoxColumn8
            // 
            dataGridViewTextBoxColumn8.HeaderText = "TP";
            dataGridViewTextBoxColumn8.Name = "dataGridViewTextBoxColumn8";
            dataGridViewTextBoxColumn8.ReadOnly = true;
            // 
            // dataGridViewTextBoxColumn9
            // 
            dataGridViewTextBoxColumn9.HeaderText = "P&L ($)";
            dataGridViewTextBoxColumn9.Name = "dataGridViewTextBoxColumn9";
            dataGridViewTextBoxColumn9.ReadOnly = true;
            // 
            // dataGridViewTextBoxColumn10
            // 
            dataGridViewTextBoxColumn10.HeaderText = "Pips";
            dataGridViewTextBoxColumn10.Name = "dataGridViewTextBoxColumn10";
            dataGridViewTextBoxColumn10.ReadOnly = true;
            // 
            // dataGridViewTextBoxColumn11
            // 
            dataGridViewTextBoxColumn11.HeaderText = "Time";
            dataGridViewTextBoxColumn11.Name = "dataGridViewTextBoxColumn11";
            dataGridViewTextBoxColumn11.ReadOnly = true;
            // 
            // dataGridViewTextBoxColumn12
            // 
            dataGridViewTextBoxColumn12.HeaderText = "Comment";
            dataGridViewTextBoxColumn12.Name = "dataGridViewTextBoxColumn12";
            dataGridViewTextBoxColumn12.ReadOnly = true;
            // 
            // dataGridViewTextBoxColumn13
            // 
            dataGridViewTextBoxColumn13.HeaderText = "Time";
            dataGridViewTextBoxColumn13.Name = "dataGridViewTextBoxColumn13";
            dataGridViewTextBoxColumn13.ReadOnly = true;
            // 
            // dataGridViewTextBoxColumn14
            // 
            dataGridViewTextBoxColumn14.HeaderText = "Id";
            dataGridViewTextBoxColumn14.Name = "dataGridViewTextBoxColumn14";
            dataGridViewTextBoxColumn14.ReadOnly = true;
            // 
            // dataGridViewTextBoxColumn15
            // 
            dataGridViewTextBoxColumn15.HeaderText = "Pair";
            dataGridViewTextBoxColumn15.Name = "dataGridViewTextBoxColumn15";
            dataGridViewTextBoxColumn15.ReadOnly = true;
            // 
            // dataGridViewTextBoxColumn16
            // 
            dataGridViewTextBoxColumn16.HeaderText = "Dir";
            dataGridViewTextBoxColumn16.Name = "dataGridViewTextBoxColumn16";
            dataGridViewTextBoxColumn16.ReadOnly = true;
            // 
            // dataGridViewTextBoxColumn17
            // 
            dataGridViewTextBoxColumn17.HeaderText = "Lots";
            dataGridViewTextBoxColumn17.Name = "dataGridViewTextBoxColumn17";
            dataGridViewTextBoxColumn17.ReadOnly = true;
            // 
            // dataGridViewTextBoxColumn18
            // 
            dataGridViewTextBoxColumn18.HeaderText = "Entry";
            dataGridViewTextBoxColumn18.Name = "dataGridViewTextBoxColumn18";
            dataGridViewTextBoxColumn18.ReadOnly = true;
            // 
            // dataGridViewTextBoxColumn19
            // 
            dataGridViewTextBoxColumn19.HeaderText = "SL";
            dataGridViewTextBoxColumn19.Name = "dataGridViewTextBoxColumn19";
            dataGridViewTextBoxColumn19.ReadOnly = true;
            // 
            // dataGridViewTextBoxColumn20
            // 
            dataGridViewTextBoxColumn20.HeaderText = "TP";
            dataGridViewTextBoxColumn20.Name = "dataGridViewTextBoxColumn20";
            dataGridViewTextBoxColumn20.ReadOnly = true;
            // 
            // dataGridViewTextBoxColumn21
            // 
            dataGridViewTextBoxColumn21.HeaderText = "Ticket";
            dataGridViewTextBoxColumn21.Name = "dataGridViewTextBoxColumn21";
            dataGridViewTextBoxColumn21.ReadOnly = true;
            // 
            // dataGridViewTextBoxColumn22
            // 
            dataGridViewTextBoxColumn22.HeaderText = "Status";
            dataGridViewTextBoxColumn22.Name = "dataGridViewTextBoxColumn22";
            dataGridViewTextBoxColumn22.ReadOnly = true;
            // 
            // dataGridViewTextBoxColumn23
            // 
            dataGridViewTextBoxColumn23.HeaderText = "Exec Price";
            dataGridViewTextBoxColumn23.Name = "dataGridViewTextBoxColumn23";
            dataGridViewTextBoxColumn23.ReadOnly = true;
            // 
            // dataGridViewTextBoxColumn24
            // 
            dataGridViewTextBoxColumn24.HeaderText = "Error";
            dataGridViewTextBoxColumn24.Name = "dataGridViewTextBoxColumn24";
            dataGridViewTextBoxColumn24.ReadOnly = true;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(13, 13, 19);
            ClientSize = new Size(1264, 890);
            Controls.Add(_layoutRoot);
            DoubleBuffered = true;
            Font = new Font("Segoe UI", 9F);
            ForeColor = Color.FromArgb(218, 218, 230);
            MinimumSize = new Size(1100, 720);
            Name = "MainForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "MT5 Trading Bot — Professional";
            _layoutRoot.ResumeLayout(false);
            _pnlHeader.ResumeLayout(false);
            _pnlConnBar.ResumeLayout(false);
            _pnlConnBar.PerformLayout();
            _pnlAccountBar.ResumeLayout(false);
            _tabControl.ResumeLayout(false);
            _tabTrade.ResumeLayout(false);
            _pnlTradeLeft.ResumeLayout(false);
            _pnlTradeLeft.PerformLayout();
            _pnlRR.ResumeLayout(false);
            _pnlTradeRight.ResumeLayout(false);
            _pnlTradeRight.PerformLayout();
            _tabPositions.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)_gridPos).EndInit();
            _tabHistory.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)_gridHistory).EndInit();
            _tabBot.ResumeLayout(false);
            _pnlBotCard.ResumeLayout(false);
            _pnlBotCard.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)_nudRisk).EndInit();
            ((System.ComponentModel.ISupportInitialize)_nudMinRR).EndInit();
            ((System.ComponentModel.ISupportInitialize)_nudMaxTrades).EndInit();
            ((System.ComponentModel.ISupportInitialize)_nudPollMs).EndInit();
            ((System.ComponentModel.ISupportInitialize)_nudRetry).EndInit();
            ((System.ComponentModel.ISupportInitialize)_nudDrawdownPct).EndInit();
            _tabClaude.ResumeLayout(false);
            _pnlClaudeCard.ResumeLayout(false);
            _pnlClaudeCard.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)_nudClaudePollSec).EndInit();
            _pnlClaudePromptCard.ResumeLayout(false);
            _pnlClaudePromptCard.PerformLayout();
            _tabLog.ResumeLayout(false);
            ResumeLayout(false);
        }
        private DataGridViewTextBoxColumn dataGridViewTextBoxColumn1;
        private DataGridViewTextBoxColumn dataGridViewTextBoxColumn2;
        private DataGridViewTextBoxColumn dataGridViewTextBoxColumn3;
        private DataGridViewTextBoxColumn dataGridViewTextBoxColumn4;
        private DataGridViewTextBoxColumn dataGridViewTextBoxColumn5;
        private DataGridViewTextBoxColumn dataGridViewTextBoxColumn6;
        private DataGridViewTextBoxColumn dataGridViewTextBoxColumn7;
        private DataGridViewTextBoxColumn dataGridViewTextBoxColumn8;
        private DataGridViewTextBoxColumn dataGridViewTextBoxColumn9;
        private DataGridViewTextBoxColumn dataGridViewTextBoxColumn10;
        private DataGridViewTextBoxColumn dataGridViewTextBoxColumn11;
        private DataGridViewTextBoxColumn dataGridViewTextBoxColumn12;
        private DataGridViewTextBoxColumn dataGridViewTextBoxColumn13;
        private DataGridViewTextBoxColumn dataGridViewTextBoxColumn14;
        private DataGridViewTextBoxColumn dataGridViewTextBoxColumn15;
        private DataGridViewTextBoxColumn dataGridViewTextBoxColumn16;
        private DataGridViewTextBoxColumn dataGridViewTextBoxColumn17;
        private DataGridViewTextBoxColumn dataGridViewTextBoxColumn18;
        private DataGridViewTextBoxColumn dataGridViewTextBoxColumn19;
        private DataGridViewTextBoxColumn dataGridViewTextBoxColumn20;
        private DataGridViewTextBoxColumn dataGridViewTextBoxColumn21;
        private DataGridViewTextBoxColumn dataGridViewTextBoxColumn22;
        private DataGridViewTextBoxColumn dataGridViewTextBoxColumn23;
        private DataGridViewTextBoxColumn dataGridViewTextBoxColumn24;
    }
}

