using MT5TradingBot.Models;

namespace MT5TradingBot.Modules.NewsFilter
{
    public sealed class NewsFilterService : INewsFilterService
    {
        public Task<NewsFilterResult> CheckAsync(
            string pair,
            IEnumerable<NewsEvent> knownEvents,
            TimeSpan blackoutWindow,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(pair))
                return Task.FromResult(Blocked("Pair is required for news filtering."));

            string normalizedPair = pair.Trim().ToUpperInvariant();
            var currencies = ExtractCurrencies(normalizedPair);
            DateTime now = DateTime.UtcNow;

            var blockingEvents = knownEvents
                .Where(e => IsHighImpact(e))
                .Where(e => currencies.Contains(e.Currency.Trim().ToUpperInvariant()))
                .Where(e => Math.Abs((e.EventTimeUtc - now).TotalMinutes) <= blackoutWindow.TotalMinutes)
                .OrderBy(e => e.EventTimeUtc)
                .ToList();

            if (blockingEvents.Count == 0)
                return Task.FromResult(new NewsFilterResult
                {
                    IsBlocked = false,
                    Reason = "No high-impact news event inside blackout window.",
                    BlockingEvents = []
                });

            return Task.FromResult(new NewsFilterResult
            {
                IsBlocked = true,
                Reason = $"High-impact news event inside +/- {blackoutWindow.TotalMinutes:F0} min blackout window.",
                BlockingEvents = blockingEvents
            });
        }

        private static NewsFilterResult Blocked(string reason) =>
            new()
            {
                IsBlocked = true,
                Reason = reason,
                BlockingEvents = []
            };

        private static HashSet<string> ExtractCurrencies(string pair)
        {
            string compact = pair.Replace("/", "").Replace("_", "");
            if (compact.Length < 6) return [];

            return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                compact[..3],
                compact.Substring(3, 3)
            };
        }

        private static bool IsHighImpact(NewsEvent newsEvent) =>
            string.Equals(newsEvent.Impact, "High", StringComparison.OrdinalIgnoreCase);
    }
}
