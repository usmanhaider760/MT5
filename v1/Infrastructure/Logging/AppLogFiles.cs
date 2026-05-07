using Serilog;

namespace MT5TradingBot.Core
{
    internal static class AppLogFiles
    {
        private static readonly object Sync = new();
        private static Serilog.Core.Logger? _tradeLogger;

        public static string LogDirectory { get; } = AppPaths.LogDirectory;

        public static string CurrentLogFile { get; private set; } = "";
        public static string CurrentTradeLogFile { get; private set; } = "";

        public static void ConfigureNewSession()
        {
            lock (Sync)
            {
                Directory.CreateDirectory(LogDirectory);
                string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                CurrentLogFile = Path.Combine(LogDirectory, $"bot-{stamp}.log");
                CurrentTradeLogFile = Path.Combine(LogDirectory, $"trade-{stamp}.log");
                Configure(CurrentLogFile, CurrentTradeLogFile);
            }
        }

        public static void RecreateCurrentFile()
        {
            lock (Sync)
            {
                if (string.IsNullOrWhiteSpace(CurrentLogFile))
                {
                    ConfigureNewSession();
                    return;
                }

                if (string.IsNullOrWhiteSpace(CurrentTradeLogFile))
                {
                    string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                    CurrentTradeLogFile = Path.Combine(LogDirectory, $"trade-{stamp}.log");
                }

                Configure(CurrentLogFile, CurrentTradeLogFile);
            }
        }

        public static void WriteTrade(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            lock (Sync)
            {
                if (_tradeLogger == null)
                {
                    if (string.IsNullOrWhiteSpace(CurrentTradeLogFile))
                    {
                        Directory.CreateDirectory(LogDirectory);
                        CurrentTradeLogFile = Path.Combine(LogDirectory, $"trade-{DateTime.Now:yyyyMMdd-HHmmss}.log");
                    }

                    ConfigureTradeLogger(CurrentTradeLogFile);
                }

                _tradeLogger?.Information("{Message}", message);
            }
        }

        public static void Close()
        {
            lock (Sync)
            {
                Log.CloseAndFlush();
                _tradeLogger?.Dispose();
                _tradeLogger = null;
            }
        }

        private static void Configure(string logPath, string tradeLogPath)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(
                    logPath,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            ConfigureTradeLogger(tradeLogPath);
        }

        private static void ConfigureTradeLogger(string path)
        {
            _tradeLogger?.Dispose();
            _tradeLogger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(
                    path,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();
        }
    }
}
