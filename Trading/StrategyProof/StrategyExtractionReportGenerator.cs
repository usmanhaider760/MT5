using System.Text;

namespace MT5TradingBot.Modules.StrategyProof
{
    public sealed record StrategyExtractionReportResult
    {
        public bool Success { get; init; }
        public string OutputPath { get; init; } = "";
        public string Markdown { get; init; } = "";
        public IReadOnlyList<string> Warnings { get; init; } = [];
    }

    public sealed class StrategyExtractionReportGenerator
    {
        public const string DefaultReportFileName = "STRATEGY_EXTRACTION_REPORT.md";

        public async Task<StrategyExtractionReportResult> GenerateAsync(
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

            var evidence = BuildEvidence(root);
            var warnings = evidence
                .Where(e => e.Status == "Not verified")
                .Select(e => $"{e.Area}: {e.Summary}")
                .ToList();
            string markdown = BuildMarkdown(evidence);

            string? directory = Path.GetDirectoryName(Path.GetFullPath(reportPath));
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            await File.WriteAllTextAsync(reportPath, markdown, cancellationToken)
                .ConfigureAwait(false);

            return new StrategyExtractionReportResult
            {
                Success = true,
                OutputPath = Path.GetFullPath(reportPath),
                Markdown = markdown,
                Warnings = warnings
            };
        }

