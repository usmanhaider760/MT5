using Microsoft.Extensions.Logging.Abstractions;
using MT5GoldScalper.V2.Core.Abstractions;
using MT5GoldScalper.V2.Core.Configuration;
using MT5GoldScalper.V2.Core.Engines;
using MT5GoldScalper.V2.Core.Models;
using MT5GoldScalper.V2.ViewModels;
using Xunit;

namespace MT5GoldScalper.V2.Tests;

public sealed class MainViewModelTests
{
    private readonly DecisionEngine _engine = new(new DecisionMakerOptions());

    [Fact]
    public void Exposes_SignalDecision_And_ExecutionReadiness_Displays()
    {
        var viewModel = CreateViewModel(_engine.Evaluate(CreateReadyBuySnapshot()));

        Assert.Equal("BUY", viewModel.SignalDecisionDisplay);
        Assert.Equal("READY", viewModel.ExecutionReadinessDisplay);
        Assert.Equal("YES", viewModel.CanPlaceTradeDisplay);
    }

    [Fact]
    public void Exposes_PrimaryBlockReason_Display()
    {
        var snapshot = CreateReadyBuySnapshot();
        snapshot.ExecutionSafety.SpreadAcceptable = false;
        var viewModel = CreateViewModel(_engine.Evaluate(snapshot));

        Assert.Equal("SpreadTooWide", viewModel.PrimaryBlockReasonCodeDisplay);
        Assert.Contains("Spread is wider", viewModel.PrimaryBlockReasonMessageDisplay);
        Assert.Contains("SpreadTooWide", viewModel.PrimaryBlockReasonDisplay);
    }

    [Fact]
    public void TradeCommand_Disables_Buy_When_Cannot_Place_Trade()
    {
        var snapshot = CreateReadyBuySnapshot();
        snapshot.ExecutionSafety.SpreadAcceptable = false;
        var viewModel = CreateViewModel(_engine.Evaluate(snapshot));

        Assert.False(viewModel.TradeCommand.CanExecute("BUY"));
        Assert.False(viewModel.TradeCommand.CanExecute("SELL"));
        Assert.True(viewModel.TradeCommand.CanExecute("SKIP"));
    }

    [Fact]
    public void TradeCommand_Enables_Only_Correct_Direction_When_Can_Place_Trade()
    {
        var viewModel = CreateViewModel(_engine.Evaluate(CreateReadyBuySnapshot()));

        Assert.True(viewModel.TradeCommand.CanExecute("BUY"));
        Assert.False(viewModel.TradeCommand.CanExecute("SELL"));
    }

    private static MainViewModel CreateViewModel(TradingDecisionSnapshot snapshot)
    {
        var viewModel = new MainViewModel(new StaticSnapshotService(snapshot), NullLogger<MainViewModel>.Instance)
        {
            Snapshot = snapshot
        };

        return viewModel;
    }

    private static TradingDecisionSnapshot CreateReadyBuySnapshot() =>
        new()
        {
            Pair = "XAUUSD",
            Market = new MarketDataModel
            {
                Pair = "XAUUSD",
                Bid = 3348.18m,
                Ask = 3348.42m,
                CurrentPrice = 3348.30m,
                SpreadPips = 2.4m,
                SpreadPoints = 24,
                LastTickAgeMs = 120,
                MinLot = 0.01m,
                MaxLot = 100m,
                MarketOpen = true,
                PriceFresh = true
            },
            AccountRisk = new AccountRiskModel
            {
                RiskAmount = 20m,
                LotSize = 0.10m,
                FreeMargin = 9000m,
                EstimatedMarginRequired = 240m,
                DailyLossRemaining = 250m,
                TradesTakenToday = 1,
                MaxTradesPerDay = 5
            },
            SessionNews = new SessionNewsModel
            {
                CurrentSession = "London",
                IsSessionAllowed = true,
                NewsBlackoutActive = false,
                NextHighImpactEvent = "None in blackout window",
                NewsImpact = "Low"
            },
            StrategySignal = new StrategySignalModel
            {
                SetupDirection = "BUY",
                HigherTimeframeTrend = "Bullish",
                M15Trend = "Bullish",
                M5Trend = "Bullish",
                TrendAligned = true,
                AlmaPass = true,
                RsiPass = true,
                AtrPass = true,
                BollingerPass = true,
                LiquiditySweepFound = true,
                MomentumCandleFound = true,
                ConfirmationCandleClosed = true,
                EntryPrice = 3348m,
                StopLossPrice = 3346m,
                Tp1Price = 3351m,
                Tp2Price = 3354m,
                StopLossPips = 20m,
                Tp1Pips = 30m,
                Tp2Pips = 60m,
                RiskRewardTp1 = 1.5m,
                RiskRewardTp2 = 3m,
                EstimatedTp2Profit = 60m
            },
            ExecutionSafety = new ExecutionSafetyModel
            {
                TerminalConnected = true,
                TradingAllowed = true,
                MarketOpen = true,
                PriceFresh = true,
                SpreadAcceptable = true,
                VolumeValid = true,
                StopsValid = true,
                MarginEnough = true,
                RiskLimitPass = true,
                NewsFilterPass = true,
                DuplicateTradePass = true,
                OrderCheckPassed = true,
                FinalReadyToTrade = true
            }
        };

    private sealed class StaticSnapshotService(TradingDecisionSnapshot snapshot) : ITradingDecisionSnapshotService
    {
        public Task<TradingDecisionSnapshot> CreateAsync(string symbol, CancellationToken ct = default) =>
            Task.FromResult(snapshot);
    }
}
