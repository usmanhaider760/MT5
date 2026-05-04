using System.Globalization;
using System.Text;
using MT5TradingBot.Models;
using MT5TradingBot.Modules.Backtesting;
using MT5TradingBot.Modules.StrategyProof;

namespace MT5TradingBot.Modules.LiveReadiness
{
    public sealed record EvidencePackageCommandRequest
    {
        public string OutputDirectory { get; init; } = "";
        public string? TickCsvPath { get; init; }
        public string? OhlcCsvPath { get; init; }
        public bool UseSampleFixture { get; init; }
    }

    public sealed record EvidencePackageCommandResult
    {
        public IReadOnlyList<string> GeneratedReports { get; init; } = [];
        public bool UsedRealMarketData { get; init; }
        public bool UsedSampleFixture { get; init; }
        public string MarketDataSource { get; init; } = "";
        public int TicksLoaded { get; init; }
        public int CandlesLoaded { get; init; }
        public int CandidatesGenerated { get; init; }
        public int SkippedOrHoldSignals { get; init; }
        public int IncompleteSignals { get; init; }
        public string CandidateGenerationDiagnostic { get; init; } = "";
        public string StrategyEvidenceClassification { get; init; } = "";
        public FinalGoNoGoDecision GoNoGoDecision { get; init; }
        public IReadOnlyList<string> MissingInputs { get; init; } = [];
        public IReadOnlyList<string> RerunCommands { get; init; } = [];
        public string OutputDirectory { get; init; } = "";
    }

