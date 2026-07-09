using Atlas_Execution.MT5;
using Xunit;

namespace Atlas_Tests;

/// <summary>
/// Demo-mode behavior of MT5_Execution_Service — every mutating call short-circuits
/// without touching the bridge, so these are testable without a live MT5 connection.
/// </summary>
public class MT5_Execution_Service_Tests
{
    private readonly MT5_Execution_Service _sut = new(new MT5_Bridge_Client(), demo_mode: true);

    [Fact]
    public async Task Modify_Take_Profit_Returns_True_In_Demo_Mode()
    {
        var result = await _sut.Modify_Take_Profit_Async(12345, 1.2000m);
        Assert.True(result);
    }

    [Fact]
    public async Task Modify_Stop_Loss_Returns_True_In_Demo_Mode()
    {
        var result = await _sut.Modify_Stop_Loss_Async(12345, 1.1950m);
        Assert.True(result);
    }
}
