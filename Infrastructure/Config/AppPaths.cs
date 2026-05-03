namespace MT5TradingBot.Core
{
    internal static class AppPaths
    {
        public static string RootDirectory { get; } = FindRootDirectory();
        public static string DataDirectory => Path.Combine(RootDirectory, "Data");
        public static string ConfigDirectory => Path.Combine(DataDirectory, "Config");
        public static string DatabaseDirectory => Path.Combine(DataDirectory, "Database");
        public static string DeploymentDirectory => Path.Combine(DataDirectory, "Deployment");
        public static string LogDirectory => Path.Combine(DataDirectory, "Logs");

        public static string SettingsFile => Path.Combine(ConfigDirectory, "settings.json");
        public static string TradesDatabaseFile => Path.Combine(DatabaseDirectory, "trades.db");
        public static string EaDeployStatusFile => Path.Combine(DeploymentDirectory, "ea_deploy_status.json");

        public static string LegacySettingsFile => Path.Combine(LegacyAppDataDirectory, "settings.json");
        public static string LegacyTradesDatabaseFile => Path.Combine(LegacyAppDataDirectory, "trades.db");
        public static string LegacyEaDeployStatusFile => Path.Combine(LegacyAppDataDirectory, "ea_deploy_status.json");
        public static string PreviousProjectSettingsFile => Path.Combine(RootDirectory, "Config", "settings.json");
        public static string PreviousProjectTradesDatabaseFile => Path.Combine(DataDirectory, "trades.db");
        public static string PreviousProjectEaDeployStatusFile => Path.Combine(DataDirectory, "ea_deploy_status.json");

        private static string LegacyAppDataDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MT5TradingBot");

        public static string PrepareSettingsFile()
        {
            Directory.CreateDirectory(ConfigDirectory);
            CopyFirstExistingIfMissing(SettingsFile, PreviousProjectSettingsFile, LegacySettingsFile);
            return SettingsFile;
        }

        public static string PrepareTradesDatabaseFile()
        {
            Directory.CreateDirectory(DatabaseDirectory);
            CopyFirstExistingIfMissing(TradesDatabaseFile, PreviousProjectTradesDatabaseFile, LegacyTradesDatabaseFile);
            return TradesDatabaseFile;
        }

        public static string PrepareEaDeployStatusFile()
        {
            Directory.CreateDirectory(DeploymentDirectory);
            CopyFirstExistingIfMissing(EaDeployStatusFile, PreviousProjectEaDeployStatusFile, LegacyEaDeployStatusFile);
            return EaDeployStatusFile;
        }

        private static void CopyFirstExistingIfMissing(string targetPath, params string[] sourcePaths)
        {
            if (File.Exists(targetPath))
                return;

            foreach (string sourcePath in sourcePaths)
            {
                if (!File.Exists(sourcePath))
                    continue;

                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                File.Copy(sourcePath, targetPath);
                return;
            }
        }

        private static string FindRootDirectory()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "MT5TradingBot.csproj")))
                    return dir.FullName;

                dir = dir.Parent;
            }

            return AppContext.BaseDirectory;
        }
    }
}
