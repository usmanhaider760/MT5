using Atlas_Application.Backtest;
using Atlas_Application.Services;
using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;
using Atlas_Execution.MT5;

namespace Atlas_WinForms.Forms;

public partial class Atlas_Dashboard : Form
{
    private Bot_Controller? _controller;
    private MT5_Account_Service? _account_service;
    private Risk_Setting_BO? _risk_settings;
    private System.Windows.Forms.Timer? _ui_refresh_timer;
    private Connection_Status_Panel? _connection_status;
    private int _cycle_interval_seconds = 60;

    private bool _sounds_muted = false;

    // Backtest state
    private List<Candle_BO>? _bt_m15;
    private List<Candle_BO>? _bt_h1;
    private List<Candle_BO>? _bt_h4;
    private List<Candle_BO>? _bt_d1;
    private Backtest_Result? _bt_last_result;
    private CancellationTokenSource? _bt_cts;
    // ── Palette ──────────────────────────────────────────────────────
    static readonly Color BG_DARK        = Color.FromArgb(14,  14,  21);
    static readonly Color BG_CARD        = Color.FromArgb(25,  25,  42);
    static readonly Color BG_CARD_HEADER = Color.FromArgb(18,  18,  32);
    static readonly Color BG_HEADER      = Color.FromArgb(10,  10,  18);
    static readonly Color BG_BUTTON      = Color.FromArgb(30,  30,  55);
    static readonly Color BORDER         = Color.FromArgb(46,  48,  85);
    static readonly Color TEXT_PRIMARY   = Color.FromArgb(221, 224, 240);
    static readonly Color TEXT_SECONDARY = Color.FromArgb(128, 136, 192);
    static readonly Color TEXT_MUTED     = Color.FromArgb(80,  88,  140);
    static readonly Color COL_GREEN      = Color.FromArgb(61,  221, 128);
    static readonly Color COL_RED        = Color.FromArgb(240, 85,  85);
    static readonly Color COL_YELLOW     = Color.FromArgb(240, 176, 48);
    static readonly Color COL_BLUE       = Color.FromArgb(85,  150, 240);
    static readonly Color COL_ORANGE     = Color.FromArgb(240, 130, 48);

    static readonly Font FONT_LARGE  = new("Segoe UI", 20, FontStyle.Bold);
    static readonly Font FONT_MEDIUM = new("Segoe UI", 13, FontStyle.Bold);
    static readonly Font FONT_NORMAL = new("Segoe UI",  9);
    static readonly Font FONT_SMALL  = new("Segoe UI",  8);
    static readonly Font FONT_BOLD   = new("Segoe UI",  9, FontStyle.Bold);
    static readonly Font FONT_MONO   = new("Consolas",  9);

    public Atlas_Dashboard()
    {
        InitializeComponent();
        Apply_Dark_Theme();
        Wire_Tab_Drawing();
        Populate_Demo_Data();
        Initialize_Controller();
        Start_UI_Timer();
    }

    private void Initialize_Controller()
    {
        string host = Atlas_Config.MT5_Host;
        int    port = Atlas_Config.MT5_Port;
        _cycle_interval_seconds = Atlas_Config.Cycle_Interval_Seconds;

        var (controller, _, account_service) = Atlas_Bot_Bootstrap.Create(
            mt5_host: host, mt5_port: port,
            demo_mode: Atlas_Config.Demo_Mode,
            db_path: Atlas_Config.Db_Path);
        _controller      = controller;
        _account_service = account_service;
        _risk_settings   = controller.Risk_Settings;

        // Wire connection monitor (pings MT5 bridge every 10 seconds)
        var bridge = new MT5_Bridge_Client(host, port);
        _connection_status = new Connection_Status_Panel(bridge, lbl_connection);
        _ = _connection_status.Ping_Async(); // first ping immediately

        _controller.On_Log += msg => Safe_Invoke(() =>
        {
            txt_log.AppendText(msg + "\r\n");
            txt_log.ScrollToCaret();
        });

        _controller.On_Signal_Approved += sig => Safe_Invoke(() =>
        {
            Add_Signal_Log($"{sig.Symbol_Name} [{sig.Strategy}]", "APPROVED",
                $"Score:{sig.Quality_Score} RR:{sig.Reward_Risk_Ratio:F1}", COL_GREEN);
            if (!_sounds_muted) System.Media.SystemSounds.Asterisk.Play();
        });

        _controller.On_Signal_Rejected += sig => Safe_Invoke(() =>
            Add_Signal_Log($"{sig.Symbol_Name} [{sig.Strategy}]", "REJECTED",
                sig.Reject_Detail, COL_RED));

        _controller.On_Emergency_Stop += reason => Safe_Invoke(() =>
        {
            lbl_bot_status.Text      = "⚠ EMERGENCY STOP";
            lbl_bot_status.ForeColor = COL_RED;
            lbl_mode.Text            = "EMERGENCY STOP";
            lbl_mode.ForeColor       = COL_RED;
            txt_log.AppendText($"[EMERGENCY STOP] {reason}\r\n");
            if (!_sounds_muted) System.Media.SystemSounds.Hand.Play();
        });

        _controller.On_Running_Changed += running => Safe_Invoke(() =>
        {
            lbl_bot_status.Text      = running ? "● RUNNING  (0 cycles)" : "● STOPPED";
            lbl_bot_status.ForeColor = running ? COL_GREEN : COL_RED;
        });

        _controller.On_Cycle_Completed += count => Safe_Invoke(() =>
            lbl_bot_status.Text = $"● RUNNING  ({count} cycles)");

        _controller.On_Regime_Updated   += (sym, ctx) => Safe_Invoke(() => Update_Regime_Row(sym, ctx));
        _controller.On_Positions_Updated += pos       => Safe_Invoke(() => Update_Positions_Grid(pos));

        Populate_Symbol_Grid();
    }

