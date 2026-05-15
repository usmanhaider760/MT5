using MT5GoldScalper.V2.Core.Abstractions;
using MT5GoldScalper.V2.Core.Models;

namespace MT5GoldScalper.V2.Core.Services;

public sealed class TradingDecisionSnapshotService(
    IMarketDataService marketData,
    IAccountDataService accountData,
    INewsService news,
    IIndicatorService indicators,
    IOrderSafetyService orderSafety,
    IDecisionEngine decisionEngine) : ITradingDecisionSnapshotService
{
    public async Task<TradingDecisionSnapshot> CreateAsync(string symbol, CancellationToken ct = default)
    {
        var normalizedSymbol = symbol.Trim().ToUpperInvariant().Replace("M", string.Empty);
        var snapshot = new TradingDecisionSnapshot
        {
            Pair = normalizedSymbol,
            AsOfUtc = DateTime.UtcNow,
            Market = await marketData.GetMarketDataAsync(normalizedSymbol, ct),
            AccountRisk = await accountData.GetAccountRiskAsync(normalizedSymbol, ct),
            SessionNews = await news.GetSessionNewsAsync(normalizedSymbol, ct),
            StrategySignal = await indicators.GetStrategySignalAsync(normalizedSymbol, ct)
        };

        snapshot.ExecutionSafety = await orderSafety.EvaluateAsync(snapshot, ct);
        return decisionEngine.Evaluate(snapshot);
    }
}
