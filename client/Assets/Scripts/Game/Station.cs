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
    ///
    /// PLANv3.md K1-Umbau: es gibt pro Station genau EINE Instanz in der 3D-
    /// Szene, keine Stueckzahl -- "OwnedCount = 87" hatte dafuer nie einen
    /// visuellen Ausdruck. Ersetzt durch zwei unabhaengige Upgrade-Achsen
    /// (wie bei Eatventure): Preis (Ertrag/Verkauf) und Ausstattung
    /// (Zyklusgeschwindigkeit). Freischalten (Unlock) ersetzt den ersten Kauf.
    /// </summary>
    [Serializable]
    public class Station
    {
        /// <summary>
        /// Nur fuer die Migration alter Spielstaende (SaveSystem.Migrate,
        /// SchemaVersion 1 -> 2) noch vorhanden -- JsonUtility wuerde den
        /// Wert sonst schon beim Laden verwerfen, bevor die Migration ihn
        /// lesen kann. In neuem Code nicht mehr verwenden, siehe PriceLevel/
        /// EquipmentLevel/IsUnlocked.
        /// </summary>
        [Obsolete("Nur fuer SaveSystem.Migrate -- Gameplay-Code nutzt PriceLevel/EquipmentLevel.")]
        public int OwnedCount;

        public int PriceLevel;
        public int EquipmentLevel;
        public double CycleProgressSeconds;

        /// <summary>Manager ersetzt das manuelle Antippen (Plan Abschnitt 1) -- ohne ihn produziert Tick() nichts.</summary>
        public bool HasManager;

        public bool IsUnlocked => PriceLevel > 0;

        /// <summary>Kosten des Erstkaufs, der die Station ueberhaupt erst freischaltet.</summary>
        public BigDouble UnlockCost(StationDefinition def) => def.BaseCost;

        public void Unlock()
        {
            PriceLevel = 1;
            EquipmentLevel = 1;
        }

        /// <summary>Kosten der naechsten Preis-Stufe (mehr Ertrag/Verkauf). Nur sinnvoll, wenn <see cref="IsUnlocked"/>.</summary>
        public BigDouble NextPriceUpgradeCost(StationDefinition def) =>
            CostCurve.Cost(def.PriceUpgradeBaseCost, def.CostGrowthRate, Math.Max(0, PriceLevel - 1));

        public void UpgradePrice() => PriceLevel++;

        /// <summary>Kosten der naechsten Ausstattungs-Stufe (kuerzerer Zyklus). Nur sinnvoll, wenn <see cref="IsUnlocked"/>.</summary>
        public BigDouble NextEquipmentUpgradeCost(StationDefinition def) =>
            CostCurve.Cost(def.EquipmentUpgradeBaseCost, def.CostGrowthRate, Math.Max(0, EquipmentLevel - 1));

        public void UpgradeEquipment() => EquipmentLevel++;

        /// <summary>Ertrag eines einzelnen Verkaufs -- waechst mit PriceLevel, Meilensteine greifen auf derselben Achse.</summary>
        public BigDouble YieldPerSale(StationDefinition def) =>
            !IsUnlocked
                ? BigDouble.Zero
                : def.BaseYield * BigDouble.Pow(def.YieldGrowthPerPriceLevel, PriceLevel - 1) * Milestones.Multiplier(PriceLevel);

        /// <summary>Aktuelle Zykluszeit -- sinkt mit EquipmentLevel, nach unten begrenzt durch def.MinCycleSeconds.</summary>
        public double CycleSeconds(StationDefinition def) =>
            !IsUnlocked
                ? def.CycleSeconds
                : Math.Max(def.MinCycleSeconds, def.CycleSeconds * Math.Pow(def.SpeedGrowthPerEquipmentLevel, EquipmentLevel - 1));

        public BigDouble YieldPerSecond(StationDefinition def) =>
            !IsUnlocked ? BigDouble.Zero : YieldPerSale(def) / CycleSeconds(def);

        /// <summary>
        /// Rueckt den Zyklus um deltaSeconds vor. Ohne Manager (oder ohne
        /// Freischaltung) ein No-Op -- reines Antippen ueber
        /// <see cref="ProduceNow"/> bleibt dann der einzige Weg zu Ertrag.
        /// </summary>
        public BigDouble Tick(StationDefinition def, double deltaSeconds)
        {
            if (!IsUnlocked || !HasManager)
            {
                return BigDouble.Zero;
            }

            var cycleSeconds = CycleSeconds(def);
            CycleProgressSeconds += deltaSeconds;
            var completedCycles = (int)(CycleProgressSeconds / cycleSeconds);
            if (completedCycles <= 0)
            {
                return BigDouble.Zero;
            }

            CycleProgressSeconds -= completedCycles * cycleSeconds;
            return YieldPerSale(def) * completedCycles;
        }

        /// <summary>Manueller Klick -- schliesst den aktuellen Zyklus sofort ab, unabhaengig vom Manager.</summary>
        public BigDouble ProduceNow(StationDefinition def)
        {
            if (!IsUnlocked)
            {
                return BigDouble.Zero;
            }

            CycleProgressSeconds = 0;
            return YieldPerSale(def);
        }
    }
}
