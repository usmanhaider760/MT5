using MT5TradingBot.Models;

namespace MT5TradingBot.Modules.UserApproval
{
    public interface IUserApprovalService
    {
        Task<UserApprovalDecision> RequestApprovalAsync(
            MarketSignal signal,
            RiskValidationResult riskResult,
            CancellationToken cancellationToken = default);
    }
}
