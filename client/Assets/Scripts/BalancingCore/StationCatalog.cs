using BreakInfinity;

namespace BalancingCore;

/// <summary>Statische Konfiguration einer Station-Art (Plan Abschnitt 1).</summary>
public readonly record struct StationDefinition(
    string Name,
    double CycleSeconds,
    BigDouble BaseYield,
    BigDouble BaseCost,
    double CostGrowthRate,
    BigDouble ManagerCost);

/// <summary>
/// Die sieben Stationen aus PLAN.md Abschnitt 1, in Reihenfolge. Kosten/Ertrag
/// sind Platzhalter-Werte (grober x8-Sprung je Stufe) -- echtes Balancing
/// braucht Playtest (siehe PLAN.md Abschnitt 8, "Prestige-Ertrag": ebenfalls
/// als Kalibrierungsaufgabe markiert, nicht als fertige Zahl).
/// </summary>
public static class StationCatalog
{
    public static readonly IReadOnlyList<StationDefinition> All = new[]
    {
        new StationDefinition("Kaffeemaschine", 2, 1, 10, 1.07, 500),
        new StationDefinition("Fritteuse", 5, 4, 80, 1.07, 4_000),
        new StationDefinition("Grill", 15, 18, 640, 1.08, 32_000),
        new StationDefinition("Pizzaofen", 45, 90, 5_120, 1.08, 256_000),
        new StationDefinition("Sushi-Bar", 120, 320, 40_960, 1.10, 2_048_000),
        new StationDefinition("Patisserie", 300, 1_000, 327_680, 1.10, 16_384_000),
        new StationDefinition("Chef's Table", 900, 4_000, 2_621_440, 1.15, 131_072_000),
    };
}
