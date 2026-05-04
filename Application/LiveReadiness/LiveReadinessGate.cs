using MT5TradingBot.Core;
using MT5TradingBot.Models;
using MT5TradingBot.Modules.Deployment;
using MT5TradingBot.Modules.StrategyProof;

namespace MT5TradingBot.Modules.LiveReadiness
{
    public sealed class LiveReadinessGate : ILiveReadinessGate
    {
        private const string StrategyProofFileName = "FINAL_STRATEGY_PROOF_PACKAGE.md";
        private const string StrategyEdgeFileName = "STRATEGY_EDGE_VERDICT_REPORT.md";
        private readonly DemoForwardTestGate _demoForwardTestGate = new();

        public LiveReadinessResult Evaluate(BotConfig config, LiveReadinessContext context)
        {
            bool gateApplies = config.EnableFinalLiveReadinessGate &&
                (context.IsLiveMode || config.ApplyFinalLiveReadinessGateToPaper);
            if (!gateApplies)
                return new LiveReadinessResult { IsAllowed = true };

            var failed = new List<string>();

            if (context.KillSwitchActive || context.EmergencyStopActive)
                failed.Add(LiveReadinessCodes.KillSwitchActive);

            if (!P0P1ReadinessVerified(config))
                failed.Add(LiveReadinessCodes.TestStatusNotVerified);

            var evidence = ReadEvidence(config);
            if (!IsLiveProofAcceptable(config, evidence))
                failed.Add(LiveReadinessCodes.StrategyEdgeNotProven);

            if (config.RequireDemoReconciliationForLive &&
                !evidence.DemoReconciliationVerdict.Equals(DemoPaperReconciliationVerdicts.Matches, StringComparison.OrdinalIgnoreCase))
            {
                failed.Add(LiveReadinessCodes.DemoReconciliationRequired);
            }

            DemoForwardTestResult? demoForwardTest = null;
            if (config.RequireDemoReconciliationForLive && config.RequireDemoForwardTestForLive)
            {
                demoForwardTest = _demoForwardTestGate.Evaluate(config.DemoForwardTest);
                if (!demoForwardTest.Passed)
                    failed.Add(LiveReadinessCodes.DemoForwardTestNotPassed);
            }

            if (config.RequireBrokerReadinessForLive &&
                context.BrokerDeploymentResult?.Passed != true)
            {
                failed.Add(LiveReadinessCodes.BrokerReadinessFailed);
            }

            if (IsBrokerReadinessExplicitlyFailed(config.BrokerEaReadinessStatus))
                failed.Add(LiveReadinessCodes.BrokerReadinessFailed);

            if (config.RequireUserLiveEnablement && !config.UserLiveTradingEnabled)
                failed.Add(LiveReadinessCodes.UserLiveEnableRequired);

            return new LiveReadinessResult
            {
                IsAllowed = failed.Count == 0,
                FailedCriteria = failed.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                EvidenceClassification = evidence.EvidenceClassification,
                StrategyEdgeVerdict = evidence.StrategyEdgeVerdict,
                DemoReconciliationVerdict = evidence.DemoReconciliationVerdict,
                DemoForwardTestResult = demoForwardTest,
                BrokerDeploymentResult = context.BrokerDeploymentResult,
                RolloutEvaluation = context.RolloutEvaluation
            };
        }

        private static bool P0P1ReadinessVerified(BotConfig config)
        {
            if (config.P0SafetyReadinessVerified && config.P1ExecutionReadinessVerified)
                return true;

            string markerPath = ResolvePath(config.P0P1ReadinessMarkerFile);
            if (string.IsNullOrWhiteSpace(markerPath) || !File.Exists(markerPath))
                return false;

            try
            {
                string text = File.ReadAllText(markerPath);
                return MarkerContainsVerifiedStatus(text, "p0") &&
                    MarkerContainsVerifiedStatus(text, "p1");
            }
            catch
            {
                return false;
            }
        }

