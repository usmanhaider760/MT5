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
    public static string Telegram_Bot_Token     => _config["Telegram:BotToken"]          ?? string.Empty;
    public static string Telegram_Chat_Id       => _config["Telegram:ChatId"]            ?? string.Empty;

    public static decimal Risk_Forex_Per_Trade   => decimal.TryParse(_config["Risk:ForexRiskPct"],    out var rf) ? rf : 0.25m;
    public static decimal Risk_Max_Daily_Loss    => decimal.TryParse(_config["Risk:MaxDailyLossPct"], out var rd) ? rd : 0.75m;
    public static decimal Risk_Max_Drawdown      => decimal.TryParse(_config["Risk:MaxDrawdownPct"],  out var rw) ? rw : 5.00m;

    public static string Email_Smtp_Host        => _config["Email:SmtpHost"]             ?? string.Empty;
    public static int    Email_Smtp_Port        => int.TryParse(_config["Email:SmtpPort"], out var ep) ? ep : 587;
    public static string Email_Username         => _config["Email:Username"]             ?? string.Empty;
    public static string Email_Password         => _config["Email:Password"]             ?? string.Empty;
    public static string Email_To               => _config["Email:To"]                   ?? string.Empty;

    public static string News_Feed_Url          => _config["NewsFeed:Url"]               ?? string.Empty;

    public static decimal Spread_Limit_Multiplier => decimal.TryParse(
        _config["SpreadLimits:GlobalMultiplier"], out var sm) ? sm : 1.0m;
}
