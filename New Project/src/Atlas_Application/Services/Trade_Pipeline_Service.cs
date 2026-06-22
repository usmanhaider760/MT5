using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;
using Atlas_Domain.Interfaces;
using Atlas_Market_Data.Services;
using Atlas_Risk.Services;
using Atlas_Strategy;

namespace Atlas_Application.Services;

/// <summary>
/// Implements the complete 22-step trading process flow from the methodology document.
/// UI never calls MT5 directly — it goes through this service.
/// </summary>
public class Trade_Pipeline_Service
{
    private readonly I_Market_Data_Service _market_data;
    private readonly Market_Regime_Detector _regime_detector;
    private readonly Session_Filter_Service _session_filter;
    private readonly Spread_Filter_Service _spread_filter;
    private readonly News_Filter_Service _news_filter;
    private readonly Volatility_Filter_Service _volatility_filter;
    private readonly Strategy_Orchestrator _strategy_orchestrator;
    private readonly Trade_Quality_Scorer _quality_scorer;
    private readonly Risk_Manager _risk_manager;
    private readonly Drawdown_Guard _drawdown_guard;
    private readonly I_Execution_Service _execution;
    private readonly I_Emergency_Stop_Service _emergency_stop;
    private readonly Risk_Setting_BO _risk_settings;
    private readonly Trade_Manager_Service? _trade_manager;
    private readonly Correlation_Guard? _correlation_guard;

    public event Action<string>? On_Log;
    public event Action<Trade_Signal_BO>? On_Signal_Approved;
    public event Action<Trade_Signal_BO>? On_Signal_Rejected;
    public event Action<string, Market_Context_BO>? On_Regime_Updated;
    public event Action<List<Position_BO>>? On_Positions_Updated;

    public Trade_Pipeline_Service(
        I_Market_Data_Service market_data,
        Market_Regime_Detector regime_detector,
        Session_Filter_Service session_filter,
        Spread_Filter_Service spread_filter,
        News_Filter_Service news_filter,
        Volatility_Filter_Service volatility_filter,
        Strategy_Orchestrator strategy_orchestrator,
        Trade_Quality_Scorer quality_scorer,
        Risk_Manager risk_manager,
        Drawdown_Guard drawdown_guard,
        I_Execution_Service execution,
        I_Emergency_Stop_Service emergency_stop,
        Risk_Setting_BO risk_settings,
        Trade_Manager_Service? trade_manager = null,
        Correlation_Guard? correlation_guard = null)
    {
        _market_data = market_data;
        _regime_detector = regime_detector;
        _session_filter = session_filter;
        _spread_filter = spread_filter;
        _news_filter = news_filter;
        _volatility_filter = volatility_filter;
        _strategy_orchestrator = strategy_orchestrator;
        _quality_scorer = quality_scorer;
        _risk_manager = risk_manager;
        _drawdown_guard = drawdown_guard;
        _execution = execution;
        _emergency_stop = emergency_stop;
        _risk_settings = risk_settings;
        _trade_manager     = trade_manager;
        _correlation_guard = correlation_guard;
        if (_trade_manager != null)
            _trade_manager.On_Log += msg => On_Log?.Invoke(msg);
    }

    public async Task Run_Cycle_Async(List<Market_Symbol_BO> symbols)
    {
        if (_emergency_stop.Kill_Switch_Active)
        {
            Log("Kill switch active — pipeline blocked.");
            return;
        }

        // Step 1: Account state + drawdown check
        var account = await _risk_manager.Get_Account_State_Async();
        await _drawdown_guard.Evaluate_And_Apply_Async(account);

        if (_emergency_stop.Kill_Switch_Active) return;

        var open_positions = await _execution.Get_Open_Positions_Async();
        On_Positions_Updated?.Invoke(open_positions);

        if (_trade_manager != null && open_positions.Count > 0)
            await _trade_manager.Manage_Open_Positions_Async(open_positions);

        var now_utc = DateTime.UtcNow;

        // Step 2: Session context
        var session = _session_filter.Get_Current_Session(now_utc);
        if (!_session_filter.Is_Trading_Session_Active(session))
        {
            Log($"Session {session} — not active, skipping cycle.");
            return;
        }

        // Step 3–22 per symbol
        foreach (var symbol in symbols.Where(s => s.Is_Enabled))
        {
            await Evaluate_Symbol_Async(symbol, account, open_positions, session, now_utc);
        }
    }

