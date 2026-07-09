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
    public static string Telegram_Bot_Token     =>
        Environment.GetEnvironmentVariable("ATLAS_TELEGRAM_TOKEN")
        ?? _config["Telegram:BotToken"] ?? string.Empty;
    public static string Telegram_Chat_Id       => _config["Telegram:ChatId"]            ?? string.Empty;

    public static decimal Risk_Forex_Per_Trade   => decimal.TryParse(_config["Risk:ForexRiskPct"],    out var rf) ? rf : 0.25m;
    public static decimal Risk_Gold_Per_Trade    => decimal.TryParse(_config["Risk:GoldRiskPct"],     out var rg) ? rg : 0.15m;
    public static decimal Risk_Max_Daily_Loss    => decimal.TryParse(_config["Risk:MaxDailyLossPct"], out var rd) ? rd : 0.75m;
    public static decimal Risk_Max_Weekly_Loss   => decimal.TryParse(_config["Risk:MaxWeeklyLossPct"], out var rwl) ? rwl : 2.00m;
    public static decimal Risk_Max_Drawdown      => decimal.TryParse(_config["Risk:MaxDrawdownPct"],  out var rw) ? rw : 5.00m;
    public static decimal Risk_Full_Stop_Drawdown    => decimal.TryParse(_config["Risk:FullStopDrawdownPct"],    out var rfs) ? rfs : 8.00m;
    public static decimal Risk_Caution_Drawdown      => decimal.TryParse(_config["Risk:CautionDrawdownPct"],     out var rc)  ? rc  : 2.00m;
    public static decimal Risk_Recovery_Drawdown     => decimal.TryParse(_config["Risk:RecoveryDrawdownPct"],    out var rr)  ? rr  : 4.00m;
    public static decimal Risk_Protection_Drawdown   => decimal.TryParse(_config["Risk:ProtectionDrawdownPct"],  out var rp)  ? rp  : 6.00m;
    public static int     Risk_Max_Open_Trades       => int.TryParse(_config["Risk:MaxOpenTrades"],              out var rmo) ? rmo : 2;
    public static int     Risk_Max_Gold_Trades       => int.TryParse(_config["Risk:MaxGoldTrades"],              out var rmg) ? rmg : 1;
    public static int     Risk_Max_Consecutive_Losses => int.TryParse(_config["Risk:MaxConsecutiveLosses"],      out var rmc) ? rmc : 2;
    public static int     Risk_Min_Quality_Score_Live      => int.TryParse(_config["Risk:MinQualityScoreLive"],      out var rql) ? rql : 85;
    public static int     Risk_Min_Quality_Score_Gold_Live => int.TryParse(_config["Risk:MinQualityScoreGold"],      out var rqg) ? rqg : 85;
    public static decimal Risk_Min_RR_Forex          => decimal.TryParse(_config["Risk:MinRRForex"],      out var rrf) ? rrf : 1.8m;
    public static decimal Risk_Min_RR_Gold_Swing     => decimal.TryParse(_config["Risk:MinRRGoldSwing"],  out var rrg) ? rrg : 2.0m;
    public static decimal Risk_Min_RR_Intraday       => decimal.TryParse(_config["Risk:MinRRIntraday"],   out var rri) ? rri : 1.5m;
    public static decimal Risk_Max_Lot_Forex         => decimal.TryParse(_config["Risk:MaxLotForex"],     out var mlf) ? mlf : 5.0m;
    public static decimal Risk_Max_Lot_Gold          => decimal.TryParse(_config["Risk:MaxLotGold"],      out var mlg) ? mlg : 1.0m;

    public static string Email_Smtp_Host        => _config["Email:SmtpHost"]             ?? string.Empty;
    public static int    Email_Smtp_Port        => int.TryParse(_config["Email:SmtpPort"], out var ep) ? ep : 587;
    public static string Email_Username         => _config["Email:Username"]             ?? string.Empty;
    public static string Email_Password         =>
        Environment.GetEnvironmentVariable("ATLAS_EMAIL_PASSWORD")
        ?? _config["Email:Password"] ?? string.Empty;
    public static string Email_To               => _config["Email:To"]                   ?? string.Empty;

    public static string News_Feed_Url          => _config["NewsFeed:Url"]               ?? string.Empty;

    public static decimal Spread_Limit_Multiplier => decimal.TryParse(
        _config["SpreadLimits:GlobalMultiplier"], out var sm) ? sm : 1.0m;
}
