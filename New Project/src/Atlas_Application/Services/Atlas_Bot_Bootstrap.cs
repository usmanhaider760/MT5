using Atlas_Data_Access;
using Atlas_Data_Access.Repositories;
using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Interfaces;
using Atlas_Execution.MT5;
using Atlas_Execution.Services;
using Atlas_Market_Data.Services;
using Atlas_Risk.Services;
using Atlas_Strategy;


namespace Atlas_Application.Services;

/// <summary>
/// Wires up all services and returns a ready-to-use Bot_Controller.
/// Call once at application startup.
/// </summary>
public static class Atlas_Bot_Bootstrap
{
    public static (Bot_Controller Controller, Database_Schema Db, MT5_Account_Service Account_Service, MT5_Bridge_Client Bridge) Create(
        string mt5_host             = "127.0.0.1",
        int    mt5_port             = 9090,
        bool   demo_mode            = true,
        string db_path              = "atlas_trading.db",
        string telegram_token       = "",
        string telegram_chatid      = "",
        string email_smtp_host      = "",
        int    email_smtp_port      = 587,
        string email_username       = "",
        string email_password       = "",
        string email_to             = "",
        string news_feed_url        = "",
        Risk_Setting_BO? risk_override   = null,
        decimal spread_multiplier   = 1.0m,
        I_Ai_Signal_Filter? ai_filter = null)
    {
        // Database
        var db = new Database_Schema(db_path);
        db.Ensure_Created();

        // Repositories
        var signal_repo   = new Trade_Signal_Repository(db.Connection_String);
        var result_repo   = new Trade_Result_Repository(db.Connection_String);
        var account_repo  = new Account_Snapshot_Repository(db.Connection_String);
        var news_repo     = new News_Event_Repository(db.Connection_String);
        var position_repo = new Open_Position_Repository(db.Connection_String);
        var perf_repo     = new Strategy_Performance_Repository(db.Connection_String);

        // MT5 bridge
        var bridge        = new MT5_Bridge_Client(mt5_host, mt5_port);
        var market_data   = new MT5_Market_Data_Service(bridge);
        var execution     = new MT5_Execution_Service(bridge, demo_mode);
        var account_svc   = new MT5_Account_Service(bridge);

        // Risk settings — caller may override individual limits from config
        var risk_settings = risk_override ?? Risk_Setting_BO.Conservative_Launch();

        // Emergency stop
        var emergency_stop = new Emergency_Stop_Service();

        // Risk
        var risk_manager  = new Risk_Manager(risk_settings);
        var drawdown_guard = new Drawdown_Guard(risk_settings, emergency_stop);

        // Correlation guard
        var correlation_guard = new Atlas_Risk.Services.Correlation_Guard();

        // Quality scorer
        var scorer        = new Trade_Quality_Scorer();

        // News calendar — uses live feed URL when configured, demo events otherwise
        var calendar = new Economic_Calendar_Service(new System.Net.Http.HttpClient(), news_feed_url);

        // Market data
        var regime_detector    = new Market_Regime_Detector();
        var session_filter     = new Session_Filter_Service();
        var spread_filter      = new Spread_Filter_Service();
        var volatility_filter  = new Volatility_Filter_Service();
        var news_filter        = new News_Filter_Service(calendar);

        // Strategy
        var orchestrator = new Strategy_Orchestrator();

        // Performance monitor — replays full trade history from the DB to restore
        // equity curve, Sharpe/Sortino, and per-strategy stats after a restart
        var perf_monitor = new Performance_Monitor_Service(perf_repo);
        perf_monitor.Load_History_From_Db_Async(result_repo).GetAwaiter().GetResult();

        // Trade manager (breakeven at 1R, trailing SL at 1.5R+)
        var trade_manager = new Trade_Manager_Service(market_data, execution);

        // Pipeline
        var pipeline = new Trade_Pipeline_Service(
            market_data, regime_detector, session_filter, spread_filter,
            news_filter, volatility_filter, orchestrator, scorer,
            risk_manager, drawdown_guard, execution, emergency_stop, risk_settings,
            trade_manager, correlation_guard, account_svc, bridge, ai_filter);

        // Notifiers (each silently no-ops when not configured)
        var telegram = new Telegram_Notifier(telegram_token, telegram_chatid);
        var email    = new Email_Notifier(email_smtp_host, email_smtp_port, email_username,
                                          email_password, email_username, email_to);

        // Controller
        var controller = new Bot_Controller(
            pipeline, perf_monitor, orchestrator, emergency_stop,
            risk_settings, result_repo, execution, calendar, telegram, email, position_repo,
            spread_multiplier, account_repo, signal_repo, news_repo);

        return (controller, db, account_svc, bridge);
    }
}
