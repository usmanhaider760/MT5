using MT5TradingBot.Models;

namespace MT5TradingBot.Modules.NewsFilter
{
    public interface INewsCalendarService
    {
        Task<NewsRiskSnapshot> GetRiskSnapshotAsync(
            string pair,
            ApiIntegrationConfig config,
            CancellationToken cancellationToken = default);
    }
}
