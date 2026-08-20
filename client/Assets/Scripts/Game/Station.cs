using System;
using BalancingCore;
using BreakInfinity;

namespace RestaurantIdle.Game
{
    /// <summary>
    /// Laufzeit-Zustand einer einzelnen Station-Instanz. Kennt die eigene
    /// <see cref="StationDefinition"/> absichtlich nicht (kein gespeichertes
    /// Feld dafuer) -- die kommt vom Aufrufer (GameManager, ueber den Index in
    /// StationCatalog.All), damit sich nichts nach dem Laden eines Spielstands
    /// erst wieder "verdrahten" muss.
    /// </summary>
    [Serializable]
    public class Station
    {
        public int OwnedCount;
        public double CycleProgressSeconds;

        /// <summary>Manager ersetzt das manuelle Antippen (Plan Abschnitt 1) -- ohne ihn produziert Tick() nichts.</summary>
        public bool HasManager;

        public BigDouble NextCost(StationDefinition def) =>
            CostCurve.Cost(def.BaseCost, def.CostGrowthRate, OwnedCount);

        public void Buy() => OwnedCount++;

        public BigDouble YieldPerCycle(StationDefinition def) =>
            def.BaseYield * Milestones.Multiplier(OwnedCount);

        public BigDouble YieldPerSecond(StationDefinition def) =>
            OwnedCount == 0 ? BigDouble.Zero : YieldPerCycle(def) * OwnedCount / def.CycleSeconds;

        /// <summary>
        /// Rueckt den Zyklus um deltaSeconds vor. Ohne Manager (oder ohne
        /// besessene Einheiten) ein No-Op -- reines Antippen ueber
        /// <see cref="ProduceNow"/> bleibt dann der einzige Weg zu Ertrag.
        /// </summary>
        public BigDouble Tick(StationDefinition def, double deltaSeconds)
        {
            if (OwnedCount == 0 || !HasManager)
            {
                return BigDouble.Zero;
            }

            CycleProgressSeconds += deltaSeconds;
            var completedCycles = (int)(CycleProgressSeconds / def.CycleSeconds);
            if (completedCycles <= 0)
            {
                return BigDouble.Zero;
            }

            CycleProgressSeconds -= completedCycles * def.CycleSeconds;
            return YieldPerCycle(def) * OwnedCount * completedCycles;
        }

        /// <summary>Manueller Klick -- schliesst den aktuellen Zyklus sofort ab, unabhaengig vom Manager.</summary>
        public BigDouble ProduceNow(StationDefinition def)
        {
            if (OwnedCount == 0)
            {
                return BigDouble.Zero;
            }

            CycleProgressSeconds = 0;
            return YieldPerCycle(def);
        }
    }
}
