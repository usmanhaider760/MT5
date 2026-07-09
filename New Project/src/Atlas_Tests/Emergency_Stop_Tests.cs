using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;
using Atlas_Execution.Services;
using Xunit;

namespace Atlas_Tests;

public class Emergency_Stop_Tests
{
    private static Account_State_BO Account_With_Drawdown(decimal drawdown_percent) => new()
    {
        Peak_Equity = 10_000,
        Equity = 10_000 * (1 - drawdown_percent / 100m)
    };

    [Fact]
    public async Task No_Drawdown_Stays_At_Base_Mode()
    {
        var sut = new Emergency_Stop_Service();
        var settings = Risk_Setting_BO.Conservative_Launch(); // Mode = Demo

        await sut.Evaluate_Drawdown_Mode_Async(Account_With_Drawdown(0m), settings);

        Assert.Equal(Bot_Mode_Type.Demo, sut.Current_Mode);
        Assert.False(sut.Kill_Switch_Active);
    }

    [Fact]
    public async Task Caution_Level_Drawdown_Stays_At_Base_Mode()
    {
        // 2.5% drawdown is above Caution (2%) but below Recovery (4%) — this service has no Caution tier of its own
        var sut = new Emergency_Stop_Service();
        var settings = Risk_Setting_BO.Conservative_Launch();

        await sut.Evaluate_Drawdown_Mode_Async(Account_With_Drawdown(2.5m), settings);

        Assert.Equal(Bot_Mode_Type.Demo, sut.Current_Mode);
    }

    [Fact]
    public async Task Recovery_Level_Drawdown_Switches_To_Micro_Live()
    {
        var sut = new Emergency_Stop_Service();
        var settings = Risk_Setting_BO.Conservative_Launch(); // Recovery = 4.0%

        await sut.Evaluate_Drawdown_Mode_Async(Account_With_Drawdown(4.5m), settings);

        Assert.Equal(Bot_Mode_Type.Micro_Live, sut.Current_Mode);
    }

    [Fact]
    public async Task Protection_Level_Drawdown_Switches_To_Emergency_Stop_Without_Activating_Kill_Switch()
    {
        var sut = new Emergency_Stop_Service();
        var settings = Risk_Setting_BO.Conservative_Launch(); // Protection = 6.0%, Full_Stop = 8.0%

        await sut.Evaluate_Drawdown_Mode_Async(Account_With_Drawdown(6.5m), settings);

        Assert.Equal(Bot_Mode_Type.Emergency_Stop, sut.Current_Mode);
        Assert.False(sut.Kill_Switch_Active); // hard kill switch is only tripped at Full_Stop, via Drawdown_Guard
    }

    [Fact]
    public async Task Mode_Recovers_To_Base_Mode_After_Drawdown_Subsides()
    {
        // This is the P0-6 regression: mode must step back down, not stay stuck at the worst level ever seen
        var sut = new Emergency_Stop_Service();
        var settings = Risk_Setting_BO.Conservative_Launch();

        await sut.Evaluate_Drawdown_Mode_Async(Account_With_Drawdown(4.5m), settings);
        Assert.Equal(Bot_Mode_Type.Micro_Live, sut.Current_Mode);

        await sut.Evaluate_Drawdown_Mode_Async(Account_With_Drawdown(1.0m), settings);

        Assert.Equal(Bot_Mode_Type.Demo, sut.Current_Mode);
    }

    [Fact]
    public async Task Mode_Recovers_To_Configured_Base_Mode_Not_Hardcoded_Demo()
    {
        var sut = new Emergency_Stop_Service();
        var settings = Risk_Setting_BO.Conservative_Launch();
        settings.Mode = Bot_Mode_Type.Micro_Live;

        await sut.Evaluate_Drawdown_Mode_Async(Account_With_Drawdown(6.5m), settings);
        Assert.Equal(Bot_Mode_Type.Emergency_Stop, sut.Current_Mode);

        await sut.Evaluate_Drawdown_Mode_Async(Account_With_Drawdown(0m), settings);

        Assert.Equal(Bot_Mode_Type.Micro_Live, sut.Current_Mode);
    }

    [Fact]
    public void Kill_Switch_Starts_False()
    {
        var sut = new Emergency_Stop_Service();
        Assert.False(sut.Kill_Switch_Active);
        Assert.Equal(Bot_Mode_Type.Demo, sut.Current_Mode);
    }

    [Fact]
    public async Task Activate_Emergency_Stop_Sets_Kill_Switch_And_Mode_And_Fires_Event_With_Reason()
    {
        var sut = new Emergency_Stop_Service();
        string? fired_reason = null;
        sut.On_Emergency_Stop += reason => fired_reason = reason;

        await sut.Activate_Emergency_Stop_Async("Drawdown exceeded full-stop threshold");

        Assert.True(sut.Kill_Switch_Active);
        Assert.Equal(Bot_Mode_Type.Emergency_Stop, sut.Current_Mode);
        Assert.Equal("Drawdown exceeded full-stop threshold", fired_reason);
    }

    [Fact]
    public async Task On_Mode_Changed_Fires_On_Transition_But_Not_When_Mode_Is_Unchanged()
    {
        var sut = new Emergency_Stop_Service();
        var settings = Risk_Setting_BO.Conservative_Launch();
        int fire_count = 0;
        sut.On_Mode_Changed += _ => fire_count++;

        await sut.Evaluate_Drawdown_Mode_Async(Account_With_Drawdown(4.5m), settings); // Demo -> Micro_Live
        Assert.Equal(1, fire_count);

        await sut.Evaluate_Drawdown_Mode_Async(Account_With_Drawdown(4.6m), settings); // still Micro_Live
        Assert.Equal(1, fire_count);

        await sut.Evaluate_Drawdown_Mode_Async(Account_With_Drawdown(0m), settings); // Micro_Live -> Demo
        Assert.Equal(2, fire_count);
    }

    [Fact]
    public async Task Reset_Kill_Switch_Clears_Kill_Switch_And_Returns_Mode_To_Demo()
    {
        var sut = new Emergency_Stop_Service();
        await sut.Activate_Emergency_Stop_Async("Full stop");
        Assert.True(sut.Kill_Switch_Active);

        sut.Reset_Kill_Switch();

        Assert.False(sut.Kill_Switch_Active);
        Assert.Equal(Bot_Mode_Type.Demo, sut.Current_Mode);
    }
}
