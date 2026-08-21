using System.Collections.Generic;
using BreakInfinity;

namespace BalancingCore
{
    /// <summary>
    /// Statische Konfiguration einer Station-Art (Plan Abschnitt 1). Klassischer
    /// struct statt "record struct" -- Unitys Standard-C#-Sprachversion (9.0)
    /// kennt record structs noch nicht, siehe "Unity-CI einrichten" in README.md.
    ///
    /// PLANv3.md K1-Umbau: zwei unabhaengige Upgrade-Achsen statt Stueckzahl
    /// (Preis -> Ertrag/Verkauf, Ausstattung -> Zyklusgeschwindigkeit). Die
    /// abgeleiteten Achsen-Felder (PriceUpgrade*/EquipmentUpgrade*/*GrowthPer*)
    /// werden bewusst aus den bestehenden sechs Katalogwerten berechnet statt
    /// pro Station 4 weitere frei erfundene Zahlen einzufuehren -- die
    /// Ausgangswerte sind ohnehin nur Platzhalter (siehe Klassenkommentar
    /// unten), echte Kalibrierung ist erst mit Playtestdaten sinnvoll
    /// (PLANv3 Phase H).
    /// </summary>
    public readonly struct StationDefinition
    {
        public readonly string Name;
        public readonly double CycleSeconds;
        public readonly double MinCycleSeconds;
        public readonly BigDouble BaseYield;
        public readonly BigDouble BaseCost;
        public readonly double CostGrowthRate;
        public readonly BigDouble PriceUpgradeBaseCost;
        public readonly BigDouble EquipmentUpgradeBaseCost;
        public readonly double YieldGrowthPerPriceLevel;
        public readonly double SpeedGrowthPerEquipmentLevel;
        public readonly BigDouble ManagerCost;

        public StationDefinition(
            string name,
            double cycleSeconds,
            BigDouble baseYield,
            BigDouble baseCost,
            double costGrowthRate,
            BigDouble managerCost)
        {
            Name = name;
            CycleSeconds = cycleSeconds;
            BaseYield = baseYield;
            BaseCost = baseCost;
            CostGrowthRate = costGrowthRate;
            ManagerCost = managerCost;

            MinCycleSeconds = cycleSeconds * 0.2;
            PriceUpgradeBaseCost = baseCost * 0.5;
            EquipmentUpgradeBaseCost = baseCost * 0.5;
            YieldGrowthPerPriceLevel = 1.12;
            SpeedGrowthPerEquipmentLevel = 0.94;
        }
    }

    /// <summary>
    /// Die sieben Stationen aus PLAN.md Abschnitt 1, in Reihenfolge. Kosten/Ertrag
    /// sind Platzhalter-Werte (grober x8-Sprung je Stufe) -- echtes Balancing
    /// braucht Playtest (siehe PLAN.md Abschnitt 8, "Prestige-Ertrag": ebenfalls
    /// als Kalibrierungsaufgabe markiert, nicht als fertige Zahl).
    ///
    /// ManagerCost war urspruenglich durchgehend das 50-fache von BaseCost --
    /// bei einem Startertrag von 1/Verkauf und einem Gast alle paar Sekunden
    /// bedeutete das ~10-15 Minuten reines Antippen bis zum allerersten
    /// Manager. Der erste Automatisierungs-Meilenstein soll sich frueh und
    /// befriedigend anfuehlen (Genre-Konvention: erste Automatisierung
    /// innerhalb der ersten ein bis drei Minuten), deshalb auf das 12-fache
    /// reduziert -- weiterhin ein spuerbarer Sprung, aber kein Grind.
    /// </summary>
    public static class StationCatalog
    {
        public static readonly IReadOnlyList<StationDefinition> All = new[]
        {
            new StationDefinition("Kaffeemaschine", 2, 1, 10, 1.07, 120),
            new StationDefinition("Fritteuse", 5, 4, 80, 1.07, 960),
            new StationDefinition("Grill", 15, 18, 640, 1.08, 7_680),
            new StationDefinition("Pizzaofen", 45, 90, 5_120, 1.08, 61_440),
            new StationDefinition("Sushi-Bar", 120, 320, 40_960, 1.10, 491_520),
            new StationDefinition("Patisserie", 300, 1_000, 327_680, 1.10, 3_932_160),
            new StationDefinition("Chef's Table", 900, 4_000, 2_621_440, 1.15, 31_457_280),
        };
    }
}
