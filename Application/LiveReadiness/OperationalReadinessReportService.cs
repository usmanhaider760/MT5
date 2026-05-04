using System.Text;
using MT5TradingBot.Data;
using MT5TradingBot.Models;
using MT5TradingBot.Modules.Alerts;
using MT5TradingBot.Modules.Monitoring;

namespace MT5TradingBot.Modules.LiveReadiness
{
    public sealed class OperationalReadinessReportService
    {
        public const string ReportFileName = "OPERATIONAL_READINESS_REPORT.md";

        public async Task<OperationalReadinessReportResult> GenerateAsync(
            OperationalReadinessReportInput input,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var timestamp = input.TimestampUtc ?? DateTime.UtcNow;
            var failed = new List<string>();
            var warnings = new List<string>();

            EvaluateInput(input, failed, warnings);
            string readiness = ResolveOverallReadiness(input, failed, warnings);
            string action = RecommendedAction(readiness);
            string reportPath = Path.Combine(
                string.IsNullOrWhiteSpace(input.OutputDirectory)
                    ? Directory.GetCurrentDirectory()
                    : input.OutputDirectory,
                ReportFileName);

            string markdown = BuildMarkdown(input, readiness, failed, warnings, action, timestamp);

            string? folder = Path.GetDirectoryName(reportPath);
            if (!string.IsNullOrWhiteSpace(folder))
                Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(reportPath, markdown, cancellationToken).ConfigureAwait(false);

            return new OperationalReadinessReportResult
            {
                OverallReadiness = readiness,
                ReportPath = reportPath,
                FailedCriteria = failed.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                Warnings = warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                RecommendedAction = action,
                TimestampUtc = timestamp,
                Markdown = markdown
            };
        }

        private static void EvaluateInput(
            OperationalReadinessReportInput input,
            List<string> failed,
            List<string> warnings)
        {
            if (input.LiveReadiness == null)
                warnings.Add("LIVE_READINESS_UNKNOWN");
            else if (!input.LiveReadiness.IsAllowed)
                failed.AddRange(input.LiveReadiness.FailedCriteria.Count > 0
                    ? input.LiveReadiness.FailedCriteria
                    : [LiveReadinessCodes.Blocked]);

            if (input.DemoForwardTest == null)
                warnings.Add("DEMO_FORWARD_TEST_UNKNOWN");
            else
            {
                if (!input.DemoForwardTest.Passed)
                    failed.AddRange(input.DemoForwardTest.FailedCriteria.Count > 0
                        ? input.DemoForwardTest.FailedCriteria
                        : [DemoForwardTestCodes.NotPassed]);
                warnings.AddRange(input.DemoForwardTest.Warnings);
            }

            if (input.BrokerDeployment == null)
                warnings.Add("BROKER_DEPLOYMENT_UNKNOWN");
            else
            {
                if (!input.BrokerDeployment.Passed)
                    failed.AddRange(input.BrokerDeployment.FailedCriteria.Count > 0
                        ? input.BrokerDeployment.FailedCriteria
                        : [LiveReadinessCodes.BrokerReadinessFailed]);
                warnings.AddRange(input.BrokerDeployment.Warnings);
            }

            if (input.RuntimeHealth == null)
                warnings.Add("RUNTIME_HEALTH_UNKNOWN");
            else
            {
                failed.AddRange(input.RuntimeHealth.CriticalIssues);
                warnings.AddRange(input.RuntimeHealth.Warnings);
            }

            if (input.KillSwitch?.KillSwitchActive == true)
                failed.Add(LiveReadinessCodes.KillSwitchActive);

            foreach (var alert in input.RecentAlerts)
            {
                if (string.Equals(alert.Severity, SafetyAlertSeverities.Critical, StringComparison.OrdinalIgnoreCase) &&
                    !alert.Acknowledged)
                {
                    failed.Add(string.IsNullOrWhiteSpace(alert.RelatedCode)
                        ? "UNACKNOWLEDGED_CRITICAL_ALERT"
                        : alert.RelatedCode);
                }
            }
        }

