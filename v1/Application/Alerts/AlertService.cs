using MT5TradingBot.Models;
using MT5TradingBot.Modules.Monitoring;

namespace MT5TradingBot.Modules.Alerts
{
    public sealed class AlertService : IAlertService
    {
        private readonly ISafetyAlertSink _sink;
        private readonly SafetyAlertingConfig _config;
        private readonly Func<DateTime> _utcNow;

        public AlertService(
            ISafetyAlertSink sink,
            SafetyAlertingConfig config,
            Func<DateTime>? utcNow = null)
        {
            _sink = sink;
            _config = config;
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        public async Task<SafetyAlert?> RaiseAsync(
            SafetyAlertRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_config.Enabled)
                return null;

            var now = _utcNow();
            var alerts = (await _sink.LoadAsync(cancellationToken).ConfigureAwait(false)).ToList();
            var existing = FindDedupCandidate(alerts, request, now);
            if (existing != null)
            {
                existing.Severity = MostSevere(existing.Severity, request.Severity);
                existing.Message = request.Message;
                existing.RecommendedAction = request.RecommendedAction;
                existing.LastSeenUtc = now;
                existing.OccurrenceCount = Math.Max(1, existing.OccurrenceCount) + 1;
                if (string.Equals(existing.Severity, SafetyAlertSeverities.Critical, StringComparison.OrdinalIgnoreCase))
                    existing.Acknowledged = false;
                await _sink.SaveAsync(alerts, cancellationToken).ConfigureAwait(false);
                return existing;
            }

            var alert = new SafetyAlert
            {
                AlertId = Guid.NewGuid().ToString("N"),
                Severity = request.Severity,
                Category = request.Category,
                Message = request.Message,
                TimestampUtc = now,
                LastSeenUtc = now,
                RelatedCode = request.RelatedCode,
                RecommendedAction = request.RecommendedAction,
                Acknowledged = false,
                OccurrenceCount = 1
            };
            alerts.Add(alert);
            await _sink.SaveAsync(alerts, cancellationToken).ConfigureAwait(false);
            return alert;
        }

        public async Task<IReadOnlyList<SafetyAlert>> RaiseRuntimeHealthAlertsAsync(
            RuntimeHealthSnapshot snapshot,
            CancellationToken cancellationToken = default)
        {
            var created = new List<SafetyAlert>();
            foreach (string code in snapshot.CriticalIssues)
            {
                var request = RuntimeCriticalRequest(code, snapshot);
                if (request == null)
                    continue;
                var alert = await RaiseAsync(request, cancellationToken).ConfigureAwait(false);
                if (alert != null)
                    created.Add(alert);
            }

            foreach (string code in snapshot.Warnings)
            {
                var request = RuntimeWarningRequest(code, snapshot);
                if (request == null)
                    continue;
                var alert = await RaiseAsync(request, cancellationToken).ConfigureAwait(false);
                if (alert != null)
                    created.Add(alert);
            }

            return created;
        }

        public Task<SafetyAlert?> AlertLiveReadinessBlockedAsync(
            string message,
            IEnumerable<string>? failedCriteria = null,
            CancellationToken cancellationToken = default)
        {
            string criteria = failedCriteria == null
                ? ""
                : string.Join(", ", failedCriteria.Where(c => !string.IsNullOrWhiteSpace(c)));
            string fullMessage = string.IsNullOrWhiteSpace(criteria)
                ? message
                : $"{message} Failed criteria: {criteria}";
            return RaiseAsync(new SafetyAlertRequest
            {
                Severity = SafetyAlertSeverities.Critical,
                Category = SafetyAlertCategories.LiveReadiness,
                RelatedCode = SafetyAlertCodes.LiveReadinessGateBlocked,
                Message = fullMessage,
                RecommendedAction = "Keep live trading disabled until every failed live-readiness criterion passes."
            }, cancellationToken);
        }

        public Task<SafetyAlert?> AlertKillSwitchTriggeredAsync(
            string message,
            CancellationToken cancellationToken = default) =>
            RaiseAsync(new SafetyAlertRequest
            {
                Severity = SafetyAlertSeverities.Critical,
                Category = SafetyAlertCategories.KillSwitch,
                RelatedCode = SafetyAlertCodes.KillSwitchActive,
                Message = message,
                RecommendedAction = "Keep trading paused and investigate the kill-switch trigger before clearing it explicitly."
            }, cancellationToken);

        public Task<SafetyAlert?> AlertRepeatedOrderRejectionAsync(
            string relatedCode,
            string message,
            int recentRejectionCount,
            CancellationToken cancellationToken = default)
        {
            if (recentRejectionCount < Math.Max(1, _config.RepeatedOrderRejectionThreshold))
                return Task.FromResult<SafetyAlert?>(null);

            return RaiseAsync(new SafetyAlertRequest
            {
                Severity = SafetyAlertSeverities.Warning,
                Category = SafetyAlertCategories.OrderRejection,
                RelatedCode = string.IsNullOrWhiteSpace(relatedCode)
                    ? SafetyAlertCodes.RepeatedOrderRejection
                    : relatedCode,
                Message = $"{message} Recent rejection count: {recentRejectionCount}.",
                RecommendedAction = "Review broker retcodes, spread, OrderCheck diagnostics, and connectivity before sending more live orders."
            }, cancellationToken);
        }

