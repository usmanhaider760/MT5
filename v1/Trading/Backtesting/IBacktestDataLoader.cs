using MT5TradingBot.Data;
using MT5TradingBot.Models;

namespace MT5TradingBot.Modules.Backtesting
{
    public interface IBacktestDataLoader
    {
        string DisplayName { get; }
        Task<IReadOnlyList<BacktestTrade>> LoadAsync(CancellationToken ct = default);
    }

    // Loads from the SQLite trade history populated by ITradeRepository.
    public sealed class DbBacktestLoader(ITradeRepository repo, int maxTrades = 500) : IBacktestDataLoader
    {
        public string DisplayName => "Trade History (SQLite DB)";

        public async Task<IReadOnlyList<BacktestTrade>> LoadAsync(CancellationToken ct = default)
        {
            var records = await repo.GetByDateRangeAsync(
                DateTime.UtcNow.AddYears(-10), DateTime.UtcNow, ct)
                .ConfigureAwait(false);

            return records
                .Where(r => r.ClosedAt.HasValue && r.ExecutedPrice > 0)
                .OrderBy(r => r.ExecutedAt)
                .Take(maxTrades)
                .Select(r =>
                {
                    // Back-calculate exit price from entry + P&L
                    double pipSize  = Core.LotCalculator.GetPipSize(r.Pair.ToUpperInvariant());
                    double pipValue = Core.LotCalculator.GetPipValuePerLot(r.Pair.ToUpperInvariant());
                    double lots     = r.ExecutedLots > 0 ? r.ExecutedLots : r.LotSize;
                    double pips     = (pipValue > 0 && lots > 0) ? r.ProfitUsd / (pipValue * lots) : 0;
                    bool isBuy      = r.Direction.Equals("BUY", StringComparison.OrdinalIgnoreCase);
                    double exitPx   = r.ExecutedPrice + pips * pipSize * (isBuy ? 1 : -1);

                    return new BacktestTrade
                    {
                        Signal = new MarketSignal
                        {
                            Pair      = r.Pair,
                            Direction = isBuy ? SignalDirection.Buy : SignalDirection.Sell,
                            EntryPrice = r.ExecutedPrice,
                            StopLoss   = r.StopLoss,
                            TakeProfit = r.TakeProfit
                        },
                        EntryPrice = r.ExecutedPrice,
                        ExitPrice  = exitPx,
                        Lots       = lots,
                        OpenedAt   = r.ExecutedAt,
                        ClosedAt   = r.ClosedAt!.Value
                    };
                })
                .ToList();
        }
    }

    // Loads from a user-supplied CSV.
    // Expected header (case-insensitive, order flexible):
    //   Date,Pair,Direction,Lots,Entry,Exit,ProfitUsd
    // or our trade_history.csv format:
    //   Time,Id,Pair,Direction,Lots,Entry,SL,TP,Ticket,Status,ExecutedPrice,Error
    // When ProfitUsd column absent, ExitPrice is used to derive P&L.
    public sealed class CsvBacktestLoader(string filePath) : IBacktestDataLoader
    {
        public string DisplayName => $"CSV: {Path.GetFileName(filePath)}";

        public Task<IReadOnlyList<BacktestTrade>> LoadAsync(CancellationToken ct = default)
        {
            var trades = new List<BacktestTrade>();

            string[] lines = File.ReadAllLines(filePath);
            if (lines.Length < 2)
                return Task.FromResult<IReadOnlyList<BacktestTrade>>(trades);

            // Parse header
            string[] headers = lines[0].Split(',')
                .Select(h => h.Trim().ToLowerInvariant()).ToArray();

            int iDate  = IndexOf(headers, "date", "time");
            int iPair  = IndexOf(headers, "pair", "symbol");
            int iDir   = IndexOf(headers, "direction", "type", "tradetype");
            int iLots  = IndexOf(headers, "lots", "volume", "lotsize");
            int iEntry = IndexOf(headers, "entry", "entryprice", "executedprice", "openprice");
            int iExit  = IndexOf(headers, "exit", "exitprice", "closeprice");

            if (iDate < 0 || iPair < 0 || iDir < 0 || iLots < 0 || iEntry < 0)
                return Task.FromResult<IReadOnlyList<BacktestTrade>>(trades);

            for (int i = 1; i < lines.Length; i++)
            {
                ct.ThrowIfCancellationRequested();
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                string[] cols = line.Split(',');
                if (cols.Length <= Math.Max(iEntry, Math.Max(iDir, iLots))) continue;

                try
                {
                    string pair = cols[iPair].Trim().ToUpperInvariant();
                    string dir  = cols[iDir].Trim().ToUpperInvariant();
                    bool isBuy  = dir is "BUY" or "B" or "LONG";

                    double lots  = double.Parse(cols[iLots].Trim());
                    double entry = double.Parse(cols[iEntry].Trim());
                    double exit  = iExit >= 0 && iExit < cols.Length
                        ? double.Parse(cols[iExit].Trim()) : entry;

                    DateTime openedAt = DateTime.TryParse(cols[iDate].Trim(), out var dt)
                        ? dt.ToUniversalTime() : DateTime.UtcNow;

                    trades.Add(new BacktestTrade
                    {
                        Signal = new MarketSignal
                        {
                            Pair      = pair,
                            Direction = isBuy ? SignalDirection.Buy : SignalDirection.Sell,
                            EntryPrice = entry
                        },
                        EntryPrice = entry,
                        ExitPrice  = exit,
                        Lots       = lots,
                        OpenedAt   = openedAt,
                        ClosedAt   = openedAt.AddHours(1)  // placeholder
                    });
                }
                catch { /* skip malformed rows */ }
            }

            return Task.FromResult<IReadOnlyList<BacktestTrade>>(trades);
        }

        private static int IndexOf(string[] headers, params string[] candidates)
        {
            foreach (var c in candidates)
            {
                int idx = Array.IndexOf(headers, c);
                if (idx >= 0) return idx;
            }
            return -1;
        }
    }
}
