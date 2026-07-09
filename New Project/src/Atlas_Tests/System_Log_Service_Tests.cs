using Atlas_Application.Services;
using Atlas_Domain.Enums;
using Xunit;

namespace Atlas_Tests;

public class System_Log_Service_Tests
{
    [Fact]
    public void Log_Adds_Entry_Retrievable_Via_Get_Recent()
    {
        var sut = new System_Log_Service();

        sut.Log(Log_Level_Type.Warning, "Spread too high", "Spread_Filter_Service");

        var recent = sut.Get_Recent();
        var entry = Assert.Single(recent);
        Assert.Equal(Log_Level_Type.Warning, entry.Level);
        Assert.Equal("Spread too high", entry.Message);
        Assert.Equal("Spread_Filter_Service", entry.Source);
    }

    [Fact]
    public void Log_Fires_On_Log_Event()
    {
        var sut = new System_Log_Service();
        Atlas_Domain.BusinessObjects.Log_Entry_BO? fired = null;
        sut.On_Log += e => fired = e;

        sut.Log(Log_Level_Type.Error, "Pipeline cycle exception");

        Assert.NotNull(fired);
        Assert.Equal(Log_Level_Type.Error, fired!.Level);
    }

    [Fact]
    public void Ring_Buffer_Caps_At_1000_Entries_Dropping_The_Oldest()
    {
        var sut = new System_Log_Service();

        for (int i = 0; i < 1005; i++)
            sut.Log(Log_Level_Type.Info, $"entry {i}");

        var recent = sut.Get_Recent(count: 2000); // ask for more than the cap
        Assert.Equal(1000, recent.Count);
        Assert.Equal("entry 5", recent[0].Message);   // the first 5 were evicted
        Assert.Equal("entry 1004", recent[^1].Message);
    }

    [Fact]
    public void Get_Recent_Returns_Only_The_Requested_Count_Newest_First_Order_Preserved()
    {
        var sut = new System_Log_Service();
        sut.Log(Log_Level_Type.Info, "first");
        sut.Log(Log_Level_Type.Info, "second");
        sut.Log(Log_Level_Type.Info, "third");

        var recent = sut.Get_Recent(count: 2);

        Assert.Equal(2, recent.Count);
        Assert.Equal("second", recent[0].Message);
        Assert.Equal("third", recent[1].Message);
    }
}
