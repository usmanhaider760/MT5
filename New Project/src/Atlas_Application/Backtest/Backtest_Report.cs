using System.Text;
using Atlas_Domain.Enums;

namespace Atlas_Application.Backtest;

public static class Backtest_Report
{
    public static string Generate_Text(Backtest_Result result)
    {
        var sb = new StringBuilder();

        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine($"  ATLAS BACKTEST REPORT  —  {result.Symbol_Name}");
        sb.AppendLine($"  Period : {result.Equity_Curve.First().Date:yyyy-MM-dd} → {result.Equity_Curve.Last().Date:yyyy-MM-dd}");
        sb.AppendLine($"  Runtime: {result.Duration.TotalSeconds:F1}s");
        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine();

        sb.AppendLine("─── OVERALL PERFORMANCE ───────────────────────────────────");
        sb.AppendLine($"  Total Trades     : {result.Total_Trades}");
        sb.AppendLine($"  Winning Trades   : {result.Winning_Trades}  ({result.Win_Rate:F1}%)");
        sb.AppendLine($"  Losing Trades    : {result.Losing_Trades}");
        sb.AppendLine($"  Profit Factor    : {result.Profit_Factor:F2}");
        sb.AppendLine($"  Expectancy (Avg R): {result.Avg_R:+0.000;-0.000}R");
        sb.AppendLine($"  Net R Total      : {result.Net_R:+0.000;-0.000}R");
        sb.AppendLine($"  Max Drawdown     : {result.Max_Drawdown_Percent:F2}%");
        sb.AppendLine($"  Total Return     : {result.Total_Return_Percent:+0.00;-0.00}%");
        sb.AppendLine($"  Final Equity     : ${result.Final_Equity:F2}");
        sb.AppendLine($"  Total Commission : ${result.Total_Commission:F2}");
        sb.AppendLine($"  Total Slippage   : {result.Total_Slippage_Pips:F1} pips");
        sb.AppendLine($"  Signals Rejected : {result.Signals_Rejected}");
        sb.AppendLine($"  Filter Blocks    : {result.Filters_Blocked}");
        sb.AppendLine();

        sb.AppendLine($"  VALIDATION : {(result.Passed_Validation ? "✓ PASSED" : "✗ FAILED")}");
        sb.AppendLine($"    PF ≥ 1.20  : {(result.Profit_Factor >= 1.20m ? "✓" : "✗")} ({result.Profit_Factor:F2})");
        sb.AppendLine($"    E  > 0     : {(result.Expectancy > 0    ? "✓" : "✗")} ({result.Avg_R:F3}R)");
        sb.AppendLine($"    DD < 12%   : {(result.Max_Drawdown_Percent < 12m ? "✓" : "✗")} ({result.Max_Drawdown_Percent:F2}%)");
        sb.AppendLine($"    Trades ≥30 : {(result.Total_Trades >= 30 ? "✓" : "✗")} ({result.Total_Trades})");
        sb.AppendLine();

        if (result.Strategy_Stats.Count > 0)
        {
            sb.AppendLine("─── BY STRATEGY ───────────────────────────────────────────");
            sb.AppendLine($"  {"Strategy",-36} {"Trades",6} {"WR%",6} {"PF",6} {"AvgR",8}");
            sb.AppendLine($"  {"─────────────────────────────────────",-36} {"──────",6} {"────",6} {"────",6} {"──────",8}");
            foreach (var (strategy, stats) in result.Strategy_Stats)
            {
                sb.AppendLine($"  {strategy,-36} {stats.Total_Trades,6} {stats.Win_Rate,5:F1}% {stats.Profit_Factor,6:F2} {stats.Avg_R,7:+0.000;-0.000}R");
            }
            sb.AppendLine();
        }

        if (result.In_Sample != null && result.Out_Sample != null)
        {
            sb.AppendLine("─── WALK-FORWARD VALIDATION ───────────────────────────────");
            Write_Summary(sb, result.In_Sample);
            Write_Summary(sb, result.Out_Sample);
            sb.AppendLine();
            bool oos_pass = result.Out_Sample.Passed;
            sb.AppendLine($"  Out-of-sample result : {(oos_pass ? "✓ LIVE CANDIDATE" : "✗ NOT YET READY FOR LIVE")}");
            sb.AppendLine();
        }

        sb.AppendLine("─── EQUITY CURVE (daily snapshots) ────────────────────────");
        foreach (var (date, eq) in result.Equity_Curve.TakeLast(30))
            sb.AppendLine($"  {date:yyyy-MM-dd}  ${eq:F2}");

        sb.AppendLine("═══════════════════════════════════════════════════════════");
        return sb.ToString();
    }

