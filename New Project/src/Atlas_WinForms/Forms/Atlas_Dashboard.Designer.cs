namespace Atlas_WinForms.Forms;

partial class Atlas_Dashboard
{
    private System.ComponentModel.IContainer components = null;

    // ── Controls ─────────────────────────────────────────────────────
    private Panel          pnl_header;
    private Label          lbl_title;
    private Label          lbl_connection;
    private Label          lbl_mode;
    private Label          lbl_bot_status;
    private Label          lbl_server_time;
    private Button         btn_settings;
    private Button         btn_mute_sounds;
    private Button         btn_start;
    private Button         btn_stop;
    private Button         btn_emergency_stop;
    private TabControl     tab_main;
    private TabPage        tab_dashboard;
    private TabPage        tab_performance;
    private TabPage        tab_risk;
    private TabPage        tab_log;

    // Dashboard tab
    private Label          lbl_session;
    private Panel          pnl_account;
    private Label          lbl_account_header;
    private Label          lbl_lbl_balance;
    private Label          lbl_balance;
    private Label          lbl_lbl_equity;
    private Label          lbl_equity;
    private Label          lbl_lbl_free_margin;
    private Label          lbl_free_margin;
    private Label          lbl_lbl_daily_pnl;
    private Label          lbl_daily_pnl;
    private Label          lbl_lbl_drawdown;
    private Label          lbl_drawdown;
    private Label          lbl_lbl_peak_equity;
    private Label          lbl_peak_equity;

    private Panel          pnl_regime;
    private Label          lbl_regime_header;
    private DataGridView   grid_regime;

    private Panel          pnl_positions;
    private Label          lbl_positions_header;
    private DataGridView   grid_positions;

    private Panel          pnl_signals;
    private Label          lbl_signals_header;
    private DataGridView   grid_signal_log;

    private Panel          pnl_news;
    private Label          lbl_news_header;
    private DataGridView   grid_news;

    // Performance tab
    private Panel          pnl_stats;
    private Label          lbl_lbl_total_trades;
    private Label          lbl_total_trades;
    private Label          lbl_lbl_win_rate;
    private Label          lbl_win_rate;
    private Label          lbl_lbl_avg_r;
    private Label          lbl_avg_r;
    private Label          lbl_lbl_profit_factor;
    private Label          lbl_profit_factor;
    private Label          lbl_lbl_max_dd;
    private Label          lbl_max_dd;
    private Label          lbl_lbl_total_r;
    private Label          lbl_total_r;
    private DataGridView   grid_strategy_perf;
    private Panel          pnl_live_equity;
    private Label          lbl_lbl_sharpe;
    private Label          lbl_sharpe;
    private Label          lbl_lbl_sortino;
    private Label          lbl_sortino;

    // Risk tab
    private Panel          pnl_risk_settings;
    private Label          lbl_risk_header;
    private Label          lbl_lbl_risk_forex;
    private Label          lbl_risk_forex;
    private Label          lbl_lbl_risk_gold;
    private Label          lbl_risk_gold;
    private Label          lbl_lbl_daily_limit;
    private Label          lbl_daily_limit;
    private Label          lbl_lbl_weekly_limit;
    private Label          lbl_weekly_limit;
    private Label          lbl_lbl_dd_breaker;
    private Label          lbl_dd_breaker;
    private Label          lbl_lbl_open_trades;
    private Label          lbl_open_trades;

    // Backtest tab
    private TabPage        tab_backtest;
    private Panel          pnl_bt_config;
    private Button         btn_bt_monte_carlo;
    private Button         btn_bt_save_report;
    private Label          lbl_bt_symbol;
    private ComboBox       cmb_bt_symbol;
    private Label          lbl_bt_from;
    private DateTimePicker dtp_bt_from;
    private Label          lbl_bt_to;
    private DateTimePicker dtp_bt_to;
    private Label          lbl_bt_balance;
    private TextBox        txt_bt_balance;
    private Button         btn_bt_run;
    private Button         btn_bt_load_csv;
    private Label          lbl_bt_status;
    private RichTextBox    txt_bt_report;
    private Panel          pnl_bt_equity;
    private Label          lbl_bt_equity_header;

    // Trade History tab
    private TabPage        tab_history;
    private DataGridView   grid_trade_history;
    private Button         btn_refresh_history;
    private Button         btn_export_history;
    private Button         btn_view_detail;
    private DateTimePicker dtp_history_date;
    private Button         btn_history_filter;
    private Label          lbl_history_status;

