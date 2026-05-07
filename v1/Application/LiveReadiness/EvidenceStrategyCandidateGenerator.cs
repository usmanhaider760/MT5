using MT5TradingBot.Modules.Backtesting;
using MT5TradingBot.Core;
using MT5TradingBot.Models;

namespace MT5TradingBot.Modules.LiveReadiness
{
    public sealed record EvidenceStrategyCandidateGenerationResult
    {
        public IReadOnlyList<RealisticBacktestTradeCandidate> Candidates { get; init; } = [];
        public int SkippedOrHoldSignals { get; init; }
        public int IncompleteSignals { get; init; }
        public string CandidateGenerationSource { get; init; } = "";
        public string AiDisabledReason { get; init; } = "";
        public string OfflineLiveDifferences { get; init; } = "";
        public string DiagnosticCode { get; init; } = "";
        public string Reason { get; init; } = "";
    }

    public sealed class EvidenceStrategyCandidateGenerator
    {
        public const string NotImplementedCode =
            "REAL_MARKET_DATA_LOADED_BUT_STRATEGY_CANDIDATE_GENERATION_NOT_IMPLEMENTED";

        public EvidenceStrategyCandidateGenerationResult Generate(
            IReadOnlyList<BacktestTick> ticks,
            IReadOnlyList<BacktestOhlcCandle> candles,
            BotConfig? config = null)
        {
            if (ticks.Count == 0 && candles.Count == 0)
            {
                return new EvidenceStrategyCandidateGenerationResult
                {
                    DiagnosticCode = "NO_MARKET_DATA_LOADED",
                    CandidateGenerationSource = "offline-auto-scalping-price-movement",
                    AiDisabledReason = "Offline candidate generation never calls external AI APIs.",
                    OfflineLiveDifferences = OfflineDifferences,
                    Reason = "No tick or OHLC rows were loaded."
                };
            }

            var rawCfg = config?.Scalping ?? new ScalpingConfig();
            bool invalidSlTpConfig = rawCfg.StopLossPips <= 0 || rawCfg.TakeProfitPips <= 0;
            var cfg = Normalize(rawCfg);
            var candidates = new List<RealisticBacktestTradeCandidate>();
            int skipped = 0;
            int incomplete = 0;
            var maxTradesBySymbol = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var group in BuildBars(ticks, candles).GroupBy(b => b.Symbol, StringComparer.OrdinalIgnoreCase))
            {
                OfflineBar? previous = null;
                foreach (var bar in group.OrderBy(b => b.TimestampUtc))
                {
                    if (!maxTradesBySymbol.TryGetValue(bar.Symbol, out int generatedForSymbol))
                        generatedForSymbol = 0;
                    if (generatedForSymbol >= cfg.MaxTrades)
                    {
                        skipped++;
                        previous = bar;
                        continue;
                    }

                    if (previous == null)
                    {
                        skipped++;
                        previous = bar;
                        continue;
                    }

                    double spreadPips = ResolveSpreadPips(bar);
                    if (!IsFinitePositive(spreadPips) || spreadPips > cfg.MaxSpreadPips)
                    {
                        skipped++;
                        previous = bar;
                        continue;
                    }

                    if (bar.Mid == previous.Mid)
                    {
                        skipped++;
                        previous = bar;
                        continue;
                    }

                    TradeType direction = ResolveOfflineDirection(cfg.DirectionMode, bar.Mid, previous.Mid);
                    var candidate = invalidSlTpConfig
                        ? null
                        : BuildCandidate(bar, direction, cfg, candidates.Count + 1);
                    if (candidate == null)
                        incomplete++;
                    else
                    {
                        candidates.Add(candidate);
                        maxTradesBySymbol[bar.Symbol] = generatedForSymbol + 1;
                    }

                    previous = bar;
                }
            }

            string diagnostic = candidates.Count > 0
                ? "OFFLINE_AUTO_SCALPING_PRICE_MOVEMENT_CANDIDATES_GENERATED"
                : "REAL_MARKET_DATA_LOADED_BUT_NO_STRATEGY_CANDIDATES";

            return new EvidenceStrategyCandidateGenerationResult
            {
                Candidates = candidates,
                SkippedOrHoldSignals = skipped,
                IncompleteSignals = incomplete,
                CandidateGenerationSource = "offline-auto-scalping-price-movement",
                AiDisabledReason = "AI disabled: no historical AI decisions were supplied and offline evidence generation must not call external AI APIs.",
                OfflineLiveDifferences = OfflineDifferences,
                DiagnosticCode = diagnostic,
                Reason = candidates.Count > 0
                    ? $"Generated {candidates.Count} offline auto-scalping price-movement candidate(s)."
                    : "Market data loaded, but offline price-movement auto-scalping generated zero complete candidates."
            };
        }

        private const string OfflineDifferences =
            "Offline mirror uses the live auto-scalping price-movement fallback only. It does not call MT5 GetMarketSnapshot, does not use live M5/M15/H1 indicator snapshot scoring, does not call AI confirmation, does not inspect open positions, and does not execute orders.";

        private static ScalpingConfig Normalize(ScalpingConfig cfg) => new()
        {
            MaxTrades = Math.Clamp(cfg.MaxTrades, 1, 50),
            StopLossPips = Math.Max(1, cfg.StopLossPips),
            TakeProfitPips = Math.Max(1, cfg.TakeProfitPips),
            MaxSpreadPips = Math.Max(0.1, cfg.MaxSpreadPips),
            DirectionMode = cfg.DirectionMode,
            MinDecisionScore = Math.Clamp(cfg.MinDecisionScore, 1, 10)
        };

