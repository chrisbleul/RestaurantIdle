using System.Collections.Generic;
using BreakInfinity;

namespace BalancingCore
{
    /// <summary>
    /// Meilenstein-Boni: ×2 Ertrag bei Preis-Level 10 / 25 / 50 einer Station
    /// (PLANv3.md K1: Level statt Stueckzahl, seit dem OwnedCount->PriceLevel-
    /// Umbau gibt es keine "besessenen Einheiten" mehr). Jeder erreichte
    /// Meilenstein multipliziert zusaetzlich, statt ihn zu ersetzen -- bei
    /// Level 50 greifen also 10, 25 *und* 50 (×8 insgesamt), das erzeugt die
    /// "spürbaren Sprünge" aus dem Plan.
    /// </summary>
    public static class Milestones
    {
        public static readonly int[] DefaultThresholds = new[] { 10, 25, 50 };

        public static BigDouble Multiplier(int ownedCount, IReadOnlyList<int> thresholds = null, double perMilestone = 2.0)
        {
            thresholds ??= DefaultThresholds;

            BigDouble multiplier = 1;
            foreach (var threshold in thresholds)
            {
                if (ownedCount >= threshold)
                {
                    multiplier *= perMilestone;
                }
            }

            return multiplier;
        }

        /// <summary>Nächster noch nicht erreichter Meilenstein, oder null wenn alle erreicht sind -- fürs UI ("noch 7 bis ×2").</summary>
        public static int? NextThreshold(int ownedCount, IReadOnlyList<int> thresholds = null)
        {
            thresholds ??= DefaultThresholds;
            foreach (var threshold in thresholds)
            {
                if (ownedCount < threshold)
                {
                    return threshold;
                }
            }

            return null;
        }
    }
}