    public sealed class EvidencePackageCommand
    {
        public async Task<EvidencePackageCommandResult> RunAsync(
            EvidencePackageCommandRequest request,
            CancellationToken cancellationToken = default)
        {
            string outputDirectory = string.IsNullOrWhiteSpace(request.OutputDirectory)
                ? Directory.GetCurrentDirectory()
                : request.OutputDirectory;
            Directory.CreateDirectory(outputDirectory);

            bool hasRealMarketData =
                ExistingFile(request.TickCsvPath) ||
                ExistingFile(request.OhlcCsvPath);
            bool useSampleFixture = request.UseSampleFixture || !hasRealMarketData;
            string marketDataSource = useSampleFixture
                ? "Built-in sample/test fixture data"
                : "Configured CSV market data";

            var generated = new List<string>();
            var ticks = ExistingFile(request.TickCsvPath)
                ? await new CsvBacktestTickDataLoader()
                    .LoadAsync(request.TickCsvPath!, cancellationToken: cancellationToken)
                    .ConfigureAwait(false)
                : [];
            var candles = ExistingFile(request.OhlcCsvPath)
                ? await new CsvBacktestOhlcDataLoader()
                    .LoadAsync(request.OhlcCsvPath!, cancellationToken: cancellationToken)
                    .ConfigureAwait(false)
                : [];

            EvidenceStrategyCandidateGenerationResult generation = new();
            RealisticBacktestReportRequest realisticRequest;
            if (useSampleFixture)
            {
                realisticRequest = RealisticBacktestReportCommand.CreateMinimalExample(
                    Path.Combine(outputDirectory, RealisticBacktestReportCommand.DefaultReportFileName));
                realisticRequest = realisticRequest with
                {
                    AssumptionsUsed = WithEvidenceAssumptions(
                        realisticRequest.AssumptionsUsed,
                        hasRealMarketData,
                        useSampleFixture,
                        realisticRequest.Ticks.Count,
                        realisticRequest.Candles.Count,
                        realisticRequest.Candidates.Count,
                        0,
                        0,
                        request.UseSampleFixture
                            ? "Explicit --use-sample-fixture flag selected built-in example candidates."
                            : "No CSV market data was provided; built-in example candidates were used.",
                        "built-in-minimal-fixture",
                        "AI disabled: sample fixture does not call external AI APIs.",
                        "Sample fixture is not a live or historical strategy replay.")
                };
            }
            else
            {
                generation = new EvidenceStrategyCandidateGenerator().Generate(ticks, candles);
                realisticRequest = new RealisticBacktestReportRequest
                {
                    OutputPath = Path.Combine(outputDirectory, RealisticBacktestReportCommand.DefaultReportFileName),
                    Ticks = ticks,
                    Candles = candles,
                    Candidates = generation.Candidates,
                    SymbolInfoBySymbol = BuildSymbolInfo(ticks, candles),
                    AssumptionsUsed = WithEvidenceAssumptions(
                        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                        hasRealMarketData,
                        useSampleFixture,
                        ticks.Count,
                        candles.Count,
                        generation.Candidates.Count,
                        generation.SkippedOrHoldSignals,
                        generation.IncompleteSignals,
                        generation.DiagnosticCode,
                        generation.CandidateGenerationSource,
                        generation.AiDisabledReason,
                        generation.OfflineLiveDifferences)
                };
            }

            var realistic = await new RealisticBacktestReportCommand()
                .RunAsync(realisticRequest, cancellationToken)
                .ConfigureAwait(false);
            generated.Add(realistic.OutputPath);

            var signalQuality = StrategySignalQualityMetrics.BuildReport(
                realistic.BacktestResult,
                assumptionsUsed: realistic.BacktestResult.AssumptionsUsed);

            var extraction = await new StrategyExtractionReportGenerator()
                .GenerateAsync(outputDirectory, Path.Combine(outputDirectory, StrategyExtractionReportGenerator.DefaultReportFileName), cancellationToken)
                .ConfigureAwait(false);
            var repaint = await new RepaintLookaheadAuditReportGenerator()
                .GenerateAsync(outputDirectory, Path.Combine(outputDirectory, RepaintLookaheadAuditReportGenerator.DefaultReportFileName), cancellationToken)
                .ConfigureAwait(false);

            var edge = await new StrategyEdgeVerdictReportBuilder()
                .GenerateAsync(
                    new StrategyEdgeVerdictReportInput
                    {
                        SignalQualityReport = signalQuality,
                        StrategyExtractionMarkdown = extraction.Markdown,
                        RepaintLookaheadAuditMarkdown = repaint.Markdown,
                        AssumptionsUsed = realistic.BacktestResult.AssumptionsUsed
                    },
                    Path.Combine(outputDirectory, StrategyEdgeVerdictReportBuilder.DefaultReportFileName),
                    cancellationToken)
                .ConfigureAwait(false);
            generated.Add(edge.ReportPath);

            var finalProof = await new FinalStrategyProofPackageGenerator()
                .GenerateAsync(
                    new FinalStrategyProofPackageInput
                    {
                        StrategyExtractionMarkdown = extraction.Markdown,
                        RepaintLookaheadAuditMarkdown = repaint.Markdown,
                        RealisticBacktestMarkdown = realistic.Markdown,
                        SignalQualityReport = signalQuality,
                        StrategyEdgeVerdictReport = edge,
                        AssumptionsUsed = realistic.BacktestResult.AssumptionsUsed
                    },
                    Path.Combine(outputDirectory, FinalStrategyProofPackageGenerator.DefaultReportFileName),
                    cancellationToken)
                .ConfigureAwait(false);
            generated.Add(finalProof.ReportPath);

            string operationalPath = Path.Combine(outputDirectory, "OPERATIONAL_READINESS_REPORT.md");
            await File.WriteAllTextAsync(
                    operationalPath,
                    BuildOperationalReadinessMarkdown(hasRealMarketData, finalProof, edge),
                    cancellationToken)
                .ConfigureAwait(false);
            generated.Add(Path.GetFullPath(operationalPath));

            var goNoGoInput = BuildGoNoGoInput(outputDirectory, hasRealMarketData, finalProof);
            var goNoGo = new FinalGoNoGoChecklist().EvaluateAndWriteReport(goNoGoInput);
            generated.Add(goNoGo.ReportPath);

            return new EvidencePackageCommandResult
            {
                GeneratedReports = generated,
                UsedRealMarketData = hasRealMarketData,
                UsedSampleFixture = useSampleFixture,
                MarketDataSource = marketDataSource,
                TicksLoaded = useSampleFixture ? realisticRequest.Ticks.Count : ticks.Count,
                CandlesLoaded = useSampleFixture ? realisticRequest.Candles.Count : candles.Count,
                CandidatesGenerated = realisticRequest.Candidates.Count,
                SkippedOrHoldSignals = generation.SkippedOrHoldSignals,
                IncompleteSignals = generation.IncompleteSignals,
                CandidateGenerationDiagnostic = useSampleFixture
                    ? "SAMPLE_FIXTURE_USED"
                    : generation.DiagnosticCode,
                StrategyEvidenceClassification = finalProof.EvidenceClassification,
                GoNoGoDecision = goNoGo.Decision,
                MissingInputs = MissingInputs(hasRealMarketData, finalProof, edge, goNoGo),
                RerunCommands = RerunCommands(),
                OutputDirectory = Path.GetFullPath(outputDirectory)
            };
        }

