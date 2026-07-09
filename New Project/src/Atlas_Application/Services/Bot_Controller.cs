using Atlas_Data_Access.Repositories;
using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;
using Atlas_Domain.Interfaces;
using Atlas_Market_Data.Services;
using Atlas_Strategy;

namespace Atlas_Application.Services;

/// <summary>
/// Top-level bot lifecycle controller. UI calls only this.
/// </summary>
public class Bot_Controller
{
    private readonly Trade_Pipeline_Service _pipeline;
    private readonly Performance_Monitor_Service _perf_monitor;
    private readonly Strategy_Orchestrator _strategy_orchestrator;
    private readonly I_Emergency_Stop_Service _emergency_stop;
    private readonly Risk_Setting_BO _risk_settings;
    private readonly Trade_Result_Repository? _result_repo;
    private readonly I_Execution_Service? _execution;
    private readonly Economic_Calendar_Service? _calendar;
    private readonly Telegram_Notifier? _notifier;
    private readonly Email_Notifier? _email;
    private readonly Atlas_Data_Access.Repositories.Open_Position_Repository? _position_repo;
    private readonly Atlas_Data_Access.Repositories.Account_Snapshot_Repository? _account_repo;
    private readonly Atlas_Data_Access.Repositories.Trade_Signal_Repository? _signal_repo;
    private readonly Atlas_Data_Access.Repositories.News_Event_Repository? _news_repo;
    private readonly List<Market_Symbol_BO> _symbols;
    private readonly System_Log_Service _system_log = new();
    private DateTime _last_snapshot_at = DateTime.MinValue;

    private CancellationTokenSource? _cts;
    private Task? _bot_task;
    private int      _cycle_count    = 0;
    private DateTime _last_cycle_at  = DateTime.MinValue;
    private bool     _stale_alerted  = false;
    private readonly Dictionary<string, DateTime> _alert_cooldown = new();
    private const int Alert_Cooldown_Minutes = 5;

    public bool     Is_Running    { get; private set; }
    public int      Cycle_Count   => _cycle_count;
    public DateTime Last_Cycle_At => _last_cycle_at;
    public Bot_Mode_Type Current_Mode => _emergency_stop.Current_Mode;
    public Risk_Setting_BO Risk_Settings => _risk_settings;
    public List<Log_Entry_BO> Get_Recent_Logs(int count = 100) => _system_log.Get_Recent(count);

    public event Action<string>? On_Log;
    public event Action<bool>?   On_Running_Changed;
    public event Action<Trade_Signal_BO>? On_Signal_Approved;
    public event Action<Trade_Signal_BO>? On_Signal_Rejected;
    public event Action<string>? On_Emergency_Stop;
    public event Action<string, Market_Context_BO>? On_Regime_Updated;
    public event Action<List<Position_BO>>? On_Positions_Updated;
    public event Action<int>?    On_Cycle_Completed;
    public event Action<string>? On_Health_Alert;
    public event Action<Strategy_Type, bool>? On_Strategy_State_Changed;

