using MT5TradingBot.Models;
using MT5TradingBot.Modules.BrokerIntegration;
using MT5TradingBot.Modules.NormalTrading;
using MT5TradingBot.Modules.NewsFilter;
using MT5TradingBot.Modules.PairSettings;
using MT5TradingBot.Modules.Scalping;
using MT5TradingBot.Services;

namespace MT5TradingBot.Modules.TradeRules
{
    public sealed class TradeRulesRuntimeSnapshotService
    {
        private readonly AppSettings _settings;
        private readonly TradeRuleCatalog _catalog;
        private readonly MT5Bridge? _bridge;
        private readonly IPairSettingsService? _pairSettings;
        private readonly IScalpingSessionService? _scalpingSession;
        private readonly NormalTradeManager? _normalTradeManager;
        private readonly INewsCalendarService? _newsCalendar;
        private readonly ApiIntegrationConfig? _apiConfig;
        private NewsRiskSnapshot? _cachedNews;
        private string _cachedNewsPair = "";
        private DateTime _cachedNewsAtUtc = DateTime.MinValue;

        public TradeRulesRuntimeSnapshotService(
            AppSettings settings,
            TradeRuleCatalog? catalog = null,
            MT5Bridge? bridge = null,
            IPairSettingsService? pairSettings = null,
            IScalpingSessionService? scalpingSession = null,
            NormalTradeManager? normalTradeManager = null,
            INewsCalendarService? newsCalendar = null,
            ApiIntegrationConfig? apiConfig = null)
        {
            _settings = settings;
            _catalog = catalog ?? new TradeRuleCatalog();
            _bridge = bridge;
            _pairSettings = pairSettings;
            _scalpingSession = scalpingSession;
            _normalTradeManager = normalTradeManager;
            _newsCalendar = newsCalendar;
            _apiConfig = apiConfig;
        }

        public async Task<TradeRulesRuntimeSnapshotResult> BuildAsync(
            TradeRulesContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            AccountInfo? account = null;
            SymbolInfo? symbol = null;
            LivePosition? position = null;
            IReadOnlyList<LivePosition> positions = [];

            if (_bridge?.IsConnected == true)
            {
                account = await _bridge.GetAccountInfoAsync().ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(context.Pair))
                    symbol = await _bridge.GetSymbolInfoAsync(context.Pair).ConfigureAwait(false);

                positions = await _bridge.GetPositionsAsync().ConfigureAwait(false);
                position = ResolvePosition(context, positions);
            }

            var pairSettings = !string.IsNullOrWhiteSpace(context.Pair)
                ? _pairSettings?.GetForPair(context.Pair)
                : null;
            var scalpingRuntime = _scalpingSession?.GetRuntimeSnapshot();
            var lastAudit = AutoBotService.LastExecutionAuditSnapshot;
            var news = await GetCachedNewsAsync(context.Pair, cancellationToken).ConfigureAwait(false);

            var rules = _catalog.GetAll()
                .Select(item => BuildRule(item, context, account, symbol, position, positions, pairSettings, scalpingRuntime, lastAudit, news))
                .ToList();

            return new TradeRulesRuntimeSnapshotResult
            {
                Context = context,
                Account = account,
                Symbol = symbol,
                Position = position,
                Rules = rules,
                Summary = TradeRuleStatusEvaluator.BuildSummary(rules),
                CapturedAtUtc = DateTime.UtcNow
            };
        }

        private TradeRuleRuntimeSnapshot BuildRule(
            TradeRuleCatalogItem item,
            TradeRulesContext context,
            AccountInfo? account,
            SymbolInfo? symbol,
            LivePosition? position,
            IReadOnlyList<LivePosition> positions,
            PairTradingSettings? pairSettings,
            ScalpingRuntimeSnapshot? scalpingRuntime,
            IReadOnlyList<TradeRuleAuditSnapshot> lastAudit,
            NewsRiskSnapshot? news)
        {
            object? configured = ResolveConfiguredValue(item.RuleCode, context, pairSettings);
            object? live = ResolveLiveValue(item.RuleCode, account, symbol, position, positions, scalpingRuntime, news);
            bool enabled = IsEnabled(item.RuleCode);
            string result = ResolveInitialResult(item.RuleCode, configured, live, account, symbol, position);
            var audit = ResolveAudit(item.RuleCode, context, lastAudit);
            if (audit != null)
            {
                live = $"Order {audit.Order}";
                result = audit.Result;
            }

            var snapshot = new TradeRuleRuntimeSnapshot
            {
                RuleCode = item.RuleCode,
                RuleName = item.RuleName,
                Category = item.Category,
                GroupName = item.GroupName,
                FunctionName = item.FunctionName,
                VariableName = item.VariableName,
                SourceFile = item.SourceFile,
                SourceName = item.SourceName,
                IsCritical = item.IsCritical,
                IsEnabled = enabled,
                StandardValue = ResolveStandardValue(item.RuleCode),
                ConfiguredValue = configured,
                LiveValue = live,
                MinValue = ResolveMinValue(item.RuleCode),
                MaxValue = ResolveMaxValue(item.RuleCode),
                Unit = ResolveUnit(item.RuleCode),
                Reason = audit?.Reason ?? BuildReason(item.RuleCode, result, enabled, account, symbol, live, news),
                RuntimeMode = ResolveRuntimeMode(item, context, configured, live),
                ValueType = item.ValueType,
                LastCheckedAtUtc = DateTime.UtcNow
            };

            TradeRuleStatusEvaluator.ApplyEnabledState(snapshot, result);
            if (snapshot.RuntimeMode == TradeRuleRuntimeModes.MonitorOnly)
                snapshot.ActualEffect += " Monitor Only - live value is displayed but runtime apply is not wired for this rule.";
            return snapshot;
        }