        private static FinalGoNoGoInput BuildGoNoGoInput(
            string outputDirectory,
            bool hasRealMarketData,
            FinalStrategyProofPackageResult finalProof)
        {
            bool proven = finalProof.EvidenceClassification == StrategyEvidenceClassifications.ProvenPositiveEdge;
            return new FinalGoNoGoInput
            {
                Target = FinalGoNoGoTarget.FullLive,
                ReportDirectory = outputDirectory,
                AllowFullLiveGo = false,
                TinyLiveRiskCapsConfigured = false,
                NewsProviderRequired = true,
                P0AccountSafetyReadiness = FinalChecklistStatus.Missing,
                P1ExecutionRealismReadiness = FinalChecklistStatus.Missing,
                P2RealisticBacktestReadiness = hasRealMarketData ? FinalChecklistStatus.Pass : FinalChecklistStatus.Warning,
                P3StrategyEdgeProofReadiness = proven ? FinalChecklistStatus.Pass : FinalChecklistStatus.Fail,
                P4LiveReadinessGate = FinalChecklistStatus.Missing,
                DemoForwardTestGate = FinalChecklistStatus.Missing,
                BrokerEaDeploymentChecklist = FinalChecklistStatus.Missing,
                RuntimeHealthStatus = FinalRuntimeHealthStatus.Missing,
                SafetyAlertStatus = FinalChecklistStatus.Missing,
                OperationalReadinessReportStatus = FinalChecklistStatus.Warning,
                StagedRolloutStatus = FinalChecklistStatus.Missing,
                KillSwitchInactive = null,
                UserLiveEnablementConfirmed = null,
                EaCompiledRedeployedNote = FinalChecklistStatus.Missing,
                Mt5ConnectionHealth = FinalChecklistStatus.Missing,
                NewsProviderStatus = FinalChecklistStatus.Missing
            };
        }

