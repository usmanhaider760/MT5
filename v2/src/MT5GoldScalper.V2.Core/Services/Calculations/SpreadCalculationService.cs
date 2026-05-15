using MT5GoldScalper.V2.Core.Abstractions;
using MT5GoldScalper.V2.Core.Configuration;
using MT5GoldScalper.V2.Core.Models;

namespace MT5GoldScalper.V2.Core.Services.Calculations;

public sealed class SpreadCalculationService(IPipCalculationService pipCalculation, DecisionMakerOptions options) : ISpreadCalculationService
{
    public void Calculate(TradingDecisionSnapshot snapshot)
    {
        var market = snapshot.Market;
        market.SpreadPrice = Math.Max(0, market.Ask - market.Bid);
        market.SpreadPoints = pipCalculation.PriceDistanceToPoints(market, market.SpreadPrice);
        market.SpreadPips = DecimalRound(pipCalculation.PriceDistanceToPips(market, market.SpreadPrice));
        market.SpreadPercentOfTp1 = snapshot.StrategySignal.Tp1Pips > 0
            ? DecimalRound(market.SpreadPips / snapshot.StrategySignal.Tp1Pips * 100m)
            : 0;

        snapshot.ExecutionSafety.SpreadAcceptable = market.SpreadPips <= options.MaxSpreadPips;
    }

    private static decimal DecimalRound(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
