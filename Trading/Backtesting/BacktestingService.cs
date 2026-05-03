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
            return RunAsync(trades, new BotConfig(), cancellationToken);
        }

        public Task<BacktestResult> RunAsync(
            IEnumerable<BacktestTrade> trades,
            BotConfig config,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var list  = trades.OrderBy(t => t.OpenedAt).ToList();
            var notes = new List<string>();

            if (list.Count == 0)
            {
                notes.Add("No trades supplied.");
                return Task.FromResult(new BacktestResult { Notes = notes });
            }

            var usdPnls   = new List<double>(list.Count);
            var pipPnls   = new List<double>(list.Count);
            var curve     = new List<EquityPoint>(list.Count + 1);

            double cumUsd  = 0;
            double cumPips = 0;
            double totalCommission = 0;
            double totalSlippage = 0;
            double peak    = 0;
            double maxDd   = 0;

            // Origin point
            curve.Add(new EquityPoint(list[0].OpenedAt.AddMinutes(-1), 0, true));

            foreach (var t in list)
            {
                cancellationToken.ThrowIfCancellationRequested();

                double pips    = CalculatePips(t);
                double pipVal  = LotCalculator.GetPipValuePerLot(t.Signal.Pair.ToUpperInvariant());
                double commission = 0;
                var commissionEstimate = CommissionCalculator.EstimateRoundTurn(t.Lots, config);
                if (CommissionCalculator.IsEnabled(config))
                {
                    if (commissionEstimate.Success)
                    {
                        commission = commissionEstimate.Amount;
                    }
                    else
                    {
                        notes.Add($"Commission unavailable for signal {t.Signal.Id}: {commissionEstimate.Error}");
                    }
                }

                double slippageCost = 0;
                var slippageEstimate = SlippageCalculator.EstimateCost(t.Signal.Pair, t.Lots, config);
                if (SlippageCalculator.IsEnabled(config))
                {
                    if (slippageEstimate.Success)
                    {
                        slippageCost = slippageEstimate.CostUsd;
                    }
                    else
                    {
                        notes.Add($"Slippage unavailable for signal {t.Signal.Id}: {slippageEstimate.Error}");
                    }
                }

                double usdPnl  = pips * pipVal * t.Lots - commission - slippageCost;

                cumPips += pips;
                cumUsd  += usdPnl;
                totalCommission += commission;
                totalSlippage += slippageCost;
                usdPnls.Add(usdPnl);
                pipPnls.Add(pips);

                // Max drawdown (on USD equity)
                if (cumUsd > peak) peak = cumUsd;
                double dd = peak > 0 ? (peak - cumUsd) / peak * 100 : 0;
                if (dd > maxDd) maxDd = dd;

                curve.Add(new EquityPoint(
                    t.ClosedAt == default ? t.OpenedAt : t.ClosedAt,
                    cumUsd,
                    usdPnl >= 0));

                if (pips == 0)
                    notes.Add($"Flat result for signal {t.Signal.Id}.");
            }

            int wins   = usdPnls.Count(p => p >= 0);
            int losses = usdPnls.Count(p => p <  0);

            double grossWin  = usdPnls.Where(p => p > 0).Sum();
            double grossLoss = Math.Abs(usdPnls.Where(p => p < 0).Sum());

            double pf  = grossLoss > 0 ? grossWin / grossLoss
                       : grossWin > 0  ? 99 : 1;

            double avgWin  = wins   > 0 ? grossWin  / wins   : 0;
            double avgLoss = losses > 0 ? grossLoss / losses : 0;

            // Simplified per-trade Sharpe (annualised assuming 252 trading days)
            double sharpe = 0;
            if (usdPnls.Count > 1)
            {
                double mean = usdPnls.Average();
                double variance = usdPnls.Sum(r => (r - mean) * (r - mean)) / usdPnls.Count;
                double std = Math.Sqrt(variance);
                if (std > 0) sharpe = Math.Round(mean / std * Math.Sqrt(252), 2);
            }

            return Task.FromResult(new BacktestResult
            {
                TotalTrades    = list.Count,
                WinningTrades  = wins,
                LosingTrades   = losses,
                NetProfitPips  = Math.Round(cumPips, 1),
                NetProfitUsd   = Math.Round(cumUsd, 2),
                TotalCommissionUsd = Math.Round(totalCommission, 2),
                TotalSlippageUsd = Math.Round(totalSlippage, 2),
                WinRatePercent = Math.Round((double)wins / list.Count * 100, 1),
                MaxDrawdownPct = Math.Round(maxDd, 2),
                SharpeRatio    = sharpe,
                ProfitFactor   = Math.Round(pf, 2),
                AvgWinUsd      = Math.Round(avgWin, 2),
                AvgLossUsd     = Math.Round(avgLoss, 2),
                EquityCurve    = curve,
                Notes          = notes
            });
        }

        private static double CalculatePips(BacktestTrade trade)
        {
            if (string.IsNullOrWhiteSpace(trade.Signal.Pair)) return 0;
            double pipSize = LotCalculator.GetPipSize(trade.Signal.Pair.ToUpperInvariant());
            var direction = trade.Signal.Direction == SignalDirection.Sell
                ? TradeType.SELL
                : TradeType.BUY;
            return PipCalculator.MoveInPips(direction, trade.EntryPrice, trade.ExitPrice, pipSize);
        }
    }
}
