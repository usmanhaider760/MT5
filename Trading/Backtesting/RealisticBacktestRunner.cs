using MT5TradingBot.Core;
using MT5TradingBot.Models;

namespace MT5TradingBot.Modules.Backtesting
{
    public enum RealisticBacktestOutcomeStatus
    {
        Successful,
        Rejected,
        Open
    }

    public sealed record RealisticBacktestTradeCandidate
    {
        public string Id { get; init; } = "";
        public DateTime TimestampUtc { get; init; }
        public string Symbol { get; init; } = "";
        public TradeType Direction { get; init; } = TradeType.BUY;
        public double EntryPrice { get; init; }
        public string EntryRulePlaceholder { get; init; } = "";
        public double StopLoss { get; init; }
        public double TakeProfit { get; init; }
        public double LotSize { get; init; }
        public string Session { get; init; } = "";
        public string SpreadRegime { get; init; } = "";
        public double? SpreadPips { get; init; }
        public string SourceSignalReason { get; init; } = "";
        public string SourceType { get; init; } = "";
        public double? SourceSignalConfidence { get; init; }
    }

    public sealed record RealisticBacktestRunInput
    {
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

    public sealed record RealisticBacktestTradeOutcome
    {
        public string CandidateId { get; init; } = "";
        public RealisticBacktestOutcomeStatus Status { get; init; }
        public DateTime TimestampUtc { get; init; }
        public string Symbol { get; init; } = "";
        public TradeType Direction { get; init; } = TradeType.BUY;
        public double EntryPrice { get; init; }
        public double? ExitPrice { get; init; }
        public DateTime? ExitTimestampUtc { get; init; }
        public IntrabarExitType ExitType { get; init; } = IntrabarExitType.None;
        public double GrossProfitLossUsd { get; init; }
        public double NetProfitLossUsd { get; init; }
        public double CommissionCostUsd { get; init; }
        public double SlippageCostUsd { get; init; }
        public double SpreadCostUsd { get; init; }
        public double TotalExecutionCostUsd { get; init; }
        public string RejectionCode { get; init; } = "";
        public string RejectionReason { get; init; } = "";
        public string Session { get; init; } = "";
        public string SpreadRegime { get; init; } = "";
        public IReadOnlyList<string> Warnings { get; init; } = [];
    }

