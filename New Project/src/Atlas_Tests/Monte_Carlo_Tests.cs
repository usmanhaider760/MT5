using Atlas_Application.Backtest;
using Xunit;

namespace Atlas_Tests;

/// <summary>
/// Tests for Monte_Carlo_Tester: shuffle logic, percentile distribution,
/// ruin probability, and determinism with a fixed seed.
/// </summary>
public class Monte_Carlo_Tests
{
    [Fact]
    public void Empty_Input_Returns_Zero_Result()
    {
        var result = new Monte_Carlo_Tester(seed: 1).Run([], iterations: 100);

        Assert.Equal(0, result.Trade_Count);
        Assert.Equal(0m, result.Median_Final_R);
        Assert.Equal(0m, result.Prob_Ruin_Percent);
    }

    [Fact]
    public void All_Winners_Gives_Zero_Ruin_Probability()
    {
        var r_list = Enumerable.Repeat(1.0m, 50).ToList();
        var result = new Monte_Carlo_Tester(seed: 42).Run(r_list, iterations: 200);

        Assert.Equal(0m, result.Prob_Ruin_Percent);
        Assert.True(result.Median_Final_R > 0);
    }

    [Fact]
    public void Positive_Expectancy_Gives_Positive_Median_Final_R()
    {
        var r_list = Enumerable.Repeat(1.5m, 60).Concat(Enumerable.Repeat(-1.0m, 40)).ToList();
        var result = new Monte_Carlo_Tester(seed: 42).Run(r_list, iterations: 500);

        Assert.True(result.Median_Final_R > 0);
    }

    [Fact]
    public void P05_Final_R_Is_Less_Than_Or_Equal_To_Median()
    {
        var r_list = Enumerable.Range(0, 40).Select(i => i % 3 == 0 ? -1.0m : 1.5m).ToList();
        var result = new Monte_Carlo_Tester(seed: 7).Run(r_list, iterations: 500);

        Assert.True(result.P05_Final_R <= result.Median_Final_R);
    }

    [Fact]
    public void P95_Final_R_Is_Greater_Than_Or_Equal_To_Median()
    {
        var r_list = Enumerable.Range(0, 40).Select(i => i % 3 == 0 ? -1.0m : 1.5m).ToList();
        var result = new Monte_Carlo_Tester(seed: 7).Run(r_list, iterations: 500);

        Assert.True(result.P95_Final_R >= result.Median_Final_R);
    }

    [Fact]
    public void P95_Drawdown_Is_At_Least_As_Bad_As_Median()
    {
        var r_list = Enumerable.Range(0, 30).Select(i => i % 2 == 0 ? -1.5m : 1.0m).ToList();
        var result = new Monte_Carlo_Tester(seed: 42).Run(r_list, iterations: 200);

        Assert.True(result.Median_Max_Drawdown >= 0);
        Assert.True(result.P95_Max_Drawdown >= result.Median_Max_Drawdown);
    }

    [Fact]
    public void Results_Are_Deterministic_With_Same_Seed()
    {
        var r_list = Enumerable.Range(0, 50).Select(i => i % 3 == 0 ? -1.0m : 1.5m).ToList();

        var r1 = new Monte_Carlo_Tester(seed: 999).Run(r_list, iterations: 300);
        var r2 = new Monte_Carlo_Tester(seed: 999).Run(r_list, iterations: 300);

        Assert.Equal(r1.Median_Final_R,    r2.Median_Final_R);
        Assert.Equal(r1.P95_Max_Drawdown,  r2.P95_Max_Drawdown);
        Assert.Equal(r1.Prob_Ruin_Percent, r2.Prob_Ruin_Percent);
    }

    [Fact]
    public void Trade_Count_And_Iterations_Recorded_Correctly()
    {
        var r_list = Enumerable.Repeat(1.0m, 25).ToList();
        var result = new Monte_Carlo_Tester(seed: 1).Run(r_list, iterations: 150);

        Assert.Equal(25,  result.Trade_Count);
        Assert.Equal(150, result.Iterations);
    }

    [Fact]
    public void All_Losers_Give_One_Hundred_Percent_Ruin()
    {
        var r_list = Enumerable.Repeat(-5.0m, 20).ToList();
        var result = new Monte_Carlo_Tester(seed: 42).Run(r_list, iterations: 200);

        Assert.Equal(100m, result.Prob_Ruin_Percent);
    }
}
