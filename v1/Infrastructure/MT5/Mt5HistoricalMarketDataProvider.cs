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
                ? HistoricalMarketDataProviderResult<BacktestTick>.Ok(
                    result.Ticks,
                    "GET_TICKS",
                    $"MT5Bridge.TryGetHistoricalTicksAsync called GET_TICKS and parsed {result.Ticks.Count} rows.")
                : HistoricalMarketDataProviderResult<BacktestTick>.Fail(
                    result.Error,
                    "GET_TICKS",
                    0,
                    "MT5Bridge.TryGetHistoricalTicksAsync called GET_TICKS but MT5/EA returned an error.");
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
                ? HistoricalMarketDataProviderResult<BacktestOhlcCandle>.Ok(
                    result.Candles,
                    "GET_RATES",
                    $"MT5Bridge.TryGetHistoricalRatesAsync called GET_RATES and parsed {result.Candles.Count} rows.")
                : HistoricalMarketDataProviderResult<BacktestOhlcCandle>.Fail(
                    result.Error,
                    "GET_RATES",
                    0,
                    "MT5Bridge.TryGetHistoricalRatesAsync called GET_RATES but MT5/EA returned an error.");
        }
    }
}
