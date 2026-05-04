using Microsoft.Extensions.DependencyInjection;
using MT5TradingBot.Core;
using MT5TradingBot.Data;
using MT5TradingBot.Models;
using MT5TradingBot.Modules.BrokerIntegration;
using MT5TradingBot.Modules.LiveReadiness;
using MT5TradingBot.Modules.MarketData;
using MT5TradingBot.Modules.NewsFilter;
using MT5TradingBot.Modules.RiskManagement;
using MT5TradingBot.Services;
using MT5TradingBot.UI;
using Serilog;

namespace MT5TradingBot
{
    internal static class Program
    {
        [STAThread]
        static async Task Main(string[] args)
        {
            if (args.Any(a => string.Equals(a, "--update-market-data", StringComparison.OrdinalIgnoreCase)))
            {
                NativeConsole.TryAttachParent();
                var settings = await LoadSettingsAsync().ConfigureAwait(false);
                using var bridge = new MT5Bridge(settings.Mt5);
                var command = new HistoricalMarketDataCommand(
                    () => new HistoricalMarketDataUpdater(new Mt5HistoricalMarketDataProvider(bridge)),
                    Console.Out);

                Environment.ExitCode = await command.RunUpdateAsync(settings, args).ConfigureAwait(false);
                return;
            }

            if (args.Any(a => string.Equals(a, "--diagnose-market-data-sync", StringComparison.OrdinalIgnoreCase)))
            {
                NativeConsole.TryAttachParent();
                var settings = await LoadSettingsAsync().ConfigureAwait(false);
                using var bridge = new MT5Bridge(settings.Mt5);
                var command = new HistoricalMarketDataCommand(
                    () => new HistoricalMarketDataUpdater(new Mt5HistoricalMarketDataProvider(bridge)),
                    Console.Out);

                Environment.ExitCode = await command
                    .RunDiagnoseAsync(settings, args, bridge.PingAsync)
                    .ConfigureAwait(false);
                return;
            }

            if (args.Any(a => string.Equals(a, "--generate-evidence-package", StringComparison.OrdinalIgnoreCase)))
            {
                var settings = await LoadSettingsAsync().ConfigureAwait(false);
                if (settings.Bot.EnableMarketDataAutoUpdate &&
                    (settings.Bot.UpdateMarketDataOnStartup || settings.Bot.UpdateOnStartup))
                {
                    using var bridge = new MT5Bridge(settings.Mt5);
                    var command = new HistoricalMarketDataCommand(
                        () => new HistoricalMarketDataUpdater(new Mt5HistoricalMarketDataProvider(bridge)),
                        Console.Out);
                    await command.RunUpdateAsync(settings, args).ConfigureAwait(false);
                }

                var result = await new EvidencePackageCommand()
                    .RunAsync(new EvidencePackageCommandRequest
                    {
                        OutputDirectory = Directory.GetCurrentDirectory(),
                        TickCsvPath = ArgValue(args, "--tick-csv"),
                        OhlcCsvPath = ArgValue(args, "--ohlc-csv"),
                        UseSampleFixture = HasArg(args, "--use-sample-fixture")
                    })
                    .ConfigureAwait(false);

                Console.WriteLine("Evidence package generated.");
                Console.WriteLine($"Output directory: {result.OutputDirectory}");
                Console.WriteLine($"Market data: {result.MarketDataSource}");
                Console.WriteLine($"Ticks loaded: {result.TicksLoaded}");
                Console.WriteLine($"Candles loaded: {result.CandlesLoaded}");
                Console.WriteLine($"Candidates generated: {result.CandidatesGenerated}");
                Console.WriteLine($"Candidate diagnostic: {result.CandidateGenerationDiagnostic}");
                Console.WriteLine($"Strategy evidence classification: {result.StrategyEvidenceClassification}");
                Console.WriteLine($"Go/no-go decision: {result.GoNoGoDecision}");
                return;
            }

            // ── Logger setup ──────────────────────────────────────
            AppLogFiles.ConfigureNewSession();

            Log.Information("=== MT5TradingBot starting ===");

            // ── Global exception handlers ─────────────────────────
            Application.ThreadException += (_, e) =>
            {
                Log.Error(e.Exception, "Unhandled UI thread exception");
                AppMessageBox.Error(
                    null,
                    $"An error occurred:\n\n{e.Exception.Message}\n\nDetails saved to log.",
                    "MT5 Bot — Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                    Log.Fatal(ex, "Fatal unhandled exception");
                AppLogFiles.Close();
            };

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // ── DI container ─────────────────────────────────────
            var services = new ServiceCollection();

            // Singletons that have no runtime-config dependency
            services.AddSingleton<SettingsManager>();
            services.AddSingleton<INewsCalendarService, FmpNewsCalendarService>();
            services.AddSingleton<IRiskManager, RiskManager>();
            services.AddSingleton<IAiContextManager, AiContextManager>();
            services.AddSingleton<ITradeRepository>(_ =>
            {
                return new SqliteTradeRepository(AppPaths.PrepareTradesDatabaseFile());
            });

            // MainForm itself - resolved so its constructor receives IServiceProvider
            services.AddSingleton<MainForm>();

            using ServiceProvider provider = services.BuildServiceProvider();

            // ── Splash / startup checks ───────────────────────────
            using var splash = new SplashScreen();
            splash.ShowDialog();
            if (!splash.ShouldProceed) return;

            // ── Run ───────────────────────────────────────────────
            Application.Run(provider.GetRequiredService<MainForm>());

            Log.Information("=== MT5TradingBot shutdown ===");
            AppLogFiles.Close();
        }

        private static string? ArgValue(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            }

            return null;
        }

        private static bool HasArg(string[] args, string name) =>
            args.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));

        private static async Task<AppSettings> LoadSettingsAsync()
        {
            var manager = new SettingsManager();
            await manager.LoadAsync().ConfigureAwait(false);
            return manager.Current;
        }

        private static async Task<HistoricalMarketDataUpdateSummary> RunMarketDataUpdateAsync(
            AppSettings settings,
            HistoricalMarketDataCliOptions? cliOptions,
            bool writeConsole,
            CancellationToken cancellationToken = default)
        {
            using var bridge = new MT5Bridge(settings.Mt5);
            var updater = new HistoricalMarketDataUpdater(new Mt5HistoricalMarketDataProvider(bridge));
            var request = HistoricalMarketDataUpdater.FromConfig(settings.Bot, cliOptions);
            var summary = await updater.UpdateAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (writeConsole)
                WriteMarketDataUpdateSummary(summary);

            LogMarketDataUpdateSummary(summary);
            return summary;
        }

        private static void WriteMarketDataUpdateSummary(HistoricalMarketDataUpdateSummary summary)
        {
            foreach (string line in MarketDataUpdateConsoleFormatter.Format(summary))
                Console.WriteLine(line);
        }

        private static void LogMarketDataUpdateSummary(HistoricalMarketDataUpdateSummary summary)
        {
            foreach (var result in summary.SymbolResults)
            {
                Log.Information(
                    "Market data update {Symbol}: type={Type} before={Before} fetched={Fetched} after={After} removed={Removed} fallback={Fallback} output={Output}",
                    result.Symbol,
                    result.DataTypeUsed,
                    result.RowsBefore,
                    result.RowsFetched,
                    result.RowsAfter,
                    result.RowsRemovedByRetention,
                    result.FallbackUsed,
                    result.OutputFilePath);
            }

            foreach (string warning in summary.Warnings)
                Log.Warning("Market data update warning: {Warning}", warning);
            foreach (string error in summary.Errors)
                Log.Error("Market data update error: {Error}", error);
        }
    }
}
