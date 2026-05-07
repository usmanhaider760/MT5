using MT5TradingBot.Models;

namespace MT5TradingBot.Modules.Scalping
{
    public sealed class ScalpingTradeManager
    {
        public bool IsRunning { get; private set; }
        public ScalpingSettings Settings { get; private set; } = new();

        public void Start(ScalpingSettings settings)
        {
            Settings = settings;
            IsRunning = true;
        }

        public void Stop() => IsRunning = false;
    }
}
