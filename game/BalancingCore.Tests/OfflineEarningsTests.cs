using BalancingCore;
using BreakInfinity;
using Xunit;

namespace BalancingCore.Tests;

public class OfflineEarningsTests
{
    [Fact]
    public void Calculate_WithinFullRateWindow_Pays100Percent()
    {
        var earnings = OfflineEarnings.Calculate(incomePerSecond: 2, offlineDuration: TimeSpan.FromHours(1));
        // 3600s * 2/s = 7200.
        Assert.Equal((BigDouble)7200, earnings);
    }

    [Fact]
    public void Calculate_ExactlyAtTwoHours_Pays100PercentThroughout()
    {
        var earnings = OfflineEarnings.Calculate(incomePerSecond: 1, offlineDuration: TimeSpan.FromHours(2));
        Assert.Equal((BigDouble)7200, earnings);
    }

    [Fact]
    public void Calculate_PastTwoHours_SwitchesToHalfRate()
    {
        // 2h @ 100% + 1h @ 50%, 1/s: 7200 + 1800 = 9000.
        var earnings = OfflineEarnings.Calculate(incomePerSecond: 1, offlineDuration: TimeSpan.FromHours(3));
        Assert.Equal((BigDouble)9000, earnings);
    }

    [Fact]
    public void Calculate_BeyondCap_ClampsAt24Hours()
    {
        var at24h = OfflineEarnings.Calculate(incomePerSecond: 1, offlineDuration: TimeSpan.FromHours(24));
        var at48h = OfflineEarnings.Calculate(incomePerSecond: 1, offlineDuration: TimeSpan.FromHours(48));

        Assert.Equal(at24h, at48h);
    }

    [Fact]
    public void Calculate_Zero_IsZero()
    {
        Assert.Equal(BigDouble.Zero, OfflineEarnings.Calculate(5, TimeSpan.Zero));
    }

    [Fact]
    public void Calculate_NegativeDuration_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => OfflineEarnings.Calculate(1, TimeSpan.FromSeconds(-1)));
    }
}
