using BalancingCore;
using BreakInfinity;
using Xunit;

namespace BalancingCore.Tests;

public class PrestigeTests
{
    [Fact]
    public void TotalStars_ZeroRevenue_IsZero()
    {
        Assert.Equal(BigDouble.Zero, Prestige.TotalStars(0, k: 1.0));
    }

    [Fact]
    public void TotalStars_MatchesFormula()
    {
        // k * sqrt(revenue): k=2, revenue=100 -> 2*10 = 20.
        var stars = Prestige.TotalStars(lifetimeRevenue: 100, k: 2.0);
        Assert.Equal((BigDouble)20, stars);
    }

    [Fact]
    public void TotalStars_NegativeRevenue_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Prestige.TotalStars(-1, 1.0));
    }

    [Fact]
    public void StarsGainedFromReset_SubtractsCurrentStars()
    {
        // total = 1 * sqrt(10000) = 100; bereits 30 gehalten -> Gewinn 70.
        var gain = Prestige.StarsGainedFromReset(lifetimeRevenue: 10000, k: 1.0, currentStars: 30);
        Assert.Equal((BigDouble)70, gain);
    }

    [Fact]
    public void StarsGainedFromReset_BelowCurrentStars_ClampsToZero()
    {
        var gain = Prestige.StarsGainedFromReset(lifetimeRevenue: 100, k: 1.0, currentStars: 999);
        Assert.Equal(BigDouble.Zero, gain);
    }
}
