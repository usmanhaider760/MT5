using System.Text;

namespace MT5TradingBot.Modules.LiveReadiness
{
    public sealed class FinalGoNoGoChecklist
    {
        public const string ReportFileName = "FINAL_GO_NO_GO_CHECKLIST.md";

        public FinalGoNoGoResult EvaluateAndWriteReport(FinalGoNoGoInput input)
        {
            var result = Evaluate(input);
            string directory = string.IsNullOrWhiteSpace(input.ReportDirectory)
                ? Directory.GetCurrentDirectory()
                : input.ReportDirectory;
            Directory.CreateDirectory(directory);

            string path = Path.Combine(directory, ReportFileName);
            File.WriteAllText(path, result.Markdown);

            return new FinalGoNoGoResult
            {
                Decision = result.Decision,
                FailedCriteria = result.FailedCriteria,
                Warnings = result.Warnings,
                RequiredManualActions = result.RequiredManualActions,
                RecommendedNextStep = result.RecommendedNextStep,
                TimestampUtc = result.TimestampUtc,
                ReportPath = path,
                Markdown = result.Markdown,
                ChecklistItems = result.ChecklistItems
            };
        }

        public FinalGoNoGoResult Evaluate(FinalGoNoGoInput input)
        {
            DateTime timestampUtc = DateTime.UtcNow;
            var items = BuildItems(input);
            var failed = new List<string>();
            var warnings = new List<string>();
            var manualActions = new List<string>();

            foreach (var item in items)
            {
                if (item.Status == FinalChecklistStatus.Fail && item.Required)
                    failed.Add(item.Name);
                else if (item.Status == FinalChecklistStatus.Missing && item.Required)
                    warnings.Add($"Missing evidence: {item.Name}");
                else if (item.Status == FinalChecklistStatus.Warning)
                    warnings.Add(item.Name);

                if (!string.IsNullOrWhiteSpace(item.ManualAction) &&
                    item.Status != FinalChecklistStatus.Pass)
                    manualActions.Add(item.ManualAction);
            }

            ApplyHardSafetyRules(input, failed, warnings, manualActions);
            FinalGoNoGoDecision decision = Decide(input, items, failed, warnings, manualActions);
            string nextStep = RecommendedNextStep(decision, input, failed, warnings, manualActions);
            string markdown = BuildMarkdown(input, items, decision, failed, warnings, manualActions, nextStep, timestampUtc);

            return new FinalGoNoGoResult
            {
                Decision = decision,
                FailedCriteria = failed.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                Warnings = warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                RequiredManualActions = manualActions.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                RecommendedNextStep = nextStep,
                TimestampUtc = DateTime.SpecifyKind(timestampUtc, DateTimeKind.Utc),
                Markdown = markdown,
                ChecklistItems = items
            };
        }

