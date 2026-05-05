using MT5TradingBot.Models;
using Newtonsoft.Json.Linq;
using Serilog;
using System.Globalization;
using System.Net.Http;

namespace MT5TradingBot.Modules.NewsFilter
{
    public sealed class FmpNewsCalendarService : INewsCalendarService
    {
        private static readonly HttpClient Http = new()
        {
            Timeout = TimeSpan.FromSeconds(12)
        };

        private readonly SemaphoreSlim _cacheLock = new(1, 1);
        private IReadOnlyList<NewsEvent> _cachedEvents = [];
        private DateTime _cacheUpdatedAtUtc = DateTime.MinValue;
        private string _cacheKey = "";

        public async Task<NewsRiskSnapshot> GetRiskSnapshotAsync(
            string pair,
            ApiIntegrationConfig config,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string provider = config.NewsProvider?.Trim() ?? "";
            if (string.Equals(provider, "None", StringComparison.OrdinalIgnoreCase))
                return Unavailable("News filtering is disabled in AI API Config.", "None", configured: true);

            bool isFmp = IsFmpProvider(provider);
            bool isTradingEconomics = IsTradingEconomicsProvider(provider);
            if (!isFmp && !isTradingEconomics)
                return Unavailable($"News provider '{provider}' is not wired yet. Select Financial Modeling Prep or Trading Economics.", provider, configured: false);

            if (isFmp && string.IsNullOrWhiteSpace(config.NewsApiKey))
                return Unavailable("Financial Modeling Prep API key is missing.", "Financial Modeling Prep", configured: false);

            var currencies = ExtractCurrencies(pair);
            if (currencies.Count == 0)
                return Unavailable($"Could not extract currencies from pair '{pair}'.", "Financial Modeling Prep", configured: true);

            try
            {
                var events = await GetCachedEventsAsync(config, cancellationToken).ConfigureAwait(false);
                DateTime now = DateTime.UtcNow;
                int before = Math.Max(0, config.NewsBlackoutBeforeMinutes);
                int after = Math.Max(0, config.NewsBlackoutAfterMinutes);

                var relevant = events
                    .Where(e => IsRelevantCurrency(e, currencies))
                    .Where(e => IsImpactIncluded(e.Impact, config.NewsImpactFilter))
                    .Where(e => e.EventTimeUtc >= now.AddHours(-6) && e.EventTimeUtc <= now.AddHours(24))
                    .OrderBy(e => e.EventTimeUtc)
                    .Take(12)
                    .ToList();

                var blocking = relevant
                    .Where(e => IsHighImpact(e.Impact))
                    .Where(e => e.EventTimeUtc >= now.AddMinutes(-after) && e.EventTimeUtc <= now.AddMinutes(before))
                    .ToList();

                bool highNext60 = relevant.Any(e => IsHighImpact(e.Impact) &&
                    e.EventTimeUtc >= now && e.EventTimeUtc <= now.AddMinutes(60));

                string riskLevel = blocking.Count > 0 || highNext60
                    ? "HIGH"
                    : relevant.Any(e => IsMediumImpact(e.Impact) && e.EventTimeUtc >= now && e.EventTimeUtc <= now.AddMinutes(120))
                        ? "MEDIUM"
                        : "LOW";

                string reason = blocking.Count > 0
                    ? $"{blocking.Count} high-impact event(s) inside blackout window."
                    : highNext60
                        ? "High-impact event within the next 60 minutes."
                        : relevant.Count == 0
                            ? "No configured-currency events found in the next 24 hours."
                            : "No high-impact event inside blackout window.";

                return new NewsRiskSnapshot
                {
                    RiskLevel = riskLevel,
                    HighImpactNext60Minutes = highNext60,
                    IsBlackoutActive = blocking.Count > 0,
                    IsConfigured = true,
                    Source = ProviderDisplayName(provider),
                    Reason = reason,
                    CheckedAtUtc = now,
                    CacheUpdatedAtUtc = _cacheUpdatedAtUtc,
                    RelevantEvents = relevant,
                    BlockingEvents = blocking
                };
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "News calendar check failed for {Pair} using {Provider}", pair, ProviderDisplayName(provider));
                return Unavailable($"News API call failed: {ex.Message}", ProviderDisplayName(provider), configured: true);
            }
        }

