using BalancingCore;
using BreakInfinity;
using Xunit;

namespace BalancingCore.Tests;

public class MilestonesTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(9, 1)]
    [InlineData(10, 2)]
    [InlineData(24, 2)]
    [InlineData(25, 4)]
    [InlineData(49, 4)]
    [InlineData(50, 8)]
    [InlineData(1000, 8)]
    public void Multiplier_StacksAtEachDefaultThreshold(int level, double expected)
    {
        var multiplier = Milestones.Multiplier(level);
        Assert.Equal((BigDouble)expected, multiplier);
    }

    [Fact]
    public void Multiplier_CustomThresholdsAndFactor()
    {
        var multiplier = Milestones.Multiplier(ownedCount: 15, thresholds: [10, 20], perMilestone: 3.0);
        Assert.Equal((BigDouble)3.0, multiplier);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(9, 10)]
    [InlineData(10, 25)]
    [InlineData(49, 50)]
    public void NextThreshold_ReturnsFirstUnreached(int level, int expected)
    {
        Assert.Equal(expected, Milestones.NextThreshold(level));
    }

    [Fact]
    public void NextThreshold_AllReached_ReturnsNull()
    {
        Assert.Null(Milestones.NextThreshold(50));
    }
}