        private static string BuildOperationalReadinessMarkdown(
            bool hasRealMarketData,
            FinalStrategyProofPackageResult finalProof,
            StrategyEdgeVerdictReportResult edge)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Operational Readiness Report");
            sb.AppendLine();
            sb.AppendLine("This operational readiness report is generated from repository-local evidence only.");
            sb.AppendLine("It does not enable live trading and does not place orders.");
            sb.AppendLine();
            sb.AppendLine("## Summary");
            sb.AppendLine();
            sb.AppendLine("| Item | Status | Notes |");
            sb.AppendLine("| --- | --- | --- |");
            sb.AppendLine($"| Market data source | {(hasRealMarketData ? "Configured CSV" : "Sample/test fixture only")} | {(hasRealMarketData ? "External CSV path was supplied." : "No real tick/OHLC CSV was configured; profitability is not proven.")} |");
            sb.AppendLine($"| Strategy edge verdict | {edge.Verdict} | {Escape(edge.Reason)} |");
            sb.AppendLine($"| Strategy evidence classification | {finalProof.EvidenceClassification} | {finalProof.ReadinessRecommendation} |");
            sb.AppendLine("| Live readiness gate | Missing | P4 live gate evidence was not supplied to this package command. |");
            sb.AppendLine("| Demo forward-test gate | Missing | Demo/paper forward-test evidence was not supplied. |");
            sb.AppendLine("| Broker/EA checklist | Missing | MT5/EA deployment checks were not run by this offline package command. |");
            sb.AppendLine("| Runtime health | Missing | Live runtime monitor data was not supplied. |");
            sb.AppendLine("| Safety alerts | Missing | Alert history was not supplied. |");
            sb.AppendLine("| Staged rollout | Missing | Live rollout state was not persisted/proven for full-live deployment. |");
            sb.AppendLine("| Kill switch | Unknown | Current live kill-switch state was not read by this offline package command. |");
            sb.AppendLine("| User live enablement | Missing | Manual live confirmation was not captured. |");
            sb.AppendLine("| MT5 connection/health | Missing | MT5 connection was not required or contacted. |");
            sb.AppendLine("| News provider | Missing | Required live news provider status was not verified. |");
            sb.AppendLine();
            sb.AppendLine("## Readiness Conclusion");
            sb.AppendLine();
            sb.AppendLine("Operational readiness is Unknown/No-Go for real-money trading until missing live, broker, runtime, alert, news, and user-confirmation evidence is supplied.");
            sb.AppendLine("Backtests are not live proof, and sample/test fixture output must not be used as profitability proof.");
            return sb.ToString();
        }

        private static IReadOnlyDictionary<string, string> WithEvidenceAssumptions(
            IReadOnlyDictionary<string, string> source,
            bool hasRealMarketData,
            bool useSampleFixture,
            int ticksLoaded,
            int candlesLoaded,
            int candidatesGenerated,
            int skippedOrHoldSignals,
            int incompleteSignals,
            string candidateGenerationDiagnostic,
            string candidateGenerationSource,
            string aiDisabledReason,
            string offlineLiveDifferences)
        {
            var values = new Dictionary<string, string>(source, StringComparer.OrdinalIgnoreCase)
            {
                ["evidence_package_scope"] = hasRealMarketData
                    ? "Configured CSV market data was supplied; still not live proof."
                    : "No CSV market data was supplied; sample/test fixture only; profitability is not proven.",
                ["market_data_source"] = hasRealMarketData ? "Configured CSV market data" : "Built-in sample/test fixture data",
                ["sample_fixture_used"] = useSampleFixture ? "Yes" : "No",
                ["real_strategy_candidates_used"] = !useSampleFixture && candidatesGenerated > 0 ? "Yes" : "No",
                ["ticks_loaded"] = ticksLoaded.ToString(CultureInfo.InvariantCulture),
                ["candles_loaded"] = candlesLoaded.ToString(CultureInfo.InvariantCulture),
                ["candidates_generated_from_real_data"] = useSampleFixture ? "0" : candidatesGenerated.ToString(CultureInfo.InvariantCulture),
                ["skipped_or_hold_signals"] = skippedOrHoldSignals.ToString(CultureInfo.InvariantCulture),
                ["incomplete_signals"] = incompleteSignals.ToString(CultureInfo.InvariantCulture),
                ["candidate_generation_diagnostic"] = candidateGenerationDiagnostic,
                ["candidate_generation_source"] = candidateGenerationSource,
                ["ai_disabled_reason"] = aiDisabledReason,
                ["offline_live_logic_differences"] = offlineLiveDifferences,
                ["live_trading"] = "Not enabled; no live orders are placed by this command."
            };
            return values;
        }

