#nullable enable
using System.Drawing;
using System.Windows.Forms;

namespace AIForexBot.Forms
{
    partial class TradingDashboard
    {
        private System.ComponentModel.IContainer? components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();

            // ── Instantiate ───────────────────────────────────────────
            pnlHeader               = new Panel();
            pnlHeaderDivider        = new Panel();
            pnlHeaderLeft           = new Panel();
            pnlHeaderRight          = new Panel();
            pnlHeaderButtons        = new FlowLayoutPanel();

            lblBotName              = new Label();
            lblAccountNumber        = new Label();
            lblBrokerName           = new Label();
            lblConnectionStatus     = new Label();

            btnStartBot             = new Button();
            btnStopBot              = new Button();
            btnPauseBot             = new Button();
            btnEmergencyStop        = new Button();

            tabMain                 = new TabControl();
            tabPageDashboard        = new TabPage();
            tabPageSignals          = new TabPage();
            tabPageOpenTrades       = new TabPage();
            tabPageRiskSettings     = new TabPage();
            tabPageStrategySettings = new TabPage();
            tabPageLogs             = new TabPage();
            tabPageBacktest         = new TabPage();

            // Dashboard
            pnlDashContent          = new Panel();
            pnlDashTop              = new Panel();
            tlpDashCards            = new TableLayoutPanel();

            pnlAccountSummaryWrap   = new Panel();
            pnlAccountSummary       = new Panel();
            pnlAccHdr               = new Panel();
            lblAccountSummaryTitle  = new Label();
            lblBalanceTitle         = new Label(); lblBalance         = new Label();
            lblEquityTitle          = new Label(); lblEquity          = new Label();
            lblMarginTitle          = new Label(); lblMargin          = new Label();
            lblFreeMarginTitle      = new Label(); lblFreeMargin      = new Label();
            lblDailyPLTitle         = new Label(); lblDailyProfitLoss = new Label();

            pnlSignalPanelWrap      = new Panel();
            pnlSignalPanel          = new Panel();
            pnlSigHdr               = new Panel();
            lblSignalPanelTitle     = new Label();
            lblAIScoreTitle         = new Label(); lblAIScore         = new Label();
            lblSignalDirTitle       = new Label(); lblSignalDirection  = new Label();
            lblSignalConfTitle      = new Label(); lblSignalConfidence = new Label();
            lblSignalReasonTitle    = new Label(); lblSignalReason     = new Label();

            pnlRiskSummaryWrap      = new Panel();
            pnlRiskSummary          = new Panel();
            pnlRiskHdr              = new Panel();
            lblRiskSummaryTitle     = new Label();
            lblRiskPercentTitle     = new Label(); lblRiskPercent       = new Label();
            lblLotSizeTitle         = new Label(); lblLotSize           = new Label();
            lblMaxDailyLossTitle    = new Label(); lblMaxDailyLossValue = new Label();
            lblTradesTodayTitle     = new Label(); lblTradesToday       = new Label();

            pnlSymbolWatchWrap      = new Panel();
            pnlSymbolWatchHdr       = new Panel();
            lblSymbolWatchTitle     = new Label();
            gridSymbolWatch         = new DataGridView();

            // Signals tab
            pnlSignalsContent       = new Panel();
            pnlSignalsHdr           = new Panel();
            lblSignalsTitle         = new Label();
            gridSignals             = new DataGridView();

            // Open Trades tab
            pnlOpenTradesContent    = new Panel();
            pnlOpenTradesHdr        = new Panel();
            lblOpenTradesTitle      = new Label();
            lblOpenTradesSummary    = new Label();
            gridOpenTrades          = new DataGridView();

            // Risk Settings tab
            pnlRiskSettingsContent  = new Panel();
            tlpRiskSettings         = new TableLayoutPanel();
            grpRiskParams           = new GroupBox();
            lblRiskPercentSetting   = new Label(); numRiskPercent    = new NumericUpDown();
            lblMaxDailyLossSetting  = new Label(); numMaxDailyLoss   = new NumericUpDown();
            lblMaxTradesSetting     = new Label(); numMaxTrades      = new NumericUpDown();
            lblSpreadLimitSetting   = new Label(); numSpreadLimit    = new NumericUpDown();
            grpRiskFilters          = new GroupBox();
            chkNewsFilter           = new CheckBox();
            chkAutoTrading          = new CheckBox();
            chkMaxDailyDrawdown     = new CheckBox();

            // Strategy Settings tab
            pnlStrategyContent      = new Panel();
            tlpStrategy             = new TableLayoutPanel();
            grpSymbolConfig         = new GroupBox();
            lblSymbolSetting        = new Label(); cmbSymbol    = new ComboBox();
            lblTimeframeSetting     = new Label(); cmbTimeFrame = new ComboBox();
            lblSessionSetting       = new Label(); cmbSession   = new ComboBox();
            grpAiConfig             = new GroupBox();
            lblAiModelSetting       = new Label(); cmbAiModel   = new ComboBox();
            lblMinScoreSetting      = new Label(); numMinAiScore = new NumericUpDown();

            // Logs tab
            pnlLogsContent          = new Panel();
            pnlLogsHdr              = new Panel();
            lblLogsTitle            = new Label();
            btnClearLogs            = new Button();
            txtBotLogs              = new RichTextBox();

            // Backtest tab
            pnlBacktestContent      = new Panel();
            pnlBacktestControls     = new Panel();
            grpBacktestParams       = new GroupBox();
            lblBtFromDate           = new Label(); dtpBacktestFrom    = new DateTimePicker();
            lblBtToDate             = new Label(); dtpBacktestTo      = new DateTimePicker();
            lblBtSymbol             = new Label(); cmbBacktestSymbol  = new ComboBox();
            btnRunBacktest          = new Button();
            pnlBacktestResultsWrap  = new Panel();
            pnlBacktestResultsHdr   = new Panel();
            lblBacktestResultsTitle = new Label();
            gridBacktestResults     = new DataGridView();

