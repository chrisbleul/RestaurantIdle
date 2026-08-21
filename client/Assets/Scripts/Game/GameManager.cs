using System;
using System.Collections;
using System.Collections.Generic;
using BalancingCore;
using BreakInfinity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RestaurantIdle.Game
{
    /// <summary>
    /// Phase 3 (PLAN.md Abschnitt 7): alle sieben Stationen, Manager
    /// (Automatisierung), Marketing/Gaestestrom. Baut sein UI weiterhin zur
    /// Laufzeit selbst auf (kein Art-Pass vor Phase 4).
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        private const float BackendSyncIntervalSeconds = 30f;

        // Platzhalter -- PLAN.md Abschnitt 2: "k so waehlen, dass Reset nach
        // ~1 Std. lohnt", muss im Playtest kalibriert werden (siehe Prestige.cs).
        private const double PrestigeK = 1.0;

        // PLANv3.md K3: Renovierungspunkte wurden berechnet/angezeigt, aber
        // nirgends als Multiplikator verwendet -- ein Reset war reiner
        // Verlust. 2%/Punkt ist ein Platzhalter (wie PrestigeK), muss im
        // Playtest kalibriert werden.
        private const double PrestigeMultiplierPerStar = 0.02;

        // Icons aus dem Kenney Food Kit (CC0, Assets/Resources/Icons/) --
        // Reihenfolge muss zu StationCatalog.All passen (Kaffeemaschine,
        // Fritteuse, Grill, Pizzaofen, Sushi-Bar, Patisserie, Chef's Table).
        private static readonly string[] StationIconNames =
        {
            "station-kaffeemaschine",
            "station-fritteuse",
            "station-grill",
            "station-pizzaofen",
            "station-sushibar",
            "station-patisserie",
            "station-chefstable",
        };

        private GameState state;
        private BigDouble revenue;
        private BigDouble lifetimeRevenue;
        private BigDouble prestigeStars;
        private float timeSinceLastSync;
        private float guestSpawnTimer;

        /// <summary>
        /// PLANv3.md A5: RefreshUi() jeden Frame aus dem passiven Tick-Pfad
        /// aufzurufen erzeugt auf Mobile/WebGL unnoetigen GC-Druck (Text.text-
        /// Zuweisung alloziert). 10Hz reicht fuer eine Zahl, die sich nur
        /// kontinuierlich hochzaehlt -- diskrete Nutzeraktionen (Kauf, Tap,
        /// Renovieren) rufen RefreshUi() weiterhin direkt und sofort auf.
        /// </summary>
        private const float UiRefreshIntervalSeconds = 0.1f;
        private float uiRefreshTimer;
        // Weltposition pro Station, fuer den Muenz-Burst bei UI-Button-Klicks
        // (dort gibt es keinen Raycast-Treffpunkt wie beim 3D-Tap). Einmalig
        // aus den StationHotspot-Objekten in der Szene aufgebaut.
        private readonly Dictionary<int, Vector3> stationWorldPositions = new();

        /// <summary>
        /// PLANv3.md Abschnitt 4 ("Alle Stationen sofort sichtbar -> keine
        /// Entdeckung, keine Belohnung fuers Weiterspielen"): Station i>=1
        /// bleibt unsichtbar (SetActive false, damit auch Collider/Tap aus
        /// sind), bis Station i-1 freigeschaltet ist -- macht jede neue
        /// Station zu einem Ereignis statt einer von sieben gleichzeitig
        /// sichtbaren Listenzeilen.
        /// </summary>
        private readonly Dictionary<int, GameObject> stationGameObjects = new();

        /// <summary>
        /// PLANv3.md K2-Umbau: Geld entsteht ausschliesslich beim Servieren
        /// eines echten Gastes -- diese Zuordnung IST die Theke jeder
        /// Station. Hoechstens ein Eintrag pro Stations-Index (kein
        /// gleichzeitiges Bedienen mehrerer Gaeste an derselben Station ohne
        /// echtes Raumlayout/Warteschlange, siehe PLANv3 Phase C/E). Ein
        /// Eintrag existiert bereits ab dem Spawn (Reservierung), nicht erst
        /// ab Ankunft -- sonst koennten zwei Gaeste gleichzeitig zur selben
        /// freien Station laufen.
        /// </summary>
        private class GuestVisit
        {
            public GuestMover Mover;
            public float PatienceRemaining;

            /// <summary>PLANv3.md Abschnitt 4: Dauer-Dampfeffekt als Bedient-Signal, siehe SteamEffect. Null bis der Gast ankommt, danach bis Visit-Ende aktiv.</summary>
            public GameObject SteamEffect;
        }

        // PLANv3.md K2: Geduld muss mindestens eine volle Zykluszeit der
        // Zielstation abdecken (+ Puffer) -- sonst wuerden Gaeste an
        // langsamen Stationen (z.B. Chef's Table: 900s Basis-Zyklus) JEDES
        // Mal gehen, bevor ein Manager ueberhaupt fertig servieren kann.
        // GuestPatienceSeconds bleibt die Untergrenze fuer schnelle
        // Stationen (Kaffeemaschine: 2s), damit dort trotzdem genug Zeit
        // fuer einen manuellen Tap bleibt.
        private const float GuestPatienceSeconds = 12f;
        private const float GuestPatienceBufferSeconds = 3f;
        private readonly Dictionary<int, GuestVisit> guestAtStation = new();

        // InitializeGame() laedt asynchron (Backend-Request ueber mehrere Frames)
        // -- Update() etc. laufen aber schon ab dem ersten Frame und wuerden ohne
        // dieses Flag mit NullReferenceException auf "state" abstuerzen, bevor
        // InitializeGame() ueberhaupt fertig ist.
        private bool isInitialized;

        private Text headerLabel;
        private Button marketingButtonRef;
        private Text prestigeLabel;
        private Button prestigeButtonRef;
        private readonly List<StationRow> rows = new();

        private struct StationRow
        {
            public Text Label;
            public Button BuyButton;
            public Image BuyButtonImage;
            public Text BuyButtonLabel;
            public Button EquipButton;
            public Text EquipButtonLabel;
            public Button ManagerButton;
        }

        // PLANv3.md Phase D ("Kein Tutorial, kein geführter erster Kauf"):
        // der allererste Kauf im Spiel bekommt eine auffaellige Farbe statt
        // im grauen Einheitsbrei unterzugehen -- billigste Form von
        // Fuehrung ohne echtes Tutorial-System.
        private static readonly Color GuidedButtonColor = new Color(0.98f, 0.75f, 0.25f);
        private static readonly Color DefaultButtonColor = new Color(0.8f, 0.8f, 0.8f);

        private void Start()
        {
            StartCoroutine(InitializeGame());
        }

        /// <summary>
        /// Backend zuerst versuchen (PLAN.md Abschnitt 5) -- nur wenn es dort
        /// wirklich einen Spielstand gibt (Stations.Count > 0) wird er
        /// massgeblich. Sonst lokalen Stand nehmen (neuer Account, oder
        /// Backend gerade nicht erreichbar) und offline lokal berechnen --
        /// ein bewusst schwaecherer, aber weiterhin funktionierender
        /// Rueckfallpfad (siehe ApplyLocalOfflineEarnings).
        /// </summary>
        private IEnumerator InitializeGame()
        {
            BackendClient.LoadResult backendResult = null;
            yield return BackendClient.Load(r => backendResult = r);

            // Kein erweitertes Property-Pattern (State.Stations.Count: > 0) --
            // das ist C# 10, Unity kompiliert dieses Projekt mit C# 9.0.
            var hasBackendSave = backendResult != null && backendResult.Success
                && backendResult.State != null && backendResult.State.Stations != null
                && backendResult.State.Stations.Count > 0;

            if (hasBackendSave)
            {
                state = backendResult.State;
                SaveSystem.Normalize(state);
                revenue = BigDouble.Parse(state.RevenueString);
                lifetimeRevenue = BigDouble.Parse(backendResult.LifetimeRevenue);
                prestigeStars = BigDouble.Parse(backendResult.PrestigeStars);
                ApplyOfflineEarnings(TimeSpan.FromSeconds(backendResult.OfflineSeconds));
            }
            else
            {
                state = SaveSystem.LoadOrCreate();
                revenue = BigDouble.Parse(state.RevenueString);
                lifetimeRevenue = BigDouble.Parse(state.LifetimeRevenueString);
                prestigeStars = BigDouble.Parse(state.PrestigeStarsString);
                ApplyLocalOfflineEarnings();
            }

            BuildUi();
            ApplyLocationTheme();

            foreach (var hotspot in FindObjectsByType<StationHotspot>(FindObjectsSortMode.None))
            {
                stationWorldPositions[hotspot.StationIndex] = hotspot.transform.position;
                stationGameObjects[hotspot.StationIndex] = hotspot.gameObject;
            }

            RevealStationsAsNeeded();

            for (var i = 0; i < state.Stations.Count; i++)
            {
                if (state.Stations[i].HasManager)
                {
                    SpawnStaffWorker(i);
                }
            }

            RefreshUi();

            // Deckt insbesondere den Umzug eines bisher rein lokalen
            // Spielstands aufs Backend ab (erster Start nach dieser Aenderung).
            Persist();

            isInitialized = true;
        }

        /// <summary>Theoretische Produktionskapazitaet, wenn jede Station mit Manager ununterbrochen bedient wuerde -- nur noch fuer die Offline-Naeherung (OfflineCapacityFactor) relevant, siehe PLANv3 K2.</summary>
        private BigDouble SumManagedYieldPerSecond()
        {
            var total = BigDouble.Zero;
            for (var i = 0; i < state.Stations.Count; i++)
            {
                if (state.Stations[i].HasManager)
                {
                    total += state.Stations[i].YieldPerSecond(StationCatalog.All[i]);
                }
            }

            return total;
        }

        /// <summary>
        /// PLANv3.md K2: im Live-Betrieb entsteht Geld jetzt ausschliesslich
        /// beim Servieren eines echten GuestMover-Objekts (siehe
        /// UpdateGuestVisits/ProduceNow) -- GuestFlow.CapacityFactor spielt
        /// dort keine Rolle mehr. Fuer Offline-Ertrag (ApplyOfflineEarnings)
        /// ist eine echte Gast-fuer-Gast-Simulation ueber Stunden hinweg nicht
        /// praktikabel; dort bleibt CapacityFactor als aggregierte Schaetzung
        /// (Produktionskapazitaet vs. Gaestestrom) die ehrliche Naeherung.
        /// </summary>
        private double OfflineCapacityFactor() =>
            GuestFlow.CapacityFactor(SumManagedYieldPerSecond(), GuestFlow.GuestFlowAt(state.MarketingLevel));

        /// <summary>
        /// PLANv3.md K3-Fix: globaler Ertragsmultiplikator aus den
        /// Renovierungspunkten -- macht "Renovieren" zum ersten Mal zu
        /// einem echten Gewinn statt nur einem Reset mit wirkungsloser
        /// Anzeigezahl.
        /// </summary>
        private double PrestigeMultiplier() => 1.0 + prestigeStars.ToDouble() * PrestigeMultiplierPerStar;

        /// <summary>Serverautoritative Variante -- offlineDuration kommt vom Backend, nicht von der lokalen Systemuhr (PLAN.md Abschnitt 8).</summary>
        private void ApplyOfflineEarnings(TimeSpan offlineDuration)
        {
            if (offlineDuration <= TimeSpan.Zero)
            {
                return;
            }

            var effectivePerSecond = SumManagedYieldPerSecond() * OfflineCapacityFactor() * PrestigeMultiplier();
            var earned = OfflineEarnings.Calculate(effectivePerSecond, offlineDuration);
            if (earned > BigDouble.Zero)
            {
                revenue += earned;
                lifetimeRevenue += earned;
                Debug.Log($"Offline-Ertrag ({offlineDuration.TotalMinutes:F0} Min.): {earned}");
            }
        }

        /// <summary>
        /// Rueckfallpfad, nur wenn kein Backend-Save existiert oder das
        /// Backend nicht erreichbar ist -- verlaesst sich auf die lokale
        /// Systemuhr und ist deshalb bewusst schwaecher gegen Manipulation
        /// als der Server-Pfad (PLAN.md Abschnitt 8), aber besser als gar
        /// kein Offline-Ertrag.
        /// </summary>
        private void ApplyLocalOfflineEarnings()
        {
            if (state.LastSavedAtUnixSeconds == 0)
            {
                return;
            }

            var lastSeen = DateTimeOffset.FromUnixTimeSeconds(state.LastSavedAtUnixSeconds);
            ApplyOfflineEarnings(DateTimeOffset.UtcNow - lastSeen);
        }

        private void Update()
        {
            if (!isInitialized)
            {
                return;
            }

            HandleStationTap();
            UpdateGuestSpawner();
            UpdateGuestVisits();

            uiRefreshTimer += Time.deltaTime;
            if (uiRefreshTimer >= UiRefreshIntervalSeconds)
            {
                uiRefreshTimer = 0f;
                RefreshUi();
            }

            timeSinceLastSync += Time.deltaTime;
            if (timeSinceLastSync >= BackendSyncIntervalSeconds)
            {
                timeSinceLastSync = 0f;
                Persist();
            }
        }

        /// <summary>
        /// PLANv3.md K2: Kernstueck der Auftragskette. Fuer jede Station mit
        /// wartendem Gast entweder automatisch bedienen (Manager -> Tick())
        /// oder die Geduld herunterzaehlen, bis entweder ein manueller Tap
        /// (ProduceNow) den Gast bedient oder die Geduld ablaeuft (Gast geht
        /// unbedient). Ohne wartenden Gast passiert an einer Station schlicht
        /// nichts -- das ist der ganze Punkt von K2.
        /// </summary>
        private void UpdateGuestVisits()
        {
            List<int> finished = null;

            foreach (var kvp in guestAtStation)
            {
                var stationIndex = kvp.Key;
                var visit = kvp.Value;

                if (visit.Mover == null)
                {
                    (finished ??= new List<int>()).Add(stationIndex);
                    continue;
                }

                if (!visit.Mover.HasArrivedAtStation)
                {
                    continue; // noch unterwegs -- Geduld laeuft erst ab Ankunft an der Station.
                }

                if (visit.SteamEffect == null && stationWorldPositions.TryGetValue(stationIndex, out var steamPosition))
                {
                    visit.SteamEffect = SteamEffect.SpawnLoopingAt(steamPosition);
                }

                var station = state.Stations[stationIndex];
                var def = StationCatalog.All[stationIndex];

                if (station.HasManager)
                {
                    var earned = station.Tick(def, Time.deltaTime);
                    if (earned > BigDouble.Zero)
                    {
                        var effective = earned * PrestigeMultiplier();
                        revenue += effective;
                        lifetimeRevenue += effective;

                        visit.Mover.Leave();
                        (finished ??= new List<int>()).Add(stationIndex);

                        if (stationWorldPositions.TryGetValue(stationIndex, out var burstPos))
                        {
                            CoinBurst.SpawnAt(burstPos);
                        }

                        continue;
                    }
                }

                visit.PatienceRemaining -= Time.deltaTime;
                if (visit.PatienceRemaining <= 0f)
                {
                    visit.Mover.Leave();
                    (finished ??= new List<int>()).Add(stationIndex);
                }
            }

            if (finished == null)
            {
                return;
            }

            foreach (var stationIndex in finished)
            {
                if (guestAtStation.TryGetValue(stationIndex, out var visit) && visit.SteamEffect != null)
                {
                    Destroy(visit.SteamEffect);
                }

                guestAtStation.Remove(stationIndex);
            }
        }

        /// <summary>
        /// Tap-Layer (PLANv2.md Abschnitt 1.3): Klick auf eine Station in der
        /// 3D-Szene loest ProduceNow aus -- der einzige Weg, manuell zu
        /// produzieren (der separate "Produzieren"-Listenbutton wurde
        /// entfernt, sobald der Tap-Layer stand: redundant, "Kaufen" und
        /// "Manager" bleiben als Listenbuttons, dafuer gibt es keine
        /// Tap-Geste). EventSystem-Check zuerst, damit Klicks auf die UI
        /// (untere Bildschirmhaelfte) nicht zusaetzlich einen 3D-Raycast
        /// auf die Szene ausloesen.
        /// </summary>
        /// <summary>
        /// PLANv3.md K3-Nachbarbefund: IsPointerOverGameObject() ohne
        /// fingerId liefert auf Touch-Geraeten den (nicht vorhandenen)
        /// Maus-Status -- auf echten Geraeten (iOS) schlagen UI-Taps dann
        /// zusaetzlich als 3D-Raycast durch die Szene durch. Touch- und
        /// Maus-Pfad deshalb strikt getrennt statt Input.mousePosition auf
        /// Touch-Geraeten "mitlaufen" zu lassen.
        /// </summary>
        private void HandleStationTap()
        {
            bool tapped;
            bool overUi;
            Vector2 screenPosition;

            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                tapped = touch.phase == TouchPhase.Began;
                screenPosition = touch.position;
                overUi = EventSystem.current.IsPointerOverGameObject(touch.fingerId);
            }
            else
            {
                tapped = Input.GetMouseButtonDown(0);
                screenPosition = Input.mousePosition;
                overUi = EventSystem.current.IsPointerOverGameObject();
            }

            if (!tapped || overUi || Camera.main == null)
            {
                return;
            }

            var ray = Camera.main.ScreenPointToRay(screenPosition);
            if (Physics.Raycast(ray, out var hit) && hit.collider.TryGetComponent<StationHotspot>(out var hotspot))
            {
                ProduceNow(hotspot.StationIndex, hit.point);
            }
        }

        // PLANv2.md Abschnitt 9: "Gast-Spawn-Rate an den echten Wert
        // koppeln, nicht als Deko animieren." GuestFlowAt() ist eine
        // abstrakte Balancing-Groesse (Basis 10, waechst mit Marketing),
        // keine woertliche "Gaeste pro Sekunde" -- 60/Wert mit Clamp bildet
        // das auf eine optisch sinnvolle Spawn-Frequenz ab.
        private const float GuestSpawnRateNumerator = 60f;
        private const float GuestSpawnMinInterval = 1.5f;
        private const float GuestSpawnMaxInterval = 8f;

        // Kenney-Sprite ist 96x128px bei 100 PPU (Unity-Default) = 0.96 x
        // 1.28 Weltmasse nativ -- deutlich zu gross fuer diese Szene (siehe
        // FurnitureScale in CIBuild.cs, gleiche Groessenordnung wie dort).
        // 0.55 ergibt eine Sprite-Hoehe von ~0.7, aehnlich der vorherigen
        // Kapsel (0.25/0.4/0.25 -> Hoehe 0.8).
        private const float GuestSpriteScale = 0.55f;
        private static readonly Vector3 GuestEntrance = new Vector3(-1.5f, 0.4f, -1.2f);
        private static readonly Vector3 GuestExit = new Vector3(7f, 0.4f, -1.2f);

        private void UpdateGuestSpawner()
        {
            var guestFlow = GuestFlow.GuestFlowAt(state.MarketingLevel).ToDouble();
            var interval = guestFlow > 0
                ? Mathf.Clamp((float)(GuestSpawnRateNumerator / guestFlow), GuestSpawnMinInterval, GuestSpawnMaxInterval)
                : GuestSpawnMaxInterval;

            guestSpawnTimer += Time.deltaTime;
            if (guestSpawnTimer < interval)
            {
                return;
            }

            guestSpawnTimer = 0f;
            SpawnGuest();
        }

        /// <summary>
        /// PLANv3.md K2: Ziel ist jetzt eine freigeschaltete Station OHNE
        /// aktuell wartenden Gast -- ein Gast reserviert seinen Zielplatz
        /// bereits beim Spawn (nicht erst bei Ankunft), sonst koennten zwei
        /// Gaeste gleichzeitig zur selben freien Station loslaufen. Findet
        /// sich keine freie Station (keine freigeschaltet, oder alle
        /// belegt), dreht der Gast sichtbar am Eingang ab, statt unsichtbar
        /// verworfen zu werden -- macht die emergente Kapazitaetsgrenze
        /// ("Schlange wird zu lang -> Gaeste gehen") sichtbar, ohne eine
        /// echte Warteschlangen-Visualisierung (Raumlayout, PLANv3 Phase E)
        /// zu brauchen.
        /// </summary>
        private void SpawnGuest()
        {
            var stationIndex = PickAvailableStationIndex();

            // PLANv3.md Abschnitt 5: Kenney-Toon-Character-Sprite statt
            // eingefaerbter Kapsel. Kein Collider noetig -- anders als die
            // Kapsel vorher blockiert ein SpriteRenderer ohne Collider von
            // Natur aus keinen Raycast.
            var guest = new GameObject("Guest", typeof(SpriteRenderer));
            var spriteRenderer = guest.GetComponent<SpriteRenderer>();
            spriteRenderer.sprite = Resources.Load<Sprite>("Characters/guest-idle");
            guest.transform.localScale = Vector3.one * GuestSpriteScale;
            if (Camera.main != null)
            {
                // Billboard: Sprite-Ebene richtet sich einmalig nach der
                // (fest stehenden) Kamera aus, kein Nachfuehren pro Frame
                // noetig -- die Kamera bewegt sich nirgends im Spiel.
                guest.transform.rotation = Camera.main.transform.rotation;
            }

            var mover = guest.AddComponent<GuestMover>();
            guest.AddComponent<GuestSpriteAnimator>();

            if (stationIndex.HasValue && stationWorldPositions.TryGetValue(stationIndex.Value, out var targetPosition))
            {
                mover.Init(GuestEntrance, targetPosition, GuestExit, waitsForService: true);
                var station = state.Stations[stationIndex.Value];
                var cycleSeconds = (float)station.CycleSeconds(StationCatalog.All[stationIndex.Value]);
                var patience = Mathf.Max(GuestPatienceSeconds, cycleSeconds + GuestPatienceBufferSeconds);
                guestAtStation[stationIndex.Value] = new GuestVisit { Mover = mover, PatienceRemaining = patience };
            }
            else
            {
                var bouncePoint = Vector3.Lerp(GuestEntrance, GuestExit, 0.15f);
                mover.Init(GuestEntrance, bouncePoint, GuestExit, waitsForService: false);
            }
        }

        private int? PickAvailableStationIndex()
        {
            var candidates = new List<int>();
            for (var i = 0; i < state.Stations.Count; i++)
            {
                if (state.Stations[i].IsUnlocked && !guestAtStation.ContainsKey(i) && stationWorldPositions.ContainsKey(i))
                {
                    candidates.Add(i);
                }
            }

            return candidates.Count == 0 ? null : candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }

        /// <summary>
        /// PLANv3.md Phase D: Station 0 ist immer sichtbar, jede weitere
        /// erst, sobald die vorherige freigeschaltet ist. Wird beim Laden
        /// und nach jedem erfolgreichen Freischalten aufgerufen -- billig
        /// genug (7 Stationen, kein Hot-Path) fuer ein simples Neu-Setzen
        /// statt Delta-Tracking.
        /// </summary>
        private void RevealStationsAsNeeded()
        {
            for (var i = 0; i < state.Stations.Count; i++)
            {
                if (!stationGameObjects.TryGetValue(i, out var go))
                {
                    continue;
                }

                var shouldBeVisible = i == 0 || state.Stations[i - 1].IsUnlocked;
                if (go.activeSelf != shouldBeVisible)
                {
                    go.SetActive(shouldBeVisible);
                }
            }
        }

        private void OnApplicationQuit()
        {
            if (isInitialized)
            {
                PersistLocal();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && isInitialized)
            {
                Persist();
            }
        }

        /// <summary>Lokal (sofort, synchron) UND ans Backend (asynchron) -- der lokale Save faengt einen fehlgeschlagenen/verzoegerten Sync auf.</summary>
        private void Persist()
        {
            PersistLocal();
            StartCoroutine(BackendClient.Save(state, lifetimeRevenue.ToString(), prestigeStars.ToString()));
        }

        private void PersistLocal()
        {
            state.RevenueString = revenue.ToString();
            state.LifetimeRevenueString = lifetimeRevenue.ToString();
            state.PrestigeStarsString = prestigeStars.ToString();
            SaveSystem.Save(state);
        }

        // Kenney Interface Sounds (CC0, Assets/Resources/Audio). PlayClipAtPoint
        // statt eines persistenten AudioSource-Components -- braucht keine
        // Instanzreferenz, passt deshalb auch in statische Methoden wie
        // CreateButton, und raeumt sich selbst auf.
        private static void PlaySfx(string resourceName)
        {
            var clip = Resources.Load<AudioClip>($"Audio/{resourceName}");
            if (clip != null)
            {
                AudioSource.PlayClipAtPoint(clip, Vector3.zero);
            }
        }

        /// <summary>
        /// PLANv3.md K2: manuelles Antippen bezahlt nur noch aus, wenn hier
        /// tatsaechlich ein Gast wartet -- vorher war das ein von Gaesten
        /// komplett unabhaengiger Geld-Button.
        /// </summary>
        private void ProduceNow(int i, Vector3? burstPosition = null)
        {
            if (!guestAtStation.TryGetValue(i, out var visit) || visit.Mover == null || !visit.Mover.HasArrivedAtStation)
            {
                return;
            }

            var earned = state.Stations[i].ProduceNow(StationCatalog.All[i]);
            if (earned <= BigDouble.Zero)
            {
                return;
            }

            var effective = earned * PrestigeMultiplier();
            revenue += effective;
            lifetimeRevenue += effective;
            RefreshUi();
            FlashHeader();
            PlaySfx("sfx-produce");

            visit.Mover.Leave();
            if (visit.SteamEffect != null)
            {
                Destroy(visit.SteamEffect);
            }

            guestAtStation.Remove(i);

            var position = burstPosition ?? (stationWorldPositions.TryGetValue(i, out var pos) ? pos : (Vector3?)null);
            if (position.HasValue)
            {
                CoinBurst.SpawnAt(position.Value);
            }
        }

        /// <summary>
        /// PLANv3.md K1: eine Station wird einmal freigeschaltet (Unlock),
        /// danach kauft dieser Button die Preis-Achse hoch (mehr Ertrag/
        /// Verkauf). Die Ausstattungs-Achse (Zyklusgeschwindigkeit) hat einen
        /// eigenen Button, siehe UpgradeEquipment.
        /// </summary>
        private void BuyStation(int i)
        {
            var station = state.Stations[i];
            var def = StationCatalog.All[i];
            var cost = station.IsUnlocked ? station.NextPriceUpgradeCost(def) : station.UnlockCost(def);
            if (revenue < cost)
            {
                return;
            }

            revenue -= cost;
            if (station.IsUnlocked)
            {
                station.UpgradePrice();
                PlayMilestoneEffectIfReached(i, station.PriceLevel);
            }
            else
            {
                station.Unlock();
                RevealStationsAsNeeded();
            }

            RefreshUi();
            FlashHeader();
            PlaySfx("sfx-purchase");
        }

        private void UpgradeEquipment(int i)
        {
            var station = state.Stations[i];
            var def = StationCatalog.All[i];
            if (!station.IsUnlocked)
            {
                return;
            }

            var cost = station.NextEquipmentUpgradeCost(def);
            if (revenue < cost)
            {
                return;
            }

            revenue -= cost;
            station.UpgradeEquipment();
            PlayMilestoneEffectIfReached(i, station.EquipmentLevel);
            RefreshUi();
            FlashHeader();
            PlaySfx("sfx-purchase");
        }

        /// <summary>
        /// PLANv2.md Abschnitt 8/11: groesserer Partikel-Burst + eigener
        /// Sound, wenn Preis- oder Ausstattungs-Level gerade eine der
        /// Milestones.DefaultThresholds-Schwellen (10/25/50) erreicht hat --
        /// dieselbe Schwelle, die BalancingCore.Milestones fuer den
        /// Ertrags-Multiplikator verwendet (siehe Station.YieldPerSale).
        /// </summary>
        private void PlayMilestoneEffectIfReached(int stationIndex, int newLevel)
        {
            if (Array.IndexOf(Milestones.DefaultThresholds, newLevel) < 0)
            {
                return;
            }

            if (stationWorldPositions.TryGetValue(stationIndex, out var position))
            {
                MilestoneEffect.SpawnAt(position);
            }

            PlaySfx("sfx-milestone");
        }

        private void BuyManager(int i)
        {
            var def = StationCatalog.All[i];
            if (state.Stations[i].HasManager || revenue < def.ManagerCost)
            {
                return;
            }

            revenue -= def.ManagerCost;
            state.Stations[i].HasManager = true;
            RefreshUi();
            FlashHeader();
            PlaySfx("sfx-purchase");
            SpawnStaffWorker(i);
        }

        /// <summary>
        /// PLANv2.md Abschnitt 9: sichtbares Personal an Stationen mit
        /// Manager. Wird sowohl bei einem frischen Kauf (BuyManager) als
        /// auch beim Laden eines Spielstands mit bereits vorhandenen
        /// Managern aufgerufen (siehe InitializeGame).
        /// </summary>
        private void SpawnStaffWorker(int i)
        {
            if (!stationWorldPositions.TryGetValue(i, out var stationPosition))
            {
                return;
            }

            var worker = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            worker.name = $"Staff_{i}";
            worker.transform.localScale = new Vector3(0.22f, 0.35f, 0.22f);
            Destroy(worker.GetComponent<Collider>());

            var renderer = worker.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                color = Color.white,
            };

            var staff = worker.AddComponent<StaffWorker>();
            staff.Init(stationPosition + new Vector3(0.3f, 0.3f, 0f), state.Stations[i].CycleSeconds(StationCatalog.All[i]));
        }

        /// <summary>
        /// PLANv2.md Abschnitt 10: faerbt Boden/Waende nach dem aktuellen
        /// Location-Index um -- macht eine Renovierung sichtbar, ohne eigene
        /// 3D-Assets pro Location zu brauchen (Farbpalette statt neuer
        /// Geometrie, siehe LocationTheme). Objekte werden per Name
        /// gesucht, weil sie editor-seitig in CIBuild.cs einmalig gebaut
        /// werden, nicht zur Laufzeit.
        /// </summary>
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>
        /// Material.color ist eine Kompatibilitaets-Eigenschaft, die bei
        /// URP-Shadern (_BaseColor statt des klassischen _Color) unzuverlaessig
        /// ist -- der Getter liefert zwar den neu gesetzten Wert zurueck, ohne
        /// dass es sich sichtbar auf das Rendering auswirkt. SetColor auf den
        /// konkreten URP-Property-Namen umgeht das.
        /// </summary>
        private void ApplyLocationTheme()
        {
            var theme = LocationTheme.For(state.CurrentLocation);

            var ground = GameObject.Find("Ground");
            if (ground != null && ground.TryGetComponent<MeshRenderer>(out var groundRenderer))
            {
                groundRenderer.sharedMaterial.SetColor(BaseColorId, theme.Ground);
            }

            for (var i = 0; i < 4; i++)
            {
                var wall = GameObject.Find($"Wall_{i}");
                if (wall != null && wall.TryGetComponent<MeshRenderer>(out var wallRenderer))
                {
                    wallRenderer.sharedMaterial.SetColor(BaseColorId, theme.Wall);
                }
            }
        }

        private void BuyMarketing()
        {
            var cost = GuestFlow.NextMarketingCost(state.MarketingLevel);
            if (revenue < cost)
            {
                return;
            }

            revenue -= cost;
            state.MarketingLevel++;
            RefreshUi();
            FlashHeader();
            PlaySfx("sfx-purchase");
        }

        /// <summary>
        /// Renovierung (PLANv2.md Abschnitt 1.1/7/10): Reset der laufenden
        /// Runde gegen Renovierungspunkte -- mathematisch identisch zum
        /// urspruenglichen Michelin-Sterne-Prestige (PLAN.md Phase 6), nur
        /// umgedeutet. Lifetime-Umsatz bleibt bewusst erhalten -- die
        /// Punkte-Formel rechnet auf dem kumulierten Gesamtwert, nicht auf
        /// einem pro-Run-Wert (siehe Prestige.StarsGainedFromReset). Erhoeht
        /// zusaetzlich den Location-Index und faerbt Boden/Waende um --
        /// macht die Renovierung sichtbar statt nur eine Zahl zurueckzusetzen.
        /// </summary>
        private void PrestigeReset()
        {
            var gain = Prestige.StarsGainedFromReset(lifetimeRevenue, PrestigeK, prestigeStars);
            if (gain <= BigDouble.Zero)
            {
                return;
            }

            prestigeStars += gain;
            revenue = BigDouble.Zero;
            state.RevenueString = revenue.ToString();
            state.MarketingLevel = 0;
            state.Stations = new List<Station>();
            state.CurrentLocation = Mathf.Min(state.CurrentLocation + 1, LocationTheme.MaxIndex);
            // Gibt wie beim allerersten Start die erste Station gratis --
            // sonst waere nach dem Reset buchstaeblich kein Kauf mehr moeglich.
            SaveSystem.Normalize(state);
            ApplyLocationTheme();

            // PLANv3.md Phase D: ohne diesen Aufruf blieben zuvor
            // freigeschaltete Stationen (i>=1) in der 3D-Szene faelschlich
            // sichtbar, obwohl state.Stations gerade komplett zurueckgesetzt
            // wurde -- RevealStationsAsNeeded() wird sonst nur beim Laden
            // und bei BuyStation() aufgerufen.
            RevealStationsAsNeeded();

            // Alte Personal-Figuren gehoeren zu Stationen, die soeben
            // zurueckgesetzt wurden -- ohne Aufraeumen wuerden sie verwaist
            // weiter herumwippen.
            foreach (var staff in FindObjectsByType<StaffWorker>(FindObjectsSortMode.None))
            {
                Destroy(staff.gameObject);
            }

            RefreshUi();
            FlashHeader();
            PlaySfx("sfx-milestone");
            Persist();
        }

        // Kurzer Goldton-Blitz auf dem Umsatz-Label bei jeder Aktion, die
        // ihn tatsaechlich veraendert (PLAN.md Abschnitt 6: "jede
        // hochzaehlende Zahl tweent"). Bewusst nur bei diesen diskreten
        // Ereignissen statt bei jedem passiven Tick in Update() -- ein
        // Blitz jeden Frame waere kein Feedback mehr, sondern nur Flackern.
        private static readonly Color HeaderFlashColor = new Color(0.85f, 0.65f, 0.1f);
        private Coroutine headerFlashRoutine;

        private void FlashHeader()
        {
            if (headerFlashRoutine != null)
            {
                StopCoroutine(headerFlashRoutine);
            }

            headerFlashRoutine = StartCoroutine(FlashHeaderRoutine());
        }

        private IEnumerator FlashHeaderRoutine()
        {
            const float duration = 0.25f;
            const float half = duration / 2f;
            var original = headerLabel.color;

            var elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                headerLabel.color = Color.Lerp(original, HeaderFlashColor, elapsed / half);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                headerLabel.color = Color.Lerp(HeaderFlashColor, original, elapsed / half);
                yield return null;
            }

            headerLabel.color = original;
            headerFlashRoutine = null;
        }

        private void RefreshUi()
        {
            var guestFlow = GuestFlow.GuestFlowAt(state.MarketingLevel);
            var marketingCost = GuestFlow.NextMarketingCost(state.MarketingLevel);
            var unlockedCount = 0;
            var nextGoalIndex = -1;
            for (var i = 0; i < state.Stations.Count; i++)
            {
                if (state.Stations[i].IsUnlocked)
                {
                    unlockedCount++;
                }
                else if (nextGoalIndex < 0)
                {
                    nextGoalIndex = i;
                }
            }

            // PLANv3.md Phase D: "kein sichtbares naechstes Ziel" war einer
            // der konkreten Befunde -- diese Zeile ersetzt sieben
            // gleichzeitig sichtbare Listenzeilen durch eine klare Ansage,
            // was als naechstes zu tun ist.
            var nextGoalText = nextGoalIndex < 0
                ? "Alle Stationen freigeschaltet!"
                : $"Naechstes Ziel: {StationCatalog.All[nextGoalIndex].Name} fuer {NumberFormat.Format(state.Stations[nextGoalIndex].UnlockCost(StationCatalog.All[nextGoalIndex]))}";

            headerLabel.text = $"{LocationTheme.For(state.CurrentLocation).Name}"
                + $"\nUmsatz: {NumberFormat.Format(revenue)}\nLifetime: {NumberFormat.Format(lifetimeRevenue)}"
                + $"\nGaestestrom: {NumberFormat.Format(guestFlow)}  (Stationen belegt: {guestAtStation.Count}/{unlockedCount})"
                + $"\n{nextGoalText}"
                + $"\nMarketing Stufe {state.MarketingLevel} -- naechste Stufe: {NumberFormat.Format(marketingCost)}";
            marketingButtonRef.interactable = revenue >= marketingCost;

            var prestigeGain = Prestige.StarsGainedFromReset(lifetimeRevenue, PrestigeK, prestigeStars);
            prestigeLabel.text = $"Renovierungspunkte: {NumberFormat.Format(prestigeStars)} (x{PrestigeMultiplier():F2} Ertrag)"
                + $"\nNaechste Renovierung bringt: +{NumberFormat.Format(prestigeGain)}";
            prestigeButtonRef.interactable = prestigeGain > BigDouble.Zero;

            for (var i = 0; i < rows.Count; i++)
            {
                var def = StationCatalog.All[i];
                var station = state.Stations[i];
                var row = rows[i];

                // PLANv3.md Phase D: dieselbe Staffelung wie in der 3D-Szene
                // (RevealStationsAsNeeded) -- sonst wuerde die Liste alle
                // sieben Stationen zeigen, waehrend die Szene nur die
                // naechste sichtbar macht. Bewusst kein eigener Boolean-
                // Zustand: die gleiche Bedingung neu auszuwerten ist billig
                // (7 Stationen) und kann nie aus dem Takt geraten.
                var isRevealed = i == 0 || state.Stations[i - 1].IsUnlocked;
                row.Label.gameObject.SetActive(isRevealed);
                row.BuyButton.gameObject.SetActive(isRevealed);
                if (!isRevealed)
                {
                    row.EquipButton.gameObject.SetActive(false);
                    row.ManagerButton.gameObject.SetActive(false);
                    continue;
                }

                if (station.IsUnlocked)
                {
                    row.Label.text = $"{def.Name}: Preis Lv.{station.PriceLevel} / Ausstattung Lv.{station.EquipmentLevel}"
                        + $"\nErtrag/Verkauf: {NumberFormat.Format(station.YieldPerSale(def))}  |  Zyklus: {station.CycleSeconds(def):0.0}s"
                        + (station.HasManager ? "\nManager: aktiv" : $"\nManager: {NumberFormat.Format(def.ManagerCost)}");

                    row.BuyButtonLabel.text = $"Preis erhoehen ({NumberFormat.Format(station.NextPriceUpgradeCost(def))})";
                    row.BuyButton.interactable = revenue >= station.NextPriceUpgradeCost(def);

                    row.EquipButton.gameObject.SetActive(true);
                    row.EquipButtonLabel.text = $"Ausstattung ({NumberFormat.Format(station.NextEquipmentUpgradeCost(def))})";
                    row.EquipButton.interactable = revenue >= station.NextEquipmentUpgradeCost(def);
                }
                else
                {
                    row.Label.text = $"{def.Name}: nicht freigeschaltet"
                        + $"\nFreischalten: {NumberFormat.Format(station.UnlockCost(def))}";

                    row.BuyButtonLabel.text = "Kaufen";
                    row.BuyButton.interactable = revenue >= station.UnlockCost(def);

                    row.EquipButton.gameObject.SetActive(false);
                }

                row.BuyButtonImage.color = (i == 0 && !station.IsUnlocked) ? GuidedButtonColor : DefaultButtonColor;

                row.ManagerButton.gameObject.SetActive(station.IsUnlocked && !station.HasManager);
                row.ManagerButton.interactable = revenue >= def.ManagerCost;
            }
        }

        // -- UI-Aufbau: grauer Prototyp, kein Art-Pass (PLAN.md Abschnitt 4/7). --
        //
        // Bewusst mit Auto-Layout (VerticalLayoutGroup + ScrollRect) statt
        // manuell berechneten anchoredPosition-Werten: zwei vorherige Versuche
        // mit Pixel-/Anker-Mathe waren im echten WebGL-Build (Handy-Bildschirm)
        // sichtbar falsch, ohne dass sich das ohne Editor-Zugriff zuverlaessig
        // vorhersagen liess. Layout-Groups berechnen Position und Groesse
        // selbst zur Laufzeit aus der tatsaechlichen Bildschirmgroesse -- das
        // eliminiert diese ganze Fehlerklasse, unabhaengig von der Aufloesung.

        private void BuildUi()
        {
            var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));

            var scrollViewGo = new GameObject("ScrollView", typeof(Image), typeof(ScrollRect));
            scrollViewGo.transform.SetParent(canvasObject.transform, false);
            // Nur untere ~55% des Bildschirms -- PLANv2.md Abschnitt 1.2
            // ("Die Szene ist das UI"): die 3D-Location bleibt im oberen
            // Bereich sichtbar statt komplett von der Liste verdeckt zu
            // werden.
            var scrollViewRect = scrollViewGo.GetComponent<RectTransform>();
            scrollViewRect.anchorMin = new Vector2(0f, 0f);
            scrollViewRect.anchorMax = new Vector2(1f, 0.55f);
            scrollViewRect.offsetMin = Vector2.zero;
            scrollViewRect.offsetMax = Vector2.zero;
            scrollViewGo.GetComponent<Image>().color = new Color(0.93f, 0.93f, 0.93f);

            var viewportGo = new GameObject("Viewport", typeof(RectMask2D));
            viewportGo.transform.SetParent(scrollViewGo.transform, false);
            var viewportRect = viewportGo.GetComponent<RectTransform>();
            StretchToFillParent(viewportRect);

            var contentGo = new GameObject("Content", typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewportGo.transform, false);
            var contentRect = contentGo.GetComponent<RectTransform>();
            // Oben ausgerichtet und ueber die volle Breite -- Hoehe ergibt sich
            // aus dem Inhalt (ContentSizeFitter), damit die Liste wachsen kann.
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;

            var layoutGroup = contentGo.GetComponent<VerticalLayoutGroup>();
            layoutGroup.padding = new RectOffset(30, 30, 30, 30);
            layoutGroup.spacing = 12;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;

            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollRect = scrollViewGo.GetComponent<ScrollRect>();
            scrollRect.content = contentRect;
            scrollRect.viewport = viewportRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;

            // 205 statt 130 -- bei 130 wurde die vierte Zeile (Marketing-
            // Stufe) im Text-Rect abgeschnitten (siehe PLANv3 Phase-C-Fix),
            // die fuenfte Zeile (Naechstes Ziel, PLANv3 Phase D) braucht
            // noch mal Platz dazu.
            headerLabel = CreateLabel(contentGo.transform, preferredHeight: 205);
            marketingButtonRef = CreateButton(contentGo.transform, "Marketing kaufen", BuyMarketing, preferredHeight: 70);
            prestigeLabel = CreateLabel(contentGo.transform, preferredHeight: 80);
            prestigeButtonRef = CreateButton(contentGo.transform, "Renovieren", PrestigeReset, preferredHeight: 70);

            for (var i = 0; i < StationCatalog.All.Count; i++)
            {
                var index = i; // lokale Kopie fuer die Closures unten
                var icon = Resources.Load<Sprite>($"Icons/{StationIconNames[index]}");
                var label = CreateStationHeader(contentGo.transform, icon, preferredHeight: 80);
                var buyButton = CreateButton(contentGo.transform, "Kaufen", () => BuyStation(index), preferredHeight: 60);
                var equipButton = CreateButton(contentGo.transform, "Ausstattung", () => UpgradeEquipment(index), preferredHeight: 60);
                var managerButton = CreateButton(contentGo.transform, "Manager", () => BuyManager(index), preferredHeight: 60);

                rows.Add(new StationRow
                {
                    Label = label,
                    BuyButton = buyButton,
                    BuyButtonImage = buyButton.GetComponent<Image>(),
                    BuyButtonLabel = buyButton.GetComponentInChildren<Text>(),
                    EquipButton = equipButton,
                    EquipButtonLabel = equipButton.GetComponentInChildren<Text>(),
                    ManagerButton = managerButton,
                });
            }
        }

        private static void StretchToFillParent(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Text CreateLabel(Transform parent, float preferredHeight)
        {
            var go = new GameObject("Label", typeof(Text), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            text.font = Resources.Load<Font>("Fonts/Fredoka");
            text.fontSize = 28;
            text.alignment = TextAnchor.UpperLeft;
            text.color = Color.black;

            var layoutElement = go.GetComponent<LayoutElement>();
            layoutElement.preferredHeight = preferredHeight;
            layoutElement.flexibleWidth = 1;

            return text;
        }

        /// <summary>
        /// Wie CreateLabel, aber mit optionalem quadratischem Icon links
        /// daneben (Kenney Food Kit, Assets/Resources/Icons) -- fuer die
        /// Stationszeilen. icon darf null sein (z.B. Icon-PNG fehlt), dann
        /// verhaelt es sich wie eine reine Textzeile.
        /// </summary>
        private static Text CreateStationHeader(Transform parent, Sprite icon, float preferredHeight)
        {
            var rowGo = new GameObject("StationHeader", typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            rowGo.transform.SetParent(parent, false);

            var rowLayoutGroup = rowGo.GetComponent<HorizontalLayoutGroup>();
            rowLayoutGroup.spacing = 12;
            rowLayoutGroup.childAlignment = TextAnchor.MiddleLeft;
            rowLayoutGroup.childForceExpandWidth = false;
            rowLayoutGroup.childForceExpandHeight = true;
            rowLayoutGroup.childControlWidth = true;
            rowLayoutGroup.childControlHeight = true;

            var rowLayoutElement = rowGo.GetComponent<LayoutElement>();
            rowLayoutElement.preferredHeight = preferredHeight;
            rowLayoutElement.flexibleWidth = 1;

            if (icon != null)
            {
                var iconGo = new GameObject("Icon", typeof(Image), typeof(LayoutElement));
                iconGo.transform.SetParent(rowGo.transform, false);
                var iconImage = iconGo.GetComponent<Image>();
                iconImage.sprite = icon;
                iconImage.preserveAspect = true;
                var iconLayoutElement = iconGo.GetComponent<LayoutElement>();
                iconLayoutElement.preferredWidth = preferredHeight;
                iconLayoutElement.flexibleWidth = 0;
            }

            return CreateLabel(rowGo.transform, preferredHeight);
        }

        private static Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick, float preferredHeight)
        {
            var go = new GameObject(label, typeof(Image), typeof(Button), typeof(LayoutElement), typeof(ButtonPunch));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.8f, 0.8f, 0.8f);

            var button = go.GetComponent<Button>();
            var punch = go.GetComponent<ButtonPunch>();
            button.onClick.AddListener(onClick);
            button.onClick.AddListener(punch.Punch);
            button.onClick.AddListener(() => PlaySfx("sfx-click"));

            var layoutElement = go.GetComponent<LayoutElement>();
            layoutElement.preferredHeight = preferredHeight;
            layoutElement.flexibleWidth = 1;

            var labelGo = new GameObject("Text", typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            StretchToFillParent(labelGo.GetComponent<RectTransform>());
            var labelText = labelGo.GetComponent<Text>();
            labelText.font = Resources.Load<Font>("Fonts/Fredoka");
            labelText.fontSize = 28;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = Color.black;
            labelText.text = label;

            return button;
        }
    }
}
