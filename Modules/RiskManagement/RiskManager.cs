using MT5TradingBot.Core;
using MT5TradingBot.Models;
using MT5TradingBot.Modules.PairSettings;

namespace MT5TradingBot.Modules.RiskManagement
{
    public sealed class RiskManager : IRiskManager
    {
        private readonly IPairSettingsService? _pairSettings;

        public RiskManager(IPairSettingsService? pairSettings = null)
        {
            _pairSettings = pairSettings;
        }

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

            // Max concurrent open positions cap
            if (config.MaxConcurrentPositions > 0)
            {
                int botPositions = openPositions.Count(
                    p => p.MagicNumber == config.MagicNumber);

                if (botPositions >= config.MaxConcurrentPositions)
                    return Task.FromResult(Blocked(
                        $"Already have {botPositions} open position(s) " +
                        $"(max {config.MaxConcurrentPositions}). " +
                        $"Close an existing position before opening a new one.",
                        warnings));
            }

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
            var pairRules = _pairSettings?.GetForPair(request.Pair);
            double minRr = pairRules?.ScalpingMinRR > 0 ? pairRules.ScalpingMinRR : config.MinRRRatio;

            if (config.EnforceRR && riskReward < minRr)
                return Task.FromResult(Blocked(
                    $"R:R {riskReward:F2} is below minimum {minRr:F2}.",
                    warnings,
                    referenceEntry,
                    lotSize,
                    riskReward));

            if (!config.EnforceRR && riskReward < minRr)
                warnings.Add($"R:R {riskReward:F2} is below configured minimum {minRr:F2}.");

            if (pairRules != null && referenceEntry > 0)
            {
                double pipSize = pairRules.PipSize > 0 ? pairRules.PipSize : LotCalculator.GetPipSize(request.Pair);
                double slPips = Math.Abs(referenceEntry - request.StopLoss) / pipSize;
                double tpPips = Math.Abs(request.TakeProfit - referenceEntry) / pipSize;

                if (pairRules.MinSlPips > 0 && slPips < pairRules.MinSlPips)
                    return Task.FromResult(Blocked(
                        $"{request.Pair} SL distance {slPips:F1} pips is below pair minimum {pairRules.MinSlPips:F1} pips.",
                        warnings,
                        referenceEntry,
                        lotSize,
                        riskReward));

                if (pairRules.MaxSlPips > 0 && slPips > pairRules.MaxSlPips)
                    return Task.FromResult(Blocked(
                        $"{request.Pair} SL distance {slPips:F1} pips exceeds pair maximum {pairRules.MaxSlPips:F1} pips.",
                        warnings,
                        referenceEntry,
                        lotSize,
                        riskReward));

                if (pairRules.MinTpPips > 0 && tpPips < pairRules.MinTpPips)
                    return Task.FromResult(Blocked(
                        $"{request.Pair} TP distance {tpPips:F1} pips is below pair minimum {pairRules.MinTpPips:F1} pips.",
                        warnings,
                        referenceEntry,
                        lotSize,
                        riskReward));
            }

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
            double maxSpreadPips = request.MaxSpreadPips > 0
                ? request.MaxSpreadPips
                : pairRules?.MaxSpreadPips > 0
                    ? pairRules.MaxSpreadPips
                    : config.MaxSpreadPips;
            if (maxSpreadPips > 0)
            {
                if (symbolInfo == null)
                    warnings.Add($"Spread unavailable for {request.Pair}; caller should decide whether to continue.");
                else if (spreadPips > maxSpreadPips)
                    return Task.FromResult(Blocked(
                        $"{request.Pair} spread {spreadPips:F1} pips exceeds max {maxSpreadPips:F1} pips.",
                        warnings,
                        referenceEntry,
                        lotSize,
                        riskReward,
                        dollarRisk,
                        riskPercent,
                        spreadPips));
                else if (pairRules?.AvoidTradeIfSpreadAbovePercentOfTp > 0 && referenceEntry > 0)
                {
                    double pipSize = pairRules.PipSize > 0 ? pairRules.PipSize : LotCalculator.GetPipSize(request.Pair);
                    double tpPips = Math.Abs(request.TakeProfit - referenceEntry) / pipSize;
                    double spreadPercentOfTp = tpPips > 0 ? spreadPips / tpPips * 100.0 : 0;
                    if (tpPips > 0 && spreadPercentOfTp > pairRules.AvoidTradeIfSpreadAbovePercentOfTp)
                        return Task.FromResult(Blocked(
                            $"{request.Pair} spread is {spreadPercentOfTp:F1}% of TP distance; pair max is {pairRules.AvoidTradeIfSpreadAbovePercentOfTp:F1}%.",
                            warnings,
                            referenceEntry,
                            lotSize,
                            riskReward,
                            dollarRisk,
                            riskPercent,
                            spreadPips));
                }
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
