using MT5TradingBot.Models;
using MT5TradingBot.Modules.BrokerIntegration;
using Serilog;

namespace MT5TradingBot.Modules.MarketData
{
    public sealed class MarketDataService : IMarketDataService
    {
        private readonly MT5Bridge _bridge;

        public MarketDataService(MT5Bridge bridge) => _bridge = bridge;

        public async Task<AccountInfo?> GetAccountInfoAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await _bridge.GetAccountInfoAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Market data account request failed");
                return null;
            }
        }

        public async Task<SymbolInfo?> GetSymbolInfoAsync(
            string symbol,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(symbol))
                return null;

            try
            {
                return await _bridge.GetSymbolInfoAsync(symbol.Trim().ToUpperInvariant())
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Market data symbol request failed for {Symbol}", symbol);
                return null;
            }
        }

        public async Task<IReadOnlyList<LivePosition>> GetOpenPositionsAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await _bridge.GetPositionsAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Market data positions request failed");
                return [];
            }
        }
    }
}
