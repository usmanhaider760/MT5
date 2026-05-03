using MT5TradingBot.Models;

namespace MT5TradingBot.Modules.Backtesting
{
    public enum IntrabarExitType
    {
        None,
        StopLoss,
        TakeProfit
    }

    public sealed record IntrabarExitResult
    {
        public bool ExitTriggered { get; init; }
        public IntrabarExitType ExitType { get; init; } = IntrabarExitType.None;
        public double ExitPrice { get; init; }
        public DateTime? ExitTimestampUtc { get; init; }
        public bool IsAmbiguous { get; init; }
        public string Explanation { get; init; } = "";
    }

    public static class IntrabarExitSimulator
    {
        public static IntrabarExitResult SimulateTickExit(
            TradeType side,
            double stopLoss,
            double takeProfit,
            IEnumerable<BacktestTick> ticks)
        {
            if (!IsFinitePositive(stopLoss) || !IsFinitePositive(takeProfit))
                return Open("Stop loss and take profit must be positive for tick intrabar simulation.");

            foreach (var tick in ticks.OrderBy(t => t.TimestampUtc))
            {
                double exitPrice = side == TradeType.BUY ? tick.Bid : tick.Ask;
                if (!IsFinitePositive(exitPrice))
                    continue;

                var hit = EvaluatePrice(side, exitPrice, stopLoss, takeProfit);
                if (hit != IntrabarExitType.None)
                {
                    return new IntrabarExitResult
                    {
                        ExitTriggered = true,
                        ExitType = hit,
                        ExitPrice = hit == IntrabarExitType.StopLoss ? stopLoss : takeProfit,
                        ExitTimestampUtc = tick.TimestampUtc.Kind == DateTimeKind.Utc
                            ? tick.TimestampUtc
                            : DateTime.SpecifyKind(tick.TimestampUtc, DateTimeKind.Utc),
                        Explanation = $"Tick mode resolved first hit as {hit}."
                    };
                }
            }

            return Open("Tick mode found no SL/TP hit.");
        }

        public static IntrabarExitResult SimulateOhlcExit(
            TradeType side,
            double stopLoss,
            double takeProfit,
            BacktestOhlcCandle candle)
        {
            if (!IsFinitePositive(stopLoss) || !IsFinitePositive(takeProfit))
                return Open("Stop loss and take profit must be positive for OHLC intrabar simulation.");

            bool stopHit = IsStopHit(side, candle.Low, candle.High, stopLoss);
            bool takeProfitHit = IsTakeProfitHit(side, candle.Low, candle.High, takeProfit);
            DateTime timestamp = candle.TimestampUtc.Kind == DateTimeKind.Utc
                ? candle.TimestampUtc
                : DateTime.SpecifyKind(candle.TimestampUtc, DateTimeKind.Utc);

            if (stopHit && takeProfitHit)
            {
                return new IntrabarExitResult
                {
                    ExitTriggered = true,
                    ExitType = IntrabarExitType.StopLoss,
                    ExitPrice = stopLoss,
                    ExitTimestampUtc = timestamp,
                    IsAmbiguous = true,
                    Explanation = "OHLC candle hit both SL and TP; conservative SL-first handling applied."
                };
            }

            if (stopHit)
            {
                return new IntrabarExitResult
                {
                    ExitTriggered = true,
                    ExitType = IntrabarExitType.StopLoss,
                    ExitPrice = stopLoss,
                    ExitTimestampUtc = timestamp,
                    Explanation = "OHLC candle hit stop loss only."
                };
            }

            if (takeProfitHit)
            {
                return new IntrabarExitResult
                {
                    ExitTriggered = true,
                    ExitType = IntrabarExitType.TakeProfit,
                    ExitPrice = takeProfit,
                    ExitTimestampUtc = timestamp,
                    Explanation = "OHLC candle hit take profit only."
                };
            }

            return Open("OHLC candle found no SL/TP hit.");
        }

        private static IntrabarExitResult Open(string explanation) => new()
        {
            ExitTriggered = false,
            ExitType = IntrabarExitType.None,
            ExitPrice = 0,
            ExitTimestampUtc = null,
            IsAmbiguous = false,
            Explanation = explanation
        };

        private static IntrabarExitType EvaluatePrice(
            TradeType side,
            double price,
            double stopLoss,
            double takeProfit)
        {
            if (side == TradeType.BUY)
            {
                if (price <= stopLoss) return IntrabarExitType.StopLoss;
                if (price >= takeProfit) return IntrabarExitType.TakeProfit;
            }
            else
            {
                if (price >= stopLoss) return IntrabarExitType.StopLoss;
                if (price <= takeProfit) return IntrabarExitType.TakeProfit;
            }

            return IntrabarExitType.None;
        }

        private static bool IsStopHit(TradeType side, double low, double high, double stopLoss) =>
            side == TradeType.BUY
                ? low <= stopLoss
                : high >= stopLoss;

        private static bool IsTakeProfitHit(TradeType side, double low, double high, double takeProfit) =>
            side == TradeType.BUY
                ? high >= takeProfit
                : low <= takeProfit;

        private static bool IsFinitePositive(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;
    }
}