            // ── Suspend layout ────────────────────────────────────────
            SuspendLayout();
            pnlHeader.SuspendLayout();
            pnlHeaderLeft.SuspendLayout();
            pnlHeaderRight.SuspendLayout();
            pnlHeaderButtons.SuspendLayout();
            tlpDashCards.SuspendLayout();
            pnlDashTop.SuspendLayout();
            pnlDashContent.SuspendLayout();
            tabMain.SuspendLayout();
            tabPageDashboard.SuspendLayout();
            tabPageSignals.SuspendLayout();
            tabPageOpenTrades.SuspendLayout();
            tabPageRiskSettings.SuspendLayout();
            tabPageStrategySettings.SuspendLayout();
            tabPageLogs.SuspendLayout();
            tabPageBacktest.SuspendLayout();

            // ═══════════════════════════════════════════════════════════
            // FORM
            // ═══════════════════════════════════════════════════════════
            Text            = "AI Forex Trading Bot  —  Dashboard";
            Size            = new Size(1440, 900);
            MinimumSize     = new Size(1100, 720);
            BackColor       = BgDark;
            ForeColor       = TextPrimary;
            Font            = new Font("Segoe UI", 9.25f, FontStyle.Regular, GraphicsUnit.Point);
            StartPosition   = FormStartPosition.CenterScreen;
            Controls.Add(tabMain);
            Controls.Add(pnlHeaderDivider);
            Controls.Add(pnlHeader);

            // ═══════════════════════════════════════════════════════════
            // HEADER PANEL
            // ═══════════════════════════════════════════════════════════
            pnlHeader.BackColor = BgHeader;
            pnlHeader.Dock      = DockStyle.Top;
            pnlHeader.Height    = 68;
            pnlHeader.Padding   = new Padding(12, 0, 12, 0);
            pnlHeader.Controls.Add(pnlHeaderRight);
            pnlHeader.Controls.Add(pnlHeaderLeft);

            pnlHeaderDivider.BackColor = BorderCol;
            pnlHeaderDivider.Dock      = DockStyle.Top;
            pnlHeaderDivider.Height    = 1;

            // Header left — bot name + account info
            pnlHeaderLeft.BackColor = Color.Transparent;
            pnlHeaderLeft.Dock      = DockStyle.Left;
            pnlHeaderLeft.Width     = 520;
            pnlHeaderLeft.Padding   = new Padding(0, 10, 0, 10);
            pnlHeaderLeft.Controls.Add(lblBrokerName);
            pnlHeaderLeft.Controls.Add(lblAccountNumber);
            pnlHeaderLeft.Controls.Add(lblBotName);

            lblBotName.Text      = "AI FOREX BOT";
            lblBotName.Font      = new Font("Segoe UI", 16f, FontStyle.Bold, GraphicsUnit.Point);
            lblBotName.ForeColor = TextPrimary;
            lblBotName.Location  = new Point(0, 6);
            lblBotName.AutoSize  = true;

            lblAccountNumber.Text      = "Account: #12345678";
            lblAccountNumber.Font      = new Font("Segoe UI", 8.5f, FontStyle.Regular, GraphicsUnit.Point);
            lblAccountNumber.ForeColor = TextSecondary;
            lblAccountNumber.Location  = new Point(2, 38);
            lblAccountNumber.AutoSize  = true;

            lblBrokerName.Text      = "Broker: IC Markets  |  Demo";
            lblBrokerName.Font      = new Font("Segoe UI", 8.5f, FontStyle.Regular, GraphicsUnit.Point);
            lblBrokerName.ForeColor = TextSecondary;
            lblBrokerName.Location  = new Point(160, 38);
            lblBrokerName.AutoSize  = true;

            // Header right — connection status + control buttons
            pnlHeaderRight.BackColor = Color.Transparent;
            pnlHeaderRight.Dock      = DockStyle.Right;
            pnlHeaderRight.Width     = 680;
            pnlHeaderRight.Padding   = new Padding(0, 14, 0, 14);
            pnlHeaderRight.Controls.Add(pnlHeaderButtons);
            pnlHeaderRight.Controls.Add(lblConnectionStatus);

            lblConnectionStatus.Text      = "● CONNECTING...";
            lblConnectionStatus.Font      = new Font("Segoe UI", 9f, FontStyle.Bold, GraphicsUnit.Point);
            lblConnectionStatus.ForeColor = ColYellow;
            lblConnectionStatus.Dock      = DockStyle.Right;
            lblConnectionStatus.TextAlign = ContentAlignment.MiddleRight;
            lblConnectionStatus.Width     = 160;

            // Control buttons (FlowLayout — right to left)
            pnlHeaderButtons.BackColor    = Color.Transparent;
            pnlHeaderButtons.Dock         = DockStyle.Right;
            pnlHeaderButtons.Width        = 510;
            pnlHeaderButtons.FlowDirection= FlowDirection.LeftToRight;
            pnlHeaderButtons.WrapContents = false;
            pnlHeaderButtons.Padding      = new Padding(0, 0, 10, 0);
            StyleHeaderButton(btnStartBot,      "▶  START",       ColGreen,  new Size(115, 38));
            StyleHeaderButton(btnStopBot,       "■  STOP",        ColRed,    new Size(105, 38));
            StyleHeaderButton(btnPauseBot,      "⏸  PAUSE",       ColYellow, new Size(110, 38));
            StyleHeaderButton(btnEmergencyStop, "🛑  EMERGENCY",  ColRed,    new Size(150, 38));
            btnEmergencyStop.Font      = new Font("Segoe UI", 8.5f, FontStyle.Bold, GraphicsUnit.Point);
            btnEmergencyStop.BackColor = Color.FromArgb(80, 20, 20);
            pnlHeaderButtons.Controls.AddRange(new Control[] {
                btnStartBot, btnStopBot, btnPauseBot, btnEmergencyStop });

            btnStartBot.Click       += btnStartBot_Click;
            btnStopBot.Click        += btnStopBot_Click;
            btnPauseBot.Click       += btnPauseBot_Click;
            btnEmergencyStop.Click  += btnEmergencyStop_Click;

            // ═══════════════════════════════════════════════════════════
            // MAIN TAB CONTROL
            // ═══════════════════════════════════════════════════════════
            tabMain.Dock          = DockStyle.Fill;
            tabMain.BackColor     = BgDark;
            tabMain.Padding       = new Point(14, 6);
            tabMain.ItemSize      = new Size(120, 30);
            tabMain.SizeMode      = TabSizeMode.Fixed;
            tabMain.Appearance    = TabAppearance.Normal;
            tabMain.Font          = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
            tabMain.TabPages.AddRange(new TabPage[] {
                tabPageDashboard, tabPageSignals, tabPageOpenTrades,
                tabPageRiskSettings, tabPageStrategySettings,
                tabPageLogs, tabPageBacktest });

