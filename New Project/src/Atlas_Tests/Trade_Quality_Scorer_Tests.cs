using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;
using Atlas_Risk.Services;
using Xunit;

namespace Atlas_Tests;

public class Trade_Quality_Scorer_Tests
{
    private readonly Trade_Quality_Scorer _sut = new();

    private static Trade_Signal_BO Make_Signal(
        Trade_Direction_Type dir = Trade_Direction_Type.Buy,
        Strategy_Type strat      = Strategy_Type.Trend_Pullback_Continuation,
        decimal rr               = 2.0m)
    {
        // Entry at 1.0, SL 100 pips away; TP driven by desired RR
        const decimal sl_distance = 0.0100m;
        return new Trade_Signal_BO
        {
            Symbol_Name       = "EURUSD",
            Direction         = dir,
            Strategy          = strat,
            Entry_Price       = 1.0000m,
            Stop_Loss_Price   = dir == Trade_Direction_Type.Buy ? 1.0000m - sl_distance : 1.0000m + sl_distance,
            Take_Profit_Price = dir == Trade_Direction_Type.Buy ? 1.0000m + sl_distance * rr : 1.0000m - sl_distance * rr
        };
    }

    private static Market_Context_BO Make_Context(
        Trade_Direction_Type d1  = Trade_Direction_Type.Buy,
        Trade_Direction_Type h4  = Trade_Direction_Type.Buy,
        Trade_Direction_Type h1  = Trade_Direction_Type.Buy,
        Market_Regime_Type regime = Market_Regime_Type.Trend_Swing,
        bool news_lockout        = false,
        int spread_score         = 10,
        int correlation_safety   = 10) => new()
    {
        D1_Bias                = d1,
        H4_Bias                = h4,
        H1_Bias                = h1,
        Regime                 = regime,
        News_Lockout_Active    = news_lockout,
        Score_Spread_Quality   = spread_score,
        Score_Correlation_Safety = correlation_safety
    };

    [Fact]
    public void Perfect_Signal_Returns_Live_Ready()
    {
        var sig = Make_Signal(rr: 2.5m);
        var ctx = Make_Context();
        var (score, grade) = _sut.Score_Trade(sig, ctx);
        Assert.Equal(Trade_Quality_Grade.Live_Ready, grade);
        Assert.True(score >= 85);
    }

    [Fact]
    public void News_Lockout_Reduces_Score_By_Five()
    {
        var sig = Make_Signal(rr: 2.5m);
        var ctx_no_news   = Make_Context(news_lockout: false);
        var ctx_with_news = Make_Context(news_lockout: true);

        var (score_no_news,   _) = _sut.Score_Trade(sig, ctx_no_news);
        var (score_with_news, _) = _sut.Score_Trade(sig, ctx_with_news);

        Assert.Equal(5, score_no_news - score_with_news);
    }

    [Fact]
    public void Regime_Mismatch_Lowers_Grade()
    {
        // Trend strategy in a Range regime — no regime match bonus
        var sig = Make_Signal(strat: Strategy_Type.Trend_Pullback_Continuation, rr: 1.5m);
        var ctx = Make_Context(regime: Market_Regime_Type.Range);
        var (_, grade) = _sut.Score_Trade(sig, ctx);
        Assert.NotEqual(Trade_Quality_Grade.Live_Ready, grade);
    }

    [Fact]
    public void Opposing_Bias_Reduces_Score()
    {
        var sig = Make_Signal(dir: Trade_Direction_Type.Buy);
        var ctx_aligned  = Make_Context(d1: Trade_Direction_Type.Buy,  h4: Trade_Direction_Type.Buy);
        var ctx_opposing = Make_Context(d1: Trade_Direction_Type.Sell, h4: Trade_Direction_Type.Sell);

        var (s_aligned,  _) = _sut.Score_Trade(sig, ctx_aligned);
        var (s_opposing, _) = _sut.Score_Trade(sig, ctx_opposing);

        Assert.True(s_aligned > s_opposing);
    }
}
