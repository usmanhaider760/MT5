using MT5TradingBot.Models;

namespace MT5TradingBot.Modules.MarketData
{
    public sealed record HistoricalMarketDataCliOptions
    {
        public IReadOnlyList<string> Symbols { get; init; } = [];
        public int? LookbackDays { get; init; }
        public string? DataDirectory { get; init; }
        public MarketDataUpdateType? PreferredDataType { get; init; }

        public static HistoricalMarketDataCliOptions Parse(string[] args)
        {
            return new HistoricalMarketDataCliOptions
            {
                Symbols = SplitSymbols(Value(args, "--symbols")),
                LookbackDays = int.TryParse(Value(args, "--lookback-days"), out int days) ? days : null,
                DataDirectory = Value(args, "--data-dir"),
                PreferredDataType = ParseType(Value(args, "--type"))
            };
        }

        public HistoricalMarketDataUpdateRequest ToRequest(BotConfig config) =>
            HistoricalMarketDataUpdater.FromConfig(config, this);

        private static IReadOnlyList<string> SplitSymbols(string? symbols) =>
            string.IsNullOrWhiteSpace(symbols)
                ? []
                : symbols.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(s => s.Replace("/", "", StringComparison.Ordinal).ToUpperInvariant())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

        private static MarketDataUpdateType? ParseType(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            return value.Trim().ToLowerInvariant() switch
            {
                "tick" or "ticks" => MarketDataUpdateType.Tick,
                "ohlc" or "rates" or "m1" => MarketDataUpdateType.OHLC,
                "tick-then-ohlc" or "ticks-then-ohlc" or "tickthenohlc" => MarketDataUpdateType.TickThenOHLC,
                _ => null
            };
        }

        private static string? Value(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            }

            return null;
        }
    }
}
