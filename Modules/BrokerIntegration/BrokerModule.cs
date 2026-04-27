using MT5TradingBot.Core;
using MT5TradingBot.Models;

namespace MT5TradingBot.Modules.BrokerIntegration
{
    /// <summary>
    /// Module 1 — Broker Integration.
    /// Verifies MT5 EA is running and the named-pipe / TCP connection is live.
    /// </summary>
    public sealed class BrokerModule : IModule
    {
        private readonly MT5Settings _settings;

        public BrokerModule(MT5Settings settings) => _settings = settings;

        public string Name        => "Broker Integration";
        public string Icon        => "🔌";
        public string Description => "MT5 Named Pipe / TCP connection";

        public async Task<ModuleStatus> CheckAsync(CancellationToken ct = default)
        {
            try
            {
                using var bridge = new MT5Bridge(_settings);
                bool ok = await bridge.PingAsync().ConfigureAwait(false);
                return new ModuleStatus(ok,
                    ok ? "MT5 EA connected and responding"
                       : "MT5 EA not reachable — check pipe name and that AutoTrading is ON");
            }
            catch (Exception ex)
            {
                return new ModuleStatus(false, $"Connection error: {ex.Message}");
            }
        }
    }
}
