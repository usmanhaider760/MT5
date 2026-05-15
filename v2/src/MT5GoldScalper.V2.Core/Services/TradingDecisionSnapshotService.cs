using MT5GoldScalper.V2.Core.Abstractions;
using MT5GoldScalper.V2.Core.Models;

namespace MT5GoldScalper.V2.Core.Services;

public sealed class TradingDecisionSnapshotService(
    IMarketDataService marketData,
    IAccountDataService accountData,
    INewsService news,
    IIndicatorService indicators,
    ISpreadCalculationService spreadCalculation,
    IRiskCalculationService riskCalculation,
    ILotSizeCalculationService lotSizeCalculation,
    ITargetCalculationService targetCalculation,
    IStopValidationService stopValidation,
    IRewardRiskCalculationService rewardRiskCalculation,
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

        snapshot.TradeDirection = ToTradeDirection(snapshot.StrategySignal.SetupDirection);
        targetCalculation.Calculate(snapshot);
        stopValidation.Calculate(snapshot);
        spreadCalculation.Calculate(snapshot);
        riskCalculation.Calculate(snapshot);
        lotSizeCalculation.Calculate(snapshot);
        rewardRiskCalculation.Calculate(snapshot);
        spreadCalculation.Calculate(snapshot);

        snapshot.ExecutionSafety = await orderSafety.EvaluateAsync(snapshot, ct);
        return decisionEngine.Evaluate(snapshot);
    }

    private static TradeDirection ToTradeDirection(string setupDirection) =>
        setupDirection.ToUpperInvariant() switch
        {
            "BUY" => TradeDirection.Buy,
            "SELL" => TradeDirection.Sell,
            _ => TradeDirection.None
        };
}
