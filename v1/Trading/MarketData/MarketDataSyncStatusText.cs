using System.Globalization;

namespace MT5TradingBot.Modules.MarketData
{
    public static class MarketDataSyncStatusText
    {
        public const string Disabled = "Market data auto-sync disabled";
        public const string Starting = "Market data sync: starting...";
        public const string Mt5Unavailable = "MT5 unavailable; market data sync failed";
        public const string NoData = "No market data available";

        public static string Format(HistoricalMarketDataSyncProgress progress)
        {
            string message = string.IsNullOrWhiteSpace(progress.Message)
                ? progress.Status.ToString()
                : progress.Message;

            if (string.Equals(message, Disabled, StringComparison.Ordinal) ||
                string.Equals(message, Starting, StringComparison.Ordinal) ||
                string.Equals(message, Mt5Unavailable, StringComparison.Ordinal) ||
                string.Equals(message, NoData, StringComparison.Ordinal))
                return message;

            int percent = Math.Clamp(progress.Percent, 0, 100);
            string symbol = string.IsNullOrWhiteSpace(progress.Symbol) ? "-" : progress.Symbol;
            string updated = progress.LastUpdatedUtc.HasValue
                ? progress.LastUpdatedUtc.Value.ToString("u", CultureInfo.InvariantCulture)
                : "-";

            return $"Market data sync: {progress.Status} | {symbol} | {progress.DataType} | {percent}% | rows {progress.RowsFetched} | updated {updated} | {message}";
        }
    }
}
