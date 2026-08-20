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
        public string RevenueString = "0";
        public string LifetimeRevenueString = "0";
        public string PrestigeStarsString = "0";
        public List<Station> Stations = new();
        public int MarketingLevel;
        public long LastSavedAtUnixSeconds;
    }
}
