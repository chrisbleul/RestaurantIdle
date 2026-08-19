using BreakInfinity;

namespace RestaurantIdle.Game
{
    /// <summary>
    /// Statische Konfiguration einer Station (Plan Abschnitt 1). Fest im Code fuer
    /// den grauen Prototyp (Phase 2) -- ein Content-System mit mehreren Stationen
    /// kommt erst in Phase 3, nicht auf Vorrat vorab bauen.
    /// </summary>
    public static class StationDefinition
    {
        public const string Name = "Kaffeemaschine";
        public const double CycleSeconds = 2.0;
        public static readonly BigDouble BaseYield = 1;
        public static readonly BigDouble BaseCost = 10;
        public const double CostGrowthRate = 1.07;

        /// <summary>Rezept-Upgrade (Plan Abschnitt 1): einmalig kaufbar, verdoppelt den Ertrag dauerhaft.</summary>
        public static readonly BigDouble RecipeUpgradeCost = 250;
    }
}