        private async Task<IReadOnlyList<NewsEvent>> GetCachedEventsAsync(
            ApiIntegrationConfig config,
            CancellationToken cancellationToken)
        {
            string currencies = string.Join(",", config.NewsCurrencies.OrderBy(c => c, StringComparer.OrdinalIgnoreCase));
            string key = $"{config.NewsProvider}|{currencies}|{config.NewsImpactFilter}|{DateTime.UtcNow:yyyyMMddHH}";
            bool isFresh = _cachedEvents.Count > 0 &&
                string.Equals(_cacheKey, key, StringComparison.Ordinal) &&
                DateTime.UtcNow - _cacheUpdatedAtUtc < TimeSpan.FromMinutes(15);

            if (isFresh)
                return _cachedEvents;

            await _cacheLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                isFresh = _cachedEvents.Count > 0 &&
                    string.Equals(_cacheKey, key, StringComparison.Ordinal) &&
                    DateTime.UtcNow - _cacheUpdatedAtUtc < TimeSpan.FromMinutes(15);
                if (isFresh)
                    return _cachedEvents;

                string url = BuildCalendarUrl(config);

                string json = await Http.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
                var array = JArray.Parse(json);
                bool isTradingEconomics = IsTradingEconomicsProvider(config.NewsProvider);
                _cachedEvents = [.. array
                    .Select(token => isTradingEconomics ? ParseTradingEconomicsEvent(token) : ParseFmpEvent(token))
                    .Where(e => e != null)
                    .Cast<NewsEvent>()];
                _cacheUpdatedAtUtc = DateTime.UtcNow;
                _cacheKey = key;
                return _cachedEvents;
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        private static NewsEvent? ParseFmpEvent(JToken token)
        {
            string title = ReadString(token, "event", "title", "name");
            string dateText = ReadString(token, "date", "datetime", "eventDate");
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(dateText))
                return null;

            if (!DateTime.TryParse(dateText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var eventTime))
                return null;

            string country = ReadString(token, "country");
            string currency = NormalizeCurrency(ReadString(token, "currency", "symbol"), country);
            string impact = NormalizeImpact(ReadString(token, "impact", "importance"));

            return new NewsEvent
            {
                Currency = currency,
                Country = country,
                Title = title,
                Impact = impact,
                EventTimeUtc = eventTime,
                Source = "Financial Modeling Prep",
                Previous = ReadString(token, "previous"),
                Forecast = ReadString(token, "forecast", "estimate", "consensus"),
                Actual = ReadString(token, "actual")
            };
        }

        private static NewsEvent? ParseTradingEconomicsEvent(JToken token)
        {
            string title = ReadString(token, "Event", "event", "Category", "category");
            string dateText = ReadString(token, "Date", "date");
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(dateText))
                return null;

            if (!DateTime.TryParse(dateText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var eventTime))
                return null;

            string country = ReadString(token, "Country", "country");
            string currency = NormalizeCurrency(ReadString(token, "Currency", "currency", "Symbol", "symbol"), country);
            string impact = NormalizeImpact(ReadString(token, "Importance", "importance", "Impact", "impact"));

