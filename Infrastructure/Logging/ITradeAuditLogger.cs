using MT5TradingBot.Models;

namespace MT5TradingBot.Modules.LoggingDiagnostics
{
    public interface ITradeAuditLogger
    {
        Task LogAsync(TradeAuditEntry entry, CancellationToken cancellationToken = default);
    }
}