        private static IReadOnlyList<OfflineBar> BuildBars(
            IReadOnlyList<BacktestTick> ticks,
            IReadOnlyList<BacktestOhlcCandle> candles)
        {
            if (ticks.Count > 0)
            {
                return ticks
                    .Where(t => t.Bid > 0 && t.Ask > 0 && t.Ask >= t.Bid)
                    .Select(t => new OfflineBar(
                        EnsureUtc(t.TimestampUtc),
                        t.Symbol.ToUpperInvariant(),
                        t.Bid,
                        t.Ask,
                        null))
                    .ToList();
            }

            return candles
                .Where(c => c.Open > 0 && c.High >= c.Low && c.Close >= c.Low && c.Close <= c.High)
                .Select(c =>
                {
                    double bid = c.BidClose ?? c.BidOpen ?? c.Close;
                    double ask = c.AskClose ?? c.AskOpen ?? c.Close;
                    if (ask < bid)
                    {
                        double mid = c.Close;
                        bid = mid;
                        ask = mid;
                    }

                    return new OfflineBar(
                        EnsureUtc(c.TimestampUtc),
                        c.Symbol.ToUpperInvariant(),
                        bid,
                        ask,
                        c.SpreadPips);
                })
                .ToList();
        }

        private static TradeType ResolveOfflineDirection(
            ScalpingDirectionMode mode,
            double currentMid,
            double previousMid) =>
            mode switch
            {
                ScalpingDirectionMode.BuyOnly => TradeType.BUY,
                ScalpingDirectionMode.SellOnly => TradeType.SELL,
                _ => currentMid >= previousMid ? TradeType.BUY : TradeType.SELL
            };

        private static RealisticBacktestTradeCandidate? BuildCandidate(
            OfflineBar bar,
            TradeType direction,
            ScalpingConfig cfg,
            int index)
        {
            double pip = LotCalculator.GetPipSize(bar.Symbol);
            double entry = direction == TradeType.BUY ? bar.Ask : bar.Bid;
            double sl = direction == TradeType.BUY
                ? entry - cfg.StopLossPips * pip
                : entry + cfg.StopLossPips * pip;
            double tp = direction == TradeType.BUY
                ? entry + cfg.TakeProfitPips * pip
                : entry - cfg.TakeProfitPips * pip;

            if (!IsFinitePositive(entry) ||
                !IsFinitePositive(sl) ||
                !IsFinitePositive(tp) ||
                !IsFinitePositive(cfg.StopLossPips) ||
                !IsFinitePositive(cfg.TakeProfitPips))
            {
                return null;
            }

            return new RealisticBacktestTradeCandidate
            {
                Id = $"OFFLINE-SCALP-{index:000000}",
                TimestampUtc = bar.TimestampUtc,
                Symbol = bar.Symbol,
                Direction = direction,
                EntryPrice = Math.Round(entry, Digits(bar.Symbol)),
                EntryRulePlaceholder = "offline-auto-scalping-price-movement",
                StopLoss = Math.Round(sl, Digits(bar.Symbol)),
                TakeProfit = Math.Round(tp, Digits(bar.Symbol)),
                LotSize = 0.01,
                SpreadPips = ResolveSpreadPips(bar),
                Session = SessionName(bar.TimestampUtc),
                SpreadRegime = SpreadRegime(ResolveSpreadPips(bar), cfg.MaxSpreadPips),
                SourceSignalReason = "Offline auto-scalping price-movement fallback; AI disabled.",
                SourceType = "auto-scalping / AI-disabled",
                SourceSignalConfidence = null
            };
        }

        private static double ResolveSpreadPips(OfflineBar bar)
        {
            if (bar.SpreadPips.HasValue)
                return bar.SpreadPips.Value;

            double pip = LotCalculator.GetPipSize(bar.Symbol);
            return pip > 0 ? Math.Round((bar.Ask - bar.Bid) / pip, 4) : double.NaN;
        }

        private static string SessionName(DateTime timestampUtc) =>
            timestampUtc.Hour switch
            {
                >= 7 and < 12 => "London",
                >= 12 and < 17 => "London+NY Overlap",
                >= 17 and < 22 => "New York",
                >= 0 and < 7 => "Asia",
                _ => "Off Hours"
            };

        private static string SpreadRegime(double spreadPips, double maxSpreadPips) =>
            spreadPips <= maxSpreadPips * 0.50 ? "Tight" :
            spreadPips <= maxSpreadPips ? "Acceptable" :
            "Wide";

        private static int Digits(string symbol) =>
            symbol.Contains("JPY", StringComparison.OrdinalIgnoreCase) ? 3 : 5;

        private static bool IsFinitePositive(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;

        private static DateTime EnsureUtc(DateTime timestamp) =>
            timestamp.Kind == DateTimeKind.Utc
                ? timestamp
                : DateTime.SpecifyKind(timestamp, DateTimeKind.Utc);

        private sealed record OfflineBar(
            DateTime TimestampUtc,
            string Symbol,
            double Bid,
            double Ask,
            double? SpreadPips)
        {
            public double Mid => (Bid + Ask) / 2.0;
        }
    }
}
