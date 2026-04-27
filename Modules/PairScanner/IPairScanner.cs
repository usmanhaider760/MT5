using MT5TradingBot.Models;

namespace MT5TradingBot.Modules.PairScanner
{
    public interface IPairScanner
    {
        Task<IReadOnlyList<PairScanResult>> ScanAsync(
            IEnumerable<string> pairs,
            BotConfig config,
            CancellationToken cancellationToken = default);
    }
}
