using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace RestaurantIdle.Game
{
    /// <summary>
    /// Redet mit apps/api (PLAN.md Abschnitt 5) -- Save-Endpunkte und
    /// serverautoritativer Offline-Progress ("Systemuhr-Manipulation ->
    /// Offline-Progress serverseitig", PLAN.md Abschnitt 8). Der WebGL-Build
    /// laeuft same-origin unter cgo-app.de/restaurant/, deshalb reicht das
    /// Browser-Cookie fuer Auth -- kein Bearer-Token noetig (der ist fuer
    /// einen spaeteren nativen Client gedacht, siehe Arbeitsanweisung
    /// Abschnitt 3a "Native Clients ohne Cookie-Jar").
    /// </summary>
    public static class BackendClient
    {
        private const string SaveUrl = "/restaurant/api/save";

        [Serializable]
        private class SaveResponseDto
        {
            public GameState state;
            public string lifetimeRevenue;
            public string prestigeStars;
            public double offlineSeconds;
        }

        public class LoadResult
        {
            public bool Success;
            public GameState State;
            public string LifetimeRevenue;
            public double OfflineSeconds;
        }

        /// <summary>GET /api/save. Bei jedem Fehler (Netzwerk, 401, kaputtes JSON) Success=false -- der Aufrufer faellt dann auf den lokalen Save zurueck.</summary>
        public static IEnumerator Load(Action<LoadResult> onComplete)
        {
            using var request = UnityWebRequest.Get(SaveUrl);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"Backend-Load fehlgeschlagen ({request.responseCode}): {request.error}");
                onComplete(new LoadResult { Success = false });
                yield break;
            }

            SaveResponseDto dto;
            try
            {
                dto = JsonUtility.FromJson<SaveResponseDto>(request.downloadHandler.text);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Backend-Antwort nicht lesbar: {e.Message}");
                onComplete(new LoadResult { Success = false });
                yield break;
            }

            onComplete(new LoadResult
            {
                Success = true,
                State = dto?.state ?? new GameState(),
                LifetimeRevenue = string.IsNullOrEmpty(dto?.lifetimeRevenue) ? "0" : dto.lifetimeRevenue,
                OfflineSeconds = dto?.offlineSeconds ?? 0,
            });
        }

        /// <summary>PUT /api/save. Schlaegt still fehl (nur Log-Warnung) -- ein einzelner verlorener Sync-Versuch ist kein Grund, das Spiel zu unterbrechen, der lokale Save faengt es auf.</summary>
        public static IEnumerator Save(GameState state, string lifetimeRevenue, string prestigeStars)
        {
            var stateJson = JsonUtility.ToJson(state);
            var body = "{\"state\":" + stateJson
                + ",\"lifetimeRevenue\":\"" + Escape(lifetimeRevenue) + "\""
                + ",\"prestigeStars\":\"" + Escape(prestigeStars) + "\"}";
            var bytes = Encoding.UTF8.GetBytes(body);

            using var request = new UnityWebRequest(SaveUrl, "PUT");
            request.uploadHandler = new UploadHandlerRaw(bytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"Backend-Save fehlgeschlagen ({request.responseCode}): {request.error}");
            }
        }

        private static string Escape(string s) => (s ?? "0").Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
