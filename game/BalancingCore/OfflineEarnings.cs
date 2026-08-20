using System;
using BreakInfinity;

namespace BalancingCore
{
    /// <summary>
    /// Offline-Ertrag: 100 % für 2 Std., danach 50 %, Cap bei 24 Std. (Plan Abschnitt 2).
    /// Diese Klasse ist reine Berechnung -- die Autorität über die verstrichene Zeit
    /// liegt serverseitig (Plan Abschnitt 5, last_seen_at), damit niemand die
    /// Systemuhr vordreht. Client-seitig dient sie nur der Vorschau vor dem Login.
    /// </summary>
    public static class OfflineEarnings
    {
        public static readonly TimeSpan FullRateDuration = TimeSpan.FromHours(2);
        public static readonly TimeSpan Cap = TimeSpan.FromHours(24);
        public const double ReducedRate = 0.5;

        /// <summary>Ertrag für <paramref name="offlineDuration"/> bei einem Einkommen von <paramref name="incomePerSecond"/>/s.</summary>
        public static BigDouble Calculate(BigDouble incomePerSecond, TimeSpan offlineDuration)
        {
            if (offlineDuration < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(offlineDuration));
            }

            var cappedSeconds = Math.Min(offlineDuration.TotalSeconds, Cap.TotalSeconds);
            var fullRateSeconds = Math.Min(cappedSeconds, FullRateDuration.TotalSeconds);
            var reducedRateSeconds = cappedSeconds - fullRateSeconds;

            return incomePerSecond * fullRateSeconds + incomePerSecond * reducedRateSeconds * ReducedRate;
        }
    }
}