    public sealed record RealisticBacktestResult
    {
        public bool Success { get; init; }
        public string FailureCode { get; init; } = "";
        public string FailureReason { get; init; } = "";
        public IReadOnlyList<string> Warnings { get; init; } = [];
        public IReadOnlyList<RealisticBacktestTradeOutcome> SuccessfulTrades { get; init; } = [];
        public IReadOnlyList<RealisticBacktestTradeOutcome> RejectedTrades { get; init; } = [];
        public IReadOnlyList<RealisticBacktestTradeOutcome> OpenTrades { get; init; } = [];
        public BacktestMetricsReport MetricsReport { get; init; } = new();
        public IReadOnlyDictionary<string, string> AssumptionsUsed { get; init; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public static class RealisticBacktestRunner
    {
        public static RealisticBacktestResult Run(RealisticBacktestRunInput input)
        {
            if (input.Candidates.Count == 0)
            {
                return new RealisticBacktestResult
                {
                    Success = false,
                    FailureCode = "REALISTIC_BACKTEST_NO_CANDIDATES",
                    FailureReason = "At least one trade candidate is required for realistic backtest runner.",
                    AssumptionsUsed = input.AssumptionsUsed
                };
            }

            var successful = new List<RealisticBacktestTradeOutcome>();
            var rejected = new List<RealisticBacktestTradeOutcome>();
            var open = new List<RealisticBacktestTradeOutcome>();
            var warnings = new List<string>();

            foreach (var candidate in input.Candidates.OrderBy(c => EnsureUtc(c.TimestampUtc)))
            {
                var outcome = EvaluateCandidate(candidate, input);
                warnings.AddRange(outcome.Warnings.Select(w => $"{candidate.Id}: {w}"));

                if (outcome.Status == RealisticBacktestOutcomeStatus.Successful)
                    successful.Add(outcome);
                else if (outcome.Status == RealisticBacktestOutcomeStatus.Rejected)
                    rejected.Add(outcome);
                else
                    open.Add(outcome);
            }

            var reportTrades = successful.Select(ToReportTrade).ToList();
            var metrics = BacktestReportingMetrics.BuildReport(reportTrades, input.AssumptionsUsed);
            if (!metrics.Success)
                warnings.Add(metrics.FailureReason);
            warnings.AddRange(metrics.Warnings);

            return new RealisticBacktestResult
            {
                Success = true,
                Warnings = warnings,
                SuccessfulTrades = successful,
                RejectedTrades = rejected,
                OpenTrades = open,
                MetricsReport = metrics,
                AssumptionsUsed = input.AssumptionsUsed
            };
        }

        private static RealisticBacktestTradeOutcome EvaluateCandidate(
            RealisticBacktestTradeCandidate candidate,
            RealisticBacktestRunInput input)
        {
            DateTime timestampUtc = EnsureUtc(candidate.TimestampUtc);
            double? spreadPips = ResolveSpreadPips(candidate, input, timestampUtc);
            var filter = BacktestNoTradeFilterSimulator.Evaluate(new BacktestNoTradeFilterInput
            {
                TimestampUtc = timestampUtc,
                Symbol = candidate.Symbol,
                SpreadPips = spreadPips,
                Config = input.Config,
                NewsConfig = input.NewsConfig,
                HistoricalNewsEvents = input.HistoricalNewsEvents
            });

            if (!filter.Allowed)
                return Rejected(candidate, filter.RejectionCode, filter.RejectionReason);

            if (!TryResolveSymbolInfo(candidate.Symbol, input, timestampUtc, out var symbolInfo))
            {
                return Rejected(
                    candidate,
                    "REALISTIC_BACKTEST_SYMBOL_METADATA_UNAVAILABLE",
                    $"Symbol metadata is unavailable for {candidate.Symbol}.");
            }

            var brokerRule = BacktestBrokerRuleSimulator.Validate(new BacktestBrokerRuleInput
            {
                Symbol = candidate.Symbol,
                TradeType = candidate.Direction,
                OrderType = OrderType.MARKET,
                EntryPrice = candidate.EntryPrice,
                StopLoss = candidate.StopLoss,
                TakeProfit = candidate.TakeProfit,
                LotSize = candidate.LotSize,
                SymbolInfo = symbolInfo
            });

            if (!brokerRule.Approved)
                return Rejected(candidate, brokerRule.RejectionCode, brokerRule.RejectionReason);

            var exit = ResolveExit(candidate, input, timestampUtc);
            if (!exit.ExitTriggered)
            {
                return new RealisticBacktestTradeOutcome
                {
                    CandidateId = candidate.Id,
                    Status = RealisticBacktestOutcomeStatus.Open,
                    TimestampUtc = timestampUtc,
                    Symbol = candidate.Symbol,
                    Direction = candidate.Direction,
                    EntryPrice = candidate.EntryPrice,
                    Session = candidate.Session,
                    SpreadRegime = candidate.SpreadRegime,
                    Warnings = [exit.Explanation]
                };
            }

            var cost = EstimateCost(candidate, input, timestampUtc, exit.ExitPrice, spreadPips);
            double gross = CalculateGrossProfitLoss(candidate, exit.ExitPrice);
            double net = Math.Round(gross - cost.TotalCostUsd, 2);
            var warnings = new List<string>();
            warnings.AddRange(filter.Warnings);
            warnings.AddRange(brokerRule.Warnings);
            warnings.AddRange(cost.Warnings);
            if (!cost.Success)
                warnings.Add("Execution cost simulation returned missing-data flags: " + string.Join(", ", cost.MissingDataFlags));

            return new RealisticBacktestTradeOutcome
            {
                CandidateId = candidate.Id,
                Status = RealisticBacktestOutcomeStatus.Successful,
                TimestampUtc = timestampUtc,
                Symbol = candidate.Symbol,
                Direction = candidate.Direction,
                EntryPrice = candidate.EntryPrice,
                ExitPrice = exit.ExitPrice,
                ExitTimestampUtc = exit.ExitTimestampUtc,
                ExitType = exit.ExitType,
                GrossProfitLossUsd = gross,
                NetProfitLossUsd = net,
                CommissionCostUsd = cost.CommissionCostUsd,
                SlippageCostUsd = cost.SlippageCostUsd,
                SpreadCostUsd = cost.SpreadCostUsd,
                TotalExecutionCostUsd = cost.TotalCostUsd,
                Session = candidate.Session,
                SpreadRegime = candidate.SpreadRegime,
                Warnings = warnings
            };
        }

        private static IntrabarExitResult ResolveExit(
            RealisticBacktestTradeCandidate candidate,
            RealisticBacktestRunInput input,
            DateTime timestampUtc)
        {
            var ticks = input.Ticks
                .Where(t => SymbolEquals(t.Symbol, candidate.Symbol) && EnsureUtc(t.TimestampUtc) >= timestampUtc)
                .OrderBy(t => t.TimestampUtc)
                .ToList();
            if (ticks.Count > 0)
                return IntrabarExitSimulator.SimulateTickExit(candidate.Direction, candidate.StopLoss, candidate.TakeProfit, ticks);

            var candles = input.Candles
                .Where(c => SymbolEquals(c.Symbol, candidate.Symbol) && EnsureUtc(c.TimestampUtc) >= timestampUtc)
                .OrderBy(c => c.TimestampUtc)
                .ToList();
            if (candles.Count == 0)
            {
                return new IntrabarExitResult
                {
                    ExitTriggered = false,
                    Explanation = "No tick or OHLC candle data was available to resolve SL/TP."
                };
            }

            foreach (var candle in candles)
            {
                var exit = IntrabarExitSimulator.SimulateOhlcExit(
                    candidate.Direction,
                    candidate.StopLoss,
                    candidate.TakeProfit,
                    candle);
                if (exit.ExitTriggered)
                    return exit;
            }

            return new IntrabarExitResult
            {
                ExitTriggered = false,
                Explanation = "Future OHLC candles found no SL/TP hit."
            };
        }

        private static BacktestExecutionCostResult EstimateCost(
            RealisticBacktestTradeCandidate candidate,
            RealisticBacktestRunInput input,
            DateTime timestampUtc,
            double exitPrice,
            double? spreadPips)
        {
            var tick = input.Ticks
                .Where(t => SymbolEquals(t.Symbol, candidate.Symbol) && EnsureUtc(t.TimestampUtc) >= timestampUtc)
                .OrderBy(t => t.TimestampUtc)
                .FirstOrDefault();

            return BacktestExecutionCostModel.Estimate(new BacktestExecutionCostInput
            {
                Symbol = candidate.Symbol,
                EntrySide = candidate.Direction,
                LotSize = candidate.LotSize,
                EntryPrice = candidate.EntryPrice,
                ExitPrice = exitPrice,
                Bid = tick?.Bid,
                Ask = tick?.Ask,
                SpreadPips = tick == null ? spreadPips : null,
                CommissionAndSlippageConfig = input.Config
            });
        }

        private static double? ResolveSpreadPips(
            RealisticBacktestTradeCandidate candidate,
            RealisticBacktestRunInput input,
            DateTime timestampUtc)
        {
            if (candidate.SpreadPips.HasValue)
                return candidate.SpreadPips.Value;

            var tick = input.Ticks
                .Where(t => SymbolEquals(t.Symbol, candidate.Symbol) && EnsureUtc(t.TimestampUtc) >= timestampUtc)
                .OrderBy(t => t.TimestampUtc)
                .FirstOrDefault();
            if (tick != null)
            {
                double pipSize = LotCalculator.GetPipSize(candidate.Symbol.ToUpperInvariant());
                if (pipSize > 0 && tick.Ask >= tick.Bid)
                    return Math.Round((tick.Ask - tick.Bid) / pipSize, 4);
            }

            return input.Candles
                .Where(c => SymbolEquals(c.Symbol, candidate.Symbol) && EnsureUtc(c.TimestampUtc) >= timestampUtc)
                .OrderBy(c => c.TimestampUtc)
                .Select(c => c.SpreadPips)
                .FirstOrDefault(s => s.HasValue);
        }

        private static bool TryResolveSymbolInfo(
            string symbol,
            RealisticBacktestRunInput input,
            DateTime timestampUtc,
            out SymbolInfo symbolInfo)
        {
            if (input.SymbolInfoBySymbol.TryGetValue(symbol, out var configured))
            {
                symbolInfo = configured;
                return true;
            }

            var tick = input.Ticks
                .Where(t => SymbolEquals(t.Symbol, symbol) && EnsureUtc(t.TimestampUtc) >= timestampUtc)
                .OrderBy(t => t.TimestampUtc)
                .FirstOrDefault();
            if (tick != null)
            {
                symbolInfo = DefaultSymbolInfo(symbol, tick.Bid, tick.Ask);
                return true;
            }

            symbolInfo = new SymbolInfo();
            return false;
        }

        private static SymbolInfo DefaultSymbolInfo(string symbol, double bid, double ask) => new()
        {
            Symbol = symbol,
            Bid = bid,
            Ask = ask,
            Spread = Math.Max(0, (ask - bid) / 0.00001),
            Digits = 5,
            MinLot = 0.01,
            MaxLot = 100,
            LotStep = 0.01,
            VolumeLimit = 0,
            PointSize = 0.00001,
            StopLevelPoints = 0,
            FreezeLevelPoints = 0
        };

        private static double CalculateGrossProfitLoss(
            RealisticBacktestTradeCandidate candidate,
            double exitPrice)
        {
            double pipSize = LotCalculator.GetPipSize(candidate.Symbol.ToUpperInvariant());
            double pips = PipCalculator.MoveInPips(candidate.Direction, candidate.EntryPrice, exitPrice, pipSize);
            double pipValue = LotCalculator.GetPipValuePerLot(candidate.Symbol.ToUpperInvariant(), candidate.EntryPrice);
            return Math.Round(pips * pipValue * candidate.LotSize, 2);
        }

        private static BacktestReportTrade ToReportTrade(RealisticBacktestTradeOutcome outcome) => new()
        {
            Id = outcome.CandidateId,
            TimestampUtc = outcome.ExitTimestampUtc ?? outcome.TimestampUtc,
            Symbol = outcome.Symbol,
            Session = outcome.Session,
            SpreadRegime = outcome.SpreadRegime,
            ProfitLossUsd = outcome.NetProfitLossUsd,
            CommissionUsd = outcome.CommissionCostUsd,
            SlippageUsd = outcome.SlippageCostUsd,
            SpreadCostUsd = outcome.SpreadCostUsd
        };

        private static RealisticBacktestTradeOutcome Rejected(
            RealisticBacktestTradeCandidate candidate,
            string code,
            string reason) => new()
        {
            CandidateId = candidate.Id,
            Status = RealisticBacktestOutcomeStatus.Rejected,
            TimestampUtc = EnsureUtc(candidate.TimestampUtc),
            Symbol = candidate.Symbol,
            Direction = candidate.Direction,
            EntryPrice = candidate.EntryPrice,
            RejectionCode = code,
            RejectionReason = reason,
            Session = candidate.Session,
            SpreadRegime = candidate.SpreadRegime
        };

        private static bool SymbolEquals(string left, string right) =>
            string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

        private static DateTime EnsureUtc(DateTime timestamp) =>
            timestamp.Kind == DateTimeKind.Utc
                ? timestamp
                : DateTime.SpecifyKind(timestamp, DateTimeKind.Utc);
    }
}
