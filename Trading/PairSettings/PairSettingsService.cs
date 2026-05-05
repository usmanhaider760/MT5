using MT5TradingBot.Models;
using MT5TradingBot.Services;
using Newtonsoft.Json;
using Serilog;

namespace MT5TradingBot.Modules.PairSettings
{
    public sealed class PairSettingsService : IPairSettingsService
    {
        private readonly SettingsManager _settingsManager;
        private readonly AppSettings _settings;

        public PairSettingsService(SettingsManager settingsManager, AppSettings settings)
        {
            _settingsManager = settingsManager;
            _settings = settings;
            _settings.PairSettings ??= new Dictionary<string, PairTradingSettings>(StringComparer.OrdinalIgnoreCase);
            NormalizeAll();
        }

        public IReadOnlyList<PairTradingSettings> GetAll() =>
            [.. _settings.PairSettings
                .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .Select(kv => WithPair(kv.Key, kv.Value))];

        public PairTradingSettings? GetForPair(string pair)
        {
            string key = NormalizePair(pair);
            if (string.IsNullOrWhiteSpace(key))
                return null;

            if (_settings.PairSettings.TryGetValue(key, out var exact))
            {
                Log.Debug("Pair settings loaded for {Pair}", key);
                return WithPair(key, exact);
            }

            var match = _settings.PairSettings.FirstOrDefault(kv =>
                key.StartsWith(kv.Key, StringComparison.OrdinalIgnoreCase) ||
                kv.Key.StartsWith(key, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(match.Key))
            {
                Log.Debug("Pair settings loaded for {Pair} via configured base {BasePair}", pair, match.Key);
                return WithPair(match.Key, match.Value);
            }

            return null;
        }

        public void Upsert(PairTradingSettings settings)
        {
            string key = NormalizePair(settings.Pair);
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Pair is required.", nameof(settings));

            Validate(settings);
            settings.Pair = key;
            _settings.PairSettings[key] = Clone(settings);
            Save();
            Log.Information("Pair settings saved for {Pair}", key);
        }

        public bool Delete(string pair)
        {
            string key = NormalizePair(pair);
            bool removed = _settings.PairSettings.Remove(key);
            if (removed)
            {
                Save();
                Log.Information("Pair settings deleted for {Pair}", key);
            }

            return removed;
        }

        public int ImportJson(string json)
        {
            var doc = JsonConvert.DeserializeObject<PairSettingsDocument>(json)
                ?? throw new InvalidOperationException("JSON did not contain pair_settings.");

            if (doc.PairSettings.Count == 0)
                throw new InvalidOperationException("JSON did not contain any pair_settings entries.");

            int imported = 0;
            foreach (var (pair, settings) in doc.PairSettings)
            {
                string key = NormalizePair(pair);
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                settings.Pair = key;
                Validate(settings);
                _settings.PairSettings[key] = Clone(settings);
                imported++;
            }

            Save();
            Log.Information("Imported {Count} pair settings entries.", imported);
            return imported;
        }

        private void Save() => _settingsManager.SaveAsync(_settings).GetAwaiter().GetResult();

        private void NormalizeAll()
        {
            var normalized = new Dictionary<string, PairTradingSettings>(StringComparer.OrdinalIgnoreCase);
            foreach (var (pair, value) in _settings.PairSettings)
            {
                string key = NormalizePair(string.IsNullOrWhiteSpace(value.Pair) ? pair : value.Pair);
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                value.Pair = key;
                normalized[key] = Clone(value);
            }

            _settings.PairSettings = normalized;
        }

        private static void Validate(PairTradingSettings settings)
        {
            if (settings.PipSize <= 0) throw new InvalidOperationException("pip_size must be greater than 0.");
            if (settings.MinAtrPipsM5 < 0) throw new InvalidOperationException("min_atr_pips_m5 cannot be negative.");
            if (settings.MaxAtrPipsM5 < 0) throw new InvalidOperationException("max_atr_pips_m5 cannot be negative.");
            if (settings.MaxAtrPipsM5 > 0 && settings.MaxAtrPipsM5 < settings.MinAtrPipsM5)
                throw new InvalidOperationException("max_atr_pips_m5 must be greater than or equal to min_atr_pips_m5.");
            if (settings.MinAtrPipsM15 < 0) throw new InvalidOperationException("min_atr_pips_m15 cannot be negative.");
            if (settings.MaxAtrPipsM15 < 0) throw new InvalidOperationException("max_atr_pips_m15 cannot be negative.");
            if (settings.MaxAtrPipsM15 > 0 && settings.MaxAtrPipsM15 < settings.MinAtrPipsM15)
                throw new InvalidOperationException("max_atr_pips_m15 must be greater than or equal to min_atr_pips_m15.");
            if (settings.MinimumDistanceFromKeyLevelPips < 0)
                throw new InvalidOperationException("minimum_distance_from_key_level_pips cannot be negative.");
            if (settings.TrailingStartPips < 0) throw new InvalidOperationException("trailing_start_pips cannot be negative.");
            if (settings.TrailingStepPips < 0) throw new InvalidOperationException("trailing_step_pips cannot be negative.");
            if (settings.MaxSlippagePips < 0) throw new InvalidOperationException("max_slippage_pips cannot be negative.");
        }

        private static string NormalizePair(string pair) => pair.Trim().ToUpperInvariant();

        private static PairTradingSettings WithPair(string pair, PairTradingSettings settings)
        {
            var clone = Clone(settings);
            clone.Pair = NormalizePair(pair);
            return clone;
        }

        private static PairTradingSettings Clone(PairTradingSettings settings) => new()
        {
            Pair = settings.Pair,
            PipSize = settings.PipSize,
            MinAtrPipsM5 = settings.MinAtrPipsM5,
            MaxAtrPipsM5 = settings.MaxAtrPipsM5,
            MinAtrPipsM15 = settings.MinAtrPipsM15,
            MaxAtrPipsM15 = settings.MaxAtrPipsM15,
            MinimumDistanceFromKeyLevelPips = settings.MinimumDistanceFromKeyLevelPips,
            TrailingStartPips = settings.TrailingStartPips,
            TrailingStepPips = settings.TrailingStepPips,
            MaxSlippagePips = settings.MaxSlippagePips,
            RecommendedSessions = [.. settings.RecommendedSessions],
            AvoidSessions = [.. settings.AvoidSessions]
        };
    }
}
