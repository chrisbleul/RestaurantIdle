using System;
using System.IO;
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
                return new GameState();
            }

            try
            {
                return JsonUtility.FromJson<GameState>(File.ReadAllText(SavePath)) ?? new GameState();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Speicherstand beschaedigt, starte neu: {e.Message}");
                return new GameState();
            }
        }
    }
}