            return new NewsEvent
            {
                Currency = currency,
                Country = country,
                Title = title,
                Impact = impact,
                EventTimeUtc = eventTime,
                Source = "Trading Economics",
                Previous = ReadString(token, "Previous", "previous"),
                Forecast = ReadString(token, "Forecast", "forecast", "TEForecast", "teForecast"),
                Actual = ReadString(token, "Actual", "actual")
            };
        }

        private static string ReadString(JToken token, params string[] names)
        {
            foreach (string name in names)
            {
                var value = token[name]?.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }

            return "";
        }

        private static bool IsFmpProvider(string provider) =>
            provider.Contains("Financial Modeling Prep", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(provider, "FMP", StringComparison.OrdinalIgnoreCase);

        private static bool IsTradingEconomicsProvider(string provider) =>
            provider.Contains("Trading Economics", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(provider, "TE", StringComparison.OrdinalIgnoreCase);

        private static string ProviderDisplayName(string provider) =>
            IsTradingEconomicsProvider(provider) ? "Trading Economics" : "Financial Modeling Prep";

        private static string BuildCalendarUrl(ApiIntegrationConfig config)
        {
            if (IsTradingEconomicsProvider(config.NewsProvider))
            {
                string credentials = string.IsNullOrWhiteSpace(config.NewsApiKey)
                    ? "guest:guest"
                    : config.NewsApiKey.Trim();
                string countries = BuildTradingEconomicsCountryPath(config.NewsCurrencies);
                string importance = TradingEconomicsImportanceQuery(config.NewsImpactFilter);
                return $"https://api.tradingeconomics.com/calendar/country/{countries}" +
                    $"?c={Uri.EscapeDataString(credentials)}{importance}";
            }

            var from = DateTime.UtcNow.Date.AddDays(-1);
            var to = DateTime.UtcNow.Date.AddDays(2);
            return "https://financialmodelingprep.com/stable/economic-calendar" +
                $"?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}&apikey={Uri.EscapeDataString(config.NewsApiKey)}";
        }

        private static string BuildTradingEconomicsCountryPath(IReadOnlyCollection<string> currencies)
        {
            var countries = currencies
                .SelectMany(CurrencyToTradingEconomicsCountries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .DefaultIfEmpty("united states");

            return string.Join(",", countries.Select(Uri.EscapeDataString));
        }

        private static IEnumerable<string> CurrencyToTradingEconomicsCountries(string currency)
        {
            switch (currency.Trim().ToUpperInvariant())
            {
                case "USD":
                case "XAU":
                    yield return "united states";
                    break;
                case "GBP":
                    yield return "united kingdom";
                    break;
                case "EUR":
                    yield return "euro area";
                    break;
                case "JPY":
                    yield return "japan";
                    break;
                case "CAD":
                    yield return "canada";
                    break;
                case "AUD":
                    yield return "australia";
                    break;
                case "CHF":
                    yield return "switzerland";
                    break;
                case "NZD":
                    yield return "new zealand";
                    break;
                case "CNY":
                    yield return "china";
                    break;
            }
        }

        private static string TradingEconomicsImportanceQuery(string impactFilter)
        {
            if (string.Equals(impactFilter, "High only", StringComparison.OrdinalIgnoreCase))
                return "&importance=3";
            if (string.Equals(impactFilter, "Medium + High", StringComparison.OrdinalIgnoreCase))
                return "&importance=2,3";
            return "";
        }

        private static string NormalizeImpact(string value)
        {
            string raw = value.Trim();
            if (string.IsNullOrWhiteSpace(raw)) return "Medium";
            if (raw.Contains("high", StringComparison.OrdinalIgnoreCase) || raw is "3" or "3.0") return "High";
            if (raw.Contains("medium", StringComparison.OrdinalIgnoreCase) || raw is "2" or "2.0") return "Medium";
            if (raw.Contains("low", StringComparison.OrdinalIgnoreCase) || raw is "1" or "1.0") return "Low";
            return raw;
        }

        private static string NormalizeCurrency(string currency, string country)
        {
            if (!string.IsNullOrWhiteSpace(currency))
                return currency.Trim().ToUpperInvariant();

            return country.Trim().ToUpperInvariant() switch
            {
                "UNITED STATES" or "US" or "USA" => "USD",
                "UNITED KINGDOM" or "UK" or "GREAT BRITAIN" => "GBP",
                "EURO AREA" or "EUROZONE" or "EUROPEAN UNION" => "EUR",
                "JAPAN" => "JPY",
                "CANADA" => "CAD",
                "AUSTRALIA" => "AUD",
                "SWITZERLAND" => "CHF",
                "NEW ZEALAND" => "NZD",
                "CHINA" => "CNY",
                _ => country.Trim().ToUpperInvariant()
            };
        }

        private static HashSet<string> ExtractCurrencies(string pair)
        {
            string compact = pair.Replace("/", "").Replace("_", "").Replace(".", "").Trim().ToUpperInvariant();
            if (compact.StartsWith("XAUUSD", StringComparison.OrdinalIgnoreCase))
                return new HashSet<string>(["XAU", "USD"], StringComparer.OrdinalIgnoreCase);
            if (compact.Length < 6)
                return [];

            return new HashSet<string>([compact[..3], compact.Substring(3, 3)], StringComparer.OrdinalIgnoreCase);
        }

        private static bool IsRelevantCurrency(NewsEvent newsEvent, HashSet<string> pairCurrencies)
        {
            string currency = newsEvent.Currency.Trim().ToUpperInvariant();
            if (pairCurrencies.Contains(currency))
                return true;

            return pairCurrencies.Contains("XAU") && currency == "USD";
        }

        private static bool IsImpactIncluded(string impact, string filter)
        {
            if (string.Equals(filter, "All", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(filter, "Medium + High", StringComparison.OrdinalIgnoreCase))
                return IsHighImpact(impact) || IsMediumImpact(impact);
            return IsHighImpact(impact);
        }

        private static bool IsHighImpact(string impact) =>
            string.Equals(impact, "High", StringComparison.OrdinalIgnoreCase);

        private static bool IsMediumImpact(string impact) =>
            string.Equals(impact, "Medium", StringComparison.OrdinalIgnoreCase);

        private static NewsRiskSnapshot Unavailable(string reason, string source, bool configured) => new()
        {
            RiskLevel = "UNAVAILABLE",
            HighImpactNext60Minutes = false,
            IsBlackoutActive = false,
            IsConfigured = configured,
            Source = source,
            Reason = reason,
            CheckedAtUtc = DateTime.UtcNow,
            CacheUpdatedAtUtc = DateTime.MinValue,
            RelevantEvents = [],
            BlockingEvents = []
        };
    }
}
