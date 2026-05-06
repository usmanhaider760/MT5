namespace MT5TradingBot.Modules.TradeRules
{
    public static class TradeRuleStatusEvaluator
    {
        public static void ApplyEnabledState(TradeRuleRuntimeSnapshot snapshot, string wouldHaveResult)
        {
            if (snapshot.IsEnabled)
            {
                snapshot.Result = Normalize(wouldHaveResult);
                snapshot.WouldHaveResult = null;
                snapshot.ActualEffect = "Active rule participates in future checks.";
                return;
            }

            snapshot.Result = TradeRuleResults.Disabled;
            snapshot.WouldHaveResult = Normalize(wouldHaveResult);
            snapshot.ActualEffect = "Ignored because rule is disabled.";
        }

        public static TradeRuleDecisionSummary BuildSummary(IReadOnlyList<TradeRuleRuntimeSnapshot> rules)
        {
            int blocked = rules.Count(r => r.Result == TradeRuleResults.Block);
            int warnings = rules.Count(r => r.Result == TradeRuleResults.Warning);
            int disabledButWouldBlock = rules.Count(r =>
                r.Result == TradeRuleResults.Disabled && r.WouldHaveResult == TradeRuleResults.Block);

            return new TradeRuleDecisionSummary
            {
                CurrentDecision = blocked > 0 ? "NO TRADE" : "UNKNOWN",
                MainBlockingRule = rules.FirstOrDefault(r => r.Result == TradeRuleResults.Block)?.RuleCode ?? "",
                RiskLevel = blocked > 0 || disabledButWouldBlock > 0 ? "High" : warnings > 0 ? "Medium" : "Low",
                Passed = rules.Count(r => r.Result == TradeRuleResults.Pass),
                Warning = warnings,
                Blocked = blocked,
                Disabled = rules.Count(r => r.Result == TradeRuleResults.Disabled),
                DisabledButWouldBlock = disabledButWouldBlock
            };
        }

        private static string Normalize(string result) =>
            result is TradeRuleResults.Pass or TradeRuleResults.Warning or TradeRuleResults.Block or TradeRuleResults.NotChecked
                ? result
                : TradeRuleResults.NotChecked;
    }
}