        public Task<SafetyAlert?> AlertEmergencyCloseAsync(
            bool failed,
            string message,
            CancellationToken cancellationToken = default) =>
            RaiseAsync(new SafetyAlertRequest
            {
                Severity = failed ? SafetyAlertSeverities.Critical : SafetyAlertSeverities.Info,
                Category = SafetyAlertCategories.EmergencyDrawdown,
                RelatedCode = failed
                    ? SafetyAlertCodes.EmergencyCloseFailed
                    : SafetyAlertCodes.EmergencyCloseAttempted,
                Message = message,
                RecommendedAction = failed
                    ? "Keep kill switch active and manually verify all open positions at the broker."
                    : "Audit emergency close results and keep monitoring account exposure."
            }, cancellationToken);

        public async Task<bool> AcknowledgeAsync(
            string alertId,
            CancellationToken cancellationToken = default)
        {
            var alerts = (await _sink.LoadAsync(cancellationToken).ConfigureAwait(false)).ToList();
            var alert = alerts.FirstOrDefault(a =>
                string.Equals(a.AlertId, alertId, StringComparison.OrdinalIgnoreCase));
            if (alert == null)
                return false;

            alert.Acknowledged = true;
            await _sink.SaveAsync(alerts, cancellationToken).ConfigureAwait(false);
            return true;
        }

        public Task<IReadOnlyList<SafetyAlert>> GetAlertsAsync(CancellationToken cancellationToken = default) =>
            _sink.LoadAsync(cancellationToken);

