using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;
using Atlas_Market_Data.Services;
using Atlas_Risk.Services;
using Atlas_Strategy;

namespace Atlas_Application.Backtest;

/// <summary>
/// Replays historical candles bar-by-bar through the full regime → filter → strategy → risk
/// pipeline. No real orders are sent. All trade simulation happens here.
/// </summary>
public class Backtest_Engine
{
    public event Action<string>? On_Progress;
    public event Action<Trade_Result_BO>? On_Trade_Closed;

    public async Task<Backtest_Result> Run_Async(
        Backtest_Config config,
        Backtest_Data_Provider data,
        CancellationToken ct = default)
    {
        var result = new Backtest_Result
        {
            Symbol_Name = config.Symbol_Name,
            Run_Start   = DateTime.UtcNow
        };

        // Services (stateless, created fresh per backtest run)
        var regime_detector   = new Market_Regime_Detector();
        var session_filter    = new Session_Filter_Service();
        var spread_filter     = new Spread_Filter_Service();
        var volatility_filter = new Volatility_Filter_Service();
        var news_filter       = new News_Filter_Service();
        var quality_scorer    = new Trade_Quality_Scorer();
        var risk_manager      = new Risk_Manager(config.Risk_Settings);
        var orchestrator      = new Strategy_Orchestrator();

        // Enable only configured strategies
        foreach (Strategy_Type st in Enum.GetValues<Strategy_Type>())
            orchestrator.Disable_Strategy(st);
        foreach (var st in config.Enabled_Strategies)
            orchestrator.Enable_Strategy(st);

        // Simulation state
        decimal equity           = config.Initial_Balance;
        decimal peak_equity      = equity;
        decimal day_open_balance = equity;
        var open_positions       = new List<Simulated_Position>();
        int bar_count            = 0;

        result.Equity_Curve.Add((config.Start_Date, equity));

        var bars = data.Replay_M15_Bars(config.Start_Date, config.End_Date).ToList();
        int total_bars = bars.Count;

        foreach (var bar in bars)
        {
            if (ct.IsCancellationRequested) break;

            bar_count++;
            if (bar_count % 500 == 0)
                On_Progress?.Invoke($"Processing bar {bar_count}/{total_bars}  |  Equity: {equity:F2}  |  Trades: {result.Total_Trades}");

            // Manage open positions against this bar
            var closed = Manage_Open_Positions(open_positions, bar, config, result);
            foreach (var trade in closed)
            {
                equity += trade.Net_PnL_Currency;
                result.Trades.Add(trade);
                result.Total_Trades++;
                if (trade.Is_Winner) result.Winning_Trades++; else result.Losing_Trades++;
                result.Gross_Profit_R += trade.R_Multiple > 0 ? trade.R_Multiple : 0;
                result.Gross_Loss_R   += trade.R_Multiple < 0 ? trade.R_Multiple : 0;
                result.Total_Commission += trade.Commission;
                result.Total_Slippage_Pips += trade.Slippage_Pips;
                On_Trade_Closed?.Invoke(trade);
            }

            if (equity > peak_equity) peak_equity = equity;
            decimal dd = peak_equity > 0 ? (peak_equity - equity) / peak_equity * 100m : 0;
            if (dd > result.Max_Drawdown_Percent) result.Max_Drawdown_Percent = dd;

            // Daily reset
            if (bar.Open_Time_UTC.Date != bar.Open_Time_UTC.AddMinutes(-15).Date)
            {
                day_open_balance = equity;
                result.Equity_Curve.Add((bar.Open_Time_UTC, equity));
            }

            // Generate new signals on this bar
            if (open_positions.Count >= config.Risk_Settings.Max_Open_Trades) continue;

            var (d1, h4, h1, m15) = data.Get_Visible_Candles(bar.Open_Time_UTC);
            if (h4.Count < 50 || h1.Count < 20 || m15.Count < 10) continue;

            // Regime
            var context = await regime_detector.Detect_Market_Regime_Async(config.Symbol_Name, d1, h4, h1);
            context.Current_Session      = session_filter.Get_Current_Session(bar.Open_Time_UTC);
            context.Current_Spread_Pips  = config.Spread_Pips;
            context.Spread_Is_Normal     = true;
            context.News_Lockout_Active  = false;
            context.Score_Session_Quality = session_filter.Get_Session_Quality_Score(context.Current_Session);
            context.Score_Spread_Quality  = 12;
            context.Score_News_Safety     = 10;
            context.Regime_Quality_Score  =
                context.Score_Trend_Alignment + context.Score_Volatility_Quality +
                context.Score_Session_Quality + context.Score_Spread_Quality +
                context.Score_Structure_Clarity + context.Score_News_Safety +
                context.Score_Correlation_Safety;

            if (context.Regime == Market_Regime_Type.Abnormal_No_Trade) { result.Filters_Blocked++; continue; }
            if (!context.Is_Tradeable) { result.Filters_Blocked++; continue; }

            // Strategy signals
            var signals = await orchestrator.Generate_All_Signals_Async(config.Symbol_Name, context, h4, h1, m15);
            foreach (var signal in signals)
            {
                // Quality check
                var (score, grade) = quality_scorer.Score_Trade(signal, context);
                signal.Quality_Score = score;
                signal.Quality_Grade = grade;
                if (score < config.Risk_Settings.Min_Quality_Score_Live) { result.Signals_Rejected++; continue; }

                // Risk check
                var account = Build_Account_State(equity, day_open_balance, open_positions.Count, config);
                var positions_bo = open_positions.Select(p => p.As_Position_BO()).ToList();
                var symbol = Market_Symbol_BO.Default_Universe().First(s => s.Symbol_Name == config.Symbol_Name);
                var (risk_ok, _, _) = await risk_manager.Validate_Trade_Risk_Async(signal, account, positions_bo);
                if (!risk_ok) { result.Signals_Rejected++; continue; }

                // Lot size
                decimal lot = risk_manager.Calculate_Lot_Size(
                    equity, config.Risk_Settings.Forex_Risk_Per_Trade_Percent,
                    signal.Stop_Loss_Pips, symbol.Pip_Value_Per_Lot, symbol.Lot_Step);
                if (lot < symbol.Min_Lot_Size) continue;

                open_positions.Add(new Simulated_Position(signal, lot, bar, config, symbol.Pip_Value_Per_Lot));
            }
        }

        // Close any remaining open positions at last bar price
        if (bars.Count > 0)
        {
            var last_bar = bars[^1];
            foreach (var pos in open_positions.ToList())
            {
                var trade = pos.Force_Close(last_bar.Close, "Backtest end — position force-closed", config);
                equity += trade.Net_PnL_Currency;
                result.Trades.Add(trade);
                result.Total_Trades++;
                if (trade.Is_Winner) result.Winning_Trades++; else result.Losing_Trades++;
            }
        }

        result.Final_Equity = equity;
        result.Total_Return_Percent = Math.Round((equity - config.Initial_Balance) / config.Initial_Balance * 100m, 2);
        result.Max_Drawdown_Percent = Math.Round(result.Max_Drawdown_Percent, 2);
        result.Equity_Curve.Add((config.End_Date, equity));
        result.Run_End = DateTime.UtcNow;

        Build_Strategy_Stats(result);
        Build_Walk_Forward(result, config);

        return result;
    }

