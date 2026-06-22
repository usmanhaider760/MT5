using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;
using Atlas_Domain.Interfaces;
using Atlas_Risk.Services;
using Xunit;

namespace Atlas_Tests;

public class Drawdown_Guard_Tests
{
    private sealed class Stub_Emergency_Stop : I_Emergency_Stop_Service
    {
        public bool Kill_Switch_Active => false;
        public Bot_Mode_Type Current_Mode => Bot_Mode_Type.Demo;
        public Task Activate_Emergency_Stop_Async(string reason) => Task.CompletedTask;
        public Task Evaluate_Drawdown_Mode_Async(Account_State_BO account, Risk_Setting_BO risk_settings) => Task.CompletedTask;
    }

    private static Drawdown_Guard Build(decimal caution = 2.0m, decimal recovery = 4.0m, decimal full_stop = 8.0m) =>
        new(new Risk_Setting_BO
        {
            Caution_Drawdown_Percent   = caution,
            Recovery_Drawdown_Percent  = recovery,
            Full_Stop_Drawdown_Percent = full_stop
        }, new Stub_Emergency_Stop());

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    [InlineData(1.99)]
    public void Get_Risk_Multiplier_Returns_1_In_Normal_Mode(double dd)
    {
        Assert.Equal(1.0m, Build().Get_Risk_Multiplier((decimal)dd));
    }

    [Theory]
    [InlineData(2.0)]
    [InlineData(3.0)]
    [InlineData(3.99)]
    public void Get_Risk_Multiplier_Returns_Half_In_Caution_Mode(double dd)
    {
        Assert.Equal(0.50m, Build().Get_Risk_Multiplier((decimal)dd));
    }

    [Theory]
    [InlineData(4.0)]
    [InlineData(6.0)]
    [InlineData(7.99)]
    public void Get_Risk_Multiplier_Returns_Quarter_In_Recovery_Mode(double dd)
    {
        Assert.Equal(0.25m, Build().Get_Risk_Multiplier((decimal)dd));
    }

    [Theory]
    [InlineData(0.0, true)]
    [InlineData(3.99, true)]
    [InlineData(4.0, false)]
    [InlineData(6.0, false)]
    public void Is_Gold_Allowed_Reflects_Recovery_Threshold(double dd, bool expected)
    {
        Assert.Equal(expected, Build().Is_Gold_Allowed((decimal)dd));
    }

    [Fact]
    public void Custom_Thresholds_Are_Respected()
    {
        var guard = Build(caution: 1.0m, recovery: 3.0m);
        Assert.Equal(0.50m, guard.Get_Risk_Multiplier(1.0m));   // hits custom caution at 1%
        Assert.Equal(0.25m, guard.Get_Risk_Multiplier(3.0m));   // hits custom recovery at 3%
        Assert.False(guard.Is_Gold_Allowed(3.0m));
    }
}
