using MT5TradingBot.Models;

namespace MT5TradingBot.Core
{
    public readonly record struct BrokerLotSizeValidation(
        bool Success,
        bool DataUnavailable,
        string Message,
        double LotSize,
        double MinLot,
        double MaxLot,
        double LotStep,
        double VolumeLimit);

    public static class BrokerLotSizeValidator
    {
        public static double Normalize(double lotSize, SymbolInfo? symbolInfo)
        {
            double minLot = symbolInfo != null && IsFinitePositive(symbolInfo.MinLot) ? symbolInfo.MinLot : 0.01;
            double maxLot = symbolInfo != null && IsFinitePositive(symbolInfo.MaxLot) ? symbolInfo.MaxLot : 100.0;
            double lotStep = symbolInfo?.LotStep.HasValue == true && IsFinitePositive(symbolInfo.LotStep.Value)
                ? symbolInfo.LotStep.Value
                : 0.01;
            if (maxLot < minLot)
                maxLot = minLot;

            if (!IsFinitePositive(lotSize))
                lotSize = minLot;

            lotSize = Math.Min(Math.Max(lotSize, minLot), maxLot);

            double steps = Math.Floor((lotSize - minLot) / lotStep + 1e-9);
            double normalized = minLot + steps * lotStep;
            normalized = Math.Min(Math.Max(normalized, minLot), maxLot);

            return Math.Round(normalized, LotStepDecimals(lotStep));
        }

        public static BrokerLotSizeValidation Validate(double lotSize, SymbolInfo? symbolInfo)
        {
            if (symbolInfo == null)
                return DataUnavailable("Symbol lot metadata is unavailable.");

            if (!IsFinitePositive(lotSize))
                return DataUnavailable("Final lot size is unavailable for broker lot validation.");

            if (!IsFinitePositive(symbolInfo.MinLot) ||
                !IsFinitePositive(symbolInfo.MaxLot) ||
                !symbolInfo.LotStep.HasValue ||
                !IsFinitePositive(symbolInfo.LotStep.Value) ||
                symbolInfo.MaxLot < symbolInfo.MinLot)
            {
                return DataUnavailable("Broker lot min/max/step metadata is unavailable.");
            }

            double minLot = symbolInfo.MinLot;
            double maxLot = symbolInfo.MaxLot;
            double lotStep = symbolInfo.LotStep.Value;
            double volumeLimit = symbolInfo.VolumeLimit.GetValueOrDefault();

            if (symbolInfo.VolumeLimit.HasValue &&
                !IsFiniteNonNegative(symbolInfo.VolumeLimit.Value))
            {
                return DataUnavailable("Broker volume limit metadata is unavailable.");
            }

            if (lotSize + 1e-9 < minLot)
            {
                return Violation(
                    $"Lot size {lotSize:F2} is below broker minimum {minLot:F2}.",
                    lotSize,
                    minLot,
                    maxLot,
                    lotStep,
                    volumeLimit);
            }

            if (lotSize - 1e-9 > maxLot)
            {
                return Violation(
                    $"Lot size {lotSize:F2} is above broker maximum {maxLot:F2}.",
                    lotSize,
                    minLot,
                    maxLot,
                    lotStep,
                    volumeLimit);
            }

            if (IsFinitePositive(volumeLimit) && lotSize - 1e-9 > volumeLimit)
            {
                return Violation(
                    $"Lot size {lotSize:F2} is above broker volume limit {volumeLimit:F2}.",
                    lotSize,
                    minLot,
                    maxLot,
                    lotStep,
                    volumeLimit);
            }

            double stepsFromMinimum = (lotSize - minLot) / lotStep;
            double nearestStep = Math.Round(stepsFromMinimum);
            if (Math.Abs(stepsFromMinimum - nearestStep) > 1e-6)
            {
                return Violation(
                    $"Lot size {lotSize:F2} does not align with broker lot step {lotStep:F2}.",
                    lotSize,
                    minLot,
                    maxLot,
                    lotStep,
                    volumeLimit);
            }

            return new BrokerLotSizeValidation(
                true,
                false,
                "",
                lotSize,
                minLot,
                maxLot,
                lotStep,
                volumeLimit);
        }

        private static BrokerLotSizeValidation DataUnavailable(string message) =>
            new(false, true, message, 0, 0, 0, 0, 0);

        private static BrokerLotSizeValidation Violation(
            string message,
            double lotSize,
            double minLot,
            double maxLot,
            double lotStep,
            double volumeLimit) =>
            new(false, false, message, lotSize, minLot, maxLot, lotStep, volumeLimit);

        private static bool IsFinitePositive(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;

        private static bool IsFiniteNonNegative(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0;

        private static int LotStepDecimals(double lotStep)
        {
            int decimals = 0;
            while (decimals < 8 && Math.Abs(lotStep - Math.Round(lotStep, decimals)) > 1e-9)
                decimals++;
            return decimals;
        }
    }
}
