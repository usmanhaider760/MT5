using MT5TradingBot.Models;
using MT5TradingBot.Modules.MarketData;
using Serilog;

namespace MT5TradingBot.Modules.StrategyEngine
{
    public sealed class StrategyEngine : IStrategyEngine
    {
        private readonly IMarketDataService _marketData;

        public StrategyEngine(IMarketDataService marketData) => _marketData = marketData;

        public async Task<MarketSignal> CreateInitialSignalAsync(
            IReadOnlyList<PairScanResult> scannedPairs,
            BotConfig config,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var candidate = scannedPairs
                .Where(p => p.IsAvailable)
                .OrderByDescending(p => p.Score)
                .ThenBy(p => p.SpreadPips)
                .FirstOrDefault();

            if (candidate == null)
                return Hold("No available pair passed the scanner.");

            try
            {
                var info = await _marketData.GetSymbolInfoAsync(candidate.Pair, cancellationToken)
                    .ConfigureAwait(false);

                if (info == null || info.Ask <= 0 || info.Bid <= 0)
                    return Hold($"Market data unavailable for {candidate.Pair}.");

                double mid = (info.Ask + info.Bid) / 2.0;
                double pipSize = GetPipSize(candidate.Pair);
                double stopDistance = Math.Max(15 * pipSize, info.SpreadPips * 3 * pipSize);
                double takeProfitDistance = stopDistance * Math.Max(config.MinRRRatio, 1.5);

                return new MarketSignal
                {
                    Pair = candidate.Pair,
                    Direction = SignalDirection.Hold,
                    OrderType = OrderType.MARKET,
                    EntryPrice = mid,
                    StopLoss = Math.Round(mid - stopDistance, info.Digits),
                    TakeProfit = Math.Round(mid + takeProfitDistance, info.Digits),
                    Source = "StrategyEngine",
                    Reason = "Pair passed scanner. Direction remains HOLD until AI/user decision confirms setup."
                };
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Strategy signal creation failed for {Pair}", candidate.Pair);
                return Hold($"Strategy failed for {candidate.Pair}: {ex.Message}");
            }
        }

        private static MarketSignal Hold(string reason) =>
            new()
            {
                Direction = SignalDirection.Hold,
                Source = "StrategyEngine",
                Reason = reason
            };

        private static double GetPipSize(string symbol)
        {
            string s = symbol.ToUpperInvariant();
            if (s.Contains("JPY")) return 0.01;
            if (s.Contains("XAU") || s.Contains("GOLD")) return 0.1;
            if (s.Contains("XAG")) return 0.01;
            if (s.Contains("BTC") || s.Contains("ETH")) return 1.0;
            return 0.0001;
        }
    }
}
