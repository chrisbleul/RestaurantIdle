using System;

namespace RestaurantIdle.Game
{
    /// <summary>
    /// Speicherbarer Zustand fuer JsonUtility. Revenue als String statt BigDouble
    /// direkt -- unabhaengig davon, welche internen Felder BigDouble serialisiert,
    /// und im selben Format wie das Backend-Save-Schema (PLAN.md Abschnitt 5:
    /// lifetime_revenue als String/NUMERIC, nicht als Zahl mit begrenzter Praezision).
    /// </summary>
    [Serializable]
    public class GameState
    {
        public string RevenueString = "0";
        public string LifetimeRevenueString = "0";
        public Station Station = new();
        public long LastSavedAtUnixSeconds;
    }
}
