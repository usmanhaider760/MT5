using MT5TradingBot.Models;

namespace MT5TradingBot.Core
{
    public static class PipCalculator
    {
        public static string NormalizeSymbol(string symbol) =>
            symbol.ToUpperInvariant().Replace("/", "").Replace("_", "");

        public static double GetPipSize(string symbol)
        {
            string sym = NormalizeSymbol(symbol);
            if (sym.Contains("JPY")) return 0.01;
            if (sym.Contains("XAU") || sym.Contains("GOLD")) return 0.01;
            if (sym.Contains("XAG")) return 0.01;
            if (sym.Contains("BTC") || sym.Contains("ETH")) return 1.0;
            return 0.0001;
        }

        public static double DistanceInPips(double firstPrice, double secondPrice, double pipSize) =>
            pipSize > 0 ? Math.Abs(firstPrice - secondPrice) / pipSize : 0;

        public static double MoveInPips(
            TradeType direction,
            double openPrice,
            double currentPrice,
            double pipSize)
        {
            if (pipSize <= 0) return 0;

            double move = direction == TradeType.BUY
                ? currentPrice - openPrice
                : openPrice - currentPrice;

            return move / pipSize;
        }

        public static double ProfitUsd(
            TradeType direction,
            double openPrice,
            double closePrice,
            double lots,
            string symbol)
        {
            string normalized = NormalizeSymbol(symbol);
            double pipSize = GetPipSize(normalized);
            double pipValue = LotCalculator.GetPipValuePerLot(normalized);
            return MoveInPips(direction, openPrice, closePrice, pipSize) * pipValue * lots;
        }
    }
}
