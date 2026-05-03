using System.Globalization;

namespace MT5TradingBot.Modules.Backtesting
{
    public sealed class CsvBacktestTickDataLoader : IBacktestMarketDataLoader<BacktestTick>
    {
        public Task<IReadOnlyList<BacktestTick>> LoadAsync(
            string filePath,
            string? symbolFilter = null,
            CancellationToken cancellationToken = default)
        {
            var parser = CsvMarketDataParser.Load(filePath, ["timestamp", "symbol", "bid", "ask"]);
            var rows = new List<BacktestTick>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in parser.Rows)
            {
                cancellationToken.ThrowIfCancellationRequested();

                DateTime timestampUtc = CsvMarketDataParser.ParseUtcTimestamp(row, "timestamp");
                string symbol = CsvMarketDataParser.Required(row, "symbol").ToUpperInvariant();
                if (!MatchesSymbol(symbol, symbolFilter)) continue;

                double bid = CsvMarketDataParser.ParsePositiveDouble(row, "bid");
                double ask = CsvMarketDataParser.ParsePositiveDouble(row, "ask");
                if (bid > ask)
                    throw CsvMarketDataParser.Error(row.LineNumber, "bid must be less than or equal to ask.");

                string key = $"{symbol}|{timestampUtc:O}";
                if (!seen.Add(key))
                    throw CsvMarketDataParser.Error(row.LineNumber,
                        $"Duplicate tick timestamp for {symbol} at {timestampUtc:O}.");

                rows.Add(new BacktestTick
                {
                    TimestampUtc = timestampUtc,
                    Symbol = symbol,
                    Bid = bid,
                    Ask = ask,
                    Volume = CsvMarketDataParser.ParseOptionalNonNegativeDouble(row, "volume")
                });
            }

            return Task.FromResult<IReadOnlyList<BacktestTick>>(
                rows.OrderBy(r => r.TimestampUtc).ThenBy(r => r.Symbol, StringComparer.OrdinalIgnoreCase).ToList());
        }

