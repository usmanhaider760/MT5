using MT5TradingBot.Models;
using Newtonsoft.Json;
using Serilog;

namespace MT5TradingBot.Services
{
    /// <summary>Thread-safe settings persistence.</summary>
    public sealed class SettingsManager
    {
        private static readonly string _dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MT5TradingBot");

        private static string FilePath => Path.Combine(_dir, "settings.json");

        private readonly SemaphoreSlim _lock = new(1, 1);

        public AppSettings Current { get; private set; } = new();

        public async Task LoadAsync()
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                Directory.CreateDirectory(_dir);
                if (!File.Exists(FilePath)) return;
                string json = await File.ReadAllTextAsync(FilePath).ConfigureAwait(false);
                Current = JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
            }
            catch (Exception ex)
            {
                Log.Warning("Settings load failed: {ex}", ex.Message);
                Current = new AppSettings();
            }
            finally { _lock.Release(); }
        }

        public async Task SaveAsync(AppSettings settings)
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                Current = settings;
                Current.LastSaved = DateTime.UtcNow;
                Directory.CreateDirectory(_dir);
                string json = JsonConvert.SerializeObject(Current, Formatting.Indented);
                await File.WriteAllTextAsync(FilePath, json).ConfigureAwait(false);
            }
            catch (Exception ex) { Log.Warning("Settings save failed: {ex}", ex.Message); }
            finally { _lock.Release(); }
        }
    }
}
