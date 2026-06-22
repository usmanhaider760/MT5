using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;
using Atlas_Risk.Services;
using Xunit;

namespace Atlas_Tests;

public class Correlation_Guard_Tests
{
    private readonly Correlation_Guard _sut = new();

    private static Position_BO Pos(string symbol, Trade_Direction_Type dir) => new()
    {
        Symbol_Name = symbol,
        Direction   = dir,
        Status      = Order_Status_Type.Filled
    };

    [Fact]
    public void Get_Correlation_Score_Returns_10_When_No_Correlated_Positions()
    {
        int score = _sut.Get_Correlation_Score("EURUSD", []);
        Assert.Equal(10, score);
    }

    [Fact]
    public void Get_Correlation_Score_Returns_5_When_One_Correlated_Position()
    {
        var positions = new List<Position_BO> { Pos("GBPUSD", Trade_Direction_Type.Buy) };
        int score = _sut.Get_Correlation_Score("EURUSD", positions);
        Assert.Equal(5, score);
    }

    [Fact]
    public void Get_Correlation_Score_Returns_0_When_Two_Or_More_Correlated_Positions()
    {
        var positions = new List<Position_BO>
        {
            Pos("GBPUSD", Trade_Direction_Type.Buy),
            Pos("AUDUSD", Trade_Direction_Type.Sell)
        };
        int score = _sut.Get_Correlation_Score("EURUSD", positions);
        Assert.Equal(0, score);
    }

    [Fact]
    public void Is_Direction_Correlated_Returns_True_When_Same_Direction_On_Correlated_Pair()
    {
        var positions = new List<Position_BO> { Pos("GBPUSD", Trade_Direction_Type.Buy) };
        bool blocked = _sut.Is_Direction_Correlated("EURUSD", Trade_Direction_Type.Buy, positions);
        Assert.True(blocked);
    }

    [Fact]
    public void Is_Direction_Correlated_Returns_False_When_Opposite_Direction()
    {
        var positions = new List<Position_BO> { Pos("GBPUSD", Trade_Direction_Type.Sell) };
        bool blocked = _sut.Is_Direction_Correlated("EURUSD", Trade_Direction_Type.Buy, positions);
        Assert.False(blocked);
    }

    [Fact]
    public void Get_Correlation_Score_Returns_10_For_XAUUSD_With_Any_Positions()
    {
        var positions = new List<Position_BO>
        {
            Pos("EURUSD", Trade_Direction_Type.Buy),
            Pos("GBPUSD", Trade_Direction_Type.Buy)
        };
        int score = _sut.Get_Correlation_Score("XAUUSD", positions);
        Assert.Equal(10, score);
    }
}
