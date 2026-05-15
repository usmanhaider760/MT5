using MT5GoldScalper.V2.Core.Abstractions;
using MT5GoldScalper.V2.Core.Configuration;
using MT5GoldScalper.V2.Core.Engines;
using MT5GoldScalper.V2.Core.Models;
using MT5GoldScalper.V2.Core.Services;
using MT5GoldScalper.V2.Core.Services.Calculations;
using Xunit;

namespace MT5GoldScalper.V2.Tests;

public sealed class CalculationServicesTests
{
    private readonly DecisionMakerOptions _options = new();
    private readonly IPipCalculationService _pip = new PipCalculationService();
    private readonly ITargetCalculationService _targets;
    private readonly IStopValidationService _stops;
    private readonly ISpreadCalculationService _spread;
    private readonly IRiskCalculationService _risk = new RiskCalculationService();
    private readonly ILotSizeCalculationService _lots = new LotSizeCalculationService();
    private readonly IRewardRiskCalculationService _rewardRisk;

    public CalculationServicesTests()
    {
        _targets = new TargetCalculationService(_pip);
        _stops = new StopValidationService(_pip);
        _spread = new SpreadCalculationService(_pip, _options);
        _rewardRisk = new RewardRiskCalculationService(_pip);
    }

    [Fact]
    public void Calculates_Buy_Spread()
    {
        var snapshot = CreateSnapshot(TradeDirection.Buy);

        _targets.Calculate(snapshot);
        _spread.Calculate(snapshot);

        Assert.Equal(0.00012m, snapshot.Market.SpreadPrice);
        Assert.Equal(12, snapshot.Market.SpreadPoints);
        Assert.Equal(1.2m, snapshot.Market.SpreadPips);
        Assert.True(snapshot.ExecutionSafety.SpreadAcceptable);
    }

    [Fact]
    public void Calculates_Sell_Spread()
    {
        var snapshot = CreateSnapshot(TradeDirection.Sell);

        _targets.Calculate(snapshot);
        _spread.Calculate(snapshot);

        Assert.Equal(0.00012m, snapshot.Market.SpreadPrice);
        Assert.Equal(12, snapshot.Market.SpreadPoints);
        Assert.Equal(1.2m, snapshot.Market.SpreadPips);
    }

    [Fact]
    public void Buy_StopLoss_Below_Entry_Is_Valid()
    {
        var snapshot = CreateSnapshot(TradeDirection.Buy, entry: 1.10000m, stopLoss: 1.09900m);

        _stops.Calculate(snapshot);

        Assert.True(_stops.AreStopsValid(snapshot));
        Assert.Equal(10m, snapshot.StrategySignal.StopLossPips);
    }

    [Fact]
    public void Buy_StopLoss_Above_Entry_Is_Invalid()
    {
        var snapshot = CreateSnapshot(TradeDirection.Buy, entry: 1.10000m, stopLoss: 1.10100m);

        _stops.Calculate(snapshot);

        Assert.False(_stops.AreStopsValid(snapshot));
    }

    [Fact]
    public void Sell_StopLoss_Above_Entry_Is_Valid()
    {
        var snapshot = CreateSnapshot(TradeDirection.Sell, entry: 1.10000m, stopLoss: 1.10100m);

        _stops.Calculate(snapshot);

        Assert.True(_stops.AreStopsValid(snapshot));
        Assert.Equal(10m, snapshot.StrategySignal.StopLossPips);
    }

    [Fact]
    public void Sell_StopLoss_Below_Entry_Is_Invalid()
    {
        var snapshot = CreateSnapshot(TradeDirection.Sell, entry: 1.10000m, stopLoss: 1.09900m);

        _stops.Calculate(snapshot);

        Assert.False(_stops.AreStopsValid(snapshot));
    }

    [Fact]
    public void Buy_Targets_Validate_Direction()
    {
        var valid = CreateSnapshot(TradeDirection.Buy, tp1: 1.10100m, tp2: 1.10200m);
        var invalid = CreateSnapshot(TradeDirection.Buy, tp1: 1.09900m, tp2: 1.09800m);

        _targets.Calculate(valid);
        _targets.Calculate(invalid);

        Assert.True(_targets.AreTargetsValid(valid));
        Assert.False(_targets.AreTargetsValid(invalid));
        Assert.Equal(10m, valid.StrategySignal.Tp1Pips);
        Assert.Equal(20m, valid.StrategySignal.Tp2Pips);
    }

    [Fact]
    public void Sell_Targets_Validate_Direction()
    {
        var valid = CreateSnapshot(TradeDirection.Sell, tp1: 1.09900m, tp2: 1.09800m);
        var invalid = CreateSnapshot(TradeDirection.Sell, tp1: 1.10100m, tp2: 1.10200m);

        _targets.Calculate(valid);
        _targets.Calculate(invalid);

        Assert.True(_targets.AreTargetsValid(valid));
        Assert.False(_targets.AreTargetsValid(invalid));
        Assert.Equal(10m, valid.StrategySignal.Tp1Pips);
        Assert.Equal(20m, valid.StrategySignal.Tp2Pips);
    }

    [Fact]
    public void Lot_Size_Respects_MinLot_MaxLot_And_LotStep()
    {
        var snapshot = CreateSnapshot(TradeDirection.Buy, entry: 1.10000m, stopLoss: 1.09900m);
        snapshot.AccountRisk.Balance = 10_000m;
        snapshot.AccountRisk.RiskPercent = 1m;
        snapshot.Market.MinLot = 0.01m;
        snapshot.Market.MaxLot = 0.27m;
        snapshot.Market.LotStep = 0.03m;

        _risk.Calculate(snapshot);
        _stops.Calculate(snapshot);
        _lots.Calculate(snapshot);

        Assert.Equal(0.25m, snapshot.AccountRisk.LotSize);
        Assert.True(_lots.IsVolumeValid(snapshot.AccountRisk, snapshot.Market));
    }

