using MT5GoldScalper.V2.Core.Abstractions;
using MT5GoldScalper.V2.Core.Models;

namespace MT5GoldScalper.V2.Core.Services.Calculations;

public sealed class LotSizeCalculationService : ILotSizeCalculationService
{
    public void Calculate(TradingDecisionSnapshot snapshot)
    {
        var market = snapshot.Market;
        var account = snapshot.AccountRisk;
        var signal = snapshot.StrategySignal;

        var riskPerLot = CalculateRiskPerLot(market, signal);
        if (account.RiskAmount <= 0 || riskPerLot <= 0)
        {
            account.LotSize = 0;
            snapshot.ExecutionSafety.VolumeValid = false;
            return;
        }

        account.LotSize = NormalizeLot(account.RiskAmount / riskPerLot, market);
        snapshot.ExecutionSafety.VolumeValid = IsVolumeValid(account, market);
    }

    public bool IsVolumeValid(AccountRiskModel account, MarketDataModel market)
    {
        if (account.LotSize < market.MinLot || account.LotSize > market.MaxLot || market.LotStep <= 0)
        {
            return false;
        }

        var steps = (account.LotSize - market.MinLot) / market.LotStep;
        return Math.Abs(steps - Math.Round(steps)) < 0.000001m;
    }

    private static decimal CalculateRiskPerLot(MarketDataModel market, StrategySignalModel signal)
    {
        var distance = Math.Abs(signal.EntryPrice - signal.StopLossPrice);
        if (distance <= 0 || market.TickSize <= 0 || market.TickValue <= 0)
        {
            return 0;
        }

        return distance / market.TickSize * market.TickValue;
    }

    private static decimal NormalizeLot(decimal rawLot, MarketDataModel market)
    {
        if (market.LotStep <= 0)
        {
            return Math.Clamp(rawLot, market.MinLot, market.MaxLot);
        }

        var clamped = Math.Clamp(rawLot, market.MinLot, market.MaxLot);
        var steps = Math.Floor((clamped - market.MinLot) / market.LotStep);
        return Math.Round(market.MinLot + steps * market.LotStep, CountDecimals(market.LotStep), MidpointRounding.AwayFromZero);
    }

    private static int CountDecimals(decimal value)
    {
        value = Math.Abs(value);
        var bits = decimal.GetBits(value);
        return (bits[3] >> 16) & 0x7F;
    }
}
