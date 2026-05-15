using Microsoft.Extensions.DependencyInjection;
using MT5GoldScalper.V2.Core.Abstractions;
using MT5GoldScalper.V2.Core.Configuration;
using MT5GoldScalper.V2.Core.Engines;
using MT5GoldScalper.V2.Core.Models;
using MT5GoldScalper.V2.Core.Services;
using MT5GoldScalper.V2.Core.Services.Calculations;
using Xunit;

namespace MT5GoldScalper.V2.Tests;

public sealed class PrototypeIntegrationTests
{
    [Fact]
    public async Task Builds_Buy_Snapshot_For_XauUsd()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new DecisionMakerOptions());
        services.AddSingleton<DummyDataService>();
        services.AddSingleton<IMarketDataService>(sp => sp.GetRequiredService<DummyDataService>());
        services.AddSingleton<IAccountDataService>(sp => sp.GetRequiredService<DummyDataService>());
        services.AddSingleton<INewsService>(sp => sp.GetRequiredService<DummyDataService>());
        services.AddSingleton<IIndicatorService>(sp => sp.GetRequiredService<DummyDataService>());
        services.AddSingleton<IPipCalculationService, PipCalculationService>();
        services.AddSingleton<ISpreadCalculationService, SpreadCalculationService>();
        services.AddSingleton<IRiskCalculationService, RiskCalculationService>();
        services.AddSingleton<ILotSizeCalculationService, LotSizeCalculationService>();
        services.AddSingleton<ITargetCalculationService, TargetCalculationService>();
        services.AddSingleton<IStopValidationService, StopValidationService>();
        services.AddSingleton<IRewardRiskCalculationService, RewardRiskCalculationService>();
        services.AddSingleton<IOrderSafetyService, OrderSafetyService>();
        services.AddSingleton<IDecisionEngine, DecisionEngine>();
        services.AddSingleton<ITradingDecisionSnapshotService, TradingDecisionSnapshotService>();

        var provider = services.BuildServiceProvider();
        var snapshotService = provider.GetRequiredService<ITradingDecisionSnapshotService>();

        var snapshot = await snapshotService.CreateAsync("XAUUSD");

        Assert.Equal("XAUUSD", snapshot.Pair);
        Assert.Equal(SignalDecision.Buy, snapshot.SignalDecision);
        Assert.Equal(ExecutionReadiness.Ready, snapshot.ExecutionReadiness);
        Assert.Equal("BUY", snapshot.FinalDecisionText);
        Assert.True(snapshot.CanPlaceTrade);
        Assert.NotEmpty(snapshot.Sections);
        Assert.True(snapshot.ExecutionSafety.FinalReadyToTrade);
    }
}