    public Bot_Controller(
        Trade_Pipeline_Service pipeline,
        Performance_Monitor_Service perf_monitor,
        Strategy_Orchestrator strategy_orchestrator,
        I_Emergency_Stop_Service emergency_stop,
        Risk_Setting_BO risk_settings,
        Trade_Result_Repository? result_repo = null,
        I_Execution_Service? execution = null,
        Economic_Calendar_Service? calendar = null,
        Telegram_Notifier? notifier = null,
        Email_Notifier? email = null,
        Atlas_Data_Access.Repositories.Open_Position_Repository? position_repo = null,
        decimal spread_multiplier = 1.0m,
        Atlas_Data_Access.Repositories.Account_Snapshot_Repository? account_repo = null,
        Atlas_Data_Access.Repositories.Trade_Signal_Repository? signal_repo = null,
        Atlas_Data_Access.Repositories.News_Event_Repository? news_repo = null)
    {
        _pipeline              = pipeline;
        _perf_monitor          = perf_monitor;
        _strategy_orchestrator = strategy_orchestrator;
        _emergency_stop        = emergency_stop;
        _risk_settings         = risk_settings;
        _result_repo           = result_repo;
        _execution             = execution;
        _calendar              = calendar;
        _notifier              = notifier;
        _email                 = email;
        _position_repo         = position_repo;
        _account_repo          = account_repo;
        _signal_repo           = signal_repo;
        _news_repo             = news_repo;
        _symbols               = Market_Symbol_BO.Default_Universe();
        if (spread_multiplier != 1.0m)
            foreach (var s in _symbols)
                s.Max_Allowed_Spread_Pips = Math.Round(s.Max_Allowed_Spread_Pips * spread_multiplier, 2);

        // Wire up pipeline events
        _pipeline.On_Log              += msg        => Emit_Log(msg);
        _pipeline.On_Signal_Approved  += s =>
        {
            if (_signal_repo != null) _ = _signal_repo.Save_Signal_Async(s);
            On_Signal_Approved?.Invoke(s);
            string mode = _risk_settings.Mode == Atlas_Domain.Enums.Bot_Mode_Type.Demo ? "DEMO" : "LIVE";

            // Cooldown: suppress duplicate symbol alerts within 5 minutes
            bool in_cooldown = _alert_cooldown.TryGetValue(s.Symbol_Name, out var last_alert)
                && (DateTime.UtcNow - last_alert).TotalMinutes < Alert_Cooldown_Minutes;

            if (!in_cooldown)
            {
                _alert_cooldown[s.Symbol_Name] = DateTime.UtcNow;
                if (_notifier != null)
                    _ = _notifier.Send_Async(
                        $"<b>ATLAS SIGNAL</b>\n" +
                        $"Symbol: <code>{s.Symbol_Name}</code>  Dir: {s.Direction}\n" +
                        $"Strategy: {s.Strategy}  RR: {s.Reward_Risk_Ratio:F1}\n" +
                        $"Score: {s.Quality_Score}  Mode: {mode}");
                if (_email != null)
                    _ = _email.Send_Async(
                        $"ATLAS {mode}: {s.Symbol_Name} {s.Direction} [{s.Strategy}]",
                        $"Symbol:   {s.Symbol_Name}\nDirection: {s.Direction}\nStrategy: {s.Strategy}\n" +
                        $"R:R:      {s.Reward_Risk_Ratio:F1}\nScore:    {s.Quality_Score}\nMode:     {mode}\n" +
                        $"Time:     {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            }
        };
        _pipeline.On_Signal_Rejected  += s =>
        {
            if (_signal_repo != null) _ = _signal_repo.Save_Signal_Async(s);
            On_Signal_Rejected?.Invoke(s);
        };
        _pipeline.On_Regime_Updated   += (sym, ctx) => On_Regime_Updated?.Invoke(sym, ctx);
        _pipeline.On_Positions_Updated += pos =>
        {
            On_Positions_Updated?.Invoke(pos);
            if (_position_repo != null)
                _ = _position_repo.Save_All_Async(pos);
        };

        if (_emergency_stop is Atlas_Execution.Services.Emergency_Stop_Service ess)
        {
            ess.On_Emergency_Stop += reason =>
            {
                On_Emergency_Stop?.Invoke(reason);
                if (_notifier != null)
                    _ = _notifier.Send_Async($"<b>⚠ ATLAS EMERGENCY STOP</b>\n{reason}");
                if (_email != null)
                    _ = _email.Send_Async("ATLAS EMERGENCY STOP", reason);
                Stop();
            };
        }
    }

    /// <summary>Raises the raw string On_Log event (unchanged, for the existing UI log tab) and
    /// also records a leveled entry in System_Log_Service for structured/queryable access.</summary>
    private void Emit_Log(string message)
    {
        On_Log?.Invoke(message);
        _system_log.Log(Infer_Log_Level(message), message);
    }

    private static Log_Level_Type Infer_Log_Level(string message)
    {
        if (message.Contains("[ERROR]", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("exception", StringComparison.OrdinalIgnoreCase))
            return Log_Level_Type.Error;

        if (message.Contains("⚠", StringComparison.Ordinal) ||
            message.Contains("[AUTO-DISABLE]", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("STALE", StringComparison.OrdinalIgnoreCase))
            return Log_Level_Type.Warning;

        if (message.Contains("[DAILY REPORT]", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("position sync", StringComparison.OrdinalIgnoreCase))
            return Log_Level_Type.Trade;

        return Log_Level_Type.Info;
    }

    public Task<List<Position_BO>> Get_Last_Known_Positions_Async() =>
        _position_repo?.Get_All_Async() ?? Task.FromResult(new List<Position_BO>());

    public Task<List<Trade_Result_BO>> Get_All_Trades_Async() =>
        _result_repo?.Get_All_Results_Async() ?? Task.FromResult(new List<Trade_Result_BO>());

    public Task Update_Trade_Notes_Async(Guid signal_id, string notes) =>
        _result_repo?.Update_Notes_Async(signal_id, notes) ?? Task.CompletedTask;

    public Task<List<Trade_Result_BO>> Get_Trades_By_Date_Async(DateTime date_utc) =>
        _result_repo?.Get_By_Date_Async(date_utc) ?? Task.FromResult(new List<Trade_Result_BO>());

    public async Task Send_Daily_Summary_Async(DateTime date_utc)
    {
        var trades  = await Get_Trades_By_Date_Async(date_utc);
        string text = Daily_Report_Service.Generate(date_utc, trades);
        Emit_Log($"[DAILY REPORT] {date_utc:yyyy-MM-dd} — {trades.Count} trades");
        if (_notifier != null) await _notifier.Send_Async(text);
        if (_email    != null) await _email.Send_Async($"ATLAS Daily Report {date_utc:yyyy-MM-dd}", text);
    }

    public Task<List<Trade_Result_BO>> Get_Recent_Trades_Async(int count = 50) =>
        _result_repo?.Get_Recent_Async(count) ?? Task.FromResult(new List<Trade_Result_BO>());

    public (int Total, decimal Total_R, IReadOnlyDictionary<Strategy_Type, Strategy_Performance_BO> Strategies)
        Get_Live_Performance() =>
        (_perf_monitor.Total_Trades_Count, _perf_monitor.Total_R, _perf_monitor.Get_All_Strategy_Performance());

    public List<decimal> Get_Equity_Series()   => _perf_monitor.Get_Equity_Series();
    public decimal       Get_Sharpe_Ratio()    => _perf_monitor.Calculate_Sharpe_Ratio();
    public decimal       Get_Sortino_Ratio()   => _perf_monitor.Calculate_Sortino_Ratio();

    public async Task Sync_Live_Positions_Async()
    {
        if (_execution == null) return;
        try
        {
            var positions = await _execution.Get_Open_Positions_Async();
            On_Positions_Updated?.Invoke(positions);
            if (_position_repo != null && positions.Count > 0)
                await _position_repo.Save_All_Async(positions);
            Emit_Log($"[{DateTime.UtcNow:HH:mm:ss}] Position sync: {positions.Count} open position(s) found.");
        }
        catch (Exception ex)
        {
            Emit_Log($"[{DateTime.UtcNow:HH:mm:ss}] Position sync failed: {ex.Message}");
        }
    }

    public Task<List<(DateTime UTC, decimal Equity, decimal Drawdown)>> Get_Equity_Curve_Async(int days = 90) =>
        _account_repo?.Get_Equity_Curve_Async(days) ?? Task.FromResult(new List<(DateTime, decimal, decimal)>());

    public Task<decimal> Get_Peak_Equity_From_Db_Async() =>
        _account_repo?.Get_Peak_Equity_Async() ?? Task.FromResult(0m);

    public Task<List<Trade_Signal_BO>> Get_Recent_Signals_Async(int limit = 100) =>
        _signal_repo?.Get_Recent_Signals_Async(limit) ?? Task.FromResult(new List<Trade_Signal_BO>());

    public async Task Save_Account_Snapshot_Async(Account_State_BO account)
    {
        if (_account_repo == null || account.Balance == 0) return;
        if ((DateTime.UtcNow - _last_snapshot_at).TotalMinutes < 5) return;
        try
        {
            await _account_repo.Save_Snapshot_Async(account);
            _last_snapshot_at = DateTime.UtcNow;
        }
        catch { /* non-critical */ }
    }

    public async Task<bool> Close_Position_Async(long ticket, string reason = "Manual close via UI")
    {
        bool ok = _execution != null && await _execution.Close_Position_Async(ticket, reason);
        if (ok && _position_repo != null)
            await _position_repo.Delete_By_Ticket_Async(ticket);
        return ok;
    }

    public async Task<List<News_Event_BO>> Get_Upcoming_News_Async(int hours = 24)
    {
        var events = _calendar != null
            ? await _calendar.Get_High_Impact_Events_Async(hours)
            : new List<News_Event_BO>();

        if (events.Count > 0 && _news_repo != null)
            _ = _news_repo.Save_Events_Async(events);

        // Fall back to DB cache when live feed has no data (offline / no URL configured)
        if (events.Count == 0 && _news_repo != null)
            events = await _news_repo.Get_Upcoming_High_Impact_Async(hours);

        return events;
    }

    public void Start(int cycle_interval_seconds = 60)
    {
        if (Is_Running) return;
        Is_Running     = true;
        _cycle_count   = 0;
        _last_cycle_at = DateTime.UtcNow;
        _stale_alerted = false;
        On_Running_Changed?.Invoke(true);
        _cts = new CancellationTokenSource();
        _bot_task = Run_Loop_Async(_cts.Token, cycle_interval_seconds);
    }

    public void Stop()
    {
        _cts?.Cancel();
        Is_Running = false;
        On_Running_Changed?.Invoke(false);
    }

    public void Trigger_Emergency_Stop(string reason)
    {
        _ = _emergency_stop.Activate_Emergency_Stop_Async(reason);
        Stop();
    }

    public void Enable_Strategy(Strategy_Type type)
    {
        _strategy_orchestrator.Enable_Strategy(type);
        On_Strategy_State_Changed?.Invoke(type, true);
    }

    public void Disable_Strategy(Strategy_Type type)
    {
        _strategy_orchestrator.Disable_Strategy(type);
        On_Strategy_State_Changed?.Invoke(type, false);
    }
    public IReadOnlyDictionary<Strategy_Type, bool> Get_Strategy_Status() => _strategy_orchestrator.Get_Strategy_Status();
    public (decimal WR, decimal AvgR, decimal PF, decimal MaxDD) Get_Overall_Stats() => _perf_monitor.Get_Overall_Stats();

    public IReadOnlyList<Market_Symbol_BO> Get_Symbol_Universe() => _symbols.AsReadOnly();

    public void Toggle_Symbol(string symbol_name, bool enabled)
    {
        var sym = _symbols.FirstOrDefault(s => s.Symbol_Name == symbol_name);
        if (sym == null) return;
        sym.Is_Enabled = enabled;
        Emit_Log($"[{DateTime.UtcNow:HH:mm:ss}] Symbol {symbol_name} {(enabled ? "ENABLED" : "DISABLED")} by operator.");
    }

    private async Task Run_Loop_Async(CancellationToken ct, int interval_seconds)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await _pipeline.Run_Cycle_Async(_symbols);
                Check_Strategy_Health();
                _cycle_count++;
                _last_cycle_at = DateTime.UtcNow;
                _stale_alerted = false;
                On_Cycle_Completed?.Invoke(_cycle_count);
            }
            catch (Exception ex)
            {
                Emit_Log($"[ERROR] Pipeline cycle exception: {ex.Message}");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(interval_seconds), ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
        }
    }

    // Called by the Dashboard's 5-second UI timer to detect stuck cycles
    public void Check_Health(int cycle_interval_seconds)
    {
        if (!Is_Running || _last_cycle_at == DateTime.MinValue) return;

        double stale_threshold = cycle_interval_seconds * 2.5;
        double elapsed = (DateTime.UtcNow - _last_cycle_at).TotalSeconds;

        if (elapsed >= stale_threshold && !_stale_alerted)
        {
            _stale_alerted = true;
            string msg = $"Bot cycle stale — last cycle {(int)elapsed}s ago (threshold: {(int)stale_threshold}s). MT5 bridge may be down.";
            On_Health_Alert?.Invoke(msg);
            if (_notifier != null) _ = _notifier.Send_Async($"⚠ ATLAS WATCHDOG\n{msg}");
            if (_email != null)    _ = _email.Send_Async("ATLAS Watchdog Alert", msg);
        }
    }

    private void Check_Strategy_Health()
    {
        var to_disable = _perf_monitor.Get_Strategies_To_Disable();
        foreach (var s in to_disable)
        {
            _strategy_orchestrator.Disable_Strategy(s);
            On_Strategy_State_Changed?.Invoke(s, false);
            string msg = $"[AUTO-DISABLE] {s} disabled — rolling performance deteriorated.";
            Emit_Log(msg);

            string alert = $"⚠ ATLAS AUTO-DISABLE\nStrategy: {s}\n{msg}\n\nReview performance before re-enabling.";
            if (_notifier != null) _ = _notifier.Send_Async(alert);
            if (_email    != null) _ = _email.Send_Async($"ATLAS Auto-Disable: {s}", alert);
        }
    }
}
