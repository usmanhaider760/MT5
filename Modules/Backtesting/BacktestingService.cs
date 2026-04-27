using MT5TradingBot.Core;
using MT5TradingBot.Models;

namespace MT5TradingBot.Modules.Backtesting
{
    public sealed class BacktestingService : IBacktestingService
    {
        public Task<BacktestResult> RunAsync(
            IEnumerable<BacktestTrade> trades,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var tradeList = trades.ToList();
            var notes = new List<string>();

            if (tradeList.Count == 0)
            {
                notes.Add("No historical trades supplied.");
                return Task.FromResult(new BacktestResult { Notes = notes });
            }

            int wins = 0;
            int losses = 0;
            double netPips = 0;

            foreach (var trade in tradeList)
            {
                cancellationToken.ThrowIfCancellationRequested();

                double pips = CalculatePips(trade);
                netPips += pips;

                if (pips > 0) wins++;
                else if (pips < 0) losses++;
                else notes.Add($"Flat result for signal {trade.Signal.Id}.");
            }

            return Task.FromResult(new BacktestResult
            {
                TotalTrades = tradeList.Count,
                WinningTrades = wins,
                LosingTrades = losses,
                NetProfitPips = Math.Round(netPips, 1),
                WinRatePercent = Math.Round((double)wins / tradeList.Count * 100.0, 2),
                Notes = notes
            });
        }

        private static double CalculatePips(BacktestTrade trade)
        {
            if (string.IsNullOrWhiteSpace(trade.Signal.Pair))
                return 0;

            double pipSize = LotCalculator.GetPipSize(trade.Signal.Pair.ToUpperInvariant());
            double move = trade.Signal.Direction == SignalDirection.Sell
                ? trade.EntryPrice - trade.ExitPrice
                : trade.ExitPrice - trade.EntryPrice;

            return pipSize > 0 ? move / pipSize : 0;
        }
    }
}
