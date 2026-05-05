using MT5TradingBot.Core;
using MT5TradingBot.Data;
using MT5TradingBot.Models;
using MT5TradingBot.Modules.BrokerIntegration;
using MT5TradingBot.Modules.NewsFilter;
using Newtonsoft.Json.Linq;

namespace MT5TradingBot.Modules.Scalping
{
    public sealed class ScalpingSessionService : IScalpingSessionService
    {
        private const int ProfessionalMaxScalpsPerSession = 6;
        private const int ProfessionalMinDecisionScore = 6;
        private const double ProfessionalMaxSpreadPercentOfTp = 20.0;
        private const double ScalpSignalExpiryMinutes = 0.17;

        private readonly MT5Bridge _bridge;
        private readonly INewsCalendarService? _newsCalendar;
        private readonly ApiIntegrationConfig _apiConfig;
        private readonly object _gate = new();
        private readonly Queue<double> _atrHistory = new();
        private CancellationTokenSource? _sessionCts;
        private Task? _sessionTask;

        public event Action<string>? OnLog;
        public event Action<string>? OnStatusChanged;
        public bool IsRunning { get; private set; }

        public ScalpingSessionService(
            MT5Bridge bridge,
            INewsCalendarService? newsCalendar,
            ApiIntegrationConfig apiConfig)
        {
            _bridge = bridge;
            _newsCalendar = newsCalendar;
            _apiConfig = apiConfig;
        }

        public Task StartAsync(ScalpingSessionRequest request, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                if (IsRunning)
                    throw new InvalidOperationException("An auto scalping session is already running.");

                _sessionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                IsRunning = true;
                _sessionTask = Task.Run(() => RunAsync(request, _sessionCts.Token), _sessionCts.Token);
            }

            Log(
                $"[SCALP] Started {request.Pair} mode={request.Config.DirectionMode} lot={request.LotSize:F2} " +
                $"maxTrades={request.Config.MaxTrades} maxMinutes={request.Config.MaxMinutes} " +
                $"SL={request.Config.StopLossPips:F1}p TP={request.Config.TakeProfitPips:F1}p " +
                $"maxSpread={request.Config.MaxSpreadPips:F1}p score>={request.Config.MinDecisionScore}");
            Status("Running");
            return Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            CancellationTokenSource? cts;
            Task? task;
            lock (_gate)
            {
                cts = _sessionCts;
                task = _sessionTask;
                _sessionCts = null;
                _sessionTask = null;
                IsRunning = false;
            }

            if (cts != null)
            {
                cts.Cancel();
                cts.Dispose();
            }

            if (task != null)
            {
                try { await task.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
            }

            Status("Stopped");
            Log("[SCALP] Stopped.");
        }

