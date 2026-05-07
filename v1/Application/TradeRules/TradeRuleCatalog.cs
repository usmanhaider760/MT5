namespace MT5TradingBot.Modules.TradeRules
{
    public sealed class TradeRuleCatalog
    {
        private static readonly IReadOnlyList<TradeRuleCatalogItem> FixedItems =
        [
            Item("COMMON-TRADING-MODE", "Trading Mode", "Common Rules", "Execution Mode", "Bot.CommonTrading.TradingMode", "Enum"),
            Item("COMMON-AI-CONFIRM", "AI Confirmation", "Common Rules", "AI", "Bot.CommonTrading.UseAiConfirmation", "Bool"),
            Item("COMMON-AUTO-CLOSE", "Auto Close After Open", "Common Rules", "Trade Management", "Bot.CommonTrading.AutoCloseAfterOpen", "Bool"),
            Item("COMMON-PROFIT-PIPS", "Common Profit Target Pips", "Common Rules", "Targets", "Bot.CommonTrading.ProfitTargetPips"),
            Item("COMMON-PROFIT-USD", "Common Profit Target USD", "Common Rules", "Targets", "Bot.CommonTrading.ProfitTargetUsd"),
            Item("COMMON-BREAKEVEN-TRIGGER", "Move SL to Breakeven Trigger", "Common Rules", "Trade Management", "Bot.CommonTrading.BeTriggerPercentOfTp"),
            Item("COMMON-MAX-SPREAD", "Common Max Spread Limit", "Common Rules", "Spread", "Bot.MaxSpreadPips"),
            Item("COMMON-MAX-POSITIONS", "Max Open Positions", "Common Rules", "Exposure", "Bot.MaxConcurrentPositions", critical: true),
            Item("COMMON-CORRELATION", "Correlation Protection", "Common Rules", "Exposure", "Bot.CorrelationProtection", "Bool"),

            Item("SCALP-ENABLED", "Scalping Enabled", "Scalping Rules", "Runtime Session Rules", "Bot.Scalping.Enabled", "Bool"),
            Item("SCALP-MAX-TRADES", "Max Scalping Trades", "Scalping Rules", "Runtime Session Rules", "Bot.Scalping.MaxTrades"),
            Item("SCALP-MAX-MINUTES", "Scalping Session Time Limit", "Scalping Rules", "Runtime Session Rules", "Bot.Scalping.MaxMinutes"),
            Item("SCALP-SESSION-LOSS", "Scalping Session Loss Limit", "Scalping Rules", "Runtime Session Rules", "Bot.Scalping.MaxSessionLossUsd", critical: true),
            Item("SCALP-PROFIT-TARGET", "Scalping Profit Target", "Scalping Rules", "Runtime Session Rules", "Bot.Scalping.ProfitTargetUsd"),
            Item("SCALP-SL-PIPS", "Scalping Stop Loss Pips", "Scalping Rules", "Request Build Rules", "Bot.Scalping.StopLossPips"),
            Item("SCALP-TP-PIPS", "Scalping Take Profit Pips", "Scalping Rules", "Request Build Rules", "Bot.Scalping.TakeProfitPips"),
            Item("SCALP-RISK-REWARD", "Scalping Risk Reward", "Scalping Rules", "Request Build Rules", "Bot.Scalping.RiskRewardRatio"),
            Item("SCALP-SPREAD-LIMIT", "Scalping Spread Limit", "Scalping Rules", "Spread Rules", "Bot.Scalping.MaxSpreadPips"),
            Item("SCALP-SPREAD-TP-PERCENT", "Spread Percent of TP", "Scalping Rules", "Spread Rules", "Bot.Scalping.MaxSpreadPercentOfTp"),
            Item("SCALP-DYNAMIC-VALUES", "Dynamic Scalping Values", "Scalping Rules", "Request Build Rules", "Bot.Scalping.DynamicValuesEnabled", "Bool"),
            Item("SCALP-POLL-INTERVAL", "Scalping Check Interval", "Scalping Rules", "Runtime Session Rules", "Bot.Scalping.PollIntervalMs"),
            Item("SCALP-COOLDOWN", "Scalping Cooldown", "Scalping Rules", "Runtime Session Rules", "Bot.Scalping.CooldownSeconds"),
            Item("SCALP-DIRECTION-MODE", "Scalping Direction Mode", "Scalping Rules", "Direction Rules", "Bot.Scalping.DirectionMode", "Enum"),
            Item("SCALP-PYRAMIDING", "Pyramiding Rule", "Scalping Rules", "Direction Rules", "Bot.Scalping.AllowPyramiding", "Bool"),
            Item("SCALP-SNAPSHOT-CONFIRM", "Snapshot Confirmation", "Scalping Rules", "Direction Rules", "Bot.Scalping.RequireSnapshotConfirmation", "Bool"),
            Item("SCALP-MIN-SCORE", "Minimum Scalping Score", "Scalping Rules", "Score Rules", "Bot.Scalping.MinDecisionScore"),
            Item("SCALP-AI-CONFIRM", "Scalping AI Confirmation", "Scalping Rules", "Score Rules", "Bot.Scalping.UseAiConfirmation", "Bool"),
            Item("SCALP-BUY-SCORE", "BUY Score", "Scalping Rules", "Score Rules", "LastBuyScore", editable: false),
            Item("SCALP-SELL-SCORE", "SELL Score", "Scalping Rules", "Score Rules", "LastSellScore", editable: false),
            Item("SCALP-DIRECTION-TIE", "Buy/Sell Equal Strength Block", "Scalping Rules", "Direction Rules", "DirectionTie", "Bool"),
            Item("SCALP-REQUEST-BUILD", "Scalping Trade Request Build", "Scalping Rules", "Request Build Rules", "TradeRequest", "Text", editable: false),

            Item("NORMAL-ENABLED", "Normal Trading Enabled", "Normal Rules", "Normal Runtime", "Bot.NormalTrading.Enabled", "Bool"),
            Item("NORMAL-MAX-TRADES", "Max Normal Trades", "Normal Rules", "Normal Runtime", "Bot.NormalTrading.MaxTrades"),
            Item("NORMAL-EXPIRY", "Normal Trade Expiry", "Normal Rules", "Normal Runtime", "Bot.NormalTrading.ExpiryMinutes"),
            Item("NORMAL-SL-PIPS", "Normal Stop Loss Pips", "Normal Rules", "Normal Request", "Bot.NormalTrading.StopLossPips"),
            Item("NORMAL-TP-PIPS", "Normal Take Profit Pips", "Normal Rules", "Normal Request", "Bot.NormalTrading.TakeProfitPips"),
            Item("NORMAL-RISK-REWARD", "Normal Risk Reward", "Normal Rules", "Normal Request", "Bot.NormalTrading.RiskRewardRatio"),
            Item("NORMAL-SPREAD-LIMIT", "Normal Spread Limit", "Normal Rules", "Normal Spread", "Bot.NormalTrading.MaxSpreadPips"),

            Item("PAIR-PIP-SIZE", "Pair Pip Size", "Pair Rules", "Pair Metadata", "PairSettings.PipSize"),
            Item("PAIR-M5-ATR-MIN", "M5 Minimum ATR", "Pair Rules", "ATR", "PairSettings.MinAtrPipsM5"),
            Item("PAIR-M5-ATR-MAX", "M5 Maximum ATR", "Pair Rules", "ATR", "PairSettings.MaxAtrPipsM5"),
            Item("PAIR-M15-ATR-MIN", "M15 Minimum ATR", "Pair Rules", "ATR", "PairSettings.MinAtrPipsM15"),
            Item("PAIR-M15-ATR-MAX", "M15 Maximum ATR", "Pair Rules", "ATR", "PairSettings.MaxAtrPipsM15"),
            Item("PAIR-KEYLEVEL-DISTANCE", "Key Level Distance", "Pair Rules", "Market Structure", "PairSettings.MinimumDistanceFromKeyLevelPips"),
            Item("PAIR-TRAILING-START", "Trailing Start", "Pair Rules", "Trade Management", "PairSettings.TrailingStartPips"),
            Item("PAIR-TRAILING-STEP", "Trailing Step", "Pair Rules", "Trade Management", "PairSettings.TrailingStepPips"),
            Item("PAIR-MAX-SLIPPAGE", "Pair Max Slippage", "Pair Rules", "Execution Cost", "PairSettings.MaxSlippagePips"),
            Item("PAIR-RECOMMENDED-SESSION", "Recommended Session", "Pair Rules", "Session", "PairSettings.RecommendedSessions", "List"),
            Item("PAIR-AVOID-SESSION", "Avoid Session", "Pair Rules", "Session", "PairSettings.AvoidSessions", "List"),

            Item("BROKER-SYMBOL-DATA", "Broker Symbol Data", "Broker Rules", "Broker Data", "MT5Bridge.GetSymbolInfoAsync", "Text", critical: true, editable: false),
            Item("BROKER-STOP-LEVEL", "Broker Stop Level", "Broker Rules", "Broker Limits", "BrokerStopLevelValidator", critical: true),
            Item("BROKER-FREEZE-LEVEL", "Broker Freeze Level", "Broker Rules", "Broker Limits", "BrokerFreezeLevelValidator", critical: true),
            Item("BROKER-LOT-SIZE", "Broker Lot Size", "Broker Rules", "Broker Limits", "BrokerLotSizeValidator", critical: true),
            Item("BROKER-ORDER-CHECK", "Broker OrderCheck", "Broker Rules", "Broker Precheck", "MT5Bridge.TryCheckOrderAsync", "Text", critical: true, editable: false),
            Item("BROKER-MARKET-OPEN", "Market Open / Trade Allowed", "Broker Rules", "Broker Data", "SymbolInfo.TradeAllowed", "Bool", critical: true),
            Item("BROKER-COMMISSION", "Commission Model", "Broker Rules", "Execution Cost", "CommissionCalculator"),
            Item("BROKER-SLIPPAGE", "Slippage Model", "Broker Rules", "Execution Cost", "SlippageCalculator"),

            Item("ACCOUNT-DATA", "Account Data Available", "Account Protection", "Account Data", "MT5Bridge.GetAccountInfoAsync", "Text", critical: true, editable: false),
            Item("ACCOUNT-DAILY-LOSS", "Daily Loss Limit", "Account Protection", "Loss Limits", "Bot.DailyLossLimit", critical: true),
            Item("ACCOUNT-WEEKLY-LOSS", "Weekly Loss Limit", "Account Protection", "Loss Limits", "Bot.WeeklyLossLimit", critical: true),
            Item("ACCOUNT-FLOATING-LOSS", "Floating Loss Protection", "Account Protection", "Loss Limits", "AccountInfo.FloatingProfit", critical: true),
            Item("ACCOUNT-SYMBOL-EXPOSURE", "Same Symbol Exposure", "Account Protection", "Exposure", "SymbolExposure", critical: true),
            Item("ACCOUNT-MAX-CONCURRENT", "Max Concurrent Positions", "Account Protection", "Exposure", "Bot.MaxConcurrentPositions", critical: true),
            Item("ACCOUNT-MARGIN", "Projected Margin Validation", "Account Protection", "Margin", "MT5Bridge.TryGetMarginEstimateAsync", critical: true),
            Item("ACCOUNT-DRAWDOWN", "Emergency Drawdown Stop", "Account Protection", "Kill Switch", "Bot.EmergencyCloseDrawdownPct", critical: true),
            Item("ACCOUNT-KILL-SWITCH", "Kill Switch", "Account Protection", "Kill Switch", "AutoBotService.KillSwitch", "Bool", critical: true),

            Item("SAFETY-ROLLOUT-STAGE", "Rollout Stage", "Safety / News / Session", "Rollout", "RolloutEvaluator", "Enum", critical: true),
            Item("SAFETY-NO-TRADE-WINDOW", "No-Trade Window", "Safety / News / Session", "Session", "NoTradeWindowValidator", "List", critical: true),
            Item("SAFETY-SESSION-GATE", "Pair Session Gate", "Safety / News / Session", "Session", "PairSessionWindow"),
            Item("SAFETY-SIGNAL-AGE", "Signal Age / Expiry", "Safety / News / Session", "Signal", "TradeRequest.ExpiryMinutes"),
            Item("SAFETY-PAIR-ALLOWLIST", "Pair Allowlist", "Safety / News / Session", "Signal", "Bot.AllowedPairs", "List", critical: true),
            Item("SAFETY-NEWS-BLACKOUT", "News Blackout Filter", "Safety / News / Session", "News", "INewsFilterService", "Bool", critical: true),
            Item("SAFETY-ADX-RANGING", "ADX Ranging Filter", "Safety / News / Session", "Market Structure", "Snapshot.ADX"),
            Item("SAFETY-FINAL-LIVE-READY", "Final Live Readiness Gate", "Safety / News / Session", "Rollout", "LiveReadinessGate", "Bool", critical: true),
            Item("SAFETY-EDGE-MONITOR", "Edge Monitor", "Safety / News / Session", "Rollout", "EdgeHealthMonitor", "Bool", critical: true),

            Item("EXEC-FINAL-GATE", "Final Execution Gate", "Decision Audit", "Execution", "AutoBotService.ExecuteTradeWithValidationAsync", "Text", critical: true, editable: false),
            Item("EXEC-RISK-VALIDATION", "Risk Validation", "Decision Audit", "Execution", "RiskManager.ValidateAsync", "Text", critical: true, editable: false),
            Item("EXEC-EFFECTIVE-SETTINGS", "Effective Settings Resolve", "Decision Audit", "Execution", "EffectiveTradeSettings.Resolve", "Text", editable: false),
            Item("EXEC-REQUEST-VALIDATION", "Trade Request Validation", "Decision Audit", "Execution", "TradeRequest.Validate", "Text", critical: true, editable: false),
            Item("EXEC-ORDER-SEND", "Order Send Result", "Decision Audit", "Execution", "MT5Bridge.OpenTradeAsync", "Text", critical: true, editable: false),
            Item("EXEC-ORDER-RETRY", "Order Retry Policy", "Decision Audit", "Execution", "AutoBotService.ExecuteWithRetryAsync", "Text", editable: false),
            Item("EXEC-TRADE-ACCEPTED", "Trade Accepted", "Decision Audit", "Execution", "TradeResult.Success", "Text", editable: false),
            Item("EXEC-TRADE-REJECTED", "Trade Rejected", "Decision Audit", "Execution", "TradeResult.Rejected", "Text", editable: false),
            Item("EXEC-NO-TRADE", "No Trade Decision", "Decision Audit", "Execution", "StrategyDecision.NoTrade", "Text", editable: false)
        ];

        public IReadOnlyList<TradeRuleCatalogItem> GetAll() => FixedItems;

        public TradeRuleCatalogItem? Find(string ruleCode) =>
            FixedItems.FirstOrDefault(item => string.Equals(item.RuleCode, ruleCode, StringComparison.OrdinalIgnoreCase));

        private static TradeRuleCatalogItem Item(
            string code,
            string name,
            string category,
            string group,
            string variable,
            string valueType = TradeRuleValueTypes.Number,
            bool critical = false,
            bool editable = true) =>
            new()
            {
                RuleCode = code,
                RuleName = name,
                Category = category,
                GroupName = group,
                VariableName = variable,
                SourceFile = ResolveSourceFile(category, variable),
                SourceName = category,
                FunctionName = variable.Contains('.', StringComparison.Ordinal) ? variable.Split('.')[0] : variable,
                ValueType = valueType,
                IsCritical = critical,
                IsEditable = editable
            };

        private static string ResolveSourceFile(string category, string variable)
        {
            if (variable.StartsWith("MT5Bridge.", StringComparison.Ordinal)) return "Infrastructure/MT5/MT5Bridge.cs";
            if (variable.StartsWith("Bot.", StringComparison.Ordinal) || variable.StartsWith("PairSettings.", StringComparison.Ordinal)) return "Domain/Models/Models.cs";
            if (variable.StartsWith("AutoBotService.", StringComparison.Ordinal)) return "Application/Workflows/AutoBotService.cs";
            if (category == "Broker Rules") return "Domain/Common/";
            if (category == "Decision Audit") return "Application/Workflows/AutoBotService.cs";
            if (category == "Safety / News / Session") return "Application/LiveReadiness/";
            return "";
        }
    }
}
