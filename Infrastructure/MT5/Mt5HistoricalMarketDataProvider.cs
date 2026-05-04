using MT5TradingBot.Modules.Backtesting;
using MT5TradingBot.Modules.MarketData;

namespace MT5TradingBot.Modules.BrokerIntegration
{
    public sealed class Mt5HistoricalMarketDataProvider : IHistoricalMarketDataProvider
    {
        private readonly MT5Bridge _bridge;

        public Mt5HistoricalMarketDataProvider(MT5Bridge bridge)
        {
            _bridge = bridge;
        }

        public async Task<HistoricalMarketDataProviderResult<BacktestTick>> GetTicksAsync(
            string symbol,
            DateTime fromUtc,
            DateTime toUtc,
            int maxRows,
            CancellationToken cancellationToken = default)
        {
            var result = await _bridge.TryGetHistoricalTicksAsync(symbol, fromUtc, toUtc, maxRows)
                .ConfigureAwait(false);

            return result.Success
                ? HistoricalMarketDataProviderResult<BacktestTick>.Ok(result.Ticks)
                : HistoricalMarketDataProviderResult<BacktestTick>.Fail(result.Error);
        }

        public async Task<HistoricalMarketDataProviderResult<BacktestOhlcCandle>> GetOhlcM1Async(
            string symbol,
            DateTime fromUtc,
            DateTime toUtc,
            int maxRows,
            CancellationToken cancellationToken = default)
        {
            var result = await _bridge.TryGetHistoricalRatesAsync(symbol, "M1", fromUtc, toUtc, maxRows)
                .ConfigureAwait(false);

            return result.Success
                ? HistoricalMarketDataProviderResult<BacktestOhlcCandle>.Ok(result.Candles)
                : HistoricalMarketDataProviderResult<BacktestOhlcCandle>.Fail(result.Error);
        }
    }
}
