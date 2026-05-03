using System.Globalization;
using System.Text.RegularExpressions;
using MT5TradingBot.Models;

namespace MT5TradingBot.Modules.TradeExecution
{
    public enum OrderRetryDecision
    {
        DoNotRetry,
        RetryTransient
    }

    public sealed record OrderFailureClassification(
        string Code,
        OrderRetryDecision RetryDecision,
        long? BrokerRetcode = null,
        string BrokerComment = "");

    public static class OrderFailureClassifier
    {
        private static readonly Regex Mt5RetcodeRegex = new(@"MT5_(\d+)", RegexOptions.Compiled);

        private static readonly HashSet<string> SafetyAndValidationCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            "VALIDATION",
            "REJECTED_CONFIG",
            "SIGNAL_EXPIRED",
            "DAILY_LIMIT",
            "MAX_CONCURRENT_POSITIONS",
            "EDGE_PAUSED",
            "KILL_SWITCH_ACTIVE",
            "NO_ACCOUNT",
            "NO_SYMBOL_DATA",
            "NO_MARGIN_DATA",
            "NEWS_UNAVAILABLE",
            "NEWS_BLACKOUT",
            "RISK_DATA_UNAVAILABLE",
            "RISK_BLOCKED",
            "USER_APPROVAL_REQUIRED",
            "CORRELATION_BLOCK",
            "DAILY_LOSS_DATA_UNAVAILABLE",
            "DAILY_LOSS_LIMIT",
            "WEEKLY_LOSS_DATA_UNAVAILABLE",
            "WEEKLY_LOSS_LIMIT",
            "SYMBOL_EXPOSURE_DATA_UNAVAILABLE",
            "SYMBOL_EXPOSURE_LIMIT",
            "MARGIN_DATA_UNAVAILABLE",
            "MARGIN_LEVEL_LIMIT",
            "COMMISSION_DATA_UNAVAILABLE",
            "COMMISSION_COST_LIMIT",
            "SLIPPAGE_DATA_UNAVAILABLE",
            "SLIPPAGE_LIMIT",
            "BROKER_STOP_LEVEL",
            "BROKER_STOP_LEVEL_UNAVAILABLE",
            "BROKER_FREEZE_LEVEL",
            "BROKER_FREEZE_LEVEL_UNAVAILABLE",
            "BROKER_LOT_LIMIT",
            "BROKER_LOT_STEP",
            "BROKER_LOT_DATA_UNAVAILABLE",
            "ROLLOVER_WINDOW",
            "NO_TRADE_WINDOW",
            "SESSION_DATA_UNAVAILABLE",
            "SESSION_SPREAD_LIMIT",
            "SPREAD_WIDENING_LIMIT",
            "SESSION_SPREAD_DATA_UNAVAILABLE",
            "BROKER_ORDERCHECK_UNAVAILABLE",
            "BROKER_ORDERCHECK_REJECTED",
            "ORDERCHECK_FAILED",
            "ORDERCHECK_UNAVAILABLE",
            "ORDERCHECK_REJECTED"
        };

        public static bool IsRetryable(TradeResult result) =>
            Classify(result).RetryDecision == OrderRetryDecision.RetryTransient;

