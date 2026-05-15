namespace MT5GoldScalper.V2.Models;

public sealed class TradeLogItem
{
    public string Time { get; set; } = string.Empty;
    public string Pair { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public string Entry { get; set; } = string.Empty;
    public string StopLoss { get; set; } = string.Empty;
    public string TakeProfit { get; set; } = string.Empty;
    public string RiskReward { get; set; } = string.Empty;
    public string Lot { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string Pips { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}
