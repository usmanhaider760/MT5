using MT5TradingBot.Core;
using MT5TradingBot.Models;

namespace MT5TradingBot.Modules.Backtesting
{
    public sealed record HistoricalNewsEvent
    {
        public DateTime TimestampUtc { get; init; }
        public string Currency { get; init; } = "";
        public string Symbol { get; init; } = "";
        public string Impact { get; init; } = "";
        public string Title { get; init; } = "";
        public int? BlackoutBeforeMinutes { get; init; }
        public int? BlackoutAfterMinutes { get; init; }
    }

    public sealed record BacktestNoTradeFilterInput
    {
        public DateTime TimestampUtc { get; init; }
        public string Symbol { get; init; } = "";
        public double? SpreadPips { get; init; }
        public BotConfig Config { get; init; } = new();
        public ApiIntegrationConfig? NewsConfig { get; init; }
        public IReadOnlyList<HistoricalNewsEvent>? HistoricalNewsEvents { get; init; }
    }

    public sealed record BacktestNoTradeFilterResult
    {
        public bool Allowed { get; init; }
        public string RejectionCode { get; init; } = "";
        public string RejectionReason { get; init; } = "";
        public string MatchedFilterName { get; init; } = "";
        public string MatchedFilterType { get; init; } = "";
        public IReadOnlyList<string> Warnings { get; init; } = [];
    }

    public static class BacktestNoTradeFilterSimulator
    {
        public static BacktestNoTradeFilterResult Evaluate(BacktestNoTradeFilterInput input)
        {
            var warnings = new List<string>();
            DateTime timestampUtc = EnsureUtc(input.TimestampUtc);

            if (string.IsNullOrWhiteSpace(input.Symbol))
                return Reject("BACKTEST_FILTER_DATA_UNAVAILABLE", "Symbol is required for backtest filter simulation.", "", "Data");

            var noTrade = NoTradeWindowValidator.Validate(input.Config, timestampUtc);
            if (!noTrade.Success)
            {
                return Reject(
                    noTrade.InvalidConfig ? "BACKTEST_NO_TRADE_CONFIG_INVALID" : "BACKTEST_NO_TRADE_WINDOW",
                    noTrade.Message,
                    noTrade.WindowName,
                    noTrade.InvalidConfig ? "InvalidConfig" : "NoTradeWindow");
            }

            if (input.Config.EnableSessionSpreadProtection)
            {
                if (!input.SpreadPips.HasValue || !IsFiniteNonNegative(input.SpreadPips.Value))
                {
                    return Reject(
                        "BACKTEST_SPREAD_DATA_UNAVAILABLE",
                        "Session spread protection is enabled but historical spread data is unavailable.",
                        "session-spread",
                        "SessionSpread");
                }

                var spreadSymbol = SpreadOnlySymbolInfo(input.Symbol, input.SpreadPips.Value);
                var sessionSpread = SessionSpreadValidator.Validate(
                    input.Config,
                    spreadSymbol,
                    input.Symbol,
                    timestampUtc);

                if (!sessionSpread.Success)
                {
                    return Reject(
                        sessionSpread.InvalidConfig ? "BACKTEST_SESSION_SPREAD_CONFIG_INVALID" : "BACKTEST_SESSION_SPREAD_LIMIT",
                        sessionSpread.Message,
                        string.IsNullOrWhiteSpace(sessionSpread.RuleName) ? "session-spread" : sessionSpread.RuleName,
                        sessionSpread.InvalidConfig ? "InvalidConfig" : "SessionSpread");
                }
            }

            var newsConfig = input.NewsConfig;
            if (newsConfig is { BlockTradesOnHighImpactNews: true })
            {
                if (input.HistoricalNewsEvents == null)
                {
                    string message = "Historical news data is unavailable for enabled news blackout simulation.";
                    if (newsConfig.BlockTradesWhenNewsUnavailable)
                        return Reject("BACKTEST_NEWS_DATA_UNAVAILABLE", message, "news", "News");

                    warnings.Add(message);
                }
                else
                {
                    var blocked = FindBlockingNews(input, newsConfig, timestampUtc);
                    if (blocked != null)
                    {
                        string name = string.IsNullOrWhiteSpace(blocked.Title)
                            ? blocked.Currency
                            : blocked.Title;
                        return Reject(
                            "BACKTEST_NEWS_BLACKOUT",
                            $"{input.Symbol} is inside historical news blackout for {name}.",
                            name,
                            "News");
                    }
                }
            }

            return new BacktestNoTradeFilterResult
            {
                Allowed = true,
                Warnings = warnings
            };
        }

