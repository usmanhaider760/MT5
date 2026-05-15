using MT5GoldScalper.V2.Core.Abstractions;
using MT5GoldScalper.V2.Core.Models;

namespace MT5GoldScalper.V2.Core.Services.Calculations;

public sealed class StopValidationService(IPipCalculationService pipCalculation) : IStopValidationService
{
    public void Calculate(TradingDecisionSnapshot snapshot)
    {
        var signal = snapshot.StrategySignal;
        signal.StopLossPips = DecimalRound(pipCalculation.PriceDistanceToPips(snapshot.Market, signal.EntryPrice - signal.StopLossPrice));
        snapshot.ExecutionSafety.StopsValid = AreStopsValid(snapshot);
    }

    public bool AreStopsValid(TradingDecisionSnapshot snapshot)
    {
        var signal = snapshot.StrategySignal;
        var market = snapshot.Market;

        if (signal.EntryPrice <= 0 || signal.StopLossPrice <= 0)
        {
            return false;
        }

        var directionValid = snapshot.TradeDirection switch
        {
            TradeDirection.Buy => signal.StopLossPrice < signal.EntryPrice,
            TradeDirection.Sell => signal.StopLossPrice > signal.EntryPrice,
            _ => false
        };

        if (!directionValid)
        {
            return false;
        }

        var stopDistancePoints = pipCalculation.PriceDistanceToPoints(market, signal.EntryPrice - signal.StopLossPrice);
        if (market.StopsLevelPoints > 0 && stopDistancePoints < market.StopsLevelPoints)
        {
            return false;
        }

        return market.FreezeLevelPoints <= 0 || stopDistancePoints > market.FreezeLevelPoints;
    }

    private static decimal DecimalRound(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
