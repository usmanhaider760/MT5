using System.Text;

namespace MT5TradingBot.Modules.StrategyProof
{
    public sealed record RepaintLookaheadAuditReportResult
    {
        public bool Success { get; init; }
        public string OutputPath { get; init; } = "";
        public string Markdown { get; init; } = "";
        public IReadOnlyList<string> Warnings { get; init; } = [];
    }

    public sealed class RepaintLookaheadAuditReportGenerator
    {
        public const string DefaultReportFileName = "REPAINT_LOOKAHEAD_AUDIT_REPORT.md";

        public async Task<RepaintLookaheadAuditReportResult> GenerateAsync(
            string repositoryRoot,
            string? outputPath = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string root = string.IsNullOrWhiteSpace(repositoryRoot)
                ? Directory.GetCurrentDirectory()
                : repositoryRoot;
            string reportPath = string.IsNullOrWhiteSpace(outputPath)
                ? Path.Combine(root, DefaultReportFileName)
                : outputPath;

            var findings = BuildFindings(root);
            var warnings = findings
                .Where(f => f.Status is "Potential" or "Not verified")
                .Select(f => $"{f.Category}: {f.Finding}")
                .ToList();
            string markdown = BuildMarkdown(findings);

            string? directory = Path.GetDirectoryName(Path.GetFullPath(reportPath));
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            await File.WriteAllTextAsync(reportPath, markdown, cancellationToken)
                .ConfigureAwait(false);

            return new RepaintLookaheadAuditReportResult
            {
                Success = true,
                OutputPath = Path.GetFullPath(reportPath),
                Markdown = markdown,
                Warnings = warnings
            };
        }

