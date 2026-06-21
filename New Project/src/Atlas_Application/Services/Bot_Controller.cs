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
    private readonly List<Market_Symbol_BO> _symbols;

    private CancellationTokenSource? _cts;
    private Task? _bot_task;
    private int _cycle_count = 0;

    public bool Is_Running { get; private set; }
    public int  Cycle_Count => _cycle_count;
    public Bot_Mode_Type Current_Mode => _emergency_stop.Current_Mode;
    public Risk_Setting_BO Risk_Settings => _risk_settings;

    public event Action<string>? On_Log;
    public event Action<bool>? On_Running_Changed;
    public event Action<Trade_Signal_BO>? On_Signal_Approved;
    public event Action<Trade_Signal_BO>? On_Signal_Rejected;
    public event Action<string>? On_Emergency_Stop;
    public event Action<string, Market_Context_BO>? On_Regime_Updated;
    public event Action<List<Position_BO>>? On_Positions_Updated;
    public event Action<int>? On_Cycle_Completed;

    public Bot_Controller(
        Trade_Pipeline_Service pipeline,
        Performance_Monitor_Service perf_monitor,
        Strategy_Orchestrator strategy_orchestrator,
        I_Emergency_Stop_Service emergency_stop,
        Risk_Setting_BO risk_settings,
        Trade_Result_Repository? result_repo = null,
        I_Execution_Service? execution = null,
        Economic_Calendar_Service? calendar = null)
    {
        _pipeline              = pipeline;
        _perf_monitor          = perf_monitor;
        _strategy_orchestrator = strategy_orchestrator;
        _emergency_stop        = emergency_stop;
        _risk_settings         = risk_settings;
        _result_repo           = result_repo;
        _execution             = execution;
        _calendar              = calendar;
        _symbols               = Market_Symbol_BO.Default_Universe();

        // Wire up pipeline events
        _pipeline.On_Log              += msg        => On_Log?.Invoke(msg);
        _pipeline.On_Signal_Approved  += s          => On_Signal_Approved?.Invoke(s);
        _pipeline.On_Signal_Rejected  += s          => On_Signal_Rejected?.Invoke(s);
        _pipeline.On_Regime_Updated   += (sym, ctx) => On_Regime_Updated?.Invoke(sym, ctx);
        _pipeline.On_Positions_Updated += pos       => On_Positions_Updated?.Invoke(pos);

        if (_emergency_stop is Atlas_Execution.Services.Emergency_Stop_Service ess)
        {
            ess.On_Emergency_Stop += reason => { On_Emergency_Stop?.Invoke(reason); Stop(); };
        }
    }

    public Task<List<Trade_Result_BO>> Get_Recent_Trades_Async(int count = 50) =>
        _result_repo?.Get_Recent_Async(count) ?? Task.FromResult(new List<Trade_Result_BO>());

    public (int Total, decimal Total_R, IReadOnlyDictionary<Strategy_Type, Strategy_Performance_BO> Strategies)
        Get_Live_Performance() =>
        (_perf_monitor.Total_Trades_Count, _perf_monitor.Total_R, _perf_monitor.Get_All_Strategy_Performance());

    public async Task<bool> Close_Position_Async(long ticket, string reason = "Manual close via UI") =>
        _execution != null && await _execution.Close_Position_Async(ticket, reason);

    public async Task<List<News_Event_BO>> Get_Upcoming_News_Async(int hours = 24) =>
        _calendar != null
            ? await _calendar.Get_High_Impact_Events_Async(hours)
            : [];

    public void Start(int cycle_interval_seconds = 60)
    {
        if (Is_Running) return;
        Is_Running = true;
        _cycle_count = 0;
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

    public void Enable_Strategy(Strategy_Type type) => _strategy_orchestrator.Enable_Strategy(type);
    public void Disable_Strategy(Strategy_Type type) => _strategy_orchestrator.Disable_Strategy(type);
    public IReadOnlyDictionary<Strategy_Type, bool> Get_Strategy_Status() => _strategy_orchestrator.Get_Strategy_Status();
    public (decimal WR, decimal AvgR, decimal PF, decimal MaxDD) Get_Overall_Stats() => _perf_monitor.Get_Overall_Stats();

    public IReadOnlyList<Market_Symbol_BO> Get_Symbol_Universe() => _symbols.AsReadOnly();

    public void Toggle_Symbol(string symbol_name, bool enabled)
    {
        var sym = _symbols.FirstOrDefault(s => s.Symbol_Name == symbol_name);
        if (sym == null) return;
        sym.Is_Enabled = enabled;
        On_Log?.Invoke($"[{DateTime.UtcNow:HH:mm:ss}] Symbol {symbol_name} {(enabled ? "ENABLED" : "DISABLED")} by operator.");
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
                On_Cycle_Completed?.Invoke(_cycle_count);
            }
            catch (Exception ex)
            {
                On_Log?.Invoke($"[ERROR] Pipeline cycle exception: {ex.Message}");
            }
            await Task.Delay(TimeSpan.FromSeconds(interval_seconds), ct).ConfigureAwait(false);
        }
    }

    private void Check_Strategy_Health()
    {
        var to_disable = _perf_monitor.Get_Strategies_To_Disable();
        foreach (var s in to_disable)
        {
            _strategy_orchestrator.Disable_Strategy(s);
            On_Log?.Invoke($"[AUTO-DISABLE] {s} disabled — rolling performance deteriorated.");
        }
    }
}