        private static string ResolveOverallReadiness(
            OperationalReadinessReportInput input,
            IReadOnlyList<string> failed,
            IReadOnlyList<string> warnings)
        {
            if (failed.Count > 0)
                return OperationalReadinessStatuses.NotReady;

            bool hasUnknown = input.LiveReadiness == null ||
                input.DemoForwardTest == null ||
                input.BrokerDeployment == null ||
                input.RuntimeHealth == null;
            if (hasUnknown)
                return OperationalReadinessStatuses.Unknown;

            if (warnings.Count > 0)
                return OperationalReadinessStatuses.Warning;

            return OperationalReadinessStatuses.Ready;
        }

        private static string RecommendedAction(string readiness) => readiness switch
        {
            OperationalReadinessStatuses.Ready =>
                "All supplied readiness inputs are passing. Keep live trading disabled unless explicitly enabled through the final live gate.",
            OperationalReadinessStatuses.Warning =>
                "Review warnings before enabling live trading or increasing risk.",
            OperationalReadinessStatuses.NotReady =>
                "Do not enable live trading. Resolve failed criteria and rerun the operational readiness report.",
            _ =>
                "Do not treat readiness as proven. Collect missing operational data and rerun the report."
        };

        private static string BuildMarkdown(
            OperationalReadinessReportInput input,
            string readiness,
            IReadOnlyList<string> failed,
            IReadOnlyList<string> warnings,
            string action,
            DateTime timestamp)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Operational Readiness Report");
            sb.AppendLine();
            sb.AppendLine($"- Timestamp UTC: {timestamp:O}");
            sb.AppendLine($"- Overall readiness: {readiness}");
            sb.AppendLine($"- Recommended next action: {action}");
            sb.AppendLine($"- Mode recommendation: {ValueOrUnknown(input.ModeRecommendation)}");
            sb.AppendLine();

            AppendList(sb, "Failed Criteria", failed, "None");
            AppendList(sb, "Warnings", warnings, "None");

            AppendLiveReadiness(sb, input.LiveReadiness);
            AppendDemoForward(sb, input.DemoForwardTest);
            AppendBroker(sb, input.BrokerDeployment);
            AppendRuntimeHealth(sb, input.RuntimeHealth);
            AppendKillSwitch(sb, input.KillSwitch);
            AppendRiskUsage(sb, input.RuntimeHealth?.Metrics);
            AppendTrades(sb, input.RecentTrades);
            AppendAlerts(sb, input.RecentAlerts);
            AppendStrategyEvidence(sb, input);

            return sb.ToString();
        }

        private static void AppendLiveReadiness(StringBuilder sb, LiveReadinessResult? result)
        {
            sb.AppendLine("## Final Live Readiness Gate");
            if (result == null)
            {
                sb.AppendLine("- Status: Unknown");
                sb.AppendLine("- Note: Live readiness result was not supplied.");
                sb.AppendLine();
                return;
            }

            sb.AppendLine($"- Status: {(result.IsAllowed ? "Pass" : "Blocked")}");
            sb.AppendLine($"- Evidence classification: {ValueOrUnknown(result.EvidenceClassification)}");
            sb.AppendLine($"- Strategy edge verdict: {ValueOrUnknown(result.StrategyEdgeVerdict)}");
            sb.AppendLine($"- Demo reconciliation verdict: {ValueOrUnknown(result.DemoReconciliationVerdict)}");
            sb.AppendLine($"- Failed criteria: {ListInline(result.FailedCriteria)}");
            sb.AppendLine();
        }