        private SafetyAlert? FindDedupCandidate(
            IReadOnlyList<SafetyAlert> alerts,
            SafetyAlertRequest request,
            DateTime now)
        {
            TimeSpan cooldown = TimeSpan.FromSeconds(Math.Max(0, _config.DedupCooldownSeconds));
            return alerts.FirstOrDefault(alert =>
                string.Equals(alert.Category, request.Category, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(alert.RelatedCode, request.RelatedCode, StringComparison.OrdinalIgnoreCase) &&
                now - alert.LastSeenUtc <= cooldown);
        }

        private static SafetyAlertRequest? RuntimeCriticalRequest(string code, RuntimeHealthSnapshot snapshot) =>
            code switch
            {
                RuntimeHealthCodes.Mt5Disconnected => Critical(
                    SafetyAlertCategories.Mt5Connection,
                    SafetyAlertCodes.Mt5Disconnected,
                    "MT5 bridge is disconnected.",
                    "Pause live entries and restore the MT5 bridge before trading."),
                RuntimeHealthCodes.EaHeartbeatFailed => Critical(
                    SafetyAlertCategories.EaHeartbeat,
                    SafetyAlertCodes.EaHeartbeatFailed,
                    "EA heartbeat/health check failed.",
                    "Verify the EA is running, responding, and deployed to the expected terminal."),
                RuntimeHealthCodes.OrderRejectionRateCritical => Critical(
                    SafetyAlertCategories.OrderRejection,
                    SafetyAlertCodes.RepeatedOrderRejection,
                    $"Order rejection rate is critical: {snapshot.Metrics.OrderRejectionRatePercent:F2}%.",
                    "Pause order flow and inspect broker rejection diagnostics."),
                RuntimeHealthCodes.DrawdownCritical => Critical(
                    SafetyAlertCategories.RiskLimits,
                    RuntimeHealthCodes.DrawdownCritical,
                    $"Drawdown is critical: {snapshot.Metrics.DrawdownPercent:F2}%.",
                    "Pause trading and verify drawdown protection state."),
                RuntimeHealthCodes.KillSwitchActive => Critical(
                    SafetyAlertCategories.KillSwitch,
                    SafetyAlertCodes.KillSwitchActive,
                    "Kill switch is active.",
                    "Keep live trading blocked until the kill switch is explicitly cleared."),
                RuntimeHealthCodes.MarginLevelCritical => Critical(
                    SafetyAlertCategories.Margin,
                    SafetyAlertCodes.MarginLevelCritical,
                    $"Margin level is critical: {snapshot.Metrics.MarginLevelPercent:F2}%.",
                    "Do not open new live positions until margin level recovers."),
                RuntimeHealthCodes.LatencyCritical => Critical(
                    SafetyAlertCategories.ExecutionQuality,
                    RuntimeHealthCodes.LatencyCritical,
                    $"VPS/server latency is critical: {snapshot.Metrics.LatencyMs:F0} ms.",
                    "Pause live entries and investigate VPS, terminal, or bridge latency."),
                _ => null
            };

        private static SafetyAlertRequest? RuntimeWarningRequest(string code, RuntimeHealthSnapshot snapshot) =>
            code switch
            {
                RuntimeHealthCodes.NewsUnavailable => Warning(
                    SafetyAlertCategories.News,
                    SafetyAlertCodes.NewsDataUnavailable,
                    "News data is unavailable while news checks are required.",
                    "Restore the news provider or keep live trading disabled."),
                RuntimeHealthCodes.MissingAccountData => Warning(
                    SafetyAlertCategories.AccountData,
                    SafetyAlertCodes.AccountDataUnavailable,
                    "Account data is unavailable.",
                    "Verify MT5 account connectivity before trading."),
                RuntimeHealthCodes.MissingSymbolData => Warning(
                    SafetyAlertCategories.SymbolData,
                    SafetyAlertCodes.SymbolMetadataUnavailable,
                    "Symbol metadata is unavailable.",
                    "Verify broker symbol metadata before trading."),
                RuntimeHealthCodes.LatencyUnavailable => Warning(
                    SafetyAlertCategories.ExecutionQuality,
                    RuntimeHealthCodes.LatencyUnavailable,
                    "Latency could not be measured.",
                    "Verify bridge health before live trading."),
                RuntimeHealthCodes.LatencyHigh => Warning(
                    SafetyAlertCategories.ExecutionQuality,
                    RuntimeHealthCodes.LatencyHigh,
                    $"VPS/server latency is elevated: {snapshot.Metrics.LatencyMs:F0} ms.",
                    "Review VPS, terminal, and bridge responsiveness."),
                RuntimeHealthCodes.SpreadDriftHigh => Warning(
                    SafetyAlertCategories.ExecutionQuality,
                    SafetyAlertCodes.HighSpreadDrift,
                    $"Spread drift is high: {snapshot.Metrics.SpreadDriftPips:F2} pips.",
                    "Review broker spread versus backtest/demo assumptions."),
                RuntimeHealthCodes.SlippageDriftHigh => Warning(
                    SafetyAlertCategories.ExecutionQuality,
                    SafetyAlertCodes.HighSlippageDrift,
                    $"Slippage drift is high: {snapshot.Metrics.SlippageDriftPips:F2} pips.",
                    "Review execution quality before increasing live risk."),
                RuntimeHealthCodes.OrderRejectionRateHigh => Warning(
                    SafetyAlertCategories.OrderRejection,
                    SafetyAlertCodes.RepeatedOrderRejection,
                    $"Order rejection rate is elevated: {snapshot.Metrics.OrderRejectionRatePercent:F2}%.",
                    "Review recent broker rejections before continuing live order flow."),
                RuntimeHealthCodes.DrawdownWarning => Warning(
                    SafetyAlertCategories.RiskLimits,
                    RuntimeHealthCodes.DrawdownWarning,
                    $"Drawdown is elevated: {snapshot.Metrics.DrawdownPercent:F2}%.",
                    "Monitor drawdown and verify risk limits remain active."),
                RuntimeHealthCodes.DailyLossWarning => Warning(
                    SafetyAlertCategories.RiskLimits,
                    SafetyAlertCodes.DailyLossWarning,
                    $"Daily loss usage is elevated: {snapshot.Metrics.DailyLossUsagePercent:F2}%.",
                    "Reduce trading activity and verify daily loss stop state."),
                RuntimeHealthCodes.WeeklyLossWarning => Warning(
                    SafetyAlertCategories.RiskLimits,
                    SafetyAlertCodes.WeeklyLossWarning,
                    $"Weekly loss usage is elevated: {snapshot.Metrics.WeeklyLossUsagePercent:F2}%.",
                    "Reduce trading activity and verify weekly loss stop state."),
                RuntimeHealthCodes.SymbolExposureWarning => Warning(
                    SafetyAlertCategories.RiskLimits,
                    RuntimeHealthCodes.SymbolExposureWarning,
                    $"Symbol exposure usage is elevated: {snapshot.Metrics.SymbolExposureUsagePercent:F2}%.",
                    "Review same-symbol exposure before adding positions."),
                _ => null
            };

        private static SafetyAlertRequest Critical(
            string category,
            string code,
            string message,
            string action) => new()
        {
            Severity = SafetyAlertSeverities.Critical,
            Category = category,
            RelatedCode = code,
            Message = message,
            RecommendedAction = action
        };

        private static SafetyAlertRequest Warning(
            string category,
            string code,
            string message,
            string action) => new()
        {
            Severity = SafetyAlertSeverities.Warning,
            Category = category,
            RelatedCode = code,
            Message = message,
            RecommendedAction = action
        };

        private static string MostSevere(string current, string incoming)
        {
            int CurrentRank(string severity) => severity switch
            {
                SafetyAlertSeverities.Critical => 3,
                SafetyAlertSeverities.Warning => 2,
                _ => 1
            };

            return CurrentRank(incoming) > CurrentRank(current) ? incoming : current;
        }
    }
}
