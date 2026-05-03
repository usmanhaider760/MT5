using MT5TradingBot.Core;
using MT5TradingBot.Models;

namespace MT5TradingBot.Modules.Backtesting
{
    public sealed record BacktestExecutionCostInput
    {
        public string Symbol { get; init; } = "";
        public TradeType EntrySide { get; init; } = TradeType.BUY;
        public double LotSize { get; init; }
        public double EntryPrice { get; init; }
        public double ExitPrice { get; init; }
        public double? Bid { get; init; }
        public double? Ask { get; init; }
        public double? SpreadPips { get; init; }
        public BotConfig CommissionAndSlippageConfig { get; init; } = new();
    }

    public sealed record BacktestExecutionCostResult
    {
        public bool Success { get; init; }
        public double SpreadCostUsd { get; init; }
        public double CommissionCostUsd { get; init; }
        public double SlippageCostUsd { get; init; }
        public double TotalCostUsd { get; init; }
        public double? SpreadPips { get; init; }
        public IReadOnlyList<string> Warnings { get; init; } = [];
        public IReadOnlyList<string> MissingDataFlags { get; init; } = [];
    }

    public static class BacktestExecutionCostModel
    {
        public static BacktestExecutionCostResult Estimate(BacktestExecutionCostInput input)
        {
            var warnings = new List<string>();
            var missing = new List<string>();

            if (string.IsNullOrWhiteSpace(input.Symbol))
            {
                missing.Add("SYMBOL");
                warnings.Add("Symbol is required for execution cost simulation.");
            }

            if (!IsFinitePositive(input.LotSize))
            {
                missing.Add("LOT_SIZE");
                warnings.Add("Lot size must be positive for execution cost simulation.");
            }

            if (!IsFinitePositive(input.EntryPrice))
                warnings.Add("Entry price is unavailable or invalid; default pip-value assumptions may be used.");

            if (!IsFinitePositive(input.ExitPrice))
                warnings.Add("Exit price is unavailable or invalid; cost-only estimate can still be calculated.");

            double? spreadPips = ResolveSpreadPips(input, warnings, missing);
            double spreadCost = 0;

            if (spreadPips.HasValue &&
                !string.IsNullOrWhiteSpace(input.Symbol) &&
                IsFinitePositive(input.LotSize))
            {
                double pipValue = LotCalculator.GetPipValuePerLot(
                    input.Symbol.ToUpperInvariant(),
                    IsFinitePositive(input.EntryPrice) ? input.EntryPrice : 1.0);
                spreadCost = Math.Round(spreadPips.Value * pipValue * input.LotSize, 2);
            }

            var commission = CommissionCalculator.EstimateRoundTurn(
                input.LotSize,
                input.CommissionAndSlippageConfig);
            if (!commission.Success)
            {
                missing.Add("COMMISSION");
                warnings.Add(commission.Error);
            }

            var slippage = SlippageCalculator.EstimateCost(
                input.Symbol,
                input.LotSize,
                input.CommissionAndSlippageConfig);
            if (!slippage.Success)
            {
                missing.Add("SLIPPAGE");
                warnings.Add(slippage.Error);
            }

            double commissionCost = commission.Success ? commission.Amount : 0;
            double slippageCost = slippage.Success ? slippage.CostUsd : 0;
            double totalCost = Math.Round(spreadCost + commissionCost + slippageCost, 2);

            return new BacktestExecutionCostResult
            {
                Success = missing.Count == 0,
                SpreadCostUsd = spreadCost,
                CommissionCostUsd = commissionCost,
                SlippageCostUsd = slippageCost,
                TotalCostUsd = totalCost,
                SpreadPips = spreadPips,
                Warnings = warnings,
                MissingDataFlags = missing.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            };
        }

        public static BacktestExecutionCostResult EstimateFromTick(
            BacktestTick tick,
            TradeType entrySide,
            double lotSize,
            double entryPrice,
            double exitPrice,
            BotConfig config)
        {
            return Estimate(new BacktestExecutionCostInput
            {
                Symbol = tick.Symbol,
                EntrySide = entrySide,
                LotSize = lotSize,
                EntryPrice = entryPrice,
                ExitPrice = exitPrice,
                Bid = tick.Bid,
                Ask = tick.Ask,
                CommissionAndSlippageConfig = config
            });
        }

        public static BacktestExecutionCostResult EstimateFromOhlc(
            BacktestOhlcCandle candle,
            TradeType entrySide,
            double lotSize,
            double entryPrice,
            double exitPrice,
            BotConfig config,
            double? configuredSpreadPips = null)
        {
            return Estimate(new BacktestExecutionCostInput
            {
                Symbol = candle.Symbol,
                EntrySide = entrySide,
                LotSize = lotSize,
                EntryPrice = entryPrice,
                ExitPrice = exitPrice,
                Bid = candle.BidClose,
                Ask = candle.AskClose,
                SpreadPips = candle.SpreadPips ?? configuredSpreadPips,
                CommissionAndSlippageConfig = config
            });
        }

        private static double? ResolveSpreadPips(
            BacktestExecutionCostInput input,
            List<string> warnings,
            List<string> missing)
        {
            if (input.Bid.HasValue &&
                input.Ask.HasValue &&
                IsFinitePositive(input.Bid.Value) &&
                IsFinitePositive(input.Ask.Value))
            {
                double bid = input.Bid.Value;
                double ask = input.Ask.Value;
                if (bid > ask)
                {
                    missing.Add("SPREAD");
                    warnings.Add("Bid must be less than or equal to ask for spread cost simulation.");
                    return null;
                }

                double pipSize = LotCalculator.GetPipSize(input.Symbol.ToUpperInvariant());
                if (!IsFinitePositive(pipSize))
                {
                    missing.Add("SPREAD");
                    warnings.Add("Pip size is unavailable for spread cost simulation.");
                    return null;
                }

                return Math.Round((ask - bid) / pipSize, 4);
            }

            if (IsFiniteNonNegative(input.SpreadPips))
                return input.SpreadPips!.Value;

            missing.Add("SPREAD");
            warnings.Add("Spread data is unavailable for execution cost simulation.");
            return null;
        }

        private static bool IsFinitePositive(double? value) =>
            value.HasValue &&
            !double.IsNaN(value.Value) &&
            !double.IsInfinity(value.Value) &&
            value.Value > 0;

        private static bool IsFiniteNonNegative(double? value) =>
            value.HasValue &&
            !double.IsNaN(value.Value) &&
            !double.IsInfinity(value.Value) &&
            value.Value >= 0;
    }
}
