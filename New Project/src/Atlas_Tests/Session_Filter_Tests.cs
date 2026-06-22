using Atlas_Domain.Enums;
using Atlas_Market_Data.Services;
using Xunit;

namespace Atlas_Tests;

public class Session_Filter_Tests
{
    private readonly Session_Filter_Service _sut = new();

    [Theory]
    [InlineData(10, 0,  Session_Type.London)]             // 10:00 UTC — London only (07:00-16:00)
    [InlineData(14, 0,  Session_Type.London_NY_Overlap)]  // 14:00 UTC — LDN+NY overlap (12:00-16:00)
    [InlineData(20, 0,  Session_Type.New_York)]           // 20:00 UTC — NY only (12:00-21:00)
    [InlineData( 3, 0,  Session_Type.Asian)]              // 03:00 UTC — Asian (<08:00)
    [InlineData(22, 0,  Session_Type.Off_Hours)]          // 22:00 UTC — off hours (21:00-23:00)
    public void Get_Current_Session_Returns_Expected(int hour, int minute, Session_Type expected)
    {
        var utc = new DateTime(2026, 1, 6, hour, minute, 0, DateTimeKind.Utc); // Tuesday
        var result = _sut.Get_Current_Session(utc);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Off_Hours_Is_Not_Trading_Active()
    {
        Assert.False(_sut.Is_Trading_Session_Active(Session_Type.Off_Hours));
    }

    [Theory]
    [InlineData(Session_Type.London)]
    [InlineData(Session_Type.New_York)]
    [InlineData(Session_Type.Asian)]
    [InlineData(Session_Type.London_NY_Overlap)]
    public void All_Named_Sessions_Are_Active(Session_Type session)
    {
        Assert.True(_sut.Is_Trading_Session_Active(session));
    }

    [Theory]
    [InlineData(Session_Type.London,           13)]
    [InlineData(Session_Type.London_NY_Overlap, 15)]
    [InlineData(Session_Type.New_York,          12)]
    [InlineData(Session_Type.Asian,              7)]
    [InlineData(Session_Type.Off_Hours,          0)]
    public void Session_Quality_Score_Is_Expected(Session_Type session, int expected_score)
    {
        Assert.Equal(expected_score, _sut.Get_Session_Quality_Score(session));
    }

    [Theory]
    [InlineData(Session_Type.London,           true)]
    [InlineData(Session_Type.London_NY_Overlap, true)]
    [InlineData(Session_Type.New_York,          false)]
    [InlineData(Session_Type.Asian,             false)]
    public void Is_Good_For_Breakout_Only_London(Session_Type session, bool expected)
    {
        Assert.Equal(expected, _sut.Is_Good_For_Breakout(session));
    }
}