        private static IReadOnlyList<FinalChecklistItem> BuildItems(FinalGoNoGoInput input)
        {
            bool liveTarget = input.Target != FinalGoNoGoTarget.PaperOrDemo;
            bool tinyLive = input.Target == FinalGoNoGoTarget.TinyLive;
            var items = new List<FinalChecklistItem>
            {
                Item("P0 account safety readiness", input.P0AccountSafetyReadiness, true,
                    "P0 safety tests and account-protection controls must be current.",
                    "Run/record P0 safety readiness evidence."),
                Item("P1 execution realism readiness", input.P1ExecutionRealismReadiness, true,
                    "P1 execution realism tests and broker-order assumptions must be current.",
                    "Run/record P1 execution realism evidence."),
                Item("P2 realistic backtest readiness", input.P2RealisticBacktestReadiness, true,
                    "P2 realistic backtest report must be generated without MT5/live dependency.",
                    "Generate/attach the P2 realistic backtest report."),
                Item("P3 strategy edge proof readiness", input.P3StrategyEdgeProofReadiness, true,
                    "P3 proof must support the requested deployment scope.",
                    "Generate/attach an acceptable P3 final proof package."),
                Item("P4 live readiness gate", input.P4LiveReadinessGate, liveTarget,
                    "Live readiness gate must pass before any real-money deployment.",
                    "Pass the P4 live readiness gate before live enablement."),
                Item("Demo forward-test gate", input.DemoForwardTestGate, liveTarget,
                    "Demo/paper reconciliation and forward-test criteria must support live escalation.",
                    "Complete demo forward-test evidence and reconciliation."),
                Item("Broker/EA deployment checklist", input.BrokerEaDeploymentChecklist, liveTarget,
                    "Broker, EA, symbol metadata, OrderCheck, margin, latency, and dependency checks must pass.",
                    "Fix broker/EA checklist failures and redeploy the EA if needed."),
                Item("Runtime health status", RuntimeStatus(input.RuntimeHealthStatus), liveTarget,
                    $"Runtime health is {input.RuntimeHealthStatus}.",
                    "Restore runtime health to Healthy before live deployment."),
                Item("Safety alert status", input.SafetyAlertStatus, liveTarget,
                    "No unresolved critical safety alerts should be present.",
                    "Clear or acknowledge critical safety alerts."),
                Item("Operational readiness report status", input.OperationalReadinessReportStatus, liveTarget,
                    "Dashboard/readiness report must be available for audit.",
                    "Generate the operational readiness report."),
                Item("Staged rollout status", input.StagedRolloutStatus, liveTarget,
                    "Rollout stage must match requested deployment scope.",
                    "Review staged rollout status before escalation."),
                Item("Kill switch inactive", BoolStatus(input.KillSwitchInactive), liveTarget,
                    "Kill switch must be inactive for live deployment.",
                    "Resolve and explicitly review the kill switch state."),
                Item("User live enablement status", BoolStatus(input.UserLiveEnablementConfirmed), liveTarget,
                    "User must manually confirm live enablement.",
                    "Capture explicit user live enablement confirmation."),
                Item("EA compiled/redeployed note", input.EaCompiledRedeployedNote, liveTarget,
                    "EA compile/redeploy status must be documented.",
                    "Compile/redeploy the EA and record the deployment note."),
                Item("MT5 connection/health", input.Mt5ConnectionHealth, liveTarget,
                    "MT5 connection and account health must be known.",
                    "Restore MT5 connection/account health."),
                Item("News provider status", input.NewsProviderRequired ? input.NewsProviderStatus : FinalChecklistStatus.Pass,
                    input.NewsProviderRequired && liveTarget,
                    input.NewsProviderRequired ? "News provider is required by configuration." : "News provider is not required by current configuration.",
                    "Configure or restore the required news provider.")
            };

            if (tinyLive)
            {
                items.Add(Item(
                    "Tiny-live reduced risk caps",
                    input.TinyLiveRiskCapsConfigured ? FinalChecklistStatus.Pass : FinalChecklistStatus.Fail,
                    true,
                    "Tiny-live must use reduced risk caps.",
                    "Configure TinyLive reduced max risk percent and lot multiplier."));
            }

            return items;
        }

        private static void ApplyHardSafetyRules(
            FinalGoNoGoInput input,
            List<string> failed,
            List<string> warnings,
            List<string> manualActions)
        {
            bool liveRequested = input.Target != FinalGoNoGoTarget.PaperOrDemo;

            if (liveRequested && input.P3StrategyEdgeProofReadiness == FinalChecklistStatus.Fail)
                failed.Add("P3 strategy edge proof readiness");

            if (input.KillSwitchInactive == false)
                failed.Add("Kill switch inactive");

            if (input.BrokerEaDeploymentChecklist == FinalChecklistStatus.Fail)
                failed.Add("Broker/EA deployment checklist");

            if (input.RuntimeHealthStatus == FinalRuntimeHealthStatus.Critical)
                failed.Add("Runtime health status");

            if (input.Target == FinalGoNoGoTarget.FullLive && !input.AllowFullLiveGo)
            {
                warnings.Add("Full live Go is not allowed by default.");
                manualActions.Add("Explicitly authorize full-live release review before marking full live as Go.");
            }
        }

        private static FinalGoNoGoDecision Decide(
            FinalGoNoGoInput input,
            IReadOnlyList<FinalChecklistItem> items,
            IReadOnlyList<string> failed,
            IReadOnlyList<string> warnings,
            IReadOnlyList<string> manualActions)
        {
            if (failed.Count > 0)
                return FinalGoNoGoDecision.NoGo;

            bool missingRequired = items.Any(i => i.Required && i.Status == FinalChecklistStatus.Missing);
            if (missingRequired)
                return FinalGoNoGoDecision.Unknown;

            if (input.Target == FinalGoNoGoTarget.PaperOrDemo)
                return FinalGoNoGoDecision.ConditionalGo;

            if (input.Target == FinalGoNoGoTarget.TinyLive)
                return input.TinyLiveRiskCapsConfigured
                    ? FinalGoNoGoDecision.ConditionalGo
                    : FinalGoNoGoDecision.NoGo;

            return input.AllowFullLiveGo && manualActions.Count == 0 && warnings.Count == 0
                ? FinalGoNoGoDecision.Go
                : FinalGoNoGoDecision.ConditionalGo;
        }

