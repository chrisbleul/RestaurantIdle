using BalancingCore;
using BreakInfinity;
using Xunit;

namespace BalancingCore.Tests;

public class MilestonesTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(24, 1)]
    [InlineData(25, 2)]
    [InlineData(49, 2)]
    [InlineData(50, 4)]
    [InlineData(99, 4)]
    [InlineData(100, 8)]
    [InlineData(199, 8)]
    [InlineData(200, 16)]
    [InlineData(1000, 16)]
    public void Multiplier_StacksAtEachDefaultThreshold(int owned, double expected)
    {
        var multiplier = Milestones.Multiplier(owned);
        Assert.Equal((BigDouble)expected, multiplier);
    }

    [Fact]
    public void Multiplier_CustomThresholdsAndFactor()
    {
        var multiplier = Milestones.Multiplier(ownedCount: 15, thresholds: [10, 20], perMilestone: 3.0);
        Assert.Equal((BigDouble)3.0, multiplier);
    }

    [Theory]
    [InlineData(0, 25)]
    [InlineData(24, 25)]
    [InlineData(25, 50)]
    [InlineData(199, 200)]
    public void NextThreshold_ReturnsFirstUnreached(int owned, int expected)
    {
        Assert.Equal(expected, Milestones.NextThreshold(owned));
    }

    [Fact]
    public void NextThreshold_AllReached_ReturnsNull()
    {
        Assert.Null(Milestones.NextThreshold(200));
    }
}