    // Symbol manager (Risk tab)
    private DataGridView   grid_symbols;

    // Walk-forward panel (Backtest tab)
    private DataGridView   grid_wf;

    // Parameter sweep panel (Backtest tab)
    private Button         btn_bt_sweep;
    private DataGridView   grid_bt_sweep;

    // Log tab
    private RichTextBox    txt_log;
    private Button         btn_save_log;
    private Button         btn_clear_log;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        SuspendLayout();

        Build_Header();
        Build_Tab_Control();
        Build_Dashboard_Tab();
        Build_Performance_Tab();
        Build_Risk_Tab();
        Build_Backtest_Tab();
        Build_History_Tab();
        Build_Log_Tab();

        Controls.Add(pnl_header);
        Controls.Add(tab_main);

        ResumeLayout(false);
    }

    // ── Header ───────────────────────────────────────────────────────
    private void Build_Header()
    {
        pnl_header = Card(0, 0, ClientSize.Width, 62, BG_HEADER);
        pnl_header.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        lbl_title = Lbl("ATLAS  FOREX + GOLD", 14, 12, 300, 36, TEXT_PRIMARY, FONT_MEDIUM);
        lbl_connection = Lbl("● DISCONNECTED", 330, 22, 160, 20, COL_RED, FONT_BOLD);
        lbl_mode   = Lbl("DEMO MODE", 500, 22, 140, 20, COL_YELLOW, FONT_BOLD);
        lbl_server_time = Lbl("Server: --:--:-- UTC", 650, 22, 200, 20, TEXT_MUTED, FONT_SMALL);
        lbl_bot_status  = Lbl("● STOPPED", ClientSize.Width - 400, 22, 180, 20, COL_RED, FONT_BOLD);

        btn_mute_sounds    = Btn("🔊",                 852,                  16,  34, 30, COL_BLUE,   btn_mute_sounds_Click);
        btn_settings       = Btn("⚙ SETTINGS",        892,                  16, 120, 30, COL_BLUE,   btn_settings_Click);
        btn_emergency_stop = Btn("⚠ EMERGENCY STOP", ClientSize.Width - 210, 12, 195, 38, COL_RED, btn_emergency_stop_Click);
        btn_start          = Btn("▶ START",           ClientSize.Width - 390, 12, 85, 38, COL_GREEN,  btn_start_Click);
        btn_stop           = Btn("■ STOP",            ClientSize.Width - 300, 12, 85, 38, COL_YELLOW, btn_stop_Click);
        btn_stop.Enabled   = false;

        pnl_header.Controls.AddRange([lbl_title, lbl_connection, lbl_mode, lbl_server_time,
            lbl_bot_status, btn_mute_sounds, btn_settings, btn_emergency_stop, btn_start, btn_stop]);
    }

    // ── Tab Control ──────────────────────────────────────────────────
    private void Build_Tab_Control()
    {
        tab_main = new TabControl
        {
            Location  = new Point(0, 64),
            Size      = new Size(ClientSize.Width, ClientSize.Height - 64),
            Anchor    = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = BG_DARK,
            ForeColor = TEXT_PRIMARY,
            Font      = FONT_BOLD,
            ItemSize  = new Size(140, 30),
            Padding   = new Point(12, 4)
        };

        tab_dashboard   = Tab("DASHBOARD");
        tab_performance = Tab("PERFORMANCE");
        tab_risk        = Tab("RISK CONTROLS");
        tab_backtest    = Tab("BACKTEST");
        tab_history     = Tab("TRADE HISTORY");
        tab_log         = Tab("SYSTEM LOG");

        tab_main.TabPages.AddRange([tab_dashboard, tab_performance, tab_risk, tab_backtest, tab_history, tab_log]);
        Controls.Add(tab_main);
    }

    // ── Dashboard Tab ────────────────────────────────────────────────
    private void Build_Dashboard_Tab()
    {
        int w = ClientSize.Width - 24;

        // Account panel (top-left)
        pnl_account = Card(8, 8, 320, 228, BG_CARD);
        lbl_account_header = CardHeader("ACCOUNT", 8, 8, 290, pnl_account);

        int ay = 42;
        lbl_lbl_balance    = StatLabel("Balance:",        10, ay,     pnl_account);
        lbl_balance        = StatValue("$0.00",           170, ay,    pnl_account);  ay += 28;
        lbl_lbl_equity     = StatLabel("Equity:",         10, ay,     pnl_account);
        lbl_equity         = StatValue("$0.00",           170, ay,    pnl_account);  ay += 28;
        lbl_lbl_free_margin= StatLabel("Free Margin:",    10, ay,     pnl_account);
        lbl_free_margin    = StatValue("$0.00",           170, ay,    pnl_account);  ay += 28;
        lbl_lbl_daily_pnl  = StatLabel("Daily P&L:",      10, ay,     pnl_account);
        lbl_daily_pnl      = StatValue("$0.00",           170, ay,    pnl_account);  ay += 28;
        lbl_lbl_drawdown    = StatLabel("Drawdown:",       10, ay,    pnl_account);
        lbl_drawdown        = StatValue("0.00%",          170, ay,   pnl_account);  ay += 28;
        lbl_lbl_peak_equity = StatLabel("Peak Equity:",   10, ay,    pnl_account);
        lbl_peak_equity     = StatValue("$0.00",          170, ay,   pnl_account);

        lbl_session = Lbl("Session:  —", 10, 206, 295, 18, TEXT_MUTED, FONT_SMALL);
        pnl_account.Controls.Add(lbl_session);

        // Regime panel (top-right of account)
        pnl_regime = Card(336, 8, w - 336, 200, BG_CARD);
        lbl_regime_header = CardHeader("REGIME + MTF CONFLUENCE", 8, 8, pnl_regime.Width - 16, pnl_regime);
        grid_regime = Build_Grid(pnl_regime, 8, 36, pnl_regime.Width - 16, 152,
            ["Symbol", "Regime", "Score", "Session", "Spread", "D1", "H4", "H1", "Aligned"]);
        SetColumnWidths(grid_regime, [80, 120, 60, 80, 60, 60, 60, 60, 60]);

        // Positions panel
        pnl_positions = Card(8, 244, w, 180, BG_CARD);
        lbl_positions_header = CardHeader("OPEN POSITIONS", 8, 8, w - 16, pnl_positions);
        grid_positions = Build_Grid(pnl_positions, 8, 36, w - 16, 130,
            ["Symbol", "Direction", "Entry", "Stop Loss", "Take Profit", "P&L", "R-Multiple", "Duration"]);
        SetColumnWidths(grid_positions, [80, 70, 80, 80, 80, 80, 80, 76]);

        // Add close button column at col 8
        var col_pos_close = new DataGridViewButtonColumn
        {
            HeaderText = "Close", Name = "col_pos_close", Width = 60,
            FlatStyle  = FlatStyle.Flat, SortMode = DataGridViewColumnSortMode.NotSortable,
            UseColumnTextForButtonValue = false
        };
        col_pos_close.DefaultCellStyle.BackColor = Color.FromArgb(30, 30, 55);
        col_pos_close.DefaultCellStyle.ForeColor = COL_RED;
        col_pos_close.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        col_pos_close.DefaultCellStyle.Font = new Font("Segoe UI", 8, FontStyle.Bold);
        grid_positions.Columns.Add(col_pos_close);
        grid_positions.ReadOnly = false;
        for (int i = 0; i < 8; i++) grid_positions.Columns[i].ReadOnly = true;
        grid_positions.CellContentClick += Grid_Positions_CellContentClick;

        // Signal log
        pnl_signals = Card(8, 432, w, 190, BG_CARD);
        lbl_signals_header = CardHeader("SIGNAL LOG", 8, 8, w - 16, pnl_signals);
        grid_signal_log = Build_Grid(pnl_signals, 8, 36, w - 16, 140,
            ["Time", "Signal", "Status", "Detail"]);
        SetColumnWidths(grid_signal_log, [70, 250, 90, 0]);
        grid_signal_log.Columns[3].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

        // News calendar panel
        pnl_news = Card(8, 630, w, 175, BG_CARD);
        lbl_news_header = CardHeader("UPCOMING HIGH-IMPACT NEWS  (next 24h)", 8, 8, w - 16, pnl_news);
        grid_news = Build_Grid(pnl_news, 8, 36, w - 16, 126,
            ["UTC Time", "Ccy", "Event", "In", "Status"]);
        SetColumnWidths(grid_news, [80, 50, 0, 80, 110]);
        grid_news.Columns[2].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

        tab_dashboard.Controls.AddRange([pnl_account, pnl_regime, pnl_positions, pnl_signals, pnl_news]);
    }

    // ── Performance Tab ──────────────────────────────────────────────
    private void Build_Performance_Tab()
    {
        int w = ClientSize.Width - 24;
        pnl_stats = Card(8, 8, w, 120, BG_CARD);
        CardHeader("OVERALL PERFORMANCE", 8, 8, w - 16, pnl_stats);

        int sx = 20;
        lbl_lbl_total_trades   = StatLabel("Total Trades:",   sx, 40, pnl_stats); lbl_total_trades   = BigStat("0",    sx + 10, 62, pnl_stats); sx += 140;
        lbl_lbl_win_rate       = StatLabel("Win Rate:",       sx, 40, pnl_stats); lbl_win_rate       = BigStat("—",    sx + 10, 62, pnl_stats); sx += 140;
        lbl_lbl_avg_r          = StatLabel("Avg R/Trade:",    sx, 40, pnl_stats); lbl_avg_r          = BigStat("—",    sx + 10, 62, pnl_stats); sx += 140;
        lbl_lbl_profit_factor  = StatLabel("Profit Factor:",  sx, 40, pnl_stats); lbl_profit_factor  = BigStat("—",    sx + 10, 62, pnl_stats); sx += 140;
        lbl_lbl_max_dd         = StatLabel("Max Drawdown:",   sx, 40, pnl_stats); lbl_max_dd         = BigStat("0.00%",sx + 10, 62, pnl_stats); sx += 140;
        lbl_lbl_total_r        = StatLabel("Total R:",        sx, 40, pnl_stats); lbl_total_r        = BigStat("0.0R", sx + 10, 62, pnl_stats); sx += 140;
        lbl_lbl_sharpe         = StatLabel("Sharpe R:",       sx, 40, pnl_stats); lbl_sharpe         = BigStat("—",    sx + 10, 62, pnl_stats); sx += 140;
        lbl_lbl_sortino        = StatLabel("Sortino:",        sx, 40, pnl_stats); lbl_sortino        = BigStat("—",    sx + 10, 62, pnl_stats);

        var pnl_strat = Card(8, 136, w, 400, BG_CARD);
        CardHeader("STRATEGY PERFORMANCE", 8, 8, w - 16, pnl_strat);
        grid_strategy_perf = Build_Grid(pnl_strat, 8, 36, w - 16, 352,
            ["Strategy", "Trades", "Win Rate", "Profit Factor", "Avg R", "Status"]);
        SetColumnWidths(grid_strategy_perf, [260, 80, 80, 100, 80, 90]);

        // Replace the 6th text column with a toggle button column
        var col_toggle = new DataGridViewButtonColumn
        {
            HeaderText  = "Toggle",
            Name        = "col_toggle",
            Width       = 120,
            FlatStyle   = FlatStyle.Flat,
            SortMode    = DataGridViewColumnSortMode.NotSortable,
            UseColumnTextForButtonValue = false
        };
        col_toggle.DefaultCellStyle.BackColor  = Color.FromArgb(30, 30, 55);
        col_toggle.DefaultCellStyle.ForeColor  = COL_YELLOW;
        col_toggle.DefaultCellStyle.Alignment  = DataGridViewContentAlignment.MiddleCenter;
        col_toggle.DefaultCellStyle.Font       = new Font("Segoe UI", 8, FontStyle.Bold);
        grid_strategy_perf.Columns.Add(col_toggle);
        grid_strategy_perf.Columns[6].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

        // Make only the button column interactive; text columns stay read-only
        grid_strategy_perf.ReadOnly = false;
        for (int i = 0; i < 6; i++)
            grid_strategy_perf.Columns[i].ReadOnly = true;
        grid_strategy_perf.CellContentClick += Grid_Strategy_Perf_CellContentClick;

        pnl_live_equity = Card(8, 544, w, 150, BG_CARD);
        CardHeader("LIVE EQUITY CURVE", 8, 8, w - 16, pnl_live_equity);
        pnl_live_equity.Paint += Pnl_Live_Equity_Paint;

        tab_performance.Controls.AddRange([pnl_stats, pnl_strat, pnl_live_equity]);
    }

    // ── Risk Tab ─────────────────────────────────────────────────────
    private void Build_Risk_Tab()
    {
        int w = ClientSize.Width - 24;
        pnl_risk_settings = Card(8, 8, 420, 280, BG_CARD);
        lbl_risk_header = CardHeader("RISK SETTINGS", 8, 8, 390, pnl_risk_settings);

        int ry = 42;
        lbl_lbl_risk_forex   = StatLabel("Forex Risk/Trade:",  10, ry, pnl_risk_settings); lbl_risk_forex   = StatValue("0.25%", 220, ry, pnl_risk_settings); ry += 30;
        lbl_lbl_risk_gold    = StatLabel("Gold Risk/Trade:",   10, ry, pnl_risk_settings); lbl_risk_gold    = StatValue("0.20%", 220, ry, pnl_risk_settings); ry += 30;
        lbl_lbl_daily_limit  = StatLabel("Max Daily Loss:",    10, ry, pnl_risk_settings); lbl_daily_limit  = StatValue("1.00%", 220, ry, pnl_risk_settings); ry += 30;
        lbl_lbl_weekly_limit = StatLabel("Max Weekly Loss:",   10, ry, pnl_risk_settings); lbl_weekly_limit = StatValue("2.00%", 220, ry, pnl_risk_settings); ry += 30;
        lbl_lbl_dd_breaker   = StatLabel("Drawdown Breaker:",  10, ry, pnl_risk_settings); lbl_dd_breaker   = StatValue("5.00%", 220, ry, pnl_risk_settings); ry += 30;
        lbl_lbl_open_trades  = StatLabel("Max Open Trades:",   10, ry, pnl_risk_settings); lbl_open_trades  = StatValue("2",     220, ry, pnl_risk_settings);

        // Symbol universe panel
        var pnl_symbols = Card(436, 8, 490, 290, BG_CARD);
        CardHeader("SYMBOL UNIVERSE  —  click to pause/enable per pair", 8, 8, 464, pnl_symbols);
        grid_symbols = Build_Grid(pnl_symbols, 8, 36, 474, 242,
            ["Symbol", "Tier", "Spread", "Risk%", "Status"]);
        SetColumnWidths(grid_symbols, [80, 70, 70, 55, 80]);

        var col_sym_toggle = new DataGridViewButtonColumn
        {
            HeaderText  = "Toggle",
            Name        = "col_sym_toggle",
            FlatStyle   = FlatStyle.Flat,
            SortMode    = DataGridViewColumnSortMode.NotSortable,
            UseColumnTextForButtonValue = false
        };
        col_sym_toggle.DefaultCellStyle.BackColor  = Color.FromArgb(30, 30, 55);
        col_sym_toggle.DefaultCellStyle.ForeColor  = COL_YELLOW;
        col_sym_toggle.DefaultCellStyle.Alignment  = DataGridViewContentAlignment.MiddleCenter;
        col_sym_toggle.DefaultCellStyle.Font       = new Font("Segoe UI", 8, FontStyle.Bold);
        grid_symbols.Columns.Add(col_sym_toggle);
        grid_symbols.Columns[5].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        grid_symbols.ReadOnly = false;
        for (int i = 0; i < 5; i++) grid_symbols.Columns[i].ReadOnly = true;
        grid_symbols.CellContentClick += Grid_Symbols_CellContentClick;

        tab_risk.Controls.AddRange([pnl_risk_settings, pnl_symbols]);
    }

    // ── Backtest Tab ─────────────────────────────────────────────────
    private void Build_Backtest_Tab()
    {
        int w = ClientSize.Width - 24;

        // Config panel (left sidebar)
        pnl_bt_config = Card(8, 8, 280, 520, BG_CARD);
        CardHeader("BACKTEST CONFIG", 8, 8, 260, pnl_bt_config);

        int cy = 40;
        lbl_bt_symbol = StatLabel("Symbol:", 10, cy, pnl_bt_config); cy += 22;
        cmb_bt_symbol = new ComboBox { Location = new Point(10, cy), Size = new Size(250, 24),
            BackColor = Color.FromArgb(20,20,36), ForeColor = TEXT_PRIMARY, FlatStyle = FlatStyle.Flat,
            DropDownStyle = ComboBoxStyle.DropDownList };
        cmb_bt_symbol.Items.AddRange(["EURUSD","GBPUSD","USDJPY","XAUUSD","AUDUSD","USDCAD","USDCHF","NZDUSD"]);
        cmb_bt_symbol.SelectedIndex = 0;
        pnl_bt_config.Controls.Add(cmb_bt_symbol); cy += 34;

        lbl_bt_from = StatLabel("From date:", 10, cy, pnl_bt_config); cy += 22;
        dtp_bt_from = new DateTimePicker { Location = new Point(10, cy), Size = new Size(250, 24),
            Value = DateTime.Today.AddMonths(-6), Format = DateTimePickerFormat.Short,
            CalendarForeColor = TEXT_PRIMARY, CalendarMonthBackground = BG_CARD };
        pnl_bt_config.Controls.Add(dtp_bt_from); cy += 34;

        lbl_bt_to = StatLabel("To date:", 10, cy, pnl_bt_config); cy += 22;
        dtp_bt_to = new DateTimePicker { Location = new Point(10, cy), Size = new Size(250, 24),
            Value = DateTime.Today, Format = DateTimePickerFormat.Short };
        pnl_bt_config.Controls.Add(dtp_bt_to); cy += 34;

        lbl_bt_balance = StatLabel("Initial balance ($):", 10, cy, pnl_bt_config); cy += 22;
        txt_bt_balance = new TextBox { Location = new Point(10, cy), Size = new Size(250, 24),
            Text = "10000", BackColor = Color.FromArgb(20,20,36), ForeColor = TEXT_PRIMARY,
            BorderStyle = BorderStyle.FixedSingle };
        pnl_bt_config.Controls.Add(txt_bt_balance); cy += 42;

        btn_bt_load_csv    = Btn("📂 Load CSV File",        10, cy, 250, 34, COL_BLUE,  btn_bt_load_csv_Click);    cy += 44;
        btn_bt_run         = Btn("▶ RUN BACKTEST",          10, cy, 250, 40, COL_GREEN, btn_bt_run_Click);          cy += 50;
        btn_bt_monte_carlo = Btn("🎲 Monte Carlo (1K runs)", 10, cy, 250, 34, COL_BLUE,  btn_bt_monte_carlo_Click);
        btn_bt_monte_carlo.Enabled = false; cy += 44;

        btn_bt_save_report = Btn("💾 Save Report...",        10, cy, 250, 30, TEXT_SECONDARY, btn_bt_save_report_Click);
        btn_bt_save_report.Enabled = false; cy += 40;

        btn_bt_sweep = Btn("🔍 Param Sweep",                10, cy, 250, 30, COL_ORANGE, btn_bt_sweep_Click);
        btn_bt_sweep.Enabled = false; cy += 40;

        pnl_bt_config.Controls.AddRange([btn_bt_load_csv, btn_bt_run, btn_bt_monte_carlo, btn_bt_save_report, btn_bt_sweep]);

        lbl_bt_status = new Label { Location = new Point(10, cy), Size = new Size(260, 36),
            ForeColor = TEXT_MUTED, BackColor = Color.Transparent, Font = FONT_SMALL, Text = "Ready" };
        pnl_bt_config.Controls.Add(lbl_bt_status);

        // Equity curve panel (top-right)
        pnl_bt_equity = Card(296, 8, w - 296, 200, BG_CARD);
        lbl_bt_equity_header = CardHeader("EQUITY CURVE", 8, 8, w - 316, pnl_bt_equity);
        pnl_bt_equity.Paint += Pnl_Bt_Equity_Paint;

        // Report panel (bottom-right)
        var pnl_bt_report_container = Card(296, 216, w - 296, 232, BG_CARD);
        CardHeader("BACKTEST REPORT", 8, 8, w - 316, pnl_bt_report_container);

        txt_bt_report = new RichTextBox
        {
            Location    = new Point(8, 32),
            Size        = new Size(pnl_bt_report_container.Width - 16, pnl_bt_report_container.Height - 40),
            BackColor   = BG_DARK,
            ForeColor   = COL_GREEN,
            Font        = new Font("Consolas", 8),
            ReadOnly    = true,
            ScrollBars  = RichTextBoxScrollBars.Vertical,
            BorderStyle = BorderStyle.None
        };
        pnl_bt_report_container.Controls.Add(txt_bt_report);

        // Walk-forward panel (below report)
        var pnl_wf = Card(296, 456, w - 296, 160, BG_CARD);
        CardHeader("WALK-FORWARD VALIDATION  (in-sample vs. out-of-sample)", 8, 8, w - 316, pnl_wf);
        grid_wf = Build_Grid(pnl_wf, 8, 36, w - 316, 112,
            ["Period", "Trades", "Win Rate", "Profit Factor", "Avg R", "Max DD"]);
        SetColumnWidths(grid_wf, [0, 70, 80, 110, 80, 80]);
        grid_wf.Columns[0].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

        // Parameter sweep panel (below walk-forward)
        var pnl_bt_sweep = Card(296, 624, w - 296, 180, BG_CARD);
        CardHeader("PARAMETER SWEEP  —  Quality Score vs Trades/PF  (double-click best to apply)", 8, 8, w - 316, pnl_bt_sweep);
        grid_bt_sweep = Build_Grid(pnl_bt_sweep, 8, 36, w - 316, 132,
            ["Min Quality", "Trades", "Win Rate", "Profit Factor", "Avg R", "Net R"]);
        SetColumnWidths(grid_bt_sweep, [0, 80, 90, 110, 80, 80]);
        grid_bt_sweep.Columns[0].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        grid_bt_sweep.CellDoubleClick += Grid_Bt_Sweep_CellDoubleClick;

        tab_backtest.Controls.AddRange([pnl_bt_config, pnl_bt_equity, pnl_bt_report_container, pnl_wf, pnl_bt_sweep]);
    }

    // ── Trade History Tab ─────────────────────────────────────────────
    private void Build_History_Tab()
    {
        int w = ClientSize.Width - 24;

        // Top bar: refresh button + status label
        var pnl_top = new Panel
        {
            Location  = new Point(8, 8),
            Size      = new Size(w, 38),
            BackColor = BG_CARD_HEADER
        };
        btn_refresh_history = Btn("⟳ Refresh from DB",  8,   4, 180, 30, COL_BLUE,        btn_refresh_history_Click);
        btn_export_history  = Btn("📥 Export CSV...",   196,  4, 150, 30, TEXT_SECONDARY,  btn_export_history_Click);
        btn_view_detail     = Btn("🔍 Trade Detail",    354,  4, 140, 30, COL_ORANGE,      btn_view_detail_Click);
        btn_view_detail.Enabled = false;
        dtp_history_date = new DateTimePicker
        {
            Location    = new Point(502, 6),
            Size        = new Size(130, 26),
            Format      = DateTimePickerFormat.Short,
            Value       = DateTime.Today,
            MaxDate     = DateTime.Today,
            BackColor   = Color.FromArgb(20, 20, 36),
            ForeColor   = Color.FromArgb(221, 224, 240),
            CalendarForeColor  = Color.FromArgb(221, 224, 240),
            CalendarMonthBackground = Color.FromArgb(25, 25, 42)
        };
        btn_history_filter = Btn("Filter (UTC)", 640, 4, 108, 30, COL_BLUE, btn_history_filter_Click);
        lbl_history_status  = new Label
        {
            Location  = new Point(748, 10), Size = new Size(w - 758, 20),
            ForeColor = TEXT_MUTED, BackColor = Color.Transparent, Font = FONT_SMALL,
            Text      = "Last 50 closed trades from SQLite · dates are UTC  (click Refresh to load)"
        };
        pnl_top.Controls.AddRange([btn_refresh_history, btn_export_history, btn_view_detail,
            dtp_history_date, btn_history_filter, lbl_history_status]);

        // Grid panel
        var pnl_grid = new Panel
        {
            Location  = new Point(8, 54),
            Size      = new Size(w, ClientSize.Height - 64 - 28 - 62),
            BackColor = BG_DARK,
            Anchor    = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };

        grid_trade_history = Build_Grid(pnl_grid, 0, 0, pnl_grid.Width, pnl_grid.Height,
            ["Closed (UTC)", "Symbol", "Strategy", "Dir", "Entry", "Exit", "R-Mult", "Net P&L", "Reason", "Notes"]);
        SetColumnWidths(grid_trade_history, [130, 70, 210, 50, 90, 90, 76, 110, 120, 0]);
        grid_trade_history.Columns[9].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        grid_trade_history.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        grid_trade_history.CellDoubleClick += Grid_Trade_History_CellDoubleClick;

        tab_history.Controls.AddRange([pnl_top, pnl_grid]);
    }

    // ── Log Tab ──────────────────────────────────────────────────────
    private void Build_Log_Tab()
    {
        var pnl_log_bar = new Panel { Dock = DockStyle.Bottom, Height = 36, BackColor = BG_CARD_HEADER };
        btn_save_log  = Btn("💾 Save Log...", 8,   4, 130, 28, TEXT_SECONDARY, btn_save_log_Click);
        btn_clear_log = Btn("🗑 Clear",       146,  4,  70, 28, COL_RED,        btn_clear_log_Click);
        pnl_log_bar.Controls.AddRange([btn_save_log, btn_clear_log]);

        txt_log = new RichTextBox
        {
            Dock        = DockStyle.Fill,
            BackColor   = BG_DARK,
            ForeColor   = COL_GREEN,
            Font        = FONT_MONO,
            ReadOnly    = true,
            ScrollBars  = RichTextBoxScrollBars.Vertical,
            BorderStyle = BorderStyle.None
        };

        tab_log.Controls.Add(txt_log);
        tab_log.Controls.Add(pnl_log_bar);
    }

    // ── Helpers ──────────────────────────────────────────────────────
    private static Panel Card(int x, int y, int w, int h, Color bg)
    {
        var p = new Panel { Location = new Point(x, y), Size = new Size(w, h), BackColor = bg };
        p.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(46, 48, 85), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1);
        };
        return p;
    }

    private Label CardHeader(string text, int x, int y, int w, Panel parent)
    {
        var lbl = new Label
        {
            Text      = text,
            Location  = new Point(x, y),
            Size      = new Size(w, 22),
            Font      = FONT_BOLD,
            ForeColor = TEXT_SECONDARY,
            BackColor = Color.Transparent
        };
        parent.Controls.Add(lbl);
        return lbl;
    }

    private static Label Lbl(string text, int x, int y, int w, int h, Color fg, Font font)
    {
        return new Label { Text = text, Location = new Point(x, y), Size = new Size(w, h),
            ForeColor = fg, BackColor = Color.Transparent, Font = font, TextAlign = ContentAlignment.MiddleLeft };
    }

    private static Button Btn(string text, int x, int y, int w, int h, Color fg, EventHandler click)
    {
        var btn = new Button
        {
            Text      = text,
            Location  = new Point(x, y),
            Size      = new Size(w, h),
            ForeColor = fg,
            BackColor = Color.FromArgb(30, 30, 55),
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 8, FontStyle.Bold),
            Cursor    = Cursors.Hand
        };
        btn.FlatAppearance.BorderColor = fg;
        btn.Click += click;
        return btn;
    }

    private Label StatLabel(string text, int x, int y, Panel parent)
    {
        var lbl = new Label { Text = text, Location = new Point(x, y), Size = new Size(150, 20),
            ForeColor = TEXT_SECONDARY, BackColor = Color.Transparent, Font = FONT_NORMAL };
        parent.Controls.Add(lbl);
        return lbl;
    }

    private Label StatValue(string text, int x, int y, Panel parent)
    {
        var lbl = new Label { Text = text, Location = new Point(x, y), Size = new Size(160, 20),
            ForeColor = TEXT_PRIMARY, BackColor = Color.Transparent, Font = FONT_BOLD };
        parent.Controls.Add(lbl);
        return lbl;
    }

    private Label BigStat(string text, int x, int y, Panel parent)
    {
        var lbl = new Label { Text = text, Location = new Point(x, y), Size = new Size(120, 36),
            ForeColor = TEXT_PRIMARY, BackColor = Color.Transparent, Font = FONT_MEDIUM };
        parent.Controls.Add(lbl);
        return lbl;
    }

    private TabPage Tab(string text)
    {
        return new TabPage { Text = text, BackColor = BG_DARK, ForeColor = TEXT_PRIMARY };
    }

    private DataGridView Build_Grid(Control parent, int x, int y, int w, int h, string[] cols)
    {
        var grid = new DataGridView
        {
            Location              = new Point(x, y),
            Size                  = new Size(w, h),
            BackgroundColor       = BG_DARK,
            ForeColor             = TEXT_PRIMARY,
            GridColor             = BORDER,
            BorderStyle           = BorderStyle.None,
            Font                  = FONT_SMALL,
            ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = BG_CARD_HEADER, ForeColor = TEXT_SECONDARY,
                Font = FONT_BOLD, Alignment = DataGridViewContentAlignment.MiddleLeft
            },
            DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = BG_DARK, ForeColor = TEXT_PRIMARY,
                SelectionBackColor = Color.FromArgb(40, 42, 70),
                SelectionForeColor = TEXT_PRIMARY
            },
            AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(20, 20, 38), ForeColor = TEXT_PRIMARY,
                SelectionBackColor = Color.FromArgb(40, 42, 70),
                SelectionForeColor = TEXT_PRIMARY
            },
            RowHeadersVisible         = false,
            AllowUserToAddRows        = false,
            AllowUserToDeleteRows     = false,
            ReadOnly                  = true,
            SelectionMode             = DataGridViewSelectionMode.FullRowSelect,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersHeight       = 28,
            RowTemplate               = { Height = 24 }
        };

        foreach (var col in cols)
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = col, SortMode = DataGridViewColumnSortMode.NotSortable });

        parent.Controls.Add(grid);
        return grid;
    }

    private static void SetColumnWidths(DataGridView grid, int[] widths)
    {
        for (int i = 0; i < widths.Length && i < grid.Columns.Count; i++)
            grid.Columns[i].Width = widths[i];
    }

    // Field stubs referenced by CardHeader overload with 4 args
    private Label CardHeader(string t, int x, int y, int w, int h, Panel p) => CardHeader(t, x, y, w, p);
}
