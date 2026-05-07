using MT5TradingBot.Data;
using MT5TradingBot.Models;

namespace MT5TradingBot.Services
{
    public sealed class PerformanceSummary
    {
        public int TotalTrades { get; init; }
        public int WinCount { get; init; }
        public int LossCount { get; init; }
        public double WinRatePct { get; init; }
        public double NetProfitUsd { get; init; }
        public double MaxDrawdownPct { get; init; }
        public double SharpeRatio { get; init; }
        public IReadOnlyList<EquityPoint> EquityCurve { get; init; } = [];
    }

    public static class PerformanceCalculator
    {
        public static PerformanceSummary Calculate(IReadOnlyList<TradeRecord> records)
        {
            var closed = records
                .Where(r => r.ClosedAt.HasValue)
                .OrderBy(r => r.ClosedAt!.Value)
                .ToList();

            if (closed.Count == 0)
                return new PerformanceSummary();

            var profits = closed.Select(r => r.ProfitUsd).ToList();
            var curve = new List<EquityPoint>(closed.Count);
            double running = 0;
            double peak = 0;
            double maxDrawdownPct = 0;

            foreach (var trade in closed)
            {
                running += trade.ProfitUsd;
                curve.Add(new EquityPoint(
                    trade.ClosedAt!.Value,
                    Math.Round(running, 2),
                    trade.ProfitUsd >= 0));

                if (running > peak)
                    peak = running;

                if (peak > 0)
                {
                    double drawdownPct = (peak - running) / peak * 100.0;
                    if (drawdownPct > maxDrawdownPct)
                        maxDrawdownPct = drawdownPct;
                }
            }

            int wins = profits.Count(p => p >= 0);
            int losses = profits.Count - wins;

            return new PerformanceSummary
            {
                TotalTrades = closed.Count,
                WinCount = wins,
                LossCount = losses,
                WinRatePct = Math.Round(wins * 100.0 / closed.Count, 1),
                NetProfitUsd = Math.Round(running, 2),
                MaxDrawdownPct = Math.Round(maxDrawdownPct, 2),
                SharpeRatio = CalculateSharpe(profits),
                EquityCurve = curve
            };
        }

        private static double CalculateSharpe(IReadOnlyList<double> profits)
        {
            if (profits.Count < 2)
                return 0;

            double mean = profits.Average();
            double variance = profits.Sum(p => (p - mean) * (p - mean)) / profits.Count;
            double stdDev = Math.Sqrt(variance);

            return stdDev > 0
                ? Math.Round(mean / stdDev * Math.Sqrt(252), 2)
                : 0;
        }
    }
}