        private static IReadOnlyList<LookaheadFinding> BuildFindings(string root) =>
        [
            Evidence(
                root,
                "Live Signal-Generation Risk",
                "Closed-candle snapshots are used for candle and indicator fields",
                "Low",
                "Confirmed",
                "MT5_EA/TradingBotEA.mq5",
                "SnapshotCandleJson / SnapshotIndicatorsJson",
                "The EA requests candle data from shift 1 and indicator buffers from shift 1 for several snapshot fields.",
                "This lowers live repainting risk because the most recent still-forming candle is skipped for these fields.",
                "Keep closed-candle shift usage covered when adding snapshot fields. Verify any new field documents its candle shift.",
                ["CopyRates(sym, tf, 1, 3", "SnapshotBufferValue(iRSI(sym, tf, 14, PRICE_CLOSE), 0, 1)"]),

            Evidence(
                root,
                "Live Signal-Generation Risk",
                "Current daily and weekly high/low fields can become look-ahead in historical replay",
                "High",
                "Potential",
                "MT5_EA/TradingBotEA.mq5",
                "SnapshotPriceJson",
                "The snapshot includes current-period daily and weekly highs/lows from shift 0.",
                "In live trading, current-period high/low are known only up to the current tick. In a historical replay, using the final daily or weekly high/low before the period has completed would leak future information into entry decisions or AI context.",
                "When replaying history, reconstruct these fields from ticks or lower-timeframe candles available at the signal timestamp, or mark them unavailable until verified.",
                ["CopyRates(sym, PERIOD_D1, 0, 2", "double dailyHigh = ArraySize(d1) > 0 ? d1[0].high"]),

            Evidence(
                root,
                "Live Signal-Generation Risk",
                "Support, resistance, and structure levels need timestamped reconstruction proof",
                "High",
                "Potential",
                "MT5_EA/TradingBotEA.mq5",
                "SnapshotStructureJson / SnapshotLevelsJson",
                "Structure uses closed H1 swings, while levels also read current and prior daily data.",
                "Swing and level fields can repaint in historical tests if they are reconstructed from complete future candles instead of the bars known at signal time.",
                "Add a timestamp-aware snapshot fixture proving each support/resistance input is calculated only from closed or elapsed bars available at the candidate timestamp.",
                ["SnapshotHigh(sym, PERIOD_H1, 1, 20)", "CopyRates(sym, PERIOD_D1, 0, 2"]),

            new(
                "Live Signal-Generation Risk",
                "No per-signal consumed-data watermark was found",
                "Medium",
                "Not verified",
                "Trading/Scalping/ScalpingSessionService.cs",
                "EvaluateSnapshot",
                "The report did not verify a persisted timestamp for the newest candle, indicator, or level consumed by each generated signal.",
                "Without a data watermark, later audits cannot prove that a signal avoided future candles or partially formed candle closes.",
                "Persist signal timestamp, newest consumed candle timestamp by timeframe, and source snapshot timestamp for strategy-proof datasets."),

            Evidence(
                root,
                "Realistic Backtest Runner Risk",
                "Exit simulation uses future path after the candidate timestamp",
                "Medium",
                "Confirmed",
                "Trading/Backtesting/RealisticBacktestRunner.cs",
                "RealisticBacktestRunner.ResolveExit",
                "The runner searches ticks and candles with timestamp greater than or equal to the candidate timestamp to determine exits.",
                "Using future market path is correct for resolving post-entry exits, but these same future bars must never be used to create or filter the entry candidate.",
                "Keep candidate generation separate from exit resolution and add guards that adapters cannot read ticks/candles after the signal timestamp when producing candidates.",
                ["ResolveExit", "EnsureUtc(t.TimestampUtc) >= timestampUtc", "EnsureUtc(c.TimestampUtc) >= timestampUtc"]),

            Evidence(
                root,
                "Realistic Backtest Runner Risk",
                "Strategy adapter can accept an externally supplied historical market price",
                "Medium",
                "Potential",
                "Trading/Backtesting/StrategyToRealisticBacktestAdapter.cs",
                "StrategyToRealisticBacktestAdapter.ResolveEntryPrice",
                "When signal entry is not positive, the adapter uses HistoricalMarketPrice supplied by the caller.",
                "The adapter itself cannot prove whether that price came from the signal timestamp or a later candle. A caller could accidentally supply a future close.",
                "Require fixture metadata for historical market price source and add tests where future-bar prices are rejected or explicitly marked as invalid input.",
                ["HistoricalMarketPrice", "ResolveEntryPrice", "historicalMarketPrice.GetValueOrDefault()"]),

            Evidence(
                root,
                "Realistic Backtest Runner Risk",
                "OHLC exit simulation uses candle high/low but resolves ambiguous same-bar hits conservatively",
                "Low",
                "Confirmed",
                "Trading/Backtesting/IntrabarExitSimulator.cs",
                "IntrabarExitSimulator.SimulateOhlcExit",
                "OHLC exit checks final high/low for stop-loss and take-profit hits and chooses stop loss when both are hit in the same candle.",
                "Final high/low are acceptable for closed-candle exit simulation, but they do not prove intrabar order unless tick data is available.",
                "Prefer tick data for scalping proof. Keep OHLC both-hit behavior conservative and label OHLC reports as path-assumption based.",
                ["SimulateOhlcExit", "if (stopHit && takeProfitHit)", "ExitType = IntrabarExitType.StopLoss"]),

            Evidence(
                root,
                "Old Trade-Summary Backtest Limitation",
                "SQLite trade-history backtest reconstructs exits from realized P/L",
                "Critical",
                "Confirmed",
                "Trading/Backtesting/IBacktestDataLoader.cs",
                "DbBacktestLoader.LoadAsync",
                "The old loader derives pips from ProfitUsd and reconstructs an exit price from executed price and realized profit.",
                "Closed trade summaries prove what happened to already-taken trades, not whether historical signals had positive expectancy. They cannot detect skipped signals, future-data bias, or entry-rule edge.",
                "Keep the old summary backtest for trade-summary reporting only. Use realistic timestamped market-data backtests for strategy edge proof.",
                ["r.ProfitUsd", "double exitPx", "ExitPrice  = exitPx"]),

            Evidence(
                root,
                "Old Trade-Summary Backtest Limitation",
                "Summary backtest calculates results from supplied exit prices",
                "High",
                "Confirmed",
                "Trading/Backtesting/BacktestingService.cs",
                "BacktestingService.CalculatePips",
                "The service computes pips from BacktestTrade entry and exit prices.",
                "If the input is a closed-trade summary, the service evaluates historical trade outcomes rather than replaying entry decisions without future knowledge.",
                "Report this as trade-summary analytics, not signal-edge proof. Do not use it as evidence that current deterministic strategy entries are profitable.",
                ["CalculatePips", "trade.EntryPrice", "trade.ExitPrice"]),

            Evidence(
                root,
                "AI-Prompt Leakage Risk",
                "AI prompt includes recent trade outcomes and daily performance fields",
                "High",
                "Potential",
                "Infrastructure/AI/AiPrompts.cs",
                "AiPrompts.AiInputPromptTemplate",
                "The prompt contains TRADE HISTORY, consecutive losses, win rate today, total PnL today, and Last 5 Trades.",
                "Those fields are valid live context only if they contain outcomes known before the signal timestamp. In historical AI-filter tests, including later trades or end-of-day statistics would leak future results into the decision.",
                "Freeze AI prompt context as of each candidate timestamp and add tests that future closed trades are excluded from prompt fixtures.",
                ["TRADE HISTORY:", "Win Rate Today", "Last 5 Trades"]),

            Evidence(
                root,
                "AI-Prompt Leakage Risk",
                "AI instructions ask for current-market derivation, but historical prompt snapshots are not proven frozen",
                "Medium",
                "Not verified",
                "Infrastructure/AI/AiPrompts.cs",
                "AiPrompts.AiInputPromptTemplate",
                "The template says to derive SL/TP from current market structure, ATR, and levels, but this audit did not verify archived prompt inputs by timestamp.",
                "A correct prompt can still leak if the snapshot filler injects fields calculated from future candles or future trade outcomes.",
                "For AI-filter proof, store the exact prompt payload, signal timestamp, data watermark, and model response used for each historical decision.",
                ["derive stop_loss and take_profit from current", "Use only the live"]),

            new(
                "AI-Prompt Leakage Risk",
                "No confirmed use of realized P/L, exit price, or future result in deterministic entry logic was found",
                "Low",
                "Not verified",
                "Trading/StrategyEngine/StrategyEngine.cs",
                "StrategyEngine.CreateInitialSignalAsync",
                "This audit did not find source evidence that base deterministic entry generation reads exit price or realized P/L. The result is marked not verified because the full runtime data flow was not exhaustively traced.",
                "If realized outcomes feed entry generation indirectly, the strategy can overfit to prior or future performance instead of market state.",
                "Add a source guard for deterministic strategy modules that blocks references to ExitPrice, ProfitUsd, realized P/L, or completed-trade outcome fields in entry-signal methods.")
        ];

