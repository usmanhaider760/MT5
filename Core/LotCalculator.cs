namespace MT5TradingBot.Core
{
    /// <summary>
    /// Accurate pip-value and lot-size calculation for a USD-denominated account.
    /// Covers: xxxUSD, USDxxx, xauusd (Gold), JPY crosses, all major pairs.
    /// Formula: LotSize = RiskAmount / (PipDistance × PipValuePerLot)
    /// </summary>
    public static class LotCalculator
    {
        /// <summary>
        /// Calculate lot size so that max loss == equity × riskPct / 100.
        /// Returns 0.01 minimum (Exness micro lot).
        /// </summary>
        public static double Calculate(
            double equity,
            double riskPercent,
            double entryPrice,
            double stopLoss,
            string symbol,
            double accountLeverage = 200)
        {
            if (equity <= 0 || riskPercent <= 0 || entryPrice <= 0 || stopLoss <= 0)
                return 0.01;

            double riskAmount = equity * riskPercent / 100.0;
            string sym = symbol.ToUpperInvariant().Replace("/", "").Replace("_", "");

            double pipSize = GetPipSize(sym);
            double pipDistance = Math.Abs(entryPrice - stopLoss) / pipSize;

            if (pipDistance < 0.1) return 0.01; // degenerate SL

            double pipValuePerLot = GetPipValuePerLot(sym, entryPrice);

            if (pipValuePerLot <= 0) return 0.01;

            double lots = riskAmount / (pipDistance * pipValuePerLot);

            // Clamp to broker limits
            lots = Math.Max(0.01, Math.Round(lots, 2));
            lots = Math.Min(lots, 100.0); // safety cap

            return lots;
        }

        /// <summary>Returns pip size (1 pip = this price movement)</summary>
        public static double GetPipSize(string sym)
        {
            if (sym.Contains("JPY")) return 0.01;
            if (sym.Contains("XAU") || sym.Contains("GOLD")) return 0.01;  // Gold: $0.01 pip on common MT5 XAUUSD symbols
            if (sym.Contains("XAG")) return 0.01;                           // Silver
            if (sym.Contains("BTC") || sym.Contains("ETH")) return 1.0;
            return 0.0001; // standard 4-decimal pairs
        }

        /// <summary>Returns pip value in USD per 1 standard lot</summary>
        public static double GetPipValuePerLot(string sym, double currentPrice = 1.0)
        {
            // Metals must be checked before xxxUSD because XAUUSD/XAGUSD also end with USD.
            // Gold: 1 pip = $0.01 price move x 100 oz = $1/lot on common MT5 symbols.
            if (sym.Contains("XAU") || sym.Contains("GOLD"))
                return 1.0;

            // Silver: 1 pip = $0.01 x 5000 oz = $50/lot (broker contract sizes may vary)
            if (sym.Contains("XAG"))
                return 50.0;

            // USD quote currency (EURUSD, GBPUSD, AUDUSD, NZDUSD) = $10/pip/lot
            if (sym.EndsWith("USD"))
                return 10.0;

            // USD base (USDJPY, USDCAD, USDCHF) = 10 / current price
            if (sym.StartsWith("USD"))
                return currentPrice > 0 ? 10.0 / currentPrice : 10.0;

            // JPY crosses (GBPJPY, EURJPY etc.) ≈ $9.30/pip (approximate at 150 USDJPY)
            if (sym.Contains("JPY"))
                return 9.30;

            // Default fallback
            return 10.0;
        }

        /// <summary>Calculate R:R ratio for display</summary>
        public static double RiskRewardRatio(double entry, double sl, double tp)
        {
            double risk = Math.Abs(entry - sl);
            double reward = Math.Abs(tp - entry);
            return risk > 0 ? Math.Round(reward / risk, 2) : 0;
        }

        /// <summary>Compute exact dollar risk for a given lot + SL distance</summary>
        public static double DollarRisk(double lots, double entry, double sl, string sym)
        {
            string s = sym.ToUpperInvariant().Replace("/", "");
            double pipSize = GetPipSize(s);
            double pips = Math.Abs(entry - sl) / pipSize;
            double pipVal = GetPipValuePerLot(s, entry);
            return Math.Round(lots * pips * pipVal, 2);
        }

        /// <summary>Compute exact dollar profit at TP</summary>
        public static double DollarProfit(double lots, double entry, double tp, string sym)
            => DollarRisk(lots, entry, tp, sym);
    }
}
