using System.Text;

namespace MT5TradingBot.Modules.StrategyProof
{
    public static class StrategyEdgeVerdicts
    {
        public const string Pass = "Pass";
        public const string Fail = "Fail";
        public const string Inconclusive = "Inconclusive";
    }

    public sealed record StrategyEdgeVerdictCriteria
    {
        public int MinimumCompletedTrades { get; init; } = 300;
        public double MinimumProfitFactorAfterCosts { get; init; } = 1.20;
        public double MinimumExpectancyAfterCostsUsd { get; init; } = 0.01;
        public double MaximumDrawdownUsd { get; init; } = double.PositiveInfinity;
        public int MaximumLosingStreak { get; init; } = int.MaxValue;
        public double MaximumCostSensitivityNetProfitDegradationUsd { get; init; } = double.PositiveInfinity;
        public bool RobustnessMustPassOrBeInconclusive { get; init; } = true;
        public bool CriticalRepaintLookaheadFindingFails { get; init; } = true;
    }

    public sealed record StrategyEdgeVerdictReportInput
    {
        public StrategySignalQualityReport SignalQualityReport { get; init; } = new();
        public StrategySegmentAnalysisReport? SegmentAnalysisReport { get; init; }
        public CostSensitivityReport? CostSensitivityReport { get; init; }
        public StrategyRobustnessReport? RobustnessReport { get; init; }
        public AiFilterImpactReport? AiFilterImpactReport { get; init; }
        public string StrategyExtractionMarkdown { get; init; } = "";
        public string RepaintLookaheadAuditMarkdown { get; init; } = "";
        public StrategyEdgeVerdictCriteria Criteria { get; init; } = new();
        public IReadOnlyDictionary<string, string> AssumptionsUsed { get; init; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public sealed record StrategyEdgeVerdictReportResult
    {
        public bool Success { get; init; }
        public string Verdict { get; init; } = StrategyEdgeVerdicts.Inconclusive;
        public string Reason { get; init; } = "";
        public IReadOnlyList<string> FailedCriteria { get; init; } = [];
        public IReadOnlyList<string> Warnings { get; init; } = [];
        public string ReportPath { get; init; } = "";
        public string Markdown { get; init; } = "";
        public int LiveDemoReadinessScore { get; init; }
    }

    public sealed class StrategyEdgeVerdictReportBuilder
    {
        public const string DefaultReportFileName = "STRATEGY_EDGE_VERDICT_REPORT.md";
        public const string NotLiveProofWarning = "Backtest edge is not live proof.";
        public const string DemoRequiredWarning = "Live demo/paper validation is still required.";
        public const string AiCautionWarning = "AI should not be trusted unless AI impact analysis shows improvement.";

        public async Task<StrategyEdgeVerdictReportResult> GenerateAsync(
            StrategyEdgeVerdictReportInput input,
            string outputPath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string reportPath = string.IsNullOrWhiteSpace(outputPath)
                ? Path.Combine(Directory.GetCurrentDirectory(), DefaultReportFileName)
                : outputPath;

            var evaluation = Evaluate(input);
            string markdown = BuildMarkdown(input, evaluation, Path.GetFileName(reportPath));

            string? directory = Path.GetDirectoryName(Path.GetFullPath(reportPath));
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            await File.WriteAllTextAsync(reportPath, markdown, cancellationToken)
                .ConfigureAwait(false);

            return evaluation with
            {
                ReportPath = Path.GetFullPath(reportPath),
                Markdown = markdown
            };
        }

        private static StrategyEdgeVerdictReportResult Evaluate(StrategyEdgeVerdictReportInput input)
        {
            var warnings = new List<string>
            {
                NotLiveProofWarning,
                DemoRequiredWarning,
                AiCautionWarning
            };
            var failed = new List<string>();
            var inconclusive = new List<string>();
            var metrics = input.SignalQualityReport.OverallMetrics;

            if (!input.SignalQualityReport.Success)
            {
                inconclusive.Add("Signal-quality metrics are unavailable.");
                if (!string.IsNullOrWhiteSpace(input.SignalQualityReport.FailureReason))
                    warnings.Add(input.SignalQualityReport.FailureReason);
            }

            if (metrics.CompletedTrades < input.Criteria.MinimumCompletedTrades)
            {
                inconclusive.Add(
                    $"Completed trade sample is too small: {metrics.CompletedTrades} < required {input.Criteria.MinimumCompletedTrades}.");
            }

            if (metrics.ProfitFactorAfterCosts < input.Criteria.MinimumProfitFactorAfterCosts &&
                !metrics.ProfitFactorAfterCostsUnlimited)
            {
                failed.Add(
                    $"Profit factor after costs {Format(metrics.ProfitFactorAfterCosts)} is below required {Format(input.Criteria.MinimumProfitFactorAfterCosts)}.");
            }

            if (metrics.ExpectancyAfterCostsUsd < input.Criteria.MinimumExpectancyAfterCostsUsd)
            {
                failed.Add(
                    $"Expectancy after costs {FormatUsd(metrics.ExpectancyAfterCostsUsd)} is below required {FormatUsd(input.Criteria.MinimumExpectancyAfterCostsUsd)}.");
            }

            if (metrics.MaxDrawdownUsd > input.Criteria.MaximumDrawdownUsd)
            {
                failed.Add(
                    $"Max drawdown {FormatUsd(metrics.MaxDrawdownUsd)} exceeds allowed {FormatUsd(input.Criteria.MaximumDrawdownUsd)}.");
            }

            if (metrics.WorstLosingStreak > input.Criteria.MaximumLosingStreak)
            {
                failed.Add(
                    $"Worst losing streak {metrics.WorstLosingStreak} exceeds allowed {input.Criteria.MaximumLosingStreak}.");
            }

            AddCostSensitivityVerdict(input, failed, warnings);
            AddRobustnessVerdict(input, failed, warnings);
            AddAiVerdict(input, warnings);
            AddExtractionAndAuditEvidence(input, failed, warnings);

            string verdict = failed.Count > 0
                ? StrategyEdgeVerdicts.Fail
                : inconclusive.Count > 0
                    ? StrategyEdgeVerdicts.Inconclusive
                    : StrategyEdgeVerdicts.Pass;

            warnings.AddRange(inconclusive);
            warnings.AddRange(input.SignalQualityReport.Warnings);
            if (input.SegmentAnalysisReport != null)
                warnings.AddRange(input.SegmentAnalysisReport.Warnings);
            if (input.CostSensitivityReport != null)
                warnings.AddRange(input.CostSensitivityReport.Warnings);
            if (input.RobustnessReport != null)
                warnings.AddRange(input.RobustnessReport.Warnings);
            if (input.AiFilterImpactReport != null)
                warnings.AddRange(input.AiFilterImpactReport.Warnings);

            string reason = Reason(verdict, failed, inconclusive, metrics);
            int readiness = ReadinessScore(verdict, failed.Count, warnings, input);

            return new StrategyEdgeVerdictReportResult
            {
                Success = input.SignalQualityReport.Success,
                Verdict = verdict,
                Reason = reason,
                FailedCriteria = failed.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                Warnings = warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                LiveDemoReadinessScore = readiness
            };
        }

        private static void AddCostSensitivityVerdict(
            StrategyEdgeVerdictReportInput input,
            List<string> failed,
            List<string> warnings)
        {
            if (input.CostSensitivityReport == null)
            {
                warnings.Add("Cost sensitivity analysis is missing.");
                return;
            }

            if (!input.CostSensitivityReport.Success)
            {
                warnings.Add($"Cost sensitivity analysis is unavailable: {input.CostSensitivityReport.FailureReason}");
                return;
            }

            double worstNetProfitChange = input.CostSensitivityReport.ScenarioMetrics.Count == 0
                ? 0
                : input.CostSensitivityReport.ScenarioMetrics.Min(s => s.DegradationFromBase.NetProfitChangeUsd);
            double allowed = Math.Abs(input.Criteria.MaximumCostSensitivityNetProfitDegradationUsd);
            if (Math.Abs(worstNetProfitChange) > allowed && worstNetProfitChange < 0)
            {
                failed.Add(
                    $"Cost sensitivity degradation {FormatUsd(worstNetProfitChange)} exceeds allowed {FormatUsd(allowed)}.");
            }
        }

        private static void AddRobustnessVerdict(
            StrategyEdgeVerdictReportInput input,
            List<string> failed,
            List<string> warnings)
        {
            if (input.RobustnessReport == null)
            {
                warnings.Add("Strategy robustness analysis is missing.");
                return;
            }

            if (!input.RobustnessReport.Success)
            {
                warnings.Add($"Strategy robustness analysis is unavailable: {input.RobustnessReport.FailureReason}");
                return;
            }

            if (input.RobustnessReport.Verdict == StrategyRobustnessVerdicts.Fail &&
                input.Criteria.RobustnessMustPassOrBeInconclusive)
            {
                failed.Add("Strategy robustness verdict is Fail.");
            }

            failed.AddRange(input.RobustnessReport.FailedCriteria.Select(c => $"Robustness: {c}"));
        }

        private static void AddAiVerdict(StrategyEdgeVerdictReportInput input, List<string> warnings)
        {
            if (input.AiFilterImpactReport == null)
            {
                warnings.Add("AI filter impact analysis is missing; AI should remain outside edge proof.");
                return;
            }

            if (!input.AiFilterImpactReport.Success)
            {
                warnings.Add($"AI filter impact analysis is unavailable: {input.AiFilterImpactReport.FailureReason}");
                return;
            }

            if (input.AiFilterImpactReport.Verdict != AiFilterImpactVerdicts.Improves)
                warnings.Add("AI filter impact does not show improvement; do not treat AI confirmation as proven edge.");
        }

        private static void AddExtractionAndAuditEvidence(
            StrategyEdgeVerdictReportInput input,
            List<string> failed,
            List<string> warnings)
        {
            if (string.IsNullOrWhiteSpace(input.StrategyExtractionMarkdown))
                warnings.Add("Strategy extraction summary is missing.");

            bool criticalLookahead = HasCriticalRepaintFinding(input.RepaintLookaheadAuditMarkdown);
            if (string.IsNullOrWhiteSpace(input.RepaintLookaheadAuditMarkdown))
            {
                warnings.Add("Repaint/lookahead audit summary is missing.");
            }
            else if (criticalLookahead && input.Criteria.CriticalRepaintLookaheadFindingFails)
            {
                failed.Add("Critical repaint/lookahead audit finding is present.");
            }
            else if (criticalLookahead)
            {
                warnings.Add("Critical repaint/lookahead audit finding is present.");
            }
        }

        private static string Reason(
            string verdict,
            IReadOnlyList<string> failed,
            IReadOnlyList<string> inconclusive,
            StrategySignalQualitySummary metrics) =>
            verdict switch
            {
                StrategyEdgeVerdicts.Fail => $"Failed objective criteria: {string.Join(" ", failed)}",
                StrategyEdgeVerdicts.Inconclusive => $"Evidence is incomplete: {string.Join(" ", inconclusive)}",
                _ => $"Completed sample passed configured criteria with profit factor {Format(metrics.ProfitFactorAfterCosts, metrics.ProfitFactorAfterCostsUnlimited)} and expectancy {FormatUsd(metrics.ExpectancyAfterCostsUsd)}."
            };

        private static int ReadinessScore(
            string verdict,
            int failedCriteriaCount,
            IReadOnlyList<string> warnings,
            StrategyEdgeVerdictReportInput input)
        {
            int score = 100;
            if (verdict == StrategyEdgeVerdicts.Fail)
                score -= 40;
            if (verdict == StrategyEdgeVerdicts.Inconclusive)
                score -= 25;

            score -= Math.Min(30, failedCriteriaCount * 10);
            score -= Math.Min(20, warnings.Count(w => !IsAlwaysRequiredWarning(w)) * 3);

            if (input.AiFilterImpactReport == null ||
                input.AiFilterImpactReport.Verdict != AiFilterImpactVerdicts.Improves)
                score -= 10;
            if (HasCriticalRepaintFinding(input.RepaintLookaheadAuditMarkdown))
                score -= 20;

            return Math.Clamp(score, 0, 100);
        }

        private static string BuildMarkdown(
            StrategyEdgeVerdictReportInput input,
            StrategyEdgeVerdictReportResult evaluation,
            string reportFileName)
        {
            var metrics = input.SignalQualityReport.OverallMetrics;
            var costVerdict = CostSensitivityVerdict(input);
            var bestWorst = BestWorstSegments(input.SegmentAnalysisReport);

            var sb = new StringBuilder();
            sb.AppendLine("# Strategy Edge Verdict Report");
            sb.AppendLine();
            sb.AppendLine("Scope: P3 strategy proof reporting only. This report does not change strategy logic, indicators, AI prompts, take-profit logic, live trading behavior, or execution behavior.");
            sb.AppendLine();
            sb.AppendLine("## Executive Verdict");
            sb.AppendLine();
            sb.AppendLine($"- Verdict: {evaluation.Verdict}");
            sb.AppendLine($"- Reason for verdict: {evaluation.Reason}");
            sb.AppendLine($"- Report file: `{reportFileName}`");
            sb.AppendLine($"- Live-demo readiness score: {evaluation.LiveDemoReadinessScore}/100");
            sb.AppendLine();
            sb.AppendLine("## Required Proof Warnings");
            sb.AppendLine();
            sb.AppendLine($"- {NotLiveProofWarning}");
            sb.AppendLine($"- {DemoRequiredWarning}");
            sb.AppendLine($"- {AiCautionWarning}");
            sb.AppendLine();
            sb.AppendLine("## Core Metrics");
            sb.AppendLine();
            sb.AppendLine("| Metric | Value |");
            sb.AppendLine("|---|---:|");
            sb.AppendLine($"| Sample size / completed trades | {metrics.CompletedTrades} |");
            sb.AppendLine($"| Total signals | {metrics.TotalSignals} |");
            sb.AppendLine($"| Profit factor after costs | {Format(metrics.ProfitFactorAfterCosts, metrics.ProfitFactorAfterCostsUnlimited)} |");
            sb.AppendLine($"| Expectancy after costs | {FormatUsd(metrics.ExpectancyAfterCostsUsd)} |");
            sb.AppendLine($"| Max drawdown | {FormatUsd(metrics.MaxDrawdownUsd)} |");
            sb.AppendLine($"| Worst losing streak | {metrics.WorstLosingStreak} |");
            sb.AppendLine();
            sb.AppendLine("## Component Verdicts");
            sb.AppendLine();
            sb.AppendLine("| Component | Verdict | Notes |");
            sb.AppendLine("|---|---|---|");
            sb.AppendLine($"| Cost sensitivity verdict | {costVerdict.Verdict} | {Escape(costVerdict.Notes)} |");
            sb.AppendLine($"| Robustness verdict | {RobustnessVerdict(input.RobustnessReport)} | {Escape(RobustnessNotes(input.RobustnessReport))} |");
            sb.AppendLine($"| AI filter verdict | {AiVerdict(input.AiFilterImpactReport)} | {Escape(AiNotes(input.AiFilterImpactReport))} |");
            sb.AppendLine($"| Repaint/lookahead audit summary | {RepaintVerdict(input)} | {Escape(RepaintNotes(input))} |");
            sb.AppendLine($"| Strategy extraction summary | {ExtractionVerdict(input.StrategyExtractionMarkdown)} | {Escape(ExtractionNotes(input.StrategyExtractionMarkdown))} |");
            sb.AppendLine();
            sb.AppendLine("## Best/Worst Segments");
            sb.AppendLine();
            sb.AppendLine($"- Best segment: {bestWorst.Best}");
            sb.AppendLine($"- Worst segment: {bestWorst.Worst}");
            sb.AppendLine();
            sb.AppendLine("## Failed Criteria");
            sb.AppendLine();
            if (evaluation.FailedCriteria.Count == 0)
                sb.AppendLine("- None.");
            else
                foreach (var item in evaluation.FailedCriteria)
                    sb.AppendLine($"- {item}");
            sb.AppendLine();
            sb.AppendLine("## Key Risks");
            sb.AppendLine();
            foreach (var warning in evaluation.Warnings.Where(w => !IsAlwaysRequiredWarning(w)))
                sb.AppendLine($"- {warning}");
            sb.AppendLine("- Backtest-positive results can still fail in demo due to broker spread, slippage, commission, latency, rejection rate, and execution path differences.");
            sb.AppendLine();
            sb.AppendLine("## Missing Evidence");
            sb.AppendLine();
            foreach (var item in MissingEvidence(input))
                sb.AppendLine($"- {item}");
            sb.AppendLine();
            sb.AppendLine("## Assumptions");
            sb.AppendLine();
            if (input.AssumptionsUsed.Count == 0)
                sb.AppendLine("- No top-level strategy verdict assumptions were supplied.");
            else
                foreach (var item in input.AssumptionsUsed.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
                    sb.AppendLine($"- {item.Key}: {item.Value}");
            return sb.ToString();
        }

        private static (string Verdict, string Notes) CostSensitivityVerdict(StrategyEdgeVerdictReportInput input)
        {
            if (input.CostSensitivityReport == null)
                return (StrategyEdgeVerdicts.Inconclusive, "Cost sensitivity analysis is missing.");
            if (!input.CostSensitivityReport.Success)
                return (StrategyEdgeVerdicts.Inconclusive, input.CostSensitivityReport.FailureReason);

            double worst = input.CostSensitivityReport.ScenarioMetrics.Count == 0
                ? 0
                : input.CostSensitivityReport.ScenarioMetrics.Min(s => s.DegradationFromBase.NetProfitChangeUsd);
            double allowed = Math.Abs(input.Criteria.MaximumCostSensitivityNetProfitDegradationUsd);
            string verdict = worst < 0 && Math.Abs(worst) > allowed
                ? StrategyEdgeVerdicts.Fail
                : StrategyEdgeVerdicts.Pass;
            return (verdict, $"Worst net-profit degradation from base: {FormatUsd(worst)}.");
        }

        private static string RobustnessVerdict(StrategyRobustnessReport? report) =>
            report == null || !report.Success
                ? StrategyEdgeVerdicts.Inconclusive
                : report.Verdict;

        private static string RobustnessNotes(StrategyRobustnessReport? report)
        {
            if (report == null)
                return "Strategy robustness analysis is missing.";
            if (!report.Success)
                return report.FailureReason;
            if (report.FailedCriteria.Count > 0)
                return string.Join(" ", report.FailedCriteria);
            return $"OOS expectancy {FormatUsd(report.OutOfSampleMetrics.ExpectancyUsd)}, Monte Carlo max drawdown {FormatUsd(report.MonteCarloSummary.MaxDrawdown.Max)}.";
        }

        private static string AiVerdict(AiFilterImpactReport? report) =>
            report == null || !report.Success
                ? AiFilterImpactVerdicts.Inconclusive
                : report.Verdict;

        private static string AiNotes(AiFilterImpactReport? report)
        {
            if (report == null)
                return "AI filter impact analysis is missing.";
            if (!report.Success)
                return report.FailureReason;
            return $"AI vs non-AI expectancy delta: {FormatUsd(report.OverallComparison.ExpectancyDeltaAiVsNonAiUsd)}.";
        }

        private static string RepaintVerdict(StrategyEdgeVerdictReportInput input)
        {
            if (string.IsNullOrWhiteSpace(input.RepaintLookaheadAuditMarkdown))
                return StrategyEdgeVerdicts.Inconclusive;
            return HasCriticalRepaintFinding(input.RepaintLookaheadAuditMarkdown) &&
                input.Criteria.CriticalRepaintLookaheadFindingFails
                    ? StrategyEdgeVerdicts.Fail
                    : "Warn";
        }

        private static string RepaintNotes(StrategyEdgeVerdictReportInput input)
        {
            if (string.IsNullOrWhiteSpace(input.RepaintLookaheadAuditMarkdown))
                return "Repaint/lookahead audit summary is missing.";
            return HasCriticalRepaintFinding(input.RepaintLookaheadAuditMarkdown)
                ? "Critical repaint/lookahead audit finding is present."
                : "No Critical severity text was found in the supplied audit summary.";
        }

        private static string ExtractionVerdict(string markdown) =>
            string.IsNullOrWhiteSpace(markdown) ? StrategyEdgeVerdicts.Inconclusive : "Available";

        private static string ExtractionNotes(string markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown))
                return "Strategy extraction summary is missing.";
            if (markdown.Contains("mostly HOLD", StringComparison.OrdinalIgnoreCase))
                return "Extraction summary says the base strategy itself produces mostly HOLD.";
            return "Extraction summary is available.";
        }

        private static (string Best, string Worst) BestWorstSegments(StrategySegmentAnalysisReport? report)
        {
            if (report == null || !report.Success || report.SegmentGroups.Count == 0)
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
                $"{best.Group}={best.Segment.Key} expectancy {FormatUsd(best.Segment.ExpectancyUsd)}, net {FormatUsd(best.Segment.NetProfitUsd)}",
                $"{worst.Group}={worst.Segment.Key} expectancy {FormatUsd(worst.Segment.ExpectancyUsd)}, net {FormatUsd(worst.Segment.NetProfitUsd)}");
        }

