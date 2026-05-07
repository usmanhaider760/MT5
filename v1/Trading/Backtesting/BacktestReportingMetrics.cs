namespace MT5TradingBot.Modules.Backtesting
{
    public sealed record BacktestReportTrade
    {
        public string Id { get; init; } = "";
        public DateTime TimestampUtc { get; init; }
        public string Symbol { get; init; } = "";
        public string Session { get; init; } = "";
        public string SpreadRegime { get; init; } = "";
        public double ProfitLossUsd { get; init; }
        public double? RMultiple { get; init; }
        public double? CommissionUsd { get; init; }
        public double? SlippageUsd { get; init; }
        public double? SpreadCostUsd { get; init; }
    }

    public sealed record BacktestMetricSummary
    {
        public int TotalTrades { get; init; }
        public int WinningTrades { get; init; }
        public int LosingTrades { get; init; }
        public double WinRatePercent { get; init; }
        public double GrossProfitUsd { get; init; }
        public double GrossLossUsd { get; init; }
        public double NetProfitUsd { get; init; }
        public double ProfitFactor { get; init; }
        public bool ProfitFactorUnlimited { get; init; }
        public double ExpectancyPerTradeUsd { get; init; }
        public double MaxDrawdownUsd { get; init; }
        public int WorstLosingStreak { get; init; }
        public double AverageWinUsd { get; init; }
        public double AverageLossUsd { get; init; }
        public double? AverageRMultiple { get; init; }
        public double TotalCommissionUsd { get; init; }
        public double TotalSlippageUsd { get; init; }
        public double TotalSpreadCostUsd { get; init; }
        public double TotalExecutionCostUsd { get; init; }
    }

    public sealed record BacktestGroupedMetrics
    {
        public string Key { get; init; } = "";
        public BacktestMetricSummary Metrics { get; init; } = new();
    }

    public sealed record BacktestMetricsReport
    {
        public bool Success { get; init; }
        public string FailureCode { get; init; } = "";
        public string FailureReason { get; init; } = "";
        public IReadOnlyList<string> Warnings { get; init; } = [];
        public BacktestMetricSummary Overall { get; init; } = new();
        public IReadOnlyList<BacktestGroupedMetrics> BySymbol { get; init; } = [];
        public IReadOnlyList<BacktestGroupedMetrics> BySession { get; init; } = [];
        public IReadOnlyList<BacktestGroupedMetrics> BySpreadRegime { get; init; } = [];
        public IReadOnlyDictionary<string, string> AssumptionsUsed { get; init; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public static class BacktestReportingMetrics
    {
        private const string UnknownGroup = "UNKNOWN";

        public static BacktestMetricsReport BuildReport(
            IEnumerable<BacktestReportTrade> trades,
            IReadOnlyDictionary<string, string>? assumptionsUsed = null)
        {
            var list = trades
                .OrderBy(t => EnsureUtc(t.TimestampUtc))
                .ToList();

            if (list.Count == 0)
            {
                return new BacktestMetricsReport
                {
                    Success = false,
                    FailureCode = "BACKTEST_REPORT_NO_TRADES",
                    FailureReason = "No trades were supplied for backtest reporting metrics.",
                    AssumptionsUsed = CopyAssumptions(assumptionsUsed)
                };
            }

            var warnings = BuildWarnings(list);

            return new BacktestMetricsReport
            {
                Success = true,
                Warnings = warnings,
                Overall = Calculate(list),
                BySymbol = Group(list, t => NormalizeGroup(t.Symbol)),
                BySession = Group(list, t => NormalizeGroup(t.Session)),
                BySpreadRegime = Group(list, t => NormalizeGroup(t.SpreadRegime)),
                AssumptionsUsed = CopyAssumptions(assumptionsUsed)
            };
        }

        private static BacktestMetricSummary Calculate(IReadOnlyList<BacktestReportTrade> trades)
        {
            int total = trades.Count;
            int wins = trades.Count(t => t.ProfitLossUsd > 0);
            int losses = trades.Count(t => t.ProfitLossUsd < 0);

            double grossProfit = trades.Where(t => t.ProfitLossUsd > 0).Sum(t => t.ProfitLossUsd);
            double grossLoss = Math.Abs(trades.Where(t => t.ProfitLossUsd < 0).Sum(t => t.ProfitLossUsd));
            double netProfit = trades.Sum(t => t.ProfitLossUsd);
            bool pfUnlimited = grossLoss == 0 && grossProfit > 0;
            double profitFactor = grossLoss > 0
                ? grossProfit / grossLoss
                : pfUnlimited
                    ? double.PositiveInfinity
                    : 0;

            double totalCommission = trades.Sum(t => t.CommissionUsd ?? 0);
            double totalSlippage = trades.Sum(t => t.SlippageUsd ?? 0);
            double totalSpread = trades.Sum(t => t.SpreadCostUsd ?? 0);

            var rMultiples = trades
                .Where(t => t.RMultiple.HasValue)
                .Select(t => t.RMultiple!.Value)
                .ToList();

            return new BacktestMetricSummary
            {
                TotalTrades = total,
                WinningTrades = wins,
                LosingTrades = losses,
                WinRatePercent = Math.Round((double)wins / total * 100.0, 2),
                GrossProfitUsd = Round(grossProfit),
                GrossLossUsd = Round(grossLoss),
                NetProfitUsd = Round(netProfit),
                ProfitFactor = double.IsPositiveInfinity(profitFactor) ? profitFactor : Round(profitFactor),
                ProfitFactorUnlimited = pfUnlimited,
                ExpectancyPerTradeUsd = Round(netProfit / total),
                MaxDrawdownUsd = Round(MaxDrawdown(trades)),
                WorstLosingStreak = WorstLosingStreak(trades),
                AverageWinUsd = wins > 0 ? Round(grossProfit / wins) : 0,
                AverageLossUsd = losses > 0 ? Round(grossLoss / losses) : 0,
                AverageRMultiple = rMultiples.Count > 0 ? Round(rMultiples.Average()) : null,
                TotalCommissionUsd = Round(totalCommission),
                TotalSlippageUsd = Round(totalSlippage),
                TotalSpreadCostUsd = Round(totalSpread),
                TotalExecutionCostUsd = Round(totalCommission + totalSlippage + totalSpread)
            };
        }

        private static IReadOnlyList<BacktestGroupedMetrics> Group(
            IReadOnlyList<BacktestReportTrade> trades,
            Func<BacktestReportTrade, string> keySelector) =>
            trades
                .GroupBy(keySelector, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g => new BacktestGroupedMetrics
                {
                    Key = g.Key,
                    Metrics = Calculate(g.ToList())
                })
                .ToList();

        private static double MaxDrawdown(IReadOnlyList<BacktestReportTrade> trades)
        {
            double equity = 0;
            double peak = 0;
            double maxDrawdown = 0;

            foreach (var trade in trades)
            {
                equity += trade.ProfitLossUsd;
                if (equity > peak)
                    peak = equity;

                double drawdown = peak - equity;
                if (drawdown > maxDrawdown)
                    maxDrawdown = drawdown;
            }

            return maxDrawdown;
        }

        private static int WorstLosingStreak(IReadOnlyList<BacktestReportTrade> trades)
        {
            int current = 0;
            int worst = 0;

            foreach (var trade in trades)
            {
                if (trade.ProfitLossUsd < 0)
                {
                    current++;
                    if (current > worst)
                        worst = current;
                }
                else
                {
                    current = 0;
                }
            }

            return worst;
        }

        private static List<string> BuildWarnings(IReadOnlyList<BacktestReportTrade> trades)
        {
            var warnings = new List<string>();

            if (trades.Any(t => !t.CommissionUsd.HasValue))
                warnings.Add("One or more trades are missing commission cost data; missing commission values were treated as 0.");
            if (trades.Any(t => !t.SlippageUsd.HasValue))
                warnings.Add("One or more trades are missing slippage cost data; missing slippage values were treated as 0.");
            if (trades.Any(t => !t.SpreadCostUsd.HasValue))
                warnings.Add("One or more trades are missing spread cost data; missing spread values were treated as 0.");
            if (trades.Any(t => string.IsNullOrWhiteSpace(t.Session)))
                warnings.Add("One or more trades are missing session labels and were grouped as UNKNOWN.");
            if (trades.Any(t => string.IsNullOrWhiteSpace(t.SpreadRegime)))
                warnings.Add("One or more trades are missing spread-regime labels and were grouped as UNKNOWN.");
            if (trades.All(t => !t.RMultiple.HasValue))
                warnings.Add("R-multiple data is unavailable; average R multiple was not calculated.");

            return warnings;
        }

        private static string NormalizeGroup(string value) =>
            string.IsNullOrWhiteSpace(value) ? UnknownGroup : value.Trim();

        private static IReadOnlyDictionary<string, string> CopyAssumptions(
            IReadOnlyDictionary<string, string>? assumptionsUsed) =>
            assumptionsUsed == null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(assumptionsUsed, StringComparer.OrdinalIgnoreCase);

        private static DateTime EnsureUtc(DateTime timestamp) =>
            timestamp.Kind == DateTimeKind.Utc
                ? timestamp
                : DateTime.SpecifyKind(timestamp, DateTimeKind.Utc);

        private static double Round(double value) =>
            Math.Round(value, 2);
    }
}
