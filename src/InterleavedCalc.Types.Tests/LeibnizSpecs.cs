using InterleavedCalc.Types.Leibniz;

namespace InterleavedCalc.Types.Tests;

public class LeibnizSpecs
{
    [Fact]
    public void ShouldCalculatePi()
    {
        var output = PiCalculator.CalculatePi(1000);
        
        // at 1000 iterations, we're at a pretty low precision for PI
        Assert.True(output > 3.14);
    }
}