        private async Task<NewsRiskSnapshot?> GetCachedNewsAsync(string pair, CancellationToken cancellationToken)
        {
            if (_newsCalendar == null || _apiConfig == null || string.IsNullOrWhiteSpace(pair))
                return null;

            if (string.Equals(_cachedNewsPair, pair, StringComparison.OrdinalIgnoreCase) &&
                _cachedNews != null &&
                DateTime.UtcNow - _cachedNewsAtUtc < TimeSpan.FromMinutes(5))
                return _cachedNews;

            try
            {
                _cachedNews = await _newsCalendar.GetRiskSnapshotAsync(pair, _apiConfig, cancellationToken).ConfigureAwait(false);
                _cachedNewsPair = pair;
                _cachedNewsAtUtc = DateTime.UtcNow;
                return _cachedNews;
            }
            catch
            {
                return null;
            }
        }

        private static TradeRuleAuditSnapshot? ResolveAudit(
            string ruleCode,
            TradeRulesContext context,
            IReadOnlyList<TradeRuleAuditSnapshot> lastAudit)
        {
            if (lastAudit.Count == 0)
                return null;

            return lastAudit.FirstOrDefault(a =>
                string.Equals(a.RuleCode, ruleCode, StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(context.Pair) || string.Equals(a.Pair, context.Pair, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(context.RequestId) || string.Equals(a.RequestId, context.RequestId, StringComparison.OrdinalIgnoreCase)))
                ?? lastAudit.FirstOrDefault(a => string.Equals(a.RuleCode, ruleCode, StringComparison.OrdinalIgnoreCase));
        }

        private static string ResolveRuntimeMode(TradeRuleCatalogItem item, TradeRulesContext context, object? configured, object? live)
        {
            if (TradeRulesRuntimeControlService.IsRuntimeControllableRule(item.RuleCode, context.Strategy))
                return TradeRuleRuntimeModes.RuntimeControllable;

            if (item.Category == "Pair Rules")
                return TradeRuleRuntimeModes.SaveOnly;

            if (configured == null && live == null)
                return TradeRuleRuntimeModes.NotAvailable;

            return TradeRuleRuntimeModes.MonitorOnly;
        }

        private object? ResolveConfiguredValue(string ruleCode, TradeRulesContext context, PairTradingSettings? pairSettings)
        {
            BotConfig bot = _settings.Bot;
            ScalpingConfig scalping = ResolveScalping(context);
            NormalTradingSettings normal = ResolveNormal(context);

            return ruleCode switch
            {
                "COMMON-TRADING-MODE" => bot.CommonTrading.TradingMode,
                "COMMON-AI-CONFIRM" => bot.CommonTrading.UseAiConfirmation,
                "COMMON-AUTO-CLOSE" => bot.CommonTrading.AutoCloseAfterOpen,
                "COMMON-PROFIT-PIPS" => bot.CommonTrading.ProfitTargetPips,
                "COMMON-PROFIT-USD" => bot.CommonTrading.ProfitTargetUsd,
                "COMMON-BREAKEVEN-TRIGGER" => bot.CommonTrading.BeTriggerPercentOfTp,
                "COMMON-MAX-SPREAD" => bot.MaxSpreadPips,
                "COMMON-MAX-POSITIONS" => bot.MaxConcurrentPositions,
                "COMMON-CORRELATION" => bot.CorrelationCheckEnabled,

                "SCALP-ENABLED" => bot.Scalping.Enabled,
                "SCALP-MAX-TRADES" => scalping.MaxTrades,
                "SCALP-MAX-MINUTES" => scalping.MaxMinutes,
                "SCALP-SESSION-LOSS" => scalping.MaxSessionLossUsd,
                "SCALP-PROFIT-TARGET" => scalping.ProfitTargetUsd,
                "SCALP-SL-PIPS" => scalping.StopLossPips,
                "SCALP-TP-PIPS" => scalping.TakeProfitPips,
                "SCALP-RISK-REWARD" => scalping.RiskRewardRatio,
                "SCALP-SPREAD-LIMIT" => scalping.MaxSpreadPips,
                "SCALP-SPREAD-TP-PERCENT" => scalping.MaxSpreadPercentOfTp,
                "SCALP-DYNAMIC-VALUES" => scalping.DynamicValuesEnabled,
                "SCALP-POLL-INTERVAL" => scalping.PollIntervalMs,
                "SCALP-COOLDOWN" => scalping.CooldownSeconds,
                "SCALP-DIRECTION-MODE" => scalping.DirectionMode,
                "SCALP-PYRAMIDING" => scalping.AllowPyramiding,
                "SCALP-SNAPSHOT-CONFIRM" => scalping.RequireSnapshotConfirmation,
                "SCALP-MIN-SCORE" => scalping.MinDecisionScore,
                "SCALP-AI-CONFIRM" => scalping.UseAiConfirmation,

                "NORMAL-ENABLED" => normal.Enabled,
                "NORMAL-MAX-TRADES" => normal.MaxTrades,
                "NORMAL-EXPIRY" => normal.ExpiryMinutes,
                "NORMAL-SL-PIPS" => normal.StopLossPips,
                "NORMAL-TP-PIPS" => normal.TakeProfitPips,
                "NORMAL-RISK-REWARD" => normal.RiskRewardRatio,
                "NORMAL-SPREAD-LIMIT" => normal.MaxSpreadPips,

                "PAIR-PIP-SIZE" => pairSettings?.PipSize,
                "PAIR-M5-ATR-MIN" => pairSettings?.MinAtrPipsM5,
                "PAIR-M5-ATR-MAX" => pairSettings?.MaxAtrPipsM5,
                "PAIR-M15-ATR-MIN" => pairSettings?.MinAtrPipsM15,
                "PAIR-M15-ATR-MAX" => pairSettings?.MaxAtrPipsM15,
                "PAIR-KEYLEVEL-DISTANCE" => pairSettings?.MinimumDistanceFromKeyLevelPips,
                "PAIR-TRAILING-START" => pairSettings?.TrailingStartPips,
                "PAIR-TRAILING-STEP" => pairSettings?.TrailingStepPips,
                "PAIR-MAX-SLIPPAGE" => pairSettings?.MaxSlippagePips,
                "PAIR-RECOMMENDED-SESSION" => pairSettings?.RecommendedSessions,
                "PAIR-AVOID-SESSION" => pairSettings?.AvoidSessions,

                "ACCOUNT-DAILY-LOSS" => bot.MaxDailyLossAmount > 0 ? bot.MaxDailyLossAmount : bot.MaxDailyLossPercent,
                "ACCOUNT-WEEKLY-LOSS" => bot.MaxWeeklyLossAmount > 0 ? bot.MaxWeeklyLossAmount : bot.MaxWeeklyLossPercent,
                "ACCOUNT-MAX-CONCURRENT" => bot.MaxConcurrentPositions,
                "ACCOUNT-MARGIN" => bot.MinProjectedMarginLevelPercent,
                "ACCOUNT-DRAWDOWN" => bot.EmergencyCloseDrawdownPct,
                "ACCOUNT-KILL-SWITCH" => "Configured in kill switch state file",
                "SAFETY-NO-TRADE-WINDOW" => bot.AdditionalNoTradeWindows,
                "SAFETY-PAIR-ALLOWLIST" => bot.AllowedPairs,
                "SAFETY-NEWS-BLACKOUT" => _settings.ApiIntegrations.BlockTradesOnHighImpactNews,
                "SAFETY-EDGE-MONITOR" => bot.EdgeMonitorEnabled,
                _ => null
            };
        }

        private object? ResolveLiveValue(
            string ruleCode,
            AccountInfo? account,
            SymbolInfo? symbol,
            LivePosition? position,
            IReadOnlyList<LivePosition> positions,
            ScalpingRuntimeSnapshot? scalpingRuntime,
            NewsRiskSnapshot? news) =>
            ruleCode switch
            {
                "BROKER-SYMBOL-DATA" => symbol == null ? null : $"{symbol.Bid:F5}/{symbol.Ask:F5}",
                "BROKER-STOP-LEVEL" => symbol?.StopLevelPips,
                "BROKER-FREEZE-LEVEL" => symbol?.FreezeLevelPips,
                "BROKER-LOT-SIZE" => symbol == null ? null : $"{symbol.MinLot:0.##}-{symbol.MaxLot:0.##}",
                "COMMON-MAX-SPREAD" or "SCALP-SPREAD-LIMIT" or "NORMAL-SPREAD-LIMIT" => symbol?.SpreadPips,
                "ACCOUNT-DATA" => account == null ? null : $"{account.AccountNumber} {account.Server}",
                "ACCOUNT-FLOATING-LOSS" => account?.Profit,
                "ACCOUNT-MARGIN" => account?.MarginLevel,
                "ACCOUNT-MAX-CONCURRENT" or "COMMON-MAX-POSITIONS" => positions.Count,
                "ACCOUNT-SYMBOL-EXPOSURE" => positions.Count,
                "SCALP-ENABLED" => scalpingRuntime?.IsRunning ?? _scalpingSession?.IsRunning,
                "SCALP-MAX-TRADES" => scalpingRuntime?.TradesCount,
                "SCALP-MAX-MINUTES" => scalpingRuntime?.ElapsedSeconds is null ? null : scalpingRuntime.ElapsedSeconds.Value / 60.0,
                "SCALP-SESSION-LOSS" => scalpingRuntime?.SessionProfitUsd,
                "SCALP-COOLDOWN" => scalpingRuntime?.CooldownRemainingSeconds,
                "SCALP-DIRECTION-MODE" => scalpingRuntime?.SelectedDirection?.ToString(),
                "SCALP-BUY-SCORE" => scalpingRuntime?.LastBuyScore,
                "SCALP-SELL-SCORE" => scalpingRuntime?.LastSellScore,
                "SCALP-DIRECTION-TIE" => scalpingRuntime?.LastNoTradeReason,
                "NORMAL-ENABLED" => _normalTradeManager?.IsRunning,
                "SAFETY-NEWS-BLACKOUT" => news == null ? null : $"{news.RiskLevel}; blackout={news.IsBlackoutActive}; highImpact60={news.HighImpactNext60Minutes}",
                "SAFETY-ADX-RANGING" => null,
                "EXEC-FINAL-GATE" => "Last audit source not connected yet",
                "EXEC-RISK-VALIDATION" => "Last risk audit source not connected yet",
                "EXEC-TRADE-ACCEPTED" => null,
                "EXEC-TRADE-REJECTED" => null,
                "EXEC-NO-TRADE" => null,
                "PAIR-PIP-SIZE" => symbol?.EffectivePointSize,
                _ => position != null && IsPositionRule(ruleCode) ? $"{position.Symbol} #{position.Ticket}" : null
            };

        private bool IsEnabled(string ruleCode) =>
            !_settings.Bot.TradeRuleEnabled.TryGetValue(ruleCode, out bool enabled) || enabled;

        private ScalpingConfig ResolveScalping(TradeRulesContext context) =>
            !string.IsNullOrWhiteSpace(context.Pair)
            && _settings.Bot.ScalpingByPair.TryGetValue(context.Pair.ToUpperInvariant(), out var pairConfig)
                ? pairConfig
                : _settings.Bot.Scalping;

        private NormalTradingSettings ResolveNormal(TradeRulesContext context) =>
            !string.IsNullOrWhiteSpace(context.Pair)
            && _settings.Bot.NormalTradingByPair.TryGetValue(context.Pair.ToUpperInvariant(), out var pairConfig)
                ? pairConfig
                : _settings.Bot.NormalTrading;

        private static object? ResolveStandardValue(string ruleCode) =>
            ruleCode switch
            {
                "SCALP-SPREAD-TP-PERCENT" => 20.0,
                "SCALP-MIN-SCORE" => 6,
                "COMMON-BREAKEVEN-TRIGGER" => 0.60,
                _ => null
            };

        private static double? ResolveMinValue(string ruleCode) =>
            ruleCode switch
            {
                "SCALP-MIN-SCORE" => 1,
                "SCALP-RISK-REWARD" or "NORMAL-RISK-REWARD" => 0.1,
                "COMMON-BREAKEVEN-TRIGGER" => 0,
                _ when ruleCode.Contains("PIPS", StringComparison.Ordinal) => 0,
                _ when ruleCode.Contains("SPREAD", StringComparison.Ordinal) => 0,
                _ when ruleCode.Contains("LOSS", StringComparison.Ordinal) => 0,
                _ when ruleCode.Contains("PROFIT", StringComparison.Ordinal) => 0,
                _ when ruleCode.Contains("MARGIN", StringComparison.Ordinal) => 0,
                _ when ruleCode.Contains("TRADES", StringComparison.Ordinal) => 0,
                _ => null
            };

        private static double? ResolveMaxValue(string ruleCode) =>
            ruleCode switch
            {
                "SCALP-MIN-SCORE" => 10,
                "SCALP-SPREAD-TP-PERCENT" => 100,
                "COMMON-BREAKEVEN-TRIGGER" => 1,
                "ACCOUNT-MARGIN" => 1000,
                _ when ruleCode.Contains("PIPS", StringComparison.Ordinal) => 1000,
                _ when ruleCode.Contains("SPREAD", StringComparison.Ordinal) => 1000,
                _ when ruleCode.Contains("TRADES", StringComparison.Ordinal) => 100,
                _ => null
            };

        private static string ResolveUnit(string ruleCode) =>
            ruleCode switch
            {
                "COMMON-BREAKEVEN-TRIGGER" => "fraction",
                _ when ruleCode.Contains("PIPS", StringComparison.Ordinal) => "pips",
                _ when ruleCode.Contains("SPREAD", StringComparison.Ordinal) => "pips",
                _ when ruleCode.Contains("PERCENT", StringComparison.Ordinal) => "%",
                _ when ruleCode.Contains("MARGIN", StringComparison.Ordinal) => "%",
                _ when ruleCode.Contains("USD", StringComparison.Ordinal) => "USD",
                _ => ""
            };

        private static string ResolveInitialResult(
            string ruleCode,
            object? configured,
            object? live,
            AccountInfo? account,
            SymbolInfo? symbol,
            LivePosition? position)
        {
            if (ruleCode is "ACCOUNT-DATA")
                return account == null ? TradeRuleResults.NotChecked : TradeRuleResults.Pass;

            if (ruleCode is "BROKER-SYMBOL-DATA")
                return symbol == null ? TradeRuleResults.NotChecked : TradeRuleResults.Pass;

            if (configured is null && live is null)
                return TradeRuleResults.NotChecked;

            if (TryDouble(configured, out double configuredNumber) &&
                TryDouble(live, out double liveNumber) &&
                configuredNumber > 0)
            {
                if (ruleCode.Contains("SPREAD", StringComparison.OrdinalIgnoreCase) && liveNumber > configuredNumber)
                    return TradeRuleResults.Block;

                if (ruleCode is "ACCOUNT-MARGIN" && liveNumber < configuredNumber)
                    return TradeRuleResults.Block;
            }

            return TradeRuleResults.Pass;
        }

        private static string BuildReason(
            string ruleCode,
            string result,
            bool enabled,
            AccountInfo? account,
            SymbolInfo? symbol,
            object? live,
            NewsRiskSnapshot? news)
        {
            if (result == TradeRuleResults.NotChecked)
                return ruleCode switch
                {
                    "ACCOUNT-DATA" when account == null => "Account live data is unavailable.",
                    "BROKER-SYMBOL-DATA" when symbol == null => "Broker symbol data is unavailable.",
                    "SAFETY-NEWS-BLACKOUT" when news == null => "News source unavailable, unconfigured, or cached snapshot not ready.",
                    "SAFETY-ADX-RANGING" => "ADX live source not wired yet.",
                    _ => "Live source is not connected yet or this rule has no runtime source in Phase 3."
                };

            if (ruleCode == "SAFETY-NEWS-BLACKOUT" && news != null)
                return news.Reason;

            if (!enabled)
                return $"Rule is disabled; would-have-result is {result}.";

            return live == null ? "Configured value available." : "Live/configured value captured.";
        }

        private static LivePosition? ResolvePosition(TradeRulesContext context, IReadOnlyList<LivePosition> positions)
        {
            if (context.Ticket.HasValue)
                return positions.FirstOrDefault(p => p.Ticket == context.Ticket.Value);

            return string.IsNullOrWhiteSpace(context.Pair)
                ? null
                : positions.FirstOrDefault(p => string.Equals(p.Symbol, context.Pair, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsPositionRule(string ruleCode) =>
            ruleCode.StartsWith("EXEC-", StringComparison.Ordinal) ||
            ruleCode.StartsWith("ACCOUNT-", StringComparison.Ordinal);

        private static bool TryDouble(object? value, out double number)
        {
            if (value is double d)
            {
                number = d;
                return true;
            }

            if (value is int i)
            {
                number = i;
                return true;
            }

            number = 0;
            return false;
        }
    }
}
