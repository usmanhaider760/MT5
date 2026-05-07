using MT5TradingBot.Models;

namespace MT5TradingBot.Modules.LiveReadiness
{
    public interface IBrokerDeploymentChecklist
    {
        Task<BrokerDeploymentChecklistResult> CheckAsync(
            TradeRequest request,
            BotConfig config,
            CancellationToken cancellationToken = default);
    }
}
