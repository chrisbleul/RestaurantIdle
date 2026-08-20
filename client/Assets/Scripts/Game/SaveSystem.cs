using System;
using System.IO;
using System.Linq;
using BalancingCore;
using BreakInfinity;
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
            GameState state;

            if (!File.Exists(SavePath))
            {
                state = new GameState();
            }
            else
            {
                try
                {
                    state = JsonUtility.FromJson<GameState>(File.ReadAllText(SavePath)) ?? new GameState();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Speicherstand beschaedigt, starte neu: {e.Message}");
                    state = new GameState();
                }
            }

            Normalize(state);
            return state;
        }

        /// <summary>
        /// Fehlende Stationen auffuellen + Deadlock-Rettung -- gilt fuer jeden
        /// Spielstand unabhaengig von der Quelle (lokal oder vom Backend
        /// geladen), deshalb oeffentlich statt nur intern in LoadOrCreate.
        /// </summary>
        public static void Normalize(GameState state)
        {
            EnsureStationSlots(state);
            RescueFromDeadlock(state);
        }

        /// <summary>Fuellt fehlende Stationen mit leeren Instanzen auf -- betrifft neue Spielstaende und alte, falls der Katalog waechst.</summary>
        private static void EnsureStationSlots(GameState state)
        {
            while (state.Stations.Count < StationCatalog.All.Count)
            {
                state.Stations.Add(new Station());
            }
        }

        /// <summary>
        /// Ohne mindestens eine besessene Station bei Umsatz 0 ist buchstaeblich
        /// kein Button jemals leistbar oder klickbar (jede Station kostet > 0,
        /// "Produzieren" braucht OwnedCount > 0) -- weder fuer einen frischen
        /// Spielstand noch fuer einen aelteren, der (z.B. wegen dieses selben
        /// Fehlers in einer frueheren Version) in genau diesem Zustand
        /// haengengeblieben ist. Deshalb hier statt nur in einem "neues Spiel"-
        /// Pfad: greift bei jedem Laden, nicht nur bei einem leeren Speicherstand.
        /// </summary>
        private static void RescueFromDeadlock(GameState state)
        {
            var hasAnyStation = state.Stations.Any(s => s.OwnedCount > 0);
            if (!hasAnyStation && BigDouble.Parse(state.RevenueString) <= BigDouble.Zero && state.Stations.Count > 0)
            {
                state.Stations[0].OwnedCount = 1;
            }
        }
    }
}