        private static bool MatchesSymbol(string symbol, string? symbolFilter) =>
            string.IsNullOrWhiteSpace(symbolFilter) ||
            string.Equals(symbol, symbolFilter.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public sealed class CsvBacktestOhlcDataLoader : IBacktestMarketDataLoader<BacktestOhlcCandle>
    {
        public Task<IReadOnlyList<BacktestOhlcCandle>> LoadAsync(
            string filePath,
            string? symbolFilter = null,
            CancellationToken cancellationToken = default)
        {
            var parser = CsvMarketDataParser.Load(
                filePath,
                ["timestamp", "symbol", "timeframe", "open", "high", "low", "close"]);
            var rows = new List<BacktestOhlcCandle>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in parser.Rows)
            {
                cancellationToken.ThrowIfCancellationRequested();

                DateTime timestampUtc = CsvMarketDataParser.ParseUtcTimestamp(row, "timestamp");
                string symbol = CsvMarketDataParser.Required(row, "symbol").ToUpperInvariant();
                if (!MatchesSymbol(symbol, symbolFilter)) continue;

                string timeframe = CsvMarketDataParser.Required(row, "timeframe").ToUpperInvariant();
                double open = CsvMarketDataParser.ParsePositiveDouble(row, "open");
                double high = CsvMarketDataParser.ParsePositiveDouble(row, "high");
                double low = CsvMarketDataParser.ParsePositiveDouble(row, "low");
                double close = CsvMarketDataParser.ParsePositiveDouble(row, "close");
                if (low > high)
                    throw CsvMarketDataParser.Error(row.LineNumber, "low must be less than or equal to high.");
                if (open < low || open > high || close < low || close > high)
                    throw CsvMarketDataParser.Error(row.LineNumber, "open and close must be inside low/high range.");

                string key = $"{symbol}|{timeframe}|{timestampUtc:O}";
                if (!seen.Add(key))
                    throw CsvMarketDataParser.Error(row.LineNumber,
                        $"Duplicate OHLC timestamp for {symbol} {timeframe} at {timestampUtc:O}.");

                rows.Add(new BacktestOhlcCandle
                {
                    TimestampUtc = timestampUtc,
                    Symbol = symbol,
                    Timeframe = timeframe,
                    Open = open,
                    High = high,
                    Low = low,
                    Close = close,
                    BidOpen = CsvMarketDataParser.ParseOptionalPositiveDouble(row, "bid_open"),
                    BidHigh = CsvMarketDataParser.ParseOptionalPositiveDouble(row, "bid_high"),
                    BidLow = CsvMarketDataParser.ParseOptionalPositiveDouble(row, "bid_low"),
                    BidClose = CsvMarketDataParser.ParseOptionalPositiveDouble(row, "bid_close"),
                    AskOpen = CsvMarketDataParser.ParseOptionalPositiveDouble(row, "ask_open"),
                    AskHigh = CsvMarketDataParser.ParseOptionalPositiveDouble(row, "ask_high"),
                    AskLow = CsvMarketDataParser.ParseOptionalPositiveDouble(row, "ask_low"),
                    AskClose = CsvMarketDataParser.ParseOptionalPositiveDouble(row, "ask_close"),
                    SpreadPips = CsvMarketDataParser.ParseOptionalNonNegativeDouble(row, "spread_pips"),
                    Volume = CsvMarketDataParser.ParseOptionalNonNegativeDouble(row, "volume")
                });
            }

            return Task.FromResult<IReadOnlyList<BacktestOhlcCandle>>(
                rows.OrderBy(r => r.TimestampUtc).ThenBy(r => r.Symbol, StringComparer.OrdinalIgnoreCase).ToList());
        }

        private static bool MatchesSymbol(string symbol, string? symbolFilter) =>
            string.IsNullOrWhiteSpace(symbolFilter) ||
            string.Equals(symbol, symbolFilter.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    internal sealed class CsvMarketDataParser
    {
        private readonly Dictionary<string, int> _headers;

        private CsvMarketDataParser(string[] lines, IReadOnlySet<string> requiredColumns)
        {
            if (lines.Length < 2)
                throw new BacktestMarketDataLoadException("CSV must contain a header and at least one data row.");

            _headers = lines[0]
                .Split(',')
                .Select((h, i) => new { Header = Normalize(h), Index = i })
                .Where(h => !string.IsNullOrWhiteSpace(h.Header))
                .ToDictionary(h => h.Header, h => h.Index, StringComparer.OrdinalIgnoreCase);

            foreach (string column in requiredColumns)
            {
                if (!_headers.ContainsKey(column))
                    throw new BacktestMarketDataLoadException($"CSV is missing required column '{column}'.");
            }

            Rows = lines
                .Skip(1)
                .Select((line, i) => new CsvMarketDataRow(i + 2, line, _headers))
                .Where(r => !string.IsNullOrWhiteSpace(r.RawLine))
                .ToList();
        }

        public IReadOnlyList<CsvMarketDataRow> Rows { get; }

        public static CsvMarketDataParser Load(string filePath, IReadOnlyList<string> requiredColumns)
        {
            if (!File.Exists(filePath))
                throw new BacktestMarketDataLoadException($"CSV file not found: {filePath}");

            return new CsvMarketDataParser(
                File.ReadAllLines(filePath),
                new HashSet<string>(requiredColumns.Select(Normalize), StringComparer.OrdinalIgnoreCase));
        }

        public static DateTime ParseUtcTimestamp(CsvMarketDataRow row, string column)
        {
            string value = Required(row, column);
            if (!DateTimeOffset.TryParse(
                    value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var dto))
            {
                throw Error(row.LineNumber, $"Invalid UTC timestamp in column '{column}': '{value}'.");
            }

            if (!HasExplicitUtcOffset(value))
                throw Error(row.LineNumber,
                    $"Timestamp '{value}' must include UTC marker 'Z' or an explicit offset.");

            return dto.UtcDateTime;
        }

        public static string Required(CsvMarketDataRow row, string column)
        {
            string value = row.Get(column);
            if (string.IsNullOrWhiteSpace(value))
                throw Error(row.LineNumber, $"Missing required value for column '{column}'.");

            return value.Trim();
        }

        public static double ParsePositiveDouble(CsvMarketDataRow row, string column)
        {
            double value = ParseDouble(row, column, required: true)!.Value;
            if (!IsFinite(value) || value <= 0)
                throw Error(row.LineNumber, $"Column '{column}' must be a positive price.");

            return value;
        }

        public static double? ParseOptionalPositiveDouble(CsvMarketDataRow row, string column)
        {
            double? value = ParseDouble(row, column, required: false);
            if (!value.HasValue) return null;
            if (!IsFinite(value.Value) || value.Value <= 0)
                throw Error(row.LineNumber, $"Column '{column}' must be positive when provided.");

            return value;
        }

        public static double? ParseOptionalNonNegativeDouble(CsvMarketDataRow row, string column)
        {
            double? value = ParseDouble(row, column, required: false);
            if (!value.HasValue) return null;
            if (!IsFinite(value.Value) || value.Value < 0)
                throw Error(row.LineNumber, $"Column '{column}' must be non-negative when provided.");

            return value;
        }

        public static BacktestMarketDataLoadException Error(int lineNumber, string message) =>
            new($"CSV row {lineNumber}: {message}");

        private static double? ParseDouble(CsvMarketDataRow row, string column, bool required)
        {
            string value = row.Get(column);
            if (string.IsNullOrWhiteSpace(value))
            {
                if (required)
                    throw Error(row.LineNumber, $"Missing required value for column '{column}'.");

                return null;
            }

            if (!double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
                throw Error(row.LineNumber, $"Invalid numeric value for column '{column}': '{value}'.");

            return parsed;
        }

        private static string Normalize(string value) =>
            value.Trim().ToLowerInvariant();

        private static bool IsFinite(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value);

        private static bool HasExplicitUtcOffset(string value)
        {
            string trimmed = value.Trim();
            if (trimmed.EndsWith("Z", StringComparison.OrdinalIgnoreCase))
                return true;

            int timeSeparator = Math.Max(trimmed.LastIndexOf('T'), trimmed.LastIndexOf(' '));
            if (timeSeparator < 0) return false;

            string timePart = trimmed[(timeSeparator + 1)..];
            return timePart.Contains('+') || timePart.LastIndexOf('-') > 0;
        }
    }

    internal sealed class CsvMarketDataRow
    {
        private readonly string[] _columns;
        private readonly IReadOnlyDictionary<string, int> _headers;

        public CsvMarketDataRow(int lineNumber, string rawLine, IReadOnlyDictionary<string, int> headers)
        {
            LineNumber = lineNumber;
            RawLine = rawLine;
            _headers = headers;
            _columns = rawLine.Split(',');
        }

        public int LineNumber { get; }
        public string RawLine { get; }

        public string Get(string column)
        {
            string normalized = column.Trim().ToLowerInvariant();
            return _headers.TryGetValue(normalized, out int index) && index < _columns.Length
                ? _columns[index]
                : "";
        }
    }
}
