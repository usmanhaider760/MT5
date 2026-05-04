using MT5TradingBot.Core;
using MT5TradingBot.Models;

namespace MT5TradingBot.Modules.NewsFilter
{
    public sealed class NewsApiConfigModule(ApiIntegrationConfig config) : IModule
    {
        public string Name => "News API / Calendar";
        public string Icon => "NEWS";
        public string Description => "Checks economic-calendar provider configuration and live availability for news risk filtering.";

        public async Task<ModuleStatus> CheckAsync(CancellationToken ct = default)
        {
            string provider = config.NewsProvider?.Trim() ?? "";
            if (string.Equals(provider, "None", StringComparison.OrdinalIgnoreCase))
                return new ModuleStatus(true, "News provider disabled by settings.");

            if (string.IsNullOrWhiteSpace(provider))
                return new ModuleStatus(false, "News provider is not selected.");

            if (!IsFmpProvider(provider))
                return new ModuleStatus(false, $"News provider '{provider}' is not wired yet. Select Financial Modeling Prep or None.");

            if (string.IsNullOrWhiteSpace(config.NewsApiKey))
                return new ModuleStatus(false, "Financial Modeling Prep API key is missing. News blackout checks will be unavailable.");

            if (config.NewsCurrencies.Count == 0)
                return new ModuleStatus(false, "News currencies list is empty. Add currencies such as USD, GBP, EUR, JPY.");

            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(8));

                var snapshot = await new FmpNewsCalendarService()
                    .GetRiskSnapshotAsync("XAUUSD", config, timeout.Token)
                    .ConfigureAwait(false);

                if (!snapshot.IsConfigured || string.Equals(snapshot.RiskLevel, "UNAVAILABLE", StringComparison.OrdinalIgnoreCase))
                    return new ModuleStatus(false, snapshot.Reason);

                return new ModuleStatus(true,
                    $"FMP news API reachable. Risk {snapshot.RiskLevel}; relevant events: {snapshot.RelevantEvents.Count}; blackout: {(snapshot.IsBlackoutActive ? "yes" : "no")}.");
            }
            catch (OperationCanceledException)
            {
                return new ModuleStatus(false, "News API check timed out. Startup continued only if you choose Proceed Anyway.");
            }
            catch (Exception ex)
            {
                return new ModuleStatus(false, $"News API check failed: {ex.Message}");
            }
        }

        private static bool IsFmpProvider(string provider) =>
            provider.Contains("Financial Modeling Prep", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(provider, "FMP", StringComparison.OrdinalIgnoreCase);
    }
}
