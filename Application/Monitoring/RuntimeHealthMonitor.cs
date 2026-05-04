using System.Diagnostics;
using MT5TradingBot.Models;
using MT5TradingBot.Modules.BrokerIntegration;
using MT5TradingBot.Modules.NewsFilter;

namespace MT5TradingBot.Modules.Monitoring
{
    public sealed class RuntimeHealthMonitor
    {
        private readonly MT5Bridge _bridge;
        private readonly INewsCalendarService? _newsCalendar;
        private readonly ApiIntegrationConfig _apiConfig;

        public RuntimeHealthMonitor(
            MT5Bridge bridge,
            INewsCalendarService? newsCalendar,
            ApiIntegrationConfig apiConfig)
        {
            _bridge = bridge;
            _newsCalendar = newsCalendar;
            _apiConfig = apiConfig;
        }

        public async Task<RuntimeHealthSnapshot> CaptureAsync(
            RuntimeHealthInput input,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var warnings = new List<string>();
            var critical = new List<string>();
            var timestamp = DateTime.UtcNow;
            var threshold = input.Config.RuntimeHealth;

            double? latencyMs = null;
            bool mt5Connected;
            var sw = Stopwatch.StartNew();
            try
            {
                mt5Connected = await _bridge.PingAsync().ConfigureAwait(false);
                sw.Stop();
                latencyMs = sw.Elapsed.TotalMilliseconds;
            }
            catch
            {
                sw.Stop();
                mt5Connected = false;
            }

            if (!mt5Connected)
            {
                critical.Add(RuntimeHealthCodes.Mt5Disconnected);
                warnings.Add(RuntimeHealthCodes.LatencyUnavailable);
            }
            else
            {
                EvaluateLatency(latencyMs, threshold, warnings, critical);
            }

            bool? eaHealthy = null;
            string eaVersion = "";
            string eaBuild = "";
            try
            {
                var health = await _bridge.TryGetEaHealthAsync().ConfigureAwait(false);
                eaHealthy = health.Success && health.Health?.IsAlive == true;
                if (eaHealthy != true)
                    critical.Add(RuntimeHealthCodes.EaHeartbeatFailed);
                else if (health.Health != null)
                {
                    eaVersion = health.Health.Version;
                    eaBuild = health.Health.BuildIdentifier;
                }
            }
            catch
            {
                eaHealthy = false;
                critical.Add(RuntimeHealthCodes.EaHeartbeatFailed);
            }

            AccountInfo? account = null;
            try
            {
                account = await _bridge.GetAccountInfoAsync().ConfigureAwait(false);
            }
            catch { }

            if (!IsUsableAccount(account))
                warnings.Add(RuntimeHealthCodes.MissingAccountData);

            SymbolInfo? symbol = null;
            try
            {
                symbol = await _bridge.GetSymbolInfoAsync(input.ProbeRequest.Pair).ConfigureAwait(false);
            }
            catch { }

            if (!IsUsableSymbol(symbol))
                warnings.Add(RuntimeHealthCodes.MissingSymbolData);

            int? openPositionCount = null;
            try
            {
                var positions = await _bridge.TryGetPositionsAsync().ConfigureAwait(false);
                if (positions.Success)
                    openPositionCount = positions.Positions.Count;
                else
                    warnings.Add(RuntimeHealthCodes.MissingPositionData);
            }
            catch
            {
                warnings.Add(RuntimeHealthCodes.MissingPositionData);
            }

            EvaluateInputMetrics(input, threshold, warnings, critical);
            EvaluateAccountMetrics(account, threshold, warnings, critical);
            bool? newsHealthy = await EvaluateNewsAsync(input, warnings).ConfigureAwait(false);

            var metrics = new RuntimeHealthMetricValues
            {
                Mt5Connected = mt5Connected,
                EaHealthy = eaHealthy,
                LatencyMs = latencyMs,
                SpreadPips = symbol?.SpreadPips,
                SpreadDriftPips = input.SpreadDriftPips,
                SlippageDriftPips = input.SlippageDriftPips,
                OrderRejectionRatePercent = input.OrderRejectionRatePercent,
                DrawdownPercent = input.DrawdownPercent,
                KillSwitchActive = input.KillSwitchActive,
                DailyLossUsagePercent = input.DailyLossUsagePercent,
                WeeklyLossUsagePercent = input.WeeklyLossUsagePercent,
                SymbolExposureUsagePercent = input.SymbolExposureUsagePercent,
                MarginLevelPercent = account?.MarginLevel,
                OpenPositionCount = openPositionCount,
                NewsProviderHealthy = newsHealthy,
                EaVersion = eaVersion,
                EaBuildIdentifier = eaBuild
            };

            string status = ResolveStatus(warnings, critical);
            return new RuntimeHealthSnapshot
            {
                OverallStatus = status,
                TimestampUtc = timestamp,
                Metrics = metrics,
                Warnings = warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                CriticalIssues = critical.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                RecommendedAction = RecommendedAction(status)
            };
        }

        private async Task<bool?> EvaluateNewsAsync(RuntimeHealthInput input, List<string> warnings)
        {
            bool newsDisabled = string.Equals(_apiConfig.NewsProvider, "None", StringComparison.OrdinalIgnoreCase);
            if (newsDisabled)
                return true;

            bool newsRequired = input.IsLiveMode ||
                _apiConfig.BlockTradesWhenNewsUnavailable ||
                input.Config.BrokerDeployment.RequireNewsProviderWhenConfigured;
            if (!newsRequired)
                return null;

            if (_newsCalendar == null)
            {
                warnings.Add(RuntimeHealthCodes.NewsUnavailable);
                return false;
            }

            try
            {
                var news = await _newsCalendar.GetRiskSnapshotAsync(input.ProbeRequest.Pair, _apiConfig)
                    .ConfigureAwait(false);
                if (!news.IsConfigured)
                    warnings.Add(RuntimeHealthCodes.NewsUnavailable);
                return news.IsConfigured;
            }
            catch
            {
                warnings.Add(RuntimeHealthCodes.NewsUnavailable);
                return false;
            }
        }