    private static List<Trade_Result_BO> Manage_Open_Positions(
        List<Simulated_Position> positions, Candle_BO bar,
        Backtest_Config config, Backtest_Result result)
    {
        var closed = new List<Trade_Result_BO>();
        foreach (var pos in positions.ToList())
        {
            var trade = pos.Check_And_Close(bar, config);
            if (trade != null)
            {
                positions.Remove(pos);
                closed.Add(trade);
            }
        }
        return closed;
    }

    private static Account_State_BO Build_Account_State(decimal equity, decimal day_open, int open_count, Backtest_Config cfg) => new()
    {
        Equity            = equity,
        Balance           = equity,
        Day_Open_Balance  = day_open,
        Week_Open_Balance = day_open,
        Peak_Equity       = equity,
        Open_Trade_Count  = open_count
    };

    private static void Build_Strategy_Stats(Backtest_Result result)
    {
        foreach (var group in result.Trades.GroupBy(t => t.Strategy))
        {
            var trades = group.ToList();
            int wins   = trades.Count(t => t.Is_Winner);
            decimal gp = trades.Where(t => t.R_Multiple > 0).Sum(t => t.R_Multiple);
            decimal gl = Math.Abs(trades.Where(t => t.R_Multiple < 0).Sum(t => t.R_Multiple));

            result.Strategy_Stats[group.Key] = new Strategy_Backtest_Stats
            {
                Strategy     = group.Key,
                Total_Trades = trades.Count,
                Win_Rate     = trades.Count > 0 ? Math.Round((decimal)wins / trades.Count * 100, 1) : 0,
                Profit_Factor= gl > 0 ? Math.Round(gp / gl, 2) : 0,
                Avg_R        = trades.Count > 0 ? Math.Round(trades.Average(t => t.R_Multiple), 3) : 0
            };
        }
    }

    private static void Build_Walk_Forward(Backtest_Result result, Backtest_Config config)
    {
        int split = (int)(result.Trades.Count * config.In_Sample_Ratio);
        if (split < 10 || result.Trades.Count - split < 5) return;

        result.In_Sample  = Summarize("In-Sample",   result.Trades.Take(split).ToList());
        result.Out_Sample = Summarize("Out-of-Sample", result.Trades.Skip(split).ToList());
    }

    private static Backtest_Summary Summarize(string label, List<Trade_Result_BO> trades)
    {
        if (trades.Count == 0) return new Backtest_Summary { Label = label };

        int wins   = trades.Count(t => t.Is_Winner);
        decimal gp = trades.Where(t => t.R_Multiple > 0).Sum(t => t.R_Multiple);
        decimal gl = Math.Abs(trades.Where(t => t.R_Multiple < 0).Sum(t => t.R_Multiple));
        decimal pf = gl > 0 ? Math.Round(gp / gl, 2) : 0;
        decimal wr = Math.Round((decimal)wins / trades.Count * 100, 1);
        decimal avg_r = Math.Round(trades.Average(t => t.R_Multiple), 3);

        return new Backtest_Summary
        {
            Label          = label,
            Period_Start   = trades.First().Opened_At_UTC,
            Period_End     = trades.Last().Closed_At_UTC,
            Total_Trades   = trades.Count,
            Win_Rate       = wr,
            Profit_Factor  = pf,
            Avg_R          = avg_r,
            Passed         = pf >= 1.15m && avg_r > 0 && trades.Count >= 15
        };
    }
}
