using System.Text;

namespace MT5TradingBot.Modules.StrategyProof
{
    public static class StrategyEvidenceClassifications
    {
        public const string ProvenPositiveEdge = "Proven positive edge";
        public const string NotProven = "Not proven";
        public const string NegativeEdge = "Negative edge";
        public const string Inconclusive = "Inconclusive";
    }

    public static class StrategyReadinessRecommendations
    {
        public const string ContinuePaperTesting = "continue paper testing";
        public const string ProceedToDemoForwardTest = "proceed to demo forward test";
        public const string ProceedToTinyLiveTest = "proceed to tiny live test";
        public const string BlockLiveTrading = "block live trading";
        public const string CollectMoreData = "collect more data";
    }

    public sealed record FinalStrategyProofCriteria
    {
        public int MinimumCompletedRealisticBacktestTrades { get; init; } = 300;
        public double MinimumProfitFactorAfterCosts { get; init; } = 1.20;
        public double MinimumExpectancyAfterCostsUsd { get; init; } = 0.01;
        public double MaximumDrawdownUsd { get; init; } = double.PositiveInfinity;
        public int MaximumLosingStreak { get; init; } = int.MaxValue;
        public double MaximumCostSensitivityNetProfitDegradationUsd { get; init; } = double.PositiveInfinity;
        public bool RequireAcceptableDemoPaperReconciliation { get; init; } = true;
        public bool CriticalRepaintLookaheadFindingBlocksPositiveClassification { get; init; } = true;
    }

