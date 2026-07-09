using Atlas_Application.Backtest;
using Atlas_Application.Services;
using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;
using Atlas_Execution.MT5;
using Atlas_Shared;

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
    private bool _tray_close  = false;
    private System.Windows.Forms.Timer? _daily_report_timer;
    private NotifyIcon? _tray_icon;

    private static readonly string _strategy_state_path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ATLAS", "strategy_state.json");

    // Backtest state
    private List<Candle_BO>? _bt_m15;
    private List<Candle_BO>? _bt_h1;
    private List<Candle_BO>? _bt_h4;
    private List<Candle_BO>? _bt_d1;
    private Backtest_Result? _bt_last_result;
    private CancellationTokenSource? _bt_cts;
    private List<decimal>? _live_equity_series;
    private decimal _peak_equity = 0m;
    private StreamWriter? _log_writer;

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

        var risk_override = new Risk_Setting_BO
        {
            Mode                                     = Atlas_Config.Demo_Mode ? Bot_Mode_Type.Demo : Bot_Mode_Type.Micro_Live,
            Forex_Risk_Per_Trade_Percent             = Atlas_Config.Risk_Forex_Per_Trade,
            Gold_Risk_Per_Trade_Percent              = Atlas_Config.Risk_Gold_Per_Trade,
            Max_Daily_Loss_Percent                   = Atlas_Config.Risk_Max_Daily_Loss,
            Max_Weekly_Loss_Percent                  = Atlas_Config.Risk_Max_Weekly_Loss,
            Account_Drawdown_Circuit_Breaker_Percent = Atlas_Config.Risk_Max_Drawdown,
            Full_Stop_Drawdown_Percent               = Atlas_Config.Risk_Full_Stop_Drawdown,
            Caution_Drawdown_Percent                 = Atlas_Config.Risk_Caution_Drawdown,
            Recovery_Drawdown_Percent                = Atlas_Config.Risk_Recovery_Drawdown,
            Protection_Drawdown_Percent              = Atlas_Config.Risk_Protection_Drawdown,
            Max_Open_Trades                          = Atlas_Config.Risk_Max_Open_Trades,
            Max_Gold_Trades                          = Atlas_Config.Risk_Max_Gold_Trades,
            Max_Consecutive_Losses_Before_Pause      = Atlas_Config.Risk_Max_Consecutive_Losses,
            Min_Quality_Score_Live                   = Atlas_Config.Risk_Min_Quality_Score_Live,
            Min_Quality_Score_Gold_Live              = Atlas_Config.Risk_Min_Quality_Score_Gold_Live,
            Min_Reward_Risk_Forex                    = Atlas_Config.Risk_Min_RR_Forex,
            Min_Reward_Risk_Gold_Swing               = Atlas_Config.Risk_Min_RR_Gold_Swing,
            Min_Reward_Risk_Intraday                 = Atlas_Config.Risk_Min_RR_Intraday,
            Max_Lot_Size_Forex                       = Atlas_Config.Risk_Max_Lot_Forex,
            Max_Lot_Size_Gold                        = Atlas_Config.Risk_Max_Lot_Gold,
        };

        var (controller, _, account_service, bridge) = Atlas_Bot_Bootstrap.Create(
            mt5_host:        host,
            mt5_port:        port,
            demo_mode:       Atlas_Config.Demo_Mode,
            db_path:         Atlas_Config.Db_Path,
            telegram_token:  Atlas_Config.Telegram_Bot_Token,
            telegram_chatid: Atlas_Config.Telegram_Chat_Id,
            email_smtp_host: Atlas_Config.Email_Smtp_Host,
            email_smtp_port: Atlas_Config.Email_Smtp_Port,
            email_username:  Atlas_Config.Email_Username,
            email_password:  Atlas_Config.Email_Password,
            email_to:        Atlas_Config.Email_To,
            news_feed_url:       Atlas_Config.News_Feed_Url,
            risk_override:       risk_override,
            spread_multiplier:   Atlas_Config.Spread_Limit_Multiplier);
        _controller      = controller;
        _account_service = account_service;
        _risk_settings   = controller.Risk_Settings;

        // Open daily log file in %AppData%\ATLAS\logs\
        try
        {
            string log_dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ATLAS", "logs");
            Directory.CreateDirectory(log_dir);
            string log_path = Path.Combine(log_dir, $"atlas_{DateTime.Now:yyyyMMdd}.log");
            _log_writer = new StreamWriter(log_path, append: true, System.Text.Encoding.UTF8)
                { AutoFlush = true };
            _log_writer.WriteLine($"──── Session started {DateTime.Now:yyyy-MM-dd HH:mm:ss} ────");
        }
        catch { /* non-critical — continue without file logging */ }

        // Wire connection monitor (pings MT5 bridge every 10 seconds) — reuses the single
        // shared bridge from Bootstrap instead of opening a second competing connection
        _connection_status = new Connection_Status_Panel(bridge, lbl_connection);
        _ = _connection_status.Ping_Async(); // first ping immediately

        _controller.On_Log += msg => Safe_Invoke(() =>
        {
            txt_log.AppendText(msg + "\r\n");
            txt_log.ScrollToCaret();
            _log_writer?.WriteLine(msg);
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
        {
            lbl_bot_status.Text      = $"● RUNNING  ({count} cycles)";
            lbl_bot_status.ForeColor = COL_GREEN;
        });

        _controller.On_Regime_Updated        += (sym, ctx)    => Safe_Invoke(() => Update_Regime_Row(sym, ctx));
        _controller.On_Positions_Updated     += pos            => Safe_Invoke(() => Update_Positions_Grid(pos));
        _controller.On_Strategy_State_Changed += (type, active) => Safe_Invoke(() =>
        {
            Refresh_Strategy_Grid_Row(type, active);
            Save_Strategy_State();
        });

        _connection_status.On_Connection_Changed += connected => Safe_Invoke(() =>
            On_Bridge_Connection_Changed(connected));

        _controller.On_Health_Alert += msg => Safe_Invoke(() =>
        {
            lbl_bot_status.Text    = "⚠ STALE CYCLE";
            lbl_bot_status.ForeColor = COL_YELLOW;
            txt_log.AppendText($"[{DateTime.UtcNow:HH:mm:ss}] [WATCHDOG] {msg}\r\n");
            txt_log.ScrollToCaret();
        });

        Populate_Symbol_Grid();
        Load_And_Apply_Strategy_State();
        _ = Restore_Saved_Positions_Async();
        _ = Load_Db_Equity_Baseline_Async();
        _ = Load_Recent_Signals_From_Db_Async();
        _ = Run_Preflight_Async();
    }

    private async Task Run_Preflight_Async()
    {
        await Task.Yield(); // let the form finish rendering first

        string ts = DateTime.UtcNow.ToString("HH:mm:ss");
        txt_log.AppendText($"[{ts}] ── Startup Preflight ──────────────────\r\n");

        // 1. Config sanity
        bool cfg_ok = !string.IsNullOrWhiteSpace(Atlas_Config.MT5_Host);
        txt_log.AppendText(cfg_ok
            ? $"[{ts}] ✓ Config: MT5 host = {Atlas_Config.MT5_Host}:{Atlas_Config.MT5_Port}\r\n"
            : $"[{ts}] ✗ Config: MT5_Host is empty — open ⚙ Settings to fix\r\n");

        // 2. MT5 bridge ping
        bool mt5_ok = false;
        if (_connection_status != null)
        {
            await _connection_status.Ping_Async();
            mt5_ok = _connection_status.Is_Connected;
        }
        string mode_str = Atlas_Config.Demo_Mode ? "DEMO" : "LIVE";
        txt_log.AppendText(mt5_ok
            ? $"[{ts}] ✓ MT5 bridge reachable — {mode_str} mode\r\n"
            : $"[{ts}] ✗ MT5 bridge unreachable — start ATLAS_Bridge.mq5 EA in MT5\r\n");

        // 3. Live position sync from MT5 (skip in demo; bridge has no real positions)
        if (mt5_ok && !Atlas_Config.Demo_Mode && _controller != null)
        {
            await _controller.Sync_Live_Positions_Async();
        }

        // 4. Demo-mode confirmation
        if (!Atlas_Config.Demo_Mode)
        {
            txt_log.AppendText($"[{ts}] ⚠ LIVE mode active — confirm risk settings before starting bot\r\n");
        }

        txt_log.AppendText($"[{ts}] ── Preflight complete ─────────────────\r\n");
        txt_log.ScrollToCaret();
    }

    private void Start_UI_Timer()
    {
        _ui_refresh_timer = new System.Windows.Forms.Timer { Interval = 5000 };
        _ui_refresh_timer.Tick += (_, _) => Update_Server_Time();
        _ui_refresh_timer.Start();

        Schedule_Daily_Report_Timer();
        Setup_Tray_Icon();
    }

    private void Setup_Tray_Icon()
    {
        // Create a 16×16 icon programmatically (dark-theme "A" badge)
        using var bmp = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.FromArgb(14, 14, 21));
            g.FillEllipse(new SolidBrush(Color.FromArgb(61, 221, 128)), 1, 1, 14, 14);
            g.DrawString("A", new Font("Arial", 7, FontStyle.Bold), Brushes.Black, 3f, 2f);
        }
        var icon = Icon.FromHandle(bmp.GetHicon());

        var ctx_menu = new ContextMenuStrip { BackColor = Color.FromArgb(25, 25, 42), ForeColor = Color.FromArgb(221, 224, 240) };
        ctx_menu.Items.Add("Show Dashboard",     null, (_, _) => { Show(); WindowState = FormWindowState.Normal; Activate(); });
        ctx_menu.Items.Add("Start Bot",          null, (_, _) => { if (_controller != null && !_controller.Is_Running) btn_start.PerformClick(); });
        ctx_menu.Items.Add("Stop Bot",           null, (_, _) => { if (_controller?.Is_Running == true) btn_stop.PerformClick(); });
        ctx_menu.Items.Add(new ToolStripSeparator());
        ctx_menu.Items.Add("Exit ATLAS",         null, (_, _) => { _tray_close = true; Close(); });

        _tray_icon = new NotifyIcon
        {
            Text            = "ATLAS Trading Bot",
            Icon            = icon,
            ContextMenuStrip = ctx_menu,
            Visible         = true
        };
        _tray_icon.DoubleClick += (_, _) => { Show(); WindowState = FormWindowState.Normal; Activate(); };
    }

    private void Schedule_Daily_Report_Timer()
    {
        // Fire once at 21:00 UTC (NY close) then every 24 hours
        const int REPORT_HOUR_UTC = 21;
        var now      = DateTime.UtcNow;
        var next_run = new DateTime(now.Year, now.Month, now.Day, REPORT_HOUR_UTC, 0, 0, DateTimeKind.Utc);
        if (now >= next_run) next_run = next_run.AddDays(1);

        int ms_until = (int)Math.Min((next_run - now).TotalMilliseconds, int.MaxValue);

        _daily_report_timer = new System.Windows.Forms.Timer { Interval = ms_until };
        _daily_report_timer.Tick += async (_, _) =>
        {
            _daily_report_timer!.Interval = 24 * 60 * 60 * 1000; // 24 h for subsequent firings
            if (_controller != null)
                await _controller.Send_Daily_Summary_Async(DateTime.UtcNow.Date);
        };
        _daily_report_timer.Start();
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

        _live_equity_series = _controller?.Get_Equity_Series();
        pnl_live_equity?.Invalidate();

        _controller?.Check_Health(_cycle_interval_seconds);
    }

    private async Task Load_Recent_Signals_From_Db_Async()
    {
        if (_controller == null) return;
        try
        {
            var signals = await _controller.Get_Recent_Signals_Async(50);
            if (signals.Count == 0) return;
            Safe_Invoke(() =>
            {
                // Insert DB signals below existing demo rows (oldest first so newest is at top)
                foreach (var sig in signals.OrderBy(s => s.Generated_At_UTC))
                {
                    bool approved   = sig.Is_Approved;
                    string label    = $"{sig.Symbol_Name} [{sig.Strategy}]";
                    string detail   = approved
                        ? $"Score:{sig.Quality_Score} RR:{sig.Reward_Risk_Ratio:F1}"
                        : sig.Reject_Detail;
                    Color col = approved ? COL_GREEN : COL_RED;
                    string ts = sig.Generated_At_UTC.ToString("HH:mm:ss");

                    int row = grid_signal_log.Rows.Add(ts, label, approved ? "APPROVED" : "REJECTED", detail);
                    grid_signal_log.Rows[row].Cells[2].Style.ForeColor = col;
                    grid_signal_log.Rows[row].Cells[2].Style.Font = new Font("Segoe UI", 9, FontStyle.Bold);
                }
            });
        }
        catch { /* non-critical */ }
    }

    private async Task Load_Db_Equity_Baseline_Async()
    {
        if (_controller == null) return;
        try
        {
            var curve = await _controller.Get_Equity_Curve_Async(days: 90);
            if (curve.Count == 0) return;

            decimal base_equity = curve.First().Equity;
            var baseline = curve.Select(p => p.Equity - base_equity).ToList();
            decimal db_peak = await _controller.Get_Peak_Equity_From_Db_Async();

            Safe_Invoke(() =>
            {
                _live_equity_series = baseline;
                if (db_peak > _peak_equity && db_peak > 0)
                {
                    _peak_equity = db_peak;
                    lbl_peak_equity.Text      = $"${_peak_equity:N2}";
                    lbl_peak_equity.ForeColor = TEXT_MUTED;
                }
                pnl_live_equity?.Invalidate();
            });
        }
        catch { /* non-critical — chart stays empty until live data arrives */ }
    }

    private async Task Restore_Saved_Positions_Async()
    {
        if (_controller == null) return;
        try
        {
            var saved = await _controller.Get_Last_Known_Positions_Async();
            if (saved.Count == 0) return;
            Safe_Invoke(() =>
            {
                grid_positions.Rows.Clear();
                foreach (var p in saved)
                    Add_Position_Row(grid_positions,
                        p.Symbol_Name, p.Direction.ToString().ToUpper(),
                        $"{p.Entry_Price:F5}", $"{p.Stop_Loss_Price:F5}",
                        $"{p.Take_Profit_Price:F5}", "—", "—", p.Broker_Ticket);
                txt_log.AppendText($"[{DateTime.UtcNow:HH:mm:ss}] Restored {saved.Count} saved position(s) from last session.\r\n");
                txt_log.ScrollToCaret();
            });
        }
        catch { /* non-critical */ }
    }

    private async Task Refresh_Account_Async()
    {
        if (_account_service == null || _connection_status?.Is_Connected != true) return;
        try
        {
            int open_count = Safe_Get_Position_Count();
            var state = await _account_service.Get_Account_State_Async(open_trade_count: open_count);
            Safe_Invoke(() => Update_Account_Labels(state));
            if (_controller != null)
                _ = _controller.Save_Account_Snapshot_Async(state);
        }
        catch { /* Bridge not connected — labels keep last value */ }
    }

    private int Safe_Get_Position_Count()
    {
        if (InvokeRequired)
        {
            int result = 0;
            Invoke(() => result = grid_positions.Rows.Count);
            return result;
        }
        return grid_positions.Rows.Count;
    }

    private void Update_Account_Labels(Account_State_BO state)
    {
        if (state.Balance == 0) return;

        lbl_balance.Text     = $"${state.Balance:N2}";
        lbl_equity.Text      = $"${state.Equity:N2}";
        lbl_free_margin.Text = $"${state.Free_Margin:N2}";

        if (state.Equity > _peak_equity && state.Equity > 0)
        {
            _peak_equity = state.Equity;
            lbl_peak_equity.Text      = $"${_peak_equity:N2}  ▲ NEW PEAK";
            lbl_peak_equity.ForeColor = COL_GREEN;
            txt_log.AppendText($"[{DateTime.UtcNow:HH:mm:ss}] New equity peak: ${_peak_equity:N2}\r\n");
            _tray_icon?.ShowBalloonTip(3000, "ATLAS — New Equity Peak",
                $"Account equity reached a new high: ${_peak_equity:N2}", ToolTipIcon.Info);
        }
        else if (_peak_equity > 0)
        {
            lbl_peak_equity.Text      = $"${_peak_equity:N2}";
            lbl_peak_equity.ForeColor = TEXT_MUTED;
        }

        decimal daily_pnl = state.Daily_PnL;
        decimal daily_pct = state.Day_Open_Balance > 0
            ? daily_pnl / state.Day_Open_Balance * 100m : 0m;
        lbl_daily_pnl.Text      = $"{daily_pnl:+$#,##0.00;-$#,##0.00}  ({daily_pct:+0.00;-0.00}%)";
        lbl_daily_pnl.ForeColor = daily_pnl >= 0 ? COL_GREEN : COL_RED;

        decimal recovery_thr = _risk_settings?.Recovery_Drawdown_Percent ?? 4m;
        decimal caution_thr  = _risk_settings?.Caution_Drawdown_Percent  ?? 2m;
        string dd_mode = state.Drawdown_From_Peak >= recovery_thr ? "  ⚠ RECOVERY"
                       : state.Drawdown_From_Peak >= caution_thr  ? "  ⚠ CAUTION"
                       : string.Empty;
        lbl_drawdown.Text      = $"{state.Drawdown_From_Peak:F2}%{dd_mode}";
        lbl_drawdown.ForeColor = state.Drawdown_From_Peak >= recovery_thr ? COL_RED
                               : state.Drawdown_From_Peak >= caution_thr  ? COL_YELLOW
                               : COL_GREEN;

        // Update risk tab live usage
        if (_risk_settings != null && state.Day_Open_Balance > 0)
        {
            decimal daily_loss_used  = Math.Abs(Math.Min(state.Daily_PnL,  0)) / state.Day_Open_Balance  * 100m;
            decimal weekly_loss_used = state.Week_Open_Balance > 0
                ? Math.Abs(Math.Min(state.Weekly_PnL, 0)) / state.Week_Open_Balance * 100m : 0m;
            lbl_daily_limit.Text  = $"{_risk_settings.Max_Daily_Loss_Percent:F2}%  (used: {daily_loss_used:F2}%)";
            lbl_weekly_limit.Text = $"{_risk_settings.Max_Weekly_Loss_Percent:F2}%  (used: {weekly_loss_used:F2}%)";
            decimal daily_pct_used = _risk_settings.Max_Daily_Loss_Percent > 0
                ? daily_loss_used / _risk_settings.Max_Daily_Loss_Percent : 0m;
            lbl_daily_limit.ForeColor = daily_pct_used >= 0.90m ? COL_RED
                                      : daily_pct_used >= 0.80m ? COL_YELLOW
                                      : TEXT_PRIMARY;
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
            row.Cells[5].Value              = Bias_Arrow(ctx.D1_Bias);
            row.Cells[5].Style.ForeColor    = Bias_Color(ctx.D1_Bias);
            row.Cells[6].Value              = Bias_Arrow(ctx.H4_Bias);
            row.Cells[6].Style.ForeColor    = Bias_Color(ctx.H4_Bias);
            row.Cells[7].Value              = Bias_Arrow(ctx.H1_Bias);
            row.Cells[7].Style.ForeColor    = Bias_Color(ctx.H1_Bias);
            row.Cells[8].Value              = ctx.Higher_Timeframes_Aligned ? "✓" : "—";
            row.Cells[8].Style.ForeColor    = ctx.Higher_Timeframes_Aligned ? COL_GREEN : TEXT_MUTED;
            return;
        }
    }

    private static string Bias_Arrow(Trade_Direction_Type dir) => dir switch
    {
        Trade_Direction_Type.Buy  => "▲",
        Trade_Direction_Type.Sell => "▼",
        _                         => "—"
    };

    private static Color Bias_Color(Trade_Direction_Type dir) => dir switch
    {
        Trade_Direction_Type.Buy  => COL_GREEN,
        Trade_Direction_Type.Sell => COL_RED,
        _                         => TEXT_MUTED
    };

    private void Update_Positions_Grid(List<Position_BO> positions)
    {
        if (_controller?.Is_Running != true) return;
        var universe = _controller.Get_Symbol_Universe();
        grid_positions.Rows.Clear();
        foreach (var p in positions)
        {
            string duration = p.Opened_At_UTC != default
                ? Format_Duration(DateTime.UtcNow - p.Opened_At_UTC) : "—";

            // Approximate R from broker-reported PnL since Current_Price is not returned by bridge
            var sym_info   = universe.FirstOrDefault(s => s.Symbol_Name == p.Symbol_Name);
            decimal pip_val = sym_info?.Pip_Value_Per_Lot ?? 10m;
            decimal r_approx = (p.Initial_Stop_Distance_Pips > 0 && p.Lot_Size > 0)
                ? p.Unrealized_PnL / (p.Initial_Stop_Distance_Pips * p.Lot_Size * pip_val)
                : 0m;

            Add_Position_Row(grid_positions,
                p.Symbol_Name,
                p.Direction.ToString().ToUpper(),
                $"{p.Entry_Price:F5}",
                $"{p.Stop_Loss_Price:F5}",
                $"{p.Take_Profit_Price:F5}",
                $"{p.Unrealized_PnL:+$#,##0.00;-$#,##0.00}",
                $"{r_approx:+0.0;-0.0}R",
                p.Broker_Ticket,
                duration);
        }
    }

    private static string Format_Duration(TimeSpan ts)
    {
        if (ts.TotalSeconds <= 0)  return "—";
        if (ts.TotalDays >= 1)     return $"{(int)ts.TotalDays}d {ts.Hours}h";
        if (ts.TotalHours >= 1)    return $"{(int)ts.TotalHours}h {ts.Minutes:D2}m";
        return $"{(int)ts.TotalMinutes}m";
    }

    private async void Grid_Positions_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex != 8 || _controller == null) return;

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

        decimal sharpe  = _controller.Get_Sharpe_Ratio();
        decimal sortino = _controller.Get_Sortino_Ratio();
        lbl_sharpe.Text  = sharpe  != 0 ? $"{sharpe:F2}"  : "—";
        lbl_sortino.Text = sortino != 0 ? $"{sortino:F2}" : "—";

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
        _peak_equity          = 10_450m;
        lbl_peak_equity.Text  = "$10,450.00";
        lbl_peak_equity.ForeColor = TEXT_MUTED;

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
        lbl_sharpe.Text        = "0.62";
        lbl_sortino.Text       = "0.91";

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
        int row = grid.Rows.Add(symbol, regime, score, "—", "—", "—", "—", "—", "—");
        grid.Rows[row].Cells[2].Style.ForeColor = score_color;
        grid.Rows[row].Cells[2].Style.Font = new Font("Segoe UI", 9, FontStyle.Bold);
    }

    private static void Add_Position_Row(DataGridView grid, string symbol, string dir, string entry, string sl, string tp, string pnl, string r, long ticket = 0, string duration = "—")
    {
        int row = grid.Rows.Add(symbol, dir, entry, sl, tp, pnl, r, duration, "✕");
        Color dir_col = dir == "BUY" ? COL_GREEN : COL_RED;
        grid.Rows[row].Cells[1].Style.ForeColor = dir_col;
        bool positive = pnl.StartsWith('+');
        grid.Rows[row].Cells[5].Style.ForeColor = positive ? COL_GREEN : COL_RED;
        grid.Rows[row].Cells[6].Style.ForeColor = positive ? COL_GREEN : COL_RED;
        grid.Rows[row].Cells[7].Style.ForeColor = TEXT_MUTED;
        grid.Rows[row].Cells[8].Style.ForeColor = COL_RED;
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

        // Enable/Disable fires On_Strategy_State_Changed → Refresh_Strategy_Grid_Row + Save_Strategy_State
        if (is_active) _controller.Disable_Strategy(type);
        else           _controller.Enable_Strategy(type);

        bool now_active = !is_active;
        txt_log.AppendText($"[{DateTime.UtcNow:HH:mm:ss}] Strategy {type} {(now_active ? "ENABLED" : "DISABLED")} by operator.\r\n");
        txt_log.ScrollToCaret();
    }

    private void Save_Strategy_State()
    {
        if (_controller == null) return;
        try
        {
            var state = _controller.Get_Strategy_Status()
                .ToDictionary(kv => kv.Key.ToString(), kv => kv.Value);
            string json = System.Text.Json.JsonSerializer.Serialize(state,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            Directory.CreateDirectory(Path.GetDirectoryName(_strategy_state_path)!);
            File.WriteAllText(_strategy_state_path, json, System.Text.Encoding.UTF8);
        }
        catch { /* non-critical */ }
    }

    private void Load_And_Apply_Strategy_State()
    {
        if (_controller == null || !File.Exists(_strategy_state_path)) return;
        try
        {
            string json = File.ReadAllText(_strategy_state_path, System.Text.Encoding.UTF8);
            var state = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, bool>>(json);
            if (state == null) return;
            foreach (var kv in state)
            {
                if (!Enum.TryParse<Strategy_Type>(kv.Key, out var st)) continue;
                if (kv.Value) _controller.Enable_Strategy(st);
                else          _controller.Disable_Strategy(st);
            }
            // Reflect loaded state in the strategy performance grid
            Refresh_All_Strategy_Grid_Rows();
        }
        catch { /* non-critical */ }
    }

    private void Refresh_All_Strategy_Grid_Rows()
    {
        if (_controller == null) return;
        var status = _controller.Get_Strategy_Status();
        foreach (DataGridViewRow row in grid_strategy_perf.Rows)
        {
            if (row.Tag is not Strategy_Type type) continue;
            bool active = status.TryGetValue(type, out bool en) && en;
            Refresh_Strategy_Grid_Row_Cells(row, active);
        }
    }

    private void Refresh_Strategy_Grid_Row(Strategy_Type type, bool active)
    {
        foreach (DataGridViewRow row in grid_strategy_perf.Rows)
        {
            if (row.Tag is not Strategy_Type t || t != type) continue;
            Refresh_Strategy_Grid_Row_Cells(row, active);
            break;
        }
    }

    private void Refresh_Strategy_Grid_Row_Cells(DataGridViewRow row, bool active)
    {
        row.Cells[5].Value           = active ? "ACTIVE"  : "DISABLED";
        row.Cells[5].Style.ForeColor = active ? COL_GREEN : COL_YELLOW;
        row.Cells[6].Value           = active ? "DISABLE" : "ENABLE";
        row.Cells[6].Style.ForeColor = active ? COL_RED   : COL_GREEN;
    }

    private void Populate_Symbol_Grid()
    {
        grid_symbols.Rows.Clear();
        if (_controller == null) return;

        foreach (var sym in _controller.Get_Symbol_Universe())
        {
            bool   active   = sym.Is_Enabled;
            string tier_txt = sym.Tier.ToString().Replace("Tier_", "Tier ");

            decimal base_forex = _risk_settings?.Forex_Risk_Per_Trade_Percent ?? 0.25m;
            decimal risk_pct   = sym.Is_Gold ? (_risk_settings?.Gold_Risk_Per_Trade_Percent ?? 0.20m)
                : sym.Tier switch
                {
                    Symbol_Tier_Type.Tier_2 => Math.Round(base_forex * 0.75m, 3),
                    Symbol_Tier_Type.Tier_3 => Math.Round(base_forex * 0.60m, 3),
                    _                       => base_forex
                };

            int row = grid_symbols.Rows.Add(
                sym.Symbol_Name, tier_txt, $"{sym.Normal_Spread_Pips:F1}p",
                $"{risk_pct:F2}%",
                active ? "ACTIVE" : "PAUSED",
                active ? "PAUSE"  : "ENABLE");

            var cells = grid_symbols.Rows[row].Cells;
            cells[4].Style.ForeColor = active ? COL_GREEN : TEXT_MUTED;
            cells[5].Style.ForeColor = active ? COL_YELLOW : COL_GREEN;
            cells[5].Style.BackColor = Color.FromArgb(30, 30, 55);
            grid_symbols.Rows[row].Tag = sym.Symbol_Name;
        }
    }

    private void Grid_Symbols_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex != 5 || _controller == null) return;

        var row = grid_symbols.Rows[e.RowIndex];
        if (row.Tag is not string symbol_name) return;

        var sym = _controller.Get_Symbol_Universe().FirstOrDefault(s => s.Symbol_Name == symbol_name);
        if (sym == null) return;

        bool now_enabled = !sym.Is_Enabled;
        _controller.Toggle_Symbol(symbol_name, now_enabled);

        row.Cells[4].Value           = now_enabled ? "ACTIVE"  : "PAUSED";
        row.Cells[4].Style.ForeColor = now_enabled ? COL_GREEN : TEXT_MUTED;
        row.Cells[5].Value           = now_enabled ? "PAUSE"   : "ENABLE";
        row.Cells[5].Style.ForeColor = now_enabled ? COL_YELLOW : COL_GREEN;
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

    private void On_Bridge_Connection_Changed(bool connected)
    {
        string ts = DateTime.UtcNow.ToString("HH:mm:ss");
        if (!connected)
        {
            txt_log.AppendText($"[{ts}] MT5 bridge disconnected — bot paused.\r\n");
            txt_log.ScrollToCaret();
            if (_controller?.Is_Running == true)
            {
                _controller.Stop();
                btn_start.Enabled = true;
                btn_stop.Enabled  = false;
            }
        }
        else
        {
            txt_log.AppendText($"[{ts}] MT5 bridge reconnected. Press START to resume.\r\n");
            txt_log.ScrollToCaret();
        }
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
        var now  = DateTime.UtcNow;
        var time = TimeOnly.FromTimeSpan(now.TimeOfDay);

        bool ldn     = time >= Trading_Constants.London_Open && time < Trading_Constants.London_Close;
        bool ny      = time >= Trading_Constants.NY_Open     && time < Trading_Constants.NY_Close;
        bool asian   = time >= Trading_Constants.Asian_Open  || time < Trading_Constants.Asian_Close;
        bool overlap = ldn && ny;

        var ldn_close = now.Date.Add(Trading_Constants.London_Close.ToTimeSpan());
        var ny_close  = now.Date.Add(Trading_Constants.NY_Close.ToTimeSpan());
        var ldn_open  = now.Date.Add(Trading_Constants.London_Open.ToTimeSpan());
        if (ldn_open <= now) ldn_open = ldn_open.AddDays(1);

        string countdown;
        if (overlap)
            countdown = $"Overlap — LDN closes {Format_Hm(ldn_close - now)}";
        else if (ldn)
            countdown = $"LDN closes {Format_Hm(ldn_close - now)}";
        else if (ny)
            countdown = $"NY closes {Format_Hm(ny_close - now)}";
        else
            countdown = $"LDN opens {Format_Hm(ldn_open - now)}";

        var names = new List<string>();
        if (asian) names.Add("ASIAN");
        if (ldn)   names.Add("LDN");
        if (ny)    names.Add("NY");

        if (names.Count == 0)
        {
            lbl_session.Text      = $"Session:  CLOSED  —  {countdown}";
            lbl_session.ForeColor = TEXT_MUTED;
        }
        else
        {
            lbl_session.Text      = $"Session:  ● {string.Join("  ● ", names)}  —  {countdown}";
            lbl_session.ForeColor = overlap ? COL_GREEN : COL_YELLOW;
        }
    }

    private static string Format_Hm(TimeSpan ts)
    {
        if (ts.TotalSeconds <= 0) return "now";
        if (ts.TotalHours >= 1)   return $"in {(int)ts.TotalHours}h {ts.Minutes:D2}m";
        return $"in {(int)ts.TotalMinutes}m";
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (WindowState == FormWindowState.Minimized)
        {
            Hide();
            _tray_icon?.ShowBalloonTip(2000, "ATLAS Running", "Bot continues in background — double-click tray icon to restore.", ToolTipIcon.Info);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // X button: minimize to tray unless Exit was chosen from tray menu
        if (!_tray_close && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            WindowState = FormWindowState.Minimized;
            return;
        }

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
                _tray_close = false;
                return;
            }

            _controller.Stop();
        }

        _tray_icon?.Dispose();
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
                    t.Close_Reason,
                    t.Notes);
                var grid_row = grid_trade_history.Rows[row];
                grid_row.Tag = t;  // full BO stored for detail view and notes
                Color row_col = t.Is_Winner ? COL_GREEN : COL_RED;
                grid_row.Cells[6].Style.ForeColor = row_col;
                grid_row.Cells[7].Style.ForeColor = row_col;
            }

            btn_view_detail.Enabled = grid_trade_history.Rows.Count > 0;
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

    private async void btn_history_filter_Click(object sender, EventArgs e)
    {
        if (_controller == null) return;
        btn_history_filter.Enabled = false;
        DateTime day_utc = dtp_history_date.Value.Date;
        lbl_history_status.Text      = $"Loading trades for {day_utc:yyyy-MM-dd}...";
        lbl_history_status.ForeColor = COL_YELLOW;
        try
        {
            var trades = await _controller.Get_Trades_By_Date_Async(day_utc);
            grid_trade_history.Rows.Clear();
            foreach (var t in trades)
            {
                int row = grid_trade_history.Rows.Add(
                    t.Closed_At_UTC.ToString("yyyy-MM-dd HH:mm"),
                    t.Symbol_Name, t.Strategy.ToString(), t.Direction.ToString(),
                    $"{t.Entry_Price:F5}", $"{t.Exit_Price:F5}",
                    $"{t.R_Multiple:+0.00;-0.00}R",
                    $"{t.Net_PnL_Currency:+$#,##0.00;-$#,##0.00}",
                    t.Close_Reason, t.Notes);
                var grid_row = grid_trade_history.Rows[row];
                grid_row.Tag = t;
                Color row_col = t.Is_Winner ? COL_GREEN : COL_RED;
                grid_row.Cells[6].Style.ForeColor = row_col;
                grid_row.Cells[7].Style.ForeColor = row_col;
            }
            btn_view_detail.Enabled = grid_trade_history.Rows.Count > 0;
            lbl_history_status.Text      = trades.Count > 0
                ? $"{trades.Count} trade(s) on {day_utc:yyyy-MM-dd}"
                : $"No trades on {day_utc:yyyy-MM-dd}";
            lbl_history_status.ForeColor = TEXT_SECONDARY;
        }
        catch (Exception ex)
        {
            lbl_history_status.Text      = $"DB error: {ex.Message}";
            lbl_history_status.ForeColor = COL_RED;
        }
        finally { btn_history_filter.Enabled = true; }
    }

    private async void btn_export_history_Click(object sender, EventArgs e)
    {
        if (_controller == null) return;

        btn_export_history.Enabled = false;
        lbl_history_status.Text    = "Loading all trades for export...";
        lbl_history_status.ForeColor = COL_YELLOW;
        try
        {
            var trades = await _controller.Get_All_Trades_Async();
            if (trades.Count == 0)
            {
                lbl_history_status.Text     = "No trades in database to export.";
                lbl_history_status.ForeColor = COL_RED;
                return;
            }

            using var dlg = new SaveFileDialog
            {
                Title            = "Export Trade History",
                Filter           = "CSV files (*.csv)|*.csv",
                FileName         = $"ATLAS_Trades_{DateTime.UtcNow:yyyyMMdd}.csv",
                DefaultExt       = "csv"
            };

            if (dlg.ShowDialog() != DialogResult.OK) return;

            string csv = Daily_Report_Service.To_Csv(trades);
            await System.IO.File.WriteAllTextAsync(dlg.FileName, csv);

            lbl_history_status.Text      = $"Exported {trades.Count} trades → {System.IO.Path.GetFileName(dlg.FileName)}";
            lbl_history_status.ForeColor = COL_GREEN;
        }
        catch (Exception ex)
        {
            lbl_history_status.Text      = $"Export failed: {ex.Message}";
            lbl_history_status.ForeColor = COL_RED;
        }
        finally
        {
            btn_export_history.Enabled = true;
        }
    }

    private async void Grid_Trade_History_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || _controller == null) return;
        var row = grid_trade_history.Rows[e.RowIndex];
        if (row.Tag is not Trade_Result_BO trade) return;

        string trade_info = $"{row.Cells[1].Value} {row.Cells[3].Value}  {row.Cells[0].Value} UTC  |  {row.Cells[7].Value}";
        string current_notes = row.Cells[9].Value?.ToString() ?? "";

        using var form = new Trade_Notes_Form(trade_info, current_notes);
        if (form.ShowDialog(this) != DialogResult.OK) return;

        await _controller.Update_Trade_Notes_Async(trade.Signal_Id, form.Notes);
        row.Cells[9].Value = form.Notes;
        lbl_history_status.Text      = "Note saved.";
        lbl_history_status.ForeColor = COL_GREEN;
    }

    private void btn_view_detail_Click(object sender, EventArgs e)
    {
        if (grid_trade_history.SelectedRows.Count == 0) return;
        var row = grid_trade_history.SelectedRows[0];
        if (row.Tag is not Trade_Result_BO trade) return;
        using var form = new Trade_Detail_Form(trade);
        form.ShowDialog(this);
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
                btn_bt_sweep.Enabled       = _bt_last_result.Trades.Count > 0;
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

    // ── Backtest Parameter Sweep ──────────────────────────────────────
    private async void btn_bt_sweep_Click(object sender, EventArgs e)
    {
        if (_bt_m15 == null || _bt_h1 == null || _bt_h4 == null || _bt_d1 == null) return;

        btn_bt_sweep.Enabled    = false;
        lbl_bt_status.Text      = "Running parameter sweep...";
        lbl_bt_status.ForeColor = COL_YELLOW;
        Application.DoEvents();

        int[] quality_thresholds = [70, 75, 80, 85, 90, 95];
        string symbol = cmb_bt_symbol.SelectedItem?.ToString() ?? "EURUSD";
        decimal initial_balance = decimal.TryParse(txt_bt_balance.Text, out var b) ? b : 10_000m;

        var sweep_results = await Task.Run(() =>
        {
            var results = new List<(int MinQ, int Trades, decimal WR, decimal PF, decimal AvgR, decimal NetR)>();
            foreach (int min_q in quality_thresholds)
            {
                var config = new Backtest_Config
                {
                    Symbol_Name     = symbol,
                    Start_Date      = dtp_bt_from.Value.ToUniversalTime(),
                    End_Date        = dtp_bt_to.Value.ToUniversalTime(),
                    Initial_Balance = initial_balance,
                    Risk_Settings   = new Atlas_Domain.BusinessObjects.Risk_Setting_BO
                        { Min_Quality_Score_Live = min_q }
                };
                var engine   = new Backtest_Engine();
                var result   = engine.Run_Async(config, new Backtest_Data_Provider(_bt_m15, _bt_h1, _bt_h4, _bt_d1), CancellationToken.None).GetAwaiter().GetResult();
                decimal net_r = result.Trades.Count > 0 ? result.Trades.Sum(t => t.R_Multiple) : 0;
                results.Add((min_q, result.Total_Trades, result.Win_Rate, result.Profit_Factor, result.Avg_R, Math.Round(net_r, 2)));
            }
            return results;
        });

        Safe_Invoke(() =>
        {
            grid_bt_sweep.Rows.Clear();
            decimal best_pf = sweep_results.Count > 0 ? sweep_results.Max(r => r.PF) : 0;
            foreach (var (min_q, trades, wr, pf, avg_r, net_r) in sweep_results)
            {
                int idx = grid_bt_sweep.Rows.Add(
                    $"Score ≥ {min_q}",
                    trades.ToString(),
                    $"{wr:F1}%",
                    $"{pf:F2}",
                    $"{avg_r:+0.000;-0.000}R",
                    $"{net_r:+0.0;-0.0}R");
                grid_bt_sweep.Rows[idx].Tag = min_q;
                if (pf > 0 && pf >= best_pf)
                    grid_bt_sweep.Rows[idx].DefaultCellStyle.BackColor = Color.FromArgb(18, 48, 24);
            }
            lbl_bt_status.Text      = $"Sweep complete — {sweep_results.Count} configs tested";
            lbl_bt_status.ForeColor = COL_GREEN;
            btn_bt_sweep.Enabled    = true;
        });
    }

    private void Grid_Bt_Sweep_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;
        var row = grid_bt_sweep.Rows[e.RowIndex];
        if (row.Tag is not int min_q) return;
        MessageBox.Show(
            $"To apply Min Quality Score = {min_q}:\n\nThis setting takes effect in the live bot's Risk_Setting_BO.Min_Quality_Score_Live.\n" +
            $"Restart the bot after saving settings to apply.",
            "Apply Sweep Result", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

    // ── Live Equity Curve Paint ───────────────────────────────────────
    private void Pnl_Live_Equity_Paint(object? sender, PaintEventArgs e)
    {
        var series = _live_equity_series;
        var g      = e.Graphics;
        int pw     = pnl_live_equity.Width  - 24;
        int ph     = pnl_live_equity.Height - 52;
        int ox     = 12;
        int oy     = pnl_live_equity.Height - 12;

        if (series == null || series.Count < 2)
        {
            using var hint = new SolidBrush(TEXT_MUTED);
            g.DrawString("No live trades recorded yet.", FONT_NORMAL, hint, ox + 8, 36);
            return;
        }

        decimal min_v = series.Min();
        decimal max_v = series.Max();
        if (min_v == max_v) max_v = min_v + 1;

        float X(int i)   => ox + (float)i / (series.Count - 1) * pw;
        float Y(decimal v) => oy - (float)((v - min_v) / (max_v - min_v)) * ph;

        // Baseline at zero
        float y_zero = Y(0);
        using var base_pen = new Pen(BORDER, 1) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
        if (y_zero >= oy - ph && y_zero <= oy)
            g.DrawLine(base_pen, ox, y_zero, ox + pw, y_zero);

        // Equity line
        bool in_profit = series[^1] >= 0;
        using var eq_pen = new Pen(in_profit ? COL_GREEN : COL_RED, 2);
        for (int i = 1; i < series.Count; i++)
            g.DrawLine(eq_pen, X(i - 1), Y(series[i - 1]), X(i), Y(series[i]));

        // Final PnL label
        string label = $"Net P&L: {series[^1]:+$0.00;-$0.00}  ({series.Count} trades)";
        using var lbl_brush = new SolidBrush(in_profit ? COL_GREEN : COL_RED);
        g.DrawString(label, FONT_BOLD, lbl_brush, ox + pw / 2 - 60, oy - ph - 2);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _controller?.Stop();
        _ui_refresh_timer?.Stop();
        _ui_refresh_timer?.Dispose();
        _daily_report_timer?.Stop();
        _daily_report_timer?.Dispose();
        _connection_status?.Dispose();
        _bt_cts?.Cancel();
        _log_writer?.WriteLine($"──── Session ended {DateTime.Now:yyyy-MM-dd HH:mm:ss} ────");
        _log_writer?.Dispose();
        base.OnFormClosed(e);
        FONT_LARGE.Dispose();
        FONT_MEDIUM.Dispose();
        FONT_NORMAL.Dispose();
        FONT_SMALL.Dispose();
        FONT_BOLD.Dispose();
        FONT_MONO.Dispose();
    }
}
