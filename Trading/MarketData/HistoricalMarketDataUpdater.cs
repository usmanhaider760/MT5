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
        public bool Backfill { get; init; }
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
        public string ProviderCommand { get; init; } = "";
        public int ProviderRowsReturned { get; init; }
        public string ProviderDiagnostic { get; init; } = "";
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
        public string CommandName { get; init; } = "";
        public int RowsReturned { get; init; }
        public string Diagnostic { get; init; } = "";

        public static HistoricalMarketDataProviderResult<T> Ok(
            IReadOnlyList<T> rows,
            string commandName = "",
            string diagnostic = "") =>
            new()
            {
                Success = true,
                Rows = rows,
                CommandName = commandName,
                RowsReturned = rows.Count,
                Diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? $"{commandName} returned {rows.Count} rows."
                    : diagnostic
            };

        public static HistoricalMarketDataProviderResult<T> Fail(
            string error,
            string commandName = "",
            int rowsReturned = 0,
            string diagnostic = "") =>
            new()
            {
                Success = false,
                Error = error,
                CommandName = commandName,
                RowsReturned = rowsReturned,
                Diagnostic = diagnostic
            };
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
            int maxDays = request.Backfill
                ? Math.Max(1, lookbackDays)
                : Math.Max(1, request.MaxDaysPerUpdate);
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
                        symbol, request.DataDirectory, lookbackDays, maxDays, maxRows, request.TickRetentionDays, toUtc, request.Backfill, fallbackUsed: false, cancellationToken)
                        .ConfigureAwait(false),
                    MarketDataUpdateType.OHLC => await UpdateOhlcAsync(
                        symbol, request.DataDirectory, lookbackDays, maxDays, maxRows, request.OhlcRetentionDays, toUtc, request.Backfill, fallbackUsed: false, cancellationToken)
                        .ConfigureAwait(false),
                    _ => await UpdateTickThenOhlcAsync(
                        symbol, request.DataDirectory, lookbackDays, maxDays, maxRows, request.TickRetentionDays, request.OhlcRetentionDays, toUtc, request.Backfill, cancellationToken)
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
                OhlcRetentionDays = config.OhlcRetentionDays,
                Backfill = cliOptions?.Backfill ?? false
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
            bool backfill,
            CancellationToken cancellationToken)
        {
            var tickResult = await UpdateTicksAsync(
                symbol, dataDirectory, lookbackDays, maxDays, maxRows, tickRetentionDays, toUtc, backfill, fallbackUsed: false, cancellationToken)
                .ConfigureAwait(false);

            if (tickResult.Errors.Count == 0 && tickResult.RowsFetched > 0)
                return tickResult;

            var ohlcResult = await UpdateOhlcAsync(
                symbol, dataDirectory, lookbackDays, maxDays, maxRows, ohlcRetentionDays, toUtc, backfill, fallbackUsed: true, cancellationToken)
                .ConfigureAwait(false);

            var warnings = new List<string>(tickResult.Warnings);
            warnings.Add("TICK_DATA_UNAVAILABLE_FALLING_BACK_TO_M1");
            warnings.Add($"Tick update unavailable for {symbol}; used OHLC M1 fallback.");
            warnings.AddRange(ohlcResult.Warnings);

            if (ohlcResult.Errors.Count > 0)
            {
                return ohlcResult with
                {
                    Warnings = warnings,
                    Errors = ["NO_MARKET_DATA_AVAILABLE", .. tickResult.Errors, .. ohlcResult.Errors]
                };
            }

            return ohlcResult with
            {
                Warnings = warnings,
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
            bool backfill,
            bool fallbackUsed,
            CancellationToken cancellationToken)
        {
            string outputPath = TickPath(dataDirectory, symbol);
            var existing = LoadExistingTicks(outputPath);
            DateTime fromUtc = ResolveFromUtc(existing.Select(t => t.TimestampUtc), lookbackDays, maxDays, toUtc, backfill);
            var fetched = await _provider.GetTicksAsync(symbol, fromUtc, toUtc, maxRows, cancellationToken)
                .ConfigureAwait(false);

            if (!fetched.Success)
                return Failure(symbol, MarketDataUpdateType.Tick, fallbackUsed, existing.Count, fromUtc, toUtc, outputPath, fetched.Error, fetched);

            if (fetched.Rows.Count == 0 && existing.Count == 0)
                return Failure(
                    symbol,
                    MarketDataUpdateType.Tick,
                    fallbackUsed,
                    existing.Count,
                    fromUtc,
                    toUtc,
                    outputPath,
                    "TICK_DATA_UNAVAILABLE_FALLING_BACK_TO_M1: MT5 GET_TICKS returned 0 rows.",
                    fetched);

            var fetchedRows = fetched.Rows
                .Select(r => r with
                {
                    Symbol = symbol.ToUpperInvariant(),
                    TimestampUtc = EnsureUtc(r.TimestampUtc)
                });

            var mergedBeforeRetention = existing
                .Concat(fetchedRows)
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

            return Success(symbol, MarketDataUpdateType.Tick, fallbackUsed, existing.Count, fetched.Rows.Count, merged.Count, rowsRemovedByRetention, fromUtc, toUtc, outputPath, LastTimestamp(merged.Select(t => t.TimestampUtc)), fetched);
        }

        private async Task<HistoricalMarketDataSymbolResult> UpdateOhlcAsync(
            string symbol,
            string dataDirectory,
            int lookbackDays,
            int maxDays,
            int maxRows,
            int retentionDays,
            DateTime toUtc,
            bool backfill,
            bool fallbackUsed,
            CancellationToken cancellationToken)
        {
            string outputPath = OhlcPath(dataDirectory, symbol);
            var existing = LoadExistingOhlc(outputPath);
            DateTime fromUtc = ResolveFromUtc(existing.Select(c => c.TimestampUtc), lookbackDays, maxDays, toUtc, backfill);
            var fetched = await _provider.GetOhlcM1Async(symbol, fromUtc, toUtc, maxRows, cancellationToken)
                .ConfigureAwait(false);

            if (!fetched.Success)
                return Failure(symbol, MarketDataUpdateType.OHLC, fallbackUsed, existing.Count, fromUtc, toUtc, outputPath, fetched.Error, fetched);

            if (fetched.Rows.Count == 0 && existing.Count == 0)
                return Failure(
                    symbol,
                    MarketDataUpdateType.OHLC,
                    fallbackUsed,
                    existing.Count,
                    fromUtc,
                    toUtc,
                    outputPath,
                    "NO_MARKET_DATA_AVAILABLE: MT5 GET_RATES returned 0 rows.",
                    fetched);

            var fetchedRows = fetched.Rows
                .Select(r => r with
                {
                    Symbol = symbol.ToUpperInvariant(),
                    Timeframe = string.IsNullOrWhiteSpace(r.Timeframe) ? "M1" : r.Timeframe.ToUpperInvariant(),
                    TimestampUtc = EnsureUtc(r.TimestampUtc)
                });

            var mergedBeforeRetention = existing
                .Concat(fetchedRows)
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

            return Success(symbol, MarketDataUpdateType.OHLC, fallbackUsed, existing.Count, fetched.Rows.Count, merged.Count, rowsRemovedByRetention, fromUtc, toUtc, outputPath, LastTimestamp(merged.Select(c => c.TimestampUtc)), fetched);
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
            DateTime? lastUpdatedUtc,
            object? providerResult = null) => new()
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
            OutputFilePath = outputPath,
            ProviderCommand = ProviderCommand(providerResult),
            ProviderRowsReturned = ProviderRowsReturned(providerResult),
            ProviderDiagnostic = ProviderDiagnostic(providerResult)
        };

        private static HistoricalMarketDataSymbolResult Failure(
            string symbol,
            MarketDataUpdateType dataType,
            bool fallbackUsed,
            int rowsBefore,
            DateTime fromUtc,
            DateTime toUtc,
            string outputPath,
            string error,
            object? providerResult = null) => new()
        {
            Symbol = symbol,
            DataTypeUsed = dataType,
            FallbackUsed = fallbackUsed,
            RowsBefore = rowsBefore,
            RowsAfter = rowsBefore,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            OutputFilePath = outputPath,
            ProviderCommand = ProviderCommand(providerResult),
            ProviderRowsReturned = ProviderRowsReturned(providerResult),
            ProviderDiagnostic = ProviderDiagnostic(providerResult),
            Errors = [string.IsNullOrWhiteSpace(error) ? $"No {dataType} data returned for {symbol}." : error]
        };

        private static string ProviderCommand(object? providerResult) =>
            providerResult switch
            {
                HistoricalMarketDataProviderResult<BacktestTick> tick => tick.CommandName,
                HistoricalMarketDataProviderResult<BacktestOhlcCandle> ohlc => ohlc.CommandName,
                _ => ""
            };

        private static int ProviderRowsReturned(object? providerResult) =>
            providerResult switch
            {
                HistoricalMarketDataProviderResult<BacktestTick> tick => tick.RowsReturned,
                HistoricalMarketDataProviderResult<BacktestOhlcCandle> ohlc => ohlc.RowsReturned,
                _ => 0
            };

        private static string ProviderDiagnostic(object? providerResult) =>
            providerResult switch
            {
                HistoricalMarketDataProviderResult<BacktestTick> tick => tick.Diagnostic,
                HistoricalMarketDataProviderResult<BacktestOhlcCandle> ohlc => ohlc.Diagnostic,
                _ => ""
            };

        private static List<BacktestTick> LoadExistingTicks(string outputPath)
        {
            if (!File.Exists(outputPath)) return [];
            return IsHeaderOnlyCsv(outputPath)
                ? []
                : new CsvBacktestTickDataLoader().LoadAsync(outputPath).GetAwaiter().GetResult().ToList();
        }

        private static List<BacktestOhlcCandle> LoadExistingOhlc(string outputPath)
        {
            if (!File.Exists(outputPath)) return [];
            return IsHeaderOnlyCsv(outputPath)
                ? []
                : new CsvBacktestOhlcDataLoader().LoadAsync(outputPath).GetAwaiter().GetResult().ToList();
        }

        private static bool IsHeaderOnlyCsv(string outputPath)
        {
            string[] lines = File.ReadAllLines(outputPath);
            return lines.Length <= 1 || lines.Skip(1).All(string.IsNullOrWhiteSpace);
        }

        private static DateTime ResolveFromUtc(
            IEnumerable<DateTime> timestampsUtc,
            int lookbackDays,
            int maxDays,
            DateTime toUtc,
            bool backfill)
        {
            if (!backfill)
            {
                DateTime? last = timestampsUtc.OrderByDescending(t => t).FirstOrDefault();
                if (last.HasValue && last.Value != default)
                    return EnsureUtc(last.Value).AddMilliseconds(1);
            }

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