        private static string RecommendedNextStep(
            FinalGoNoGoDecision decision,
            FinalGoNoGoInput input,
            IReadOnlyList<string> failed,
            IReadOnlyList<string> warnings,
            IReadOnlyList<string> manualActions) =>
            decision switch
            {
                FinalGoNoGoDecision.Go => "Proceed only after final human review confirms the same evidence set.",
                FinalGoNoGoDecision.ConditionalGo when input.Target == FinalGoNoGoTarget.TinyLive =>
                    "Proceed only with tiny-live reduced risk caps, monitoring, and manual confirmation.",
                FinalGoNoGoDecision.ConditionalGo =>
                    "Continue paper/demo validation or complete the listed manual actions before live escalation.",
                FinalGoNoGoDecision.NoGo =>
                    $"Do not enable live trading. Resolve failed criteria: {string.Join(", ", failed)}.",
                _ => $"Collect missing evidence before making a live decision: {string.Join(", ", warnings)}."
            };

        private static string BuildMarkdown(
            FinalGoNoGoInput input,
            IReadOnlyList<FinalChecklistItem> items,
            FinalGoNoGoDecision decision,
            IReadOnlyList<string> failed,
            IReadOnlyList<string> warnings,
            IReadOnlyList<string> manualActions,
            string nextStep,
            DateTime timestampUtc)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Final Go/No-Go Checklist");
            sb.AppendLine();
            sb.AppendLine($"- Decision: {FormatDecision(decision)}");
            sb.AppendLine($"- Target: {input.Target}");
            sb.AppendLine($"- Timestamp UTC: {timestampUtc:O}");
            sb.AppendLine();
            sb.AppendLine("## Required Notices");
            sb.AppendLine("- This is not financial advice.");
            sb.AppendLine("- Backtests are not live proof.");
            sb.AppendLine("- Real-money trading remains blocked unless all Go criteria pass.");
            sb.AppendLine("- Tiny-live must use reduced risk caps.");
            sb.AppendLine("- User must manually confirm live enablement.");
            sb.AppendLine();
            sb.AppendLine("## Checklist");
            sb.AppendLine("| Criterion | Status | Required | Detail | Manual Action |");
            sb.AppendLine("| --- | --- | --- | --- | --- |");
            foreach (var item in items)
            {
                sb.Append("| ");
                sb.Append(Escape(item.Name));
                sb.Append(" | ");
                sb.Append(item.Status);
                sb.Append(" | ");
                sb.Append(item.Required ? "Yes" : "No");
                sb.Append(" | ");
                sb.Append(Escape(item.Detail));
                sb.Append(" | ");
                sb.Append(Escape(item.ManualAction));
                sb.AppendLine(" |");
            }

            AppendList(sb, "Failed Criteria", failed);
            AppendList(sb, "Warnings", warnings);
            AppendList(sb, "Required Manual Actions", manualActions);
            sb.AppendLine("## Recommended Next Step");
            sb.AppendLine(nextStep);
            return sb.ToString();
        }

        private static void AppendList(StringBuilder sb, string title, IReadOnlyList<string> values)
        {
            sb.AppendLine();
            sb.AppendLine($"## {title}");
            if (values.Count == 0)
            {
                sb.AppendLine("- None");
                return;
            }

            foreach (string value in values.Distinct(StringComparer.OrdinalIgnoreCase))
                sb.AppendLine($"- {value}");
        }

        private static FinalChecklistItem Item(
            string name,
            FinalChecklistStatus status,
            bool required,
            string detail,
            string manualAction) =>
            new()
            {
                Name = name,
                Status = status,
                Required = required,
                Detail = detail,
                ManualAction = manualAction
            };

        private static FinalChecklistStatus BoolStatus(bool? value) =>
            value.HasValue
                ? value.Value ? FinalChecklistStatus.Pass : FinalChecklistStatus.Fail
                : FinalChecklistStatus.Missing;

        private static FinalChecklistStatus RuntimeStatus(FinalRuntimeHealthStatus status) =>
            status switch
            {
                FinalRuntimeHealthStatus.Healthy => FinalChecklistStatus.Pass,
                FinalRuntimeHealthStatus.Degraded => FinalChecklistStatus.Warning,
                FinalRuntimeHealthStatus.Critical => FinalChecklistStatus.Fail,
                _ => FinalChecklistStatus.Missing
            };

        private static string FormatDecision(FinalGoNoGoDecision decision) =>
            decision switch
            {
                FinalGoNoGoDecision.NoGo => "No-Go",
                FinalGoNoGoDecision.ConditionalGo => "Conditional-Go",
                _ => decision.ToString()
            };

        private static string Escape(string value) =>
            value.Replace("|", "\\|", StringComparison.Ordinal);
    }
}