        private static IReadOnlyList<string> MissingEvidence(StrategyEdgeVerdictReportInput input)
        {
            var missing = new List<string>();
            if (input.SegmentAnalysisReport == null || !input.SegmentAnalysisReport.Success)
                missing.Add("Segmented performance analysis is unavailable or failed.");
            if (input.CostSensitivityReport == null || !input.CostSensitivityReport.Success)
                missing.Add("Cost sensitivity analysis is unavailable or failed.");
            if (input.RobustnessReport == null || !input.RobustnessReport.Success)
                missing.Add("Strategy robustness analysis is unavailable or failed.");
            if (input.AiFilterImpactReport == null || !input.AiFilterImpactReport.Success)
                missing.Add("AI filter impact analysis is unavailable or failed.");
            if (string.IsNullOrWhiteSpace(input.StrategyExtractionMarkdown))
                missing.Add("Strategy extraction report summary is unavailable.");
            if (string.IsNullOrWhiteSpace(input.RepaintLookaheadAuditMarkdown))
                missing.Add("Repaint/lookahead audit summary is unavailable.");
            if (missing.Count == 0)
                missing.Add("None from supplied P3 analytics inputs.");
            return missing;
        }

        private static bool HasCriticalRepaintFinding(string markdown) =>
            !string.IsNullOrWhiteSpace(markdown) &&
            markdown
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(line => line.Contains("| Critical |", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("Severity: Critical", StringComparison.OrdinalIgnoreCase));

        private static bool IsAlwaysRequiredWarning(string warning) =>
            warning.Equals(NotLiveProofWarning, StringComparison.OrdinalIgnoreCase) ||
            warning.Equals(DemoRequiredWarning, StringComparison.OrdinalIgnoreCase) ||
            warning.Equals(AiCautionWarning, StringComparison.OrdinalIgnoreCase);

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
