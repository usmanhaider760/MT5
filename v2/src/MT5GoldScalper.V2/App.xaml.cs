using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MT5GoldScalper.V2.Core.Abstractions;
using MT5GoldScalper.V2.Core.Configuration;
using MT5GoldScalper.V2.Core.Engines;
using MT5GoldScalper.V2.Core.Services;
using MT5GoldScalper.V2.Core.Services.Calculations;
using MT5GoldScalper.V2.ViewModels;
using TradingDecisionSystem;

namespace MT5GoldScalper.V2;

public partial class App : Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(config =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {
                var options = context.Configuration.GetSection("DecisionMaker").Get<DecisionMakerOptions>() ?? new DecisionMakerOptions();
                services.AddSingleton(options);
                services.AddLogging(builder => builder.AddDebug());

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

                services.AddSingleton<MainViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

        _host.Start();
        _host.Services.GetRequiredService<MainWindow>().Show();
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            _host.StopAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult();
            _host.Dispose();
        }

        base.OnExit(e);
    }
}