        private static bool MarkerContainsVerifiedStatus(string text, string phase)
        {
            string lowered = text.ToLowerInvariant();
            int phaseIndex = lowered.IndexOf(phase, StringComparison.Ordinal);
            if (phaseIndex < 0)
                return false;

            string phaseWindow = lowered.Substring(phaseIndex, Math.Min(120, lowered.Length - phaseIndex));
            return phaseWindow.Contains("pass", StringComparison.Ordinal) ||
                phaseWindow.Contains("passed", StringComparison.Ordinal) ||
                phaseWindow.Contains("verified", StringComparison.Ordinal);
        }

        private static LiveReadinessEvidence ReadEvidence(BotConfig config)
        {
            string proofPath = ResolvePath(config.FinalStrategyProofPackagePath);
            if (string.IsNullOrWhiteSpace(proofPath))
                proofPath = Path.Combine(AppPaths.RootDirectory, StrategyProofFileName);

            string edgePath = ResolvePath(config.StrategyEdgeVerdictReportPath);
            if (string.IsNullOrWhiteSpace(edgePath))
                edgePath = Path.Combine(AppPaths.RootDirectory, StrategyEdgeFileName);

            string proof = SafeRead(proofPath);
            string edge = SafeRead(edgePath);

            return new LiveReadinessEvidence
            {
                EvidenceClassification = ExtractBulletValue(proof, "Evidence classification"),
                StrategyEdgeVerdict = ExtractBulletValue(edge, "Verdict"),
                DemoReconciliationVerdict = !string.IsNullOrWhiteSpace(config.DemoPaperReconciliationVerdict)
                    ? config.DemoPaperReconciliationVerdict.Trim()
                    : ExtractDemoReconciliationVerdict(proof)
            };
        }

        private static bool IsLiveProofAcceptable(BotConfig config, LiveReadinessEvidence evidence)
        {
            bool proven = evidence.EvidenceClassification.Equals(
                StrategyEvidenceClassifications.ProvenPositiveEdge,
                StringComparison.OrdinalIgnoreCase);
            if (config.RequireProvenEdgeForLive && !proven)
                return false;

            if (!proven)
                return false;

            if (string.IsNullOrWhiteSpace(evidence.StrategyEdgeVerdict))
                return false;

            return !evidence.StrategyEdgeVerdict.Equals(StrategyEdgeVerdicts.Fail, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsBrokerReadinessExplicitlyFailed(string status)
        {
            if (string.IsNullOrWhiteSpace(status))
                return false;

            return status.Equals("Fail", StringComparison.OrdinalIgnoreCase) ||
                status.Equals("Failed", StringComparison.OrdinalIgnoreCase) ||
                status.Equals("ExplicitlyFailed", StringComparison.OrdinalIgnoreCase);
        }

        private static string ExtractBulletValue(string markdown, string label)
        {
            if (string.IsNullOrWhiteSpace(markdown))
                return "";

            string prefix = $"- {label}:";
            foreach (string line in markdown.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return trimmed[prefix.Length..].Trim();
            }

            return "";
        }

        private static string ExtractDemoReconciliationVerdict(string markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown))
                return "";

            foreach (string line in markdown.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = line.Trim();
                if (!trimmed.Contains("demo/paper reconciliation", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (trimmed.Contains(DemoPaperReconciliationVerdicts.Matches, StringComparison.OrdinalIgnoreCase))
                    return DemoPaperReconciliationVerdicts.Matches;
                if (trimmed.Contains(DemoPaperReconciliationVerdicts.Diverges, StringComparison.OrdinalIgnoreCase))
                    return DemoPaperReconciliationVerdicts.Diverges;
                if (trimmed.Contains(DemoPaperReconciliationVerdicts.Inconclusive, StringComparison.OrdinalIgnoreCase))
                    return DemoPaperReconciliationVerdicts.Inconclusive;
            }

            return "";
        }

        private static string ResolvePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return "";

            string trimmed = path.Trim();
            return Path.IsPathRooted(trimmed)
                ? trimmed
                : Path.Combine(AppPaths.RootDirectory, trimmed);
        }

        private static string SafeRead(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return "";

            try
            {
                return File.ReadAllText(path);
            }
            catch
            {
                return "";
            }
        }
    }
}