    public sealed record FinalStrategyProofPackageInput
    {
        public string StrategyExtractionMarkdown { get; init; } = "";
        public string RepaintLookaheadAuditMarkdown { get; init; } = "";
        public string RealisticBacktestMarkdown { get; init; } = "";
        public StrategySignalQualityReport SignalQualityReport { get; init; } = new();
        public StrategySegmentAnalysisReport? SegmentAnalysisReport { get; init; }
        public CostSensitivityReport? CostSensitivityReport { get; init; }
        public StrategyRobustnessReport? RobustnessReport { get; init; }
        public AiFilterImpactReport? AiFilterImpactReport { get; init; }
        public DemoPaperReconciliationReport? DemoPaperReconciliationReport { get; init; }
        public StrategyEdgeVerdictReportResult? StrategyEdgeVerdictReport { get; init; }
        public FinalStrategyProofCriteria Criteria { get; init; } = new();
        public IReadOnlyDictionary<string, string> AssumptionsUsed { get; init; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public sealed record FinalStrategyProofPackageResult
    {
        public bool Success { get; init; }
        public string EvidenceClassification { get; init; } = StrategyEvidenceClassifications.Inconclusive;
        public string ReadinessRecommendation { get; init; } = StrategyReadinessRecommendations.CollectMoreData;
        public IReadOnlyList<string> FailedCriteria { get; init; } = [];
        public IReadOnlyList<string> Warnings { get; init; } = [];
        public string ReportPath { get; init; } = "";
        public string Markdown { get; init; } = "";
    }

    public sealed class FinalStrategyProofPackageGenerator
    {
        public const string DefaultReportFileName = "FINAL_STRATEGY_PROOF_PACKAGE.md";
        public const string NotFinancialAdviceWarning = "This is not financial advice.";
        public const string NotLiveProofWarning = "Backtest results are not live proof.";
        public const string LiveBlockedWarning = "Real-money trading should remain blocked unless go criteria are met.";
        public const string AiCautionWarning = "AI confirmation should not be trusted unless measured as improving expectancy.";

        public async Task<FinalStrategyProofPackageResult> GenerateAsync(
            FinalStrategyProofPackageInput input,
            string outputPath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string reportPath = string.IsNullOrWhiteSpace(outputPath)
                ? Path.Combine(Directory.GetCurrentDirectory(), DefaultReportFileName)
                : outputPath;
            var result = Evaluate(input);
            string markdown = BuildMarkdown(input, result, Path.GetFileName(reportPath));

            string? directory = Path.GetDirectoryName(Path.GetFullPath(reportPath));
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            await File.WriteAllTextAsync(reportPath, markdown, cancellationToken)
                .ConfigureAwait(false);

            return result with
            {
                ReportPath = Path.GetFullPath(reportPath),
                Markdown = markdown
            };
        }

        private static FinalStrategyProofPackageResult Evaluate(FinalStrategyProofPackageInput input)
        {
            var failed = new List<string>();
            var warnings = new List<string>
            {
                NotFinancialAdviceWarning,
                NotLiveProofWarning,
                LiveBlockedWarning,
                AiCautionWarning
            };

            var metrics = input.SignalQualityReport.OverallMetrics;
            bool signalQualityAvailable = input.SignalQualityReport.Success;
            bool criticalRepaint = HasCriticalRepaintFinding(input.RepaintLookaheadAuditMarkdown);

            if (!signalQualityAvailable)
            {
                warnings.Add("Signal-quality metrics are unavailable.");
                if (!string.IsNullOrWhiteSpace(input.SignalQualityReport.FailureReason))
                    warnings.Add(input.SignalQualityReport.FailureReason);
            }

            if (metrics.CompletedTrades < input.Criteria.MinimumCompletedRealisticBacktestTrades)
            {
                failed.Add(
                    $"Minimum completed realistic backtest trades not met: {metrics.CompletedTrades} < {input.Criteria.MinimumCompletedRealisticBacktestTrades}.");
            }

            if (metrics.ProfitFactorAfterCosts < input.Criteria.MinimumProfitFactorAfterCosts &&
                !metrics.ProfitFactorAfterCostsUnlimited)
            {
                failed.Add(
                    $"Minimum profit factor after costs not met: {Format(metrics.ProfitFactorAfterCosts)} < {Format(input.Criteria.MinimumProfitFactorAfterCosts)}.");
            }

            if (metrics.ExpectancyAfterCostsUsd < input.Criteria.MinimumExpectancyAfterCostsUsd)
            {
                failed.Add(
                    $"Minimum expectancy after costs not met: {FormatUsd(metrics.ExpectancyAfterCostsUsd)} < {FormatUsd(input.Criteria.MinimumExpectancyAfterCostsUsd)}.");
            }

            if (metrics.MaxDrawdownUsd > input.Criteria.MaximumDrawdownUsd)
            {
                failed.Add(
                    $"Maximum drawdown exceeded: {FormatUsd(metrics.MaxDrawdownUsd)} > {FormatUsd(input.Criteria.MaximumDrawdownUsd)}.");
            }

            if (metrics.WorstLosingStreak > input.Criteria.MaximumLosingStreak)
            {
                failed.Add(
                    $"Maximum losing streak exceeded: {metrics.WorstLosingStreak} > {input.Criteria.MaximumLosingStreak}.");
            }

            AddCostSensitivityCriteria(input, failed, warnings);
            AddDemoReconciliationCriteria(input, failed, warnings);
            AddComponentWarnings(input, warnings);

            if (criticalRepaint && input.Criteria.CriticalRepaintLookaheadFindingBlocksPositiveClassification)
                failed.Add("Critical repaint/lookahead finding blocks positive classification.");

            string classification = Classification(input, failed, criticalRepaint);
            string recommendation = Recommendation(input, classification, failed);

            return new FinalStrategyProofPackageResult
            {
                Success = true,
                EvidenceClassification = classification,
                ReadinessRecommendation = recommendation,
                FailedCriteria = failed.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                Warnings = warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            };
        }

        private static string Classification(
            FinalStrategyProofPackageInput input,
            IReadOnlyList<string> failed,
            bool criticalRepaint)
        {
            var metrics = input.SignalQualityReport.OverallMetrics;
            bool smallSample = metrics.CompletedTrades < input.Criteria.MinimumCompletedRealisticBacktestTrades;
            if (!input.SignalQualityReport.Success || smallSample)
                return StrategyEvidenceClassifications.Inconclusive;

            if (metrics.ExpectancyAfterCostsUsd < 0 ||
                (metrics.ProfitFactorAfterCosts < 1 && !metrics.ProfitFactorAfterCostsUnlimited))
                return StrategyEvidenceClassifications.NegativeEdge;

            if (criticalRepaint && input.Criteria.CriticalRepaintLookaheadFindingBlocksPositiveClassification)
                return StrategyEvidenceClassifications.NotProven;

            if (failed.Count == 0 &&
                input.StrategyEdgeVerdictReport?.Verdict == StrategyEdgeVerdicts.Pass &&
                (!input.Criteria.RequireAcceptableDemoPaperReconciliation ||
                    input.DemoPaperReconciliationReport?.Verdict == DemoPaperReconciliationVerdicts.Matches))
            {
                return StrategyEvidenceClassifications.ProvenPositiveEdge;
            }

            return StrategyEvidenceClassifications.NotProven;
        }

        private static string Recommendation(
            FinalStrategyProofPackageInput input,
            string classification,
            IReadOnlyList<string> failed)
        {
            if (classification == StrategyEvidenceClassifications.ProvenPositiveEdge)
            {
                return input.DemoPaperReconciliationReport?.Verdict == DemoPaperReconciliationVerdicts.Matches
                    ? StrategyReadinessRecommendations.ProceedToTinyLiveTest
                    : StrategyReadinessRecommendations.ProceedToDemoForwardTest;
            }

            if (classification == StrategyEvidenceClassifications.NegativeEdge ||
                failed.Any(f => f.Contains("Critical repaint", StringComparison.OrdinalIgnoreCase)))
            {
                return StrategyReadinessRecommendations.BlockLiveTrading;
            }

            if (classification == StrategyEvidenceClassifications.Inconclusive)
                return StrategyReadinessRecommendations.CollectMoreData;

            return StrategyReadinessRecommendations.ContinuePaperTesting;
        }

        private static void AddCostSensitivityCriteria(
            FinalStrategyProofPackageInput input,
            List<string> failed,
            List<string> warnings)
        {
            if (input.CostSensitivityReport == null || !input.CostSensitivityReport.Success)
            {
                warnings.Add("Cost sensitivity summary is unavailable.");
                return;
            }

            double worstNetChange = input.CostSensitivityReport.ScenarioMetrics.Count == 0
                ? 0
                : input.CostSensitivityReport.ScenarioMetrics.Min(s => s.DegradationFromBase.NetProfitChangeUsd);
            double allowed = Math.Abs(input.Criteria.MaximumCostSensitivityNetProfitDegradationUsd);
            if (worstNetChange < 0 && Math.Abs(worstNetChange) > allowed)
            {
                failed.Add(
                    $"Acceptable cost sensitivity degradation not met: {FormatUsd(worstNetChange)} exceeds {FormatUsd(allowed)}.");
            }
        }

        private static void AddDemoReconciliationCriteria(
            FinalStrategyProofPackageInput input,
            List<string> failed,
            List<string> warnings)
        {
            if (input.DemoPaperReconciliationReport == null)
            {
                warnings.Add("Demo/paper reconciliation summary is unavailable.");
                if (input.Criteria.RequireAcceptableDemoPaperReconciliation)
                    failed.Add("Acceptable demo/paper reconciliation is unavailable.");
                return;
            }

            if (!input.DemoPaperReconciliationReport.Success)
            {
                warnings.Add($"Demo/paper reconciliation failed: {input.DemoPaperReconciliationReport.FailureReason}");
                if (input.Criteria.RequireAcceptableDemoPaperReconciliation)
                    failed.Add("Acceptable demo/paper reconciliation failed.");
                return;
            }

            if (input.Criteria.RequireAcceptableDemoPaperReconciliation &&
                input.DemoPaperReconciliationReport.Verdict != DemoPaperReconciliationVerdicts.Matches)
            {
                failed.Add($"Acceptable demo/paper reconciliation not met: {input.DemoPaperReconciliationReport.Verdict}.");
            }
        }

        private static void AddComponentWarnings(FinalStrategyProofPackageInput input, List<string> warnings)
        {
            if (string.IsNullOrWhiteSpace(input.StrategyExtractionMarkdown))
                warnings.Add("Strategy extraction findings are unavailable.");
            if (string.IsNullOrWhiteSpace(input.RepaintLookaheadAuditMarkdown))
                warnings.Add("Repaint/lookahead audit findings are unavailable.");
            if (string.IsNullOrWhiteSpace(input.RealisticBacktestMarkdown))
                warnings.Add("Realistic backtest report summary is unavailable.");
            if (input.SegmentAnalysisReport == null || !input.SegmentAnalysisReport.Success)
                warnings.Add("Segmented performance summary is unavailable.");
            if (input.RobustnessReport == null || !input.RobustnessReport.Success)
                warnings.Add("Robustness summary is unavailable.");
            if (input.AiFilterImpactReport == null || !input.AiFilterImpactReport.Success)
                warnings.Add("AI filter impact summary is unavailable.");
            if (input.StrategyEdgeVerdictReport == null)
                warnings.Add("Strategy edge verdict summary is unavailable.");

            warnings.AddRange(input.SignalQualityReport.Warnings);
            if (input.SegmentAnalysisReport != null)
                warnings.AddRange(input.SegmentAnalysisReport.Warnings);
            if (input.CostSensitivityReport != null)
                warnings.AddRange(input.CostSensitivityReport.Warnings);
            if (input.RobustnessReport != null)
                warnings.AddRange(input.RobustnessReport.Warnings);
            if (input.AiFilterImpactReport != null)
                warnings.AddRange(input.AiFilterImpactReport.Warnings);
            if (input.DemoPaperReconciliationReport != null)
                warnings.AddRange(input.DemoPaperReconciliationReport.Warnings);
            if (input.StrategyEdgeVerdictReport != null)
                warnings.AddRange(input.StrategyEdgeVerdictReport.Warnings);
        }

        private static string BuildMarkdown(
            FinalStrategyProofPackageInput input,
            FinalStrategyProofPackageResult result,
            string reportFileName)
        {
            var metrics = input.SignalQualityReport.OverallMetrics;
            var bestWorst = BestWorstSegments(input.SegmentAnalysisReport);

            var sb = new StringBuilder();
            sb.AppendLine("# Final Strategy Proof Package");
            sb.AppendLine();
            sb.AppendLine("Scope: P3 final proof reporting only. This package does not change strategy logic, indicators, AI prompts, take-profit logic, live trading behavior, or execution behavior.");
            sb.AppendLine();
            sb.AppendLine("## Executive Classification");
            sb.AppendLine();
            sb.AppendLine($"- Evidence classification: {result.EvidenceClassification}");
            sb.AppendLine($"- Readiness recommendation: {result.ReadinessRecommendation}");
            sb.AppendLine($"- Report file: `{reportFileName}`");
            sb.AppendLine();
            sb.AppendLine("## Required Warnings");
            sb.AppendLine();
            sb.AppendLine($"- {NotFinancialAdviceWarning}");
            sb.AppendLine($"- {NotLiveProofWarning}");
            sb.AppendLine($"- {LiveBlockedWarning}");
            sb.AppendLine($"- {AiCautionWarning}");
            sb.AppendLine();
            sb.AppendLine("## Go/No-Go Criteria");
            sb.AppendLine();
            sb.AppendLine("| Criterion | Required | Observed | Status |");
            sb.AppendLine("|---|---:|---:|---|");
            sb.AppendLine(CriterionRow(
                "Minimum completed realistic backtest trades",
                input.Criteria.MinimumCompletedRealisticBacktestTrades.ToString(),
                metrics.CompletedTrades.ToString(),
                metrics.CompletedTrades >= input.Criteria.MinimumCompletedRealisticBacktestTrades));
            sb.AppendLine(CriterionRow(
                "Minimum profit factor after costs",
                Format(input.Criteria.MinimumProfitFactorAfterCosts),
                Format(metrics.ProfitFactorAfterCosts, metrics.ProfitFactorAfterCostsUnlimited),
                metrics.ProfitFactorAfterCostsUnlimited || metrics.ProfitFactorAfterCosts >= input.Criteria.MinimumProfitFactorAfterCosts));
            sb.AppendLine(CriterionRow(
                "Minimum expectancy after costs",
                FormatUsd(input.Criteria.MinimumExpectancyAfterCostsUsd),
                FormatUsd(metrics.ExpectancyAfterCostsUsd),
                metrics.ExpectancyAfterCostsUsd >= input.Criteria.MinimumExpectancyAfterCostsUsd));
            sb.AppendLine(CriterionRow(
                "Maximum drawdown",
                FormatUsd(input.Criteria.MaximumDrawdownUsd),
                FormatUsd(metrics.MaxDrawdownUsd),
                metrics.MaxDrawdownUsd <= input.Criteria.MaximumDrawdownUsd));
            sb.AppendLine(CriterionRow(
                "Maximum losing streak",
                input.Criteria.MaximumLosingStreak.ToString(),
                metrics.WorstLosingStreak.ToString(),
                metrics.WorstLosingStreak <= input.Criteria.MaximumLosingStreak));
            sb.AppendLine(CriterionRow(
                "Acceptable cost sensitivity degradation",
                FormatUsd(input.Criteria.MaximumCostSensitivityNetProfitDegradationUsd),
                CostSensitivityObserved(input.CostSensitivityReport),
                CostSensitivityPasses(input)));
            sb.AppendLine(CriterionRow(
                "Acceptable demo/paper reconciliation",
                DemoPaperReconciliationVerdicts.Matches,
                input.DemoPaperReconciliationReport?.Verdict ?? "Missing",
                !input.Criteria.RequireAcceptableDemoPaperReconciliation ||
                    input.DemoPaperReconciliationReport?.Verdict == DemoPaperReconciliationVerdicts.Matches));
            sb.AppendLine(CriterionRow(
                "No critical repaint/lookahead findings",
                "No Critical",
                HasCriticalRepaintFinding(input.RepaintLookaheadAuditMarkdown) ? "Critical present" : "No Critical found",
                !HasCriticalRepaintFinding(input.RepaintLookaheadAuditMarkdown)));
            sb.AppendLine();
            sb.AppendLine("## Evidence Summaries");
            sb.AppendLine();
            sb.AppendLine($"- Strategy extraction findings: {ExtractionSummary(input.StrategyExtractionMarkdown)}");
            sb.AppendLine($"- Repaint/lookahead audit findings: {RepaintSummary(input.RepaintLookaheadAuditMarkdown)}");
            sb.AppendLine($"- Realistic backtest result summary: {RealisticBacktestSummary(input.RealisticBacktestMarkdown)}");
            sb.AppendLine($"- Signal quality metrics: {SignalQualitySummary(metrics)}");
            sb.AppendLine($"- Segmented performance summary: best {bestWorst.Best}; worst {bestWorst.Worst}.");
            sb.AppendLine($"- Cost sensitivity summary: {CostSensitivitySummary(input.CostSensitivityReport)}");
            sb.AppendLine($"- Robustness summary: {RobustnessSummary(input.RobustnessReport)}");
            sb.AppendLine($"- AI filter impact summary: {AiSummary(input.AiFilterImpactReport)}");
            sb.AppendLine($"- Demo/paper reconciliation summary: {DemoSummary(input.DemoPaperReconciliationReport)}");
            sb.AppendLine($"- Strategy edge verdict: {input.StrategyEdgeVerdictReport?.Verdict ?? StrategyEdgeVerdicts.Inconclusive}.");
            sb.AppendLine($"- Live-demo readiness recommendation: {result.ReadinessRecommendation}.");
            sb.AppendLine();
            sb.AppendLine("## Failed Criteria");
            sb.AppendLine();
            if (result.FailedCriteria.Count == 0)
                sb.AppendLine("- None.");
            else
                foreach (var item in result.FailedCriteria)
                    sb.AppendLine($"- {item}");
            sb.AppendLine();
            sb.AppendLine("## Warnings");
            sb.AppendLine();
            foreach (var warning in result.Warnings)
                sb.AppendLine($"- {warning}");
            sb.AppendLine();
            sb.AppendLine("## Assumptions");
            sb.AppendLine();
            if (input.AssumptionsUsed.Count == 0)
                sb.AppendLine("- No top-level package assumptions were supplied.");
            else
                foreach (var item in input.AssumptionsUsed.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
                    sb.AppendLine($"- {item.Key}: {item.Value}");
            return sb.ToString();
        }

        private static string CriterionRow(string name, string required, string observed, bool pass) =>
            $"| {Escape(name)} | {Escape(required)} | {Escape(observed)} | {(pass ? "Go" : "No-go")} |";

        private static string ExtractionSummary(string markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown))
                return "Not verified.";
            if (markdown.Contains("mostly HOLD", StringComparison.OrdinalIgnoreCase))
                return "Base deterministic strategy is documented as mostly HOLD; Buy/Sell depends on auto-scalping, AI, or manual paths.";
            return "Strategy extraction report is available.";
        }

        private static string RepaintSummary(string markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown))
                return "Not verified.";
            return HasCriticalRepaintFinding(markdown)
                ? "Critical audit finding is present; positive classification is blocked."
                : "No Critical severity text found in supplied audit summary.";
        }

