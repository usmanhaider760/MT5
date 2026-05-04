using MT5TradingBot.Models;

namespace MT5TradingBot.Modules.MarketData
{
    public sealed class HistoricalMarketDataCommand
    {
        private readonly Func<HistoricalMarketDataUpdater> _updaterFactory;
        private readonly Func<Task<bool>>? _mt5AvailabilityCheck;
        private readonly TextWriter _output;

        public HistoricalMarketDataCommand(
            Func<HistoricalMarketDataUpdater> updaterFactory,
            TextWriter output,
            Func<Task<bool>>? mt5AvailabilityCheck = null)
        {
            _updaterFactory = updaterFactory;
            _output = output;
            _mt5AvailabilityCheck = mt5AvailabilityCheck;
        }

        public async Task<int> RunUpdateAsync(
            AppSettings settings,
            string[] args,
            CancellationToken cancellationToken = default)
        {
            var options = HistoricalMarketDataCliOptions.Parse(args);
            var request = HistoricalMarketDataUpdater.FromConfig(settings.Bot, options);

            WriteUpdateStarted(args, request);

            HistoricalMarketDataUpdater updater;
            try
            {
                updater = _updaterFactory();
            }
            catch (Exception ex)
            {
                _output.WriteLine("MARKET_DATA_UPDATER_NOT_AVAILABLE");
                _output.WriteLine($"failure reason: {ex.Message}");
                return 1;
            }

            if (_mt5AvailabilityCheck != null && !await IsMt5AvailableAsync().ConfigureAwait(false))
            {
                _output.WriteLine(MarketDataSyncStatusText.Mt5Unavailable);
                WriteUnavailableSummary(request);
                return 1;
            }

            HistoricalMarketDataUpdateSummary summary;
            try
            {
                summary = await updater.UpdateAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _output.WriteLine($"failure reason: {ex.Message}");
                return 1;
            }

            foreach (string line in MarketDataUpdateConsoleFormatter.Format(summary))
                _output.WriteLine(line);

            return summary.Errors.Count == 0 ? 0 : 1;
        }

        public async Task<int> RunDiagnoseAsync(
            AppSettings settings,
            string[] args,
            Func<Task<bool>> mt5HealthCheck,
            CancellationToken cancellationToken = default)
        {
            var options = HistoricalMarketDataCliOptions.Parse(args);
            var request = HistoricalMarketDataUpdater.FromConfig(settings.Bot, options);

            _output.WriteLine("MARKET_DATA_SYNC_DIAGNOSTICS");
            _output.WriteLine($"raw args: {string.Join(" ", args)}");
            _output.WriteLine($"config enabled: {settings.Bot.EnableMarketDataAutoUpdate}");
            _output.WriteLine($"startup enabled: {settings.Bot.UpdateMarketDataOnStartup || settings.Bot.UpdateOnStartup}");
            _output.WriteLine($"symbols: {string.Join(",", request.Symbols)}");
            _output.WriteLine($"data type: {request.PreferredDataType}");
            _output.WriteLine($"data dir: {request.DataDirectory}");
            _output.WriteLine($"lookback days: {request.LookbackDays}");

            bool mt5Available;
            try
            {
                mt5Available = await mt5HealthCheck().ConfigureAwait(false);
            }
            catch
            {
                mt5Available = false;
            }

            _output.WriteLine($"MT5 bridge availability: {(mt5Available ? "available" : "unavailable")}");
            _output.WriteLine($"EA health availability: {(mt5Available ? "available" : "unavailable")}");
            foreach (string symbol in request.Symbols)
            {
                string normalized = symbol.Trim().Replace("/", "", StringComparison.Ordinal).ToUpperInvariant();
                _output.WriteLine($"expected tick path: {Path.Combine(request.DataDirectory, $"{normalized}_ticks.csv")}");
                _output.WriteLine($"expected OHLC path: {Path.Combine(request.DataDirectory, $"{normalized}_M1.csv")}");
            }

            cancellationToken.ThrowIfCancellationRequested();
            return mt5Available ? 0 : 1;
        }

        private void WriteUpdateStarted(string[] args, HistoricalMarketDataUpdateRequest request)
        {
            _output.WriteLine("MARKET_DATA_UPDATE_STARTED");
            _output.WriteLine($"raw args: {string.Join(" ", args)}");
            _output.WriteLine($"parsed symbols: {string.Join(",", request.Symbols)}");
            _output.WriteLine($"parsed data type: {request.PreferredDataType}");
            _output.WriteLine($"parsed data dir: {request.DataDirectory}");
            _output.WriteLine($"lookback days: {request.LookbackDays}");
            _output.WriteLine($"backfill: {(request.Backfill ? "yes" : "no")}");
        }

        private async Task<bool> IsMt5AvailableAsync()
        {
            try
            {
                return _mt5AvailabilityCheck != null && await _mt5AvailabilityCheck().ConfigureAwait(false);
            }
            catch
            {
                return false;
            }
        }

        private void WriteUnavailableSummary(HistoricalMarketDataUpdateRequest request)
        {
            foreach (string symbol in request.Symbols)
            {
                string normalized = symbol.Trim().Replace("/", "", StringComparison.Ordinal).ToUpperInvariant();
                _output.WriteLine($"{normalized}: type={request.PreferredDataType}, rows before=0, fetched=0, after=0, removed by retention=0, fallback=no");
                _output.WriteLine($"  output={ExpectedOutputPath(request.DataDirectory, normalized, request.PreferredDataType)}");
                _output.WriteLine($"  mt5_rows_returned=0");
                _output.WriteLine($"  diagnostic=MT5 bridge ping failed before historical data request.");
                _output.WriteLine($"  error={MarketDataSyncStatusText.Mt5Unavailable}");
                _output.WriteLine($"  failure reason: {MarketDataSyncStatusText.Mt5Unavailable}");
            }
        }

        private static string ExpectedOutputPath(
            string dataDirectory,
            string symbol,
            MarketDataUpdateType updateType) =>
            updateType == MarketDataUpdateType.Tick
                ? Path.Combine(dataDirectory, $"{symbol}_ticks.csv")
                : Path.Combine(dataDirectory, $"{symbol}_M1.csv");
    }
}
