namespace Atlas_Domain.BusinessObjects;

public class Candle_BO
{
    public string Symbol_Name { get; set; } = string.Empty;
    public string Timeframe { get; set; } = string.Empty;
    public DateTime Open_Time_UTC { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }
    public bool Is_Bullish => Close > Open;
    public bool Is_Bearish => Close < Open;
    public decimal Body_Size => Math.Abs(Close - Open);
    public decimal Total_Range => High - Low;
    public decimal Upper_Wick => Is_Bullish ? High - Close : High - Open;
    public decimal Lower_Wick => Is_Bullish ? Open - Low : Close - Low;
}
