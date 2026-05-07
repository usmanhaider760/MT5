using MT5TradingBot.Models;

namespace MT5TradingBot.Core
{
    public readonly record struct BrokerFreezeLevelValidation(
        bool Success,
        bool DataUnavailable,
        string Message,
        double FreezeLevelPips,
        double StopLossDistancePips,
        double TakeProfitDistancePips);

    public static class BrokerFreezeLevelValidator
    {
        public static BrokerFreezeLevelValidation Validate(
            TradeRequest request,
            SymbolInfo? symbolInfo,
            double livePrice)
        {
            if (symbolInfo == null)
                return DataUnavailable("Symbol metadata is unavailable for broker freeze-level validation.");

            if (!symbolInfo.FreezeLevelPoints.HasValue ||
                double.IsNaN(symbolInfo.FreezeLevelPoints.Value) ||
                double.IsInfinity(symbolInfo.FreezeLevelPoints.Value) ||
                symbolInfo.FreezeLevelPoints.Value < 0)
            {
                return DataUnavailable("Broker freeze-level points are unavailable.");
            }

            double? pointSize = symbolInfo.EffectivePointSize;
            if (!pointSize.HasValue || pointSize.Value <= 0)
                return DataUnavailable("Broker point size is unavailable for freeze-level validation.");

            double referencePrice = request.OrderType == OrderType.MARKET
                ? livePrice
                : request.EntryPrice > 0
                    ? request.EntryPrice
                    : livePrice;

            if (!IsFinitePositive(referencePrice))
                return DataUnavailable("Reference price is unavailable for freeze-level validation.");

            if (!IsFinitePositive(request.StopLoss) || !IsFinitePositive(request.TakeProfit))
                return DataUnavailable("SL/TP data is unavailable for freeze-level validation.");

            double freezeDistancePrice = symbolInfo.FreezeLevelPoints.Value * pointSize.Value;
            double pipSize = symbolInfo.Digits is 3 or 5
                ? pointSize.Value * 10.0
                : pointSize.Value;
            if (!IsFinitePositive(pipSize))
                return DataUnavailable("Pip size is unavailable for freeze-level validation.");

            double freezeLevelPips = freezeDistancePrice / pipSize;
            double slDistancePips = Math.Abs(referencePrice - request.StopLoss) / pipSize;
            double tpDistancePips = Math.Abs(request.TakeProfit - referencePrice) / pipSize;

            if (slDistancePips + 1e-9 < freezeLevelPips)
            {
                return Violation(
                    $"Stop-loss distance {slDistancePips:F1} pips is below broker freeze level {freezeLevelPips:F1} pips.",
                    freezeLevelPips,
                    slDistancePips,
                    tpDistancePips);
            }

            if (tpDistancePips + 1e-9 < freezeLevelPips)
            {
                return Violation(
                    $"Take-profit distance {tpDistancePips:F1} pips is below broker freeze level {freezeLevelPips:F1} pips.",
                    freezeLevelPips,
                    slDistancePips,
                    tpDistancePips);
            }

            return new BrokerFreezeLevelValidation(
                true,
                false,
                "",
                freezeLevelPips,
                slDistancePips,
                tpDistancePips);
        }

        private static BrokerFreezeLevelValidation DataUnavailable(string message) =>
            new(false, true, message, 0, 0, 0);

        private static BrokerFreezeLevelValidation Violation(
            string message,
            double freezeLevelPips,
            double stopLossDistancePips,
            double takeProfitDistancePips) =>
            new(false, false, message, freezeLevelPips, stopLossDistancePips, takeProfitDistancePips);

        private static bool IsFinitePositive(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;
    }
}
