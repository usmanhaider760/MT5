using MT5GoldScalper.V2.Core.Configuration;
using MT5GoldScalper.V2.Core.Engines;
using MT5GoldScalper.V2.Core.Models;
using Xunit;

namespace MT5GoldScalper.V2.Tests;

public sealed class DecisionEngineTests
{
    private readonly DecisionEngine _engine = new(new DecisionMakerOptions());

    [Fact]
    public void Buy_Signal_With_Spread_Block_Is_Blocked_And_Cannot_Place_Trade()
    {
        var snapshot = CreateReadyBuySnapshot();
        snapshot.ExecutionSafety.SpreadAcceptable = false;

        var result = _engine.Evaluate(snapshot);

        Assert.Equal(SignalDecision.Buy, result.SignalDecision);
        Assert.Equal(TradeDirection.Buy, result.TradeDirection);
        Assert.Equal(ExecutionReadiness.Blocked, result.ExecutionReadiness);
        Assert.Equal("BLOCKED", result.FinalDecisionText);
        Assert.False(result.CanPlaceTrade);
        Assert.Contains(result.BlockReasons, r => r.Code == BlockReasonCode.SpreadTooWide && r.IsHardBlock);
    }

    [Fact]
    public void Buy_Signal_With_All_Safety_Pass_Is_Ready_And_Can_Place_Trade()
    {
        var result = _engine.Evaluate(CreateReadyBuySnapshot());

        Assert.Equal(SignalDecision.Buy, result.SignalDecision);
        Assert.Equal(TradeDirection.Buy, result.TradeDirection);
        Assert.Equal(ExecutionReadiness.Ready, result.ExecutionReadiness);
        Assert.Equal("BUY", result.FinalDecisionText);
        Assert.True(result.CanPlaceTrade);
        Assert.Empty(result.BlockReasons);
    }

    [Fact]
    public void News_Blackout_Is_Blocked()
    {
        var snapshot = CreateReadyBuySnapshot();
        snapshot.SessionNews.NewsBlackoutActive = true;
        snapshot.ExecutionSafety.NewsFilterPass = false;

        var result = _engine.Evaluate(snapshot);

        Assert.Equal(ExecutionReadiness.Blocked, result.ExecutionReadiness);
        Assert.False(result.CanPlaceTrade);
        Assert.Contains(result.BlockReasons, r => r.Code == BlockReasonCode.NewsBlackout);
        Assert.Equal(BlockReasonCode.NewsBlackout, result.PrimaryBlockReason?.Code);
    }

    [Theory]
    [InlineData(true, SignalDecision.Wait)]
    [InlineData(false, SignalDecision.Skip)]
    public void Low_Confidence_Returns_Wait_Or_Skip(bool watchLevel, SignalDecision expectedDecision)
    {
        var snapshot = CreateReadyBuySnapshot();
        snapshot.StrategySignal.TrendAligned = false;
        snapshot.StrategySignal.RsiPass = false;
        snapshot.StrategySignal.BollingerPass = false;
        snapshot.StrategySignal.MomentumCandleFound = false;
        snapshot.StrategySignal.ConfirmationCandleClosed = false;

        if (watchLevel)
        {
            snapshot.StrategySignal.TrendAligned = true;
        }
        else
        {
            snapshot.StrategySignal.LiquiditySweepFound = false;
        }

        var result = _engine.Evaluate(snapshot);

        Assert.Equal(expectedDecision, result.SignalDecision);
        Assert.Equal(ExecutionReadiness.Review, result.ExecutionReadiness);
        Assert.False(result.CanPlaceTrade);
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
                NewsBlackoutActive = false
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
                RiskRewardTp2 = 3m
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
}
