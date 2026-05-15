using MT5GoldScalper.V2.Core.Abstractions;
using MT5GoldScalper.V2.Core.Models;

namespace MT5GoldScalper.V2.Core.Services.Calculations;

public sealed class RewardRiskCalculationService(IPipCalculationService pipCalculation) : IRewardRiskCalculationService
{
    public void Calculate(TradingDecisionSnapshot snapshot)
    {
        var signal = snapshot.StrategySignal;
        var stopPips = signal.StopLossPips;

        signal.RiskRewardTp1 = stopPips > 0 ? DecimalRound(signal.Tp1Pips / stopPips) : 0;
        signal.RiskRewardTp2 = stopPips > 0 ? DecimalRound(signal.Tp2Pips / stopPips) : 0;

        var pipValuePerLot = CalculatePipValuePerLot(snapshot.Market);
        signal.EstimatedTp1Profit = DecimalRound(signal.Tp1Pips * pipValuePerLot * snapshot.AccountRisk.LotSize);
        signal.EstimatedTp2Profit = DecimalRound(signal.Tp2Pips * pipValuePerLot * snapshot.AccountRisk.LotSize);
    }

    private decimal CalculatePipValuePerLot(MarketDataModel market)
    {
        var pipSize = pipCalculation.GetPipSize(market);
        return market.TickSize <= 0 ? 0 : market.TickValue * pipSize / market.TickSize;
    }

    private static decimal DecimalRound(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
