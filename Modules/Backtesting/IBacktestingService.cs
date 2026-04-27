using MT5TradingBot.Models;

namespace MT5TradingBot.Modules.Backtesting
{
    public interface IBacktestingService
    {
        Task<BacktestResult> RunAsync(
            IEnumerable<BacktestTrade> trades,
            CancellationToken cancellationToken = default);
    }
}
