using MT5TradingBot.Models;

namespace MT5TradingBot.Modules.MarketData
{
    public interface IMarketDataService
    {
        Task<AccountInfo?> GetAccountInfoAsync(CancellationToken cancellationToken = default);
        Task<SymbolInfo?> GetSymbolInfoAsync(string symbol, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<LivePosition>> GetOpenPositionsAsync(CancellationToken cancellationToken = default);
    }
}