        private static HistoricalNewsEvent? FindBlockingNews(
            BacktestNoTradeFilterInput input,
            ApiIntegrationConfig config,
            DateTime timestampUtc)
        {
            foreach (var newsEvent in input.HistoricalNewsEvents ?? [])
            {
                if (!IsRelevantNews(input.Symbol, newsEvent))
                    continue;

                if (!ImpactMatches(config.NewsImpactFilter, newsEvent.Impact))
                    continue;

                int before = Math.Max(0, newsEvent.BlackoutBeforeMinutes ?? config.NewsBlackoutBeforeMinutes);
                int after = Math.Max(0, newsEvent.BlackoutAfterMinutes ?? config.NewsBlackoutAfterMinutes);
                DateTime eventUtc = EnsureUtc(newsEvent.TimestampUtc);
                DateTime start = eventUtc.AddMinutes(-before);
                DateTime end = eventUtc.AddMinutes(after);

                if (timestampUtc >= start && timestampUtc <= end)
                    return newsEvent;
            }

            return null;
        }

        private static bool IsRelevantNews(string tradeSymbol, HistoricalNewsEvent newsEvent)
        {
            string normalizedTradeSymbol = PipCalculator.NormalizeSymbol(tradeSymbol);
            string eventSymbol = PipCalculator.NormalizeSymbol(newsEvent.Symbol);
            if (!string.IsNullOrWhiteSpace(eventSymbol) &&
                string.Equals(normalizedTradeSymbol, eventSymbol, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string currency = newsEvent.Currency.Trim().ToUpperInvariant();
            if (currency.Length != 3 || normalizedTradeSymbol.Length < 6)
                return false;

            string baseCurrency = normalizedTradeSymbol[..3].ToUpperInvariant();
            string quoteCurrency = normalizedTradeSymbol.Substring(3, 3).ToUpperInvariant();
            return currency == baseCurrency || currency == quoteCurrency;
        }

        private static bool ImpactMatches(string configuredImpactFilter, string impact)
        {
            string filter = configuredImpactFilter.Trim();
            string normalizedImpact = impact.Trim();

            if (string.IsNullOrWhiteSpace(filter) ||
                filter.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                return !string.IsNullOrWhiteSpace(normalizedImpact);
            }

            if (filter.Contains("High", StringComparison.OrdinalIgnoreCase))
                return normalizedImpact.Equals("High", StringComparison.OrdinalIgnoreCase);

            return normalizedImpact.Equals(filter, StringComparison.OrdinalIgnoreCase);
        }

        private static SymbolInfo SpreadOnlySymbolInfo(string symbol, double spreadPips) => new()
        {
            Symbol = symbol,
            Digits = 5,
            Spread = spreadPips * 10.0,
            Ask = 1.10000 + spreadPips * 0.00005,
            Bid = 1.10000 - spreadPips * 0.00005,
            MinLot = 0.01,
            MaxLot = 100,
            LotStep = 0.01,
            VolumeLimit = 0,
            PointSize = 0.00001,
            StopLevelPoints = 0,
            FreezeLevelPoints = 0
        };

        private static BacktestNoTradeFilterResult Reject(
            string code,
            string reason,
            string filterName,
            string filterType) => new()
        {
            Allowed = false,
            RejectionCode = code,
            RejectionReason = reason,
            MatchedFilterName = filterName,
            MatchedFilterType = filterType
        };

        private static DateTime EnsureUtc(DateTime timestamp) =>
            timestamp.Kind == DateTimeKind.Utc
                ? timestamp
                : DateTime.SpecifyKind(timestamp, DateTimeKind.Utc);

        private static bool IsFiniteNonNegative(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0;
    }
}
