using MT5TradingBot.Models;

namespace MT5TradingBot.Core
{
    public readonly record struct CommissionEstimate(
        bool Success,
        double Amount,
        string Currency,
        string Error);

    public static class CommissionCalculator
    {
        public static bool IsEnabled(BotConfig config) =>
            config.EnableCommissionModel;

        public static CommissionEstimate EstimateRoundTurn(double lots, BotConfig config)
        {
            string currency = string.IsNullOrWhiteSpace(config.CommissionCurrency)
                ? "USD"
                : config.CommissionCurrency.Trim().ToUpperInvariant();

            if (!IsEnabled(config))
                return new CommissionEstimate(true, 0, currency, "");

            if (!IsFinitePositive(lots))
                return new CommissionEstimate(false, 0, currency, "Lot size is unavailable for commission calculation.");

            if (!IsFinitePositive(config.CommissionPerLotPerSide))
                return new CommissionEstimate(false, 0, currency, "Commission per lot is not configured.");

            string mode = string.IsNullOrWhiteSpace(config.CommissionMode)
                ? "PerSide"
                : config.CommissionMode.Trim();

            double multiplier;
            if (mode.Equals("PerSide", StringComparison.OrdinalIgnoreCase) ||
                mode.Equals("Per-Side", StringComparison.OrdinalIgnoreCase))
            {
                multiplier = 2.0;
            }
            else if (mode.Equals("RoundTurn", StringComparison.OrdinalIgnoreCase) ||
                     mode.Equals("Round-Turn", StringComparison.OrdinalIgnoreCase))
            {
                multiplier = 1.0;
            }
            else
            {
                return new CommissionEstimate(false, 0, currency, $"Unknown commission mode '{config.CommissionMode}'.");
            }

            double commission = Math.Round(lots * config.CommissionPerLotPerSide * multiplier, 2);
            return IsFiniteNonNegative(commission)
                ? new CommissionEstimate(true, commission, currency, "")
                : new CommissionEstimate(false, 0, currency, "Commission could not be calculated.");
        }

        private static bool IsFinitePositive(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;

        private static bool IsFiniteNonNegative(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0;
    }
}
