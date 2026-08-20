using System.Collections.Generic;
using BreakInfinity;

namespace BalancingCore
{
    /// <summary>
    /// Meilenstein-Boni: ×2 Ertrag bei 25 / 50 / 100 / 200 besessenen Einheiten
    /// einer Station (Plan Abschnitt 2). Jeder erreichte Meilenstein multipliziert
    /// zusätzlich, statt ihn zu ersetzen -- bei 100 Stück greifen also 25, 50 *und*
    /// 100 (×8 insgesamt), das erzeugt die "spürbaren Sprünge" aus dem Plan.
    /// </summary>
    public static class Milestones
    {
        public static readonly int[] DefaultThresholds = new[] { 25, 50, 100, 200 };

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
