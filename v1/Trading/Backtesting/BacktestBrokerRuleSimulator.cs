using MT5TradingBot.Core;
using MT5TradingBot.Models;

namespace MT5TradingBot.Modules.Backtesting
{
    public sealed record BacktestBrokerMarginInput
    {
        public double AccountEquity { get; init; }
        public double CurrentUsedMargin { get; init; }
        public double EstimatedRequiredMargin { get; init; }
        public double MinProjectedMarginLevelPercent { get; init; }
    }

    public sealed record BacktestBrokerRuleInput
    {
        public string Symbol { get; init; } = "";
        public TradeType TradeType { get; init; } = TradeType.BUY;
        public OrderType OrderType { get; init; } = OrderType.MARKET;
        public double EntryPrice { get; init; }
        public double StopLoss { get; init; }
        public double TakeProfit { get; init; }
        public double LotSize { get; init; }
        public SymbolInfo? SymbolInfo { get; init; }
        public double ExistingSymbolLots { get; init; }
        public BacktestBrokerMarginInput? Margin { get; init; }
        public OrderCheckResult? SimulatedOrderCheck { get; init; }
    }

    public sealed record BacktestBrokerRuleResult
    {
        public bool Approved { get; init; }
        public string RejectionCode { get; init; } = "";
        public string RejectionReason { get; init; } = "";
        public IReadOnlyList<string> Warnings { get; init; } = [];
        public double ValidatedLotSize { get; init; }
        public double? EstimatedRequiredMargin { get; init; }
        public double? ProjectedMarginLevelPercent { get; init; }
    }

    public static class BacktestBrokerRuleSimulator
    {
        public static BacktestBrokerRuleResult Validate(BacktestBrokerRuleInput input)
        {
            var warnings = new List<string>();

            if (string.IsNullOrWhiteSpace(input.Symbol))
                return Reject("BACKTEST_BROKER_RULE_DATA_UNAVAILABLE", "Symbol is required for broker-rule simulation.", input);

            if (input.SymbolInfo == null)
                return Reject("BACKTEST_BROKER_RULE_DATA_UNAVAILABLE", "Symbol metadata is required for broker-rule simulation.", input);

            double referencePrice = ResolveReferencePrice(input);
            if (!IsFinitePositive(referencePrice))
                return Reject("BACKTEST_BROKER_PRICE_UNAVAILABLE", "Current bid/ask or entry price is required for broker-rule simulation.", input);

            var request = ToTradeRequest(input);

            var stopLevel = BrokerStopLevelValidator.Validate(request, input.SymbolInfo, referencePrice);
            if (!stopLevel.Success)
            {
                return Reject(
                    stopLevel.DataUnavailable ? "BACKTEST_BROKER_STOP_LEVEL_DATA_UNAVAILABLE" : "BACKTEST_BROKER_STOP_LEVEL",
                    stopLevel.Message,
                    input);
            }

            var freezeLevel = BrokerFreezeLevelValidator.Validate(request, input.SymbolInfo, referencePrice);
            if (!freezeLevel.Success)
            {
                return Reject(
                    freezeLevel.DataUnavailable ? "BACKTEST_BROKER_FREEZE_LEVEL_DATA_UNAVAILABLE" : "BACKTEST_BROKER_FREEZE_LEVEL",
                    freezeLevel.Message,
                    input);
            }

            var lot = BrokerLotSizeValidator.Validate(input.LotSize, input.SymbolInfo);
            if (!lot.Success)
            {
                return Reject(
                    lot.DataUnavailable ? "BACKTEST_BROKER_LOT_DATA_UNAVAILABLE" : ClassifyLotViolation(lot.Message),
                    lot.Message,
                    input);
            }

            if (IsFinitePositive(lot.VolumeLimit) &&
                IsFiniteNonNegative(input.ExistingSymbolLots) &&
                input.ExistingSymbolLots + input.LotSize - 1e-9 > lot.VolumeLimit)
            {
                return Reject(
                    "BACKTEST_BROKER_VOLUME_LIMIT",
                    $"Existing symbol lots {input.ExistingSymbolLots:F2} plus requested lot {input.LotSize:F2} exceed broker volume limit {lot.VolumeLimit:F2}.",
                    input);
            }

            double? requiredMargin = null;
            double? projectedMarginLevel = null;
            if (input.Margin != null)
            {
                var margin = input.Margin;
                requiredMargin = margin.EstimatedRequiredMargin;
                if (!IsFinitePositive(margin.AccountEquity) ||
                    !IsFiniteNonNegative(margin.CurrentUsedMargin) ||
                    !IsFinitePositive(margin.EstimatedRequiredMargin))
                {
                    return Reject("BACKTEST_MARGIN_DATA_UNAVAILABLE", "Account equity, used margin, and estimated required margin are required for margin simulation.", input);
                }

                double projectedUsedMargin = margin.CurrentUsedMargin + margin.EstimatedRequiredMargin;
                if (!IsFinitePositive(projectedUsedMargin))
                    return Reject("BACKTEST_MARGIN_DATA_UNAVAILABLE", "Projected used margin could not be calculated.", input);

                projectedMarginLevel = margin.AccountEquity / projectedUsedMargin * 100.0;
                if (!IsFinitePositive(projectedMarginLevel.Value))
                    return Reject("BACKTEST_MARGIN_DATA_UNAVAILABLE", "Projected margin level could not be calculated.", input);

                if (IsFinitePositive(margin.MinProjectedMarginLevelPercent) &&
                    projectedMarginLevel.Value + 1e-9 < margin.MinProjectedMarginLevelPercent)
                {
                    return Reject(
                        "BACKTEST_MARGIN_LEVEL_LIMIT",
                        $"Projected margin level {projectedMarginLevel.Value:F2}% is below minimum {margin.MinProjectedMarginLevelPercent:F2}%.",
                        input,
                        requiredMargin,
                        projectedMarginLevel);
                }
            }
            else
            {
                warnings.Add("Margin simulation was not supplied; margin requirement is unverified.");
            }

            if (input.SimulatedOrderCheck is { IsAccepted: false } orderCheck)
            {
                string reason = string.IsNullOrWhiteSpace(orderCheck.Comment)
                    ? $"Simulated OrderCheck rejected trade with retcode {orderCheck.Retcode}."
                    : $"Simulated OrderCheck rejected trade with retcode {orderCheck.Retcode}: {orderCheck.Comment}";
                return Reject("BACKTEST_ORDERCHECK_REJECTED", reason, input, requiredMargin, projectedMarginLevel);
            }

            return new BacktestBrokerRuleResult
            {
                Approved = true,
                Warnings = warnings,
                ValidatedLotSize = input.LotSize,
                EstimatedRequiredMargin = requiredMargin,
                ProjectedMarginLevelPercent = projectedMarginLevel
            };
        }

