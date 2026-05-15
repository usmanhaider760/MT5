using MT5GoldScalper.V2.Core.Abstractions;
using MT5GoldScalper.V2.Core.Models;

namespace MT5GoldScalper.V2.Core.Services.Calculations;

public sealed class TargetCalculationService(IPipCalculationService pipCalculation) : ITargetCalculationService
{
    public void Calculate(TradingDecisionSnapshot snapshot)
    {
        var signal = snapshot.StrategySignal;
        signal.Tp1Pips = DecimalRound(pipCalculation.PriceDistanceToPips(snapshot.Market, signal.Tp1Price - signal.EntryPrice));
        signal.Tp2Pips = DecimalRound(pipCalculation.PriceDistanceToPips(snapshot.Market, signal.Tp2Price - signal.EntryPrice));
    }

    public bool AreTargetsValid(TradingDecisionSnapshot snapshot)
    {
        var signal = snapshot.StrategySignal;
        if (signal.EntryPrice <= 0 || signal.Tp1Price <= 0 || signal.Tp2Price <= 0)
        {
            return false;
        }

        return snapshot.TradeDirection switch
        {
            TradeDirection.Buy => signal.Tp1Price > signal.EntryPrice && signal.Tp2Price > signal.EntryPrice,
            TradeDirection.Sell => signal.Tp1Price < signal.EntryPrice && signal.Tp2Price < signal.EntryPrice,
            _ => false
        };
    }

    private static decimal DecimalRound(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
