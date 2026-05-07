using MT5TradingBot.Models;
using MT5TradingBot.Modules.MarketData;
using Serilog;

namespace MT5TradingBot.Modules.PairScanner
{
    public sealed class PairScanner : IPairScanner
    {
        private readonly IMarketDataService _marketData;

        public PairScanner(IMarketDataService marketData) => _marketData = marketData;

        public async Task<IReadOnlyList<PairScanResult>> ScanAsync(
            IEnumerable<string> pairs,
            BotConfig config,
            CancellationToken cancellationToken = default)
        {
            var symbols = pairs
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim().ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var results = new List<PairScanResult>(symbols.Count);

            foreach (string pair in symbols)
            {
                cancellationToken.ThrowIfCancellationRequested();
                results.Add(await ScanPairAsync(pair, config, cancellationToken).ConfigureAwait(false));
            }

            return results
                .OrderByDescending(r => r.IsAvailable)
                .ThenByDescending(r => r.Score)
                .ThenBy(r => r.SpreadPips)
                .ToList();
        }

        private async Task<PairScanResult> ScanPairAsync(
            string pair,
            BotConfig config,
            CancellationToken cancellationToken)
        {
            try
            {
                var info = await _marketData.GetSymbolInfoAsync(pair, cancellationToken)
                    .ConfigureAwait(false);

                if (info == null)
                    return Unavailable(pair, "Symbol data unavailable.");

                if (config.AllowedPairs.Count > 0 &&
                    !config.AllowedPairs.Any(p => string.Equals(p, pair, StringComparison.OrdinalIgnoreCase)))
                    return Unavailable(pair, "Pair is not in the configured allowlist.", info.SpreadPips);

                if (config.MaxSpreadPips > 0 && info.SpreadPips > config.MaxSpreadPips)
                    return new PairScanResult
                    {
                        Pair = pair,
                        IsAvailable = false,
                        SpreadPips = info.SpreadPips,
                        Score = 0,
                        Reason = $"Spread {info.SpreadPips:F1} pips exceeds max {config.MaxSpreadPips:F1}."
                    };

                double score = config.MaxSpreadPips > 0
                    ? Math.Max(0, 100 - (info.SpreadPips / config.MaxSpreadPips * 100))
                    : Math.Max(0, 100 - info.SpreadPips);

                return new PairScanResult
                {
                    Pair = pair,
                    IsAvailable = true,
                    SpreadPips = info.SpreadPips,
                    Score = Math.Round(score, 2),
                    Reason = "Pair available."
                };
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Pair scan failed for {Pair}", pair);
                return Unavailable(pair, ex.Message);
            }
        }

        private static PairScanResult Unavailable(
            string pair,
            string reason,
            double spreadPips = 0) =>
            new()
            {
                Pair = pair,
                IsAvailable = false,
                SpreadPips = spreadPips,
                Score = 0,
                Reason = reason
            };
    }
}