        private static IReadOnlyList<string> MissingInputs(
            bool hasRealMarketData,
            FinalStrategyProofPackageResult finalProof,
            StrategyEdgeVerdictReportResult edge,
            FinalGoNoGoResult goNoGo)
        {
            var missing = new List<string>();
            if (!hasRealMarketData)
                missing.Add("Real tick/OHLC market data covering the intended symbols, sessions, spreads, and execution conditions.");
            else if (edge.Warnings.Any(w => w.Contains(EvidenceStrategyCandidateGenerator.NotImplementedCode, StringComparison.OrdinalIgnoreCase)))
                missing.Add("Offline strategy/scalping candidate generation from real CSV market data.");
            if (finalProof.EvidenceClassification != StrategyEvidenceClassifications.ProvenPositiveEdge)
                missing.Add("Sufficient completed realistic backtest sample and P3 proof package showing proven positive edge after costs.");
            if (edge.Verdict != StrategyEdgeVerdicts.Pass)
                missing.Add("Passing strategy edge verdict across signal quality, robustness, costs, repaint/lookahead, and AI-impact checks.");

            missing.AddRange(goNoGo.RequiredManualActions);
            missing.Add("Demo/paper forward-test reconciliation with enough completed trades and matching execution drift.");
            missing.Add("Broker/EA deployment checklist, runtime health, alert status, MT5 health, news provider status, and explicit user live confirmation.");
            return missing.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static IReadOnlyDictionary<string, SymbolInfo> BuildSymbolInfo(
            IReadOnlyList<BacktestTick> ticks,
            IReadOnlyList<BacktestOhlcCandle> candles)
        {
            var result = new Dictionary<string, SymbolInfo>(StringComparer.OrdinalIgnoreCase);

            foreach (var tick in ticks
                         .Where(t => t.Bid > 0 && t.Ask > 0)
                         .GroupBy(t => t.Symbol, StringComparer.OrdinalIgnoreCase)
                         .Select(g => g.OrderBy(t => t.TimestampUtc).First()))
            {
                result[tick.Symbol] = DefaultSymbolInfo(tick.Symbol, tick.Bid, tick.Ask);
            }

            foreach (var candle in candles
                         .Where(c => c.Close > 0)
                         .GroupBy(c => c.Symbol, StringComparer.OrdinalIgnoreCase)
                         .Select(g => g.OrderBy(c => c.TimestampUtc).First()))
            {
                if (result.ContainsKey(candle.Symbol))
                    continue;

                double bid = candle.BidOpen ?? candle.BidClose ?? candle.Close;
                double ask = candle.AskOpen ?? candle.AskClose ?? candle.Close;
                if (ask < bid)
                {
                    bid = candle.Close;
                    ask = candle.Close;
                }

                result[candle.Symbol] = DefaultSymbolInfo(candle.Symbol, bid, ask);
            }

            return result;
        }

        private static SymbolInfo DefaultSymbolInfo(string symbol, double bid, double ask) => new()
        {
            Symbol = symbol,
            Bid = bid,
            Ask = ask,
            Spread = Math.Max(0, (ask - bid) / 0.00001),
            Digits = symbol.Contains("JPY", StringComparison.OrdinalIgnoreCase) ? 3 : 5,
            MinLot = 0.01,
            MaxLot = 100,
            LotStep = 0.01,
            VolumeLimit = 0,
            PointSize = symbol.Contains("JPY", StringComparison.OrdinalIgnoreCase) ? 0.001 : 0.00001,
            StopLevelPoints = 0,
            FreezeLevelPoints = 0
        };

        private static IReadOnlyList<string> RerunCommands() =>
        [
            "dotnet run --project MT5TradingBot.csproj -- --generate-evidence-package --use-sample-fixture",
            "dotnet run --project MT5TradingBot.csproj -- --generate-evidence-package --tick-csv <path-to-ticks.csv>",
            "dotnet run --project MT5TradingBot.csproj -- --generate-evidence-package --ohlc-csv <path-to-ohlc.csv>"
        ];

        private static bool ExistingFile(string? path) =>
            !string.IsNullOrWhiteSpace(path) && File.Exists(path);

        private static string Escape(string value) =>
            value.Replace("|", "\\|", StringComparison.Ordinal);
    }
}
