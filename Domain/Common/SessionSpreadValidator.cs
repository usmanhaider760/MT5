using System.Globalization;
using MT5TradingBot.Models;

namespace MT5TradingBot.Core
{
    public readonly record struct SessionSpreadValidation(
        bool Success,
        bool Blocked,
        bool InvalidConfig,
        string Message,
        string RuleName,
        double SpreadPips,
        double LimitPips);

    public static class SessionSpreadValidator
    {
        private static readonly string[] TimeFormats =
        [
            @"h\:mm",
            @"hh\:mm",
            @"h\:mm\:ss",
            @"hh\:mm\:ss"
        ];

        public static SessionSpreadValidation Validate(
            BotConfig config,
            SymbolInfo symbolInfo,
            string symbol,
            DateTime utcNow)
        {
            if (!config.EnableSessionSpreadProtection)
                return Success();

            if (!IsFinitePositive(config.DefaultMaxSpreadPips))
                return Invalid("Session spread protection is enabled but default max spread is not positive.", "");

            TimeSpan currentTime = utcNow.ToUniversalTime().TimeOfDay;
            var matchingRules = new List<(string Name, double Limit)>();

            foreach (var rule in config.SessionSpreadRules)
            {
                var result = EvaluateRule(rule, currentTime);
                if (result.Invalid)
                    return Invalid(result.Message, RuleName(rule));
                if (result.Matches)
                    matchingRules.Add((RuleName(rule), rule.MaxSpreadPips));
            }

            string normalizedSymbol = PipCalculator.NormalizeSymbol(symbol);
            foreach (var entry in config.SymbolSpecificSpreadRules)
            {
                if (!string.Equals(
                        PipCalculator.NormalizeSymbol(entry.Key),
                        normalizedSymbol,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (var rule in entry.Value)
                {
                    var result = EvaluateRule(rule, currentTime);
                    if (result.Invalid)
                        return Invalid(result.Message, RuleName(rule));
                    if (result.Matches)
                        matchingRules.Add((RuleName(rule), rule.MaxSpreadPips));
                }
            }

            string ruleName = "default";
            double limit = config.DefaultMaxSpreadPips;
            if (matchingRules.Count > 0)
            {
                var active = matchingRules.OrderBy(rule => rule.Limit).First();
                ruleName = active.Name;
                limit = active.Limit;
            }

            double spread = symbolInfo.SpreadPips;
            if (spread > limit)
            {
                return new SessionSpreadValidation(
                    false,
                    true,
                    false,
                    $"{symbol} spread {spread:F1} pips exceeds {ruleName} session limit {limit:F1} pips.",
                    ruleName,
                    spread,
                    limit);
            }

            return new SessionSpreadValidation(true, false, false, "", ruleName, spread, limit);
        }

        private static (bool Invalid, bool Matches, string Message) EvaluateRule(
            SessionSpreadRuleConfig rule,
            TimeSpan currentTime)
        {
            if (!IsFinitePositive(rule.MaxSpreadPips))
                return (true, false, $"Session spread rule '{RuleName(rule)}' has no positive max spread.");

            if (!TryParseUtcTime(rule.StartUtc, out TimeSpan start) ||
                !TryParseUtcTime(rule.EndUtc, out TimeSpan end) ||
                start == end)
            {
                return (true, false, $"Session spread rule '{RuleName(rule)}' has invalid UTC start/end time.");
            }

            return (false, Contains(currentTime, start, end), "");
        }

        private static bool Contains(TimeSpan current, TimeSpan start, TimeSpan end)
        {
            if (start < end)
                return current >= start && current < end;

            return current >= start || current < end;
        }

        private static bool TryParseUtcTime(string value, out TimeSpan time)
        {
            if (TimeSpan.TryParseExact(
                    value?.Trim(),
                    TimeFormats,
                    CultureInfo.InvariantCulture,
                    out time))
            {
                return time >= TimeSpan.Zero && time < TimeSpan.FromDays(1);
            }

            time = default;
            return false;
        }

        private static string RuleName(SessionSpreadRuleConfig rule) =>
            string.IsNullOrWhiteSpace(rule.Name) ? "session" : rule.Name.Trim();

        private static SessionSpreadValidation Success() =>
            new(true, false, false, "", "", 0, 0);

        private static SessionSpreadValidation Invalid(string message, string ruleName) =>
            new(false, false, true, message, ruleName, 0, 0);

        private static bool IsFinitePositive(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;
    }
}
