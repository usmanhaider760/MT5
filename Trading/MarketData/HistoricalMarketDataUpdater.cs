using System.Globalization;
using System.Text;
using MT5TradingBot.Models;
using MT5TradingBot.Modules.Backtesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MT5TradingBot.Modules.MarketData
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum MarketDataUpdateType
    {
        Tick,
        OHLC,
        TickThenOHLC
    }

    public sealed record HistoricalMarketDataUpdateRequest
    {
        public IReadOnlyList<string> Symbols { get; init; } = [];
        public string DataDirectory { get; init; } = @".\data";
        public MarketDataUpdateType PreferredDataType { get; init; } = MarketDataUpdateType.TickThenOHLC;
        public int LookbackDays { get; init; } = 30;
        public int MaxRowsPerUpdate { get; init; } = 5_000;
        public int MaxDaysPerUpdate { get; init; } = 7;
        public int TickRetentionDays { get; init; } = 60;
        public int OhlcRetentionDays { get; init; } = 365;
        public DateTime? NowUtc { get; init; }
    }

    public sealed record HistoricalMarketDataUpdateSummary
    {
        public IReadOnlyList<HistoricalMarketDataSymbolResult> SymbolResults { get; init; } = [];
        public IReadOnlyList<string> Warnings { get; init; } = [];
        public IReadOnlyList<string> Errors { get; init; } = [];
        public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
    }

    public sealed record HistoricalMarketDataSymbolResult
    {
        public string Symbol { get; init; } = "";
        public MarketDataUpdateType DataTypeUsed { get; init; }
        public bool FallbackUsed { get; init; }
        public int RowsBefore { get; init; }
        public int RowsFetched { get; init; }
        public int RowsAfter { get; init; }
        public int RowsRemovedByRetention { get; init; }
        public DateTime FromUtc { get; init; }
        public DateTime ToUtc { get; init; }
        public DateTime? LastUpdatedUtc { get; init; }
        public string OutputFilePath { get; init; } = "";
        public IReadOnlyList<string> Warnings { get; init; } = [];
        public IReadOnlyList<string> Errors { get; init; } = [];
    }

    public enum HistoricalMarketDataSyncStatus
    {
        Syncing,
        Completed,
        Failed,
        Cancelled,
        Skipped
    }

    public sealed record HistoricalMarketDataSyncProgress
    {
        public HistoricalMarketDataSyncStatus Status { get; init; } = HistoricalMarketDataSyncStatus.Syncing;
        public string Symbol { get; init; } = "";
        public MarketDataUpdateType DataType { get; init; } = MarketDataUpdateType.TickThenOHLC;
        public int Percent { get; init; }
        public int RowsFetched { get; init; }
        public DateTime? LastUpdatedUtc { get; init; }
        public string Message { get; init; } = "";
        public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
    }

    public sealed record HistoricalMarketDataProviderResult<T>
    {
        public bool Success { get; init; }
        public IReadOnlyList<T> Rows { get; init; } = [];
        public string Error { get; init; } = "";

        public static HistoricalMarketDataProviderResult<T> Ok(IReadOnlyList<T> rows) =>
            new() { Success = true, Rows = rows };

        public static HistoricalMarketDataProviderResult<T> Fail(string error) =>
            new() { Success = false, Error = error };
    }

    public interface IHistoricalMarketDataProvider
    {
        Task<HistoricalMarketDataProviderResult<BacktestTick>> GetTicksAsync(
            string symbol,
            DateTime fromUtc,
            DateTime toUtc,
            int maxRows,
            CancellationToken cancellationToken = default);

        Task<HistoricalMarketDataProviderResult<BacktestOhlcCandle>> GetOhlcM1Async(
            string symbol,
            DateTime fromUtc,
            DateTime toUtc,
            int maxRows,
            CancellationToken cancellationToken = default);
    }

    public sealed class HistoricalMarketDataUpdater
    {
        private readonly IHistoricalMarketDataProvider _provider;

        public HistoricalMarketDataUpdater(IHistoricalMarketDataProvider provider)
        {
            _provider = provider;
        }

        public async Task<HistoricalMarketDataUpdateSummary> UpdateAsync(
            HistoricalMarketDataUpdateRequest request,
            IProgress<HistoricalMarketDataSyncProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var warnings = new List<string>();
            var errors = new List<string>();
            var results = new List<HistoricalMarketDataSymbolResult>();

            string[] symbols = NormalizeSymbols(request.Symbols);
            if (symbols.Length == 0)
            {
                errors.Add("No market-data symbols configured.");
                return new HistoricalMarketDataUpdateSummary { Errors = errors, Warnings = warnings };
            }

            int lookbackDays = Math.Max(1, request.LookbackDays);
            int maxRows = Math.Max(1, request.MaxRowsPerUpdate);
            int maxDays = Math.Max(1, request.MaxDaysPerUpdate);
            DateTime toUtc = EnsureUtc(request.NowUtc ?? DateTime.UtcNow);
            Directory.CreateDirectory(request.DataDirectory);

            for (int index = 0; index < symbols.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string symbol = symbols[index];
                int startPercent = Percent(index, symbols.Length);
                progress?.Report(new HistoricalMarketDataSyncProgress
                {
                    Status = HistoricalMarketDataSyncStatus.Syncing,
                    Symbol = symbol,
                    DataType = request.PreferredDataType,
                    Percent = startPercent,
                    Message = $"Syncing {symbol}"
                });

                var result = request.PreferredDataType switch
                {
                    MarketDataUpdateType.Tick => await UpdateTicksAsync(
                        symbol, request.DataDirectory, lookbackDays, maxDays, maxRows, request.TickRetentionDays, toUtc, fallbackUsed: false, cancellationToken)
                        .ConfigureAwait(false),
                    MarketDataUpdateType.OHLC => await UpdateOhlcAsync(
                        symbol, request.DataDirectory, lookbackDays, maxDays, maxRows, request.OhlcRetentionDays, toUtc, fallbackUsed: false, cancellationToken)
                        .ConfigureAwait(false),
                    _ => await UpdateTickThenOhlcAsync(
                        symbol, request.DataDirectory, lookbackDays, maxDays, maxRows, request.TickRetentionDays, request.OhlcRetentionDays, toUtc, cancellationToken)
                        .ConfigureAwait(false)
                };

                results.Add(result);
                warnings.AddRange(result.Warnings);
                errors.AddRange(result.Errors);
                progress?.Report(new HistoricalMarketDataSyncProgress
                {
                    Status = result.Errors.Count == 0 ? HistoricalMarketDataSyncStatus.Syncing : HistoricalMarketDataSyncStatus.Failed,
                    Symbol = symbol,
                    DataType = result.DataTypeUsed,
                    Percent = Percent(index + 1, symbols.Length),
                    RowsFetched = result.RowsFetched,
                    LastUpdatedUtc = result.LastUpdatedUtc,
                    Message = result.Errors.Count == 0
                        ? $"{symbol} {result.DataTypeUsed} rows fetched: {result.RowsFetched}"
                        : string.Join("; ", result.Errors)
                });
            }

            return new HistoricalMarketDataUpdateSummary
            {
                SymbolResults = results,
                Warnings = warnings,
                Errors = errors,
                TimestampUtc = toUtc
            };
        }

        public static HistoricalMarketDataUpdateRequest FromConfig(
            BotConfig config,
            HistoricalMarketDataCliOptions? cliOptions = null) =>
            new()
            {
                Symbols = cliOptions?.Symbols.Count > 0
                    ? cliOptions.Symbols
                    : config.MarketDataSymbols,
                DataDirectory = !string.IsNullOrWhiteSpace(cliOptions?.DataDirectory)
                    ? cliOptions.DataDirectory
                    : config.MarketDataDirectory,
                PreferredDataType = cliOptions?.PreferredDataType ?? config.PreferredMarketDataType,
                LookbackDays = cliOptions?.LookbackDays ?? config.MarketDataLookbackDays,
                MaxRowsPerUpdate = config.MaxRowsPerUpdate,
                MaxDaysPerUpdate = config.MaxDaysPerUpdate,
                TickRetentionDays = config.TickRetentionDays,
                OhlcRetentionDays = config.OhlcRetentionDays
            };

        private async Task<HistoricalMarketDataSymbolResult> UpdateTickThenOhlcAsync(
            string symbol,
            string dataDirectory,
            int lookbackDays,
            int maxDays,
            int maxRows,
            int tickRetentionDays,
            int ohlcRetentionDays,
            DateTime toUtc,
            CancellationToken cancellationToken)
        {
            var tickResult = await UpdateTicksAsync(
                symbol, dataDirectory, lookbackDays, maxDays, maxRows, tickRetentionDays, toUtc, fallbackUsed: false, cancellationToken)
                .ConfigureAwait(false);

            if (tickResult.Errors.Count == 0 && tickResult.RowsFetched > 0)
                return tickResult;

            var ohlcResult = await UpdateOhlcAsync(
                symbol, dataDirectory, lookbackDays, maxDays, maxRows, ohlcRetentionDays, toUtc, fallbackUsed: true, cancellationToken)
                .ConfigureAwait(false);

            return ohlcResult with
            {
                Warnings = [.. tickResult.Warnings, $"Tick update unavailable for {symbol}; used OHLC M1 fallback.", .. ohlcResult.Warnings],
                Errors = ohlcResult.Errors
            };
        }

        private async Task<HistoricalMarketDataSymbolResult> UpdateTicksAsync(
            string symbol,
            string dataDirectory,
            int lookbackDays,
            int maxDays,
            int maxRows,
            int retentionDays,
            DateTime toUtc,
            bool fallbackUsed,
            CancellationToken cancellationToken)
        {
            string outputPath = TickPath(dataDirectory, symbol);
            var existing = LoadExistingTicks(outputPath);
            DateTime fromUtc = ResolveFromUtc(existing.Select(t => t.TimestampUtc), lookbackDays, maxDays, toUtc);
            var fetched = await _provider.GetTicksAsync(symbol, fromUtc, toUtc, maxRows, cancellationToken)
                .ConfigureAwait(false);

            if (!fetched.Success)
                return Failure(symbol, MarketDataUpdateType.Tick, fallbackUsed, existing.Count, fromUtc, toUtc, outputPath, fetched.Error);

            var mergedBeforeRetention = existing
                .Concat(fetched.Rows.Where(r => string.Equals(r.Symbol, symbol, StringComparison.OrdinalIgnoreCase)))
                .GroupBy(t => $"{t.Symbol.ToUpperInvariant()}|{EnsureUtc(t.TimestampUtc):O}", StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(t => t.Volume.HasValue).First() with
                {
                    Symbol = g.First().Symbol.ToUpperInvariant(),
                    TimestampUtc = EnsureUtc(g.First().TimestampUtc)
                })
                .OrderBy(t => t.TimestampUtc)
                .ThenBy(t => t.Symbol, StringComparer.OrdinalIgnoreCase)
                .ToList();

            DateTime retentionCutoff = toUtc.AddDays(-Math.Max(1, retentionDays));
            var merged = mergedBeforeRetention
                .Where(t => t.TimestampUtc >= retentionCutoff)
                .ToList();
            int rowsRemovedByRetention = mergedBeforeRetention.Count - merged.Count;
            WriteTicks(outputPath, merged);
            await new CsvBacktestTickDataLoader().LoadAsync(outputPath, symbol, cancellationToken).ConfigureAwait(false);

            return Success(symbol, MarketDataUpdateType.Tick, fallbackUsed, existing.Count, fetched.Rows.Count, merged.Count, rowsRemovedByRetention, fromUtc, toUtc, outputPath, LastTimestamp(merged.Select(t => t.TimestampUtc)));
        }

        private async Task<HistoricalMarketDataSymbolResult> UpdateOhlcAsync(
            string symbol,
            string dataDirectory,
            int lookbackDays,
            int maxDays,
            int maxRows,
            int retentionDays,
            DateTime toUtc,
            bool fallbackUsed,
            CancellationToken cancellationToken)
        {
            string outputPath = OhlcPath(dataDirectory, symbol);
            var existing = LoadExistingOhlc(outputPath);
            DateTime fromUtc = ResolveFromUtc(existing.Select(c => c.TimestampUtc), lookbackDays, maxDays, toUtc);
            var fetched = await _provider.GetOhlcM1Async(symbol, fromUtc, toUtc, maxRows, cancellationToken)
                .ConfigureAwait(false);

            if (!fetched.Success)
                return Failure(symbol, MarketDataUpdateType.OHLC, fallbackUsed, existing.Count, fromUtc, toUtc, outputPath, fetched.Error);

            var mergedBeforeRetention = existing
                .Concat(fetched.Rows.Where(r => string.Equals(r.Symbol, symbol, StringComparison.OrdinalIgnoreCase)))
                .GroupBy(c => $"{c.Symbol.ToUpperInvariant()}|{c.Timeframe.ToUpperInvariant()}|{EnsureUtc(c.TimestampUtc):O}", StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First() with
                {
                    Symbol = g.First().Symbol.ToUpperInvariant(),
                    Timeframe = string.IsNullOrWhiteSpace(g.First().Timeframe) ? "M1" : g.First().Timeframe.ToUpperInvariant(),
                    TimestampUtc = EnsureUtc(g.First().TimestampUtc)
                })
                .OrderBy(c => c.TimestampUtc)
                .ThenBy(c => c.Symbol, StringComparer.OrdinalIgnoreCase)
                .ToList();

            DateTime retentionCutoff = toUtc.AddDays(-Math.Max(1, retentionDays));
            var merged = mergedBeforeRetention
                .Where(c => c.TimestampUtc >= retentionCutoff)
                .ToList();
            int rowsRemovedByRetention = mergedBeforeRetention.Count - merged.Count;
            WriteOhlc(outputPath, merged);
            await new CsvBacktestOhlcDataLoader().LoadAsync(outputPath, symbol, cancellationToken).ConfigureAwait(false);

            return Success(symbol, MarketDataUpdateType.OHLC, fallbackUsed, existing.Count, fetched.Rows.Count, merged.Count, rowsRemovedByRetention, fromUtc, toUtc, outputPath, LastTimestamp(merged.Select(c => c.TimestampUtc)));
        }

        private static HistoricalMarketDataSymbolResult Success(
            string symbol,
            MarketDataUpdateType dataType,
            bool fallbackUsed,
            int rowsBefore,
            int rowsFetched,
            int rowsAfter,
            int rowsRemovedByRetention,
            DateTime fromUtc,
            DateTime toUtc,
            string outputPath,
            DateTime? lastUpdatedUtc) => new()
        {
            Symbol = symbol,
            DataTypeUsed = dataType,
            FallbackUsed = fallbackUsed,
            RowsBefore = rowsBefore,
            RowsFetched = rowsFetched,
            RowsAfter = rowsAfter,
            RowsRemovedByRetention = rowsRemovedByRetention,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            LastUpdatedUtc = lastUpdatedUtc,
            OutputFilePath = outputPath
        };

        private static HistoricalMarketDataSymbolResult Failure(
            string symbol,
            MarketDataUpdateType dataType,
            bool fallbackUsed,
            int rowsBefore,
            DateTime fromUtc,
            DateTime toUtc,
            string outputPath,
            string error) => new()
        {
            Symbol = symbol,
            DataTypeUsed = dataType,
            FallbackUsed = fallbackUsed,
            RowsBefore = rowsBefore,
            RowsAfter = rowsBefore,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            OutputFilePath = outputPath,
            Errors = [string.IsNullOrWhiteSpace(error) ? $"No {dataType} data returned for {symbol}." : error]
        };

        private static List<BacktestTick> LoadExistingTicks(string outputPath)
        {
            if (!File.Exists(outputPath)) return [];
            return new CsvBacktestTickDataLoader().LoadAsync(outputPath).GetAwaiter().GetResult().ToList();
        }

        private static List<BacktestOhlcCandle> LoadExistingOhlc(string outputPath)
        {
            if (!File.Exists(outputPath)) return [];
            return new CsvBacktestOhlcDataLoader().LoadAsync(outputPath).GetAwaiter().GetResult().ToList();
        }

        private static DateTime ResolveFromUtc(IEnumerable<DateTime> timestampsUtc, int lookbackDays, int maxDays, DateTime toUtc)
        {
            DateTime? last = timestampsUtc.OrderByDescending(t => t).FirstOrDefault();
            if (last.HasValue && last.Value != default)
                return EnsureUtc(last.Value).AddMilliseconds(1);

            int days = Math.Min(Math.Max(1, lookbackDays), Math.Max(1, maxDays));
            return toUtc.AddDays(-days);
        }

        private static int Percent(int completed, int total) =>
            total <= 0 ? 0 : Math.Clamp((int)Math.Round(100.0 * completed / total), 0, 100);

        private static DateTime? LastTimestamp(IEnumerable<DateTime> timestampsUtc)
        {
            DateTime last = timestampsUtc.OrderByDescending(t => t).FirstOrDefault();
            return last == default ? null : last;
        }

        private static void WriteTicks(string outputPath, IReadOnlyList<BacktestTick> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("timestamp,symbol,bid,ask,volume");
            foreach (var row in rows)
            {
                sb.Append(FormatUtc(row.TimestampUtc)).Append(',')
                    .Append(row.Symbol.ToUpperInvariant()).Append(',')
                    .Append(Format(row.Bid)).Append(',')
                    .Append(Format(row.Ask)).Append(',')
                    .Append(row.Volume.HasValue ? Format(row.Volume.Value) : "")
                    .AppendLine();
            }

            File.WriteAllText(outputPath, sb.ToString());
        }

        private static void WriteOhlc(string outputPath, IReadOnlyList<BacktestOhlcCandle> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("timestamp,symbol,open,high,low,close,timeframe,spread");
            foreach (var row in rows)
            {
                sb.Append(FormatUtc(row.TimestampUtc)).Append(',')
                    .Append(row.Symbol.ToUpperInvariant()).Append(',')
                    .Append(Format(row.Open)).Append(',')
                    .Append(Format(row.High)).Append(',')
                    .Append(Format(row.Low)).Append(',')
                    .Append(Format(row.Close)).Append(',')
                    .Append(string.IsNullOrWhiteSpace(row.Timeframe) ? "M1" : row.Timeframe.ToUpperInvariant()).Append(',')
                    .Append(row.SpreadPips.HasValue ? Format(row.SpreadPips.Value) : "")
                    .AppendLine();
            }

            File.WriteAllText(outputPath, sb.ToString());
        }

        private static string[] NormalizeSymbols(IEnumerable<string> symbols) =>
            symbols
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim().Replace("/", "", StringComparison.Ordinal).ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

        private static string TickPath(string dataDirectory, string symbol) =>
            Path.Combine(dataDirectory, $"{symbol.ToUpperInvariant()}_ticks.csv");

        private static string OhlcPath(string dataDirectory, string symbol) =>
            Path.Combine(dataDirectory, $"{symbol.ToUpperInvariant()}_M1.csv");

        private static DateTime EnsureUtc(DateTime value) =>
            value.Kind == DateTimeKind.Utc
                ? value
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);

        private static string FormatUtc(DateTime value) =>
            EnsureUtc(value).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

        private static string Format(double value) =>
            value.ToString("0.##########", CultureInfo.InvariantCulture);
    }
}
