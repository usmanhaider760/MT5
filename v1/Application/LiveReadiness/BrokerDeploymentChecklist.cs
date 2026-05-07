using System.Diagnostics;
using MT5TradingBot.Models;
using MT5TradingBot.Modules.BrokerIntegration;
using MT5TradingBot.Modules.NewsFilter;

namespace MT5TradingBot.Modules.LiveReadiness
{
    public sealed class BrokerDeploymentChecklist : IBrokerDeploymentChecklist
    {
        private readonly MT5Bridge _bridge;
        private readonly INewsCalendarService? _newsCalendar;
        private readonly ApiIntegrationConfig _apiConfig;

        public BrokerDeploymentChecklist(
            MT5Bridge bridge,
            INewsCalendarService? newsCalendar,
            ApiIntegrationConfig apiConfig)
        {
            _bridge = bridge;
            _newsCalendar = newsCalendar;
            _apiConfig = apiConfig;
        }

        public async Task<BrokerDeploymentChecklistResult> CheckAsync(
            TradeRequest request,
            BotConfig config,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var checkedItems = new List<BrokerDeploymentCheckItem>();
            var warnings = new List<string>();
            var timestamp = DateTime.UtcNow;
            double? latencyMs = null;
            string eaVersion = "";
            string eaBuild = "";

            var sw = Stopwatch.StartNew();
            bool pingOk = await _bridge.PingAsync().ConfigureAwait(false);
            sw.Stop();
            latencyMs = sw.Elapsed.TotalMilliseconds;
            Add(checkedItems, "MT5 bridge reachable", pingOk, BrokerDeploymentCodes.Mt5Disconnected,
                pingOk ? "MT5 bridge ping succeeded." : "MT5 bridge ping failed.");

            if (pingOk && config.BrokerDeployment.MaxLatencyMs > 0)
            {
                bool latencyOk = latencyMs.Value <= config.BrokerDeployment.MaxLatencyMs;
                Add(checkedItems, "VPS/server latency", latencyOk, BrokerDeploymentCodes.LatencyTooHigh,
                    latencyOk
                        ? $"Latency {latencyMs.Value:F0}ms is within {config.BrokerDeployment.MaxLatencyMs}ms."
                        : $"Latency {latencyMs.Value:F0}ms exceeds {config.BrokerDeployment.MaxLatencyMs}ms.");
            }
            else if (!pingOk && config.BrokerDeployment.MaxLatencyMs > 0)
            {
                Add(checkedItems, "VPS/server latency", false, BrokerDeploymentCodes.LatencyUnavailable,
                    "Latency could not be measured because MT5 ping failed.");
            }

            if (config.BrokerDeployment.RequireEaHealth)
            {
                var health = await _bridge.TryGetEaHealthAsync().ConfigureAwait(false);
                bool healthOk = health.Success && health.Health?.IsAlive == true;
                Add(checkedItems, "EA health", healthOk, BrokerDeploymentCodes.EaNotResponding,
                    healthOk ? "EA health command responded." : health.Error);
                if (healthOk && health.Health != null)
                {
                    eaVersion = health.Health.Version;
                    eaBuild = health.Health.BuildIdentifier;
                    if (string.IsNullOrWhiteSpace(eaVersion) || string.IsNullOrWhiteSpace(eaBuild))
                        warnings.Add("EA health responded without a complete version/build identifier.");
                }
            }

            var account = await TryAccountAsync().ConfigureAwait(false);
            Add(checkedItems, "Account data", IsUsableAccount(account), BrokerDeploymentCodes.AccountUnavailable,
                IsUsableAccount(account) ? "Account data is available." : "Account data is unavailable or incomplete.");

            var symbols = ResolveSymbols(request, config);
            foreach (string symbol in symbols)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var symbolInfo = await TrySymbolAsync(symbol).ConfigureAwait(false);
                EvaluateSymbol(symbol, symbolInfo, config.BrokerDeployment, checkedItems);

                double price = ResolvePrice(request, symbolInfo);
                double lots = config.BrokerDeployment.ProbeLotSize > 0
                    ? config.BrokerDeployment.ProbeLotSize
                    : Math.Max(0.01, request.LotSize);

                if (config.BrokerDeployment.RequireMarginEstimate)
                {
                    var margin = await _bridge.TryGetMarginEstimateAsync(
                        symbol,
                        request.TradeType,
                        lots,
                        price).ConfigureAwait(false);
                    bool marginOk = margin.Success &&
                        margin.Estimate != null &&
                        IsFinitePositive(margin.Estimate.RequiredMargin);
                    Add(checkedItems, $"Margin estimate {symbol}", marginOk, BrokerDeploymentCodes.MarginEstimateUnavailable,
                        marginOk ? "Margin estimate is available." : margin.Error);
                }

                if (config.BrokerDeployment.RequireOrderCheck)
                {
                    var orderRequest = CloneRequestForSymbol(request, symbol, lots);
                    var orderCheck = await _bridge.TryCheckOrderAsync(orderRequest, price).ConfigureAwait(false);
                    bool available = orderCheck.Success && orderCheck.Result != null;
                    Add(checkedItems, $"OrderCheck {symbol}", available, BrokerDeploymentCodes.OrderCheckUnavailable,
                        available ? "OrderCheck response is available." : orderCheck.Error);
                    if (available && orderCheck.Result?.IsAccepted != true)
                    {
                        Add(checkedItems, $"OrderCheck accepted {symbol}", false, BrokerDeploymentCodes.OrderCheckRejected,
                            orderCheck.Result?.Comment ?? "OrderCheck rejected the readiness probe.");
                    }
                }
            }

