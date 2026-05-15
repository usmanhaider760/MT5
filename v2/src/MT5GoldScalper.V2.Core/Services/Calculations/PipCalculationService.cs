using MT5GoldScalper.V2.Core.Abstractions;
using MT5GoldScalper.V2.Core.Models;

namespace MT5GoldScalper.V2.Core.Services.Calculations;

public sealed class PipCalculationService : IPipCalculationService
{
    public decimal GetPipSize(MarketDataModel market)
    {
        if (market.Point <= 0)
        {
            return 0;
        }

        return market.Digits is 2 or 3 or 5 ? market.Point * 10m : market.Point;
    }

    public decimal PriceDistanceToPips(MarketDataModel market, decimal priceDistance)
    {
        var pipSize = GetPipSize(market);
        return pipSize <= 0 ? 0 : Math.Abs(priceDistance) / pipSize;
    }

    public int PriceDistanceToPoints(MarketDataModel market, decimal priceDistance)
    {
        return market.Point <= 0 ? 0 : (int)Math.Round(Math.Abs(priceDistance) / market.Point, MidpointRounding.AwayFromZero);
    }
}
