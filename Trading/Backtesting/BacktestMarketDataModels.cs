namespace MT5TradingBot.Modules.Backtesting
{
    public sealed record BacktestTick
    {
        public DateTime TimestampUtc { get; init; }
        public string Symbol { get; init; } = "";
        public double Bid { get; init; }
        public double Ask { get; init; }
        public double? Volume { get; init; }
    }

    public sealed record BacktestOhlcCandle
    {
        public DateTime TimestampUtc { get; init; }
        public string Symbol { get; init; } = "";
        public string Timeframe { get; init; } = "";
        public double Open { get; init; }
        public double High { get; init; }
        public double Low { get; init; }
        public double Close { get; init; }
        public double? BidOpen { get; init; }
        public double? BidHigh { get; init; }
        public double? BidLow { get; init; }
        public double? BidClose { get; init; }
        public double? AskOpen { get; init; }
        public double? AskHigh { get; init; }
        public double? AskLow { get; init; }
        public double? AskClose { get; init; }
        public double? SpreadPips { get; init; }
        public double? Volume { get; init; }
    }

    public sealed class BacktestMarketDataLoadException : InvalidOperationException
    {
        public BacktestMarketDataLoadException(string message) : base(message)
        {
        }
    }

    public interface IBacktestMarketDataLoader<T>
    {
        Task<IReadOnlyList<T>> LoadAsync(
            string filePath,
            string? symbolFilter = null,
            CancellationToken cancellationToken = default);
    }
}
