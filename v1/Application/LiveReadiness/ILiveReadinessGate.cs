using MT5TradingBot.Models;

namespace MT5TradingBot.Modules.LiveReadiness
{
    public interface ILiveReadinessGate
    {
        LiveReadinessResult Evaluate(BotConfig config, LiveReadinessContext context);
    }
}
