using System.Globalization;
using System.Text;
using MT5TradingBot.Models;

namespace MT5TradingBot.Modules.Backtesting
{
    public sealed record RealisticBacktestReportRequest
    {
        public string OutputPath { get; init; } = RealisticBacktestReportCommand.DefaultReportFileName;
        public string? TickCsvPath { get; init; }
        public string? OhlcCsvPath { get; init; }
        public string? SymbolFilter { get; init; }
        public IReadOnlyList<RealisticBacktestTradeCandidate> Candidates { get; init; } = [];
        public IReadOnlyList<BacktestTick> Ticks { get; init; } = [];
        public IReadOnlyList<BacktestOhlcCandle> Candles { get; init; } = [];
        public IReadOnlyDictionary<string, SymbolInfo> SymbolInfoBySymbol { get; init; } =
            new Dictionary<string, SymbolInfo>(StringComparer.OrdinalIgnoreCase);
        public BotConfig Config { get; init; } = new();
        public ApiIntegrationConfig? NewsConfig { get; init; }
        public IReadOnlyList<HistoricalNewsEvent>? HistoricalNewsEvents { get; init; }
        public IReadOnlyDictionary<string, string> AssumptionsUsed { get; init; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public sealed record RealisticBacktestReportCommandResult
    {
        public bool Success { get; init; }
        public string OutputPath { get; init; } = "";
        public string Markdown { get; init; } = "";
        public RealisticBacktestResult BacktestResult { get; init; } = new();
    }

    public sealed class RealisticBacktestReportCommand
    {
        public const string DefaultReportFileName = "REALISTIC_BACKTEST_REPORT.md";

        public async Task<RealisticBacktestReportCommandResult> RunAsync(
            RealisticBacktestReportRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var ticks = request.Ticks.ToList();
            if (!string.IsNullOrWhiteSpace(request.TickCsvPath))
            {
                var loadedTicks = await new CsvBacktestTickDataLoader()
                    .LoadAsync(request.TickCsvPath, request.SymbolFilter, cancellationToken)
                    .ConfigureAwait(false);
                ticks.AddRange(loadedTicks);
            }

            var candles = request.Candles.ToList();
            if (!string.IsNullOrWhiteSpace(request.OhlcCsvPath))
            {
                var loadedCandles = await new CsvBacktestOhlcDataLoader()
                    .LoadAsync(request.OhlcCsvPath, request.SymbolFilter, cancellationToken)
                    .ConfigureAwait(false);
                candles.AddRange(loadedCandles);
            }

            var assumptions = BuildAssumptions(request, ticks, candles);
            var backtest = RealisticBacktestRunner.Run(new RealisticBacktestRunInput
            {
                Candidates = request.Candidates,
                Ticks = ticks.OrderBy(t => EnsureUtc(t.TimestampUtc)).ToList(),
                Candles = candles.OrderBy(c => EnsureUtc(c.TimestampUtc)).ToList(),
                SymbolInfoBySymbol = request.SymbolInfoBySymbol,
                Config = request.Config,
                NewsConfig = request.NewsConfig,
                HistoricalNewsEvents = request.HistoricalNewsEvents,
                AssumptionsUsed = assumptions
            });

            string markdown = BuildMarkdown(backtest, request.Candidates, ticks, candles, assumptions);
            string outputPath = string.IsNullOrWhiteSpace(request.OutputPath)
                ? DefaultReportFileName
                : request.OutputPath;
            string? directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            await File.WriteAllTextAsync(outputPath, markdown, cancellationToken).ConfigureAwait(false);

            return new RealisticBacktestReportCommandResult
            {
                Success = backtest.Success,
                OutputPath = Path.GetFullPath(outputPath),
                Markdown = markdown,
                BacktestResult = backtest
            };
        }

        public static RealisticBacktestReportRequest CreateMinimalExample(string outputPath = DefaultReportFileName)
        {
            DateTime start = new(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc);
            var config = new BotConfig
            {
                EnableCommissionModel = true,
                CommissionMode = "RoundTurn",
                CommissionPerLotPerSide = 4,
                EnableSlippageModel = true,
                EstimatedSlippagePips = 0.2,
                MaxAllowedSlippagePips = 2
            };

            return new RealisticBacktestReportRequest
            {
                OutputPath = outputPath,
                Config = config,
                Candidates =
                [
                    new RealisticBacktestTradeCandidate
                    {
                        Id = "EXAMPLE-WIN",
                        TimestampUtc = start,
                        Symbol = "EURUSD",
                        Direction = TradeType.BUY,
                        EntryPrice = 1.1000,
                        StopLoss = 1.0990,
                        TakeProfit = 1.1010,
                        LotSize = 0.10,
                        Session = "London",
                        SpreadRegime = "Tight"
                    },
                    new RealisticBacktestTradeCandidate
                    {
                        Id = "EXAMPLE-REJECT",
                        TimestampUtc = start.AddMinutes(1),
                        Symbol = "EURUSD",
                        Direction = TradeType.BUY,
                        EntryPrice = 1.1000,
                        StopLoss = 1.09995,
                        TakeProfit = 1.1010,
                        LotSize = 0.10,
                        Session = "London",
                        SpreadRegime = "Tight"
                    },
                    new RealisticBacktestTradeCandidate
                    {
                        Id = "EXAMPLE-OPEN",
                        TimestampUtc = start.AddMinutes(2),
                        Symbol = "EURUSD",
                        Direction = TradeType.BUY,
                        EntryPrice = 1.1000,
                        StopLoss = 1.0990,
                        TakeProfit = 1.1010,
                        LotSize = 0.10,
                        Session = "London",
                        SpreadRegime = "Tight"
                    }
                ],
                Ticks =
                [
                    new BacktestTick { TimestampUtc = start, Symbol = "EURUSD", Bid = 1.10000, Ask = 1.10010 },
                    new BacktestTick { TimestampUtc = start.AddSeconds(10), Symbol = "EURUSD", Bid = 1.10110, Ask = 1.10120 },
                    new BacktestTick { TimestampUtc = start.AddMinutes(2), Symbol = "EURUSD", Bid = 1.10000, Ask = 1.10010 }
                ],
                SymbolInfoBySymbol = new Dictionary<string, SymbolInfo>(StringComparer.OrdinalIgnoreCase)
                {
                    ["EURUSD"] = new SymbolInfo
                    {
                        Symbol = "EURUSD",
                        Bid = 1.10000,
                        Ask = 1.10010,
                        Spread = 10,
                        Digits = 5,
                        MinLot = 0.01,
                        MaxLot = 100,
                        LotStep = 0.01,
                        VolumeLimit = 0,
                        PointSize = 0.00001,
                        StopLevelPoints = 20,
                        FreezeLevelPoints = 0
                    }
                },
                AssumptionsUsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["candidate_source"] = "Built-in minimal fixture",
                    ["broker_connection"] = "Not used"
                }
            };
        }

        private static IReadOnlyDictionary<string, string> BuildAssumptions(
            RealisticBacktestReportRequest request,
            IReadOnlyList<BacktestTick> ticks,
            IReadOnlyList<BacktestOhlcCandle> candles)
        {
            var assumptions = new Dictionary<string, string>(
                request.AssumptionsUsed,
                StringComparer.OrdinalIgnoreCase)
            {
                ["simulation_type"] = "Realistic simulation only; not live proof",
                ["broker_connection"] = "No MT5 or live broker connection required",
                ["market_data"] = ticks.Count > 0
                    ? "CSV/provided bid-ask ticks"
                    : candles.Count > 0
                        ? "CSV/provided OHLC candles"
                        : "No market data supplied",
                ["candidate_source"] = request.Candidates.Count > 0
                    ? assumptionsValue(request.AssumptionsUsed, "candidate_source", "Externally provided candidates")
                    : "No candidates supplied",
                ["execution_costs"] = "BacktestExecutionCostModel spread, commission, and slippage estimates"
            };

            return assumptions;

            static string assumptionsValue(
                IReadOnlyDictionary<string, string> values,
                string key,
                string fallback) =>
                values.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value)
                    ? value
                    : fallback;
        }

