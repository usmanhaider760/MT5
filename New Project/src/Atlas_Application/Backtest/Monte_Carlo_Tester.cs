namespace Atlas_Application.Backtest;

/// <summary>
/// Stress-tests a trade sequence by shuffling R-multiples across N simulated runs.
/// Measures worst-case drawdown distribution and probability of ruin independently
/// of the original trade order, which is an artifact of timing.
/// </summary>
public class Monte_Carlo_Tester
{
    private readonly Random _rng;

    public Monte_Carlo_Tester(int seed = 42) => _rng = new Random(seed);

    public Monte_Carlo_Result Run(List<decimal> r_multiples, int iterations = 1_000)
    {
        if (r_multiples.Count == 0) return new Monte_Carlo_Result();

        var final_r_list   = new List<decimal>(iterations);
        var max_dd_list    = new List<decimal>(iterations);
        var pf_list        = new List<decimal>(iterations);

        for (int iter = 0; iter < iterations; iter++)
        {
            var shuffled = r_multiples.OrderBy(_ => _rng.Next()).ToList();

            decimal cumulative_r = 0m;
            decimal equity       = 100m;  // notional starting equity in R-units
            decimal peak_eq      = 100m;
            decimal max_dd       = 0m;
            decimal gross_wins   = 0m;
            decimal gross_losses = 0m;

            foreach (var r in shuffled)
            {
                cumulative_r += r;
                equity       += r;
                if (equity > peak_eq) peak_eq = equity;

                decimal dd = (peak_eq - equity) / peak_eq * 100m;
                if (dd > max_dd) max_dd = dd;

                if (r > 0) gross_wins   += r;
                else       gross_losses += Math.Abs(r);
            }

            final_r_list.Add(cumulative_r);
            max_dd_list.Add(max_dd);
            // No losers → PF is effectively infinite; use sentinel so stress criteria pass
            pf_list.Add(gross_losses > 0 ? gross_wins / gross_losses : 99.99m);
        }

        final_r_list.Sort();
        max_dd_list.Sort();
        pf_list.Sort();

        int ruin_count = max_dd_list.Count(dd => dd >= 20m);

        return new Monte_Carlo_Result
        {
            Iterations            = iterations,
            Trade_Count           = r_multiples.Count,
            Median_Final_R        = Percentile(final_r_list, 50),
            P05_Final_R           = Percentile(final_r_list, 5),
            P95_Final_R           = Percentile(final_r_list, 95),
            Median_Max_Drawdown   = Percentile(max_dd_list, 50),
            P95_Max_Drawdown      = Percentile(max_dd_list, 95),
            Median_Profit_Factor  = Percentile(pf_list, 50),
            P05_Profit_Factor     = Percentile(pf_list, 5),
            Prob_Ruin_Percent     = Math.Round((decimal)ruin_count / iterations * 100m, 1)
        };
    }

    private static decimal Percentile(List<decimal> sorted, int pct)
    {
        if (sorted.Count == 0) return 0;
        int idx = (int)Math.Round(pct / 100m * (sorted.Count - 1));
        return sorted[Math.Clamp(idx, 0, sorted.Count - 1)];
    }
}

public class Monte_Carlo_Result
{
    public int     Iterations           { get; set; }
    public int     Trade_Count          { get; set; }
    public decimal Median_Final_R       { get; set; }
    public decimal P05_Final_R          { get; set; }
    public decimal P95_Final_R          { get; set; }
    public decimal Median_Max_Drawdown  { get; set; }
    public decimal P95_Max_Drawdown     { get; set; }
    public decimal Median_Profit_Factor { get; set; }
    public decimal P05_Profit_Factor    { get; set; }
    public decimal Prob_Ruin_Percent    { get; set; }
}