    private void Start_UI_Timer()
    {
        _ui_refresh_timer = new System.Windows.Forms.Timer { Interval = 5000 };
        _ui_refresh_timer.Tick += (_, _) => Update_Server_Time();
        _ui_refresh_timer.Start();
    }

    private void Update_Server_Time()
    {
        lbl_server_time.Text = $"Server: {DateTime.UtcNow:HH:mm:ss} UTC";
        Update_Session_Label();

        if (_controller != null)
        {
            var (wr, avg_r, pf, max_dd) = _controller.Get_Overall_Stats();
            lbl_win_rate.Text      = wr > 0      ? $"{wr:F1}%"  : "—";
            lbl_avg_r.Text         = avg_r != 0  ? $"{avg_r:+0.000;-0.000}" : "—";
            lbl_profit_factor.Text = pf > 0      ? $"{pf:F2}"   : "—";
            lbl_max_dd.Text        = $"{max_dd:F2}%";
        }

        _ = Refresh_Account_Async();
        Refresh_Performance_Tab();
        _ = Refresh_News_Async();
    }

    private async Task Refresh_Account_Async()
    {
        if (_account_service == null || _connection_status?.Is_Connected != true) return;
        try
        {
            var state = await _account_service.Get_Account_State_Async(open_trade_count: 0);
            Safe_Invoke(() => Update_Account_Labels(state));
        }
        catch { /* Bridge not connected — labels keep last value */ }
    }

    private void Update_Account_Labels(Account_State_BO state)
    {
        if (state.Balance == 0) return;

        lbl_balance.Text     = $"${state.Balance:N2}";
        lbl_equity.Text      = $"${state.Equity:N2}";
        lbl_free_margin.Text = $"${state.Free_Margin:N2}";

        decimal daily_pnl = state.Daily_PnL;
        decimal daily_pct = state.Day_Open_Balance > 0
            ? daily_pnl / state.Day_Open_Balance * 100m : 0m;
        lbl_daily_pnl.Text      = $"{daily_pnl:+$#,##0.00;-$#,##0.00}  ({daily_pct:+0.00;-0.00}%)";
        lbl_daily_pnl.ForeColor = daily_pnl >= 0 ? COL_GREEN : COL_RED;

        lbl_drawdown.Text      = $"{state.Drawdown_From_Peak:F2}%";
        lbl_drawdown.ForeColor = state.Drawdown_From_Peak >= 2m ? COL_RED
                               : state.Drawdown_From_Peak >= 1m ? COL_YELLOW
                               : COL_GREEN;

        // Update risk tab live usage
        if (_risk_settings != null && state.Day_Open_Balance > 0)
        {
            decimal daily_loss_used  = Math.Abs(Math.Min(state.Daily_PnL,  0)) / state.Day_Open_Balance  * 100m;
            decimal weekly_loss_used = state.Week_Open_Balance > 0
                ? Math.Abs(Math.Min(state.Weekly_PnL, 0)) / state.Week_Open_Balance * 100m : 0m;
            lbl_daily_limit.Text  = $"{_risk_settings.Max_Daily_Loss_Percent:F2}%  (used: {daily_loss_used:F2}%)";
            lbl_weekly_limit.Text = $"{_risk_settings.Max_Weekly_Loss_Percent:F2}%  (used: {weekly_loss_used:F2}%)";
            lbl_dd_breaker.Text   = $"{_risk_settings.Account_Drawdown_Circuit_Breaker_Percent:F2}%  (at: {state.Drawdown_From_Peak:F2}%)";
            lbl_risk_forex.Text   = $"{_risk_settings.Forex_Risk_Per_Trade_Percent:F2}% / trade";
            lbl_risk_gold.Text    = $"{_risk_settings.Gold_Risk_Per_Trade_Percent:F2}% / trade";
            lbl_open_trades.Text  = $"{state.Open_Trade_Count} / {_risk_settings.Max_Open_Trades} max";
        }
    }

    private void Update_Regime_Row(string symbol, Market_Context_BO ctx)
    {
        foreach (DataGridViewRow row in grid_regime.Rows)
        {
            if (row.Cells[0].Value?.ToString() != symbol) continue;
            Color score_col = ctx.Regime_Quality_Score >= 80 ? COL_GREEN
                            : ctx.Regime_Quality_Score >= 70 ? COL_YELLOW : COL_RED;
            row.Cells[1].Value              = ctx.Regime.ToString();
            row.Cells[2].Value              = ctx.Regime_Quality_Score.ToString();
            row.Cells[2].Style.ForeColor    = score_col;
            row.Cells[2].Style.Font         = new Font("Segoe UI", 9, FontStyle.Bold);
            row.Cells[3].Value              = ctx.Current_Session.ToString();
            row.Cells[4].Value              = $"{ctx.Current_Spread_Pips:F1}";
            return;
        }
    }

    private void Update_Positions_Grid(List<Position_BO> positions)
    {
        if (_controller?.Is_Running != true) return;
        grid_positions.Rows.Clear();
        foreach (var p in positions)
        {
            Add_Position_Row(grid_positions,
                p.Symbol_Name,
                p.Direction.ToString().ToUpper(),
                $"{p.Entry_Price:F5}",
                $"{p.Stop_Loss_Price:F5}",
                $"{p.Take_Profit_Price:F5}",
                $"{p.Unrealized_PnL:+$#,##0.00;-$#,##0.00}",
                $"{p.R_Multiple_Current:+0.0;-0.0}R",
                p.Broker_Ticket);
        }
    }

