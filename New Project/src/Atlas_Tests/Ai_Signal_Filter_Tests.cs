using Atlas_Application.Services;
using Atlas_Domain.BusinessObjects;
using Xunit;

namespace Atlas_Tests;

public class Ai_Signal_Filter_Tests
{
    [Fact]
    public async Task Default_Filter_Passes_Every_Signal_Through()
    {
        var sut = new Ai_Signal_Filter();

        var (approved, confidence, reason) = await sut.Evaluate_Async(new Trade_Signal_BO(), new Market_Context_BO());

        Assert.True(approved);
        Assert.Equal(100, confidence);
        Assert.Contains("no AI model configured", reason);
    }
}