            await CheckNewsAsync(request.Pair, config, checkedItems).ConfigureAwait(false);

            var failed = checkedItems
                .Where(i => !i.Passed)
                .Select(i => i.Code)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new BrokerDeploymentChecklistResult
            {
                Passed = failed.Count == 0,
                Verdict = failed.Count == 0 ? BrokerDeploymentVerdicts.Pass : BrokerDeploymentVerdicts.Fail,
                FailedCriteria = failed,
                Warnings = warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                CheckedItems = checkedItems,
                TimestampUtc = timestamp,
                LatencyMs = latencyMs,
                EaVersion = eaVersion,
                EaBuildIdentifier = eaBuild
            };
        }

        private async Task CheckNewsAsync(
            string pair,
            BotConfig config,
            List<BrokerDeploymentCheckItem> checkedItems)
        {
            bool newsDisabled = string.Equals(_apiConfig.NewsProvider, "None", StringComparison.OrdinalIgnoreCase);
            if (newsDisabled || !config.BrokerDeployment.RequireNewsProviderWhenConfigured)
            {
                Add(checkedItems, "News provider", true, "", "News provider readiness is not required.");
                return;
            }

            if (_newsCalendar == null)
            {
                Add(checkedItems, "News provider", false, BrokerDeploymentCodes.NewsUnavailable,
                    "News calendar service is unavailable.");
                return;
            }

            try
            {
                var news = await _newsCalendar.GetRiskSnapshotAsync(pair, _apiConfig).ConfigureAwait(false);
                Add(checkedItems, "News provider", news.IsConfigured, BrokerDeploymentCodes.NewsUnavailable,
                    news.IsConfigured ? "News provider is configured." : news.Reason);
            }
            catch (Exception ex)
            {
                Add(checkedItems, "News provider", false, BrokerDeploymentCodes.NewsUnavailable,
                    $"News provider check failed: {ex.Message}");
            }
        }

        private async Task<AccountInfo?> TryAccountAsync()
        {
            try { return await _bridge.GetAccountInfoAsync().ConfigureAwait(false); }
            catch { return null; }
        }

        private async Task<SymbolInfo?> TrySymbolAsync(string symbol)
        {
            try { return await _bridge.GetSymbolInfoAsync(symbol).ConfigureAwait(false); }
            catch { return null; }
        }