        private async Task RunAsync(ScalpingSessionRequest request, CancellationToken ct)
        {
            var cfg = Normalize(request.Config);
            DateTime started = DateTime.UtcNow;
            DateTime lastTradeAt = DateTime.MinValue;
            int openedTrades = 0;
            double sessionPnl = 0;
            var knownTickets = new Dictionary<long, double>();
            double? lastMid = null;

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    if (!_bridge.IsConnected)
                    {
                        Log("[SCALP] Waiting: MT5 is not connected.");
                        await Delay(cfg, ct).ConfigureAwait(false);
                        continue;
                    }

                    if ((DateTime.UtcNow - started).TotalMinutes >= cfg.MaxMinutes)
                    {
                        Log("[SCALP] Session time limit reached.");
                        break;
                    }

                    if (openedTrades >= cfg.MaxTrades)
                    {
                        Log("[SCALP] Max trades reached.");
                        break;
                    }

                    var positions = await _bridge.GetPositionsAsync().ConfigureAwait(false);
                    sessionPnl += TrackClosedScalps(knownTickets, positions);

                    if (cfg.MaxSessionLossUsd > 0 && sessionPnl <= -cfg.MaxSessionLossUsd)
                    {
                        Log($"[SCALP] Max session loss hit: {sessionPnl:F2} USD.");
                        break;
                    }

                    if (cfg.ProfitTargetUsd > 0 && sessionPnl >= cfg.ProfitTargetUsd)
                    {
                        Log($"[SCALP] Profit target hit: {sessionPnl:F2} USD.");
                        break;
                    }

                    var symbol = await _bridge.GetSymbolInfoAsync(request.Pair).ConfigureAwait(false);
                    if (symbol == null)
                    {
                        Log($"[SCALP] Waiting: no symbol info for {request.Pair}.");
                        await Delay(cfg, ct).ConfigureAwait(false);
                        continue;
                    }

                    var liveConfig = ResolveLiveScalpingConfig(cfg, symbol, snapshot: null);
                    if (!liveConfig.CanTrade)
                    {
                        LogScalpSeparator();
                        Log($"[SCALP] Waiting: {liveConfig.Reason} {BuildPriceSummary(request, liveConfig.Config, symbol)}");
                        await Delay(cfg, ct).ConfigureAwait(false);
                        continue;
                    }

                    var effectiveCfg = liveConfig.Config;
                    if (symbol.SpreadPips > effectiveCfg.MaxSpreadPips)
                    {
                        LogScalpSeparator();
                        Log(
                            $"[SCALP] Waiting: spread is too high ({symbol.SpreadPips:F1} pips, allowed {effectiveCfg.MaxSpreadPips:F1}). " +
                            BuildPriceSummary(request, effectiveCfg, symbol));
                        await Delay(cfg, ct).ConfigureAwait(false);
                        continue;
                    }

                    if (_newsCalendar != null)
                    {
                        var news = await _newsCalendar.GetRiskSnapshotAsync(request.Pair, _apiConfig).ConfigureAwait(false);
                        if (news.IsConfigured && (news.IsBlackoutActive || news.HighImpactNext60Minutes))
                        {
                            LogScalpSeparator();
                            Log($"[SCALP] Waiting: news risk is active ({news.RiskLevel}). {news.Reason}");
                            await Delay(cfg, ct).ConfigureAwait(false);
                            continue;
                        }
                    }

                    bool hasOpenScalp = positions.Any(p =>
                        p.MagicNumber == request.MagicNumber &&
                        p.Symbol.Equals(request.Pair, StringComparison.OrdinalIgnoreCase) &&
                        p.Comment.Contains("AutoScalp", StringComparison.OrdinalIgnoreCase));
                    if (hasOpenScalp && !cfg.AllowPyramiding)
                    {
                        LogScalpSeparator();
                        Log($"[SCALP] Waiting: there is already an AutoScalp trade open for this pair. Open positions: {positions.Count}.");
                        await Delay(cfg, ct).ConfigureAwait(false);
                        continue;
                    }

                    if ((DateTime.UtcNow - lastTradeAt).TotalSeconds < cfg.CooldownSeconds)
                    {
                        double secondsLeft = cfg.CooldownSeconds - (DateTime.UtcNow - lastTradeAt).TotalSeconds;
                        LogScalpSeparator();
                        Log($"[SCALP] Waiting: cooling down after the last trade. Next check in about {Math.Max(0, secondsLeft):F0}s.");
                        await Delay(cfg, ct).ConfigureAwait(false);
                        continue;
                    }

                    double mid = (symbol.Ask + symbol.Bid) / 2.0;
                    TradeType probeDirection = ResolveProbeDirection(effectiveCfg.DirectionMode, request.SignalDirection);
                    var probe = BuildRequest(request, effectiveCfg, symbol, probeDirection);
                    JObject? snapshot = null;
                    if (effectiveCfg.DirectionMode == ScalpingDirectionMode.Auto || effectiveCfg.RequireSnapshotConfirmation || request.ConfirmWithAiAsync != null)
                    {
                        try { snapshot = await _bridge.GetMarketSnapshotAsync(probe, new BotConfig { MaxSpreadPips = effectiveCfg.MaxSpreadPips }).ConfigureAwait(false); }
                        catch (Exception ex) { Log($"[SCALP] Snapshot unavailable: {ex.Message}"); }
                    }

                    if (snapshot != null)
                    {
                        liveConfig = ResolveLiveScalpingConfig(cfg, symbol, snapshot);
                        if (!liveConfig.CanTrade)
                        {
                            LogScalpSeparator();
                            Log($"[SCALP] Waiting: {liveConfig.Reason} {BuildPriceSummary(request, liveConfig.Config, symbol)}");
                            await Delay(cfg, ct).ConfigureAwait(false);
                            continue;
                        }

                        effectiveCfg = liveConfig.Config;
                    }

                    if (LiveValuesChanged(cfg, effectiveCfg))
                    {
                        Log(
                            $"[SCALP] Live values adjusted from saved settings: " +
                            $"SL {cfg.StopLossPips:F1}->{effectiveCfg.StopLossPips:F1} pips, " +
                            $"TP {cfg.TakeProfitPips:F1}->{effectiveCfg.TakeProfitPips:F1} pips, " +
                            $"max spread {cfg.MaxSpreadPips:F1}->{effectiveCfg.MaxSpreadPips:F1} pips.");
                    }

                    // ── ATR spike filter ──────────────────────────────────────
                    if (snapshot != null)
                    {
                        double currentAtr = ReadAtrPips(snapshot, symbol);
                        if (!double.IsNaN(currentAtr) && currentAtr > 0)
                        {
                            _atrHistory.Enqueue(currentAtr);
                            if (_atrHistory.Count > 20)
                                _atrHistory.Dequeue();

                            if (_atrHistory.Count >= 5)
                            {
                                double atrAvg = _atrHistory.Average();
                                if (currentAtr > atrAvg * 2.5)
                                {
                                    LogScalpSeparator();
                                    Log($"[SCALP] ATR spike: {currentAtr:F1} pips > 2.5× avg {atrAvg:F1} pips — waiting.");
                                    await Delay(cfg, ct).ConfigureAwait(false);
                                    continue;
                                }
                            }
                        }
                    }

                    var decision = snapshot != null
                        ? ResolveScalpingDecision(snapshot, effectiveCfg, probeDirection)
                        : ResolvePriceMovementDecision(effectiveCfg, probeDirection, mid, lastMid);
                    lastMid = mid;

                    LogScalpSeparator();
                    Log(BuildDecisionTrace(request, effectiveCfg, symbol, decision, snapshot));

                    if (!decision.Approved)
                    {
                        Log($"[SCALP] Waiting: {decision.Reason}.");
                        await Delay(cfg, ct).ConfigureAwait(false);
                        continue;
                    }

                    if (effectiveCfg.UseAiConfirmation && snapshot != null && request.ConfirmWithAiAsync != null)
                    {
                        var ai = await request.ConfirmWithAiAsync(snapshot, decision.Direction).ConfigureAwait(false);
                        if (!ai.Approved)
                        {
                            Log($"[SCALP] AI said do not trade: {ai.Reason}");
                            await Delay(cfg, ct).ConfigureAwait(false);
                            continue;
                        }
                        Log($"[SCALP] AI agreed with the trade: {ai.Reason}");
                    }

                    var trade = BuildRequest(request, effectiveCfg, symbol, decision.Direction, decision);
                    Log(
                        $"[SCALP] Trying trade {openedTrades + 1}/{effectiveCfg.MaxTrades}: {trade.TradeType} {trade.Pair}, " +
                        $"lot {trade.LotSize:F2}, entry around {(trade.TradeType == TradeType.BUY ? symbol.Ask : symbol.Bid):F5}, " +
                        $"stop loss {trade.StopLoss:F5}, take profit {trade.TakeProfit:F5}.");
                    var result = await request.ExecuteAsync(trade).ConfigureAwait(false);
                    if (result.IsSuccess)
                    {
                        openedTrades++;
                        lastTradeAt = DateTime.UtcNow;
                        knownTickets[result.Ticket] = 0;
                        Log($"[SCALP] Opened #{result.Ticket} {trade.TradeType} {trade.Pair}.");
                    }
                    else
                    {
                        Log($"[SCALP] Trade was not opened: {result.ErrorMessage} ({result.ErrorCode}).");
                        if (IsSessionStoppingError(result.ErrorCode))
                        {
                            Log("[SCALP] Stopping auto scalping because the daily trade limit has been reached.");
                            break;
                        }
                    }

                    await Delay(cfg, ct).ConfigureAwait(false);

                    if (request.GetSessionProfitAsync != null)
                    {
                        try { sessionPnl = await request.GetSessionProfitAsync(started).ConfigureAwait(false); }
                        catch { }
                    }
                }
            }
            finally
            {
                IsRunning = false;
                Status("Stopped");
            }
        }

        private static ScalpingConfig Normalize(ScalpingConfig cfg) => new()
        {
            MaxTrades = Math.Clamp(cfg.MaxTrades, 1, ProfessionalMaxScalpsPerSession),
            MaxMinutes = Math.Clamp(cfg.MaxMinutes, 1, 90),
            MaxSessionLossUsd = Math.Max(0, cfg.MaxSessionLossUsd),
            ProfitTargetUsd = Math.Max(0, cfg.ProfitTargetUsd),
            StopLossPips = Math.Max(1, cfg.StopLossPips),
            TakeProfitPips = Math.Max(1, cfg.TakeProfitPips),
            MaxSpreadPips = Math.Max(0.1, cfg.MaxSpreadPips),
            DynamicValuesEnabled = cfg.DynamicValuesEnabled,
            PollIntervalMs = Math.Clamp(cfg.PollIntervalMs, 500, 10000),
            CooldownSeconds = Math.Clamp(cfg.CooldownSeconds, 1, 600),
            DirectionMode = cfg.DirectionMode,
            AllowPyramiding = cfg.AllowPyramiding
            ,
            RequireSnapshotConfirmation = cfg.RequireSnapshotConfirmation,
            MinDecisionScore = Math.Clamp(Math.Max(cfg.MinDecisionScore, ProfessionalMinDecisionScore), 1, 10),
            UseAiConfirmation = cfg.UseAiConfirmation
        };

        private static LiveScalpingConfigResult ResolveLiveScalpingConfig(
            ScalpingConfig cfg,
            SymbolInfo symbol,
            JObject? snapshot)
        {
            if (!cfg.DynamicValuesEnabled)
                return new LiveScalpingConfigResult(cfg, true, "");

            double spread = IsFinitePositive(symbol.SpreadPips) ? symbol.SpreadPips : 0;
            if (spread > cfg.MaxSpreadPips)
            {
                return new LiveScalpingConfigResult(
                    cfg,
                    false,
                    $"spread {spread:F1} pips is above the user ceiling {cfg.MaxSpreadPips:F1} pips.");
            }

            var brokerLevels = ResolveBrokerLevelPips(symbol, snapshot);
            if (!brokerLevels.HasCompleteData && snapshot != null)
            {
                return new LiveScalpingConfigResult(
                    cfg,
                    false,
                    "broker stop/freeze-level data is unavailable from symbol info and market snapshot.");
            }

            double slPips = Math.Max(1, cfg.StopLossPips);
            double tpPips = Math.Max(1, cfg.TakeProfitPips);

            double brokerMinimum = brokerLevels.HasCompleteData
                ? Math.Max(brokerLevels.StopLevelPips, brokerLevels.FreezeLevelPips) + 1.0
                : 0;
            double requiredSl = Math.Max(1, Math.Max(spread * 2.5, brokerMinimum));

            if (requiredSl > slPips + 1e-9)
            {
                return new LiveScalpingConfigResult(
                    cfg,
                    false,
                    $"live conditions require about {requiredSl:F1} SL pips, above the Trade Page SL {slPips:F1} pips.");
            }

            double preferredRr = Math.Max(0.1, cfg.RiskRewardRatio);
            double requiredTp = Math.Max(slPips * preferredRr, spread * (100.0 / ProfessionalMaxSpreadPercentOfTp));
            if (requiredTp > tpPips + 1e-9)
            {
                return new LiveScalpingConfigResult(
                    cfg,
                    false,
                    $"live conditions require about {requiredTp:F1} TP pips to preserve spread/R:R rules, above the Trade Page TP {tpPips:F1} pips.");
            }

            double maxSpreadPips = RoundHalfPip(Math.Min(
                cfg.MaxSpreadPips,
                Math.Min(
                    tpPips * ProfessionalMaxSpreadPercentOfTp / 100.0,
                    Math.Max(spread + 1.0, spread * 1.25))));

            var effective = CopyScalpingConfig(cfg);
            effective.StopLossPips = slPips;
            effective.TakeProfitPips = tpPips;
            effective.MaxSpreadPips = Math.Max(0.1, maxSpreadPips);

            return new LiveScalpingConfigResult(effective, true, "");
        }

        private static ScalpingConfig CopyScalpingConfig(ScalpingConfig cfg) => new()
        {
            MaxTrades = cfg.MaxTrades,
            MaxMinutes = cfg.MaxMinutes,
            MaxSessionLossUsd = cfg.MaxSessionLossUsd,
            ProfitTargetUsd = cfg.ProfitTargetUsd,
            StopLossPips = cfg.StopLossPips,
            TakeProfitPips = cfg.TakeProfitPips,
            MaxSpreadPips = cfg.MaxSpreadPips,
            DynamicValuesEnabled = cfg.DynamicValuesEnabled,
            PollIntervalMs = cfg.PollIntervalMs,
            CooldownSeconds = cfg.CooldownSeconds,
            DirectionMode = cfg.DirectionMode,
            AllowPyramiding = cfg.AllowPyramiding,
            RequireSnapshotConfirmation = cfg.RequireSnapshotConfirmation,
            MinDecisionScore = cfg.MinDecisionScore,
            UseAiConfirmation = cfg.UseAiConfirmation
        };

        private static double ReadAtrPips(JObject? snapshot, SymbolInfo symbol)
        {
            if (snapshot == null)
                return double.NaN;

            double atr = ReadNumber(snapshot, "indicators.m5.atr");
            if (!IsFinitePositive(atr))
                atr = ReadNumber(snapshot, "indicators.m15.atr");
            if (!IsFinitePositive(atr))
                return double.NaN;

            double? pointSize = symbol.EffectivePointSize;
            if (!pointSize.HasValue || pointSize.Value <= 0)
                return double.NaN;

            double pipSize = symbol.Digits is 3 or 5
                ? pointSize.Value * 10.0
                : pointSize.Value;

            return pipSize > 0 ? atr / pipSize : double.NaN;
        }

        private static BrokerLevelPips ResolveBrokerLevelPips(SymbolInfo symbol, JObject? snapshot)
        {
            double? stopPips = symbol.StopLevelPips;
            double? freezePips = symbol.FreezeLevelPips;
            double symbolStopPips = stopPips.GetValueOrDefault(double.NaN);
            double symbolFreezePips = freezePips.GetValueOrDefault(double.NaN);
            if (IsFiniteNonNegative(symbolStopPips) &&
                IsFiniteNonNegative(symbolFreezePips))
            {
                return new BrokerLevelPips(symbolStopPips, symbolFreezePips, true);
            }

            if (snapshot == null)
                return new BrokerLevelPips(0, 0, false);

            double stopPoints = ReadNumber(snapshot, "symbol.stop_level");
            double freezePoints = ReadNumber(snapshot, "symbol.freeze_level");
            double pointSize = ReadNumber(snapshot, "symbol.point_size");
            double pipSize = ReadNumber(snapshot, "symbol.pip_size");

            if (!IsFinitePositive(pointSize))
                pointSize = symbol.EffectivePointSize ?? double.NaN;
            if (!IsFinitePositive(pipSize))
            {
                double fallbackPoint = symbol.EffectivePointSize ?? double.NaN;
                pipSize = symbol.Digits is 3 or 5
                    ? fallbackPoint * 10.0
                    : fallbackPoint;
            }

            if (!IsFiniteNonNegative(stopPoints) ||
                !IsFiniteNonNegative(freezePoints) ||
                !IsFinitePositive(pointSize) ||
                !IsFinitePositive(pipSize))
            {
                return new BrokerLevelPips(0, 0, false);
            }

            return new BrokerLevelPips(
                stopPoints * pointSize / pipSize,
                freezePoints * pointSize / pipSize,
                true);
        }

        private static bool LiveValuesChanged(ScalpingConfig saved, ScalpingConfig effective) =>
            Math.Abs(saved.StopLossPips - effective.StopLossPips) > 0.05 ||
            Math.Abs(saved.TakeProfitPips - effective.TakeProfitPips) > 0.05 ||
            Math.Abs(saved.MaxSpreadPips - effective.MaxSpreadPips) > 0.05;

        private static double RoundHalfPip(double value) =>
            Math.Round(value * 2.0, MidpointRounding.AwayFromZero) / 2.0;

        private static bool IsFinitePositive(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;

        private static bool IsFiniteNonNegative(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0;

        private static bool IsSessionStoppingError(string? errorCode) =>
            string.Equals(errorCode, "DAILY_LIMIT", StringComparison.OrdinalIgnoreCase);

        private static TradeRequest BuildRequest(
            ScalpingSessionRequest request,
            ScalpingConfig cfg,
            SymbolInfo symbol,
            TradeType direction,
            ScalpingDecision? decision = null)
        {
            double pip = LotCalculator.GetPipSize(request.Pair.ToUpperInvariant());
            double entry = direction == TradeType.BUY ? symbol.Ask : symbol.Bid;
            double slPips = decision?.SuggestedSlPips is > 0 ? decision.SuggestedSlPips.Value : cfg.StopLossPips;
            double tpPips = decision?.SuggestedTpPips is > 0 ? decision.SuggestedTpPips.Value : cfg.TakeProfitPips;
            double sl = direction == TradeType.BUY
                ? entry - slPips * pip
                : entry + slPips * pip;
            double tp = direction == TradeType.BUY
                ? entry + tpPips * pip
                : entry - tpPips * pip;

            return new TradeRequest
            {
                Pair = request.Pair,
                TradeType = direction,
                OrderType = OrderType.MARKET,
                EntryPrice = Math.Round(entry, 5),
                StopLoss = Math.Round(sl, 5),
                TakeProfit = Math.Round(tp, 5),
                LotSize = Math.Max(0.01, request.LotSize),
                MaxSpreadPips = cfg.MaxSpreadPips,
                Comment = decision == null ? "AutoScalp" : $"AutoScalp S{decision.Score}",
                MagicNumber = request.MagicNumber,
                ExpiryMinutes = ScalpSignalExpiryMinutes,
                MoveSLToBreakevenAfterTP1 = true,
                CreatedAt = DateTime.UtcNow
            };
        }

        private static TradeType ResolveProbeDirection(ScalpingDirectionMode mode, TradeType signalDirection) =>
            mode switch
            {
                ScalpingDirectionMode.BuyOnly => TradeType.BUY,
                ScalpingDirectionMode.SellOnly => TradeType.SELL,
                _ => signalDirection
            };

        private static ScalpingDecision ResolveScalpingDecision(
            JObject snapshot,
            ScalpingConfig cfg,
            TradeType signalDirection)
        {
            if (cfg.DirectionMode != ScalpingDirectionMode.Auto)
                return EvaluateSnapshot(snapshot, ResolveProbeDirection(cfg.DirectionMode, signalDirection), cfg);

            var buy = EvaluateSnapshot(snapshot, TradeType.BUY, cfg);
            var sell = EvaluateSnapshot(snapshot, TradeType.SELL, cfg);
            string autoSummary =
                $"Auto direction checked both sides. BUY confirmations {buy.Score}/{cfg.MinDecisionScore}; " +
                $"SELL confirmations {sell.Score}/{cfg.MinDecisionScore}.";
            string bothSides = $"BUY side: {AutoSideSummary(buy)} | SELL side: {AutoSideSummary(sell)}";

            if (buy.Approved && !sell.Approved)
                return buy with { Reason = $"{autoSummary} Choosing BUY because it is the only side that passed.", Detail = $"{buy.Detail} | {bothSides}" };

            if (sell.Approved && !buy.Approved)
                return sell with { Reason = $"{autoSummary} Choosing SELL because it is the only side that passed.", Detail = $"{sell.Detail} | {bothSides}" };

            if (buy.Approved && sell.Approved)
            {
                if (buy.Score > sell.Score)
                    return buy with { Reason = $"{autoSummary} Choosing BUY because it has the stronger score.", Detail = $"{buy.Detail} | {bothSides}" };

                if (sell.Score > buy.Score)
                    return sell with { Reason = $"{autoSummary} Choosing SELL because it has the stronger score.", Detail = $"{sell.Detail} | {bothSides}" };

                return new ScalpingDecision(
                    false,
                    buy.Score,
                    signalDirection,
                    $"{autoSummary} No trade because BUY and SELL are equally strong.",
                    $"{bothSides} | BUY details: {buy.Detail} | SELL details: {sell.Detail}");
            }

            var stronger = buy.Score >= sell.Score ? buy : sell;
            return stronger with
            {
                Approved = false,
                Reason = $"{autoSummary} No trade because neither side passed all safety checks.",
                Detail = $"{bothSides} | Stronger side details: {stronger.Detail}"
            };
        }

        private static ScalpingDecision ResolvePriceMovementDecision(
            ScalpingConfig cfg,
            TradeType probeDirection,
            double currentMid,
            double? previousMid)
        {
            if (cfg.RequireSnapshotConfirmation)
            {
                return new ScalpingDecision(
                    false,
                    0,
                    probeDirection,
                    "full market snapshot confirmation is required before scalping",
                    $"Current middle price {currentMid:F5}; snapshot unavailable");
            }

            if (previousMid == null)
            {
                return new ScalpingDecision(
                    false,
                    0,
                    probeDirection,
                    "waiting for one more price update to compare direction",
                    $"Current middle price {currentMid:F5}; previous middle price not available");
            }

            if (cfg.DirectionMode == ScalpingDirectionMode.Auto)
            {
                TradeType direction = currentMid >= previousMid.Value ? TradeType.BUY : TradeType.SELL;
                return new ScalpingDecision(
                    true,
                    cfg.MinDecisionScore,
                    direction,
                    $"Auto direction used price movement because the full market snapshot was unavailable. Price moved {(direction == TradeType.BUY ? "up" : "down")}, so choosing {direction}.",
                    $"Current middle price {currentMid:F5}; previous middle price {previousMid.Value:F5}");
            }

            bool agrees = DirectionAgrees(probeDirection, currentMid, previousMid.Value);
            return new ScalpingDecision(
                agrees,
                agrees ? cfg.MinDecisionScore : 0,
                probeDirection,
                agrees ? "price is moving in the selected direction" : "price is not moving in the selected direction",
                $"Current middle price {currentMid:F5}; previous middle price {previousMid.Value:F5}");
        }

        private static string AutoSideSummary(ScalpingDecision decision) =>
            decision.Approved
                ? $"{decision.Score} confirmations, passed"
                : decision.Score > 0
                    ? $"{decision.Score} confirmations, blocked: {decision.Reason}"
                    : $"{decision.Score} confirmations, not enough";

        private static bool DirectionAgrees(TradeType direction, double currentMid, double previousMid) =>
            direction == TradeType.BUY ? currentMid >= previousMid : currentMid <= previousMid;

        private static ScalpingDecision EvaluateSnapshot(JObject snapshot, TradeType direction, ScalpingConfig cfg)
        {
            int score = 0;
            var reasons = new List<string>();
            bool buy = direction == TradeType.BUY;
            string want = buy ? "BULLISH" : "BEARISH";

            bool marketOpen = ReadBool(snapshot, "session.market_open", true);
            bool tradeAllowed = ReadBool(snapshot, "symbol.trade_allowed", true);
            if (!marketOpen || !tradeAllowed)
                return new ScalpingDecision(
                    false,
                    0,
                    direction,
                    "the market is closed or trading is disabled",
                    $"Market open: {YesNo(marketOpen)}; trading allowed: {YesNo(tradeAllowed)}");

            double adx = ReadNumber(snapshot, "indicators.m5.adx");
            if (!double.IsNaN(adx) && adx < 20)
                return new ScalpingDecision(
                    false,
                    0,
                    direction,
                    $"ADX {adx:F1} below 20 — market is ranging, no scalp",
                    BuildSnapshotTrace(snapshot, cfg, direction, 0, []));

            string h1Trend = ReadText(snapshot, "structure.trend_h1");
            if (!string.IsNullOrWhiteSpace(h1Trend) && !h1Trend.Contains(want, StringComparison.OrdinalIgnoreCase))
                return new ScalpingDecision(
                    false,
                    0,
                    direction,
                    $"H1 trend is {h1Trend} — against required {want} direction",
                    BuildSnapshotTrace(snapshot, cfg, direction, 0, []));

            ScoreText(snapshot, "structure.trend_m5", want, ref score, reasons, "M5 trend");
            ScoreText(snapshot, "structure.trend_m15", want, ref score, reasons, "M15 trend");
            ScoreText(snapshot, "candles.m5_last.direction", buy ? "BULLISH" : "BEARISH", ref score, reasons, "M5 candle");
            ScoreText(snapshot, "indicators.m5.macd_bias", want, ref score, reasons, "M5 MACD");
            ScoreText(snapshot, "indicators.m15.macd_bias", want, ref score, reasons, "M15 MACD");

            string vsEma20 = ReadText(snapshot, "indicators.m5.price_vs_ema20");
            if (ContainsDirection(vsEma20, buy)) { score++; reasons.Add("M5 price vs EMA20"); }
            string vsEma50 = ReadText(snapshot, "indicators.m5.price_vs_ema50");
            if (ContainsDirection(vsEma50, buy)) { score++; reasons.Add("M5 price vs EMA50"); }

            double rsi = ReadNumber(snapshot, "indicators.m5.rsi");
            if (!double.IsNaN(rsi))
            {
                if (buy && rsi > 70)
                    return new ScalpingDecision(false, score, direction,
                        $"M5 RSI {rsi:F1} overbought — above 70, no buy scalp",
                        BuildSnapshotTrace(snapshot, cfg, direction, score, reasons));
                if (!buy && rsi < 30)
                    return new ScalpingDecision(false, score, direction,
                        $"M5 RSI {rsi:F1} oversold — below 30, no sell scalp",
                        BuildSnapshotTrace(snapshot, cfg, direction, score, reasons));
                if (buy && rsi is >= 45 and <= 70) { score++; reasons.Add("M5 RSI buy zone"); }
                if (!buy && rsi is >= 30 and <= 55) { score++; reasons.Add("M5 RSI sell zone"); }
                if (buy && rsi is >= 52 and <= 65) { score++; reasons.Add("M5 RSI buy momentum"); }
                if (!buy && rsi is >= 35 and <= 48) { score++; reasons.Add("M5 RSI sell momentum"); }
            }

            double stoch = ReadNumber(snapshot, "indicators.m5.stoch_k");
            if (!double.IsNaN(stoch))
            {
                if (buy && stoch < 85) { score++; reasons.Add("Stoch not overbought"); }
                if (!buy && stoch > 15) { score++; reasons.Add("Stoch not oversold"); }
            }

            bool doji = ReadBool(snapshot, "candles.m5_last.is_doji", false);
            bool inside = ReadBool(snapshot, "candles.m5_last.is_inside_bar", false);
            if (doji || inside)
                return new ScalpingDecision(false, score, direction, "the latest M5 candle is not clear enough", BuildSnapshotTrace(snapshot, cfg, direction, score, reasons));

            bool engulfing = ReadBool(snapshot, "candles.m5_last.is_engulfing", false);
            if (engulfing) { score++; reasons.Add("M5 engulfing candle"); }

            bool bosDetected = ReadBool(snapshot, "structure.bos_detected", false);
            if (bosDetected) { score++; reasons.Add("BOS detected"); }

            double supportDist = ReadNumber(snapshot, "levels.distance_to_support_pips");
            double resistanceDist = ReadNumber(snapshot, "levels.distance_to_resistance_pips");
            double targetRoomMultiplier = 1.2;
            if (buy && !double.IsNaN(resistanceDist) && resistanceDist <= cfg.TakeProfitPips * targetRoomMultiplier)
                return new ScalpingDecision(false, score, direction, "not enough room to resistance for the required TP", BuildSnapshotTrace(snapshot, cfg, direction, score, reasons));
            if (!buy && !double.IsNaN(supportDist) && supportDist <= cfg.TakeProfitPips * targetRoomMultiplier)
                return new ScalpingDecision(false, score, direction, "not enough room to support for the required TP", BuildSnapshotTrace(snapshot, cfg, direction, score, reasons));

            if (buy && !double.IsNaN(resistanceDist))
            {
                score++;
                reasons.Add("room to resistance");
            }
            if (!buy && !double.IsNaN(supportDist))
            {
                score++;
                reasons.Add("room to support");
            }

            double bid = ReadNumber(snapshot, "price.bid");
            double vwap = ReadNumber(snapshot, "session.vwap");
            if (!double.IsNaN(vwap) && vwap > 0 && !double.IsNaN(bid) && bid > 0)
            {
                bool aboveVwap = bid > vwap;
                bool vwapAligned = (buy && aboveVwap) || (!buy && !aboveVwap);
                if (vwapAligned) { score++; reasons.Add($"price {(aboveVwap ? "above" : "below")} VWAP — institutional bias aligned"); }
            }

            double asianHigh = ReadNumber(snapshot, "levels.asian_high");
            double asianLow  = ReadNumber(snapshot, "levels.asian_low");
            double currentBid = ReadNumber(snapshot, "price.bid");
            if (!double.IsNaN(asianHigh) && asianHigh > 0 &&
                !double.IsNaN(asianLow) && asianLow > 0 &&
                !double.IsNaN(currentBid))
            {
                if (buy && currentBid > asianHigh) { score++; reasons.Add("price above Asian high — London breakout"); }
                if (!buy && currentBid < asianLow)  { score++; reasons.Add("price below Asian low — London breakout"); }
            }

            bool approved = score >= cfg.MinDecisionScore;
            return new ScalpingDecision(
                approved,
                score,
                direction,
                approved
                    ? string.Join(", ", reasons.Take(6))
                    : $"only {score} of {cfg.MinDecisionScore} required confirmations matched: {string.Join(", ", reasons.Take(4))}",
                BuildSnapshotTrace(snapshot, cfg, direction, score, reasons));
        }

        private static string BuildDecisionTrace(
            ScalpingSessionRequest request,
            ScalpingConfig cfg,
            SymbolInfo symbol,
            ScalpingDecision decision,
            JObject? snapshot)
        {
            string verdict = decision.Approved ? "Trade setup passed" : "No trade yet";
            string source = snapshot == null ? "price movement only" : "market snapshot";
            string details = string.IsNullOrWhiteSpace(decision.Detail) ? "" : $" | {decision.Detail}";
            if (cfg.DirectionMode == ScalpingDirectionMode.Auto)
            {
                string result = decision.Approved
                    ? $"Selected side: {decision.Direction}"
                    : $"Best side right now: {decision.Direction}, but it did not pass";
                return
                    $"[SCALP] Auto checked BUY and SELL. {result}. " +
                    $"Confirmations {decision.Score}/{cfg.MinDecisionScore}. Based on {source}. " +
                    $"Reason: {decision.Reason}. {BuildPriceSummary(request, cfg, symbol)}{details}";
            }

            return
                $"[SCALP] {verdict}: looking for {decision.Direction}. " +
                $"Confirmations {decision.Score}/{cfg.MinDecisionScore}. Based on {source}. " +
                $"Reason: {decision.Reason}. {BuildPriceSummary(request, cfg, symbol)}{details}";
        }

        private static string BuildPriceSummary(ScalpingSessionRequest request, ScalpingConfig cfg, SymbolInfo symbol)
        {
            double pipValue = EstimatePipValuePerLot(request.Pair, symbol) * Math.Max(0.01, request.LotSize);
            double slPips = Math.Max(1, cfg.StopLossPips);
            double tpPips = Math.Max(1, cfg.TakeProfitPips);
            return
                $"Price: bid {symbol.Bid:F5}, ask {symbol.Ask:F5}. " +
                $"Spread: {symbol.SpreadPips:F1} pips (max {cfg.MaxSpreadPips:F1}). " +
                $"Lot {request.LotSize:F2}; each pip about ${pipValue:F2}. " +
                $"Risk if SL hits: {slPips:F1} pips / about ${slPips * pipValue:F2}. " +
                $"Profit target: {tpPips:F1} pips / about ${tpPips * pipValue:F2}.";
        }

        private static double EstimatePipValuePerLot(string pair, SymbolInfo symbol)
        {
            double pipSize = symbol.Digits is 3 or 5
                ? (symbol.EffectivePointSize ?? LotCalculator.GetPipSize(pair)) * 10.0
                : symbol.EffectivePointSize ?? LotCalculator.GetPipSize(pair);

            if (IsFinitePositive(symbol.TickValue.GetValueOrDefault()) &&
                IsFinitePositive(symbol.TickSize.GetValueOrDefault()) &&
                IsFinitePositive(pipSize))
            {
                return symbol.TickValue!.Value * pipSize / symbol.TickSize!.Value;
            }

            return LotCalculator.GetPipValuePerLot(pair.ToUpperInvariant());
        }

        private static string BuildSnapshotTrace(
            JObject snapshot,
            ScalpingConfig cfg,
            TradeType direction,
            int score,
            List<string> reasons)
        {
            string trendM5 = ReadText(snapshot, "structure.trend_m5");
            string trendM15 = ReadText(snapshot, "structure.trend_m15");
            string trendH1 = ReadText(snapshot, "structure.trend_h1");
            string candleM5 = ReadText(snapshot, "candles.m5_last.direction");
            string macdM5 = ReadText(snapshot, "indicators.m5.macd_bias");
            string macdM15 = ReadText(snapshot, "indicators.m15.macd_bias");
            string ema20 = ReadText(snapshot, "indicators.m5.price_vs_ema20");
            string ema50 = ReadText(snapshot, "indicators.m5.price_vs_ema50");
            double rsi = ReadNumber(snapshot, "indicators.m5.rsi");
            double stoch = ReadNumber(snapshot, "indicators.m5.stoch_k");
            double support = ReadNumber(snapshot, "levels.distance_to_support_pips");
            double resistance = ReadNumber(snapshot, "levels.distance_to_resistance_pips");
            bool doji = ReadBool(snapshot, "candles.m5_last.is_doji", false);
            bool inside = ReadBool(snapshot, "candles.m5_last.is_inside_bar", false);

            return
                $"Market checks: target {direction}; confirmations {score}/{cfg.MinDecisionScore}. " +
                $"Trend M5/M15/H1: {TextOrDash(trendM5)} / {TextOrDash(trendM15)} / {TextOrDash(trendH1)}. " +
                $"Latest M5 candle: {TextOrDash(candleM5)}. " +
                $"MACD M5/M15: {TextOrDash(macdM5)} / {TextOrDash(macdM15)}. " +
                $"Price vs EMA20/EMA50: {TextOrDash(ema20)} / {TextOrDash(ema50)}. " +
                $"RSI {NumberOrDash(rsi)}, stochastic {NumberOrDash(stoch)}. " +
                $"Room to support {NumberOrDash(support)} pips, room to resistance {NumberOrDash(resistance)} pips. " +
                $"Unclear candle: doji {YesNo(doji)}, inside bar {YesNo(inside)}. " +
                $"Matched checks: {MatchedReasons(reasons)}.";
        }

        private static void ScoreText(JObject snapshot, string path, string expected, ref int score, List<string> reasons, string label)
        {
            string value = ReadText(snapshot, path);
            if (value.Contains(expected, StringComparison.OrdinalIgnoreCase) ||
                value.Contains(expected == "BULLISH" ? "BUY" : "SELL", StringComparison.OrdinalIgnoreCase))
            {
                score++;
                reasons.Add(label);
            }
        }

        private static bool ContainsDirection(string value, bool buy) =>
            buy
                ? value.Contains("ABOVE", StringComparison.OrdinalIgnoreCase) || value.Contains("BULL", StringComparison.OrdinalIgnoreCase)
                : value.Contains("BELOW", StringComparison.OrdinalIgnoreCase) || value.Contains("BEAR", StringComparison.OrdinalIgnoreCase);

        private static string ReadText(JObject snapshot, string path) =>
            snapshot.SelectToken(path)?.ToString() ?? "";

        private static string TextOrDash(string value) =>
            string.IsNullOrWhiteSpace(value) ? "-" : value;

        private static string NumberOrDash(double value) =>
            double.IsNaN(value) ? "-" : value.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

        private static string YesNo(bool value) => value ? "yes" : "no";

        private static string MatchedReasons(List<string> reasons) =>
            reasons.Count == 0 ? "none yet" : string.Join(", ", reasons.Take(8));

        private static double ReadNumber(JObject snapshot, string path)
        {
            var token = snapshot.SelectToken(path);
            if (token == null) return double.NaN;
            return double.TryParse(token.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double value)
                ? value
                : double.NaN;
        }

        private static bool ReadBool(JObject snapshot, string path, bool fallback)
        {
            var token = snapshot.SelectToken(path);
            return token?.Value<bool?>() ?? fallback;
        }

        private static double TrackClosedScalps(Dictionary<long, double> knownTickets, List<LivePosition> openPositions)
        {
            double openProfit = 0;
            foreach (var ticket in knownTickets.Keys.ToList())
            {
                var pos = openPositions.FirstOrDefault(p => p.Ticket == ticket);
                if (pos == null || pos.Ticket == 0) continue;
                double previous = knownTickets[ticket];
                knownTickets[ticket] = pos.Profit;
                openProfit += pos.Profit - previous;
            }

            return openProfit;
        }

        private static Task Delay(ScalpingConfig cfg, CancellationToken ct) =>
            Task.Delay(cfg.PollIntervalMs, ct);

        private void LogScalpSeparator() =>
            Log("[SCALP] ------------------------------------------------------------");

        private void Log(string message) => OnLog?.Invoke(message);
        private void Status(string status) => OnStatusChanged?.Invoke(status);

        private readonly record struct LiveScalpingConfigResult(
            ScalpingConfig Config,
            bool CanTrade,
            string Reason);

        private readonly record struct BrokerLevelPips(
            double StopLevelPips,
            double FreezeLevelPips,
            bool HasCompleteData);
    }
}