        private static LookaheadFinding Evidence(
            string root,
            string category,
            string finding,
            string severity,
            string status,
            string relativePath,
            string member,
            string evidence,
            string whyItMatters,
            string recommendedFix,
            IReadOnlyList<string> requiredFragments)
        {
            string path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            string source = File.Exists(path) ? File.ReadAllText(path) : "";
            bool verified = File.Exists(path) &&
                requiredFragments.All(fragment => source.Contains(fragment, StringComparison.Ordinal));

            return new LookaheadFinding(
                category,
                finding,
                severity,
                verified ? status : "Not verified",
                relativePath,
                member,
                verified
                    ? evidence
                    : $"{evidence} Expected source fragments were not all found.",
                whyItMatters,
                recommendedFix);
        }

        private static string BuildMarkdown(IReadOnlyList<LookaheadFinding> findings)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Repainting / Future-Data Bias Audit Report");
            sb.AppendLine();
            sb.AppendLine("Scope: P3 audit/reporting only. This generator does not change strategy logic, indicators, AI prompts, take-profit logic, live trading behavior, or execution behavior.");
            sb.AppendLine();
            sb.AppendLine("This report is a source-code audit. It flags confirmed limitations, potential look-ahead risks, and areas not verified from code alone. It is not live proof and not a positive-expectancy claim.");
            sb.AppendLine();
            sb.AppendLine("## Summary");
            sb.AppendLine();
            sb.AppendLine("- Closed-candle safeguards are visible for several EA candle and indicator snapshot fields.");
            sb.AppendLine("- Current-period high/low and level fields need timestamped historical reconstruction before they can be used as proof inputs.");
            sb.AppendLine("- The realistic runner is appropriate for post-entry exit simulation, but candidate generation must remain timestamp-clean.");
            sb.AppendLine("- The old trade-summary backtest is confirmed to be outcome-summary analytics, not signal-edge proof.");
            sb.AppendLine("- AI prompt context must be frozen at the candidate timestamp before AI-confirmed backtests can be trusted.");
            sb.AppendLine();

            AppendCategory(sb, findings, "Live Signal-Generation Risk");
            AppendCategory(sb, findings, "Realistic Backtest Runner Risk");
            AppendCategory(sb, findings, "Old Trade-Summary Backtest Limitation");
            AppendCategory(sb, findings, "AI-Prompt Leakage Risk");

            sb.AppendLine("## Severity And Status Legend");
            sb.AppendLine();
            sb.AppendLine("- Severity: Critical / High / Medium / Low.");
            sb.AppendLine("- Status: Confirmed / Potential / Not verified.");
            sb.AppendLine("- Confirmed means the listed behavior is directly evidenced by source fragments.");
            sb.AppendLine("- Potential means source evidence identifies a risk that depends on runtime data preparation or historical replay usage.");
            sb.AppendLine("- Not verified means this audit could not prove the behavior from currently inspected code.");
            return sb.ToString();
        }

        private static void AppendCategory(
            StringBuilder sb,
            IReadOnlyList<LookaheadFinding> findings,
            string category)
        {
            sb.AppendLine($"## {category}");
            sb.AppendLine();
            sb.AppendLine("| Finding | Severity | Status | Code Evidence | Why It Matters | Recommended Fix Or Verification Step |");
            sb.AppendLine("|---|---|---|---|---|---|");

            foreach (var finding in findings.Where(f => f.Category == category))
            {
                sb.AppendLine(
                    $"| {Escape(finding.Finding)} | {finding.Severity} | {finding.Status} | `{finding.FilePath}` - `{finding.Member}`: {Escape(finding.Evidence)} | {Escape(finding.WhyItMatters)} | {Escape(finding.RecommendedFix)} |");
            }

            sb.AppendLine();
        }

        private static string Escape(string value) =>
            value.Replace("|", "\\|", StringComparison.Ordinal);

        private sealed record LookaheadFinding(
            string Category,
            string Finding,
            string Severity,
            string Status,
            string FilePath,
            string Member,
            string Evidence,
            string WhyItMatters,
            string RecommendedFix);
    }
}
