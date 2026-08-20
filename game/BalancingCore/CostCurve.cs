using System;
using BreakInfinity;

namespace BalancingCore
{
    /// <summary>
    /// Kostenkurve für Station-/Upgrade-Käufe. Plan Abschnitt 2: c0 * r^n,
    /// r = 1.07 (früh) bis 1.15 (spät).
    /// </summary>
    public static class CostCurve
    {
        /// <summary>Kosten des n-ten Kaufs (0-basiert: n = 0 ist der erste Kauf).</summary>
        public static BigDouble Cost(BigDouble baseCost, double growthRate, int purchaseIndex)
        {
            if (purchaseIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(purchaseIndex));
            }

            return baseCost * BigDouble.Pow(growthRate, purchaseIndex);
        }

        /// <summary>Summe der Kosten für den Kauf von <paramref name="count"/> Einheiten ab <paramref name="startIndex"/>.</summary>
        public static BigDouble TotalCost(BigDouble baseCost, double growthRate, int startIndex, int count)
        {
            if (startIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            }

            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            // Geometrische Reihe: baseCost * r^startIndex * (r^count - 1) / (r - 1).
            // Für r == 1 (kein Wachstum) degeneriert das zu count * Kosten des ersten Kaufs.
            if (count == 0)
            {
                return BigDouble.Zero;
            }

            var firstCost = Cost(baseCost, growthRate, startIndex);

            if (Math.Abs(growthRate - 1.0) < 1e-12)
            {
                return firstCost * count;
            }

            return firstCost * (BigDouble.Pow(growthRate, count) - 1) / (growthRate - 1);
        }

        /// <summary>
        /// Wie viele zusätzliche Einheiten sind ab <paramref name="startIndex"/> mit <paramref name="budget"/> finanzierbar.
        /// Lineare Suche reicht -- Käufe pro Klick liegen im Idle-Genre im niedrigen zweistelligen Bereich.
        /// </summary>
        public static int MaxAffordable(BigDouble baseCost, double growthRate, int startIndex, BigDouble budget)
        {
            var remaining = budget;
            var count = 0;

            while (true)
            {
                var next = Cost(baseCost, growthRate, startIndex + count);
                if (next > remaining)
                {
                    return count;
                }

                remaining -= next;
                count++;
            }
        }
    }
}
