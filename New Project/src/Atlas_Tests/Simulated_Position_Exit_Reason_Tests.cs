using Atlas_Application.Backtest;
using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;
using Xunit;

namespace Atlas_Tests;

public class Simulated_Position_Exit_Reason_Tests
{
    private static Trade_Signal_BO Buy_Signal() => new()
    {
        Symbol_Name = "EURUSD",
        Direction = Trade_Direction_Type.Buy,
        Entry_Price = 1.1000m,
        Stop_Loss_Price = 1.0980m,
        Take_Profit_Price = 1.1040m
    };

    private static Candle_BO Bar(decimal open, decimal high, decimal low, decimal close) => new()
    {
        Open_Time_UTC = DateTime.UtcNow, Open = open, High = high, Low = low, Close = close, Volume = 100
    };

    [Fact]
    public void Stop_Loss_Hit_Sets_Exit_Reason_Stop_Loss_Hit()
    {
        var signal = Buy_Signal();
        var entry_bar = Bar(1.1000m, 1.1005m, 1.0995m, 1.1000m);
        var pos = new Simulated_Position(signal, 0.1m, entry_bar, new Backtest_Config());

        var sl_bar = Bar(1.0990m, 1.0995m, 1.0975m, 1.0980m); // Low breaches the SL
        var result = pos.Check_And_Close(sl_bar, new Backtest_Config());

        Assert.NotNull(result);
        Assert.Equal(Exit_Reason_Type.Stop_Loss_Hit, result!.Exit_Reason);
    }

    [Fact]
    public void Take_Profit_Hit_Sets_Exit_Reason_Take_Profit_Hit()
    {
        var signal = Buy_Signal();
        var entry_bar = Bar(1.1000m, 1.1005m, 1.0995m, 1.1000m);
        var pos = new Simulated_Position(signal, 0.1m, entry_bar, new Backtest_Config());

        var tp_bar = Bar(1.1030m, 1.1045m, 1.1025m, 1.1040m); // High breaches the TP
        var result = pos.Check_And_Close(tp_bar, new Backtest_Config());

        Assert.NotNull(result);
        Assert.Equal(Exit_Reason_Type.Take_Profit_Hit, result!.Exit_Reason);
    }

    [Fact]
    public void Force_Close_Sets_Exit_Reason_Manual_Close()
    {
        var signal = Buy_Signal();
        var entry_bar = Bar(1.1000m, 1.1005m, 1.0995m, 1.1000m);
        var pos = new Simulated_Position(signal, 0.1m, entry_bar, new Backtest_Config());

        var result = pos.Force_Close(1.1010m, "Backtest end — position force-closed", new Backtest_Config());

        Assert.Equal(Exit_Reason_Type.Manual_Close, result.Exit_Reason);
    }
}
