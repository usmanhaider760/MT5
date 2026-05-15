using MT5GoldScalper.V2.Core.Abstractions;
using MT5GoldScalper.V2.Core.Configuration;
using MT5GoldScalper.V2.Core.Models;

namespace MT5GoldScalper.V2.Core.Engines;

public sealed class DecisionEngine(DecisionMakerOptions options) : IDecisionEngine
{
    public TradingDecisionSnapshot Evaluate(TradingDecisionSnapshot snapshot)
    {
        snapshot.BlockReasons = BuildBlockReasons(snapshot);
        snapshot.ConfidenceScore = Score(snapshot);

        snapshot.TradeDirection = ToTradeDirection(snapshot.StrategySignal.SetupDirection);
        snapshot.SignalDecision = EvaluateSignalDecision(snapshot);

        if (snapshot.BlockReasons.Any(reason => reason.IsHardBlock))
        {
            snapshot.ExecutionReadiness = ExecutionReadiness.Blocked;
        }
        else if (snapshot.SignalDecision is SignalDecision.Buy or SignalDecision.Sell)
        {
            snapshot.ExecutionReadiness = ExecutionReadiness.Ready;
        }
        else
        {
            snapshot.ExecutionReadiness = ExecutionReadiness.Review;
        }

        snapshot.Sections = BuildSections(snapshot);
        return snapshot;
    }

    private SignalDecision EvaluateSignalDecision(TradingDecisionSnapshot snapshot)
    {
        if (snapshot.TradeDirection == TradeDirection.None)
        {
            return SignalDecision.Skip;
        }

        if (snapshot.ConfidenceScore >= options.MinConfidenceToTrade)
        {
            return snapshot.TradeDirection == TradeDirection.Buy ? SignalDecision.Buy : SignalDecision.Sell;
        }

        return snapshot.ConfidenceScore >= options.MinConfidenceToWatch
            ? SignalDecision.Wait
            : SignalDecision.Skip;
    }

    private static TradeDirection ToTradeDirection(string setupDirection) =>
        setupDirection.ToUpperInvariant() switch
        {
            "BUY" => TradeDirection.Buy,
            "SELL" => TradeDirection.Sell,
            _ => TradeDirection.None
        };

    private static List<BlockReason> BuildBlockReasons(TradingDecisionSnapshot s)
    {
        var reasons = new List<BlockReason>();

        if (!s.ExecutionSafety.TerminalConnected) reasons.Add(Reason(BlockReasonCode.TerminalDisconnected, "MT5 terminal is not connected.", "MT5"));
        if (!s.ExecutionSafety.TradingAllowed) reasons.Add(Reason(BlockReasonCode.TradingDisabled, "Trading is disabled.", "MT5"));
        if (!s.ExecutionSafety.MarketOpen) reasons.Add(Reason(BlockReasonCode.MarketClosed, "Market is closed.", "MARKET"));
        if (!s.ExecutionSafety.PriceFresh) reasons.Add(Reason(BlockReasonCode.StalePrice, "Price feed is stale.", "MARKET"));
        if (!s.SessionNews.IsSessionAllowed) reasons.Add(Reason(BlockReasonCode.SessionNotAllowed, "Current session is not allowed.", "SESSION"));
        if (!s.ExecutionSafety.NewsFilterPass || s.SessionNews.NewsBlackoutActive) reasons.Add(Reason(BlockReasonCode.NewsBlackout, "High-impact news blackout is active.", "NEWS"));
        if (!s.ExecutionSafety.SpreadAcceptable) reasons.Add(Reason(BlockReasonCode.SpreadTooWide, "Spread is wider than policy allows.", "MARKET"));
        if (!s.ExecutionSafety.MarginEnough) reasons.Add(Reason(BlockReasonCode.InsufficientMargin, "Insufficient free margin.", "RISK"));
        if (!s.ExecutionSafety.RiskLimitPass) reasons.Add(Reason(BlockReasonCode.RiskLimitReached, "Daily risk limits or trade count limits were hit.", "RISK"));
        if (!s.ExecutionSafety.DuplicateTradePass) reasons.Add(Reason(BlockReasonCode.DuplicateTrade, "Duplicate or same-pair trade is blocked.", "RISK"));
        if (!s.ExecutionSafety.StopsValid) reasons.Add(Reason(BlockReasonCode.InvalidStops, "Stop-loss / take-profit distances are invalid.", "RISK"));
        if (!s.ExecutionSafety.VolumeValid) reasons.Add(Reason(BlockReasonCode.InvalidVolume, "Lot size is outside broker limits.", "RISK"));
        if (!s.ExecutionSafety.OrderCheckPassed) reasons.Add(Reason(BlockReasonCode.OrderCheckFailed, "Broker-side order check failed.", "MT5"));

        return reasons;
    }