        private static void AppendDemoForward(StringBuilder sb, DemoForwardTestResult? result)
        {
            sb.AppendLine("## Demo Forward-Test Gate");
            if (result == null)
            {
                sb.AppendLine("- Status: Unknown");
                sb.AppendLine("- Note: Demo forward-test result was not supplied.");
                sb.AppendLine();
                return;
            }

            sb.AppendLine($"- Status: {(result.Passed ? "Pass" : "Not Passed")}");
            sb.AppendLine($"- Verdict: {result.Verdict}");
            sb.AppendLine($"- Completed trades: {result.Metrics.CompletedTrades}");
            sb.AppendLine($"- Duration days: {result.Metrics.DurationDays:F1}");
            sb.AppendLine($"- Profit factor: {result.Metrics.ProfitFactor:F2}");
            sb.AppendLine($"- Expectancy USD: {result.Metrics.ExpectancyUsd:F2}");
            sb.AppendLine($"- Failed criteria: {ListInline(result.FailedCriteria)}");
            sb.AppendLine();
        }

        private static void AppendBroker(StringBuilder sb, BrokerDeploymentChecklistResult? result)
        {
            sb.AppendLine("## Broker / EA Deployment Checklist");
            if (result == null)
            {
                sb.AppendLine("- Status: Unknown");
                sb.AppendLine("- Note: Broker/EA checklist result was not supplied.");
                sb.AppendLine();
                return;
            }

            sb.AppendLine($"- Status: {(result.Passed ? "Pass" : "Not Passed")}");
            sb.AppendLine($"- Verdict: {result.Verdict}");
            sb.AppendLine($"- EA version: {ValueOrUnknown(result.EaVersion)}");
            sb.AppendLine($"- EA build: {ValueOrUnknown(result.EaBuildIdentifier)}");
            sb.AppendLine($"- Latency ms: {FormatNullable(result.LatencyMs)}");
            sb.AppendLine($"- Failed criteria: {ListInline(result.FailedCriteria)}");
            sb.AppendLine();
        }

        private static void AppendRuntimeHealth(StringBuilder sb, RuntimeHealthSnapshot? snapshot)
        {
            sb.AppendLine("## Runtime Health Snapshot");
            if (snapshot == null)
            {
                sb.AppendLine("- Status: Unknown");
                sb.AppendLine("- Note: Runtime health snapshot was not supplied.");
                sb.AppendLine();
                return;
            }

            sb.AppendLine($"- Status: {snapshot.OverallStatus}");
            sb.AppendLine($"- Timestamp UTC: {snapshot.TimestampUtc:O}");
            sb.AppendLine($"- Critical issues: {ListInline(snapshot.CriticalIssues)}");
            sb.AppendLine($"- Warnings: {ListInline(snapshot.Warnings)}");
            sb.AppendLine($"- Recommended action: {ValueOrUnknown(snapshot.RecommendedAction)}");
            sb.AppendLine();
        }

        private static void AppendKillSwitch(StringBuilder sb, KillSwitchState? state)
        {
            sb.AppendLine("## Kill Switch");
            if (state == null)
            {
                sb.AppendLine("- State: Unknown");
                sb.AppendLine();
                return;
            }

            sb.AppendLine($"- Active: {state.KillSwitchActive}");
            sb.AppendLine($"- Reason: {ValueOrUnknown(state.KillSwitchReason)}");
            sb.AppendLine($"- Triggered UTC: {(state.KillSwitchTriggeredAtUtc.HasValue ? state.KillSwitchTriggeredAtUtc.Value.ToString("O") : "Unknown")}");
            sb.AppendLine();
        }

        private static void AppendRiskUsage(StringBuilder sb, RuntimeHealthMetricValues? metrics)
        {
            sb.AppendLine("## Current Risk Usage");
            sb.AppendLine($"- Daily loss usage percent: {FormatNullable(metrics?.DailyLossUsagePercent)}");
            sb.AppendLine($"- Weekly loss usage percent: {FormatNullable(metrics?.WeeklyLossUsagePercent)}");
            sb.AppendLine($"- Symbol exposure usage percent: {FormatNullable(metrics?.SymbolExposureUsagePercent)}");
            sb.AppendLine($"- Margin level percent: {FormatNullable(metrics?.MarginLevelPercent)}");
            sb.AppendLine($"- Open positions: {(metrics?.OpenPositionCount.HasValue == true ? metrics.OpenPositionCount.Value.ToString() : "Unknown")}");
            sb.AppendLine();
        }

