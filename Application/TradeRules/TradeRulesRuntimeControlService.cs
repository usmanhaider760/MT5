using MT5TradingBot.Models;
using MT5TradingBot.Services;

namespace MT5TradingBot.Modules.TradeRules
{
    public sealed class TradeRulesRuntimeControlService
    {
        private readonly AppSettings _settings;
        private readonly SettingsManager? _settingsManager;

        public TradeRulesRuntimeControlService(AppSettings settings, SettingsManager? settingsManager = null)
        {
            _settings = settings;
            _settingsManager = settingsManager;
        }

        public Task ApplyRuntimeAsync(
            TradeRulesContext context,
            IReadOnlyList<TradeRuleRuntimeSnapshot> rules,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ApplyRuleStates(rules);
            ApplyEditableValues(context, rules);
            return Task.CompletedTask;
        }

        public async Task SavePairDefaultsAsync(
            TradeRulesContext context,
            IReadOnlyList<TradeRuleRuntimeSnapshot> rules,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(context.Pair))
                return;

            ApplyRuleStates(rules);
            ApplyPairDefaults(context, rules);
            if (_settingsManager != null)
                await _settingsManager.SaveAsync(_settings).ConfigureAwait(false);
        }

        public async Task SaveStrategyDefaultsAsync(
            TradeRulesContext context,
            IReadOnlyList<TradeRuleRuntimeSnapshot> rules,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ApplyRuleStates(rules);
            ApplyEditableValues(context, rules);
            if (_settingsManager != null)
                await _settingsManager.SaveAsync(_settings).ConfigureAwait(false);
        }

        public void ResetRule(TradeRuleRuntimeSnapshot rule)
        {
            rule.PreviewValue = rule.StandardValue ?? rule.ConfiguredValue;
            rule.IsEnabled = true;
        }

        public void ResetRules(IReadOnlyList<TradeRuleRuntimeSnapshot> rules)
        {
            foreach (var rule in rules)
                ResetRule(rule);
        }

        private void ApplyRuleStates(IReadOnlyList<TradeRuleRuntimeSnapshot> rules)
        {
            _settings.Bot.TradeRuleEnabled ??= new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (var rule in rules)
                _settings.Bot.TradeRuleEnabled[rule.RuleCode] = rule.IsEnabled;
        }

        private void ApplyPairDefaults(TradeRulesContext context, IReadOnlyList<TradeRuleRuntimeSnapshot> rules)
        {
            string key = context.Pair.Trim().ToUpperInvariant();

            if (context.Strategy == TradeRulesStrategy.Scalping)
            {
                _settings.Bot.ScalpingByPair ??= new Dictionary<string, ScalpingConfig>(StringComparer.OrdinalIgnoreCase);
                var cfg = CloneScalping(_settings.Bot.ScalpingByPair.TryGetValue(key, out var existing) ? existing : _settings.Bot.Scalping);
                ApplyScalpingValues(cfg, rules);
                _settings.Bot.ScalpingByPair[key] = cfg;
                return;
            }

            if (context.Strategy == TradeRulesStrategy.Normal)
            {
                _settings.Bot.NormalTradingByPair ??= new Dictionary<string, NormalTradingSettings>(StringComparer.OrdinalIgnoreCase);
                var cfg = CloneNormal(_settings.Bot.NormalTradingByPair.TryGetValue(key, out var existing) ? existing : _settings.Bot.NormalTrading);
                ApplyNormalValues(cfg, rules);
                _settings.Bot.NormalTradingByPair[key] = cfg;
            }
        }

        private void ApplyEditableValues(TradeRulesContext context, IReadOnlyList<TradeRuleRuntimeSnapshot> rules)
        {
            ApplyCommonValues(_settings.Bot, rules);

            if (context.Strategy == TradeRulesStrategy.Scalping)
                ApplyScalpingValues(_settings.Bot.Scalping, rules);
            else if (context.Strategy == TradeRulesStrategy.Normal)
                ApplyNormalValues(_settings.Bot.NormalTrading, rules);
        }

