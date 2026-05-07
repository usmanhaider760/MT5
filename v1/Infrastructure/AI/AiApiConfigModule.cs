using MT5TradingBot.Core;
using MT5TradingBot.Models;

namespace MT5TradingBot.Modules.AIAnalysis
{
    public sealed class AiApiConfigModule(ClaudeConfig config) : IModule
    {
        public string Name => "AI API Configuration";
        public string Icon => "AI";
        public string Description => "Checks whether AI provider settings are present without sending prompts or using tokens.";

        public Task<ModuleStatus> CheckAsync(CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(config.ApiKey))
                return Task.FromResult(new ModuleStatus(false, "AI API key is missing. Configure Claude/GPT API settings before AI analysis."));

            if (string.IsNullOrWhiteSpace(config.Model))
                return Task.FromResult(new ModuleStatus(false, "AI model is missing."));

            return Task.FromResult(new ModuleStatus(true,
                $"AI API configured for model {config.Model}. No-token startup check only."));
        }
    }
}
