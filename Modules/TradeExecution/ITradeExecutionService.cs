using MT5TradingBot.Models;

namespace MT5TradingBot.Modules.TradeExecution
{
    public interface ITradeExecutionService
    {
        Task<TradeResult> ExecuteAsync(
            TradeRequest request,
            RiskValidationResult riskResult,
            UserApprovalDecision approval,
            CancellationToken cancellationToken = default);
    }
}
