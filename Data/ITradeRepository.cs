using MT5TradingBot.Models;

namespace MT5TradingBot.Data
{
    public sealed class TradeRecord
    {
        public string   RequestId      { get; init; } = "";
        public DateTime CreatedAt      { get; init; }
        public DateTime ExecutedAt     { get; init; }
        public string   Pair           { get; init; } = "";
        public string   Direction      { get; init; } = "";   // "BUY" / "SELL"
        public string   OrderType      { get; init; } = "";
        public double   LotSize        { get; init; }
        public double   EntryPrice     { get; init; }
        public double   StopLoss       { get; init; }
        public double   TakeProfit     { get; init; }
        public string   Comment        { get; init; } = "";
        public int      MagicNumber    { get; init; }
        public long     Ticket         { get; init; }
        public string   Status         { get; init; } = "";
        public double   ExecutedPrice  { get; init; }
        public double   ExecutedLots   { get; init; }
        public string   ErrorCode      { get; init; } = "";
        public string   ErrorMessage   { get; init; } = "";
        public double   ProfitUsd      { get; init; }          // 0 until closed
        public DateTime? ClosedAt      { get; init; }          // null until closed
    }

    public interface ITradeRepository
    {
        // Persist one completed trade (fire-and-forget safe; never throws to caller).
        Task InsertAsync(
            TradeRequest req,
            TradeResult result,
            CancellationToken ct = default);

        // Most-recent N records, newest first.
        Task<IReadOnlyList<TradeRecord>> GetRecentAsync(
            int count = 200,
            CancellationToken ct = default);

        // Records whose ExecutedAt falls within [from, to] UTC, newest first.
        Task<IReadOnlyList<TradeRecord>> GetByDateRangeAsync(
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken ct = default);

        // Mark an open trade as closed with its final P&L.
        Task UpdateCloseAsync(
            long ticket,
            double profitUsd,
            DateTime closedAtUtc,
            CancellationToken ct = default);

        // Last N closed trades (ClosedAt IS NOT NULL), newest first.
        Task<IReadOnlyList<TradeRecord>> GetRecentClosedAsync(
            int count = 50,
            CancellationToken ct = default);
    }
}
