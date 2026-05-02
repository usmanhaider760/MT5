using Serilog;

namespace MT5TradingBot.Core
{
    internal static class AppLogFiles
    {
        public static string LogDirectory { get; } = Path.Combine(AppContext.BaseDirectory, "logs");

        public static string CurrentLogFile { get; private set; } = "";

        public static void ConfigureNewSession()
        {
            Directory.CreateDirectory(LogDirectory);
            CurrentLogFile = Path.Combine(LogDirectory, $"bot-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            Configure(CurrentLogFile);
        }

        public static void RecreateCurrentFile()
        {
            if (string.IsNullOrWhiteSpace(CurrentLogFile))
                ConfigureNewSession();
            else
                Configure(CurrentLogFile);
        }

        public static void Close() => Log.CloseAndFlush();

        private static void Configure(string path)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(
                    path,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();
        }
    }
}
