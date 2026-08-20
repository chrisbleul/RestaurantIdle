using System;
using BreakInfinity;

namespace BalancingCore
{
    /// <summary>
    /// Prestige-Ertrag (Michelin-Sterne): k * sqrt(Lifetime-Umsatz) (Plan Abschnitt 2).
    /// k ist ein Tuning-Wert, keine Konstante -- er wird so gewählt, dass ein Reset
    /// nach ~1 Stunde Spielzeit lohnt, und muss im Playtest kalibriert werden.
    /// </summary>
    public static class Prestige
    {
        /// <summary>Gesamte Sterne, die ein Reset bei diesem Lifetime-Umsatz einbringen würde.</summary>
        public static BigDouble TotalStars(BigDouble lifetimeRevenue, double k)
        {
            if (lifetimeRevenue < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(lifetimeRevenue));
            }

            return k * BigDouble.Sqrt(lifetimeRevenue);
        }

        /// <summary>
        /// Sterne, die ein Reset *zusätzlich* zu den bereits gehaltenen einbringt.
        /// Sterne aus früheren Resets bleiben erhalten (Plan: "bleibt über Reset hinweg
        /// erhalten") -- ein Reset lohnt sich nur, wenn dieser Wert positiv ist.
        /// </summary>
        public static BigDouble StarsGainedFromReset(BigDouble lifetimeRevenue, double k, BigDouble currentStars)
        {
            var total = TotalStars(lifetimeRevenue, k);
            var gain = total - currentStars;
            return gain > 0 ? gain : BigDouble.Zero;
        }
    }
}
