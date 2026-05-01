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
        private FileSystemWatcher? _watcher;
        private readonly SemaphoreSlim _reloadDebounce = new(1, 1);

        public AppSettings Current { get; private set; } = new();
        public event Action<AppSettings>? SettingsReloaded;

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

        public void StartWatching()
        {
            if (_watcher != null) return;

            Directory.CreateDirectory(_dir);
            _watcher = new FileSystemWatcher(_dir, "settings.json")
            {
                NotifyFilter          = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents   = true,
                IncludeSubdirectories = false
            };
            _watcher.Changed += OnFileChanged;
        }

        public void StopWatching()
        {
            _watcher?.Dispose();
            _watcher = null;
        }

        private async void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            if (!await _reloadDebounce.WaitAsync(0).ConfigureAwait(false)) return;

            try
            {
                await Task.Delay(300).ConfigureAwait(false);
                await _lock.WaitAsync().ConfigureAwait(false);

                try
                {
                    string json = await File.ReadAllTextAsync(FilePath).ConfigureAwait(false);
                    var loaded = JsonConvert.DeserializeObject<AppSettings>(json);
                    if (loaded == null) return;

                    Current = loaded;
                    SettingsReloaded?.Invoke(Current);
                }
                catch
                {
                    // File may be mid-write; skip this change event.
                }
                finally { _lock.Release(); }
            }
            finally { _reloadDebounce.Release(); }
        }
    }
}
