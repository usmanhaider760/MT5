using MT5TradingBot.Models;

namespace MT5TradingBot.Modules.StrategyEngine
{
    public interface IStrategyEngine
    {
        Task<MarketSignal> CreateInitialSignalAsync(
            IReadOnlyList<PairScanResult> scannedPairs,
            BotConfig config,
            CancellationToken cancellationToken = default);
    }
}
