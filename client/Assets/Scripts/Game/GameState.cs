using System;
using System.Collections.Generic;

namespace RestaurantIdle.Game
{
    /// <summary>
    /// Speicherbarer Zustand fuer JsonUtility. Revenue als String statt BigDouble
    /// direkt -- unabhaengig davon, welche internen Felder BigDouble serialisiert,
    /// und im selben Format wie das Backend-Save-Schema (PLAN.md Abschnitt 5).
    /// </summary>
    [Serializable]
    public class GameState
    {
        /// <summary>
        /// PLANv3.md Abschnitt 3 (K3-Befund): ohne Versionsfeld bricht jede
        /// spaetere Strukturaenderung an Station (z.B. der K1-Umbau von
        /// OwnedCount auf PriceLevel/EquipmentLevel) bestehende Spielstaende
        /// stillschweigend. JsonUtility setzt fehlende Felder in alten
        /// Saves automatisch auf 0 (C#-Default) -- das ist deshalb bewusst
        /// die "unversioniert/Legacy"-Markierung, nicht 1. Migration siehe
        /// SaveSystem.Migrate().
        /// </summary>
        public int SchemaVersion = SaveSystem.CurrentSchemaVersion;

        public string RevenueString = "0";
        public string LifetimeRevenueString = "0";
        public string PrestigeStarsString = "0";
        public List<Station> Stations = new();
        public int MarketingLevel;
        public long LastSavedAtUnixSeconds;

        /// <summary>
        /// PLANv2.md Abschnitt 1.1/6/10: Location-Index (0 = Limonadenstand,
        /// bis 4 = Restaurant). Steigt bei jeder Renovierung, gedeckelt bei
        /// der letzten Stufe -- Renovierungspunkte laufen darueber hinaus
        /// weiter, aber es gibt keinen sichtbaren Ortswechsel mehr.
        /// </summary>
        public int CurrentLocation;
    }
}
