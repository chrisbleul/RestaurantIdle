using System;

namespace BalancingCore
{
    /// <summary>
    /// Servicequalitaet als eigene Balancing-Achse: bisher war es voellig
    /// egal, WIE SCHNELL ein wartender Gast bedient wurde -- solange es
    /// innerhalb der Geduld passierte, gab es exakt denselben Ertrag. Damit
    /// hatte weder das manuelle Antippen noch ein Ausstattungs-Upgrade eine
    /// spuerbare Wirkung ueber "nicht verlieren" hinaus.
    ///
    /// Trinkgeld schliesst diese Luecke: wer sofort bedient, bekommt bis zu
    /// +50 % auf den Verkauf. Bewusst ein Aufschlag (>= 1.0) statt eines
    /// Abschlags -- ein Malus auf den Grundertrag wuerde die bestehenden
    /// Kostenkurven nach unten verschieben und das Balancing aus PLANv3
    /// Phase H vorwegnehmen.
    /// </summary>
    public static class Service
    {
        /// <summary>Maximaler Trinkgeld-Aufschlag bei sofortiger Bedienung.</summary>
        public const double MaxTipBonus = 0.5;

        /// <param name="waitFraction">
        /// Anteil der Geduld, der beim Servieren bereits verbraucht war
        /// (0 = sofort bedient, 1 = im letzten Moment). Werte ausserhalb
        /// 0..1 werden geklemmt, damit Aufrufer nicht selbst runden muessen.
        /// </param>
        public static double TipMultiplier(double waitFraction) =>
            1.0 + MaxTipBonus * (1.0 - Math.Clamp(waitFraction, 0.0, 1.0));
    }

    /// <summary>
    /// Ruf (PLAN.md Abschnitt 1: "Gaestestrom ... skaliert ueber Marketing/
    /// Ruf") -- die zweite Haelfte dieses Satzes existierte bisher nicht.
    /// Ohne sie war ein unbedient abgewanderter Gast folgenlos: der naechste
    /// kam im selben Takt wieder. Der Ruf macht Ueberlastung zu einem
    /// selbstverstaerkenden Problem und guten Service zu einer Investition,
    /// ohne eine neue Waehrung einzufuehren.
    ///
    /// Skala 0..100 mit Start bei 50, damit der Multiplikator in beide
    /// Richtungen Spielraum hat (0.5x bis 1.5x auf den Gaestestrom).
    /// </summary>
    public static class Reputation
    {
        public const double Min = 0.0;
        public const double Max = 100.0;
        public const double Start = 50.0;

        /// <summary>Gewinn pro bedientem Gast bei perfektem Service (skaliert mit dem Trinkgeld-Anteil).</summary>
        public const double MaxGainPerServedGuest = 1.0;

        /// <summary>
        /// Verlust pro Gast, der unbedient am Platz sitzen bleibt. Groesser
        /// als der Gewinn pro bedientem Gast -- sonst laesst sich eine
        /// dauerhaft ueberlastete Kueche durch schiere Masse "wegbedienen",
        /// und die Kapazitaetsgrenze bliebe wieder folgenlos.
        ///
        /// Erster Live-Testlauf: mit 3.0 war der Ruf nach rund zwei Minuten
        /// unbeaufsichtigtem Laufen auf 0 -- im Fruehspiel gibt es noch
        /// keinen Manager, jeder nicht angetippte Gast schlug voll durch.
        /// 1.5 laesst dieselbe Abwaertsspirale zu, gibt aber Zeit, sie zu
        /// bemerken.
        /// </summary>
        public const double LossPerLostGuest = 1.5;

        /// <summary>
        /// Ein Gast, der das Anstehen aufgibt, wiegt weniger schwer als
        /// einer, der bereits am Platz sass und ignoriert wurde -- er hat
        /// nie eine Bedienung bekommen, die man ihm schuldig geblieben ist.
        /// </summary>
        public const double LossPerAbandonedQueue = 0.75;

        public static double Clamp(double reputation) => Math.Clamp(reputation, Min, Max);

        /// <summary>Faktor auf den Gaestestrom: 0 Ruf -> 0.5x, 50 -> 1.0x, 100 -> 1.5x.</summary>
        public static double FlowMultiplier(double reputation) => 0.5 + Clamp(reputation) / Max;

        /// <param name="tipMultiplier">Ergebnis von <see cref="Service.TipMultiplier"/> -- schneller Service verbessert den Ruf staerker.</param>
        public static double AfterServed(double reputation, double tipMultiplier)
        {
            var quality = Math.Clamp((tipMultiplier - 1.0) / Service.MaxTipBonus, 0.0, 1.0);
            return Clamp(reputation + MaxGainPerServedGuest * (0.25 + 0.75 * quality));
        }

        public static double AfterLost(double reputation) => Clamp(reputation - LossPerLostGuest);

        public static double AfterQueueAbandoned(double reputation) => Clamp(reputation - LossPerAbandonedQueue);
    }
}
