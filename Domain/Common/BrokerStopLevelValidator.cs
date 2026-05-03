using MT5TradingBot.Models;

namespace MT5TradingBot.Core
{
    public readonly record struct BrokerStopLevelValidation(
        bool Success,
        bool DataUnavailable,
        string Message,
        double StopLevelPips,
        double StopLossDistancePips,
        double TakeProfitDistancePips);

    public static class BrokerStopLevelValidator
    {
        public static BrokerStopLevelValidation Validate(
            TradeRequest request,
            SymbolInfo? symbolInfo,
            double livePrice)
        {
            if (symbolInfo == null)
                return DataUnavailable("Symbol metadata is unavailable for broker stop-level validation.");

            if (!symbolInfo.StopLevelPoints.HasValue ||
                double.IsNaN(symbolInfo.StopLevelPoints.Value) ||
                double.IsInfinity(symbolInfo.StopLevelPoints.Value) ||
                symbolInfo.StopLevelPoints.Value < 0)
            {
                return DataUnavailable("Broker stop-level points are unavailable.");
            }

            double? pointSize = symbolInfo.EffectivePointSize;
            if (!pointSize.HasValue || pointSize.Value <= 0)
                return DataUnavailable("Broker point size is unavailable for stop-level validation.");

            double referencePrice = request.OrderType == OrderType.MARKET
                ? livePrice
                : request.EntryPrice > 0
                    ? request.EntryPrice
                    : livePrice;

            if (!IsFinitePositive(referencePrice))
                return DataUnavailable("Reference price is unavailable for stop-level validation.");

            if (!IsFinitePositive(request.StopLoss) || !IsFinitePositive(request.TakeProfit))
                return DataUnavailable("SL/TP data is unavailable for stop-level validation.");

            double stopDistancePrice = symbolInfo.StopLevelPoints.Value * pointSize.Value;
            double pipSize = symbolInfo.Digits is 3 or 5
                ? pointSize.Value * 10.0
                : pointSize.Value;
            if (!IsFinitePositive(pipSize))
                return DataUnavailable("Pip size is unavailable for stop-level validation.");

            double stopLevelPips = stopDistancePrice / pipSize;
            double slDistancePips = Math.Abs(referencePrice - request.StopLoss) / pipSize;
            double tpDistancePips = Math.Abs(request.TakeProfit - referencePrice) / pipSize;

            if (slDistancePips + 1e-9 < stopLevelPips)
            {
                return Violation(
                    $"Stop-loss distance {slDistancePips:F1} pips is below broker stop level {stopLevelPips:F1} pips.",
                    stopLevelPips,
                    slDistancePips,
                    tpDistancePips);
            }

            if (tpDistancePips + 1e-9 < stopLevelPips)
            {
                return Violation(
                    $"Take-profit distance {tpDistancePips:F1} pips is below broker stop level {stopLevelPips:F1} pips.",
                    stopLevelPips,
                    slDistancePips,
                    tpDistancePips);
            }

            return new BrokerStopLevelValidation(
                true,
                false,
                "",
                stopLevelPips,
                slDistancePips,
                tpDistancePips);
        }

        private static BrokerStopLevelValidation DataUnavailable(string message) =>
            new(false, true, message, 0, 0, 0);

        private static BrokerStopLevelValidation Violation(
            string message,
            double stopLevelPips,
            double stopLossDistancePips,
            double takeProfitDistancePips) =>
            new(false, false, message, stopLevelPips, stopLossDistancePips, takeProfitDistancePips);

        private static bool IsFinitePositive(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;
    }
}