        private static string RealisticBacktestSummary(string markdown) =>
            string.IsNullOrWhiteSpace(markdown)
                ? "REALISTIC_BACKTEST_REPORT.md is unavailable."
                : "Realistic backtest report text is available.";

        private static string SignalQualitySummary(StrategySignalQualitySummary metrics) =>
            $"completed {metrics.CompletedTrades}, PF {Format(metrics.ProfitFactorAfterCosts, metrics.ProfitFactorAfterCostsUnlimited)}, expectancy {FormatUsd(metrics.ExpectancyAfterCostsUsd)}, drawdown {FormatUsd(metrics.MaxDrawdownUsd)}, losing streak {metrics.WorstLosingStreak}.";

        private static string CostSensitivitySummary(CostSensitivityReport? report)
        {
            if (report == null || !report.Success)
                return "Unavailable.";
            return $"base expectancy {FormatUsd(report.BaseMetrics.ExpectancyUsd)}, worst net degradation {CostSensitivityObserved(report)}.";
        }

        private static string RobustnessSummary(StrategyRobustnessReport? report)
        {
            if (report == null || !report.Success)
                return "Unavailable.";
            return $"{report.Verdict}; OOS expectancy {FormatUsd(report.OutOfSampleMetrics.ExpectancyUsd)}.";
        }

