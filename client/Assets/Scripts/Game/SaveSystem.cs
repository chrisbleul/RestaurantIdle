using System;
using System.IO;
using BalancingCore;
using UnityEngine;

namespace RestaurantIdle.Game
{
    /// <summary>
    /// Lokaler Save fuer den grauen Prototyp (Phase 2). Der serverautoritative
    /// Offline-Progress aus PLAN.md Abschnitt 5 (last_seen_at, Backend rechnet)
    /// kommt erst mit der Backend-Anbindung in einer spaeteren Phase -- bis dahin
    /// ist ein lokaler Timestamp die ehrliche Grenze dessen, was Phase 2 braucht.
    /// </summary>
    public static class SaveSystem
    {
        private static string SavePath => Path.Combine(Application.persistentDataPath, "save.json");

        public static void Save(GameState state)
        {
            state.LastSavedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            File.WriteAllText(SavePath, JsonUtility.ToJson(state, prettyPrint: true));
        }

        public static GameState LoadOrCreate()
        {
            if (!File.Exists(SavePath))
            {
                return NewGame();
            }

            try
            {
                var state = JsonUtility.FromJson<GameState>(File.ReadAllText(SavePath)) ?? NewGame();
                EnsureStationSlots(state);
                return state;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Speicherstand beschaedigt, starte neu: {e.Message}");
                return NewGame();
            }
        }

        private static GameState NewGame()
        {
            var state = new GameState();
            EnsureStationSlots(state);

            // Ohne das gibt es fuer einen frischen Spielstand keinen Einstieg:
            // Umsatz startet bei 0, aber jede Station kostet > 0 -- ohne eine
            // kostenlose erste Station waere buchstaeblich kein Button jemals
            // leistbar/klickbar. Die erste Station (Kaffeemaschine) startet
            // deshalb bereits besessen, "Produzieren" (manuelles Antippen)
            // liefert von da an den ersten Umsatz.
            if (state.Stations.Count > 0)
            {
                state.Stations[0].OwnedCount = 1;
            }

            return state;
        }

        /// <summary>Fuellt fehlende Stationen mit leeren Instanzen auf -- betrifft neue Spielstaende und alte, falls der Katalog waechst.</summary>
        private static void EnsureStationSlots(GameState state)
        {
            while (state.Stations.Count < StationCatalog.All.Count)
            {
                state.Stations.Add(new Station());
            }
        }
    }
}
