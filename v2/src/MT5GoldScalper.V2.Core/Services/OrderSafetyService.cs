using MT5GoldScalper.V2.Core.Abstractions;
using MT5GoldScalper.V2.Core.Configuration;
using MT5GoldScalper.V2.Core.Models;

namespace MT5GoldScalper.V2.Core.Services;

public sealed class OrderSafetyService(
    DecisionMakerOptions options,
    ILotSizeCalculationService lotSizeCalculation,
    IStopValidationService stopValidation,
    ITargetCalculationService targetCalculation) : IOrderSafetyService
{
    public Task<ExecutionSafetyModel> EvaluateAsync(TradingDecisionSnapshot snapshot, CancellationToken ct = default)
    {
        var market = snapshot.Market;
        var account = snapshot.AccountRisk;
        var news = snapshot.SessionNews;
        var signal = snapshot.StrategySignal;

        var model = new ExecutionSafetyModel
        {
            TerminalConnected = true,
            TradingAllowed = true,
            MarketOpen = market.MarketOpen,
            PriceFresh = market.PriceFresh && market.LastTickAgeMs <= options.MaxTickAgeMs,
            SpreadAcceptable = market.SpreadPips <= options.MaxSpreadPips,
            VolumeValid = lotSizeCalculation.IsVolumeValid(account, market),
            StopsValid = stopValidation.AreStopsValid(snapshot) && targetCalculation.AreTargetsValid(snapshot),
            MarginEnough = account.FreeMargin >= account.EstimatedMarginRequired,
            RiskWithinLimits = account.DailyLossRemaining > 0 && account.TradesTakenToday < options.MaxTradesPerDay,
            RiskLimitPass = account.DailyLossRemaining > 0 && account.TradesTakenToday < options.MaxTradesPerDay,
            NewsFilterPass = !news.NewsBlackoutActive,
            DuplicateTradePass = !account.SamePairOpenPosition || account.DuplicateTradeAllowed,
            OrderCheckPassed = true,
            OrderCheckRetcode = 0,
            OrderCheckComment = "Prototype check passed."
        };

        model.FinalReadyToTrade =
            model.TerminalConnected &&
            model.TradingAllowed &&
            model.MarketOpen &&
            model.PriceFresh &&
            model.SpreadAcceptable &&
            model.VolumeValid &&
            model.StopsValid &&
            model.MarginEnough &&
            model.RiskLimitPass &&
            model.NewsFilterPass &&
            model.DuplicateTradePass &&
            model.OrderCheckPassed;

        return Task.FromResult(model);
    }
}