    private async void Grid_Positions_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex != 7 || _controller == null) return;

        var row    = grid_positions.Rows[e.RowIndex];
        long ticket = row.Tag is long t ? t : 0;
        if (ticket == 0) return; // demo row — no real position

        string symbol = row.Cells[0].Value?.ToString() ?? "?";
        var confirm   = MessageBox.Show(
            $"Close {symbol} position (ticket {ticket})?",
            "Confirm Close", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes) return;

        bool ok = await _controller.Close_Position_Async(ticket, "Manual close via UI");
        if (ok)
        {
            grid_positions.Rows.RemoveAt(e.RowIndex);
            Safe_Invoke(() =>
            {
                txt_log.AppendText($"[{DateTime.UtcNow:HH:mm:ss}] Position {ticket} ({symbol}) closed by operator.\r\n");
                txt_log.ScrollToCaret();
            });
        }
        else
        {
            MessageBox.Show($"Failed to close position {ticket}.\nCheck MT5 bridge connection.",
                "Close Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void Refresh_Performance_Tab()
    {
        if (_controller == null) return;
        var (total, total_r, strategies) = _controller.Get_Live_Performance();
        if (total == 0) return; // no live data yet — keep demo values

        var (wr, avg_r, pf, max_dd) = _controller.Get_Overall_Stats();
        lbl_total_trades.Text  = total.ToString();
        lbl_win_rate.Text      = $"{wr:F1}%";
        lbl_avg_r.Text         = $"{avg_r:+0.000;-0.000}";
        lbl_profit_factor.Text = $"{pf:F2}";
        lbl_max_dd.Text        = $"{max_dd:F2}%";
        lbl_total_r.Text       = $"{total_r:+0.0;-0.0}R";

        foreach (DataGridViewRow row in grid_strategy_perf.Rows)
        {
            if (row.Tag is not Strategy_Type type) continue;
            if (!strategies.TryGetValue(type, out var perf) || perf.Total_Trades == 0) continue;

            row.Cells[1].Value = $"{perf.Total_Trades} trades";
            row.Cells[2].Value = $"{perf.Win_Rate:F1}%";
            row.Cells[3].Value = perf.Profit_Factor > 0 ? $"{perf.Profit_Factor:F2}" : "—";
            row.Cells[4].Value = $"{perf.Average_R:+0.000;-0.000}";
        }
    }

    private async Task Refresh_News_Async()
    {
        if (_controller == null) return;
        try
        {
            var events = await _controller.Get_Upcoming_News_Async(hours: 24);
            Safe_Invoke(() => Update_News_Grid(events));
        }
        catch { /* Feed unavailable — grid keeps last values */ }
    }

    private void Update_News_Grid(List<News_Event_BO> events)
    {
        grid_news.Rows.Clear();
        var now = DateTime.UtcNow;

        foreach (var ev in events.Take(8))
        {
            var diff        = ev.Event_UTC - now;
            bool in_lockout = ev.Is_Active_Lockout(now);
            bool approaching = !in_lockout && diff.TotalMinutes < ev.Lockout_Before_Minutes;

            string time_in  = in_lockout ? "NOW"
                            : diff.TotalHours < 1 ? $"{(int)diff.TotalMinutes}m"
                            : $"{(int)diff.TotalHours}h {diff.Minutes:D2}m";
            string status   = in_lockout  ? "LOCKOUT"
                            : approaching ? "SOON" : "—";
            Color status_col = in_lockout  ? COL_RED
                             : approaching ? COL_ORANGE : TEXT_MUTED;

            int row_idx = grid_news.Rows.Add(
                ev.Event_UTC.ToString("HH:mm"),
                ev.Currency,
                ev.Event_Name,
                time_in,
                status);

            var cells = grid_news.Rows[row_idx].Cells;
            cells[4].Style.ForeColor = status_col;
            cells[4].Style.Font      = new Font("Segoe UI", 8, FontStyle.Bold);
            if (in_lockout || approaching)
            {
                cells[1].Style.ForeColor = COL_ORANGE;
                cells[2].Style.ForeColor = COL_ORANGE;
            }
        }
    }

    private void Safe_Invoke(Action action)
    {
        if (InvokeRequired) BeginInvoke(action);
        else action();
    }

    private void Apply_Dark_Theme()
    {
        Text            = "ATLAS Forex + Gold Trading System  v1.0";
        BackColor       = BG_DARK;
        ForeColor       = TEXT_PRIMARY;
        Size            = new Size(1440, 900);
        MinimumSize     = new Size(1200, 720);
        StartPosition   = FormStartPosition.CenterScreen;
        Font            = FONT_NORMAL;
    }

    private void Wire_Tab_Drawing()
    {
        tab_main.DrawMode = TabDrawMode.OwnerDrawFixed;
        tab_main.DrawItem += Tab_DrawItem;
    }

    private void Tab_DrawItem(object? sender, DrawItemEventArgs e)
    {
        var page    = tab_main.TabPages[e.Index];
        bool sel    = tab_main.SelectedIndex == e.Index;
        Color bg    = sel ? BG_CARD : BG_HEADER;
        Color fg    = sel ? TEXT_PRIMARY : TEXT_SECONDARY;

        using var bgBrush = new SolidBrush(bg);
        e.Graphics.FillRectangle(bgBrush, e.Bounds);

        if (sel)
        {
            using var accent = new Pen(COL_GREEN, 2);
            e.Graphics.DrawLine(accent, e.Bounds.Left, e.Bounds.Top, e.Bounds.Right, e.Bounds.Top);
        }

        using var fgBrush = new SolidBrush(fg);
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        e.Graphics.DrawString(page.Text, FONT_BOLD, fgBrush, e.Bounds, sf);
    }

    private void Populate_Demo_Data()
    {
        // Header
        lbl_connection.Text      = "● CONNECTED";
        lbl_connection.ForeColor = COL_GREEN;
        lbl_mode.Text            = "DEMO MODE";
        lbl_mode.ForeColor       = COL_YELLOW;
        lbl_server_time.Text     = $"Server: {DateTime.UtcNow:HH:mm:ss} UTC";

        // Account panel
        lbl_balance.Text      = "$10,000.00";
        lbl_equity.Text       = "$10,340.00";
        lbl_free_margin.Text  = "$9,840.00";
        lbl_daily_pnl.Text    = "+$340.00  (+3.40%)";
        lbl_daily_pnl.ForeColor = COL_GREEN;
        lbl_drawdown.Text     = "0.00%";
        lbl_drawdown.ForeColor = COL_GREEN;

        // Regime scores
        Add_Regime_Row(grid_regime, "EURUSD", "TREND_SWING",       "82", COL_GREEN);
        Add_Regime_Row(grid_regime, "GBPUSD", "COMPRESSION",       "78", COL_YELLOW);
        Add_Regime_Row(grid_regime, "USDJPY", "TREND_SWING",       "85", COL_GREEN);
        Add_Regime_Row(grid_regime, "XAUUSD", "RANGE",             "71", COL_YELLOW);
        Add_Regime_Row(grid_regime, "AUDUSD", "ABNORMAL_NO_TRADE", "44", COL_RED);

        // Open positions
        Add_Position_Row(grid_positions, "EURUSD", "BUY",  "1.08420", "1.08250", "1.09100", "+$180", "+0.9R", 0);
        Add_Position_Row(grid_positions, "XAUUSD", "SELL", "2310.50", "2325.00", "2285.00", "-$45",  "-0.2R", 0);

        // Signal log
        Add_Signal_Log("EURUSD [Trend_Pullback]", "APPROVED", "Score:88 RR:2.1", COL_GREEN);
        Add_Signal_Log("GBPUSD [Session_Breakout]", "REJECTED", "Spread too high: 3.2 pips", COL_RED);
        Add_Signal_Log("XAUUSD [Liquidity_Sweep]", "REJECTED", "News lockout: CPI in 45min", COL_ORANGE);
        Add_Signal_Log("USDJPY [Trend_Pullback]", "APPROVED", "Score:85 RR:2.4", COL_GREEN);

        // Performance tab
        lbl_total_trades.Text  = "47";
        lbl_win_rate.Text      = "44.7%";
        lbl_avg_r.Text         = "+0.31R";
        lbl_profit_factor.Text = "1.48";
        lbl_max_dd.Text        = "2.3%";
        lbl_total_r.Text       = "+14.6R";

        Add_Strategy_Row(Strategy_Type.Trend_Pullback_Continuation, "29 trades", "44.8%", "1.52", "ACTIVE",   COL_GREEN);
        Add_Strategy_Row(Strategy_Type.Session_Breakout,            "18 trades", "44.4%", "1.38", "ACTIVE",   COL_GREEN);
        Add_Strategy_Row(Strategy_Type.Liquidity_Sweep_Reversal,    "0 trades",  "—",     "—",    "DISABLED", COL_YELLOW);
        Add_Strategy_Row(Strategy_Type.Breakout_Retest_Expansion,   "0 trades",  "—",     "—",    "DISABLED", COL_YELLOW);
        Add_Strategy_Row(Strategy_Type.Controlled_Mean_Reversion,   "0 trades",  "—",     "—",    "DISABLED", COL_RED);

        // Risk panel
        lbl_risk_forex.Text = "0.25% / trade";
        lbl_risk_gold.Text  = "0.20% / trade";
        lbl_daily_limit.Text = "1.00%  (used: 0.00%)";
        lbl_weekly_limit.Text = "2.00% (used: 0.00%)";
        lbl_dd_breaker.Text   = "5.00% (at: 0.00%)";
        lbl_open_trades.Text  = "2 / 2 max";
    }

    private static void Add_Regime_Row(DataGridView grid, string symbol, string regime, string score, Color score_color)
    {
        int row = grid.Rows.Add(symbol, regime, score, "—", "—");
        grid.Rows[row].Cells[2].Style.ForeColor = score_color;
        grid.Rows[row].Cells[2].Style.Font = new Font("Segoe UI", 9, FontStyle.Bold);
    }

    private static void Add_Position_Row(DataGridView grid, string symbol, string dir, string entry, string sl, string tp, string pnl, string r, long ticket = 0)
    {
        int row = grid.Rows.Add(symbol, dir, entry, sl, tp, pnl, r, "✕");
        Color dir_col = dir == "BUY" ? COL_GREEN : COL_RED;
        grid.Rows[row].Cells[1].Style.ForeColor = dir_col;
        bool positive = pnl.StartsWith('+');
        grid.Rows[row].Cells[5].Style.ForeColor = positive ? COL_GREEN : COL_RED;
        grid.Rows[row].Cells[6].Style.ForeColor = positive ? COL_GREEN : COL_RED;
        grid.Rows[row].Cells[7].Style.ForeColor = COL_RED;
        grid.Rows[row].Tag = ticket;
    }

    private void Add_Signal_Log(string signal, string status, string detail, Color status_color)
    {
        var time = DateTime.UtcNow.ToString("HH:mm:ss");
        int row = grid_signal_log.Rows.Add(time, signal, status, detail);
        grid_signal_log.Rows[row].Cells[2].Style.ForeColor = status_color;
        grid_signal_log.Rows[row].Cells[2].Style.Font = new Font("Segoe UI", 9, FontStyle.Bold);
    }

    private void Add_Strategy_Row(Strategy_Type type, string trades, string wr, string pf, string status, Color status_color)
    {
        bool is_active  = status == "ACTIVE";
        string btn_text = is_active ? "DISABLE" : "ENABLE";
        Color  btn_color = is_active ? COL_RED   : COL_GREEN;

        int row = grid_strategy_perf.Rows.Add(type.ToString(), trades, wr, pf, "—", status, btn_text);
        var cells = grid_strategy_perf.Rows[row].Cells;
        cells[5].Style.ForeColor = status_color;
        cells[5].Style.Font      = new Font("Segoe UI", 9, FontStyle.Bold);
        cells[6].Style.ForeColor = btn_color;
        cells[6].Style.BackColor = Color.FromArgb(30, 30, 55);

        grid_strategy_perf.Rows[row].Tag = type;
    }

    private void Grid_Strategy_Perf_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex != 6 || _controller == null) return;

        var row = grid_strategy_perf.Rows[e.RowIndex];
        if (row.Tag is not Strategy_Type type) return;

        var status     = _controller.Get_Strategy_Status();
        bool is_active = status.TryGetValue(type, out bool enabled) && enabled;

        if (is_active) _controller.Disable_Strategy(type);
        else           _controller.Enable_Strategy(type);

        bool now_active = !is_active;

        row.Cells[5].Value             = now_active ? "ACTIVE"   : "DISABLED";
        row.Cells[5].Style.ForeColor   = now_active ? COL_GREEN  : COL_YELLOW;
        row.Cells[6].Value             = now_active ? "DISABLE"  : "ENABLE";
        row.Cells[6].Style.ForeColor   = now_active ? COL_RED    : COL_GREEN;

        Safe_Invoke(() =>
        {
            txt_log.AppendText($"[{DateTime.UtcNow:HH:mm:ss}] Strategy {type} {(now_active ? "ENABLED" : "DISABLED")} by operator.\r\n");
            txt_log.ScrollToCaret();
        });
    }

    private void Populate_Symbol_Grid()
    {
        grid_symbols.Rows.Clear();
        if (_controller == null) return;

        foreach (var sym in _controller.Get_Symbol_Universe())
        {
            bool   active   = sym.Is_Enabled;
            string tier_txt = sym.Tier.ToString().Replace("Tier_", "Tier ");
            int row = grid_symbols.Rows.Add(
                sym.Symbol_Name, tier_txt, $"{sym.Normal_Spread_Pips:F1}p",
                active ? "ACTIVE" : "PAUSED",
                active ? "PAUSE"  : "ENABLE");

            var cells = grid_symbols.Rows[row].Cells;
            cells[3].Style.ForeColor = active ? COL_GREEN : TEXT_MUTED;
            cells[4].Style.ForeColor = active ? COL_YELLOW : COL_GREEN;
            cells[4].Style.BackColor = Color.FromArgb(30, 30, 55);
            grid_symbols.Rows[row].Tag = sym.Symbol_Name;
        }
    }

    private void Grid_Symbols_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex != 4 || _controller == null) return;

        var row = grid_symbols.Rows[e.RowIndex];
        if (row.Tag is not string symbol_name) return;

        var sym = _controller.Get_Symbol_Universe().FirstOrDefault(s => s.Symbol_Name == symbol_name);
        if (sym == null) return;

        bool now_enabled = !sym.Is_Enabled;
        _controller.Toggle_Symbol(symbol_name, now_enabled);

        row.Cells[3].Value           = now_enabled ? "ACTIVE"  : "PAUSED";
        row.Cells[3].Style.ForeColor = now_enabled ? COL_GREEN : TEXT_MUTED;
        row.Cells[4].Value           = now_enabled ? "PAUSE"   : "ENABLE";
        row.Cells[4].Style.ForeColor = now_enabled ? COL_YELLOW : COL_GREEN;
    }

    private void btn_start_Click(object sender, EventArgs e)
    {
        btn_start.Enabled = false;
        btn_stop.Enabled  = true;
        txt_log.AppendText($"[{DateTime.UtcNow:HH:mm:ss}] Starting bot ({(Atlas_Config.Demo_Mode ? "DEMO" : "LIVE")} mode, {_cycle_interval_seconds}s cycle)...\r\n");
        _controller?.Start(cycle_interval_seconds: _cycle_interval_seconds);
    }

    private void btn_stop_Click(object sender, EventArgs e)
    {
        btn_start.Enabled = true;
        btn_stop.Enabled  = false;
        _controller?.Stop();
        txt_log.AppendText($"[{DateTime.UtcNow:HH:mm:ss}] Bot stopped.\r\n");
    }

    private void btn_emergency_stop_Click(object sender, EventArgs e)
    {
        btn_start.Enabled = false;
        btn_stop.Enabled  = false;
        _controller?.Trigger_Emergency_Stop("Manual emergency stop via UI.");
        txt_log.AppendText($"[{DateTime.UtcNow:HH:mm:ss}] EMERGENCY STOP activated by operator.\r\n");
    }

    private void btn_settings_Click(object sender, EventArgs e)
    {
        using var form = new Atlas_Settings_Form();
        form.ShowDialog(this);
    }

    private void btn_mute_sounds_Click(object sender, EventArgs e)
    {
        _sounds_muted         = !_sounds_muted;
        btn_mute_sounds.Text  = _sounds_muted ? "🔇" : "🔊";
        btn_mute_sounds.ForeColor = _sounds_muted ? TEXT_MUTED : COL_BLUE;
    }

    private void Update_Session_Label()
    {
        int h = DateTime.UtcNow.Hour;
        var active = new List<string>();

        if (h >= 22 || h < 7)  active.Add("SYD");
        if (h < 9)              active.Add("TKY");
        if (h >= 8 && h < 17)  active.Add("LDN");
        if (h >= 13 && h < 22) active.Add("NY");

        if (active.Count == 0)
        {
            lbl_session.Text      = "Session:  CLOSED";
            lbl_session.ForeColor = TEXT_MUTED;
        }
        else
        {
            bool overlap          = active.Contains("LDN") && active.Contains("NY");
            lbl_session.Text      = "Session:  ● " + string.Join("  ● ", active);
            lbl_session.ForeColor = overlap ? COL_GREEN : COL_YELLOW;
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_controller?.Is_Running == true)
        {
            var result = MessageBox.Show(
                "The trading bot is currently running.\n\nStop the bot and exit?",
                "Bot Is Running",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (result == DialogResult.No)
            {
                e.Cancel = true;
                return;
            }

            _controller.Stop();
        }

        _ui_refresh_timer?.Stop();
        base.OnFormClosing(e);
    }

    private void btn_save_log_Click(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txt_log.Text)) return;

        using var dlg = new SaveFileDialog
        {
            Title    = "Save System Log",
            Filter   = "Text files (*.txt)|*.txt|Log files (*.log)|*.log|All files (*.*)|*.*",
            FileName = $"ATLAS_Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
        };

        if (dlg.ShowDialog() != DialogResult.OK) return;

        try   { File.WriteAllText(dlg.FileName, txt_log.Text); }
        catch (Exception ex)
        {
            MessageBox.Show($"Save failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void btn_clear_log_Click(object sender, EventArgs e)
    {
        if (MessageBox.Show("Clear the system log?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            txt_log.Clear();
    }

    // ── Trade History Handlers ────────────────────────────────────────
    private async void btn_refresh_history_Click(object sender, EventArgs e)
    {
        btn_refresh_history.Enabled = false;
        lbl_history_status.Text     = "Loading from database...";
        lbl_history_status.ForeColor = COL_YELLOW;
        Application.DoEvents();

        try
        {
            var trades = await (_controller?.Get_Recent_Trades_Async(50) ?? Task.FromResult(new List<Trade_Result_BO>()));
            grid_trade_history.Rows.Clear();

            foreach (var t in trades)
            {
                int row = grid_trade_history.Rows.Add(
                    t.Closed_At_UTC.ToString("yyyy-MM-dd HH:mm"),
                    t.Symbol_Name,
                    t.Strategy.ToString(),
                    t.Direction.ToString(),
                    $"{t.Entry_Price:F5}",
                    $"{t.Exit_Price:F5}",
                    $"{t.R_Multiple:+0.00;-0.00}R",
                    $"{t.Net_PnL_Currency:+$#,##0.00;-$#,##0.00}",
                    t.Close_Reason);
                var cells = grid_trade_history.Rows[row].Cells;
                Color row_col = t.Is_Winner ? COL_GREEN : COL_RED;
                cells[6].Style.ForeColor = row_col;
                cells[7].Style.ForeColor = row_col;
            }

            lbl_history_status.Text     = trades.Count > 0
                ? $"Showing {trades.Count} recent trade(s) — last: {trades[0].Closed_At_UTC:yyyy-MM-dd HH:mm} UTC"
                : "No closed trades in database yet.";
            lbl_history_status.ForeColor = TEXT_SECONDARY;
        }
        catch (Exception ex)
        {
            lbl_history_status.Text      = $"DB error: {ex.Message}";
            lbl_history_status.ForeColor = COL_RED;
        }
        finally
        {
            btn_refresh_history.Enabled = true;
        }
    }

    // ── Backtest Handlers ─────────────────────────────────────────────
    private void btn_bt_load_csv_Click(object sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title  = "Select MT5 CSV export (M15 data recommended)",
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
        };

        if (dlg.ShowDialog() != DialogResult.OK) return;

        try
        {
            string symbol = cmb_bt_symbol.SelectedItem?.ToString() ?? "EURUSD";
            lbl_bt_status.Text      = "Loading CSV...";
            lbl_bt_status.ForeColor = COL_YELLOW;
            Application.DoEvents();

            _bt_m15 = CSV_Data_Loader.Load_From_CSV(dlg.FileName, symbol, "M15");
            _bt_h1  = CSV_Data_Loader.Resample(_bt_m15.SelectMany(c =>
            {
                // M15 → H1: group by hour
                return new[] { c };
            }).ToList(), "H1");

            // Resample from M15 → H1, H4, D1
            var m1_equiv = _bt_m15; // use M15 as base for resampling
            _bt_h1  = CSV_Data_Loader.Resample(_bt_m15, "H1");
            _bt_h4  = CSV_Data_Loader.Resample(_bt_m15, "H4");
            _bt_d1  = CSV_Data_Loader.Resample(_bt_m15, "D1");

            lbl_bt_status.Text      = $"Loaded {_bt_m15.Count:N0} M15 bars  →  H1:{_bt_h1.Count}  H4:{_bt_h4.Count}  D1:{_bt_d1.Count}";
            lbl_bt_status.ForeColor = COL_GREEN;
            btn_bt_run.Enabled      = true;
        }
        catch (Exception ex)
        {
            lbl_bt_status.Text      = $"Load failed: {ex.Message}";
            lbl_bt_status.ForeColor = COL_RED;
        }
    }

    private async void btn_bt_run_Click(object sender, EventArgs e)
    {
        if (_bt_m15 == null || _bt_h1 == null || _bt_h4 == null || _bt_d1 == null)
        {
            MessageBox.Show("Load a CSV file first.", "No Data", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        btn_bt_run.Enabled      = false;
        lbl_bt_status.Text      = "Running backtest...";
        lbl_bt_status.ForeColor = COL_YELLOW;
        txt_bt_report.Text      = "";
        Application.DoEvents();

        try
        {
            decimal initial_balance = decimal.TryParse(txt_bt_balance.Text, out var bal) ? bal : 10_000m;
            string symbol = cmb_bt_symbol.SelectedItem?.ToString() ?? "EURUSD";

            var config = new Backtest_Config
            {
                Symbol_Name     = symbol,
                Start_Date      = dtp_bt_from.Value.ToUniversalTime(),
                End_Date        = dtp_bt_to.Value.ToUniversalTime(),
                Initial_Balance = initial_balance
            };

            var data_provider = new Backtest_Data_Provider(_bt_m15, _bt_h1, _bt_h4, _bt_d1);
            var engine        = new Backtest_Engine();

            engine.On_Progress += msg => Safe_Invoke(() =>
            {
                lbl_bt_status.Text      = msg;
                lbl_bt_status.ForeColor = COL_YELLOW;
            });

            _bt_cts = new CancellationTokenSource();
            _bt_last_result = await Task.Run(() => engine.Run_Async(config, data_provider, _bt_cts.Token));

            Safe_Invoke(() =>
            {
                txt_bt_report.Text         = Backtest_Report.Generate_Text(_bt_last_result);
                lbl_bt_status.Text         = $"Done — {_bt_last_result.Total_Trades} trades, PF {_bt_last_result.Profit_Factor:F2}";
                lbl_bt_status.ForeColor    = _bt_last_result.Passed_Validation ? COL_GREEN : COL_RED;
                btn_bt_run.Enabled         = true;
                btn_bt_monte_carlo.Enabled = _bt_last_result.Trades.Count > 0;
                btn_bt_save_report.Enabled = true;
                pnl_bt_equity.Invalidate();
                Populate_Walk_Forward(_bt_last_result);
            });
        }
        catch (Exception ex)
        {
            lbl_bt_status.Text      = $"Error: {ex.Message}";
            lbl_bt_status.ForeColor = COL_RED;
            btn_bt_run.Enabled      = true;
        }
    }

    private void btn_bt_monte_carlo_Click(object sender, EventArgs e)
    {
        if (_bt_last_result == null || _bt_last_result.Trades.Count == 0)
        {
            MessageBox.Show("Run a backtest first.", "No Data", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        btn_bt_monte_carlo.Enabled  = false;
        lbl_bt_status.Text          = "Running Monte Carlo (1,000 simulations)...";
        lbl_bt_status.ForeColor     = COL_YELLOW;
        Application.DoEvents();

        var r_multiples = _bt_last_result.Trades.Select(t => t.R_Multiple).ToList();
        var tester      = new Monte_Carlo_Tester();
        var mc_result   = Task.Run(() => tester.Run(r_multiples, iterations: 1_000)).GetAwaiter().GetResult();
        var mc_text     = Backtest_Report.Generate_Monte_Carlo_Section(mc_result);

        txt_bt_report.AppendText(mc_text);
        txt_bt_report.SelectionStart  = txt_bt_report.TextLength;
        txt_bt_report.ScrollToCaret();

        bool passed             = mc_result.P95_Max_Drawdown < 15m &&
                                  mc_result.P05_Profit_Factor >= 1.0m &&
                                  mc_result.Prob_Ruin_Percent < 5m;
        lbl_bt_status.Text      = $"MC done — stress DD: {mc_result.P95_Max_Drawdown:F1}%  ruin: {mc_result.Prob_Ruin_Percent:F1}%";
        lbl_bt_status.ForeColor = passed ? COL_GREEN : COL_RED;
        btn_bt_monte_carlo.Enabled = true;
    }

    private void btn_bt_save_report_Click(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txt_bt_report.Text)) return;

        string symbol = cmb_bt_symbol.SelectedItem?.ToString() ?? "ATLAS";
        using var dlg = new SaveFileDialog
        {
            Title    = "Save Backtest Report",
            Filter   = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            FileName = $"ATLAS_Backtest_{symbol}_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
        };

        if (dlg.ShowDialog() != DialogResult.OK) return;

        try
        {
            File.WriteAllText(dlg.FileName, txt_bt_report.Text);
            lbl_bt_status.Text      = $"Report saved: {Path.GetFileName(dlg.FileName)}";
            lbl_bt_status.ForeColor = COL_GREEN;
        }
        catch (Exception ex)
        {
            lbl_bt_status.Text      = $"Save failed: {ex.Message}";
            lbl_bt_status.ForeColor = COL_RED;
        }
    }

    private void Populate_Walk_Forward(Backtest_Result result)
    {
        grid_wf.Rows.Clear();

        var ins  = result.In_Sample;
        var outs = result.Out_Sample;
        if (ins == null || outs == null)
        {
            int row = grid_wf.Rows.Add("Not enough trades for walk-forward (need 15+)", "", "", "", "", "");
            grid_wf.Rows[row].Cells[0].Style.ForeColor = TEXT_MUTED;
            return;
        }

        // In-Sample row
        grid_wf.Rows.Add("In-Sample",
            ins.Total_Trades.ToString(),
            $"{ins.Win_Rate:F1}%",
            $"{ins.Profit_Factor:F2}",
            $"{ins.Avg_R:+0.000;-0.000}R",
            $"{ins.Max_Drawdown_Pct:F1}%");

        // Out-of-Sample row (color against in-sample)
        int oos_idx = grid_wf.Rows.Add("Out-of-Sample",
            outs.Total_Trades.ToString(),
            $"{outs.Win_Rate:F1}%",
            $"{outs.Profit_Factor:F2}",
            $"{outs.Avg_R:+0.000;-0.000}R",
            $"{outs.Max_Drawdown_Pct:F1}%");

        var oos = grid_wf.Rows[oos_idx].Cells;
        oos[2].Style.ForeColor = outs.Win_Rate      >= ins.Win_Rate      * 0.85m ? COL_GREEN : COL_RED;
        oos[3].Style.ForeColor = outs.Profit_Factor >= ins.Profit_Factor * 0.80m ? COL_GREEN : COL_RED;
        oos[4].Style.ForeColor = outs.Avg_R         >= ins.Avg_R         * 0.70m ? COL_GREEN : COL_RED;
        oos[5].Style.ForeColor = outs.Max_Drawdown_Pct <= ins.Max_Drawdown_Pct * 1.25m ? COL_GREEN : COL_RED;

        // Degradation row
        decimal wr_d  = outs.Win_Rate      - ins.Win_Rate;
        decimal pf_d  = outs.Profit_Factor - ins.Profit_Factor;
        decimal ar_d  = outs.Avg_R         - ins.Avg_R;
        decimal dd_d  = outs.Max_Drawdown_Pct - ins.Max_Drawdown_Pct;

        int deg_idx = grid_wf.Rows.Add("Degradation",
            "—",
            $"{wr_d:+0.0;-0.0}%",
            $"{pf_d:+0.00;-0.00}",
            $"{ar_d:+0.000;-0.000}R",
            $"{dd_d:+0.0;-0.0}%");

        var deg = grid_wf.Rows[deg_idx].Cells;
        deg[0].Style.ForeColor = TEXT_MUTED;
        for (int i = 2; i <= 5; i++)
        {
            string v = deg[i].Value?.ToString() ?? "";
            // For WR, PF, AvgR: positive degradation = green; for DD: negative = green
            bool positive_good = (i <= 4);
            bool is_positive   = v.StartsWith('+');
            deg[i].Style.ForeColor = positive_good == is_positive ? COL_GREEN : COL_RED;
        }

        // Pass / Fail verdict
        bool passed = outs.Passed && pf_d >= -0.3m;
        int vrd_idx = grid_wf.Rows.Add(
            passed ? "✓ WALK-FORWARD PASSED" : "✗ WALK-FORWARD FAILED",
            "", "", "", "", "");
        grid_wf.Rows[vrd_idx].Cells[0].Style.ForeColor = passed ? COL_GREEN : COL_RED;
        grid_wf.Rows[vrd_idx].Cells[0].Style.Font      = new Font("Segoe UI", 9, FontStyle.Bold);
    }

    // ── Equity Curve Paint ────────────────────────────────────────────
    private void Pnl_Bt_Equity_Paint(object? sender, PaintEventArgs e)
    {
        var curve = _bt_last_result?.Equity_Curve;
        if (curve == null || curve.Count < 2) return;

        var g     = e.Graphics;
        int pw    = pnl_bt_equity.Width  - 20;
        int ph    = pnl_bt_equity.Height - 40;
        int ox    = 10;
        int oy    = 30;

        decimal min_eq = curve.Min(p => p.Equity);
        decimal max_eq = curve.Max(p => p.Equity);
        if (max_eq == min_eq) max_eq = min_eq + 1;

        float x_step = (float)pw / (curve.Count - 1);
        Func<int, float>  X = i => ox + i * x_step;
        Func<decimal, float> Y = eq => oy + ph - (float)((eq - min_eq) / (max_eq - min_eq) * ph);

        // Grid lines
        using var grid_pen = new Pen(Color.FromArgb(30, 80, 80, 140), 1);
        for (int i = 0; i <= 4; i++)
        {
            float yy = oy + ph / 4f * i;
            g.DrawLine(grid_pen, ox, yy, ox + pw, yy);
            decimal val = max_eq - (max_eq - min_eq) / 4m * i;
            g.DrawString($"${val:F0}", FONT_SMALL, new SolidBrush(TEXT_MUTED), ox, yy - 10);
        }

        // Zero-baseline (initial balance)
        decimal initial = curve[0].Equity;
        float y_initial = Y(initial);
        using var baseline_pen = new Pen(Color.FromArgb(60, 128, 128, 128), 1) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
        g.DrawLine(baseline_pen, ox, y_initial, ox + pw, y_initial);

        // Equity line
        bool in_profit = _bt_last_result!.Final_Equity >= initial;
        using var eq_pen = new Pen(in_profit ? COL_GREEN : COL_RED, 2);

        for (int i = 1; i < curve.Count; i++)
            g.DrawLine(eq_pen, X(i - 1), Y(curve[i - 1].Equity), X(i), Y(curve[i].Equity));

        // Final value label
        string final_lbl = $"Final: ${_bt_last_result.Final_Equity:F2}  ({_bt_last_result.Total_Return_Percent:+0.00;-0.00}%)";
        using var final_brush = new SolidBrush(in_profit ? COL_GREEN : COL_RED);
        g.DrawString(final_lbl, FONT_BOLD, final_brush, ox + pw / 2 - 80, oy - 20);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _controller?.Stop();
        _ui_refresh_timer?.Stop();
        _ui_refresh_timer?.Dispose();
        _connection_status?.Dispose();
        _bt_cts?.Cancel();
        base.OnFormClosed(e);
        FONT_LARGE.Dispose();
        FONT_MEDIUM.Dispose();
        FONT_NORMAL.Dispose();
        FONT_SMALL.Dispose();
        FONT_BOLD.Dispose();
        FONT_MONO.Dispose();
    }
}
