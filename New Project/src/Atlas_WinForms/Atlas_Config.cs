using Microsoft.Extensions.Configuration;

namespace Atlas_WinForms;

public static class Atlas_Config
{
    private static readonly IConfigurationRoot _config;

    static Atlas_Config()
    {
        _config = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .Build();
    }

    public static string MT5_Host               => _config["MT5:Host"]                  ?? "127.0.0.1";
    public static int    MT5_Port               => int.TryParse(_config["MT5:Port"],    out var p) ? p : 9090;
    public static bool   Demo_Mode              => !bool.TryParse(_config["Bot:DemoMode"], out var d) || d;
    public static int    Cycle_Interval_Seconds => int.TryParse(_config["Bot:CycleIntervalSeconds"], out var c) ? c : 60;
    public static string Db_Path                => _config["Database:Path"]              ?? "atlas_trading.db";
}