    private static void Write_Summary(StringBuilder sb, Backtest_Summary s)
    {
        sb.AppendLine($"  [{s.Label}]  {s.Period_Start:yyyy-MM-dd} → {s.Period_End:yyyy-MM-dd}");
        sb.AppendLine($"    Trades: {s.Total_Trades}  WR: {s.Win_Rate:F1}%  PF: {s.Profit_Factor:F2}  AvgR: {s.Avg_R:+0.000;-0.000}  Pass: {(s.Passed ? "✓" : "✗")}");
    }

    public static string Generate_Monte_Carlo_Section(Monte_Carlo_Result mc)
    {
        var sb = new StringBuilder();

        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine($"  MONTE CARLO STRESS TEST  ({mc.Iterations:N0} simulations × {mc.Trade_Count} trades)");
        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine();
        sb.AppendLine($"  Final R — median       : {mc.Median_Final_R:+0.00;-0.00}R");
        sb.AppendLine($"  Final R — 5th pct (worst) : {mc.P05_Final_R:+0.00;-0.00}R");
        sb.AppendLine($"  Final R — 95th pct (best) : {mc.P95_Final_R:+0.00;-0.00}R");
        sb.AppendLine();
        sb.AppendLine($"  Max Drawdown — median  : {mc.Median_Max_Drawdown:F1}%");
        sb.AppendLine($"  Max Drawdown — 95th pct: {mc.P95_Max_Drawdown:F1}%  ← stress scenario");
        sb.AppendLine();
        sb.AppendLine($"  Profit Factor — median : {mc.Median_Profit_Factor:F2}");
        sb.AppendLine($"  Profit Factor — 5th pct: {mc.P05_Profit_Factor:F2}  ← worst shuffle");
        sb.AppendLine();
        sb.AppendLine($"  Probability of Ruin    : {mc.Prob_Ruin_Percent:F1}%  (DD ≥ 20%)");
        sb.AppendLine();

        bool dd_ok = mc.P95_Max_Drawdown < 15m;
        bool pf_ok = mc.P05_Profit_Factor >= 1.0m;
        bool ruin_ok = mc.Prob_Ruin_Percent < 5m;
        bool passed = dd_ok && pf_ok && ruin_ok;

        sb.AppendLine($"  Stress DD < 15%   : {(dd_ok   ? "✓" : "✗")} ({mc.P95_Max_Drawdown:F1}%)");
        sb.AppendLine($"  Worst PF ≥ 1.0    : {(pf_ok   ? "✓" : "✗")} ({mc.P05_Profit_Factor:F2})");
        sb.AppendLine($"  Ruin prob < 5%    : {(ruin_ok ? "✓" : "✗")} ({mc.Prob_Ruin_Percent:F1}%)");
        sb.AppendLine();
        sb.AppendLine($"  {(passed ? "✓ MONTE CARLO PASSED — system is stress-robust" : "✗ MONTE CARLO FAILED — review position sizing or expectancy")}");
        sb.AppendLine("═══════════════════════════════════════════════════════════");

        return sb.ToString();
    }
}
