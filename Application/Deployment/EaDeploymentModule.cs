using System.Security.Cryptography;
using MT5TradingBot.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MT5TradingBot.Modules.Deployment
{
    public sealed class EaDeploymentModule : IModule
    {
        private const string EaFileName = "TradingBotEA";

        public string Name => "MT5 EA Deployment";
        public string Icon => "EA";
        public string Description => "Checks TradingBotEA files and copies the latest repo version into the MT5 Experts folder.";

        public async Task<ModuleStatus> CheckAsync(CancellationToken ct = default)
        {
            try
            {
                string sourceMq5 = Path.Combine(AppPaths.RootDirectory, "MT5_EA", $"{EaFileName}.mq5");
                string sourceEx5 = Path.Combine(AppPaths.RootDirectory, "MT5_EA", $"{EaFileName}.ex5");

                if (!File.Exists(sourceMq5))
                    return new ModuleStatus(false, $"EA source is missing: {sourceMq5}");

                if (!File.Exists(sourceEx5))
                    return new ModuleStatus(false, $"EA compiled file is missing: {sourceEx5}. Run scripts\\Deploy-MT5EA.ps1 or compile in MetaEditor.");

                string terminalDataPath = ResolveTerminalDataPath();
                if (string.IsNullOrWhiteSpace(terminalDataPath))
                    return new ModuleStatus(false, "No MT5 terminal data folder found. Open MT5 once, then restart the bot.");

                string expertsPath = Path.Combine(terminalDataPath, "MQL5", "Experts");
                Directory.CreateDirectory(expertsPath);

                string destMq5 = Path.Combine(expertsPath, $"{EaFileName}.mq5");
                string destEx5 = Path.Combine(expertsPath, $"{EaFileName}.ex5");

                bool copiedMq5 = await CopyIfDifferentAsync(sourceMq5, destMq5, ct).ConfigureAwait(false);
                bool copiedEx5 = await CopyIfDifferentAsync(sourceEx5, destEx5, ct).ConfigureAwait(false);
                bool copiedAny = copiedMq5 || copiedEx5;

                WriteDeploymentStatus(sourceMq5, sourceEx5, terminalDataPath, destMq5, destEx5, copiedAny);

                return copiedAny
                    ? new ModuleStatus(true, $"EA updated in MT5 Experts folder. Reload TradingBotEA in MT5: {destEx5}")
                    : new ModuleStatus(true, $"EA already up to date: {destEx5}");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new ModuleStatus(false, $"EA deployment check failed: {ex.Message}");
            }
        }

        private static string ResolveTerminalDataPath()
        {
            string fromStatus = ReadStatusTerminalDataPath();
            if (IsTerminalDataPath(fromStatus))
                return fromStatus;

            string basePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MetaQuotes",
                "Terminal");

            if (!Directory.Exists(basePath))
                return "";

            return Directory.EnumerateDirectories(basePath)
                .Where(IsTerminalDataPath)
                .Select(path => new DirectoryInfo(path))
                .OrderByDescending(dir => dir.LastWriteTimeUtc)
                .Select(dir => dir.FullName)
                .FirstOrDefault() ?? "";
        }

        private static string ReadStatusTerminalDataPath()
        {
            try
            {
                string statusPath = AppPaths.PrepareEaDeployStatusFile();
                if (!File.Exists(statusPath))
                    return "";

                var status = JObject.Parse(File.ReadAllText(statusPath));
                return status.Value<string>("terminal_data_path") ?? "";
            }
            catch
            {
                return "";
            }
        }

        private static bool IsTerminalDataPath(string? path) =>
            !string.IsNullOrWhiteSpace(path)
            && Directory.Exists(path)
            && Directory.Exists(Path.Combine(path, "MQL5", "Experts"));

        private static async Task<bool> CopyIfDifferentAsync(string sourcePath, string destinationPath, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            bool different = !File.Exists(destinationPath)
                || !string.Equals(await Sha256Async(sourcePath, ct).ConfigureAwait(false), await Sha256Async(destinationPath, ct).ConfigureAwait(false), StringComparison.OrdinalIgnoreCase);

            if (!different)
                return false;

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(sourcePath, destinationPath, overwrite: true);
            return true;
        }

        private static async Task<string> Sha256Async(string path, CancellationToken ct)
        {
            await using var stream = File.OpenRead(path);
            byte[] hash = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
            return Convert.ToHexString(hash);
        }

        private static void WriteDeploymentStatus(
            string sourceMq5,
            string sourceEx5,
            string terminalDataPath,
            string destMq5,
            string destEx5,
            bool copiedAny)
        {
            string statusPath = AppPaths.PrepareEaDeployStatusFile();
            var status = new
            {
                deployed_at = DateTime.UtcNow.ToString("O"),
                source = sourceMq5,
                source_ex5 = sourceEx5,
                terminal_data_path = terminalDataPath,
                mq5_path = destMq5,
                ex5_path = destEx5,
                compile_result = "Repository EX5 copied by startup EA deployment check.",
                needs_mt5_reload = copiedAny,
                message = copiedAny
                    ? "EA files were updated. Remove and re-attach TradingBotEA on the MT5 chart, or restart MT5."
                    : "EA files already matched the repository version."
            };

            File.WriteAllText(statusPath, JsonConvert.SerializeObject(status, Formatting.Indented));
        }
    }
}