        private static string AiSummary(AiFilterImpactReport? report)
        {
            if (report == null || !report.Success)
                return "Unavailable.";
            return $"{report.Verdict}; AI vs non-AI expectancy delta {FormatUsd(report.OverallComparison.ExpectancyDeltaAiVsNonAiUsd)}.";
        }

        private static string DemoSummary(DemoPaperReconciliationReport? report)
        {
            if (report == null || !report.Success)
                return "Unavailable.";
            return $"{report.Verdict}; expectancy delta {FormatUsd(report.Deltas.ExpectancyChangeUsd)}.";
        }

        private static (string Best, string Worst) BestWorstSegments(StrategySegmentAnalysisReport? report)
        {
            if (report == null || !report.Success)
                return ("Not verified", "Not verified");

            var segments = report.SegmentGroups
                .SelectMany(g => g.Segments.Select(s => new { Group = g.Name, Segment = s }))
                .Where(x => x.Segment.TotalTrades > 0)
                .ToList();
            if (segments.Count == 0)
                return ("Not verified", "Not verified");

            var best = segments
                .OrderByDescending(x => x.Segment.ExpectancyUsd)
                .ThenByDescending(x => x.Segment.NetProfitUsd)
                .First();
            var worst = segments
                .OrderBy(x => x.Segment.ExpectancyUsd)
                .ThenBy(x => x.Segment.NetProfitUsd)
                .First();

            return (
                $"{best.Group}={best.Segment.Key} expectancy {FormatUsd(best.Segment.ExpectancyUsd)}",
                $"{worst.Group}={worst.Segment.Key} expectancy {FormatUsd(worst.Segment.ExpectancyUsd)}");
        }

