using MT5TradingBot.Core;
using MT5TradingBot.Models;

namespace MT5TradingBot.Modules.RiskManagement
{
    public sealed class RiskManager : IRiskManager
    {
        public Task<RiskValidationResult> ValidateAsync(
            TradeRequest request,
            AccountInfo account,
            SymbolInfo? symbolInfo,
            IReadOnlyList<LivePosition> openPositions,
            BotConfig config,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var warnings = new List<string>();

            var (valid, error) = request.Validate();
            if (!valid)
                return Task.FromResult(Blocked(error, warnings));

            if (request.ExpiryMinutes > 0)
            {
                double ageMinutes = (DateTime.UtcNow - request.CreatedAt).TotalMinutes;
                if (ageMinutes > request.ExpiryMinutes)
                    return Task.FromResult(Blocked(
                        $"Signal is {ageMinutes:F0} min old; limit is {request.ExpiryMinutes} min.",
                        warnings));
            }

            string pair = request.Pair.ToUpperInvariant();
            if (config.AllowedPairs.Count > 0 &&
                !config.AllowedPairs.Any(p => string.Equals(p, pair, StringComparison.OrdinalIgnoreCase)))
                return Task.FromResult(Blocked(
                    $"Pair {request.Pair} is not in allowed list: [{string.Join(", ", config.AllowedPairs)}].",
                    warnings));

            if (account.Equity <= 0)
                return Task.FromResult(Blocked("Account equity must be greater than zero.", warnings));

            if (account.FreeMargin < account.Balance * 0.05)
                return Task.FromResult(Blocked(
                    $"Free margin ${account.FreeMargin:F2} is critically low.",
                    warnings));

            double livePrice = symbolInfo != null
                ? request.TradeType == TradeType.BUY ? symbolInfo.Ask : symbolInfo.Bid
                : 0;

            double referenceEntry = request.EntryPrice > 0
                ? request.EntryPrice
                : livePrice > 0
                    ? livePrice
                    : EstimateMarketPrice(request);

            double lotSize = config.AutoLotCalculation
                ? LotCalculator.Calculate(
                    account.Equity,
                    config.MaxRiskPercent,
                    referenceEntry,
                    request.StopLoss,
                    request.Pair)
                : request.LotSize;

            double riskReward = LotCalculator.RiskRewardRatio(
                referenceEntry,
                request.StopLoss,
                request.TakeProfit);

            if (config.EnforceRR && riskReward < config.MinRRRatio)
                return Task.FromResult(Blocked(
                    $"R:R {riskReward:F2} is below minimum {config.MinRRRatio:F2}.",
                    warnings,
                    referenceEntry,
                    lotSize,
                    riskReward));

            if (!config.EnforceRR && riskReward < config.MinRRRatio)
                warnings.Add($"R:R {riskReward:F2} is below configured minimum {config.MinRRRatio:F2}.");

            double dollarRisk = LotCalculator.DollarRisk(
                lotSize,
                referenceEntry,
                request.StopLoss,
                request.Pair);

            double riskPercent = dollarRisk / account.Equity * 100.0;
            if (riskPercent > config.MaxRiskPercent * 1.05)
                return Task.FromResult(Blocked(
                    $"Trade risk {riskPercent:F2}% exceeds max {config.MaxRiskPercent:F2}%.",
                    warnings,
                    referenceEntry,
                    lotSize,
                    riskReward,
                    dollarRisk,
                    riskPercent));

            if (config.MaxTotalRiskPercent > 0)
            {
                double openRisk = openPositions
                    .Where(p => p.StopLoss > 0)
                    .Sum(p => LotCalculator.DollarRisk(p.Lots, p.OpenPrice, p.StopLoss, p.Symbol));

                double totalRiskPercent = (openRisk + dollarRisk) / account.Equity * 100.0;
                if (totalRiskPercent > config.MaxTotalRiskPercent)
                    return Task.FromResult(Blocked(
                        $"Total portfolio risk would be {totalRiskPercent:F2}%; cap is {config.MaxTotalRiskPercent:F2}%.",
                        warnings,
                        referenceEntry,
                        lotSize,
                        riskReward,
                        dollarRisk,
                        riskPercent));
            }

            double spreadPips = symbolInfo?.SpreadPips ?? 0;
            if (config.MaxSpreadPips > 0)
            {
                if (symbolInfo == null)
                    warnings.Add($"Spread unavailable for {request.Pair}; caller should decide whether to continue.");
                else if (spreadPips > config.MaxSpreadPips)
                    return Task.FromResult(Blocked(
                        $"{request.Pair} spread {spreadPips:F1} pips exceeds max {config.MaxSpreadPips:F1} pips.",
                        warnings,
                        referenceEntry,
                        lotSize,
                        riskReward,
                        dollarRisk,
                        riskPercent,
                        spreadPips));
            }

            return Task.FromResult(new RiskValidationResult
            {
                IsApproved = true,
                RiskLevel = DetermineRiskLevel(riskPercent, config.MaxRiskPercent),
                Reason = "Risk validation passed.",
                RiskPercent = Math.Round(riskPercent, 2),
                DollarRisk = dollarRisk,
                RiskRewardRatio = riskReward,
                SpreadPips = spreadPips,
                ReferenceEntryPrice = referenceEntry,
                ValidatedLotSize = lotSize,
                Warnings = warnings
            });
        }

        private static RiskValidationResult Blocked(
            string reason,
            IReadOnlyList<string> warnings,
            double referenceEntry = 0,
            double lotSize = 0,
            double riskReward = 0,
            double dollarRisk = 0,
            double riskPercent = 0,
            double spreadPips = 0) =>
            new()
            {
                IsApproved = false,
                RiskLevel = RiskLevel.Blocked,
                Reason = reason,
                ReferenceEntryPrice = referenceEntry,
                ValidatedLotSize = lotSize,
                RiskRewardRatio = riskReward,
                DollarRisk = dollarRisk,
                RiskPercent = Math.Round(riskPercent, 2),
                SpreadPips = spreadPips,
                Warnings = warnings
            };

        private static RiskLevel DetermineRiskLevel(double riskPercent, double maxRiskPercent)
        {
            if (maxRiskPercent <= 0) return RiskLevel.High;
            double ratio = riskPercent / maxRiskPercent;
            if (ratio <= 0.5) return RiskLevel.Low;
            if (ratio <= 0.85) return RiskLevel.Medium;
            return RiskLevel.High;
        }

        private static double EstimateMarketPrice(TradeRequest request) =>
            request.TradeType == TradeType.BUY
                ? request.StopLoss * 1.002
                : request.StopLoss * 0.998;
    }
}
