using MT5GoldScalper.V2.Core.Abstractions;
using MT5GoldScalper.V2.Core.Models;

namespace MT5GoldScalper.V2.Core.Services.Calculations;

public sealed class RiskCalculationService : IRiskCalculationService
{
    public void Calculate(TradingDecisionSnapshot snapshot)
    {
        var account = snapshot.AccountRisk;
        account.RiskAmount = Math.Round(account.Balance * account.RiskPercent / 100m, 2, MidpointRounding.AwayFromZero);
        account.DailyLossRemaining = account.DailyLossLimit <= 0
            ? account.DailyLossRemaining
            : Math.Max(0, account.DailyLossLimit + account.DailyProfitLoss);
        snapshot.ExecutionSafety.RiskWithinLimits = account.DailyLossRemaining > 0 && account.TradesTakenToday < account.MaxTradesPerDay;
    }
}
