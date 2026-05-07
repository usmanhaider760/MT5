using MT5TradingBot.Core;
using MT5TradingBot.Models;

namespace MT5TradingBot.Modules.SignalDecision
{
    public sealed class SignalWatchFolderModule(BotConfig config) : IModule
    {
        public string Name => "Signal Watch Folder";
        public string Icon => "DIR";
        public string Description => "Checks that the Auto Bot signal folder is ready before the main form opens.";

        public Task<ModuleStatus> CheckAsync(CancellationToken ct = default)
        {
            string folder = config.WatchFolder.Trim();
            if (string.IsNullOrWhiteSpace(folder))
                return Task.FromResult(new ModuleStatus(false, "Watch folder is not set."));

            try
            {
                Directory.CreateDirectory(folder);
                int pending = Directory.GetFiles(folder, "*.json").Length;
                return Task.FromResult(new ModuleStatus(true,
                    $"Watching folder ready: {folder}. Pending JSON files: {pending}."));
            }
            catch (Exception ex)
            {
                return Task.FromResult(new ModuleStatus(false,
                    $"Watch folder cannot be prepared: {ex.Message}"));
            }
        }
    }
}
