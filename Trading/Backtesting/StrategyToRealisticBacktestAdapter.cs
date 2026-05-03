using MT5TradingBot.Models;

namespace MT5TradingBot.Modules.Backtesting
{
    public sealed record StrategyBacktestAdapterOptions
    {
        public DateTime? TimestampUtc { get; init; }
        public double? HistoricalMarketPrice { get; init; }
        public double? LotSize { get; init; }
        public string Session { get; init; } = "";
        public string SpreadRegime { get; init; } = "";
        public double? SpreadPips { get; init; }
        public string? SourceSignalReason { get; init; }
        public double? SourceSignalConfidence { get; init; }
    }

    public sealed record StrategyBacktestAdapterResult
    {
        public bool CandidateCreated { get; init; }
        public RealisticBacktestTradeCandidate? Candidate { get; init; }
        public string SkipCode { get; init; } = "";
        public string SkipReason { get; init; } = "";
        public IReadOnlyList<string> Warnings { get; init; } = [];
    }

    public static class StrategyToRealisticBacktestAdapter
    {
        public const string StrategyHoldCode = "STRATEGY_HOLD";
        public const string StrategySignalIncompleteCode = "STRATEGY_SIGNAL_INCOMPLETE";

        public static StrategyBacktestAdapterResult FromMarketSignal(
            MarketSignal? signal,
            StrategyBacktestAdapterOptions? options = null)
        {
            options ??= new StrategyBacktestAdapterOptions();

            if (signal == null)
                return Skipped(StrategySignalIncompleteCode, "Strategy signal is missing.");

            if (signal.Direction == SignalDirection.Hold)
                return Skipped(StrategyHoldCode, "Strategy signal direction is HOLD.");

            if (signal.Direction is not SignalDirection.Buy and not SignalDirection.Sell)
                return Skipped(StrategySignalIncompleteCode, "Strategy signal direction is missing or unsupported.");

            var missing = MissingCommonFields(
                signal.Pair,
                signal.StopLoss,
                signal.TakeProfit,
                options.LotSize);
            if (missing.Count > 0)
                return Skipped(
                    StrategySignalIncompleteCode,
                    "Strategy signal is incomplete: " + string.Join(", ", missing) + ".");

            double entryPrice = ResolveEntryPrice(signal.EntryPrice, options.HistoricalMarketPrice);
            var warnings = new List<string>();
            string entryPlaceholder = "";
            if (entryPrice <= 0)
            {
                entryPlaceholder = "MARKET_PRICE_FROM_HISTORICAL_DATA";
                warnings.Add("Entry price is a market-price placeholder; the runner must resolve it from historical data before full simulation.");
            }

            return Created(new RealisticBacktestTradeCandidate
            {
                Id = signal.Id,
                TimestampUtc = EnsureUtc(options.TimestampUtc ?? signal.CreatedAt),
                Symbol = signal.Pair,
                Direction = signal.Direction == SignalDirection.Buy ? TradeType.BUY : TradeType.SELL,
                EntryPrice = entryPrice,
                EntryRulePlaceholder = entryPlaceholder,
                StopLoss = signal.StopLoss,
                TakeProfit = signal.TakeProfit,
                LotSize = options.LotSize.GetValueOrDefault(),
                Session = options.Session,
                SpreadRegime = options.SpreadRegime,
                SpreadPips = options.SpreadPips,
                SourceSignalReason = options.SourceSignalReason ?? signal.Reason,
                SourceSignalConfidence = options.SourceSignalConfidence
            }, warnings);
        }

        public static StrategyBacktestAdapterResult FromTradeRequest(
            TradeRequest? request,
            StrategyBacktestAdapterOptions? options = null)
        {
            options ??= new StrategyBacktestAdapterOptions();

            if (request == null)
                return Skipped(StrategySignalIncompleteCode, "Scalping trade request is missing.");

            var missing = MissingCommonFields(
                request.Pair,
                request.StopLoss,
                request.TakeProfit,
                request.LotSize);
            if (missing.Count > 0)
                return Skipped(
                    StrategySignalIncompleteCode,
                    "Scalping trade request is incomplete: " + string.Join(", ", missing) + ".");

            double entryPrice = ResolveEntryPrice(request.EntryPrice, options.HistoricalMarketPrice);
            var warnings = new List<string>();
            string entryPlaceholder = "";
            if (entryPrice <= 0)
            {
                entryPlaceholder = "MARKET_PRICE_FROM_HISTORICAL_DATA";
                warnings.Add("Entry price is a market-price placeholder; the runner must resolve it from historical data before full simulation.");
            }

            return Created(new RealisticBacktestTradeCandidate
            {
                Id = request.Id,
                TimestampUtc = EnsureUtc(options.TimestampUtc ?? request.CreatedAt),
                Symbol = request.Pair,
                Direction = request.TradeType,
                EntryPrice = entryPrice,
                EntryRulePlaceholder = entryPlaceholder,
                StopLoss = request.StopLoss,
                TakeProfit = request.TakeProfit,
                LotSize = request.LotSize,
                Session = options.Session,
                SpreadRegime = options.SpreadRegime,
                SpreadPips = options.SpreadPips,
                SourceSignalReason = options.SourceSignalReason ?? request.Comment,
                SourceSignalConfidence = options.SourceSignalConfidence
            }, warnings);
        }

        private static IReadOnlyList<string> MissingCommonFields(
            string symbol,
            double stopLoss,
            double takeProfit,
            double? lotSize)
        {
            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(symbol))
                missing.Add("symbol");
            if (!IsPositive(stopLoss))
                missing.Add("stop loss");
            if (!IsPositive(takeProfit))
                missing.Add("take profit");
            if (!lotSize.HasValue || !IsPositive(lotSize.Value))
                missing.Add("lot size");
            return missing;
        }

        private static double ResolveEntryPrice(double signalEntryPrice, double? historicalMarketPrice) =>
            IsPositive(signalEntryPrice)
                ? signalEntryPrice
                : historicalMarketPrice.GetValueOrDefault();

        private static bool IsPositive(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;

        private static DateTime EnsureUtc(DateTime timestamp) =>
            timestamp.Kind == DateTimeKind.Utc
                ? timestamp
                : DateTime.SpecifyKind(timestamp, DateTimeKind.Utc);

        private static StrategyBacktestAdapterResult Created(
            RealisticBacktestTradeCandidate candidate,
            IReadOnlyList<string> warnings) => new()
        {
            CandidateCreated = true,
            Candidate = candidate,
            Warnings = warnings
        };

        private static StrategyBacktestAdapterResult Skipped(string code, string reason) => new()
        {
            CandidateCreated = false,
            SkipCode = code,
            SkipReason = reason
        };
    }
}
