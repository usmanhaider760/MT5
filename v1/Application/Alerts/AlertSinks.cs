using Newtonsoft.Json;

namespace MT5TradingBot.Modules.Alerts
{
    public sealed class InMemoryAlertSink : ISafetyAlertSink
    {
        private readonly object _sync = new();
        private List<SafetyAlert> _alerts = [];

        public Task<IReadOnlyList<SafetyAlert>> LoadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
                return Task.FromResult<IReadOnlyList<SafetyAlert>>(_alerts.Select(Clone).ToList());
        }

        public Task SaveAsync(IReadOnlyList<SafetyAlert> alerts, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
                _alerts = alerts.Select(Clone).ToList();
            return Task.CompletedTask;
        }

        private static SafetyAlert Clone(SafetyAlert alert) => new()
        {
            AlertId = alert.AlertId,
            Severity = alert.Severity,
            Category = alert.Category,
            Message = alert.Message,
            TimestampUtc = alert.TimestampUtc,
            LastSeenUtc = alert.LastSeenUtc,
            RelatedCode = alert.RelatedCode,
            RecommendedAction = alert.RecommendedAction,
            Acknowledged = alert.Acknowledged,
            OccurrenceCount = alert.OccurrenceCount
        };
    }

    public sealed class JsonFileAlertSink : ISafetyAlertSink
    {
        private readonly string _path;

        public JsonFileAlertSink(string path)
        {
            _path = path;
        }

        public async Task<IReadOnlyList<SafetyAlert>> LoadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(_path) || !File.Exists(_path))
                return [];

            try
            {
                string json = await File.ReadAllTextAsync(_path, cancellationToken).ConfigureAwait(false);
                return JsonConvert.DeserializeObject<List<SafetyAlert>>(json) ?? [];
            }
            catch
            {
                return [];
            }
        }

        public async Task SaveAsync(IReadOnlyList<SafetyAlert> alerts, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(_path))
                return;

            string? folder = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(folder))
                Directory.CreateDirectory(folder);

            string json = JsonConvert.SerializeObject(alerts, Formatting.Indented);
            await File.WriteAllTextAsync(_path, json, cancellationToken).ConfigureAwait(false);
        }
    }
}
