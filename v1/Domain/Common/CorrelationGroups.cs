namespace MT5TradingBot.Core
{
    /// <summary>
    /// Defines currency pairs that share a common exposure and should not
    /// be traded simultaneously. Groups are checked symmetrically: if pair A
    /// blocks pair B, then pair B also blocks pair A.
    /// </summary>
    internal static class CorrelationGroups
    {
        private static readonly List<HashSet<string>> Groups =
        [
            // USD-quoted majors: all move with USD sentiment
            new(StringComparer.OrdinalIgnoreCase) { "EURUSD", "GBPUSD", "AUDUSD", "NZDUSD" },
            // USD-base majors: all move inversely with USD sentiment
            new(StringComparer.OrdinalIgnoreCase) { "USDJPY", "USDCHF", "USDCAD" },
            // Precious metals
            new(StringComparer.OrdinalIgnoreCase) { "XAUUSD", "XAGUSD" }
        ];

        /// <summary>
        /// Returns the symbol of an already-open position that is correlated
        /// with <paramref name="newPair"/>, or null if no conflict exists.
        /// Strips broker suffixes (e.g. "GBPUSDm" → "GBPUSD") before comparing.
        /// </summary>
        public static string? FindBlockingSymbol(
            string newPair,
            IEnumerable<string> openSymbols,
            string symbolSuffix)
        {
            string normNew = Normalize(newPair, symbolSuffix);

            var group = Groups.FirstOrDefault(
                g => g.Contains(normNew, StringComparer.OrdinalIgnoreCase));

            if (group == null) return null;

            foreach (string open in openSymbols)
            {
                string normOpen = Normalize(open, symbolSuffix);

                // Same pair already open is handled elsewhere; only block
                // a DIFFERENT correlated pair.
                if (string.Equals(normOpen, normNew, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (group.Contains(normOpen, StringComparer.OrdinalIgnoreCase))
                    return open; // return the original symbol for the log message
            }

            return null;
        }

        private static string Normalize(string symbol, string suffix)
        {
            string s = symbol.Trim().ToUpperInvariant()
                             .Replace("/", "").Replace("_", "").Replace(".", "");

            // Strip the configured broker suffix if present and pair
            // would still be exactly 6 characters without it.
            if (!string.IsNullOrEmpty(suffix))
            {
                string sfx = suffix.ToUpperInvariant();
                if (s.EndsWith(sfx, StringComparison.OrdinalIgnoreCase) &&
                    s.Length - sfx.Length == 6)
                {
                    s = s[..^sfx.Length];
                }
            }

            // Fallback: if still longer than 6 chars and char 7+ is not a digit,
            // treat first 6 chars as the base symbol (handles unknown suffixes).
            if (s.Length > 6 && !char.IsDigit(s[6]))
                s = s[..6];

            return s;
        }
    }
}
