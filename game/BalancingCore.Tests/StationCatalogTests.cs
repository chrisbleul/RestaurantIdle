using BalancingCore;
using Xunit;

namespace BalancingCore.Tests;

public class StationCatalogTests
{
    [Fact]
    public void All_HatSiebenStationen()
    {
        Assert.Equal(7, StationCatalog.All.Count);
    }

    [Fact]
    public void All_NamenEntsprechenPlanReihenfolge()
    {
        var expected = new[]
        {
            "Kaffeemaschine", "Fritteuse", "Grill", "Pizzaofen", "Sushi-Bar", "Patisserie", "Chef's Table",
        };

        Assert.Equal(expected, StationCatalog.All.Select(s => s.Name));
    }

    [Fact]
    public void All_ZykluszeitWaechstMonotonMitJederStation()
    {
        for (var i = 1; i < StationCatalog.All.Count; i++)
        {
            Assert.True(StationCatalog.All[i].CycleSeconds > StationCatalog.All[i - 1].CycleSeconds,
                $"{StationCatalog.All[i].Name} sollte laenger dauern als {StationCatalog.All[i - 1].Name}");
        }
    }

    [Fact]
    public void All_BaseCostWaechstMonotonMitJederStation()
    {
        for (var i = 1; i < StationCatalog.All.Count; i++)
        {
            Assert.True(StationCatalog.All[i].BaseCost > StationCatalog.All[i - 1].BaseCost,
                $"{StationCatalog.All[i].Name} sollte teurer sein als {StationCatalog.All[i - 1].Name}");
        }
    }

    [Fact]
    public void All_ManagerKostetMehrAlsDieStationSelbst()
    {
        foreach (var station in StationCatalog.All)
        {
            Assert.True(station.ManagerCost > station.BaseCost,
                $"Manager fuer {station.Name} sollte teurer sein als die erste Station selbst");
        }
    }
}
