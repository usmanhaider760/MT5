using MT5TradingBot.Models;

namespace MT5TradingBot.Modules.NormalTrading
{
    public sealed class NormalTradeManager
    {
        public bool IsRunning { get; private set; }
        public NormalTradingSettings Settings { get; private set; } = new();

        public void Start(NormalTradingSettings settings)
        {
            Settings = settings;
            IsRunning = true;
        }

        public void Stop() => IsRunning = false;
    }
}
