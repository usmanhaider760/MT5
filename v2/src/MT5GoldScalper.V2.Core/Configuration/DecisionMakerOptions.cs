namespace MT5GoldScalper.V2.Core.Configuration;

public sealed class DecisionMakerOptions
{
    public decimal MaxSpreadPips { get; set; } = 3.0m;
    public int MaxTickAgeMs { get; set; } = 2500;
    public decimal MinConfidenceToTrade { get; set; } = 75m;
    public decimal MinConfidenceToWatch { get; set; } = 55m;
    public decimal MaxDailyLoss { get; set; } = 300m;
    public int MaxTradesPerDay { get; set; } = 5;
    public string[] AllowedSessions { get; set; } = ["London", "NewYork", "Overlap"];
}
