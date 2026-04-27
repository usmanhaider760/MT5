using Microsoft.Extensions.DependencyInjection;
using MT5TradingBot.Services;
using MT5TradingBot.UI;
using Serilog;

namespace MT5TradingBot
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // ── Logger setup ──────────────────────────────────────
            string logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MT5TradingBot", "logs");
            Directory.CreateDirectory(logDir);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(
                    Path.Combine(logDir, "bot-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            Log.Information("=== MT5TradingBot starting ===");

            // ── Global exception handlers ─────────────────────────
            Application.ThreadException += (_, e) =>
            {
                Log.Error(e.Exception, "Unhandled UI thread exception");
                MessageBox.Show(
                    $"An error occurred:\n\n{e.Exception.Message}\n\nDetails saved to log.",
                    "MT5 Bot — Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                    Log.Fatal(ex, "Fatal unhandled exception");
                Log.CloseAndFlush();
            };

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // ── DI container ─────────────────────────────────────
            var services = new ServiceCollection();
            services.AddSingleton<SettingsManager>();
            var provider = services.BuildServiceProvider();

            // ── Splash / startup checks ───────────────────────────
            using var splash = new SplashScreen();
            splash.ShowDialog();
            if (!splash.ShouldProceed) return;

            // ── Run ───────────────────────────────────────────────
            Application.Run(new MainForm());

            Log.Information("=== MT5TradingBot shutdown ===");
            Log.CloseAndFlush();
        }
    }
}
