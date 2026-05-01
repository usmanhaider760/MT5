using System.Collections.Concurrent;

namespace MT5TradingBot.Services
{
    public sealed class AiRegimeState
    {
        public string Pair      { get; init; } = "";
        public string Direction { get; init; } = "";  // "BUY", "SELL", "NO_TRADE"
        public string Reason    { get; init; } = "";
        public DateTime CapturedAt { get; init; } = DateTime.UtcNow;
    }

    public interface IAiContextManager
    {
        /// <summary>Record the latest Claude decision for a pair.</summary>
        void Update(string pair, string direction, string reason);

        /// <summary>
        /// Returns the cached state for a pair if it was recorded within
        /// maxAge, otherwise null (cache miss / stale).
        /// </summary>
        AiRegimeState? GetCurrent(string pair, TimeSpan maxAge);

        /// <summary>
        /// Returns true if newDirection directly contradicts the cached
        /// direction for the pair within maxAge.
        /// BUY vs SELL and SELL vs BUY are the only conflict pairs.
        /// Same direction, NO_TRADE, or a stale cache never conflict.
        /// </summary>
        bool HasConflict(string pair, string newDirection, TimeSpan maxAge);
    }

    public sealed class AiContextManager : IAiContextManager
    {
        private readonly ConcurrentDictionary<string, AiRegimeState> _cache =
            new(StringComparer.OrdinalIgnoreCase);

        public void Update(string pair, string direction, string reason)
        {
            if (string.IsNullOrWhiteSpace(pair)) return;
            _cache[pair.Trim().ToUpperInvariant()] = new AiRegimeState
            {
                Pair      = pair.Trim().ToUpperInvariant(),
                Direction = (direction ?? "").Trim().ToUpperInvariant(),
                Reason    = reason ?? "",
                CapturedAt = DateTime.UtcNow
            };
        }

        public AiRegimeState? GetCurrent(string pair, TimeSpan maxAge)
        {
            if (string.IsNullOrWhiteSpace(pair)) return null;
            if (!_cache.TryGetValue(pair.Trim().ToUpperInvariant(), out var state))
                return null;
            return DateTime.UtcNow - state.CapturedAt <= maxAge ? state : null;
        }

        public bool HasConflict(string pair, string newDirection, TimeSpan maxAge)
        {
            var cached = GetCurrent(pair, maxAge);
            if (cached == null) return false;  // no cache or stale -- no conflict

            string nd = (newDirection ?? "").Trim().ToUpperInvariant();
            string cd = cached.Direction;

            // Only BUY<->SELL transitions are conflicts
            return (nd == "BUY"  && cd == "SELL") ||
                   (nd == "SELL" && cd == "BUY");
        }
    }
}
