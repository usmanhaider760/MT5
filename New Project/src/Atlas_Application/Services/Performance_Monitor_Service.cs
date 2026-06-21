using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;

namespace Atlas_Application.Services;

public class Performance_Monitor_Service
{
    private readonly List<Trade_Result_BO> _results = [];
    private readonly Dictionary<Strategy_Type, Strategy_Performance_BO> _performance = [];

    public void Record_Trade_Result(Trade_Result_BO result)
    {
        _results.Add(result);
        Update_Strategy_Performance(result);
    }

    public Strategy_Performance_BO Get_Strategy_Performance(Strategy_Type strategy, string symbol)
    {
        var key = $"{strategy}:{symbol}";
        if (!_performance.ContainsKey(strategy))
            _performance[strategy] = new Strategy_Performance_BO { Strategy = strategy, Symbol_Name = symbol };
        return _performance[strategy];
    }

    public List<Strategy_Type> Get_Strategies_To_Disable()
    {
        return _performance.Values
            .Where(p => p.Is_Active && p.Should_Auto_Disable)
            .Select(p => p.Strategy)
            .ToList();
    }

    public int Total_Trades_Count => _results.Count;
    public decimal Total_R        => _results.Count > 0 ? Math.Round(_results.Sum(r => r.R_Multiple), 2) : 0;

    public IReadOnlyDictionary<Strategy_Type, Strategy_Performance_BO> Get_All_Strategy_Performance() =>
        _performance.AsReadOnly();

    public (decimal Win_Rate, decimal Avg_R, decimal Profit_Factor, decimal Max_DD) Get_Overall_Stats()
    {
        if (_results.Count == 0) return (0, 0, 0, 0);

        int wins  = _results.Count(r => r.Is_Winner);
        decimal win_rate = Math.Round((decimal)wins / _results.Count * 100, 1);
        decimal avg_r    = _results.Average(r => r.R_Multiple);
        decimal gross_p  = _results.Where(r => r.Is_Winner).Sum(r => r.Net_PnL_Currency);
        decimal gross_l  = Math.Abs(_results.Where(r => !r.Is_Winner).Sum(r => r.Net_PnL_Currency));
        decimal pf       = gross_l > 0 ? Math.Round(gross_p / gross_l, 2) : 0;

        decimal max_dd = Calculate_Max_Drawdown();

        return (win_rate, Math.Round(avg_r, 3), pf, max_dd);
    }

    private decimal Calculate_Max_Drawdown()
    {
        decimal peak = 0, max_dd = 0, equity = 0;
        foreach (var r in _results.OrderBy(r => r.Closed_At_UTC))
        {
            equity += r.Net_PnL_Currency;
            if (equity > peak) peak = equity;
            if (peak > 0) max_dd = Math.Max(max_dd, (peak - equity) / peak * 100);
        }
        return Math.Round(max_dd, 2);
    }

    private void Update_Strategy_Performance(Trade_Result_BO result)
    {
        if (!_performance.TryGetValue(result.Strategy, out var perf))
        {
            perf = new Strategy_Performance_BO { Strategy = result.Strategy, Symbol_Name = result.Symbol_Name };
            _performance[result.Strategy] = perf;
        }

        perf.Total_Trades++;
        if (result.Is_Winner)
        {
            perf.Winning_Trades++;
            perf.Gross_Profit_R += result.R_Multiple;
        }
        else
        {
            perf.Losing_Trades++;
            perf.Gross_Loss_R += result.R_Multiple;
        }

        // Rolling 20-trade expectancy
        var last_20 = _results.Where(r => r.Strategy == result.Strategy).TakeLast(20).ToList();
        if (last_20.Count >= 20)
            perf.Rolling_20_Expectancy = last_20.Average(r => r.R_Multiple);

        var last_30 = _results.Where(r => r.Strategy == result.Strategy).TakeLast(30).ToList();
        if (last_30.Count >= 30)
        {
            decimal g_p = last_30.Where(r => r.Is_Winner).Sum(r => r.Net_PnL_Currency);
            decimal g_l = Math.Abs(last_30.Where(r => !r.Is_Winner).Sum(r => r.Net_PnL_Currency));
            perf.Rolling_30_Profit_Factor = g_l > 0 ? g_p / g_l : 0;
        }
    }
}