        private static void AppendTrades(StringBuilder sb, IReadOnlyList<TradeRecord> trades)
        {
            sb.AppendLine("## Recent Trades Summary");
            if (trades.Count == 0)
            {
                sb.AppendLine("- Recent trades: Unknown / none supplied");
                sb.AppendLine("- Rejected trades: Unknown / none supplied");
                sb.AppendLine();
                return;
            }

            int rejected = trades.Count(IsRejectedTrade);
            int successful = trades.Count(t => !IsRejectedTrade(t));
            double closedPnl = trades.Where(t => t.ClosedAt.HasValue).Sum(t => t.ProfitUsd);
            sb.AppendLine($"- Recent trades supplied: {trades.Count}");
            sb.AppendLine($"- Non-rejected trades: {successful}");
            sb.AppendLine($"- Rejected trades: {rejected}");
            sb.AppendLine($"- Closed P/L USD: {closedPnl:F2}");
            sb.AppendLine();

            sb.AppendLine("### Rejected Trades Summary");
            if (rejected == 0)
            {
                sb.AppendLine("- None");
                sb.AppendLine();
                return;
            }

            foreach (var group in trades.Where(IsRejectedTrade)
                         .GroupBy(t => string.IsNullOrWhiteSpace(t.ErrorCode) ? "REJECTED" : t.ErrorCode)
                         .OrderByDescending(g => g.Count()))
            {
                sb.AppendLine($"- {group.Key}: {group.Count()}");
            }
            sb.AppendLine();
        }

        private static void AppendAlerts(StringBuilder sb, IReadOnlyList<SafetyAlert> alerts)
        {
            sb.AppendLine("## Recent Safety Alerts");
            if (alerts.Count == 0)
            {
                sb.AppendLine("- None supplied");
                sb.AppendLine();
                return;
            }

            foreach (var alert in alerts.OrderByDescending(a => a.LastSeenUtc).Take(10))
            {
                sb.AppendLine($"- {alert.Severity} | {alert.Category} | {alert.RelatedCode} | Ack={alert.Acknowledged} | Count={alert.OccurrenceCount} | {alert.Message}");
            }
            sb.AppendLine();
        }

        private static void AppendStrategyEvidence(StringBuilder sb, OperationalReadinessReportInput input)
        {
            sb.AppendLine("## Strategy Evidence");
            sb.AppendLine($"- Strategy evidence classification: {ValueOrUnknown(input.StrategyEvidenceClassification)}");
            sb.AppendLine($"- Live/demo/paper recommendation: {ValueOrUnknown(input.ModeRecommendation)}");
            sb.AppendLine();
        }

        private static void AppendList(
            StringBuilder sb,
            string title,
            IReadOnlyList<string> values,
            string emptyText)
        {
            sb.AppendLine($"## {title}");
            if (values.Count == 0)
                sb.AppendLine($"- {emptyText}");
            else
                foreach (string value in values.Distinct(StringComparer.OrdinalIgnoreCase))
                    sb.AppendLine($"- {value}");
            sb.AppendLine();
        }

        private static bool IsRejectedTrade(TradeRecord record) =>
            string.Equals(record.Status, TradeStatus.Rejected.ToString(), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(record.Status, "Rejected", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrWhiteSpace(record.ErrorCode);

        private static string ListInline(IReadOnlyList<string> values) =>
            values.Count == 0 ? "None" : string.Join(", ", values);

        private static string ValueOrUnknown(string value) =>
            string.IsNullOrWhiteSpace(value) ? "Unknown" : value;

        private static string FormatNullable(double? value) =>
            value.HasValue ? value.Value.ToString("F2") : "Unknown";
    }
}
