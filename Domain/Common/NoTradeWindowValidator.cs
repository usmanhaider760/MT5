using System.Globalization;
using MT5TradingBot.Models;

namespace MT5TradingBot.Core
{
    public readonly record struct NoTradeWindowValidation(
        bool Success,
        bool Blocked,
        bool InvalidConfig,
        string Message,
        string WindowName);

    public static class NoTradeWindowValidator
    {
        private static readonly string[] TimeFormats =
        [
            @"h\:mm",
            @"hh\:mm",
            @"h\:mm\:ss",
            @"hh\:mm\:ss"
        ];

        public static NoTradeWindowValidation Validate(BotConfig config, DateTime utcNow)
        {
            if (!config.EnableRolloverNoTradeWindow)
                return Success();

            var windows = new List<(string Name, string Start, string End)>();
            windows.Add(("rollover", config.RolloverWindowStartUtc, config.RolloverWindowEndUtc));

            foreach (var window in config.AdditionalNoTradeWindows)
            {
                string name = string.IsNullOrWhiteSpace(window.Name)
                    ? "no-trade"
                    : window.Name.Trim();
                windows.Add((name, window.StartUtc, window.EndUtc));
            }

            TimeSpan currentTime = utcNow.ToUniversalTime().TimeOfDay;
            foreach (var window in windows)
            {
                if (!TryParseUtcTime(window.Start, out TimeSpan start) ||
                    !TryParseUtcTime(window.End, out TimeSpan end) ||
                    start == end)
                {
                    return Invalid(
                        $"No-trade window '{window.Name}' has invalid UTC start/end time.",
                        window.Name);
                }

                if (Contains(currentTime, start, end))
                {
                    return Blocked(
                        $"Current UTC time {currentTime:hh\\:mm} is inside no-trade window '{window.Name}' " +
                        $"{start:hh\\:mm}-{end:hh\\:mm} UTC.",
                        window.Name);
                }
            }

            return Success();
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

        private static NoTradeWindowValidation Success() =>
            new(true, false, false, "", "");

        private static NoTradeWindowValidation Blocked(string message, string windowName) =>
            new(false, true, false, message, windowName);

        private static NoTradeWindowValidation Invalid(string message, string windowName) =>
            new(false, false, true, message, windowName);
    }
}
