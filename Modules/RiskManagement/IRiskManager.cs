using MT5TradingBot.Models;

namespace MT5TradingBot.Modules.RiskManagement
{
    public interface IRiskManager
    {
        Task<RiskValidationResult> ValidateAsync(
            TradeRequest request,
            AccountInfo account,
            SymbolInfo? symbolInfo,
            IReadOnlyList<LivePosition> openPositions,
            BotConfig config,
            CancellationToken cancellationToken = default);
    }
}