        private static void ApplyCommonValues(BotConfig bot, IReadOnlyList<TradeRuleRuntimeSnapshot> rules)
        {
            foreach (var rule in rules)
            {
                object? value = rule.PreviewValue ?? rule.ConfiguredValue;
                switch (rule.RuleCode)
                {
                    case "COMMON-AI-CONFIRM":
                        bot.CommonTrading.UseAiConfirmation = ToBool(value, bot.CommonTrading.UseAiConfirmation);
                        break;
                    case "COMMON-AUTO-CLOSE":
                        bot.CommonTrading.AutoCloseAfterOpen = ToBool(value, bot.CommonTrading.AutoCloseAfterOpen);
                        break;
                    case "COMMON-PROFIT-PIPS":
                        bot.CommonTrading.ProfitTargetPips = ToDouble(value, bot.CommonTrading.ProfitTargetPips);
                        break;
                    case "COMMON-PROFIT-USD":
                        bot.CommonTrading.ProfitTargetUsd = ToDouble(value, bot.CommonTrading.ProfitTargetUsd);
                        break;
                    case "COMMON-BREAKEVEN-TRIGGER":
                        bot.CommonTrading.BeTriggerPercentOfTp = ToDouble(value, bot.CommonTrading.BeTriggerPercentOfTp);
                        break;
                    case "COMMON-MAX-SPREAD":
                        bot.MaxSpreadPips = ToDouble(value, bot.MaxSpreadPips);
                        break;
                    case "COMMON-MAX-POSITIONS":
                        bot.MaxConcurrentPositions = ToInt(value, bot.MaxConcurrentPositions);
                        break;
                    case "COMMON-CORRELATION":
                        bot.CorrelationCheckEnabled = ToBool(value, bot.CorrelationCheckEnabled);
                        break;
                }
            }
        }

        private static void ApplyScalpingValues(ScalpingConfig cfg, IReadOnlyList<TradeRuleRuntimeSnapshot> rules)
        {
            foreach (var rule in rules)
            {
                object? value = rule.PreviewValue ?? rule.ConfiguredValue;
                switch (rule.RuleCode)
                {
                    case "SCALP-MAX-TRADES": cfg.MaxTrades = ToInt(value, cfg.MaxTrades); break;
                    case "SCALP-MAX-MINUTES": cfg.MaxMinutes = ToInt(value, cfg.MaxMinutes); break;
                    case "SCALP-SESSION-LOSS": cfg.MaxSessionLossUsd = ToDouble(value, cfg.MaxSessionLossUsd); break;
                    case "SCALP-PROFIT-TARGET": cfg.ProfitTargetUsd = ToDouble(value, cfg.ProfitTargetUsd); break;
                    case "SCALP-SL-PIPS": cfg.StopLossPips = ToDouble(value, cfg.StopLossPips); break;
                    case "SCALP-TP-PIPS": cfg.TakeProfitPips = ToDouble(value, cfg.TakeProfitPips); break;
                    case "SCALP-RISK-REWARD": cfg.RiskRewardRatio = ToDouble(value, cfg.RiskRewardRatio); break;
                    case "SCALP-SPREAD-LIMIT": cfg.MaxSpreadPips = ToDouble(value, cfg.MaxSpreadPips); break;
                    case "SCALP-SPREAD-TP-PERCENT": cfg.MaxSpreadPercentOfTp = ToDouble(value, cfg.MaxSpreadPercentOfTp); break;
                    case "SCALP-DYNAMIC-VALUES": cfg.DynamicValuesEnabled = ToBool(value, cfg.DynamicValuesEnabled); break;
                    case "SCALP-POLL-INTERVAL": cfg.PollIntervalMs = ToInt(value, cfg.PollIntervalMs); break;
                    case "SCALP-COOLDOWN": cfg.CooldownSeconds = ToInt(value, cfg.CooldownSeconds); break;
                    case "SCALP-PYRAMIDING": cfg.AllowPyramiding = ToBool(value, cfg.AllowPyramiding); break;
                    case "SCALP-SNAPSHOT-CONFIRM": cfg.RequireSnapshotConfirmation = ToBool(value, cfg.RequireSnapshotConfirmation); break;
                    case "SCALP-MIN-SCORE": cfg.MinDecisionScore = ToInt(value, cfg.MinDecisionScore); break;
                    case "SCALP-AI-CONFIRM": cfg.UseAiConfirmation = ToBool(value, cfg.UseAiConfirmation); break;
                }
            }
        }

