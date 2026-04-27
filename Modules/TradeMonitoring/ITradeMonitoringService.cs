using MT5TradingBot.Models;

namespace MT5TradingBot.Modules.TradeMonitoring
{
    public interface ITradeMonitoringService
    {
        Task<TradeMonitoringSnapshot> CaptureSnapshotAsync(
            CancellationToken cancellationToken = default);
    }
}
