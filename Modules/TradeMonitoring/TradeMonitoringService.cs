using MT5TradingBot.Models;
using MT5TradingBot.Modules.MarketData;
using Serilog;

namespace MT5TradingBot.Modules.TradeMonitoring
{
    public sealed class TradeMonitoringService : ITradeMonitoringService
    {
        private readonly IMarketDataService _marketData;

        public TradeMonitoringService(IMarketDataService marketData) => _marketData = marketData;

        public async Task<TradeMonitoringSnapshot> CaptureSnapshotAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var positions = await _marketData.GetOpenPositionsAsync(cancellationToken)
                    .ConfigureAwait(false);

                var events = new List<string>();
                foreach (var position in positions)
                {
                    if (position.StopLoss <= 0)
                        events.Add($"Position #{position.Ticket} {position.Symbol} has no stop loss.");

                    if (position.TakeProfit <= 0)
                        events.Add($"Position #{position.Ticket} {position.Symbol} has no take profit.");
                }

                return new TradeMonitoringSnapshot
                {
                    CapturedAt = DateTime.UtcNow,
                    OpenPositionCount = positions.Count,
                    FloatingProfit = positions.Sum(p => p.Profit),
                    Positions = positions,
                    Events = events
                };
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Trade monitoring snapshot failed");
                return new TradeMonitoringSnapshot
                {
                    CapturedAt = DateTime.UtcNow,
                    Events = [$"Monitoring failed: {ex.Message}"]
                };
            }
        }
    }
}
