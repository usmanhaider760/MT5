using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MT5TradingBot.UI
{
    internal static class LogLineExplainer
    {
        private const string NumberPattern = @"[0-9]+(?:\.[0-9]+)?";

        private static readonly Regex PriceSummaryRegex = new(
            @"Price:\s*bid\s*(?<bid>[0-9]+(?:\.[0-9]+)?),\s*ask\s*(?<ask>[0-9]+(?:\.[0-9]+)?)\.\s*Spread:\s*(?<spread>[0-9]+(?:\.[0-9]+)?)\s*pips\s*\(max\s*(?<maxSpread>[0-9]+(?:\.[0-9]+)?)\)\.\s*Lot\s*(?<lot>[0-9]+(?:\.[0-9]+)?);\s*each pip about\s*\$(?<pipValue>[0-9]+(?:\.[0-9]+)?)\.\s*Risk if SL hits:\s*(?<slPips>[0-9]+(?:\.[0-9]+)?)\s*pips\s*/\s*about\s*\$(?<risk>[0-9]+(?:\.[0-9]+)?)\.\s*Profit target:\s*(?<tpPips>[0-9]+(?:\.[0-9]+)?)\s*pips\s*/\s*about\s*\$(?<profit>[0-9]+(?:\.[0-9]+)?)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static AppLogDetail Explain(string rawLine)
        {
            try
            {
                string line = Clean(rawLine);
                string message = StripTimestamp(line);
                var summary = ParsePriceSummary(message);

                if (Contains(message, "Waiting: spread") && Contains(message, "above the user ceiling"))
                    return ExplainSpreadBlock(line, message, summary);

                if (Contains(message, "market volatility is too high for scalping") || Contains(message, "live conditions require about"))
                    return ExplainVolatilityBlock(line, message, summary);

                if (Contains(message, "broker stop/freeze-level data is unavailable") || Contains(message, "BROKER_STOP_LEVEL_DATA_UNAVAILABLE"))
                    return ExplainBrokerStopData(line, message, summary);

                if (Contains(message, "Trade was not opened"))
                    return ExplainTradeNotOpened(line, message, summary);

                if (Contains(message, "Trying trade"))
                    return ExplainTryingTrade(line, message, summary);

                if (Contains(message, "Started XAUUSD") || Contains(message, "Started "))
                    return ExplainSessionStarted(line, message, summary);

                if (Contains(message, "User confirmed review warnings"))
                    return ExplainReviewWarnings(line, message, summary);

                if (Contains(message, "Review market snapshot timed out") || Contains(message, "Review account info timed out") || Contains(message, "using fallback account/symbol data"))
                    return ExplainReviewTimeout(line, message, summary);

                if (Contains(message, "Pipe error") || Contains(message, "Pipe is broken"))
                    return ExplainPipeError(line, message, summary);

                if (Contains(message, "Reload required in MT5"))
                    return ExplainEaReload(line, message, summary);

                if (Contains(message, "opened") || Contains(message, "executed") || Contains(message, "filled"))
                    return ExplainExecutionResult(line, message, summary);

                return Generic(line, message, summary);
            }
            catch (Exception ex)
            {
                string line = Clean(rawLine);
                return new AppLogDetail(
                    line,
                    "The detail parser could not fully parse this log line, but the original message is still shown above.",
                    $"Parser error: {ex.Message}",
                    "No formula is available because parsing failed.",
                    "No trade action was performed by the detail window. This error only affected explanation display.",
                    "Not available from this single log line.",
                    "The bot should keep running. Report this log line so the explanation parser can be improved.");
            }
        }

        private static AppLogDetail ExplainSpreadBlock(string original, string message, PriceSummary? summary)
        {
            double? spread = ReadDouble(message, $@"spread\s*(?<v>{NumberPattern})\s*pips");
            double? ceiling = ReadDouble(message, $@"user ceiling\s*(?<v>{NumberPattern})\s*pips");
            var values = new StringBuilder();
            AppendValue(values, "Live spread", spread, "pips");
            AppendValue(values, "Allowed ceiling", ceiling, "pips");
            AppendSummary(values, summary);

            return Detail(
                original,
                "The bot waited because the current spread was above the maximum spread allowed for this scalping setup. For scalping, spread is a direct entry cost, so a small increase can destroy the trade edge.",
                values.ToString(),
                BuildFormula(summary, "Spread check: live spread <= max spread. Professional extra check: spread cost should usually stay under about 15% to 20% of TP distance."),
                "No trade was opened. This is a safety block, not a bot failure.",
                BuildExpectedPl(summary),
                "Wait for spread to normalize, reduce the max spread only if broker conditions support it, or avoid XAUUSD scalping during unstable liquidity.");
        }

        private static AppLogDetail ExplainVolatilityBlock(string original, string message, PriceSummary? summary)
        {
            double? needed = ReadDouble(message, $@"(?:ATR needs about|require about)\s*(?<v>{NumberPattern})\s*SL pips");
            double? guardrail = ReadDouble(message, $@"guardrail\s*(?<v>{NumberPattern})\s*pips");
            var values = new StringBuilder();
            AppendValue(values, "Required SL from live volatility", needed, "pips");
            AppendValue(values, "Maximum allowed SL guardrail", guardrail, "pips");
            if (needed > 0 && guardrail > 0)
            {
                AppendValue(values, "Amount above guardrail", needed - guardrail, "pips");
                AppendValue(values, "Required SL as percent of guardrail", needed / guardrail * 100.0, "%");
            }
            AppendSummary(values, summary);

            return Detail(
                original,
                "Technical meaning: the ATR volatility model says the market is moving too widely for the current scalping guardrails. ATR means Average True Range; it estimates how far price has recently been moving.\n\nNon-technical meaning: gold is currently too jumpy for this small scalping setup. The bot would need a much wider safety distance to avoid being stopped out by normal noise.",
                values.ToString(),
                BuildFormula(summary, "Dynamic SL rule: required SL = max(pair minimum SL, ATR-based volatility distance, market-structure distance, broker stop/freeze distance, spread buffer).\n\nDecision rule: if required SL > max SL guardrail, the bot must wait. In this log, the required SL was above the guardrail, so the setup failed the volatility safety check."),
                "No trade was opened. This is a professional safety block: the bot refused to force a tight scalp into a market that currently needs more room than allowed.",
                BuildExpectedPl(summary),
                "Wait for ATR/volatility to cool down. Do not simply raise the 500-pip guardrail unless account risk, TP distance, session conditions, spread, and backtest/demo evidence also support the wider stop. For XAUUSD, high ATR often appears around news, session opens, sharp momentum, or poor liquidity.");
        }

        private static AppLogDetail ExplainBrokerStopData(string original, string message, PriceSummary? summary)
        {
            return Detail(
                original,
                "The bot could not confirm the broker's stop-level or freeze-level rules. These rules say how close SL/TP can be placed and whether orders can be modified near the current price.",
                BuildValues(summary, "Broker stop level: unavailable", "Broker freeze level: unavailable"),
                BuildFormula(summary, "Broker rule check: SL/TP distance must be greater than the broker stop level, and order changes must respect the freeze level. If this data is missing, the safe behavior is to wait."),
                "No trade was opened. Blocking is correct because sending an order without broker limits can cause rejection, missing SL/TP, or uncontrolled execution behavior.",
                BuildExpectedPl(summary),
                "Reload or re-attach the MT5 EA, confirm the exact broker symbol name in Market Watch, and verify the EA returns StopLevelPoints and FreezeLevelPoints.");
        }

        private static AppLogDetail ExplainTradeNotOpened(string original, string message, PriceSummary? summary)
        {
            string reason = ExtractAfter(message, "Trade was not opened:");
            return Detail(
                original,
                "The bot attempted the trade workflow, but final execution did not happen.",
                BuildValues(summary, string.IsNullOrWhiteSpace(reason) ? "Reason: not specified in this single line" : $"Reason: {reason}"),
                BuildFormula(summary, "Final execution requires all checks to pass: connection, account, symbol, spread, news, risk, broker limits, price freshness, and order result."),
                "No position was opened.",
                BuildExpectedPl(summary),
                "Open the nearby previous log lines for the exact blocking check. The best line usually starts with Waiting, BROKER_..., spread, volatility, or Order attempt failed.");
        }

        private static AppLogDetail ExplainTryingTrade(string original, string message, PriceSummary? summary)
        {
            string values = BuildTryingTradeValues(message, summary);
            return Detail(
                original,
                "The bot found a setup and started an order attempt using the shown direction, lot size, entry estimate, stop loss, and take profit.",
                values,
                BuildFormula(summary, "Risk distance = absolute difference between entry and stop loss converted to pips. Reward distance = absolute difference between entry and take profit converted to pips. R:R = reward distance / risk distance."),
                "This line only means the bot tried to start execution. The final done/not-done result appears in the following order-attempt or trade-result log line.",
                BuildExpectedPl(summary),
                "Check the next log line to see whether broker, spread, volatility, risk, and execution checks allowed or blocked the trade.");
        }

        private static AppLogDetail ExplainSessionStarted(string original, string message, PriceSummary? summary)
        {
            return Detail(
                original,
                "A scalping session started with the displayed limits. These are guardrails the bot uses while it keeps checking live conditions.",
                BuildSessionValues(message),
                "During the session the bot refreshes live spread/SL/TP conditions. A trade should only proceed when current market values remain inside these limits.",
                "Session is active. This is not a trade entry by itself.",
                BuildExpectedPl(summary),
                "Monitor following SCALP lines. Waiting messages are normal when spread, volatility, broker data, news, or score is not acceptable.");
        }

        private static AppLogDetail ExplainReviewWarnings(string original, string message, PriceSummary? summary)
        {
            string warnings = ExtractAfter(message, "User confirmed review warnings:");
            return Detail(
                original,
                "The review screen showed warnings and the user accepted them before continuing.",
                BuildValues(summary, string.IsNullOrWhiteSpace(warnings) ? "Warnings: not parsed" : $"Warnings: {warnings}"),
                "Warnings do not bypass risk controls. They only confirm that the operator has seen non-ideal conditions before the bot continues.",
                "The workflow continued, but later trade checks can still block execution.",
                BuildExpectedPl(summary),
                "Treat confirmed warnings seriously. News unavailable, high leverage, or spread near max should usually mean smaller size or no scalping.");
        }

        private static AppLogDetail ExplainReviewTimeout(string original, string message, PriceSummary? summary)
        {
            return Detail(
                original,
                "The UI could not get fresh market snapshot or account data fast enough, so it opened the review screen with available fallback data.",
                BuildValues(summary, "Fresh snapshot/account data: timed out or unavailable", "Fallback data: used for display only"),
                "Professional rule: final trade execution should still require fresh live data. Fallback review data should not be trusted as the final execution truth.",
                "The bot did not necessarily trade. It warned that review data may be incomplete.",
                BuildExpectedPl(summary),
                "Check MT5 connection, EA status, pipe stability, and symbol availability. Do not approve live trades if fresh market/account data is repeatedly timing out.");
        }

        private static AppLogDetail ExplainPipeError(string original, string message, PriceSummary? summary)
        {
            return Detail(
                original,
                "The Windows named-pipe connection between the desktop bot and MT5 EA broke.",
                BuildValues(summary, "Bridge status: pipe broken", "MT5 data/order channel: interrupted"),
                "The bot depends on the pipe for live prices, broker symbol rules, and order responses. If the pipe is broken, execution quality cannot be trusted.",
                "Trading should stop or wait until the bridge reconnects.",
                BuildExpectedPl(summary),
                "Restart or re-attach the EA, confirm MT5 is running, and watch for repeated pipe errors before trading live.");
        }

        private static AppLogDetail ExplainEaReload(string original, string message, PriceSummary? summary)
        {
            return Detail(
                original,
                "The desktop app detected that the MT5 Expert Advisor version or deployment state requires a reload.",
                BuildValues(summary, "EA status: reload required"),
                "The EA supplies broker data such as stop level, freeze level, point size, tick value, and order responses. Old EA code can cause missing or stale values.",
                "Trading should wait until the EA is reloaded.",
                BuildExpectedPl(summary),
                "Remove and re-attach TradingBotEA on the chart, or restart MT5, then confirm broker stop/freeze-level data appears in later logs.");
        }

        private static AppLogDetail ExplainExecutionResult(string original, string message, PriceSummary? summary)
        {
            return Detail(
                original,
                "This appears to be an execution-result log. It indicates the trade workflow reached a broker/order response.",
                BuildValues(summary, "Result text: " + message),
                BuildFormula(summary, "Post-fill validation should compare requested price vs fill price, confirm position ticket, and confirm SL/TP are attached."),
                "Check this line and the following lines for ticket number, fill price, SL/TP attachment, or rejection code.",
                BuildExpectedPl(summary),
                "If the trade was filled, confirm the position exists in MT5 and that SL/TP are attached. If rejected, open the rejection detail line.");
        }

        private static AppLogDetail Generic(string original, string message, PriceSummary? summary)
        {
            return Detail(
                original,
                "This is an informational bot log. It may describe a state change, review action, warning, or execution step.",
                BuildValues(summary, "Message: " + message),
                BuildFormula(summary, "No specific formula was recognized from this line."),
                "No done/blocked result can be proven from this single line.",
                BuildExpectedPl(summary),
                "Open nearby SCALP, Order attempt, Waiting, or Trade was not opened lines for the full decision trail.");
        }

        private static AppLogDetail Detail(
            string original,
            string meaning,
            string values,
            string formula,
            string outcome,
            string expectedPl,
            string nextAction) =>
            new(original, meaning, values, formula, outcome, expectedPl, nextAction);

        private static string BuildTryingTradeValues(string message, PriceSummary? summary)
        {
            var values = new StringBuilder();
            string? side = ReadText(message, @"Trying trade\s*[0-9]+/[0-9]+:\s*(?<v>BUY|SELL)");
            string? symbol = ReadText(message, @"Trying trade\s*[0-9]+/[0-9]+:\s*(?:BUY|SELL)\s*(?<v>[A-Z0-9._-]+)");
            double? lot = ReadDouble(message, $@"lot\s*(?<v>{NumberPattern})");
            double? entry = ReadDouble(message, $@"entry around\s*(?<v>{NumberPattern})");
            double? sl = ReadDouble(message, $@"stop loss\s*(?<v>{NumberPattern})");
            double? tp = ReadDouble(message, $@"take profit\s*(?<v>{NumberPattern})");

            AppendText(values, "Side", side);
            AppendText(values, "Symbol", symbol);
            AppendValue(values, "Lot", lot, "");
            AppendValue(values, "Entry estimate", entry, "");
            AppendValue(values, "Stop loss price", sl, "");
            AppendValue(values, "Take profit price", tp, "");
            AppendSummary(values, summary);
            return values.ToString();
        }

        private static string BuildSessionValues(string message)
        {
            var values = new StringBuilder();
            AppendText(values, "Mode", ReadText(message, @"mode=(?<v>[A-Za-z]+)"));
            AppendValue(values, "Lot", ReadDouble(message, $@"lot=(?<v>{NumberPattern})"), "");
            AppendValue(values, "Max trades", ReadDouble(message, $@"maxTrades=(?<v>{NumberPattern})"), "");
            AppendValue(values, "Max minutes", ReadDouble(message, $@"maxMinutes=(?<v>{NumberPattern})"), "minutes");
            AppendValue(values, "SL", ReadDouble(message, $@"SL=(?<v>{NumberPattern})p"), "pips");
            AppendValue(values, "TP", ReadDouble(message, $@"TP=(?<v>{NumberPattern})p"), "pips");
            AppendValue(values, "Max spread", ReadDouble(message, $@"maxSpread=(?<v>{NumberPattern})p"), "pips");
            AppendValue(values, "Minimum score", ReadDouble(message, $@"score>=(?<v>{NumberPattern})"), "");
            return values.Length == 0 ? "Session values were not parsed from this line." : values.ToString();
        }

        private static string BuildValues(PriceSummary? summary, params string[] extra)
        {
            var values = new StringBuilder();
            foreach (string item in extra.Where(static x => !string.IsNullOrWhiteSpace(x)))
                values.AppendLine(item);
            AppendSummary(values, summary);
            return values.ToString().Trim();
        }

        private static string BuildFormula(PriceSummary? summary, string formula)
        {
            if (summary == null)
                return formula;

            double rr = summary.RiskMoney > 0 ? summary.ProfitMoney / summary.RiskMoney : 0;
            double spreadShare = summary.TpPips > 0 ? summary.SpreadPips / summary.TpPips * 100.0 : 0;
            return string.Create(CultureInfo.InvariantCulture,
                $"{formula}\n\nFrom this line:\nR:R = TP dollars / SL dollars = {summary.ProfitMoney:0.##} / {summary.RiskMoney:0.##} = {rr:0.00}R.\nSpread share of TP = spread pips / TP pips = {summary.SpreadPips:0.##} / {summary.TpPips:0.##} = {spreadShare:0.0}%.");
        }

        private static string BuildExpectedPl(PriceSummary? summary)
        {
            if (summary == null)
                return "This log line does not include enough lot, pip value, SL, and TP data to estimate P/L.";

            double rr = summary.RiskMoney > 0 ? summary.ProfitMoney / summary.RiskMoney : 0;
            return string.Create(CultureInfo.InvariantCulture,
                $"If this exact setup were opened and SL hit: about -${summary.RiskMoney:0.##}.\nIf TP hit: about +${summary.ProfitMoney:0.##}.\nDistance: SL {summary.SlPips:0.##} pips, TP {summary.TpPips:0.##} pips.\nApprox R:R: {rr:0.00}R.\nThis is only an estimate from the log values; real live P/L can change from spread, commission, swap, slippage, contract size, and broker tick value.");
        }

        private static void AppendSummary(StringBuilder values, PriceSummary? summary)
        {
            if (summary == null) return;

            AppendValue(values, "Bid", summary.Bid, "");
            AppendValue(values, "Ask", summary.Ask, "");
            AppendValue(values, "Spread", summary.SpreadPips, "pips");
            AppendValue(values, "Max spread", summary.MaxSpreadPips, "pips");
            AppendValue(values, "Lot", summary.Lot, "");
            AppendValue(values, "Approx pip value", summary.PipValue, "USD per pip");
            AppendValue(values, "Risk at SL", summary.RiskMoney, "USD");
            AppendValue(values, "Profit at TP", summary.ProfitMoney, "USD");
        }

        private static PriceSummary? ParsePriceSummary(string message)
        {
            var match = PriceSummaryRegex.Match(message);
            if (!match.Success) return null;

            return new PriceSummary(
                Number(match, "bid"),
                Number(match, "ask"),
                Number(match, "spread"),
                Number(match, "maxSpread"),
                Number(match, "lot"),
                Number(match, "pipValue"),
                Number(match, "slPips"),
                Number(match, "risk"),
                Number(match, "tpPips"),
                Number(match, "profit"));
        }

        private static double Number(Match match, string group) =>
            double.TryParse(match.Groups[group].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                ? value
                : double.NaN;

        private static double? ReadDouble(string text, string pattern)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (!match.Success) return null;

            return double.TryParse(match.Groups["v"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                ? value
                : null;
        }

        private static string? ReadText(string text, string pattern)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            return match.Success ? match.Groups["v"].Value.Trim() : null;
        }

        private static string ExtractAfter(string text, string marker)
        {
            int index = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            return index < 0 ? "" : text[(index + marker.Length)..].Trim();
        }

        private static void AppendValue(StringBuilder values, string label, double? value, string suffix)
        {
            if (value == null) return;
            values.Append(label)
                .Append(": ")
                .Append(value.Value.ToString("0.#####", CultureInfo.InvariantCulture));
            if (!string.IsNullOrWhiteSpace(suffix))
                values.Append(' ').Append(suffix);
            values.AppendLine();
        }

        private static void AppendText(StringBuilder values, string label, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            values.Append(label).Append(": ").AppendLine(value);
        }

        private static bool Contains(string text, string value) =>
            text.Contains(value, StringComparison.OrdinalIgnoreCase);

        private static string Clean(string rawLine) =>
            Regex.Replace(rawLine.Replace('\r', ' ').Replace('\n', ' '), @"\s+", " ").Trim();

        private static string StripTimestamp(string line)
        {
            var match = Regex.Match(line, @"^\[[0-9:\-\s.]+\]\s*(?<msg>.*)$");
            return match.Success ? match.Groups["msg"].Value.Trim() : line;
        }

        private sealed record PriceSummary(
            double Bid,
            double Ask,
            double SpreadPips,
            double MaxSpreadPips,
            double Lot,
            double PipValue,
            double SlPips,
            double RiskMoney,
            double TpPips,
            double ProfitMoney);
    }
}
