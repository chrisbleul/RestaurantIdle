using System;
using BreakInfinity;

namespace RestaurantIdle.Game
{
    /// <summary>
    /// Lesbare Kurzform fuer grosse Zahlen (PLANv2.md Abschnitt 11:
    /// "Zahlenformat: Schwelle fuer wissenschaftliche Notation festlegen").
    /// BigDouble.ToString() liefert unter 10^21 volle double-Praezision
    /// (z.B. "14.025517307") -- das hier rundet auf 2 Nachkommastellen und
    /// haengt ab 1000 ein Suffix an (K/M/B/T/...). Ab 10^21 greift
    /// BreakInfinity's eigene wissenschaftliche Notation unveraendert.
    /// </summary>
    public static class NumberFormat
    {
        private static readonly string[] Suffixes =
        {
            "", "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No",
        };

        public static string Format(BigDouble value)
        {
            if (value.Exponent >= 21 || value.Exponent <= -7)
            {
                return value.ToString();
            }

            if (BigDouble.Abs(value) < 1000)
            {
                return value.ToString("F2");
            }

            var magnitude = Math.Min((int)(value.Exponent / 3), Suffixes.Length - 1);
            var scaled = value / BigDouble.Pow10(magnitude * 3);
            return $"{scaled.ToString("F2")}{Suffixes[magnitude]}";
        }
    }
}
