using BalancingCore;
using BreakInfinity;
using Xunit;

namespace BalancingCore.Tests;

public class CostCurveTests
{
    [Fact]
    public void Cost_FirstPurchase_EqualsBaseCost()
    {
        var cost = CostCurve.Cost(baseCost: 10, growthRate: 1.07, purchaseIndex: 0);
        Assert.Equal((BigDouble)10, cost);
    }

    [Fact]
    public void Cost_GrowsByGrowthRatePerPurchase()
    {
        var first = CostCurve.Cost(baseCost: 10, growthRate: 1.07, purchaseIndex: 0);
        var second = CostCurve.Cost(baseCost: 10, growthRate: 1.07, purchaseIndex: 1);

        Assert.True(second > first);
        Assert.True((second / first - 1.07).Abs() < 1e-9);
    }

    [Fact]
    public void Cost_NegativeIndex_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CostCurve.Cost(10, 1.07, -1));
    }

    [Fact]
    public void TotalCost_MatchesSumOfIndividualCosts()
    {
        var total = CostCurve.TotalCost(baseCost: 10, growthRate: 1.15, startIndex: 5, count: 4);

        var expected = BigDouble.Zero;
        for (var i = 0; i < 4; i++)
        {
            expected += CostCurve.Cost(10, 1.15, 5 + i);
        }

        Assert.True((total - expected).Abs() < (BigDouble)1e-6);
    }

    [Fact]
    public void TotalCost_ZeroCount_IsZero()
    {
        Assert.Equal(BigDouble.Zero, CostCurve.TotalCost(10, 1.07, 0, 0));
    }

    [Fact]
    public void TotalCost_GrowthRateOne_IsFlatMultiple()
    {
        var total = CostCurve.TotalCost(baseCost: 5, growthRate: 1.0, startIndex: 0, count: 3);
        Assert.Equal((BigDouble)15, total);
    }

    [Fact]
    public void MaxAffordable_ReturnsHowManyFitInBudget()
    {
        // Kosten bei r=1: konstant 10 pro Kauf -> Budget 35 kauft genau 3.
        var count = CostCurve.MaxAffordable(baseCost: 10, growthRate: 1.0, startIndex: 0, budget: 35);
        Assert.Equal(3, count);
    }

    [Fact]
    public void MaxAffordable_BudgetBelowFirstCost_ReturnsZero()
    {
        var count = CostCurve.MaxAffordable(baseCost: 100, growthRate: 1.07, startIndex: 0, budget: 50);
        Assert.Equal(0, count);
    }
}