        private static TradeRequest ToTradeRequest(BacktestBrokerRuleInput input) => new()
        {
            Pair = input.Symbol,
            TradeType = input.TradeType,
            OrderType = input.OrderType,
            EntryPrice = input.EntryPrice,
            StopLoss = input.StopLoss,
            TakeProfit = input.TakeProfit,
            LotSize = input.LotSize
        };

        private static double ResolveReferencePrice(BacktestBrokerRuleInput input)
        {
            if (input.OrderType != OrderType.MARKET && IsFinitePositive(input.EntryPrice))
                return input.EntryPrice;

            if (input.SymbolInfo == null)
                return 0;

            return input.TradeType == TradeType.BUY
                ? input.SymbolInfo.Ask
                : input.SymbolInfo.Bid;
        }

        private static string ClassifyLotViolation(string message)
        {
            if (message.Contains("below broker minimum", StringComparison.OrdinalIgnoreCase))
                return "BACKTEST_BROKER_LOT_MIN";
            if (message.Contains("above broker maximum", StringComparison.OrdinalIgnoreCase))
                return "BACKTEST_BROKER_LOT_MAX";
            if (message.Contains("volume limit", StringComparison.OrdinalIgnoreCase))
                return "BACKTEST_BROKER_VOLUME_LIMIT";
            if (message.Contains("lot step", StringComparison.OrdinalIgnoreCase))
                return "BACKTEST_BROKER_LOT_STEP";

            return "BACKTEST_BROKER_LOT_SIZE";
        }

        private static BacktestBrokerRuleResult Reject(
            string code,
            string reason,
            BacktestBrokerRuleInput input,
            double? estimatedRequiredMargin = null,
            double? projectedMarginLevel = null) => new()
        {
            Approved = false,
            RejectionCode = code,
            RejectionReason = reason,
            ValidatedLotSize = input.LotSize,
            EstimatedRequiredMargin = estimatedRequiredMargin,
            ProjectedMarginLevelPercent = projectedMarginLevel
        };

        private static bool IsFinitePositive(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;

        private static bool IsFiniteNonNegative(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0;
    }
}
