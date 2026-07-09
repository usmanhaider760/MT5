using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;

namespace Atlas_Market_Data.Services;

/// <summary>
/// Enriches a raw regime-detector Market_Context_BO with session/spread/news/correlation
/// scoring and recomputes the final Regime_Quality_Score, including the MTF confluence bonus.
/// Extracted from Trade_Pipeline_Service.Evaluate_Symbol_Async so it can be tested in isolation.
/// </summary>
public class Market_Context_Builder
{
    private readonly Session_Filter_Service _session_filter;
    private readonly Spread_Filter_Service _spread_filter;

    public Market_Context_Builder(Session_Filter_Service session_filter, Spread_Filter_Service spread_filter)
    {
        _session_filter = session_filter;
        _spread_filter = spread_filter;
    }

    public Market_Context_BO Enrich(
        Market_Context_BO raw_context,
        Session_Type session,
        decimal spread,
        bool news_block,
        int correlation_score,
        Market_Symbol_BO symbol)
    {
        raw_context.Current_Session = session;
        raw_context.Current_Spread_Pips = spread;
        raw_context.Spread_Is_Normal = spread <= symbol.Normal_Spread_Pips * 1.5m;
        raw_context.News_Lockout_Active = news_block;
        raw_context.Score_Session_Quality = _session_filter.Get_Session_Quality_Score(session);
        raw_context.Score_Spread_Quality = _spread_filter.Get_Spread_Quality_Score(spread, symbol);
        raw_context.Score_News_Safety = news_block ? 0 : 10;
        raw_context.Score_Correlation_Safety = correlation_score;

        // Recompute total score with real session/spread/news/correlation values
        raw_context.Regime_Quality_Score =
            raw_context.Score_Trend_Alignment + raw_context.Score_Volatility_Quality +
            raw_context.Score_Session_Quality + raw_context.Score_Spread_Quality +
            raw_context.Score_Structure_Clarity + raw_context.Score_News_Safety +
            raw_context.Score_Correlation_Safety;

        // Session-MTF confluence bonus (+5): all TFs aligned during peak session
        if (raw_context.Higher_Timeframes_Aligned &&
            (session == Session_Type.London_NY_Overlap || session == Session_Type.London))
            raw_context.Regime_Quality_Score += 5;

        raw_context.Regime_Quality_Score = Math.Min(100, raw_context.Regime_Quality_Score);

        return raw_context;
    }
}