        private static IReadOnlyList<StrategyEvidence> BuildEvidence(string root) =>
        [
            Evidence(
                root,
                "Deterministic base strategy",
                "Trading/StrategyEngine/StrategyEngine.cs",
                "StrategyEngine.CreateInitialSignalAsync",
                "Selects the highest-score available scanner pair, tie-breaking by lower spread. Fetches symbol data, calculates mid price, sets market order, SL below mid, TP above mid, but returns Direction=Hold.",
                ["OrderByDescending(p => p.Score)", "ThenBy(p => p.SpreadPips)", "Direction = SignalDirection.Hold"]),

            Evidence(
                root,
                "Base strategy Buy/Sell generation",
                "Trading/StrategyEngine/StrategyEngine.cs",
                "StrategyEngine.CreateInitialSignalAsync",
                "The base strategy does not produce deterministic Buy/Sell. Its reason states direction remains HOLD until AI/user decision confirms setup.",
                ["Direction = SignalDirection.Hold", "Direction remains HOLD until AI/user decision confirms setup"]),

            Evidence(
                root,
                "Base strategy SL/TP generation",
                "Trading/StrategyEngine/StrategyEngine.cs",
                "StrategyEngine.CreateInitialSignalAsync",
                "Uses stopDistance=max(15 pips, spread*3) and takeProfitDistance from the normal trade-page RiskRewardRatio. This creates provisional levels only because direction remains Hold.",
                ["Math.Max(15 * pipSize", "info.SpreadPips * 3", "takeProfitDistance = stopDistance"]),

            Evidence(
                root,
                "Scalping deterministic preconditions",
                "Trading/Scalping/ScalpingSessionService.cs",
                "ScalpingSessionService.RunAsync",
                "Auto scalping waits for MT5 connection, session time, max trade count, max loss/profit target, symbol data, max spread, news risk, pyramiding rules, and cooldown before evaluating a setup.",
                ["Max trades reached", "spread is too high", "news risk is active", "cooling down"]),

            Evidence(
                root,
                "Scalping direction generation",
                "Trading/Scalping/ScalpingSessionService.cs",
                "ResolveScalpingDecision / ResolveProbeDirection",
                "DirectionMode can force BuyOnly/SellOnly, use the reviewed signal direction, or Auto-check both BUY and SELL and choose the only/stronger side that passes.",
                ["ScalpingDirectionMode.BuyOnly", "ScalpingDirectionMode.SellOnly", "Auto direction checked both sides"]),

            Evidence(
                root,
                "Scalping confirmation rules",
                "Trading/Scalping/ScalpingSessionService.cs",
                "EvaluateSnapshot",
                "Snapshot scoring checks M5/M15/H1 trend, M5 candle direction, M5/M15 MACD, M5 price vs EMA20/EMA50, RSI zone, stochastic guard, doji/inside-bar rejection, and room to support/resistance. Approved requires score >= MinDecisionScore.",
                ["structure.trend_m5", "indicators.m5.macd_bias", "price_vs_ema20", "score >= cfg.MinDecisionScore"]),

            Evidence(
                root,
                "Scalping fallback confirmation",
                "Trading/Scalping/ScalpingSessionService.cs",
                "ResolvePriceMovementDecision",
                "If full snapshot is unavailable, price movement can approve a trade: Auto chooses BUY when current mid >= previous mid, SELL otherwise; fixed direction requires movement to agree.",
                ["ResolvePriceMovementDecision", "currentMid >= previousMid", "price movement"]),

            Evidence(
                root,
                "Scalping SL/TP generation",
                "Trading/Scalping/ScalpingSessionService.cs",
                "BuildRequest",
                "Entry uses ask for BUY and bid for SELL. SL/TP are derived from configured or decision-suggested pip distances. EntryPrice is set to 0 for market execution.",
                ["entry = direction == TradeType.BUY ? symbol.Ask : symbol.Bid", "SuggestedSlPips", "EntryPrice = 0"]),

            Evidence(
                root,
                "Lot sizing source",
                "Trading/RiskManagement/RiskManager.cs",
                "RiskManager.ValidateAsync",
                "Lot size is selected on the Review Trade page. Auto From Risk % calculates from equity, MaxRiskPercent, reference entry, SL, and pair; manual lot selections use the dropdown value. AutoBot applies ValidatedLotSize before execution.",
                ["Review Trade lot dropdown", "LotCalculator.Calculate", "ValidatedLotSize"]),

            Evidence(
                root,
                "AI analysis boundary",
                "Infrastructure/AI/AiAnalysisService.cs",
                "AiAnalysisService.AnalyzeAsync",
                "The skeleton AI analysis calculates baseline confidence but always returns Direction=Hold and states that real AI provider confirmation is required.",
                ["Direction = SignalDirection.Hold", "Invalid until a real AI provider confirms"]),

            Evidence(
                root,
                "AI signal execution boundary",
                "Infrastructure/AI/ClaudeSignalService.cs",
                "ClaudeSignalService.ParseAndExecuteAsync",
                "Claude JSON can produce NO_TRADE or TRADE. NO_TRADE updates context and returns. TRADE requires nonzero SL/TP, builds TradeRequest from AI fields, then calls the provided execution delegate.",
                ["action == \"NO_TRADE\"", "sig.StopLoss == 0", "new TradeRequest", "_execute(req)"]),

            Evidence(
                root,
                "Signal decision AI boundary",
                "Application/SignalDecision/SignalDecisionService.cs",
                "SignalDecisionService.CreateDecisionAsync",
                "Risk must approve, AI must exist, AI risk cannot be Blocked, AI direction cannot be Hold, confidence must be at least 70, and pair must match. Final direction/SL/TP come from AI analysis.",
                ["Risk blocked signal", "AI recommends HOLD", "ConfidenceScore < 70", "Direction = aiAnalysis.Direction"]),

            Evidence(
                root,
                "User/manual approval boundary",
                "UI/Forms/MainForm.cs",
                "ExecuteSignalFromCardSafeAsync / ShowTradeReviewDialogAsync",
                "Manual card execution requires the user review dialog to approve. Review can override lot size and final request before calling AutoBotService.ExecuteTradeWithValidationAsync.",
                ["ShowTradeReviewDialogAsync", "review.Approved", "review.FinalRequest", "ExecuteTradeWithValidationAsync"]),

            Evidence(
                root,
                "Auto-scalping user approval boundary",
                "UI/Forms/MainForm.cs",
                "StartAutoScalpingFromReviewAsync",
                "Starting live auto scalping asks the user to confirm unless paper trading is enabled. The scalping session executes through AutoBotService.ExecuteTradeWithValidationAsync.",
                ["Start LIVE auto scalping", "Confirm(", "ExecuteTradeWithValidationAsync"]),

            Evidence(
                root,
                "Risk/execution safety boundary",
                "Application/Workflows/AutoBotService.cs",
                "AutoBotService.ExecuteTradeWithValidationAsync",
                "The central execution gate checks kill switch, no-trade windows, signal expiry, allowed pairs, account data, symbol/spread data, broker levels, positions, loss limits, exposure, risk validation, lot rules, costs, margin, correlation, news, then paper fill or trade execution.",
                ["KILL_SWITCH_ACTIVE", "NO_ACCOUNT", "NO_SYMBOL_DATA", "Risk validation", "NEWS_UNAVAILABLE"]),

            Evidence(
                root,
                "Broker send boundary",
                "Trading/TradeExecution/TradeExecutionService.cs",
                "TradeExecutionService.ExecuteAsync",
                "Broker execution requires valid request, approved risk, approved workflow/user decision, accepted broker OrderCheck, then calls MT5Bridge.OpenTradeAsync.",
                ["USER_APPROVAL_REQUIRED", "BROKER_ORDERCHECK_REJECTED", "OpenTradeAsync"]),

            new(
                "Not verified",
                "Historical reproducibility",
                "MT5_EA/TradingBotEA.mq5",
                "GET_MARKET_SNAPSHOT / indicator calculation",
                "This patch did not audit whether snapshot fields are calculated from closed candles only, whether support/resistance can repaint, or whether all fields are available historically.",
                "Not verified"),

            new(
                "Not verified",
                "Complete deterministic exit management",
                "Application/Workflows/AutoBotService.cs",
                "HeartbeatLoopAsync / position management",
                "SL/TP placement is documented, but full post-entry management such as break-even, trailing, manual close, and emergency close behavior is not fully proven as deterministic strategy exit logic in this report.",
                "Not verified")
        ];