        private static void EvaluateSymbol(
            string symbol,
            SymbolInfo? info,
            BrokerDeploymentReadinessConfig config,
            List<BrokerDeploymentCheckItem> checkedItems)
        {
            if (!config.RequireSymbolMetadata)
                return;

            bool metadata = info != null &&
                IsFinitePositive(info.Ask) &&
                IsFinitePositive(info.Bid) &&
                info.Ask >= info.Bid &&
                IsFinitePositive(info.Spread) &&
                info.Digits > 0 &&
                IsFinitePositive(info.EffectivePointSize);
            Add(checkedItems, $"Symbol metadata {symbol}", metadata, BrokerDeploymentCodes.SymbolMetadataUnavailable,
                metadata ? "Symbol price and point metadata are available." : "Symbol metadata is unavailable or incomplete.");

            Add(checkedItems, $"Stop level {symbol}", info?.StopLevelPoints.HasValue == true,
                BrokerDeploymentCodes.StopLevelUnavailable,
                info?.StopLevelPoints.HasValue == true ? "Stop level metadata is available." : "Stop level metadata is unavailable.");

            Add(checkedItems, $"Freeze level {symbol}", info?.FreezeLevelPoints.HasValue == true,
                BrokerDeploymentCodes.FreezeLevelUnavailable,
                info?.FreezeLevelPoints.HasValue == true ? "Freeze level metadata is available." : "Freeze level metadata is unavailable.");

            bool lots = info != null &&
                IsFinitePositive(info.MinLot) &&
                IsFinitePositive(info.MaxLot) &&
                info.MaxLot >= info.MinLot &&
                info.LotStep is > 0;
            Add(checkedItems, $"Lot metadata {symbol}", lots, BrokerDeploymentCodes.LotMetadataUnavailable,
                lots ? "Lot min/max/step metadata is available." : "Lot min/max/step metadata is unavailable.");
        }

        private static IReadOnlyList<string> ResolveSymbols(TradeRequest request, BotConfig config)
        {
            var symbols = config.AllowedPairs.Count > 0
                ? config.AllowedPairs
                : [request.Pair];

            return symbols
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => ApplySuffix(s.Trim().ToUpperInvariant(), config.SymbolSuffix))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string ApplySuffix(string symbol, string suffix) =>
            string.IsNullOrWhiteSpace(suffix) ||
            symbol.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                ? symbol
                : symbol + suffix;

        private static double ResolvePrice(TradeRequest request, SymbolInfo? symbolInfo)
        {
            if (symbolInfo != null)
                return request.TradeType == TradeType.BUY ? symbolInfo.Ask : symbolInfo.Bid;

            return request.EntryPrice > 0 ? request.EntryPrice : 1.0;
        }

        private static TradeRequest CloneRequestForSymbol(TradeRequest request, string symbol, double lots) => new()
        {
            Id = request.Id,
            Pair = symbol,
            TradeType = request.TradeType,
            OrderType = request.OrderType,
            EntryPrice = request.EntryPrice,
            StopLoss = request.StopLoss,
            TakeProfit = request.TakeProfit,
            TakeProfit2 = request.TakeProfit2,
            LotSize = lots,
            Comment = request.Comment,
            MagicNumber = request.MagicNumber,
            ExpiryMinutes = request.ExpiryMinutes,
            MoveSLToBreakevenAfterTP1 = request.MoveSLToBreakevenAfterTP1,
            CreatedAt = request.CreatedAt
        };

        private static bool IsUsableAccount(AccountInfo? account) =>
            account != null &&
            account.IsConnected &&
            IsFinitePositive(account.Equity) &&
            IsFiniteNonNegative(account.Balance) &&
            IsFiniteNonNegative(account.FreeMargin);

        private static bool IsFinitePositive(double? value) =>
            value.HasValue && IsFinitePositive(value.Value);

        private static bool IsFinitePositive(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;

        private static bool IsFiniteNonNegative(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0;

        private static void Add(
            List<BrokerDeploymentCheckItem> items,
            string name,
            bool passed,
            string code,
            string message)
        {
            items.Add(new BrokerDeploymentCheckItem
            {
                Name = name,
                Passed = passed,
                Code = passed ? "" : code,
                Message = message
            });
        }
    }
}
