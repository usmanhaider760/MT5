namespace Atlas_Shared;

public static class Config_Keys
{
    public const string MT5_Host = "MT5:Host";
    public const string MT5_Port = "MT5:Port";
    public const string MT5_Login = "MT5:Login";
    public const string MT5_Password = "MT5:Password";
    public const string MT5_Server = "MT5:Server";

    public const string Risk_Mode = "Risk:Mode";
    public const string Risk_Forex_Per_Trade = "Risk:ForexPerTrade";
    public const string Risk_Gold_Per_Trade = "Risk:GoldPerTrade";
    public const string Risk_Max_Daily_Loss = "Risk:MaxDailyLoss";
    public const string Risk_Max_Weekly_Loss = "Risk:MaxWeeklyLoss";
    public const string Risk_Drawdown_Breaker = "Risk:DrawdownBreaker";

    public const string Quality_Min_Score_Live = "Quality:MinScoreLive";
    public const string Quality_Min_Score_Gold = "Quality:MinScoreGold";

    public const string News_Api_Key = "News:ApiKey";
    public const string News_Api_Url = "News:ApiUrl";

    public const string DB_Connection_String = "Database:ConnectionString";
}
