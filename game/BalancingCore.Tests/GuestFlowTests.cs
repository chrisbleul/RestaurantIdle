using BalancingCore;
using BreakInfinity;
using Xunit;

namespace BalancingCore.Tests;

public class GuestFlowTests
{
    [Fact]
    public void GuestFlowAt_Null_IstBasiswert()
    {
        Assert.Equal(GuestFlow.BaseGuestFlow, GuestFlow.GuestFlowAt(0));
    }

    [Fact]
    public void GuestFlowAt_WaechstProStufe()
    {
        // 10 Basis + 5 pro Stufe: Stufe 3 -> 25.
        Assert.Equal((BigDouble)25, GuestFlow.GuestFlowAt(3));
    }

    [Fact]
    public void GuestFlowAt_NegativeStufe_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GuestFlow.GuestFlowAt(-1));
    }

    [Fact]
    public void NextMarketingCost_FolgtDerKostenkurve()
    {
        var expected = CostCurve.Cost(GuestFlow.MarketingBaseCost, GuestFlow.MarketingCostGrowthRate, 2);
        Assert.Equal(expected, GuestFlow.NextMarketingCost(2));
    }

    [Fact]
    public void CapacityFactor_GenugGaeste_IstVoll()
    {
        Assert.Equal(1.0, GuestFlow.CapacityFactor(potentialPerSecond: 5, guestFlow: 10));
    }

    [Fact]
    public void CapacityFactor_GenauGleich_IstVoll()
    {
        Assert.Equal(1.0, GuestFlow.CapacityFactor(potentialPerSecond: 10, guestFlow: 10));
    }

    [Fact]
    public void CapacityFactor_ZuWenigGaeste_WirdProportionalGedeckelt()
    {
        // Doppelt so viel Produktion wie Gaeste -> nur die Haelfte wird verkauft.
        Assert.Equal(0.5, GuestFlow.CapacityFactor(potentialPerSecond: 20, guestFlow: 10), precision: 10);
    }

    [Fact]
    public void CapacityFactor_KeinePotenzielleProduktion_IstVoll()
    {
        // Nichts zu verkaufen ist kein Engpass -- 1.0, nicht 0 oder NaN (Division durch 0 vermeiden).
        Assert.Equal(1.0, GuestFlow.CapacityFactor(potentialPerSecond: 0, guestFlow: 10));
    }
}
