using MT5TradingBot.Models;
using Serilog;

namespace MT5TradingBot.Modules.LoggingDiagnostics
{
    public sealed class TradeAuditLogger : ITradeAuditLogger
    {
        public Task LogAsync(TradeAuditEntry entry, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(entry.Id))
                entry.Id = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

            if (entry.CreatedAt == default)
                entry.CreatedAt = DateTime.UtcNow;

            Log.Information(
                "[TradeAudit] {AuditId} signal={SignalId} action={Action} reason={Reason} input={InputJson} result={ResultJson}",
                entry.Id,
                entry.SignalId,
                entry.Action,
                entry.Reason,
                entry.InputJson,
                entry.ResultJson);

            return Task.CompletedTask;
        }
    }
}