    [Fact]
    public void Calculates_RiskReward_Tp1_And_Tp2()
    {
        var snapshot = CreateSnapshot(TradeDirection.Buy, entry: 1.10000m, stopLoss: 1.09900m, tp1: 1.10100m, tp2: 1.10200m);
        snapshot.AccountRisk.Balance = 10_000m;
        snapshot.AccountRisk.RiskPercent = 1m;

        _targets.Calculate(snapshot);
        _stops.Calculate(snapshot);
        _risk.Calculate(snapshot);
        _lots.Calculate(snapshot);
        _rewardRisk.Calculate(snapshot);

        Assert.Equal(1m, snapshot.StrategySignal.RiskRewardTp1);
        Assert.Equal(2m, snapshot.StrategySignal.RiskRewardTp2);
        Assert.True(snapshot.StrategySignal.EstimatedTp1Profit > 0);
        Assert.True(snapshot.StrategySignal.EstimatedTp2Profit > snapshot.StrategySignal.EstimatedTp1Profit);
    }

    [Fact]
    public async Task Spread_Too_Wide_Creates_Block()
    {
        var snapshot = CreateSnapshot(TradeDirection.Buy);
        snapshot.Market.Ask = 1.10080m;
        CalculateAll(snapshot);
        var safety = CreateSafetyService();
        var engine = new DecisionEngine(_options);

        snapshot.ExecutionSafety = await safety.EvaluateAsync(snapshot);
        var result = engine.Evaluate(snapshot);

        Assert.Equal(ExecutionReadiness.Blocked, result.ExecutionReadiness);
        Assert.Contains(result.BlockReasons, reason => reason.Code == BlockReasonCode.SpreadTooWide);
    }

    [Fact]
    public async Task Invalid_Stops_Create_Block()
    {
        var snapshot = CreateSnapshot(TradeDirection.Buy, entry: 1.10000m, stopLoss: 1.10100m);
        CalculateAll(snapshot);
        var safety = CreateSafetyService();
        var engine = new DecisionEngine(_options);

        snapshot.ExecutionSafety = await safety.EvaluateAsync(snapshot);
        var result = engine.Evaluate(snapshot);

        Assert.Equal(ExecutionReadiness.Blocked, result.ExecutionReadiness);
        Assert.Contains(result.BlockReasons, reason => reason.Code == BlockReasonCode.InvalidStops);
    }

    private void CalculateAll(TradingDecisionSnapshot snapshot)
    {
        _targets.Calculate(snapshot);
        _stops.Calculate(snapshot);
        _spread.Calculate(snapshot);
        _risk.Calculate(snapshot);
        _lots.Calculate(snapshot);
        _rewardRisk.Calculate(snapshot);
        _spread.Calculate(snapshot);
    }

    private OrderSafetyService CreateSafetyService() => new(_options, _lots, _stops, _targets);

    private static TradingDecisionSnapshot CreateSnapshot(
        TradeDirection direction,
        decimal entry = 1.10000m,
        decimal stopLoss = 1.09900m,
        decimal tp1 = 1.10100m,
        decimal tp2 = 1.10200m)
    {
        return new TradingDecisionSnapshot
        {
            Pair = "EURUSD",
            TradeDirection = direction,
            Market = new MarketDataModel
            {
                Pair = "EURUSD",
                Bid = 1.10000m,
                Ask = 1.10012m,
                CurrentPrice = 1.10006m,
                Digits = 5,
                Point = 0.00001m,
                TickSize = 0.00001m,
                TickValue = 1m,
                SpreadPips = 1.2m,
                MinLot = 0.01m,
                MaxLot = 100m,
                LotStep = 0.01m,
                StopsLevelPoints = 10,
                FreezeLevelPoints = 0,
                MarketOpen = true,
                PriceFresh = true,
                LastTickAgeMs = 100
            },
            AccountRisk = new AccountRiskModel
            {
                Balance = 10_000m,
                Equity = 10_000m,
                FreeMargin = 9_000m,
                RiskPercent = 1m,
                DailyLossLimit = 300m,
                DailyProfitLoss = 0m,
                DailyLossRemaining = 300m,
                TradesTakenToday = 0,
                MaxTradesPerDay = 5,
                EstimatedMarginRequired = 200m
            },
            SessionNews = new SessionNewsModel
            {
                CurrentSession = "London",
                IsSessionAllowed = true,
                NewsBlackoutActive = false
            },
            StrategySignal = new StrategySignalModel
            {
                SetupDirection = direction == TradeDirection.Sell ? "SELL" : "BUY",
                EntryPrice = entry,
                StopLossPrice = stopLoss,
                Tp1Price = tp1,
                Tp2Price = tp2,
                TrendAligned = true,
                AlmaPass = true,
                RsiPass = true,
                AtrPass = true,
                BollingerPass = true,
                LiquiditySweepFound = true,
                MomentumCandleFound = true,
                ConfirmationCandleClosed = true
            },
            ExecutionSafety = new ExecutionSafetyModel
            {
                TerminalConnected = true,
                TradingAllowed = true,
                MarketOpen = true,
                PriceFresh = true,
                NewsFilterPass = true,
                DuplicateTradePass = true,
                OrderCheckPassed = true
            }
        };
    }
}