        private static string BuildMarkdown(
            RealisticBacktestResult result,
            IReadOnlyList<RealisticBacktestTradeCandidate> candidates,
            IReadOnlyList<BacktestTick> ticks,
            IReadOnlyList<BacktestOhlcCandle> candles,
            IReadOnlyDictionary<string, string> assumptions)
        {
            var metrics = result.MetricsReport.Overall;
            var timestamps = candidates.Select(c => EnsureUtc(c.TimestampUtc))
                .Concat(result.SuccessfulTrades.Select(t => EnsureUtc(t.ExitTimestampUtc ?? t.TimestampUtc)))
                .Concat(result.OpenTrades.Select(t => EnsureUtc(t.TimestampUtc)))
                .Concat(result.RejectedTrades.Select(t => EnsureUtc(t.TimestampUtc)))
                .Concat(ticks.Select(t => EnsureUtc(t.TimestampUtc)))
                .Concat(candles.Select(c => EnsureUtc(c.TimestampUtc)))
                .OrderBy(t => t)
                .ToList();
            string period = timestamps.Count == 0
                ? "Unavailable"
                : $"{timestamps.First():yyyy-MM-dd HH:mm:ss} UTC to {timestamps.Last():yyyy-MM-dd HH:mm:ss} UTC";

            string symbols = string.Join(", ", candidates
                .Select(c => c.Symbol)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(symbols))
                symbols = "Unavailable";

            var sb = new StringBuilder();
            sb.AppendLine("# Realistic Backtest Report");
            sb.AppendLine();
            sb.AppendLine("Realistic simulation only. This report is not live proof and does not prove live broker execution quality.");
            sb.AppendLine();
            sb.AppendLine("## Summary");
            sb.AppendLine();
            sb.AppendLine("| Item | Value |");
            sb.AppendLine("|---|---:|");
            sb.AppendLine($"| Backtest period | {period} |");
            sb.AppendLine($"| Symbols tested | {symbols} |");
            sb.AppendLine($"| Total candidates | {candidates.Count.ToString(CultureInfo.InvariantCulture)} |");
            sb.AppendLine($"| Completed trades | {result.SuccessfulTrades.Count.ToString(CultureInfo.InvariantCulture)} |");
            sb.AppendLine($"| Rejected trades | {result.RejectedTrades.Count.ToString(CultureInfo.InvariantCulture)} |");
            sb.AppendLine($"| Unresolved/open trades | {result.OpenTrades.Count.ToString(CultureInfo.InvariantCulture)} |");
            sb.AppendLine($"| Total net profit | {Usd(metrics.NetProfitUsd)} |");
            sb.AppendLine($"| Profit factor | {FormatProfitFactor(metrics)} |");
            sb.AppendLine($"| Expectancy | {Usd(metrics.ExpectancyPerTradeUsd)} |");
            sb.AppendLine($"| Max drawdown | {Usd(metrics.MaxDrawdownUsd)} |");
            sb.AppendLine($"| Worst losing streak | {metrics.WorstLosingStreak.ToString(CultureInfo.InvariantCulture)} |");
            sb.AppendLine($"| Total commission | {Usd(metrics.TotalCommissionUsd)} |");
            sb.AppendLine($"| Total slippage | {Usd(metrics.TotalSlippageUsd)} |");
            sb.AppendLine($"| Total spread cost | {Usd(metrics.TotalSpreadCostUsd)} |");
            sb.AppendLine();
            sb.AppendLine("## Rejection Breakdown");
            sb.AppendLine();
            sb.AppendLine("| Reason | Count |");
            sb.AppendLine("|---|---:|");

            var rejectionGroups = result.RejectedTrades
                .GroupBy(t => string.IsNullOrWhiteSpace(t.RejectionCode) ? "UNKNOWN" : t.RejectionCode)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (rejectionGroups.Count == 0)
            {
                sb.AppendLine("| None | 0 |");
            }
            else
            {
                foreach (var group in rejectionGroups)
                    sb.AppendLine($"| {Escape(group.Key)} | {group.Count().ToString(CultureInfo.InvariantCulture)} |");
            }

            sb.AppendLine();
            sb.AppendLine("## Assumptions And Warnings");
            sb.AppendLine();
            foreach (var assumption in assumptions.OrderBy(a => a.Key, StringComparer.OrdinalIgnoreCase))
                sb.AppendLine($"- {assumption.Key}: {assumption.Value}");
            sb.AppendLine("- This is realistic simulation only, not live proof.");

            var warnings = result.Warnings
                .Concat(result.MetricsReport.Warnings)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(w => w, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (warnings.Count == 0)
                sb.AppendLine("- No additional warnings were produced.");
            else
                foreach (string warning in warnings)
                    sb.AppendLine($"- Warning: {warning}");

            return sb.ToString();
        }

        private static string FormatProfitFactor(BacktestMetricSummary metrics)
        {
            if (metrics.ProfitFactorUnlimited)
                return "Unlimited";

            return metrics.ProfitFactor.ToString("F2", CultureInfo.InvariantCulture);
        }

        private static string Usd(double value) =>
            value.ToString("0.00", CultureInfo.InvariantCulture) + " USD";

        private static string Escape(string value) =>
            value.Replace("|", "\\|", StringComparison.Ordinal);

        private static DateTime EnsureUtc(DateTime timestamp) =>
            timestamp.Kind == DateTimeKind.Utc
                ? timestamp
                : DateTime.SpecifyKind(timestamp, DateTimeKind.Utc);
    }
}