        private static StrategyEvidence Evidence(
            string root,
            string area,
            string relativePath,
            string member,
            string summary,
            IReadOnlyList<string> requiredFragments)
        {
            string path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            string source = File.Exists(path) ? File.ReadAllText(path) : "";
            bool verified = File.Exists(path) &&
                requiredFragments.All(fragment =>
                    source.Contains(fragment, StringComparison.Ordinal));

            return new StrategyEvidence(
                verified ? "Verified" : "Not verified",
                area,
                relativePath,
                member,
                summary,
                verified
                    ? "Required source fragments were found."
                    : "One or more expected source fragments were not found.");
        }

        private static string BuildMarkdown(IReadOnlyList<StrategyEvidence> evidence)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Strategy Extraction Report");
            sb.AppendLine();
            sb.AppendLine("Scope: reporting/extraction only. No entry rules, indicators, AI prompts, take-profit logic, or live trading behavior are changed by this report generator.");
            sb.AppendLine();
            sb.AppendLine("## Strategy Flow Diagram");
            sb.AppendLine();
            sb.AppendLine("```text");
            sb.AppendLine("Pair scanner -> StrategyEngine.CreateInitialSignalAsync -> base MarketSignal");
            sb.AppendLine("  -> Base strategy selects candidate pair and provisional levels");
            sb.AppendLine("  -> Base strategy direction remains HOLD");
            sb.AppendLine("  -> AI/manual/pair-review/scalping path supplies or confirms BUY/SELL");
            sb.AppendLine("  -> User/manual approval or workflow approval");
            sb.AppendLine("  -> AutoBotService.ExecuteTradeWithValidationAsync safety gate");
            sb.AppendLine("  -> Risk/news/margin/exposure/broker checks");
            sb.AppendLine("  -> TradeExecutionService -> MT5 only after approval and OrderCheck");
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("## Base Strategy Direction Verdict");
            sb.AppendLine();
            sb.AppendLine("The base deterministic strategy currently produces mostly HOLD. `StrategyEngine.CreateInitialSignalAsync` selects the best available scanner pair and creates provisional entry/SL/TP levels, but sets `Direction = SignalDirection.Hold`. Deterministic Buy/Sell decisions are produced in the auto-scalping layer or supplied by AI/manual signal paths, then constrained by user approval and risk/execution safety.");
            sb.AppendLine();
            sb.AppendLine("## Deterministic Rule Logic");
            sb.AppendLine();
            sb.AppendLine("- Pair selection: highest available scanner score, lower spread as tie-breaker.");
            sb.AppendLine("- Base strategy direction: Hold.");
            sb.AppendLine("- Base provisional entry: mid price from bid/ask.");
            sb.AppendLine("- Base provisional stop loss: mid minus max(15 pips, spread * 3).");
            sb.AppendLine("- Base provisional take profit: stop distance multiplied by max(configured minimum R:R, 1.5).");
            sb.AppendLine("- Auto-scalping direction: configured BuyOnly/SellOnly/SignalDirection, or Auto chooses the passing stronger side.");
            sb.AppendLine("- Auto-scalping confirmation: snapshot score must meet configured minimum and hard blockers must not fire.");
            sb.AppendLine("- Auto-scalping fallback: price movement can be used when snapshot is unavailable.");
            sb.AppendLine();
            sb.AppendLine("## AI-Assisted Logic Boundary");
            sb.AppendLine();
            sb.AppendLine("- `AiAnalysisService` skeleton does not approve Buy/Sell; it returns Hold and a confidence/risk annotation.");
            sb.AppendLine("- `SignalDecisionService` can convert a strategy signal into a trade-ready signal only when AI direction is Buy/Sell, confidence is at least 70, risk is not blocked, and pair matches.");
            sb.AppendLine("- `ClaudeSignalService` can create trade requests from AI JSON, but NO_TRADE/HOLD-like responses return without execution.");
            sb.AppendLine("- Auto-scalping AI confirmation is optional and only confirms an already rule-filtered opportunity.");
            sb.AppendLine();
            sb.AppendLine("## User And Manual Approval Logic");
            sb.AppendLine();
            sb.AppendLine("- Manual signal cards stop at review until the user approves.");
            sb.AppendLine("- Review can adjust lot size and final request fields before the execution gate.");
            sb.AppendLine("- Live auto-scalping start asks for explicit user confirmation unless paper trading is enabled.");
            sb.AppendLine();
            sb.AppendLine("## Risk And Execution Safety Logic");
            sb.AppendLine();
            sb.AppendLine("- Safety gates are not strategy edge rules. They can block trades after a signal exists.");
            sb.AppendLine("- The central gate validates account, symbol/spread, broker rules, daily/weekly loss, exposure, risk, costs, margin, correlation, news, and kill switch state.");
            sb.AppendLine("- Broker send is isolated in `TradeExecutionService` after request validation, risk approval, approval decision, and broker OrderCheck.");
            sb.AppendLine();
            sb.AppendLine("## Findings Table");
            sb.AppendLine();
            sb.AppendLine("| Status | Area | Summary |");
            sb.AppendLine("|---|---|---|");
            foreach (var item in evidence)
                sb.AppendLine($"| {item.Status} | {Escape(item.Area)} | {Escape(item.Summary)} |");
            sb.AppendLine();
            sb.AppendLine("## Code Evidence");
            sb.AppendLine();
            sb.AppendLine("| Status | File | Class/Method | Evidence |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var item in evidence)
                sb.AppendLine($"| {item.Status} | `{item.FilePath}` | `{item.Member}` | {Escape(item.Evidence)} |");
            sb.AppendLine();
            sb.AppendLine("## Hold/No-Trade Behavior");
            sb.AppendLine();
            sb.AppendLine("- Base strategy returns Hold when no scanner pair is available, market data is unavailable, strategy creation throws, or after creating provisional levels pending AI/user confirmation.");
            sb.AppendLine("- Signal decision returns Hold when risk blocks, AI is missing, AI blocks, AI says Hold, confidence is below 70, or pair mismatches.");
            sb.AppendLine("- Claude path treats `NO_TRADE` as no execution and updates AI context.");
            sb.AppendLine("- Pair analysis UI maps AI `NO_TRADE` to no trade and unknown responses to WAIT.");
            sb.AppendLine("- Auto-scalping waits/no-trades on failed preconditions, insufficient confirmation score, unclear candle, RSI hard blockers, equal/failed auto sides, optional AI rejection, or safety gate rejection.");
            sb.AppendLine();
            sb.AppendLine("## Exit Conditions And SL/TP Generation");
            sb.AppendLine();
            sb.AppendLine("- Base strategy provisional SL/TP are generated from mid price, spread-aware stop distance, and configured minimum R:R.");
            sb.AppendLine("- AI/manual JSON paths can supply final SL/TP directly.");
            sb.AppendLine("- Auto-scalping builds SL/TP from configured pip distances or decision-suggested pip distances around current bid/ask.");
            sb.AppendLine("- TakeProfit2 is logged by the execution gate but one-click execution opens one trade using TakeProfit.");
            sb.AppendLine("- Not verified: full post-entry exit management, break-even movement, trailing stop behavior, manual closes, and emergency closes are outside this patch's deterministic strategy extraction proof.");
            sb.AppendLine();
            sb.AppendLine("## Areas Marked Not Verified");
            sb.AppendLine();
            foreach (var item in evidence.Where(e => e.Status == "Not verified"))
                sb.AppendLine($"- {item.Area}: {item.Summary}");
            sb.AppendLine("- Future-data/repainting risk is not proven by this patch; it belongs to P3 Patch 3.");
            return sb.ToString();
        }

        private static string Escape(string value) =>
            value.Replace("|", "\\|", StringComparison.Ordinal);

        private sealed record StrategyEvidence(
            string Status,
            string Area,
            string FilePath,
            string Member,
            string Summary,
            string Evidence);
    }
}