            ConfigTabPage(tabPageDashboard,        "  Dashboard");
            ConfigTabPage(tabPageSignals,           "  Signals");
            ConfigTabPage(tabPageOpenTrades,        "  Open Trades");
            ConfigTabPage(tabPageRiskSettings,      "  Risk");
            ConfigTabPage(tabPageStrategySettings,  "  Strategy");
            ConfigTabPage(tabPageLogs,              "  Logs");
            ConfigTabPage(tabPageBacktest,          "  Backtest");

            // ═══════════════════════════════════════════════════════════
            // TAB: DASHBOARD
            // ═══════════════════════════════════════════════════════════
            tabPageDashboard.Controls.Add(pnlDashContent);

            pnlDashContent.Dock    = DockStyle.Fill;
            pnlDashContent.Padding = new Padding(8);
            pnlDashContent.Controls.Add(pnlSymbolWatchWrap);
            pnlDashContent.Controls.Add(pnlDashTop);

            // Top row — 3 cards
            pnlDashTop.Dock   = DockStyle.Top;
            pnlDashTop.Height = 265;
            pnlDashTop.Controls.Add(tlpDashCards);

            tlpDashCards.Dock        = DockStyle.Fill;
            tlpDashCards.ColumnCount = 3;
            tlpDashCards.RowCount    = 1;
            tlpDashCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            tlpDashCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            tlpDashCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));
            tlpDashCards.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            tlpDashCards.Controls.Add(pnlAccountSummaryWrap, 0, 0);
            tlpDashCards.Controls.Add(pnlSignalPanelWrap,    1, 0);
            tlpDashCards.Controls.Add(pnlRiskSummaryWrap,    2, 0);

            // ── ACCOUNT SUMMARY CARD ──────────────────────────────────
            BuildCard(pnlAccountSummaryWrap, pnlAccountSummary, pnlAccHdr,
                      lblAccountSummaryTitle, "Account Summary");
            BuildStatRow(pnlAccountSummary, lblBalanceTitle,    "Balance",      lblBalance,         12);
            BuildStatRow(pnlAccountSummary, lblEquityTitle,     "Equity",       lblEquity,          50);
            BuildStatRow(pnlAccountSummary, lblMarginTitle,     "Margin",       lblMargin,          88);
            BuildStatRow(pnlAccountSummary, lblFreeMarginTitle, "Free Margin",  lblFreeMargin,     126);
            BuildStatRow(pnlAccountSummary, lblDailyPLTitle,    "Daily P/L",    lblDailyProfitLoss,168);
            lblDailyProfitLoss.Font = new Font("Segoe UI", 11f, FontStyle.Bold, GraphicsUnit.Point);

            // ── SIGNAL PANEL CARD ─────────────────────────────────────
            BuildCard(pnlSignalPanelWrap, pnlSignalPanel, pnlSigHdr,
                      lblSignalPanelTitle, "Current AI Signal");

            lblAIScoreTitle.Text      = "AI SCORE";
            lblAIScoreTitle.Font      = new Font("Segoe UI", 8f, FontStyle.Regular, GraphicsUnit.Point);
            lblAIScoreTitle.ForeColor = TextMuted;
            lblAIScoreTitle.Location  = new Point(12, 12);
            lblAIScoreTitle.AutoSize  = true;
            pnlSignalPanel.Controls.Add(lblAIScoreTitle);

            lblAIScore.Text      = "--";
            lblAIScore.Font      = new Font("Segoe UI", 34f, FontStyle.Bold, GraphicsUnit.Point);
            lblAIScore.ForeColor = ColGreen;
            lblAIScore.Location  = new Point(10, 26);
            lblAIScore.Size      = new Size(100, 60);
            pnlSignalPanel.Controls.Add(lblAIScore);

            BuildStatRow(pnlSignalPanel, lblSignalDirTitle,   "Direction",  lblSignalDirection,  92);
            BuildStatRow(pnlSignalPanel, lblSignalConfTitle,  "Confidence", lblSignalConfidence, 128);
            BuildStatRow(pnlSignalPanel, lblSignalReasonTitle,"Reason",     lblSignalReason,     168);
            lblSignalReason.Width = 260;

            // ── RISK SUMMARY CARD ─────────────────────────────────────
            BuildCard(pnlRiskSummaryWrap, pnlRiskSummary, pnlRiskHdr,
                      lblRiskSummaryTitle, "Risk Summary");
            BuildStatRow(pnlRiskSummary, lblRiskPercentTitle,  "Risk %",        lblRiskPercent,      12);
            BuildStatRow(pnlRiskSummary, lblLotSizeTitle,      "Lot Size",      lblLotSize,          50);
            BuildStatRow(pnlRiskSummary, lblMaxDailyLossTitle, "Max Daily Loss",lblMaxDailyLossValue, 88);
            BuildStatRow(pnlRiskSummary, lblTradesTodayTitle,  "Trades Today",  lblTradesToday,      126);

            // ── SYMBOL WATCH ──────────────────────────────────────────
            pnlSymbolWatchWrap.Dock    = DockStyle.Fill;
            pnlSymbolWatchWrap.Padding = new Padding(0, 8, 0, 0);
            pnlSymbolWatchWrap.Controls.Add(gridSymbolWatch);
            pnlSymbolWatchWrap.Controls.Add(pnlSymbolWatchHdr);

            pnlSymbolWatchHdr.Dock      = DockStyle.Top;
            pnlSymbolWatchHdr.Height    = 32;
            pnlSymbolWatchHdr.BackColor = BgCardHeader;
            pnlSymbolWatchHdr.Controls.Add(lblSymbolWatchTitle);

            lblSymbolWatchTitle.Text      = "  Symbol Watch";
            lblSymbolWatchTitle.Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold, GraphicsUnit.Point);
            lblSymbolWatchTitle.ForeColor = TextSecondary;
            lblSymbolWatchTitle.Dock      = DockStyle.Fill;
            lblSymbolWatchTitle.TextAlign = ContentAlignment.MiddleLeft;

            ConfigGrid(gridSymbolWatch);
            gridSymbolWatch.Dock = DockStyle.Fill;
            gridSymbolWatch.Columns.AddRange(new DataGridViewColumn[] {
                MakeCol("Symbol",  "SYMBOL",  90,  false),
                MakeCol("Bid",     "BID",     100, false),
                MakeCol("Ask",     "ASK",     100, false),
                MakeCol("Spread",  "SPREAD",  80,  false),
                MakeCol("Trend",   "TREND",   100, false),
                MakeCol("Signal",  "SIGNAL",  true),
            });

            // ═══════════════════════════════════════════════════════════
            // TAB: SIGNALS
            // ═══════════════════════════════════════════════════════════
            tabPageSignals.Controls.Add(pnlSignalsContent);
            pnlSignalsContent.Dock    = DockStyle.Fill;
            pnlSignalsContent.Padding = new Padding(8);
            pnlSignalsContent.Controls.Add(gridSignals);
            pnlSignalsContent.Controls.Add(pnlSignalsHdr);

            pnlSignalsHdr.Dock      = DockStyle.Top;
            pnlSignalsHdr.Height    = 36;
            pnlSignalsHdr.BackColor = BgCardHeader;
            pnlSignalsHdr.Controls.Add(lblSignalsTitle);

            lblSignalsTitle.Text      = "  AI Signal History";
            lblSignalsTitle.Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold, GraphicsUnit.Point);
            lblSignalsTitle.ForeColor = TextSecondary;
            lblSignalsTitle.Dock      = DockStyle.Fill;
            lblSignalsTitle.TextAlign = ContentAlignment.MiddleLeft;

            ConfigGrid(gridSignals);
            gridSignals.Dock = DockStyle.Fill;
            gridSignals.Columns.AddRange(new DataGridViewColumn[] {
                MakeCol("Time",       "TIME",       100, false),
                MakeCol("Symbol",     "SYMBOL",     90,  false),
                MakeCol("Direction",  "DIRECTION",  90,  false),
                MakeCol("AiScore",    "AI SCORE",   80,  false),
                MakeCol("Confidence", "CONFIDENCE", 100, false),
                MakeCol("Reason",     "REASON",     true),
                MakeCol("Status",     "STATUS",     90,  false),
            });

            // ═══════════════════════════════════════════════════════════
            // TAB: OPEN TRADES
            // ═══════════════════════════════════════════════════════════
            tabPageOpenTrades.Controls.Add(pnlOpenTradesContent);
            pnlOpenTradesContent.Dock    = DockStyle.Fill;
            pnlOpenTradesContent.Padding = new Padding(8);
            pnlOpenTradesContent.Controls.Add(gridOpenTrades);
            pnlOpenTradesContent.Controls.Add(pnlOpenTradesHdr);

            pnlOpenTradesHdr.Dock      = DockStyle.Top;
            pnlOpenTradesHdr.Height    = 36;
            pnlOpenTradesHdr.BackColor = BgCardHeader;
            pnlOpenTradesHdr.Controls.Add(lblOpenTradesSummary);
            pnlOpenTradesHdr.Controls.Add(lblOpenTradesTitle);

            lblOpenTradesTitle.Text      = "  Open Positions";
            lblOpenTradesTitle.Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold, GraphicsUnit.Point);
            lblOpenTradesTitle.ForeColor = TextSecondary;
            lblOpenTradesTitle.Dock      = DockStyle.Left;
            lblOpenTradesTitle.Width     = 180;
            lblOpenTradesTitle.TextAlign = ContentAlignment.MiddleLeft;

            lblOpenTradesSummary.Text      = "Net P/L: +$16.50  |  2 positions";
            lblOpenTradesSummary.Font      = new Font("Segoe UI", 8.5f, FontStyle.Regular, GraphicsUnit.Point);
            lblOpenTradesSummary.ForeColor = ColGreen;
            lblOpenTradesSummary.Dock      = DockStyle.Right;
            lblOpenTradesSummary.Width     = 240;
            lblOpenTradesSummary.TextAlign = ContentAlignment.MiddleRight;
            lblOpenTradesSummary.Padding   = new Padding(0, 0, 10, 0);

            ConfigGrid(gridOpenTrades);
            gridOpenTrades.Dock = DockStyle.Fill;
            gridOpenTrades.Columns.AddRange(new DataGridViewColumn[] {
                MakeCol("Ticket", "TICKET",  90,  false),
                MakeCol("Symbol", "SYMBOL",  90,  false),
                MakeCol("Type",   "TYPE",    70,  false),
                MakeCol("Lot",    "LOT",     70,  false),
                MakeCol("Entry",  "ENTRY",   100, false),
                MakeCol("SL",     "STOP LOSS",  100, false),
                MakeCol("TP",     "TAKE PROFIT",100, false),
                MakeCol("Profit", "PROFIT",  true),
            });

            // ═══════════════════════════════════════════════════════════
            // TAB: RISK SETTINGS
            // ═══════════════════════════════════════════════════════════
            tabPageRiskSettings.Controls.Add(pnlRiskSettingsContent);
            pnlRiskSettingsContent.Dock    = DockStyle.Fill;
            pnlRiskSettingsContent.Padding = new Padding(16);

            tlpRiskSettings.Dock        = DockStyle.Top;
            tlpRiskSettings.Height      = 380;
            tlpRiskSettings.ColumnCount = 2;
            tlpRiskSettings.RowCount    = 1;
            tlpRiskSettings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            tlpRiskSettings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            tlpRiskSettings.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            tlpRiskSettings.Controls.Add(grpRiskParams,  0, 0);
            tlpRiskSettings.Controls.Add(grpRiskFilters, 1, 0);
            pnlRiskSettingsContent.Controls.Add(tlpRiskSettings);

            // Risk Parameters group
            StyleGroupBox(grpRiskParams, "Risk Parameters");
            grpRiskParams.Dock = DockStyle.Fill;
            BuildSettingRow(grpRiskParams, lblRiskPercentSetting,  "Risk per Trade (%)", numRiskPercent,  20,  0.01m, 10m,   1m,    2);
            BuildSettingRow(grpRiskParams, lblMaxDailyLossSetting, "Max Daily Loss ($)", numMaxDailyLoss, 65,  10m,   5000m, 300m,  0);
            BuildSettingRow(grpRiskParams, lblMaxTradesSetting,    "Max Trades / Day",   numMaxTrades,    110, 1m,    50m,   10m,   0);
            BuildSettingRow(grpRiskParams, lblSpreadLimitSetting,  "Spread Limit (pips)",numSpreadLimit,  155, 1m,    100m,  20m,   0);

            // Risk Filters group
            StyleGroupBox(grpRiskFilters, "Filters & Protections");
            grpRiskFilters.Dock = DockStyle.Fill;
            StyleCheckBox(chkNewsFilter,       "Block trading during high-impact news", 28);
            StyleCheckBox(chkAutoTrading,      "Enable auto-trading",                  68);
            StyleCheckBox(chkMaxDailyDrawdown, "Stop bot when max daily loss hit",    108);
            chkAutoTrading.ForeColor = ColGreen;
            grpRiskFilters.Controls.AddRange(new Control[] {
                chkNewsFilter, chkAutoTrading, chkMaxDailyDrawdown });

            // ═══════════════════════════════════════════════════════════
            // TAB: STRATEGY SETTINGS
            // ═══════════════════════════════════════════════════════════
            tabPageStrategySettings.Controls.Add(pnlStrategyContent);
            pnlStrategyContent.Dock    = DockStyle.Fill;
            pnlStrategyContent.Padding = new Padding(16);

            tlpStrategy.Dock        = DockStyle.Top;
            tlpStrategy.Height      = 340;
            tlpStrategy.ColumnCount = 2;
            tlpStrategy.RowCount    = 1;
            tlpStrategy.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            tlpStrategy.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            tlpStrategy.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            tlpStrategy.Controls.Add(grpSymbolConfig, 0, 0);
            tlpStrategy.Controls.Add(grpAiConfig,     1, 0);
            pnlStrategyContent.Controls.Add(tlpStrategy);

            // Symbol Config group
            StyleGroupBox(grpSymbolConfig, "Symbol & Timeframe");
            grpSymbolConfig.Dock = DockStyle.Fill;
            BuildComboRow(grpSymbolConfig, lblSymbolSetting,    "Symbol",    cmbSymbol,
                          new[] { "XAUUSD", "EURUSD", "GBPUSD", "USDJPY", "BTCUSD" }, 20);
            BuildComboRow(grpSymbolConfig, lblTimeframeSetting, "Timeframe", cmbTimeFrame,
                          new[] { "M1", "M5", "M15", "M30", "H1", "H4", "D1" }, 65);
            BuildComboRow(grpSymbolConfig, lblSessionSetting,   "Session",   cmbSession,
                          new[] { "London", "New York", "Tokyo", "Sydney", "All Sessions" }, 110);

            // AI Config group
            StyleGroupBox(grpAiConfig, "AI Configuration");
            grpAiConfig.Dock = DockStyle.Fill;
            BuildComboRow(grpAiConfig, lblAiModelSetting, "AI Model", cmbAiModel,
                          new[] { "Claude Haiku 4.5", "Claude Sonnet 4.6", "Claude Opus 4.8" }, 20);
            BuildSettingRow(grpAiConfig, lblMinScoreSetting, "Min AI Score to Trade", numMinAiScore, 65, 50m, 100m, 75m, 0);

            // ═══════════════════════════════════════════════════════════
            // TAB: LOGS
            // ═══════════════════════════════════════════════════════════
            tabPageLogs.Controls.Add(pnlLogsContent);
            pnlLogsContent.Dock    = DockStyle.Fill;
            pnlLogsContent.Padding = new Padding(8);
            pnlLogsContent.Controls.Add(txtBotLogs);
            pnlLogsContent.Controls.Add(pnlLogsHdr);

            pnlLogsHdr.Dock      = DockStyle.Top;
            pnlLogsHdr.Height    = 36;
            pnlLogsHdr.BackColor = BgCardHeader;
            pnlLogsHdr.Controls.Add(btnClearLogs);
            pnlLogsHdr.Controls.Add(lblLogsTitle);

            lblLogsTitle.Text      = "  Bot Activity Log";
            lblLogsTitle.Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold, GraphicsUnit.Point);
            lblLogsTitle.ForeColor = TextSecondary;
            lblLogsTitle.Dock      = DockStyle.Left;
            lblLogsTitle.Width     = 180;
            lblLogsTitle.TextAlign = ContentAlignment.MiddleLeft;

            btnClearLogs.Text      = "Clear";
            btnClearLogs.Dock      = DockStyle.Right;
            btnClearLogs.Width     = 80;
            btnClearLogs.BackColor = BgButton;
            btnClearLogs.ForeColor = TextSecondary;
            btnClearLogs.FlatStyle = FlatStyle.Flat;
            btnClearLogs.FlatAppearance.BorderColor = BorderCol;
            btnClearLogs.Click    += btnClearLogs_Click;

            txtBotLogs.Dock            = DockStyle.Fill;
            txtBotLogs.BackColor       = BgDark;
            txtBotLogs.ForeColor       = TextPrimary;
            txtBotLogs.Font            = new Font("Cascadia Code", 8.5f, FontStyle.Regular, GraphicsUnit.Point);
            txtBotLogs.BorderStyle     = BorderStyle.None;
            txtBotLogs.ReadOnly        = true;
            txtBotLogs.ScrollBars      = RichTextBoxScrollBars.Vertical;
            txtBotLogs.WordWrap        = false;

            // ═══════════════════════════════════════════════════════════
            // TAB: BACKTEST
            // ═══════════════════════════════════════════════════════════
            tabPageBacktest.Controls.Add(pnlBacktestContent);
            pnlBacktestContent.Dock    = DockStyle.Fill;
            pnlBacktestContent.Padding = new Padding(8);
            pnlBacktestContent.Controls.Add(pnlBacktestResultsWrap);
            pnlBacktestContent.Controls.Add(pnlBacktestControls);

            pnlBacktestControls.Dock      = DockStyle.Top;
            pnlBacktestControls.Height    = 120;
            pnlBacktestControls.BackColor = BgCard;
            pnlBacktestControls.Padding   = new Padding(12);
            pnlBacktestControls.Controls.Add(grpBacktestParams);

            StyleGroupBox(grpBacktestParams, "Backtest Parameters");
            grpBacktestParams.Dock = DockStyle.Fill;
            BuildComboRow(grpBacktestParams, lblBtSymbol, "Symbol", cmbBacktestSymbol,
                          new[] { "XAUUSD", "EURUSD", "GBPUSD", "USDJPY" }, 20);

            lblBtFromDate.Text      = "From Date";
            lblBtFromDate.ForeColor = TextSecondary;
            lblBtFromDate.Font      = new Font("Segoe UI", 8.5f, FontStyle.Regular, GraphicsUnit.Point);
            lblBtFromDate.Location  = new Point(280, 22);
            lblBtFromDate.AutoSize  = true;
            grpBacktestParams.Controls.Add(lblBtFromDate);

            dtpBacktestFrom.Location  = new Point(280, 40);
            dtpBacktestFrom.Size      = new Size(150, 25);
            dtpBacktestFrom.BackColor = BgInput;
            dtpBacktestFrom.ForeColor = TextPrimary;
            dtpBacktestFrom.CalendarForeColor = TextPrimary;
            dtpBacktestFrom.CalendarMonthBackground = BgCard;
            dtpBacktestFrom.Format    = DateTimePickerFormat.Short;
            grpBacktestParams.Controls.Add(dtpBacktestFrom);

            lblBtToDate.Text      = "To Date";
            lblBtToDate.ForeColor = TextSecondary;
            lblBtToDate.Font      = new Font("Segoe UI", 8.5f, FontStyle.Regular, GraphicsUnit.Point);
            lblBtToDate.Location  = new Point(450, 22);
            lblBtToDate.AutoSize  = true;
            grpBacktestParams.Controls.Add(lblBtToDate);

            dtpBacktestTo.Location  = new Point(450, 40);
            dtpBacktestTo.Size      = new Size(150, 25);
            dtpBacktestTo.BackColor = BgInput;
            dtpBacktestTo.ForeColor = TextPrimary;
            dtpBacktestTo.CalendarForeColor = TextPrimary;
            dtpBacktestTo.CalendarMonthBackground = BgCard;
            dtpBacktestTo.Format    = DateTimePickerFormat.Short;
            grpBacktestParams.Controls.Add(dtpBacktestTo);

            btnRunBacktest.Text      = "▶  Run Backtest";
            btnRunBacktest.Location  = new Point(620, 35);
            btnRunBacktest.Size      = new Size(140, 34);
            btnRunBacktest.BackColor = Color.FromArgb(20, 60, 30);
            btnRunBacktest.ForeColor = ColGreen;
            btnRunBacktest.FlatStyle = FlatStyle.Flat;
            btnRunBacktest.FlatAppearance.BorderColor = ColGreen;
            btnRunBacktest.Font      = new Font("Segoe UI", 9f, FontStyle.Bold, GraphicsUnit.Point);
            btnRunBacktest.Click    += btnRunBacktest_Click;
            grpBacktestParams.Controls.Add(btnRunBacktest);

            pnlBacktestResultsWrap.Dock    = DockStyle.Fill;
            pnlBacktestResultsWrap.Padding = new Padding(0, 8, 0, 0);
            pnlBacktestResultsWrap.Controls.Add(gridBacktestResults);
            pnlBacktestResultsWrap.Controls.Add(pnlBacktestResultsHdr);

            pnlBacktestResultsHdr.Dock      = DockStyle.Top;
            pnlBacktestResultsHdr.Height    = 32;
            pnlBacktestResultsHdr.BackColor = BgCardHeader;
            pnlBacktestResultsHdr.Controls.Add(lblBacktestResultsTitle);

            lblBacktestResultsTitle.Text      = "  Backtest Results";
            lblBacktestResultsTitle.Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold, GraphicsUnit.Point);
            lblBacktestResultsTitle.ForeColor = TextSecondary;
            lblBacktestResultsTitle.Dock      = DockStyle.Fill;
            lblBacktestResultsTitle.TextAlign = ContentAlignment.MiddleLeft;

            ConfigGrid(gridBacktestResults);
            gridBacktestResults.Dock = DockStyle.Fill;
            gridBacktestResults.Columns.AddRange(new DataGridViewColumn[] {
                MakeCol("From",       "FROM DATE",  110, false),
                MakeCol("To",         "TO DATE",    110, false),
                MakeCol("Symbol",     "SYMBOL",     90,  false),
                MakeCol("Trades",     "TRADES",     80,  false),
                MakeCol("WinRate",    "WIN RATE",   90,  false),
                MakeCol("NetProfit",  "NET PROFIT", 110, false),
                MakeCol("ProfitFact", "PROFIT FACTOR", true),
            });

            // ── Resume layout ─────────────────────────────────────────
            tabPageBacktest.ResumeLayout(false);
            tabPageLogs.ResumeLayout(false);
            tabPageStrategySettings.ResumeLayout(false);
            tabPageRiskSettings.ResumeLayout(false);
            tabPageOpenTrades.ResumeLayout(false);
            tabPageSignals.ResumeLayout(false);
            tabPageDashboard.ResumeLayout(false);
            tabMain.ResumeLayout(false);
            pnlDashContent.ResumeLayout(false);
            pnlDashTop.ResumeLayout(false);
            tlpDashCards.ResumeLayout(false);
            pnlHeaderButtons.ResumeLayout(false);
            pnlHeaderRight.ResumeLayout(false);
            pnlHeaderLeft.ResumeLayout(false);
            pnlHeader.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }

        // ── Layout helpers ────────────────────────────────────────────

        private static void ConfigTabPage(TabPage page, string text)
        {
            page.Text      = text;
            page.BackColor = BgDark;
            page.ForeColor = TextPrimary;
            page.Padding   = new Padding(0);
        }

        private static void StyleHeaderButton(Button btn, string text, Color accent, Size size)
        {
            btn.Text      = text;
            btn.Size      = size;
            btn.Margin    = new Padding(0, 0, 6, 0);
            btn.BackColor = BgButton;
            btn.ForeColor = accent;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderColor = accent;
            btn.Font      = new Font("Segoe UI", 8.5f, FontStyle.Bold, GraphicsUnit.Point);
            btn.Cursor    = Cursors.Hand;
        }

        private static void BuildCard(Panel wrapper, Panel body, Panel header,
                                      Label title, string titleText)
        {
            wrapper.Dock    = DockStyle.Fill;
            wrapper.Padding = new Padding(4);

            body.Dock      = DockStyle.Fill;
            body.BackColor = BgCard;
            body.Padding   = new Padding(1);
            wrapper.Controls.Add(body);

            header.Dock      = DockStyle.Top;
            header.Height    = 34;
            header.BackColor = BgCardHeader;
            body.Controls.Add(header);

            title.Text      = "  " + titleText;
            title.Font      = new Font("Segoe UI", 9f, FontStyle.Bold, GraphicsUnit.Point);
            title.ForeColor = TextSecondary;
            title.Dock      = DockStyle.Fill;
            title.TextAlign = ContentAlignment.MiddleLeft;
            header.Controls.Add(title);
        }

        private static void BuildStatRow(Panel parent, Label titleLbl, string titleText,
                                         Label valueLbl, int yPos)
        {
            titleLbl.Text      = titleText;
            titleLbl.Font      = new Font("Segoe UI", 7.5f, FontStyle.Regular, GraphicsUnit.Point);
            titleLbl.ForeColor = TextMuted;
            titleLbl.Location  = new Point(12, 38 + yPos);
            titleLbl.AutoSize  = true;
            parent.Controls.Add(titleLbl);

            valueLbl.Text      = "--";
            valueLbl.Font      = new Font("Segoe UI", 11f, FontStyle.Bold, GraphicsUnit.Point);
            valueLbl.ForeColor = TextPrimary;
            valueLbl.Location  = new Point(130, 34 + yPos);
            valueLbl.Size      = new Size(180, 24);
            parent.Controls.Add(valueLbl);
        }

        private static void ConfigGrid(DataGridView grid)
        {
            grid.BackgroundColor                    = BgDark;
            grid.DefaultCellStyle.BackColor         = Color.FromArgb(19, 19, 30);
            grid.DefaultCellStyle.ForeColor         = TextPrimary;
            grid.DefaultCellStyle.SelectionBackColor= BorderCol;
            grid.DefaultCellStyle.SelectionForeColor= TextPrimary;
            grid.DefaultCellStyle.Font              = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
            grid.DefaultCellStyle.Padding           = new Padding(4, 0, 4, 0);
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(22, 22, 36);
            grid.ColumnHeadersDefaultCellStyle.BackColor  = BgCardHeader;
            grid.ColumnHeadersDefaultCellStyle.ForeColor  = TextSecondary;
            grid.ColumnHeadersDefaultCellStyle.Font       = new Font("Segoe UI", 8f, FontStyle.Bold, GraphicsUnit.Point);
            grid.ColumnHeadersDefaultCellStyle.Padding    = new Padding(4, 0, 4, 0);
            grid.EnableHeadersVisualStyles    = false;
            grid.ColumnHeadersHeight          = 30;
            grid.ColumnHeadersHeightSizeMode  = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.RowTemplate.Height           = 28;
            grid.GridColor                    = BorderCol;
            grid.BorderStyle                  = BorderStyle.None;
            grid.RowHeadersVisible            = false;
            grid.AllowUserToAddRows           = false;
            grid.AllowUserToResizeRows        = false;
            grid.ReadOnly                     = true;
            grid.SelectionMode                = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect                  = false;
            grid.CellBorderStyle              = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.AutoSizeColumnsMode          = DataGridViewAutoSizeColumnsMode.Fill;
        }

        private static DataGridViewTextBoxColumn MakeCol(string name, string header,
                                                          bool fill, int width = 0)
            => new DataGridViewTextBoxColumn
            {
                Name           = name,
                HeaderText     = header,
                FillWeight     = fill ? 100 : 1,
                Width          = fill ? 0 : width,
                AutoSizeMode   = fill
                    ? DataGridViewAutoSizeColumnMode.Fill
                    : DataGridViewAutoSizeColumnMode.None,
            };

        private static DataGridViewTextBoxColumn MakeCol(string name, string header,
                                                          int width, bool fill)
            => MakeCol(name, header, fill, width);

        private static void StyleGroupBox(GroupBox grp, string title)
        {
            grp.Text      = title;
            grp.ForeColor = TextSecondary;
            grp.BackColor = BgCard;
            grp.Font      = new Font("Segoe UI", 9f, FontStyle.Bold, GraphicsUnit.Point);
            grp.Margin    = new Padding(4);
        }

        private static void BuildSettingRow(GroupBox parent, Label lbl, string labelText,
                                             NumericUpDown num, int yPos,
                                             decimal min, decimal max, decimal val, int decimals)
        {
            lbl.Text      = labelText;
            lbl.Font      = new Font("Segoe UI", 8.5f, FontStyle.Regular, GraphicsUnit.Point);
            lbl.ForeColor = TextSecondary;
            lbl.Location  = new Point(12, yPos + 22);
            lbl.Size      = new Size(200, 20);
            parent.Controls.Add(lbl);

            num.Location       = new Point(220, yPos + 20);
            num.Size           = new Size(120, 25);
            num.BackColor      = BgInput;
            num.ForeColor      = TextPrimary;
            num.BorderStyle    = BorderStyle.FixedSingle;
            num.Font           = new Font("Segoe UI", 9.5f, FontStyle.Bold, GraphicsUnit.Point);
            num.Minimum        = min;
            num.Maximum        = max;
            num.Value          = val;
            num.DecimalPlaces  = decimals;
            parent.Controls.Add(num);
        }

        private static void StyleCheckBox(CheckBox chk, string text, int yPos)
        {
            chk.Text      = text;
            chk.Font      = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
            chk.ForeColor = TextSecondary;
            chk.BackColor = Color.Transparent;
            chk.Location  = new Point(14, yPos);
            chk.AutoSize  = true;
            chk.Checked   = true;
        }

        private static void BuildComboRow(GroupBox parent, Label lbl, string labelText,
                                           ComboBox cmb, string[] items, int yPos)
        {
            lbl.Text      = labelText;
            lbl.Font      = new Font("Segoe UI", 8.5f, FontStyle.Regular, GraphicsUnit.Point);
            lbl.ForeColor = TextSecondary;
            lbl.Location  = new Point(12, yPos + 22);
            lbl.Size      = new Size(180, 20);
            parent.Controls.Add(lbl);

            cmb.DropDownStyle = ComboBoxStyle.DropDownList;
            cmb.BackColor     = BgInput;
            cmb.ForeColor     = TextPrimary;
            cmb.Font          = new Font("Segoe UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);
            cmb.Location      = new Point(200, yPos + 19);
            cmb.Size          = new Size(180, 26);
            cmb.FlatStyle     = FlatStyle.Flat;
            cmb.Items.AddRange(items);
            if (cmb.Items.Count > 0) cmb.SelectedIndex = 0;
            parent.Controls.Add(cmb);
        }

        // ── Control declarations ──────────────────────────────────────
        private Panel          pnlHeader;
        private Panel          pnlHeaderDivider;
        private Panel          pnlHeaderLeft;
        private Panel          pnlHeaderRight;
        private FlowLayoutPanel pnlHeaderButtons;
        private Label          lblBotName;
        private Label          lblAccountNumber;
        private Label          lblBrokerName;
        private Label          lblConnectionStatus;
        private Button         btnStartBot;
        private Button         btnStopBot;
        private Button         btnPauseBot;
        private Button         btnEmergencyStop;
        private TabControl     tabMain;
        private TabPage        tabPageDashboard;
        private TabPage        tabPageSignals;
        private TabPage        tabPageOpenTrades;
        private TabPage        tabPageRiskSettings;
        private TabPage        tabPageStrategySettings;
        private TabPage        tabPageLogs;
        private TabPage        tabPageBacktest;
        // Dashboard
        private Panel          pnlDashContent;
        private Panel          pnlDashTop;
        private TableLayoutPanel tlpDashCards;
        private Panel          pnlAccountSummaryWrap;
        private Panel          pnlAccountSummary;
        private Panel          pnlAccHdr;
        private Label          lblAccountSummaryTitle;
        private Label          lblBalanceTitle;
        private Label          lblBalance;
        private Label          lblEquityTitle;
        private Label          lblEquity;
        private Label          lblMarginTitle;
        private Label          lblMargin;
        private Label          lblFreeMarginTitle;
        private Label          lblFreeMargin;
        private Label          lblDailyPLTitle;
        private Label          lblDailyProfitLoss;
        private Panel          pnlSignalPanelWrap;
        private Panel          pnlSignalPanel;
        private Panel          pnlSigHdr;
        private Label          lblSignalPanelTitle;
        private Label          lblAIScoreTitle;
        private Label          lblAIScore;
        private Label          lblSignalDirTitle;
        private Label          lblSignalDirection;
        private Label          lblSignalConfTitle;
        private Label          lblSignalConfidence;
        private Label          lblSignalReasonTitle;
        private Label          lblSignalReason;
        private Panel          pnlRiskSummaryWrap;
        private Panel          pnlRiskSummary;
        private Panel          pnlRiskHdr;
        private Label          lblRiskSummaryTitle;
        private Label          lblRiskPercentTitle;
        private Label          lblRiskPercent;
        private Label          lblLotSizeTitle;
        private Label          lblLotSize;
        private Label          lblMaxDailyLossTitle;
        private Label          lblMaxDailyLossValue;
        private Label          lblTradesTodayTitle;
        private Label          lblTradesToday;
        private Panel          pnlSymbolWatchWrap;
        private Panel          pnlSymbolWatchHdr;
        private Label          lblSymbolWatchTitle;
        private DataGridView   gridSymbolWatch;
        // Signals tab
        private Panel          pnlSignalsContent;
        private Panel          pnlSignalsHdr;
        private Label          lblSignalsTitle;
        private DataGridView   gridSignals;
        // Open Trades tab
        private Panel          pnlOpenTradesContent;
        private Panel          pnlOpenTradesHdr;
        private Label          lblOpenTradesTitle;
        private Label          lblOpenTradesSummary;
        private DataGridView   gridOpenTrades;
        // Risk Settings tab
        private Panel          pnlRiskSettingsContent;
        private TableLayoutPanel tlpRiskSettings;
        private GroupBox       grpRiskParams;
        private Label          lblRiskPercentSetting;
        private NumericUpDown  numRiskPercent;
        private Label          lblMaxDailyLossSetting;
        private NumericUpDown  numMaxDailyLoss;
        private Label          lblMaxTradesSetting;
        private NumericUpDown  numMaxTrades;
        private Label          lblSpreadLimitSetting;
        private NumericUpDown  numSpreadLimit;
        private GroupBox       grpRiskFilters;
        private CheckBox       chkNewsFilter;
        private CheckBox       chkAutoTrading;
        private CheckBox       chkMaxDailyDrawdown;
        // Strategy Settings tab
        private Panel          pnlStrategyContent;
        private TableLayoutPanel tlpStrategy;
        private GroupBox       grpSymbolConfig;
        private Label          lblSymbolSetting;
        private ComboBox       cmbSymbol;
        private Label          lblTimeframeSetting;
        private ComboBox       cmbTimeFrame;
        private Label          lblSessionSetting;
        private ComboBox       cmbSession;
        private GroupBox       grpAiConfig;
        private Label          lblAiModelSetting;
        private ComboBox       cmbAiModel;
        private Label          lblMinScoreSetting;
        private NumericUpDown  numMinAiScore;
        // Logs tab
        private Panel          pnlLogsContent;
        private Panel          pnlLogsHdr;
        private Label          lblLogsTitle;
        private Button         btnClearLogs;
        private RichTextBox    txtBotLogs;
        // Backtest tab
        private Panel          pnlBacktestContent;
        private Panel          pnlBacktestControls;
        private GroupBox       grpBacktestParams;
        private Label          lblBtFromDate;
        private DateTimePicker dtpBacktestFrom;
        private Label          lblBtToDate;
        private DateTimePicker dtpBacktestTo;
        private Label          lblBtSymbol;
        private ComboBox       cmbBacktestSymbol;
        private Button         btnRunBacktest;
        private Panel          pnlBacktestResultsWrap;
        private Panel          pnlBacktestResultsHdr;
        private Label          lblBacktestResultsTitle;
        private DataGridView   gridBacktestResults;
    }
}
