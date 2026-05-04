using Serilog;

namespace MT5TradingBot.Modules.MarketData
{
    public sealed class MarketDataAutoSyncService : IAsyncDisposable
    {
        private readonly Func<HistoricalMarketDataUpdater> _updaterFactory;
        private readonly Func<HistoricalMarketDataUpdateRequest> _requestFactory;
        private readonly Func<bool> _criticalTradingInProgress;
        private readonly Func<Task<bool>>? _mt5AvailabilityCheck;
        private readonly bool _allowSyncDuringTrading;
        private readonly SemaphoreSlim _syncGate = new(1, 1);
        private CancellationTokenSource? _loopCts;
        private CancellationTokenSource? _activeSyncCts;
        private Task? _loopTask;

        public MarketDataAutoSyncService(
            Func<HistoricalMarketDataUpdater> updaterFactory,
            Func<HistoricalMarketDataUpdateRequest> requestFactory,
            TimeSpan syncInterval,
            bool allowSyncDuringTrading,
            Func<bool>? criticalTradingInProgress = null,
            Func<Task<bool>>? mt5AvailabilityCheck = null)
        {
            _updaterFactory = updaterFactory;
            _requestFactory = requestFactory;
            SyncInterval = syncInterval <= TimeSpan.Zero ? TimeSpan.FromMinutes(30) : syncInterval;
            _allowSyncDuringTrading = allowSyncDuringTrading;
            _criticalTradingInProgress = criticalTradingInProgress ?? (() => false);
            _mt5AvailabilityCheck = mt5AvailabilityCheck;
        }

        public event Action<HistoricalMarketDataSyncProgress>? ProgressChanged;

        public TimeSpan SyncInterval { get; }
        public bool IsSyncRunning => _syncGate.CurrentCount == 0;

        public void Start(bool runImmediately)
        {
            if (_loopTask != null) return;

            _loopCts = new CancellationTokenSource();
            CancellationToken token = _loopCts.Token;

            _loopTask = Task.Run(async () =>
            {
                if (runImmediately)
                    _ = TriggerSyncAsync("startup", token);

                using var timer = new PeriodicTimer(SyncInterval);
                while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
                    _ = TriggerSyncAsync("scheduled", token);
            }, token);
        }

        public async Task<HistoricalMarketDataUpdateSummary> TriggerSyncAsync(
            string reason,
            CancellationToken cancellationToken = default)
        {
            if (!_allowSyncDuringTrading && _criticalTradingInProgress())
            {
                const string message = "Market data sync skipped because critical trade execution is in progress.";
                Report(HistoricalMarketDataSyncStatus.Skipped, "", MarketDataUpdateType.TickThenOHLC, 0, 0, null, message);
                return new HistoricalMarketDataUpdateSummary { Warnings = [message] };
            }

            if (!await _syncGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
            {
                const string message = "Market data sync skipped because another sync is already running.";
                Report(HistoricalMarketDataSyncStatus.Skipped, "", MarketDataUpdateType.TickThenOHLC, 0, 0, null, message);
                return new HistoricalMarketDataUpdateSummary { Warnings = [message] };
            }

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _activeSyncCts = linkedCts;

            try
            {
                var request = _requestFactory();
                Report(HistoricalMarketDataSyncStatus.Syncing, "", request.PreferredDataType, 0, 0, null, MarketDataSyncStatusText.Starting);

                if (_mt5AvailabilityCheck != null && !await _mt5AvailabilityCheck().ConfigureAwait(false))
                {
                    const string message = MarketDataSyncStatusText.Mt5Unavailable;
                    Report(HistoricalMarketDataSyncStatus.Failed, "", request.PreferredDataType, 0, 0, null, message);
                    return new HistoricalMarketDataUpdateSummary { Errors = [message] };
                }

                var progress = new Progress<HistoricalMarketDataSyncProgress>(Report);
                var result = await _updaterFactory()
                    .UpdateAsync(request, progress, linkedCts.Token)
                    .ConfigureAwait(false);

                bool failed = result.Errors.Count > 0;
                Report(
                    failed ? HistoricalMarketDataSyncStatus.Failed : HistoricalMarketDataSyncStatus.Completed,
                    result.SymbolResults.LastOrDefault()?.Symbol ?? "",
                    result.SymbolResults.LastOrDefault()?.DataTypeUsed ?? request.PreferredDataType,
                    100,
                    result.SymbolResults.Sum(r => r.RowsFetched),
                    result.SymbolResults.Select(r => r.LastUpdatedUtc).Where(d => d.HasValue).Max(),
                    failed ? FailureMessage(result.Errors) : "Market data sync completed.");
                return result;
            }
            catch (OperationCanceledException)
            {
                const string message = "Market data sync cancelled.";
                Report(HistoricalMarketDataSyncStatus.Cancelled, "", MarketDataUpdateType.TickThenOHLC, 0, 0, null, message);
                return new HistoricalMarketDataUpdateSummary { Warnings = [message] };
            }
            catch (Exception ex)
            {
                string message = $"Market data sync failed: {ex.Message}";
                Log.Warning(ex, "Market data sync failed");
                Report(HistoricalMarketDataSyncStatus.Failed, "", MarketDataUpdateType.TickThenOHLC, 0, 0, null, message);
                return new HistoricalMarketDataUpdateSummary { Errors = [message] };
            }
            finally
            {
                _activeSyncCts = null;
                _syncGate.Release();
            }
        }

        public void CancelActiveSync() =>
            _activeSyncCts?.Cancel();

        public async ValueTask DisposeAsync()
        {
            _loopCts?.Cancel();
            CancelActiveSync();
            if (_loopTask != null)
            {
                try { await _loopTask.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false); }
                catch { }
            }

            _loopCts?.Dispose();
            _activeSyncCts?.Dispose();
            _syncGate.Dispose();
        }

        private void Report(HistoricalMarketDataSyncProgress progress) =>
            ProgressChanged?.Invoke(progress);

        private void Report(
            HistoricalMarketDataSyncStatus status,
            string symbol,
            MarketDataUpdateType dataType,
            int percent,
            int rowsFetched,
            DateTime? lastUpdatedUtc,
            string message) =>
            Report(new HistoricalMarketDataSyncProgress
            {
                Status = status,
                Symbol = symbol,
                DataType = dataType,
                Percent = percent,
                RowsFetched = rowsFetched,
                LastUpdatedUtc = lastUpdatedUtc,
                Message = message,
                TimestampUtc = DateTime.UtcNow
            });

        private static string FailureMessage(IReadOnlyList<string> errors)
        {
            if (errors.Any(e => e.Contains("NO_MARKET_DATA_AVAILABLE", StringComparison.OrdinalIgnoreCase) ||
                                e.Contains("returned 0 rows", StringComparison.OrdinalIgnoreCase)))
                return MarketDataSyncStatusText.NoData;

            if (errors.Any(e => e.Contains("MT5", StringComparison.OrdinalIgnoreCase) ||
                                e.Contains("EA", StringComparison.OrdinalIgnoreCase) ||
                                e.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
                                e.Contains("response", StringComparison.OrdinalIgnoreCase)))
                return MarketDataSyncStatusText.Mt5Unavailable;

            return string.Join("; ", errors);
        }
    }
}
