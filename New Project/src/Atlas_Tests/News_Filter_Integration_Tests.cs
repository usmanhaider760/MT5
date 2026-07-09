using Atlas_Domain.BusinessObjects;
using Atlas_Market_Data.Services;
using Xunit;

namespace Atlas_Tests;

public class News_Filter_Integration_Tests
{
    [Fact]
    public async Task NFP_Thirty_Minutes_Away_Blocks_EURUSD()
    {
        var sut = new News_Filter_Service();
        sut.Seed_Event(new News_Event_BO
        {
            Currency = "USD",
            Event_Name = "Non-Farm Payrolls",
            Event_UTC = DateTime.UtcNow.AddMinutes(30),
            Impact_Level = "High"
        });

        var blocked = await sut.Is_News_Lockout_Active_Async("EURUSD");

        Assert.True(blocked);
    }

    [Fact]
    public async Task FOMC_Ninety_Minutes_Away_Blocks_XAUUSD_Within_120_Minute_Gold_Window()
    {
        var sut = new News_Filter_Service();
        sut.Seed_Event(new News_Event_BO
        {
            Currency = "USD",
            Event_Name = "FOMC Rate Decision",
            Event_UTC = DateTime.UtcNow.AddMinutes(90),
            Impact_Level = "High"
        });

        var blocked = await sut.Is_News_Lockout_Active_Async("XAUUSD", is_gold: true);

        Assert.True(blocked);
    }

    [Fact]
    public async Task Past_Event_Outside_After_Window_Does_Not_Block()
    {
        var sut = new News_Filter_Service();
        sut.Seed_Event(new News_Event_BO
        {
            Currency = "USD",
            Event_Name = "CPI m/m",
            Event_UTC = DateTime.UtcNow.AddHours(-2), // well outside the after-window (max 90 min)
            Impact_Level = "High"
        });

        var blocked = await sut.Is_News_Lockout_Active_Async("EURUSD");

        Assert.False(blocked);
    }

    [Fact]
    public async Task AUD_Event_Does_Not_Block_EURUSD()
    {
        var sut = new News_Filter_Service();
        sut.Seed_Event(new News_Event_BO
        {
            Currency = "AUD",
            Event_Name = "RBA Rate Statement",
            Event_UTC = DateTime.UtcNow.AddMinutes(10),
            Impact_Level = "High"
        });

        var blocked = await sut.Is_News_Lockout_Active_Async("EURUSD");

        Assert.False(blocked);
    }

    [Fact]
    public async Task NFP_Two_Hours_Ago_Does_Not_Block_EURUSD()
    {
        // NFP after-window is 90 minutes — 2 hours (120 min) ago is outside it
        var sut = new News_Filter_Service();
        sut.Seed_Event(new News_Event_BO
        {
            Currency = "USD",
            Event_Name = "Non-Farm Payrolls",
            Event_UTC = DateTime.UtcNow.AddHours(-2),
            Impact_Level = "High"
        });

        var blocked = await sut.Is_News_Lockout_Active_Async("EURUSD");

        Assert.False(blocked);
    }

    [Fact]
    public async Task FOMC_Sixty_Minutes_Away_Blocks_EURUSD_Within_120_Minute_Forex_Window()
    {
        var sut = new News_Filter_Service();
        sut.Seed_Event(new News_Event_BO
        {
            Currency = "USD",
            Event_Name = "FOMC Rate Decision",
            Event_UTC = DateTime.UtcNow.AddMinutes(60),
            Impact_Level = "High"
        });

        var blocked = await sut.Is_News_Lockout_Active_Async("EURUSD");

        Assert.True(blocked);
    }

    [Fact]
    public async Task CPI_FortyFive_Minutes_Away_Blocks_GBPUSD_Within_60_Minute_Window()
    {
        var sut = new News_Filter_Service();
        sut.Seed_Event(new News_Event_BO
        {
            Currency = "GBP",
            Event_Name = "CPI y/y",
            Event_UTC = DateTime.UtcNow.AddMinutes(45),
            Impact_Level = "High"
        });

        var blocked = await sut.Is_News_Lockout_Active_Async("GBPUSD");

        Assert.True(blocked);
    }

    [Fact]
    public async Task Standard_High_Impact_Event_Twenty_Minutes_Away_Blocks_Within_30_Minute_Window()
    {
        var sut = new News_Filter_Service();
        sut.Seed_Event(new News_Event_BO
        {
            Currency = "USD",
            Event_Name = "Retail Sales m/m",
            Event_UTC = DateTime.UtcNow.AddMinutes(20),
            Impact_Level = "High"
        });

        var blocked = await sut.Is_News_Lockout_Active_Async("EURUSD");

        Assert.True(blocked);
    }

    [Fact]
    public async Task Low_Impact_Event_Never_Blocks_Regardless_Of_Timing()
    {
        var sut = new News_Filter_Service();
        sut.Seed_Event(new News_Event_BO
        {
            Currency = "USD",
            Event_Name = "Non-Farm Payrolls",
            Event_UTC = DateTime.UtcNow.AddMinutes(1), // right on top of "now"
            Impact_Level = "Low"
        });

        var blocked = await sut.Is_News_Lockout_Active_Async("EURUSD");

        Assert.False(blocked);
    }

    [Fact]
    public async Task Gold_FOMC_Blocked_180_Minutes_Before_Extended_Gold_Window()
    {
        var sut = new News_Filter_Service();
        sut.Seed_Event(new News_Event_BO
        {
            Currency = "USD",
            Event_Name = "FOMC Rate Decision",
            Event_UTC = DateTime.UtcNow.AddMinutes(179),
            Impact_Level = "High"
        });

        var blocked = await sut.Is_News_Lockout_Active_Async("XAUUSD", is_gold: true);

        Assert.True(blocked);
    }
}