    private static BlockReason Reason(BlockReasonCode code, string message, string source) =>
        new()
        {
            Code = code,
            Message = message,
            Severity = "BLOCK",
            Source = source,
            IsHardBlock = true
        };

    private static decimal Score(TradingDecisionSnapshot s)
    {
        decimal score = 0;

        if (s.StrategySignal.TrendAligned) score += 20;
        if (s.StrategySignal.AlmaPass) score += 15;
        if (s.StrategySignal.RsiPass) score += 10;
        if (s.StrategySignal.AtrPass) score += 10;
        if (s.StrategySignal.BollingerPass) score += 10;
        if (s.StrategySignal.LiquiditySweepFound) score += 15;
        if (s.StrategySignal.MomentumCandleFound) score += 10;
        if (s.StrategySignal.ConfirmationCandleClosed) score += 10;

        return Math.Min(score, 100);
    }

    private static List<DecisionSectionModel> BuildSections(TradingDecisionSnapshot s)
    {
        static DecisionCheckModel Check(string label, string value, decimal score, string source, string note) =>
            new() { Label = label, Value = value, Score = score, Source = source, Note = note };

        decimal marketScore = s.ExecutionSafety.SpreadAcceptable && s.ExecutionSafety.PriceFresh ? 90 : 40;
        decimal newsScore = s.SessionNews.NewsBlackoutActive ? 10 : 90;
        decimal signalScore = s.ConfidenceScore;
        decimal executionScore = s.ExecutionSafety.FinalReadyToTrade ? 95 : 35;
        decimal verdictScore = s.BlockReasons.Any(reason => reason.IsHardBlock) ? 0 : s.ConfidenceScore;

        return
        [
            new()
            {
                Title = "Market",
                Icon = "M",
                Status = marketScore >= 70 ? "PASS" : "WATCH",
                Severity = marketScore >= 70 ? "Good" : "Watch",
                Score = marketScore,
                Summary =
                [
                    Check("Bid/Ask", $"{s.Market.Bid:F5} / {s.Market.Ask:F5}", 90, "AUTO", "From data service"),
                    Check("Spread", $"{s.Market.SpreadPips:0.0} pips", s.ExecutionSafety.SpreadAcceptable ? 90 : 20, "AUTO", "Compared with max spread policy"),
                    Check("Tick age", $"{s.Market.LastTickAgeMs} ms", s.ExecutionSafety.PriceFresh ? 90 : 20, "AUTO", "Freshness gate")
                ],
                Details =
                [
                    Check("Current price", $"{s.Market.CurrentPrice:F5}", 90, "AUTO", "Display value"),
                    Check("Market open", s.Market.MarketOpen ? "Yes" : "No", s.Market.MarketOpen ? 100 : 0, "AUTO", "Execution precondition"),
                    Check("Stops level", $"{s.Market.StopsLevelPoints} pts", 80, "AUTO", "Broker metadata"),
                    Check("Freeze level", $"{s.Market.FreezeLevelPoints} pts", 80, "AUTO", "Broker metadata")
                ]
            },
            new()
            {
                Title = "Session & News",
                Icon = "N",
                Status = newsScore >= 70 ? "PASS" : "BLOCK",
                Severity = newsScore >= 70 ? "Good" : "Blocked",
                Score = newsScore,
                Summary =
                [
                    Check("Session", s.SessionNews.CurrentSession, s.SessionNews.IsSessionAllowed ? 90 : 10, "AUTO", "Allowed-session check"),
                    Check("News blackout", s.SessionNews.NewsBlackoutActive ? "Active" : "Inactive", s.SessionNews.NewsBlackoutActive ? 0 : 100, "API", "Upcoming high-impact event"),
                    Check("Next event", s.SessionNews.NextHighImpactEvent, s.SessionNews.NewsBlackoutActive ? 10 : 90, "API", "Headline in use")
                ],
                Details =
                [
                    Check("UTC time", s.SessionNews.UtcTime.ToString("u"), 80, "AUTO", "Display only"),
                    Check("Minutes to news", s.SessionNews.MinutesToNews.ToString(), s.SessionNews.NewsBlackoutActive ? 10 : 90, "API", "Policy gate"),
                    Check("Impact", s.SessionNews.NewsImpact, s.SessionNews.NewsBlackoutActive ? 10 : 80, "API", "Risk context")
                ]
            },
            new()
            {
                Title = "Signal",
                Icon = "S",
                Status = signalScore >= 75 ? "STRONG" : signalScore >= 55 ? "WATCH" : "WEAK",
                Severity = signalScore >= 75 ? "Good" : signalScore >= 55 ? "Watch" : "Blocked",
                Score = signalScore,
                Summary =
                [
                    Check("Direction", s.StrategySignal.SetupDirection, 80, "AUTO", "Signal bias"),
                    Check("HTF trend", s.StrategySignal.HigherTimeframeTrend, s.StrategySignal.TrendAligned ? 100 : 30, "AUTO", "Bias alignment"),
                    Check("RSI", $"{s.StrategySignal.RsiValue:0.##}", s.StrategySignal.RsiPass ? 80 : 25, "AUTO", "Momentum filter"),
                    Check("ALMA", s.StrategySignal.AlmaPass ? "Pass" : "Fail", s.StrategySignal.AlmaPass ? 90 : 20, "AUTO", "Trend confirmation")
                ],
                Details =
                [
                    Check("Liquidity sweep", s.StrategySignal.LiquiditySweepFound ? s.StrategySignal.LiquiditySweepSide : "None", s.StrategySignal.LiquiditySweepFound ? 90 : 20, "AUTO", "Structure context"),
                    Check("Momentum candle", s.StrategySignal.MomentumCandleFound ? "Found" : "Not found", s.StrategySignal.MomentumCandleFound ? 90 : 20, "AUTO", "Entry trigger"),
                    Check("Confirmation close", s.StrategySignal.ConfirmationCandleClosed ? "Closed" : "Missing", s.StrategySignal.ConfirmationCandleClosed ? 90 : 20, "AUTO", "Entry readiness"),
                    Check("RR to TP2", $"1:{s.StrategySignal.RiskRewardTp2:0.0}", s.StrategySignal.RiskRewardTp2 >= 2m ? 90 : 40, "AUTO", "Exit quality")
                ]
            },
            new()
            {
                Title = "Execution Safety",
                Icon = "E",
                Status = executionScore >= 70 ? "READY" : "BLOCK",
                Severity = executionScore >= 70 ? "Good" : "Blocked",
                Score = executionScore,
                Summary =
                [
                    Check("Terminal", s.ExecutionSafety.TerminalConnected ? "Connected" : "Disconnected", s.ExecutionSafety.TerminalConnected ? 100 : 0, "AUTO", "Prototype value"),
                    Check("Margin", s.ExecutionSafety.MarginEnough ? "Enough" : "Low", s.ExecutionSafety.MarginEnough ? 100 : 0, "AUTO", "Free margin check"),
                    Check("Order check", s.ExecutionSafety.OrderCheckComment, s.ExecutionSafety.OrderCheckPassed ? 100 : 0, "AUTO", "Broker gate placeholder")
                ],
                Details =
                [
                    Check("Volume valid", s.ExecutionSafety.VolumeValid ? "Yes" : "No", s.ExecutionSafety.VolumeValid ? 100 : 0, "AUTO", "Min/max lot"),
                    Check("Stops valid", s.ExecutionSafety.StopsValid ? "Yes" : "No", s.ExecutionSafety.StopsValid ? 100 : 0, "AUTO", "SL/TP distances"),
                    Check("Risk limits", s.ExecutionSafety.RiskLimitPass ? "Pass" : "Fail", s.ExecutionSafety.RiskLimitPass ? 100 : 0, "CFG", "Daily guardrails"),
                    Check("Duplicate trade", s.ExecutionSafety.DuplicateTradePass ? "Pass" : "Blocked", s.ExecutionSafety.DuplicateTradePass ? 100 : 0, "AUTO", "Same-pair rule")
                ]
            },
            new()
            {
                Title = "Verdict",
                Icon = "V",
                Status = s.ExecutionReadiness.ToString().ToUpperInvariant(),
                Severity = s.ExecutionReadiness == ExecutionReadiness.Ready ? "Good" : s.ExecutionReadiness == ExecutionReadiness.Review ? "Watch" : "Blocked",
                Score = verdictScore,
                Summary =
                [
                    Check("Decision", s.FinalDecisionText, verdictScore, "AUTO", "Engine output"),
                    Check("Direction", s.TradeDirection.ToString().ToUpperInvariant(), verdictScore, "AUTO", "Signal direction"),
                    Check("Can place trade", s.CanPlaceTrade ? "Yes" : "No", s.CanPlaceTrade ? 100 : 0, "AUTO", "Final execution permission"),
                    Check("Confidence", $"{s.ConfidenceScore:0}%", s.ConfidenceScore, "AUTO", "Soft-rule score")
                ],
                Details = s.BlockReasons.Count == 0
                    ?
                    [
                        Check("Entry", $"{s.StrategySignal.EntryPrice:F5}", 90, "AUTO", "Candidate entry"),
                        Check("Stop loss", $"{s.StrategySignal.StopLossPrice:F5}", 90, "AUTO", "Candidate stop"),
                        Check("TP2", $"{s.StrategySignal.Tp2Price:F5}", 90, "AUTO", "Display TP used in UI")
                    ]
                    : s.BlockReasons.Select(r => Check(r.Code.ToString(), r.Message, 0, r.Source, r.IsHardBlock ? "Hard-rule failure" : "Review item")).ToList()
            }
        ];
    }
}
