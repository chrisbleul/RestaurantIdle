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
        /// <summary>
        /// PLANv3.md Abschnitt 3/6 Phase A: muss vor jeder Strukturaenderung
        /// an GameState/Station stehen (siehe K1-Umbau in Phase B), sonst
        /// brechen bestehende Spielstaende stillschweigend. 0 bedeutet: Save
        /// stammt von vor der Einfuehrung von SchemaVersion. 1 = Phase A
        /// (SchemaVersion eingefuehrt, Struktur sonst unveraendert). 2 =
        /// Phase B, K1-Umbau: Station.OwnedCount -> PriceLevel/EquipmentLevel.
        /// 3 = Ruf-System (GameState.Reputation/GuestsServed/GuestsLost).
        /// </summary>
        public const int CurrentSchemaVersion = 3;

        private static string SavePath => Path.Combine(Application.persistentDataPath, "save.json");

        public static void Save(GameState state)
        {
            state.LastSavedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            File.WriteAllText(SavePath, JsonUtility.ToJson(state, prettyPrint: true));
        }

        /// <summary>
        /// PLANv3.md Phase F ("Einstellungen: ... Save zuruecksetzen"). Loescht
        /// nur den lokalen Stand -- ein per Backend geladener Spielstand (siehe
        /// GameManager.InitializeGame, Backend hat Vorrang wenn erreichbar)
        /// braucht dafuer eigentlich einen eigenen Loesch-Endpunkt; ausserhalb
        /// des Scopes hier, das Backend ist in der aktuellen Dev-Umgebung
        /// ohnehin nie erreichbar.
        /// </summary>
        public static void DeleteSaveFile()
        {
            if (File.Exists(SavePath))
            {
                File.Delete(SavePath);
            }
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
            Migrate(state);
            EnsureStationSlots(state);
            RescueFromDeadlock(state);
        }

        /// <summary>
        /// Hebt einen Spielstand schrittweise auf CurrentSchemaVersion.
        /// Jede Stufe ist ein eigener if-Block, damit ein Save aus jeder
        /// Vergangenheit ueber alle dazwischenliegenden Versionen laeuft,
        /// nicht nur von der unmittelbar vorherigen.
        /// </summary>
        public static void Migrate(GameState state)
        {
            if (state.SchemaVersion < 1)
            {
                // Reines Stempeln -- SchemaVersion existierte vorher nicht,
                // die Struktur davor entspricht bereits Version 1.
                state.SchemaVersion = 1;
            }

            if (state.SchemaVersion < 2)
            {
                // PLANv3.md K1-Umbau: OwnedCount (Stueckzahl) -> PriceLevel +
                // EquipmentLevel (zwei Upgrade-Achsen, eine Instanz pro
                // Station). Keine exakte Ertrags-Fortsetzung versucht -- die
                // Kostenkurven vorher wie nachher sind Platzhalter (siehe
                // StationCatalog-Kommentar), eine 1:1-Uebernahme der
                // Stueckzahl als Startlevel ist eine ehrliche, monotone
                // Uebersetzung: mehr besessene Einheiten vorher -> hoeheres
                // Level jetzt, kein besessenes Exemplar -> weiterhin
                // gesperrt.
#pragma warning disable CS0618 // Station.OwnedCount ist bewusst nur fuer diese Migration erhalten.
                foreach (var station in state.Stations)
                {
                    if (station.OwnedCount > 0)
                    {
                        station.PriceLevel = station.OwnedCount;
                        station.EquipmentLevel = station.OwnedCount;
                    }

                    station.OwnedCount = 0;
                }
#pragma warning restore CS0618

                state.SchemaVersion = 2;
            }

            if (state.SchemaVersion < 3)
            {
                // Ruf-System eingefuehrt. JsonUtility hat das neue Feld beim
                // Laden auf 0 gesetzt -- das ist der schlechtestmoegliche Ruf
                // und wuerde einen bestehenden Spielstand ohne Zutun des
                // Spielers auf den halben Gaestestrom druecken. Alle
                // Alt-Spielstaende starten deshalb neutral.
                state.Reputation = BalancingCore.Reputation.Start;
                state.SchemaVersion = 3;
            }
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
        /// Ohne mindestens eine freigeschaltete Station bei Umsatz 0 ist
        /// buchstaeblich kein Button jemals leistbar oder klickbar (jede
        /// Station kostet > 0, "Produzieren" braucht IsUnlocked) -- weder fuer
        /// einen frischen Spielstand noch fuer einen aelteren, der (z.B. wegen
        /// dieses selben Fehlers in einer frueheren Version) in genau diesem
        /// Zustand haengengeblieben ist. Deshalb hier statt nur in einem "neues
        /// Spiel"-Pfad: greift bei jedem Laden, nicht nur bei einem leeren
        /// Speicherstand.
        /// </summary>
        private static void RescueFromDeadlock(GameState state)
        {
            var hasAnyStation = state.Stations.Any(s => s.IsUnlocked);
            if (!hasAnyStation && BigDouble.Parse(state.RevenueString) <= BigDouble.Zero && state.Stations.Count > 0)
            {
                state.Stations[0].Unlock();
            }
        }
    }
}