        private static string CostSensitivityObserved(CostSensitivityReport? report)
        {
            if (report == null || !report.Success)
                return "Missing";
            double worst = report.ScenarioMetrics.Count == 0
                ? 0
                : report.ScenarioMetrics.Min(s => s.DegradationFromBase.NetProfitChangeUsd);
            return FormatUsd(worst);
        }

        private static bool CostSensitivityPasses(FinalStrategyProofPackageInput input)
        {
            if (input.CostSensitivityReport == null || !input.CostSensitivityReport.Success)
                return false;
            double worst = input.CostSensitivityReport.ScenarioMetrics.Count == 0
                ? 0
                : input.CostSensitivityReport.ScenarioMetrics.Min(s => s.DegradationFromBase.NetProfitChangeUsd);
            return !(worst < 0 && Math.Abs(worst) > Math.Abs(input.Criteria.MaximumCostSensitivityNetProfitDegradationUsd));
        }

        private static bool HasCriticalRepaintFinding(string markdown) =>
            !string.IsNullOrWhiteSpace(markdown) &&
            markdown
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(line => line.Contains("| Critical |", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("Severity: Critical", StringComparison.OrdinalIgnoreCase));

        private static string Format(double value, bool unlimited = false)
        {
            if (unlimited || double.IsPositiveInfinity(value))
                return "Unlimited";
            if (double.IsNegativeInfinity(value))
                return "-Infinity";
            return value.ToString("0.##");
        }

        private static string FormatUsd(double value) =>
            double.IsInfinity(value)
                ? value.ToString()
                : $"${value:0.##}";

        private static string Escape(string value) =>
            value.Replace("|", "\\|", StringComparison.Ordinal);
    }
}
