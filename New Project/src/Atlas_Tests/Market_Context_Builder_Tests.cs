using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;
using Atlas_Market_Data.Services;
using Xunit;

namespace Atlas_Tests;

public class Market_Context_Builder_Tests
{
    private readonly Market_Context_Builder _sut = new(new Session_Filter_Service(), new Spread_Filter_Service());

    private static Market_Symbol_BO Symbol() => new() { Symbol_Name = "EURUSD", Normal_Spread_Pips = 1.0m, Max_Allowed_Spread_Pips = 2.0m };

    [Fact]
    public void Enrich_Sets_Session_Spread_And_News_Fields_From_Inputs()
    {
        var raw = new Market_Context_BO { Score_Trend_Alignment = 10, Score_Volatility_Quality = 10, Score_Structure_Clarity = 10 };

        var ctx = _sut.Enrich(raw, Session_Type.New_York, spread: 0.8m, news_block: false, correlation_score: 7, Symbol());

        Assert.Equal(Session_Type.New_York, ctx.Current_Session);
        Assert.Equal(0.8m, ctx.Current_Spread_Pips);
        Assert.True(ctx.Spread_Is_Normal); // 0.8 <= 1.0 * 1.5
        Assert.False(ctx.News_Lockout_Active);
        Assert.Equal(7, ctx.Score_Correlation_Safety);
    }

    [Fact]
    public void Enrich_Zeroes_News_Safety_Score_When_News_Lockout_Active()
    {
        var raw = new Market_Context_BO();

        var ctx = _sut.Enrich(raw, Session_Type.London, spread: 0.8m, news_block: true, correlation_score: 10, Symbol());

        Assert.True(ctx.News_Lockout_Active);
        Assert.Equal(0, ctx.Score_News_Safety);
    }

    [Fact]
    public void Enrich_Recomputes_Regime_Quality_Score_As_Sum_Of_Components()
    {
        var raw = new Market_Context_BO
        {
            Score_Trend_Alignment = 20,
            Score_Volatility_Quality = 15,
            Score_Structure_Clarity = 15,
            // Off-peak session (Asian) so the +5 MTF confluence bonus does not apply
        };

        var ctx = _sut.Enrich(raw, Session_Type.Asian, spread: 0.8m, news_block: false, correlation_score: 10, Symbol());

        int expected = ctx.Score_Trend_Alignment + ctx.Score_Volatility_Quality + ctx.Score_Session_Quality
                     + ctx.Score_Spread_Quality + ctx.Score_Structure_Clarity + ctx.Score_News_Safety
                     + ctx.Score_Correlation_Safety;
        Assert.Equal(expected, ctx.Regime_Quality_Score);
    }

    [Fact]
    public void Enrich_Adds_Five_Point_Confluence_Bonus_When_Aligned_During_London()
    {
        // Both use the same session (so Score_Session_Quality is identical) — only alignment differs,
        // isolating the +5 confluence bonus from any session-quality difference.
        var aligned = new Market_Context_BO
        {
            D1_Bias = Trade_Direction_Type.Buy,
            H4_Bias = Trade_Direction_Type.Buy,
            H1_Bias = Trade_Direction_Type.Buy, // Higher_Timeframes_Aligned = true
            Score_Trend_Alignment = 5,
            Score_Volatility_Quality = 5,
            Score_Structure_Clarity = 5
        };
        var not_aligned = new Market_Context_BO
        {
            D1_Bias = Trade_Direction_Type.Buy,
            H4_Bias = Trade_Direction_Type.Buy,
            H1_Bias = Trade_Direction_Type.Sell, // breaks alignment -> bonus does not apply
            Score_Trend_Alignment = 5,
            Score_Volatility_Quality = 5,
            Score_Structure_Clarity = 5
        };

        var without_bonus = _sut.Enrich(not_aligned, Session_Type.London, 0.8m, false, 10, Symbol());
        var with_bonus     = _sut.Enrich(aligned, Session_Type.London, 0.8m, false, 10, Symbol());

        Assert.Equal(without_bonus.Regime_Quality_Score + 5, with_bonus.Regime_Quality_Score);
    }

    [Fact]
    public void Enrich_Caps_Regime_Quality_Score_At_100()
    {
        var raw = new Market_Context_BO
        {
            D1_Bias = Trade_Direction_Type.Buy,
            H4_Bias = Trade_Direction_Type.Buy,
            H1_Bias = Trade_Direction_Type.Buy,
            Score_Trend_Alignment = 20,
            Score_Volatility_Quality = 15,
            Score_Structure_Clarity = 15
        };

        // Session/spread/news/correlation scores are all at their max too, pushing the raw sum past 100
        var ctx = _sut.Enrich(raw, Session_Type.London_NY_Overlap, spread: 0.1m, news_block: false, correlation_score: 10, Symbol());

        Assert.True(ctx.Regime_Quality_Score <= 100);
    }
}
