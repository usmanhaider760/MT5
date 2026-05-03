using MT5TradingBot.Models;

namespace MT5TradingBot.Core
{
    public readonly record struct SlippageEstimate(
        bool Success,
        double CostUsd,
        double Pips,
        string Error);

    public static class SlippageCalculator
    {
        public static bool IsEnabled(BotConfig config) =>
            config.EnableSlippageModel;

        public static SlippageEstimate EstimateCost(string symbol, double lots, BotConfig config)
        {
            if (!IsEnabled(config))
                return new SlippageEstimate(true, 0, 0, "");

            if (string.IsNullOrWhiteSpace(symbol))
                return new SlippageEstimate(false, 0, 0, "Symbol is unavailable for slippage calculation.");

            if (!IsFinitePositive(lots))
                return new SlippageEstimate(false, 0, 0, "Lot size is unavailable for slippage calculation.");

            string mode = string.IsNullOrWhiteSpace(config.SlippageMode)
                ? "Fixed"
                : config.SlippageMode.Trim();

            if (!mode.Equals("Fixed", StringComparison.OrdinalIgnoreCase) &&
                !mode.Equals("Conservative", StringComparison.OrdinalIgnoreCase))
            {
                return new SlippageEstimate(false, 0, 0, $"Unknown slippage mode '{config.SlippageMode}'.");
            }

            if (!IsFiniteNonNegative(config.EstimatedSlippagePips))
                return new SlippageEstimate(false, 0, 0, "Estimated slippage pips is unavailable.");

            if (!IsFinitePositive(config.MaxAllowedSlippagePips))
                return new SlippageEstimate(false, 0, 0, "Maximum allowed slippage pips is not configured.");

            if (config.EstimatedSlippagePips > config.MaxAllowedSlippagePips)
            {
                return new SlippageEstimate(
                    false,
                    0,
                    config.EstimatedSlippagePips,
                    $"Estimated slippage {config.EstimatedSlippagePips:F1} pips exceeds max {config.MaxAllowedSlippagePips:F1} pips.");
            }

            double pipValue = LotCalculator.GetPipValuePerLot(symbol.ToUpperInvariant());
            if (!IsFinitePositive(pipValue))
                return new SlippageEstimate(false, 0, 0, "Pip value is unavailable for slippage calculation.");

            double cost = Math.Round(config.EstimatedSlippagePips * pipValue * lots, 2);
            return IsFiniteNonNegative(cost)
                ? new SlippageEstimate(true, cost, config.EstimatedSlippagePips, "")
                : new SlippageEstimate(false, 0, 0, "Slippage cost could not be calculated.");
        }

        private static bool IsFinitePositive(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;

        private static bool IsFiniteNonNegative(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0;
    }
}