        public static OrderFailureClassification Classify(TradeResult result)
        {
            if (result.IsSuccess)
                return new("", OrderRetryDecision.DoNotRetry);

            string code = result.ErrorCode ?? "";
            string message = result.ErrorMessage ?? "";

            if (SafetyAndValidationCodes.Contains(code))
                return new(code, OrderRetryDecision.DoNotRetry, result.BrokerRetcode, result.BrokerComment);

            if (TryExtractMt5Retcode(code, message, out long retcode))
                return ClassifyRetcode(retcode, message);

            string combined = (code + " " + message).ToLowerInvariant();

            if (ContainsAny(combined, "timeout", "timed out", "no response"))
                return new("ORDER_TIMEOUT", OrderRetryDecision.RetryTransient, result.BrokerRetcode, message);

            if (ContainsAny(combined, "connection", "socket", "pipe", "transport", "temporar"))
                return new("ORDER_UNKNOWN_FAILURE", OrderRetryDecision.RetryTransient, result.BrokerRetcode, message);

            if (ContainsAny(combined, "requote", "price changed", "off quotes"))
                return new("ORDER_REQUOTE", OrderRetryDecision.RetryTransient, result.BrokerRetcode, message);

            if (ContainsAny(combined, "market closed", "market is closed"))
                return new("ORDER_MARKET_CLOSED", OrderRetryDecision.DoNotRetry, result.BrokerRetcode, message);

            if (ContainsAny(combined, "invalid price"))
                return new("ORDER_INVALID_PRICE", OrderRetryDecision.DoNotRetry, result.BrokerRetcode, message);

            if (ContainsAny(combined, "invalid stops", "invalid stop"))
                return new("ORDER_INVALID_STOPS", OrderRetryDecision.DoNotRetry, result.BrokerRetcode, message);

            if (ContainsAny(combined, "no money", "not enough money", "insufficient"))
                return new("ORDER_NO_MONEY", OrderRetryDecision.DoNotRetry, result.BrokerRetcode, message);

            if (ContainsAny(combined, "trade disabled", "trading disabled"))
                return new("ORDER_TRADE_DISABLED", OrderRetryDecision.DoNotRetry, result.BrokerRetcode, message);

            return new("ORDER_UNKNOWN_FAILURE", OrderRetryDecision.DoNotRetry, result.BrokerRetcode, message);
        }

        public static TradeResult ApplyClassification(TradeResult result)
        {
            if (result.IsSuccess) return result;

            var classification = Classify(result);
            result.OrderFailureCode = classification.Code;
            result.BrokerRetcode = classification.BrokerRetcode ?? result.BrokerRetcode;
            result.BrokerComment = string.IsNullOrWhiteSpace(classification.BrokerComment)
                ? result.BrokerComment
                : classification.BrokerComment;

            if (string.IsNullOrWhiteSpace(result.ErrorCode) ||
                result.ErrorCode.StartsWith("MT5_", StringComparison.OrdinalIgnoreCase) ||
                result.ErrorCode is "MT5_REJECTED" or "MT5_NO_RESPONSE" or "EXCEPTION" or "EXECUTION_EXCEPTION")
            {
                result.ErrorCode = classification.Code;
            }

            return result;
        }

        private static OrderFailureClassification ClassifyRetcode(long retcode, string comment)
        {
            return retcode switch
            {
                10004 => new("ORDER_REQUOTE", OrderRetryDecision.RetryTransient, retcode, comment),
                10006 => new("ORDER_REJECTED", OrderRetryDecision.DoNotRetry, retcode, comment),
                10014 => new("ORDER_REJECTED", OrderRetryDecision.DoNotRetry, retcode, comment),
                10015 => new("ORDER_INVALID_PRICE", OrderRetryDecision.DoNotRetry, retcode, comment),
                10016 => new("ORDER_INVALID_STOPS", OrderRetryDecision.DoNotRetry, retcode, comment),
                10017 => new("ORDER_TRADE_DISABLED", OrderRetryDecision.DoNotRetry, retcode, comment),
                10018 => new("ORDER_MARKET_CLOSED", OrderRetryDecision.DoNotRetry, retcode, comment),
                10019 => new("ORDER_NO_MONEY", OrderRetryDecision.DoNotRetry, retcode, comment),
                10020 => new("ORDER_INVALID_PRICE", OrderRetryDecision.DoNotRetry, retcode, comment),
                10021 => new("ORDER_INVALID_PRICE", OrderRetryDecision.DoNotRetry, retcode, comment),
                10024 => new("ORDER_REJECTED", OrderRetryDecision.RetryTransient, retcode, comment),
                10031 => new("ORDER_TIMEOUT", OrderRetryDecision.RetryTransient, retcode, comment),
                _ => new("ORDER_UNKNOWN_FAILURE", OrderRetryDecision.DoNotRetry, retcode, comment)
            };
        }

        private static bool TryExtractMt5Retcode(string code, string message, out long retcode)
        {
            retcode = 0;
            var match = Mt5RetcodeRegex.Match(code + " " + message);
            return match.Success &&
                   long.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out retcode);
        }

        private static bool ContainsAny(string value, params string[] needles) =>
            needles.Any(value.Contains);
    }
}