    private async Task Evaluate_Symbol_Async(
        Market_Symbol_BO symbol,
        Account_State_BO account,
        List<Position_BO> open_positions,
        Session_Type session,
        DateTime now_utc)
    {
        bool is_gold = symbol.Is_Gold;

        // Gate 1: Market open + spread
        if (!await _market_data.Is_Market_Open_Async(symbol.Symbol_Name)) { Log($"{symbol.Symbol_Name}: market closed"); return; }
        var spread = await _market_data.Get_Current_Spread_Pips_Async(symbol.Symbol_Name);
        if (!_spread_filter.Is_Spread_Acceptable(spread, symbol)) { Log($"{symbol.Symbol_Name}: spread {spread:F1} too high"); return; }
        if (_spread_filter.Is_Rollover_Period(now_utc)) { Log($"{symbol.Symbol_Name}: rollover period, skipping"); return; }
        if (_spread_filter.Is_Weekend_Open_Risk(now_utc)) { Log($"{symbol.Symbol_Name}: Monday gap risk, skipping"); return; }

        // Gate 2: News lockout
        bool news_block = await _news_filter.Is_News_Lockout_Active_Async(symbol.Symbol_Name, is_gold);
        if (news_block) { Log($"{symbol.Symbol_Name}: news lockout active"); return; }

        // Gate 3: Load candles
        var d1  = await _market_data.Get_Candles_Async(symbol.Symbol_Name, "D1",  50);
        var h4  = await _market_data.Get_Candles_Async(symbol.Symbol_Name, "H4",  200);
        var h1  = await _market_data.Get_Candles_Async(symbol.Symbol_Name, "H1",  100);
        var m15 = await _market_data.Get_Candles_Async(symbol.Symbol_Name, "M15", 50);

        // Gate 4: Volatility
        decimal rolling_atr = _volatility_filter.Calculate_Rolling_Average_ATR(h4);
        var (vol_ok, vol_reason) = _volatility_filter.Check_Volatility(h4, rolling_atr);
        if (!vol_ok) { Log($"{symbol.Symbol_Name}: {vol_reason}"); return; }

        // Gate 5: Regime detection
        var context = await _regime_detector.Detect_Market_Regime_Async(symbol.Symbol_Name, d1, h4, h1);
        context.Current_Session = session;
        context.Current_Spread_Pips = spread;
        context.Spread_Is_Normal = spread <= symbol.Normal_Spread_Pips * 1.5m;
        context.News_Lockout_Active = news_block;
        context.Score_Session_Quality = _session_filter.Get_Session_Quality_Score(session);
        context.Score_Spread_Quality = _spread_filter.Get_Spread_Quality_Score(spread, symbol);
        context.Score_News_Safety        = news_block ? 0 : 10;
        context.Score_Correlation_Safety = _correlation_guard?.Get_Correlation_Score(
            symbol.Symbol_Name, open_positions) ?? 10;

        // Recompute total score with real session/spread/news/correlation values
        context.Regime_Quality_Score =
            context.Score_Trend_Alignment + context.Score_Volatility_Quality +
            context.Score_Session_Quality + context.Score_Spread_Quality +
            context.Score_Structure_Clarity + context.Score_News_Safety +
            context.Score_Correlation_Safety;

        // Session-MTF confluence bonus (+5): all TFs aligned during peak session
        if (context.Higher_Timeframes_Aligned &&
            (session == Session_Type.London_NY_Overlap || session == Session_Type.London))
            context.Regime_Quality_Score += 5;

        context.Regime_Quality_Score = Math.Min(100, context.Regime_Quality_Score);

        On_Regime_Updated?.Invoke(symbol.Symbol_Name, context);

        // Gate 5b: Block Gold when drawdown is in recovery mode
        if (is_gold && !_drawdown_guard.Is_Gold_Allowed(account.Drawdown_From_Peak))
        {
            Log($"{symbol.Symbol_Name}: Gold blocked — drawdown {account.Drawdown_From_Peak:F2}% in recovery mode");
            return;
        }

        bool tradeable = is_gold ? context.Is_Gold_Tradeable : context.Is_Tradeable;
        if (!tradeable)
        {
            Log($"{symbol.Symbol_Name}: regime score {context.Regime_Quality_Score} below threshold (regime: {context.Regime})");
            return;
        }

        // Gate 6: Strategy signals
        var signals = await _strategy_orchestrator.Generate_All_Signals_Async(
            symbol.Symbol_Name, context, h4, h1, m15);

        if (signals.Count == 0) { Log($"{symbol.Symbol_Name}: no setup found"); return; }

        // Gate 7: Score and filter signals
        foreach (var signal in signals)
        {
            // Hard gate: block same-direction trades on correlated pairs
            if (_correlation_guard?.Is_Direction_Correlated(signal.Symbol_Name, signal.Direction, open_positions) == true)
            {
                signal.Is_Approved = false;
                signal.Reject_Reason = Signal_Reject_Reason.Correlation_Exposure_Exceeded;
                signal.Reject_Detail = $"Open position in correlated pair — {signal.Direction} exposure already active";
                On_Signal_Rejected?.Invoke(signal);
                Log($"{signal.Symbol_Name} [{signal.Strategy}]: blocked — correlated {signal.Direction} exposure already open");
                continue;
            }

            var (score, grade) = _quality_scorer.Score_Trade(signal, context);
            signal.Quality_Score = score;
            signal.Quality_Grade = grade;

            int min_score = is_gold ? _risk_settings.Min_Quality_Score_Gold_Live : _risk_settings.Min_Quality_Score_Live;
            if (score < min_score)
            {
                signal.Is_Approved = false;
                signal.Reject_Reason = Signal_Reject_Reason.Low_Quality_Score;
                signal.Reject_Detail = $"Score {score} < minimum {min_score}";
                On_Signal_Rejected?.Invoke(signal);
                Log($"{symbol.Symbol_Name} [{signal.Strategy}]: rejected — score {score}");
                continue;
            }

            // Gate 8: Risk validation
            var (risk_ok, risk_reason) = await _risk_manager.Validate_Trade_Risk_Async(signal, account, open_positions);
            if (!risk_ok)
            {
                signal.Is_Approved = false;
                signal.Reject_Reason = Signal_Reject_Reason.Daily_Loss_Limit_Reached;
                signal.Reject_Detail = risk_reason;
                On_Signal_Rejected?.Invoke(signal);
                Log($"{symbol.Symbol_Name} [{signal.Strategy}]: risk blocked — {risk_reason}");
                continue;
            }

            // All gates passed — calculate lot size (risk% scaled by symbol tier for Forex)
            decimal risk_pct = is_gold ? _risk_settings.Gold_Risk_Per_Trade_Percent
                : symbol.Tier switch
                {
                    Symbol_Tier_Type.Tier_2 => Math.Round(_risk_settings.Forex_Risk_Per_Trade_Percent * 0.75m, 4),
                    Symbol_Tier_Type.Tier_3 => Math.Round(_risk_settings.Forex_Risk_Per_Trade_Percent * 0.60m, 4),
                    _                       => _risk_settings.Forex_Risk_Per_Trade_Percent
                };

            // Scale risk down in caution/recovery mode
            decimal dd_mult = _drawdown_guard.Get_Risk_Multiplier(account.Drawdown_From_Peak);
            if (dd_mult < 1.0m)
            {
                risk_pct = Math.Round(risk_pct * dd_mult, 4);
                Log($"{symbol.Symbol_Name}: drawdown {account.Drawdown_From_Peak:F2}% — risk scaled to {risk_pct:F4}% (×{dd_mult})");
            }
            decimal lot = _risk_manager.Calculate_Lot_Size(account.Equity, risk_pct, signal.Stop_Loss_Pips, symbol.Pip_Value_Per_Lot);

            if (lot < symbol.Min_Lot_Size)
            {
                signal.Reject_Detail = $"Calculated lot {lot:F2} below broker minimum {symbol.Min_Lot_Size}";
                On_Signal_Rejected?.Invoke(signal);
                Log($"{symbol.Symbol_Name}: lot too small ({lot:F2}), skipping");
                continue;
            }

            signal.Is_Approved = true;
            On_Signal_Approved?.Invoke(signal);

            var (sent, ticket, broker_msg) = await _execution.Send_Order_Async(signal, lot);
            Log($"{symbol.Symbol_Name} [{signal.Strategy}]: order sent — ticket={ticket}, msg={broker_msg}");
        }
    }

    private void Log(string message) => On_Log?.Invoke($"[{DateTime.UtcNow:HH:mm:ss}] {message}");
}
