using MT5TradingBot.Models;

namespace MT5TradingBot.Modules.NewsFilter
{
    public interface INewsFilterService
    {
        Task<NewsFilterResult> CheckAsync(
            string pair,
            IEnumerable<NewsEvent> knownEvents,
            TimeSpan blackoutWindow,
            CancellationToken cancellationToken = default);
    }
}