        private static void EvaluateLatency(
            double? latencyMs,
            RuntimeHealthMonitorConfig threshold,
            List<string> warnings,
            List<string> critical)
        {
            if (!latencyMs.HasValue)
            {
                warnings.Add(RuntimeHealthCodes.LatencyUnavailable);
                return;
            }

            if (threshold.CriticalLatencyMs > 0 && latencyMs.Value > threshold.CriticalLatencyMs)
                critical.Add(RuntimeHealthCodes.LatencyCritical);
            else if (threshold.MaxLatencyMs > 0 && latencyMs.Value > threshold.MaxLatencyMs)
                warnings.Add(RuntimeHealthCodes.LatencyHigh);
        }

        private static void EvaluateInputMetrics(
            RuntimeHealthInput input,
            RuntimeHealthMonitorConfig threshold,
            List<string> warnings,
            List<string> critical)
        {
            if (threshold.MaxSpreadDriftPips > 0 &&
                input.SpreadDriftPips > threshold.MaxSpreadDriftPips)
                warnings.Add(RuntimeHealthCodes.SpreadDriftHigh);

            if (threshold.MaxSlippageDriftPips > 0 &&
                input.SlippageDriftPips > threshold.MaxSlippageDriftPips)
                warnings.Add(RuntimeHealthCodes.SlippageDriftHigh);

            if (threshold.CriticalOrderRejectionRatePercent > 0 &&
                input.OrderRejectionRatePercent > threshold.CriticalOrderRejectionRatePercent)
                critical.Add(RuntimeHealthCodes.OrderRejectionRateCritical);
            else if (threshold.MaxOrderRejectionRatePercent > 0 &&
                input.OrderRejectionRatePercent > threshold.MaxOrderRejectionRatePercent)
                warnings.Add(RuntimeHealthCodes.OrderRejectionRateHigh);

            if (threshold.CriticalDrawdownPercent > 0 &&
                input.DrawdownPercent >= threshold.CriticalDrawdownPercent)
                critical.Add(RuntimeHealthCodes.DrawdownCritical);
            else if (threshold.WarningDrawdownPercent > 0 &&
                input.DrawdownPercent >= threshold.WarningDrawdownPercent)
                warnings.Add(RuntimeHealthCodes.DrawdownWarning);

            if (input.KillSwitchActive)
                critical.Add(RuntimeHealthCodes.KillSwitchActive);

            if (threshold.DailyLossWarningPercent > 0 &&
                input.DailyLossUsagePercent >= threshold.DailyLossWarningPercent)
                warnings.Add(RuntimeHealthCodes.DailyLossWarning);

            if (threshold.WeeklyLossWarningPercent > 0 &&
                input.WeeklyLossUsagePercent >= threshold.WeeklyLossWarningPercent)
                warnings.Add(RuntimeHealthCodes.WeeklyLossWarning);

            if (threshold.SymbolExposureWarningPercent > 0 &&
                input.SymbolExposureUsagePercent >= threshold.SymbolExposureWarningPercent)
                warnings.Add(RuntimeHealthCodes.SymbolExposureWarning);
        }

        private static void EvaluateAccountMetrics(
            AccountInfo? account,
            RuntimeHealthMonitorConfig threshold,
            List<string> warnings,
            List<string> critical)
        {
            if (account == null)
                return;

            if (threshold.MinMarginLevelPercent > 0 &&
                account.Margin > 0 &&
                account.MarginLevel > 0 &&
                account.MarginLevel < threshold.MinMarginLevelPercent)
            {
                critical.Add(RuntimeHealthCodes.MarginLevelCritical);
            }
        }

        private static string ResolveStatus(IReadOnlyList<string> warnings, IReadOnlyList<string> critical)
        {
            if (critical.Count > 0)
                return RuntimeHealthStatuses.Critical;
            if (warnings.Count > 0)
                return RuntimeHealthStatuses.Warning;
            return RuntimeHealthStatuses.Healthy;
        }

        private static string RecommendedAction(string status) => status switch
        {
            RuntimeHealthStatuses.Critical => "Pause new live entries until critical runtime health issues are resolved.",
            RuntimeHealthStatuses.Warning => "Review runtime warnings before increasing risk or enabling live trading.",
            RuntimeHealthStatuses.Healthy => "Runtime health is within configured thresholds.",
            _ => "Runtime health is unknown; verify dependencies before live trading."
        };

        private static bool IsUsableAccount(AccountInfo? account) =>
            account != null &&
            account.IsConnected &&
            IsFinitePositive(account.Equity) &&
            IsFiniteNonNegative(account.Balance) &&
            IsFiniteNonNegative(account.FreeMargin);

        private static bool IsUsableSymbol(SymbolInfo? symbol) =>
            symbol != null &&
            IsFinitePositive(symbol.Ask) &&
            IsFinitePositive(symbol.Bid) &&
            symbol.Ask >= symbol.Bid &&
            IsFinitePositive(symbol.SpreadPips);

        private static bool IsFinitePositive(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;

        private static bool IsFiniteNonNegative(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0;
    }
}