        private static void ApplyNormalValues(NormalTradingSettings cfg, IReadOnlyList<TradeRuleRuntimeSnapshot> rules)
        {
            foreach (var rule in rules)
            {
                object? value = rule.PreviewValue ?? rule.ConfiguredValue;
                switch (rule.RuleCode)
                {
                    case "NORMAL-ENABLED": cfg.Enabled = ToBool(value, cfg.Enabled); break;
                    case "NORMAL-MAX-TRADES": cfg.MaxTrades = ToInt(value, cfg.MaxTrades); break;
                    case "NORMAL-EXPIRY": cfg.ExpiryMinutes = ToInt(value, cfg.ExpiryMinutes); break;
                    case "NORMAL-SL-PIPS": cfg.StopLossPips = ToDouble(value, cfg.StopLossPips); break;
                    case "NORMAL-TP-PIPS": cfg.TakeProfitPips = ToDouble(value, cfg.TakeProfitPips); break;
                    case "NORMAL-RISK-REWARD": cfg.RiskRewardRatio = ToDouble(value, cfg.RiskRewardRatio); break;
                    case "NORMAL-SPREAD-LIMIT": cfg.MaxSpreadPips = ToDouble(value, cfg.MaxSpreadPips); break;
                }
            }
        }

        private static ScalpingConfig CloneScalping(ScalpingConfig source) => new()
        {
            MaxTrades = source.MaxTrades,
            MaxMinutes = source.MaxMinutes,
            MaxSessionLossUsd = source.MaxSessionLossUsd,
            ProfitTargetUsd = source.ProfitTargetUsd,
            StopLossPips = source.StopLossPips,
            TakeProfitPips = source.TakeProfitPips,
            RiskRewardRatio = source.RiskRewardRatio,
            MaxSpreadPips = source.MaxSpreadPips,
            MaxSpreadPercentOfTp = source.MaxSpreadPercentOfTp,
            DynamicValuesEnabled = source.DynamicValuesEnabled,
            PollIntervalMs = source.PollIntervalMs,
            CooldownSeconds = source.CooldownSeconds,
            DirectionMode = source.DirectionMode,
            AllowPyramiding = source.AllowPyramiding,
            RequireSnapshotConfirmation = source.RequireSnapshotConfirmation,
            MinDecisionScore = source.MinDecisionScore,
            UseAiConfirmation = source.UseAiConfirmation
        };

        private static NormalTradingSettings CloneNormal(NormalTradingSettings source) => new()
        {
            Enabled = source.Enabled,
            MaxTrades = source.MaxTrades,
            ExpiryMinutes = source.ExpiryMinutes,
            StopLossPips = source.StopLossPips,
            TakeProfitPips = source.TakeProfitPips,
            MaxSpreadPips = source.MaxSpreadPips,
            RiskRewardRatio = source.RiskRewardRatio
        };

        private static double ToDouble(object? value, double fallback) =>
            value switch
            {
                double d => d,
                decimal d => (double)d,
                int i => i,
                string s when double.TryParse(s, out double d) => d,
                _ => fallback
            };

        private static int ToInt(object? value, int fallback) =>
            value switch
            {
                int i => i,
                double d => (int)Math.Round(d),
                decimal d => (int)Math.Round(d),
                string s when int.TryParse(s, out int i) => i,
                string s when double.TryParse(s, out double d) => (int)Math.Round(d),
                _ => fallback
            };

        private static bool ToBool(object? value, bool fallback) =>
            value switch
            {
                bool b => b,
                string s when bool.TryParse(s, out bool b) => b,
                string s when s.Equals("yes", StringComparison.OrdinalIgnoreCase) => true,
                string s when s.Equals("no", StringComparison.OrdinalIgnoreCase) => false,
                _ => fallback
            };
    }
}
