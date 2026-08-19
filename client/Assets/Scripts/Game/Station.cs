using System;
using BalancingCore;
using BreakInfinity;

namespace RestaurantIdle.Game
{
    /// <summary>
    /// Laufzeit-Zustand einer Station: wie viele besessen, wie weit der aktuelle
    /// Produktionszyklus fortgeschritten ist. Reine Logik, kein MonoBehaviour --
    /// so bleibt sie ohne Unity-Bootstrap testbar (Plan Abschnitt 2: "als reines
    /// C#-Modul ohne Unity-Abhaengigkeit").
    /// </summary>
    [Serializable]
    public class Station
    {
        public int OwnedCount;
        public double CycleProgressSeconds;
        public bool HasRecipeUpgrade;

        public BigDouble NextCost => CostCurve.Cost(StationDefinition.BaseCost, StationDefinition.CostGrowthRate, OwnedCount);

        public bool CanAfford(BigDouble revenue) => OwnedCount == 0 ? revenue >= StationDefinition.BaseCost : revenue >= NextCost;

        public void Buy()
        {
            OwnedCount++;
        }

        /// <summary>Ertrag pro einzelnem abgeschlossenen Zyklus -- Meilenstein-Boni (Plan Abschnitt 2) und Rezept-Upgrade eingerechnet.</summary>
        public BigDouble YieldPerCycle
        {
            get
            {
                var yield = StationDefinition.BaseYield * Milestones.Multiplier(OwnedCount);
                if (HasRecipeUpgrade)
                {
                    yield *= 2;
                }
                return yield;
            }
        }

        /// <summary>Ertrag pro Sekunde -- fuer Offline-Berechnung und UI-Anzeige (kein tatsaechlicher Tick).</summary>
        public BigDouble YieldPerSecond => OwnedCount == 0 ? BigDouble.Zero : YieldPerCycle * OwnedCount / StationDefinition.CycleSeconds;

        /// <summary>
        /// Rueckt den Zyklus um deltaSeconds vor und gibt den Ertrag aller in dieser
        /// Zeitspanne abgeschlossenen Zyklen zurueck (0, wenn OwnedCount == 0 --
        /// Idle-Produktion setzt Personal/eine Station voraus).
        /// </summary>
        public BigDouble Tick(double deltaSeconds)
        {
            if (OwnedCount == 0)
            {
                return BigDouble.Zero;
            }

            CycleProgressSeconds += deltaSeconds;
            var completedCycles = (int)(CycleProgressSeconds / StationDefinition.CycleSeconds);
            if (completedCycles <= 0)
            {
                return BigDouble.Zero;
            }

            CycleProgressSeconds -= completedCycles * StationDefinition.CycleSeconds;
            return YieldPerCycle * OwnedCount * completedCycles;
        }
    }
}
