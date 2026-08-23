using System;
using System.Collections;
using System.Collections.Generic;
using BalancingCore;
using BreakInfinity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
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

        /// <summary>Urspruengliche CIBuild-Skalierung je Station (FurnitureScale) -- Zielgroesse fuer den Pop-In-Effekt beim Freischalten, siehe PopInStation.</summary>
        private readonly Dictionary<int, Vector3> stationOriginalScale = new();

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

            /// <summary>Ausgangsgeduld dieses Besuchs -- Bezugsgroesse fuer Geduldsbalken (StationBadge) und Trinkgeld (BalancingCore.Service).</summary>
            public float TotalPatience;

            /// <summary>Seltener Gast mit vielfachem Ertrag, siehe VipPayoutMultiplier.</summary>
            public bool IsVip;

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

        /// <summary>Waehrend InitializeGame() gesammelter Offline-Ertrag, fuer den Willkommens-Dialog (ShowOfflineEarningsDialog) -- siehe ApplyOfflineEarnings.</summary>
        private BigDouble pendingOfflineEarnings = BigDouble.Zero;
        private double pendingOfflineMinutes;

        // -- HUD (siehe BuildUi) --
        private Transform canvasTransform;
        private RectTransform canvasRect;

        /// <summary>Grosse Umsatzzahl in der Kopfleiste -- gleichzeitig das Ziel von FlashHeader.</summary>
        private Text headerLabel;
        private Text statsLabel;
        private Text goalLabel;
        private Image goalFill;
        private Button marketingButtonRef;
        private Text marketingButtonLabel;
        private Image marketingButtonImage;
        private Button prestigeButtonRef;
        private Text prestigeButtonLabel;
        private Image prestigeButtonImage;
        private GameObject rushBanner;

        /// <summary>Schwebendes Schild pro Station (Geduld/Kaufhinweis), siehe StationBadge.</summary>
        private readonly Dictionary<int, StationBadge> stationBadges = new();

        // PLANv3.md Phase D ("Kein Tutorial, kein geführter erster Kauf"):
        // der allererste Kauf im Spiel bekommt eine auffaellige Farbe statt
        // im grauen Einheitsbrei unterzugehen -- billigste Form von
        // Fuehrung ohne echtes Tutorial-System.
        private static readonly Color GuidedButtonColor = new Color(0.98f, 0.75f, 0.25f);

        // Nutzer-Feedback ("macht das Spiel gerade Spass?"): vorher war JEDER
        // Button exakt dasselbe Grau, leistbar oder nicht -- man musste den
        // Text lesen, um zu wissen, ob ein Kauf gerade moeglich ist. Gruen/
        // Grau macht das auf einen Blick sichtbar, ganz ohne neue Assets.
        private static readonly Color AffordableButtonColor = new Color(0.55f, 0.82f, 0.5f);
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
        private const string MutedPrefKey = "RestaurantIdle.Muted";

        private IEnumerator InitializeGame()
        {
            AudioListener.volume = PlayerPrefs.GetInt(MutedPrefKey, 0) == 1 ? 0f : 1f;

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
                stationOriginalScale[hotspot.StationIndex] = hotspot.transform.localScale;

                // Schild leicht ueber der Station -- direkt auf der
                // Stationsposition wuerde es das Moebelstueck selbst
                // verdecken, das es erklaeren soll.
                stationBadges[hotspot.StationIndex] =
                    StationBadge.Create(canvasRect, hotspot.transform.position + new Vector3(0f, 0.45f, 0f));
            }

            RevealStationsAsNeeded(animate: false);

            for (var i = 0; i < state.Stations.Count; i++)
            {
                if (state.Stations[i].HasManager)
                {
                    SpawnStaffWorker(i);
                }
            }

            RefreshUi();

            // PLANv3.md Phase F: waehrend eines Live-Testlaufs aufgefallen --
            // ein "Waehrend du weg warst..."-Dialog fuer "0 Minuten" (schneller
            // Neustart, Editor-Recompile) unterbricht mehr, als er bringt. Das
            // Geld wird trotzdem immer gutgeschrieben (siehe ApplyOfflineEarnings),
            // nur der Dialog braucht eine sinnvolle Untergrenze.
            const double MinOfflineMinutesForDialog = 1.0;
            if (pendingOfflineEarnings > BigDouble.Zero && pendingOfflineMinutes >= MinOfflineMinutesForDialog)
            {
                ShowOfflineEarningsDialog(pendingOfflineEarnings, pendingOfflineMinutes);
            }

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

                // PLANv3.md Phase F ("Kein Offline-Dialog... der zentrale
                // Moment beim Oeffnen"): der Ertrag wurde bisher nur
                // geloggt, nie dem Spieler gezeigt -- gesammelt statt sofort
                // angezeigt, weil BuildUi() (der Canvas, auf den der Dialog
                // gehaengt wird) an dieser Stelle noch nicht existiert.
                pendingOfflineEarnings += earned;
                pendingOfflineMinutes += offlineDuration.TotalMinutes;
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
            UpdateRushHour();
            UpdateGuestSpawner();
            UpdateGuestVisits();
            UpdateGuestQueue();
            ApplyCameraFraming();
            UpdateStationBadges();

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
                        ServeGuest(stationIndex, visit, earned);
                        (finished ??= new List<int>()).Add(stationIndex);
                        continue;
                    }
                }

                visit.PatienceRemaining -= Time.deltaTime;
                if (visit.PatienceRemaining <= 0f)
                {
                    visit.Mover.Leave();
                    RegisterLostGuest();
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
            if (!Physics.Raycast(ray, out var hit) || !hit.collider.TryGetComponent<StationHotspot>(out var hotspot))
            {
                return;
            }

            // Nutzer-Feedback: keine dauerhaften Listen-Buttons mehr fuer
            // Stationsaktionen -- alles laeuft ueber Antippen der Station
            // selbst. Wartet dort gerade ein Gast, bedient der Tap ihn
            // (K2, unveraendert); sonst oeffnet derselbe Tap das Dialogmenu
            // mit Kaufen/Ausstattung/Manager fuer genau diese Station.
            var stationIndex = hotspot.StationIndex;
            if (guestAtStation.TryGetValue(stationIndex, out var visit) && visit.Mover != null && visit.Mover.HasArrivedAtStation)
            {
                ProduceNow(stationIndex, hit.point);
            }
            else
            {
                OpenStationDialog(stationIndex);
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

        /// <summary>
        /// Eingang, Ausgang, Warteplaetze und Wartepositionen stehen in
        /// RestaurantLayout -- derselben Quelle, aus der CIBuild die Szene
        /// baut. Vorher waren es hier eigene Konstanten, die bei jeder
        /// Grundriss-Aenderung von Hand nachgezogen werden mussten.
        ///
        /// Gaeste liefen ausserdem exakt auf die Stationsposition -- bei
        /// Station 0 also mitten in die Kaffeemaschine, die auf der Theke
        /// steht (y=1.05). Der Platz VOR der Station auf Bodenhoehe ist der
        /// einzige, an dem ein Gast plausibel wartet.
        /// </summary>
        private static Vector3 GuestEntrance => RestaurantLayout.Entrance;
        private static Vector3 GuestExit => RestaurantLayout.Exit;

        private Vector3 GuestStandPosition(int stationIndex) =>
            stationWorldPositions.TryGetValue(stationIndex, out var position)
                ? RestaurantLayout.GuestStandPosition(position)
                : RestaurantLayout.Entrance;

        /// <summary>
        /// PLANv3.md Phase E ("echtes Raumlayout ... Warteschlange"): ein
        /// Gast, der keine freie Station fand, drehte bisher sofort am
        /// Eingang ab. Kapazitaet war damit eine harte, unsichtbare Kante --
        /// und der Ruf-Verlust (BalancingCore.Reputation) traf sofort, ohne
        /// dass der Spieler eine Chance zum Reagieren hatte. Mit einer
        /// echten Schlange entsteht stattdessen ein Puffer: man SIEHT, dass
        /// es eng wird, und kann durch Antippen oder eine neue Station noch
        /// eingreifen, bevor jemand wirklich verloren geht.
        /// </summary>
        private const int QueueCapacity = RestaurantLayout.QueueCapacity;

        /// <summary>Geduld in der Schlange -- laenger als an der Station: Anstehen ist erwartbar, Warten am bedienten Platz nicht.</summary>
        private const float QueuePatienceSeconds = 25f;

        /// <summary>
        /// Rush Hour: alle RushIntervalSeconds fuer RushDurationSeconds
        /// vielfacher Gaestestrom. Ein Idle-Spiel, dessen Gaeste in ewig
        /// gleichem Takt hereinkommen, hat keinen Grund, jemals aktiv
        /// hinzuschauen -- der Stossbetrieb erzeugt genau die Momente, in
        /// denen manuelles Antippen, Schlangenlaenge und Ruf ploetzlich
        /// zusammenspielen.
        /// </summary>
        private const float RushIntervalSeconds = 150f;
        private const float RushDurationSeconds = 25f;
        private const float RushSpawnMultiplier = 3f;
        private const float FirstRushAfterSeconds = 60f;
        private float rushCooldown = FirstRushAfterSeconds;
        private float rushRemaining;

        /// <summary>
        /// Seltener Gast mit vielfachem Ertrag -- der einzige Grund, eine
        /// bereits laufende, gemanagte Station trotzdem noch von Hand
        /// anzutippen. Ohne so ein Ereignis endet aktives Spielen exakt in
        /// dem Moment, in dem der letzte Manager gekauft ist.
        /// </summary>
        private const double VipPayoutMultiplier = 6.0;
        private const float VipChance = 0.08f;
        private static readonly Color VipTint = new Color(1f, 0.85f, 0.35f);

        private readonly List<QueuedGuest> guestQueue = new();

        private class QueuedGuest
        {
            public GuestMover Mover;
            public float PatienceRemaining;
            public bool IsVip;
        }

        /// <summary>
        /// Nutzer-Feedback ("macht das Spiel gerade Spass?"): die feste
        /// Kamera aus CIBuild.cs (orthographicSize 4, lookTarget x=2.8)
        /// zeigt bei sieben Stationen in einer Reihe (Spannweite ~6 Einheiten)
        /// immer nur einen schmalen Ausschnitt -- man sieht sein eigenes
        /// Restaurant nie als Ganzes wachsen, gerade das macht den Reiz des
        /// Genres aus. Kamera faehrt jetzt weich zurueck/zur Seite, je nachdem
        /// wie viele Stationen freigeschaltet sind (RevealStationsAsNeeded
        /// liefert dieselbe Sichtbarkeits-Bedingung) -- das Herauszoomen
        /// selbst wird so nebenbei zu einem kleinen Belohnungsmoment.
        /// </summary>
        // Nutzer-Feedback ("das design gefaellt mir weiterhin nicht"): 4
        // war der Wert aus der alten fixen CIBuild-Kamera, nie gegen die
        // neue mitwachsende Kamera geprueft. Mit nur ein bis zwei
        // Stationen sichtbar (Fruehspiel) liegt die inhaltsbasierte
        // Rechnung in RecomputeCameraTarget weit UNTER 4 -- die Untergrenze
        // selbst war die eigentliche Ursache fuer den grossen leeren
        // Bereich um die Station, nicht die Wand-/Awning-Skalierung, an
        // der zuvor mehrfach nachjustiert wurde.
        private const float MinOrthographicSize = 2f;
        private const float CameraFramingMarginX = 1.4f;
        private const float CameraFramingMarginY = 1.6f;
        private const float CameraFramingLerpSpeed = 1.2f;

        /// <summary>
        /// Die Szene ist nicht mehr ueber den ganzen Bildschirm sichtbar:
        /// Kopfleiste + Zielbalken decken oben rund 21 %, die Aktionsleiste
        /// unten rund 10 % ab (siehe BuildUi). Die Mitte des FREIEN Bereichs
        /// liegt damit unter der Bildschirmmitte -- ohne diesen Versatz
        /// zentriert die Kamera das Restaurant auf einen Punkt, der zum Teil
        /// hinter der Kopfleiste liegt.
        /// </summary>
        private const float CameraVerticalScreenBias = 0.055f;
        private static readonly Vector3 CameraBackOffset = new Vector3(0f, 0f, -15f);
        private float targetOrthoSize = MinOrthographicSize;
        private Vector3 targetLookAt = Vector3.zero;

        /// <summary>Gaestestrom inkl. Ruf-Faktor und laufender Rush Hour -- eine Quelle fuer Simulation und Anzeige.</summary>
        private BigDouble EffectiveGuestFlow() =>
            GuestFlow.GuestFlowAt(state.MarketingLevel)
            * Reputation.FlowMultiplier(state.Reputation)
            * (rushRemaining > 0f ? (double)RushSpawnMultiplier : 1.0);

        private void UpdateGuestSpawner()
        {
            // Ruf und Rush Hour wirken multiplikativ auf denselben
            // Gaestestrom, den auch die Kopfzeile anzeigt (RefreshUi) --
            // damit ist die angezeigte Zahl nicht Deko, sondern exakt die
            // Groesse, die den Takt bestimmt.
            var guestFlow = EffectiveGuestFlow().ToDouble();
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
            var isVip = UnityEngine.Random.value < VipChance;

            // PLANv3.md Abschnitt 5: Kenney-Toon-Character-Sprite statt
            // eingefaerbter Kapsel. Kein Collider noetig -- anders als die
            // Kapsel vorher blockiert ein SpriteRenderer ohne Collider von
            // Natur aus keinen Raycast.
            var guest = new GameObject(isVip ? "Guest_VIP" : "Guest", typeof(SpriteRenderer));
            var spriteRenderer = guest.GetComponent<SpriteRenderer>();
            spriteRenderer.sprite = Resources.Load<Sprite>("Characters/guest-idle");
            spriteRenderer.color = isVip ? VipTint : Color.white;
            guest.transform.localScale = Vector3.one * (isVip ? GuestSpriteScale * 1.15f : GuestSpriteScale);
            if (Camera.main != null)
            {
                // Billboard: Sprite-Ebene richtet sich einmalig nach der
                // (fest stehenden) Kamera aus, kein Nachfuehren pro Frame
                // noetig -- die Kamera bewegt sich nirgends im Spiel.
                guest.transform.rotation = Camera.main.transform.rotation;
            }

            var mover = guest.AddComponent<GuestMover>();
            mover.SpeedMultiplier = UnityEngine.Random.Range(0.85f, 1.2f);
            guest.AddComponent<GuestSpriteAnimator>();
            mover.Init(GuestEntrance, GuestEntrance, GuestExit, waitsForService: false);

            var stationIndex = PickAvailableStationIndex();
            if (stationIndex.HasValue)
            {
                AssignGuestToStation(mover, stationIndex.Value, isVip);
                if (isVip)
                {
                    Toast.Show(canvasTransform, "VIP-Gast im Haus -- sofort bedienen lohnt sich!", new Color(1f, 0.93f, 0.6f, 0.95f));
                }

                return;
            }

            if (guestQueue.Count < QueueCapacity)
            {
                guestQueue.Add(new QueuedGuest
                {
                    Mover = mover,
                    PatienceRemaining = QueuePatienceSeconds,
                    IsVip = isVip,
                });

                mover.Redirect(QueueSlotPosition(guestQueue.Count - 1), waitsForService: true);
                return;
            }

            // Schlange voll: der Gast dreht sichtbar am Eingang ab. Erst
            // HIER ist wirklich jemand verloren -- nicht schon, wenn nur
            // gerade keine Station frei ist.
            var bouncePoint = Vector3.Lerp(GuestEntrance, GuestExit, 0.15f);
            mover.Redirect(bouncePoint, waitsForService: false);
            RegisterLostGuest("Die Schlange war zu lang -- ein Gast ist gegangen", fromQueue: true);
        }

        private static Vector3 QueueSlotPosition(int slot) => RestaurantLayout.QueueSlot(slot);

        /// <summary>
        /// Setzt einen Gast (frisch gespawnt oder aus der Schlange
        /// nachgerueckt) auf eine konkrete Station an. Die Reservierung in
        /// guestAtStation passiert sofort, nicht erst bei Ankunft -- sonst
        /// koennten zwei Gaeste zur selben freien Station loslaufen.
        /// </summary>
        private void AssignGuestToStation(GuestMover mover, int stationIndex, bool isVip)
        {
            if (!stationWorldPositions.ContainsKey(stationIndex))
            {
                return;
            }

            var station = state.Stations[stationIndex];
            var cycleSeconds = (float)station.CycleSeconds(StationCatalog.All[stationIndex]);
            var patience = Mathf.Max(GuestPatienceSeconds, cycleSeconds + GuestPatienceBufferSeconds);

            mover.Redirect(GuestStandPosition(stationIndex), waitsForService: true);
            guestAtStation[stationIndex] = new GuestVisit
            {
                Mover = mover,
                PatienceRemaining = patience,
                TotalPatience = patience,
                IsVip = isVip,
            };
        }

        /// <summary>
        /// Haelt die Schlange in Bewegung: abgelaufene Geduld raus, frei
        /// gewordene Stationen an den Kopf der Schlange vergeben, Rest
        /// nachruecken lassen. Bewusst jeden Frame statt ereignisgesteuert --
        /// bei maximal QueueCapacity Eintraegen ist das billiger als die
        /// Buchfuehrung, die eine ereignisgesteuerte Variante braeuchte.
        /// </summary>
        private void UpdateGuestQueue()
        {
            for (var i = guestQueue.Count - 1; i >= 0; i--)
            {
                var queued = guestQueue[i];
                if (queued.Mover == null)
                {
                    guestQueue.RemoveAt(i);
                    continue;
                }

                queued.PatienceRemaining -= Time.deltaTime;
                if (queued.PatienceRemaining <= 0f)
                {
                    queued.Mover.Leave();
                    guestQueue.RemoveAt(i);
                    RegisterLostGuest("Ein Gast hat das Anstehen aufgegeben", fromQueue: true);
                }
            }

            while (guestQueue.Count > 0)
            {
                var free = PickAvailableStationIndex();
                if (!free.HasValue)
                {
                    break;
                }

                var next = guestQueue[0];
                guestQueue.RemoveAt(0);
                AssignGuestToStation(next.Mover, free.Value, next.IsVip);
            }

            for (var i = 0; i < guestQueue.Count; i++)
            {
                var slot = QueueSlotPosition(i);
                // Nur umlenken, wenn sich der Platz wirklich geaendert hat --
                // ein Redirect pro Frame wuerde HasArrivedAtStation dauernd
                // zuruecksetzen, der Gast kaeme nie "an".
                if ((guestQueue[i].Mover.CurrentTarget - slot).sqrMagnitude > 0.0001f)
                {
                    guestQueue[i].Mover.Redirect(slot, waitsForService: true);
                }
            }
        }

        /// <summary>
        /// Stossbetrieb im Wechsel mit Ruhephasen (siehe RushIntervalSeconds).
        /// Laeuft auf Echtzeit statt auf einem Ereignis -- der Reiz liegt
        /// gerade darin, dass die Rush Hour den Spieler unangekuendigt in
        /// einem beliebigen Ausbauzustand trifft.
        /// </summary>
        private void UpdateRushHour()
        {
            if (rushRemaining > 0f)
            {
                rushRemaining -= Time.deltaTime;
                if (rushRemaining <= 0f)
                {
                    rushRemaining = 0f;
                    rushCooldown = RushIntervalSeconds;
                    if (rushBanner != null)
                    {
                        rushBanner.SetActive(false);
                    }

                    Toast.Show(canvasTransform, "Rush Hour vorbei -- gut gemacht!");
                }

                return;
            }

            rushCooldown -= Time.deltaTime;
            if (rushCooldown > 0f)
            {
                return;
            }

            rushRemaining = RushDurationSeconds;
            if (rushBanner != null)
            {
                rushBanner.SetActive(true);
            }

            Toast.Show(canvasTransform, $"RUSH HOUR! {RushSpawnMultiplier:0}x Gaeste fuer {RushDurationSeconds:0} Sekunden", new Color(1f, 0.82f, 0.45f, 0.95f));
            PlaySfx("sfx-milestone");
        }

        /// <summary>
        /// Uebertraegt den Simulationszustand auf die schwebenden Schilder
        /// ueber den Stationen: wartender Gast mit Geduldsbalken und dem
        /// Betrag, der beim sofortigen Bedienen herausspringt -- oder, bei
        /// noch gesperrter Station, der Kaufhinweis, sobald er leistbar ist.
        /// </summary>
        private void UpdateStationBadges()
        {
            foreach (var kvp in stationBadges)
            {
                var index = kvp.Key;
                var badge = kvp.Value;
                if (badge == null || index >= state.Stations.Count)
                {
                    continue;
                }

                var visible = index == 0 || state.Stations[index - 1].IsUnlocked;
                if (!visible)
                {
                    badge.Hide();
                    continue;
                }

                var station = state.Stations[index];
                var def = StationCatalog.All[index];

                if (guestAtStation.TryGetValue(index, out var visit) && visit.Mover != null && visit.Mover.HasArrivedAtStation)
                {
                    var fraction = visit.TotalPatience > 0f ? visit.PatienceRemaining / visit.TotalPatience : 0f;
                    var payout = station.YieldPerSale(def) * PrestigeMultiplier() * (visit.IsVip ? VipPayoutMultiplier : 1.0);
                    badge.ShowWaitingGuest(
                        (visit.IsVip ? "VIP  " : string.Empty) + NumberFormat.Format(payout),
                        fraction,
                        visit.IsVip);
                    continue;
                }

                if (!station.IsUnlocked)
                {
                    var cost = station.UnlockCost(def);
                    if (revenue >= cost)
                    {
                        badge.ShowHint($"Antippen: {NumberFormat.Format(cost)}");
                        continue;
                    }
                }

                badge.Hide();
            }
        }

        /// <summary>
        /// Ein Verkauf an einen konkreten wartenden Gast -- die einzige
        /// Stelle, an der im Live-Betrieb Geld entsteht (PLANv3 K2). Rechnet
        /// Trinkgeld (schnell bedient = mehr, BalancingCore.Service), den
        /// VIP-Faktor und den Renovierungs-Multiplikator zusammen und
        /// verbucht den Ruf-Gewinn.
        /// </summary>
        private void ServeGuest(int stationIndex, GuestVisit visit, BigDouble baseEarned, Vector3? position = null)
        {
            var waitFraction = visit.TotalPatience > 0f
                ? 1f - Mathf.Clamp01(visit.PatienceRemaining / visit.TotalPatience)
                : 0f;
            var tipMultiplier = Service.TipMultiplier(waitFraction);
            var effective = baseEarned * PrestigeMultiplier() * tipMultiplier * (visit.IsVip ? VipPayoutMultiplier : 1.0);

            revenue += effective;
            lifetimeRevenue += effective;
            state.Reputation = Reputation.AfterServed(state.Reputation, tipMultiplier);
            state.GuestsServed++;

            visit.Mover.Leave();

            var worldPosition = position
                ?? (stationWorldPositions.TryGetValue(stationIndex, out var stationPosition) ? stationPosition : (Vector3?)null);
            if (!worldPosition.HasValue)
            {
                return;
            }

            CoinBurst.SpawnAt(worldPosition.Value);

            var label = NumberFormat.Format(effective);
            if (tipMultiplier > 1.2)
            {
                label += "  +Trinkgeld";
            }

            FloatingText.Spawn(
                canvasTransform,
                worldPosition.Value + Vector3.up * 0.5f,
                "+" + label,
                visit.IsVip ? new Color(1f, 0.85f, 0.25f) : new Color(0.35f, 0.95f, 0.45f),
                visit.IsVip ? 52 : 40);
        }

        /// <summary>
        /// Gast unbedient verloren: Ruf faellt (BalancingCore.Reputation) und
        /// der Spieler erfaehrt ueberhaupt davon. Bisher verschwand so ein
        /// Gast wortlos -- ein Misserfolg, den niemand bemerkt, kann auch
        /// niemanden zum Gegensteuern bewegen.
        /// </summary>
        /// <param name="fromQueue">true, wenn der Gast nie einen Platz bekommen hat -- kostet weniger Ruf, siehe BalancingCore.Reputation.</param>
        private void RegisterLostGuest(string message = "Ein Gast ist unbedient gegangen -- Ruf gesunken", bool fromQueue = false)
        {
            state.Reputation = fromQueue
                ? Reputation.AfterQueueAbandoned(state.Reputation)
                : Reputation.AfterLost(state.Reputation);
            state.GuestsLost++;
            Toast.Show(canvasTransform, message, new Color(0.98f, 0.72f, 0.68f, 0.95f));
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
        /// <param name="animate">
        /// false nur beim initialen Laden (InitializeGame) -- bereits
        /// freigeschaltete Stationen sollen einfach da sein, nicht beim
        /// Programmstart alle gleichzeitig "reinpoppen". Bei jedem
        /// spaeteren Aufruf (neue Freischaltung waehrend des Spiels) soll
        /// der Pop-In-Effekt laufen.
        /// </param>
        private void RevealStationsAsNeeded(bool animate = true)
        {
            for (var i = 0; i < state.Stations.Count; i++)
            {
                if (!stationGameObjects.TryGetValue(i, out var go))
                {
                    continue;
                }

                var shouldBeVisible = i == 0 || state.Stations[i - 1].IsUnlocked;
                if (go.activeSelf == shouldBeVisible)
                {
                    continue;
                }

                var targetScale = stationOriginalScale.TryGetValue(i, out var scale) ? scale : go.transform.localScale;
                if (shouldBeVisible && animate)
                {
                    StartCoroutine(PopInStation(go, targetScale));
                }
                else
                {
                    go.transform.localScale = targetScale;
                    go.SetActive(shouldBeVisible);
                }
            }

            RecomputeCameraTarget();
        }

        /// <summary>
        /// Nutzer-Feedback ("macht das Spiel gerade Spass?"): eine neue
        /// Station tauchte bisher instantan per SetActive(true) auf --
        /// keinerlei Feiergefuehl fuer den Moment, der eigentlich einer der
        /// wichtigsten im ganzen Spiel ist. Skaliert von 0 auf die
        /// urspruengliche CIBuild-Groesse mit leichtem Ueberschwingen
        /// (Standard-easeOutBack-Formel, kein AnimationCurve-Asset noetig).
        /// </summary>
        private IEnumerator PopInStation(GameObject go, Vector3 targetScale)
        {
            const float duration = 0.45f;
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;

            go.transform.localScale = Vector3.zero;
            go.SetActive(true);

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration) - 1f;
                var eased = 1f + c3 * t * t * t + c1 * t * t;
                go.transform.localScale = targetScale * eased;
                yield return null;
            }

            go.transform.localScale = targetScale;
        }

        /// <summary>
        /// Bestimmt Zoom/Position, die die Kamera als naechstes anfahren soll
        /// (siehe ApplyCameraFraming fuer die tatsaechliche weiche Bewegung
        /// pro Frame) -- Spannweite aller aktuell sichtbaren Stationen
        /// (dieselbe Bedingung wie RevealStationsAsNeeded), nicht nur der
        /// freigeschalteten: der Spieler soll auch die naechste, noch
        /// gesperrte Station im Blick haben.
        /// </summary>
        private void RecomputeCameraTarget()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            // Rahmung in der EBENE DER KAMERA statt entlang der Welt-X-Achse:
            // die Kamera steht isometrisch, eine Welt-X-Spanne sagt ueber die
            // benoetigte Bildbreite wenig und ueber die Bildhoehe gar nichts.
            // Mit dem diagonalen Grundriss (RestaurantLayout) lief die alte
            // X-Rechnung voellig ins Leere: sie haette zum Zoom auf die
            // Bildbreite optimiert, waehrend die Theke in die Bildhoehe
            // waechst.
            var right = cam.transform.right;
            var up = cam.transform.up;
            var forward = cam.transform.forward;
            var forwardSum = 0f;
            var pointCount = 0;
            var minRight = float.MaxValue;
            var maxRight = float.MinValue;
            var minUp = float.MaxValue;
            var maxUp = float.MinValue;
            var any = false;

            void Include(Vector3 worldPoint)
            {
                var r = Vector3.Dot(worldPoint, right);
                var u = Vector3.Dot(worldPoint, up);
                minRight = Mathf.Min(minRight, r);
                maxRight = Mathf.Max(maxRight, r);
                minUp = Mathf.Min(minUp, u);
                maxUp = Mathf.Max(maxUp, u);

                // Die Tiefe entlang der Blickrichtung beeinflusst bei einer
                // orthografischen Kamera zwar nicht, WAS seitlich im Bild
                // liegt, sehr wohl aber die Near/Far-Ebene: ohne diesen
                // Anteil landete der rekonstruierte Zielpunkt auf der
                // Kamera-Ebene durch den Weltursprung, die Kamera damit
                // mitten in der Szene -- im Testlauf ein Nahaufnahme-
                // Ausschnitt von Teppich und Wand.
                forwardSum += Vector3.Dot(worldPoint, forward);
                pointCount++;
                any = true;
            }

            for (var i = 0; i < state.Stations.Count; i++)
            {
                // Sichtbare, nicht nur freigeschaltete Stationen: der
                // Spieler soll die naechste, noch gesperrte Station im
                // Blick haben.
                var revealed = i == 0 || state.Stations[i - 1].IsUnlocked;
                if (revealed && stationWorldPositions.TryGetValue(i, out var position))
                {
                    Include(position);
                }
            }

            if (!any)
            {
                return;
            }

            // Eingang und der letzte Warteplatz muessen mit ins Bild --
            // sonst bleibt ausgerechnet die Warteschlange unsichtbar, die
            // die Kapazitaetsgrenze sichtbar machen soll.
            Include(RestaurantLayout.Entrance);
            Include(RestaurantLayout.QueueSlot(QueueCapacity - 1));

            var width = maxRight - minRight + CameraFramingMarginX;
            var height = maxUp - minUp + CameraFramingMarginY;
            targetOrthoSize = Mathf.Max(MinOrthographicSize, Mathf.Max(width / (2f * cam.aspect), height / 2f));

            // Zielpunkt zurueck in Weltkoordinaten. Die Komponente entlang
            // der Blickrichtung ist bei einer orthografischen Kamera
            // bedeutungslos -- ein beliebiger Punkt der Mittelachse reicht.
            targetLookAt = right * ((minRight + maxRight) / 2f)
                + up * ((minUp + maxUp) / 2f)
                + forward * (forwardSum / pointCount);
        }

        /// <summary>Weiche Kamerafahrt Richtung targetOrthoSize/targetLookAt, jeden Frame aus Update() aufgerufen -- siehe RecomputeCameraTarget fuer die Zielwerte.</summary>
        private void ApplyCameraFraming()
        {
            if (Camera.main == null)
            {
                return;
            }

            var cam = Camera.main;
            var t = Time.deltaTime * CameraFramingLerpSpeed;
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetOrthoSize, t);

            var lookTarget = targetLookAt;
            // Minus, nicht plus: die Kamera muss nach UNTEN, damit der
            // Blickpunkt im Bild nach oben in die freie Mitte rutscht. Mit
            // dem umgekehrten Vorzeichen sass die Theke im ersten
            // Portrait-Screenshot halb hinter der Kopfleiste.
            var screenBias = -cam.transform.up * (CameraVerticalScreenBias * 2f * cam.orthographicSize);
            var desiredPosition = lookTarget + cam.transform.rotation * CameraBackOffset + screenBias;
            cam.transform.position = Vector3.Lerp(cam.transform.position, desiredPosition, t);
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

            ServeGuest(i, visit, earned, burstPosition);

            if (visit.SteamEffect != null)
            {
                Destroy(visit.SteamEffect);
            }

            guestAtStation.Remove(i);

            RefreshUi();
            FlashHeader();
            PlaySfx("sfx-produce");
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

            var floor = GameObject.Find("InteriorFloor");
            if (floor != null && floor.TryGetComponent<MeshRenderer>(out var floorRenderer))
            {
                floorRenderer.sharedMaterial.SetColor(BaseColorId, theme.Ground);
            }

            // sharedMaterial waere hier falsch: die Wandsegmente kommen aus
            // dem Kenney Furniture Kit und teilen sich EIN Material mit
            // saemtlichen anderen Moebeln der Szene -- eine Renovierung
            // haette Theke, Geraete und Stuehle gleich mit umgefaerbt.
            // .material erzeugt pro Renderer eine Instanz; zur Laufzeit
            // (nur dort laeuft diese Methode) ist das der richtige Weg.
            //
            // Segmentanzahl haengt an der gemessenen Wandbreite (siehe
            // CIBuild.BuildBackWall) und ist deshalb nicht mehr fix 4 --
            // grosszuegig hochzaehlen und fehlende Namen ueberspringen ist
            // billiger als die Zahl an zwei Stellen zu pflegen.
            for (var i = 0; i < 24; i++)
            {
                var wall = GameObject.Find($"Wall_{i}");
                if (wall == null)
                {
                    continue;
                }

                foreach (var wallRenderer in wall.GetComponentsInChildren<MeshRenderer>())
                {
                    wallRenderer.material.SetColor(BaseColorId, theme.Wall);
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

            // Dasselbe fuer die laufende Gast-Simulation: ein Gast, der an
            // einer soeben wieder gesperrten Station wartet, kann nie
            // bedient werden und wuerde nur seine Geduld abwarten, um
            // anschliessend Ruf zu kosten.
            foreach (var guest in FindObjectsByType<GuestMover>(FindObjectsSortMode.None))
            {
                Destroy(guest.gameObject);
            }

            foreach (var visit in guestAtStation.Values)
            {
                if (visit.SteamEffect != null)
                {
                    Destroy(visit.SteamEffect);
                }
            }

            guestAtStation.Clear();
            guestQueue.Clear();

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
            var guestFlow = EffectiveGuestFlow();
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

            // Kopfzeile: nur noch die eine Zahl, um die sich alles dreht.
            // Der vorherige fuenfzeilige Textblock hat jede Groesse gleich
            // wichtig aussehen lassen -- Umsatz, Marketingstufe und
            // naechstes Ziel standen unterschiedslos untereinander.
            headerLabel.text = NumberFormat.Format(revenue);

            statsLabel.text =
                $"{LocationTheme.For(state.CurrentLocation).Name}    Ruf {state.Reputation:0}/100    x{PrestigeMultiplier():F2} Ertrag"
                + $"\nGaeste/Min {NumberFormat.Format(guestFlow)}    Plaetze {guestAtStation.Count}/{Mathf.Max(unlockedCount, 1)}    Schlange {guestQueue.Count}/{QueueCapacity}"
                + (rushRemaining > 0f ? $"    RUSH {rushRemaining:0}s" : string.Empty);

            // Fortschrittsbalken statt reinem Text: PLANv3 Phase D wollte ein
            // sichtbares naechstes Ziel -- ein Ziel, dessen Naeherkommen man
            // sieht, zieht deutlich staerker als eine Zahl, die man mit einer
            // anderen Zahl vergleichen muss.
            if (nextGoalIndex < 0)
            {
                goalLabel.text = "Alle Stationen freigeschaltet -- Zeit zu renovieren!";
                goalFill.fillAmount = 1f;
            }
            else
            {
                var goalDef = StationCatalog.All[nextGoalIndex];
                var goalCost = state.Stations[nextGoalIndex].UnlockCost(goalDef);
                goalLabel.text = $"Naechstes Ziel: {goalDef.Name}   {NumberFormat.Format(revenue)} / {NumberFormat.Format(goalCost)}";
                goalFill.fillAmount = goalCost > BigDouble.Zero
                    ? Mathf.Clamp01((float)(revenue / goalCost).ToDouble())
                    : 1f;
            }

            marketingButtonLabel.text = $"Marketing Lv. {state.MarketingLevel}\n{NumberFormat.Format(marketingCost)}";
            marketingButtonRef.interactable = revenue >= marketingCost;
            marketingButtonImage.color = marketingButtonRef.interactable ? AffordableButtonColor : DefaultButtonColor;

            var prestigeGain = Prestige.StarsGainedFromReset(lifetimeRevenue, PrestigeK, prestigeStars);
            prestigeButtonLabel.text = $"Renovieren  +{NumberFormat.Format(prestigeGain)}\n{NumberFormat.Format(prestigeStars)} Punkte";
            prestigeButtonRef.interactable = prestigeGain > BigDouble.Zero;
            prestigeButtonImage.color = prestigeButtonRef.interactable ? AffordableButtonColor : DefaultButtonColor;
        }

        /// <summary>
        /// PLANv3.md Phase F ("Kein Offline-Dialog... in jedem Genre-
        /// Vertreter der zentrale Moment beim Oeffnen"): modales Overlay
        /// ueber dem bereits gebauten Haupt-UI, nicht Teil des Scroll-
        /// Inhalts -- eigener Backdrop + zentrierte Karte, schliesst sich
        /// per Tap auf "Einsammeln" (das Geld selbst ist zu diesem Zeitpunkt
        /// laengst gutgeschrieben, siehe ApplyOfflineEarnings -- der Dialog
        /// ist die Bestaetigung, keine Gate-Mechanik).
        /// </summary>
        private void ShowOfflineEarningsDialog(BigDouble earned, double minutes)
        {
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                return;
            }

            var backdrop = new GameObject("OfflineEarningsBackdrop", typeof(Image));
            backdrop.transform.SetParent(canvas.transform, false);
            StretchToFillParent(backdrop.GetComponent<RectTransform>());
            backdrop.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

            var panelGo = new GameObject("OfflineEarningsPanel", typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panelGo.transform.SetParent(backdrop.transform, false);
            var panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(760, 0);

            var panelImage = panelGo.GetComponent<Image>();
            var panelSprite = Resources.Load<Sprite>("UI/panel-rectangle");
            if (panelSprite != null)
            {
                panelImage.sprite = panelSprite;
                panelImage.type = Image.Type.Sliced;
            }

            panelImage.color = Color.white;

            var layout = panelGo.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(40, 40, 40, 40);
            layout.spacing = 16;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childAlignment = TextAnchor.MiddleCenter;

            var fitter = panelGo.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var titleGo = new GameObject("Title", typeof(Text), typeof(LayoutElement));
            titleGo.transform.SetParent(panelGo.transform, false);
            var titleText = titleGo.GetComponent<Text>();
            titleText.font = Resources.Load<Font>("Fonts/Fredoka");
            titleText.fontSize = 34;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = Color.black;
            titleText.text = "Waehrend du weg warst...";
            titleGo.GetComponent<LayoutElement>().preferredHeight = 56;

            var amountGo = new GameObject("Amount", typeof(Text), typeof(LayoutElement));
            amountGo.transform.SetParent(panelGo.transform, false);
            var amountText = amountGo.GetComponent<Text>();
            amountText.font = Resources.Load<Font>("Fonts/Fredoka");
            amountText.fontSize = 46;
            amountText.alignment = TextAnchor.MiddleCenter;
            amountText.color = new Color(0.18f, 0.5f, 0.22f);
            amountText.text = $"+{NumberFormat.Format(earned)}";
            amountGo.GetComponent<LayoutElement>().preferredHeight = 66;

            var subGo = new GameObject("Sub", typeof(Text), typeof(LayoutElement));
            subGo.transform.SetParent(panelGo.transform, false);
            var subText = subGo.GetComponent<Text>();
            subText.font = Resources.Load<Font>("Fonts/Fredoka");
            subText.fontSize = 22;
            subText.alignment = TextAnchor.MiddleCenter;
            subText.color = new Color(0.4f, 0.4f, 0.4f);
            subText.text = $"{FormatOfflineDuration(minutes)} offline";
            subGo.GetComponent<LayoutElement>().preferredHeight = 36;

            var collectButton = CreateButton(panelGo.transform, "Einsammeln", () => Destroy(backdrop), preferredHeight: 72);
            collectButton.GetComponent<Image>().color = AffordableButtonColor;

            PlaySfx("sfx-milestone");
        }

        private static string FormatOfflineDuration(double minutes)
        {
            return minutes < 60 ? $"{minutes:F0} Minuten" : $"{minutes / 60.0:F1} Stunden";
        }

        /// <summary>
        /// PLANv3.md Phase F ("Einstellungen: Ton, Mute, Save zuruecksetzen").
        /// Gleiches Backdrop+Panel-Muster wie ShowOfflineEarningsDialog,
        /// bewusst separat statt geteilter Hilfsmethode -- ein Refactor der
        /// bereits verifizierten Offline-Dialog-Konstruktion haette dort ein
        /// Regressionsrisiko ohne echten Mehrwert, drei Modal-Bloecke sind
        /// hier billiger als das Risiko.
        /// </summary>
        private void OpenSettings()
        {
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                return;
            }

            var backdrop = new GameObject("SettingsBackdrop", typeof(Image));
            backdrop.transform.SetParent(canvas.transform, false);
            StretchToFillParent(backdrop.GetComponent<RectTransform>());
            backdrop.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

            var panelGo = new GameObject("SettingsPanel", typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panelGo.transform.SetParent(backdrop.transform, false);
            var panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(760, 0);

            var panelImage = panelGo.GetComponent<Image>();
            var panelSprite = Resources.Load<Sprite>("UI/panel-rectangle");
            if (panelSprite != null)
            {
                panelImage.sprite = panelSprite;
                panelImage.type = Image.Type.Sliced;
            }

            panelImage.color = Color.white;

            var layout = panelGo.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(40, 40, 40, 40);
            layout.spacing = 16;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childAlignment = TextAnchor.MiddleCenter;

            panelGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var titleGo = new GameObject("Title", typeof(Text), typeof(LayoutElement));
            titleGo.transform.SetParent(panelGo.transform, false);
            var titleText = titleGo.GetComponent<Text>();
            titleText.font = Resources.Load<Font>("Fonts/Fredoka");
            titleText.fontSize = 34;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = Color.black;
            titleText.text = "Einstellungen";
            titleGo.GetComponent<LayoutElement>().preferredHeight = 56;

            // Servicebilanz: die beiden Zahlen, aus denen sich der Ruf
            // ergibt (BalancingCore.Reputation). In der Kopfzeile steht nur
            // der aktuelle Ruf-Wert -- warum er dort steht, wo er steht,
            // laesst sich ohne diese Bilanz nicht nachvollziehen.
            var served = state.GuestsServed;
            var lost = state.GuestsLost;
            var quota = served + lost > 0 ? 100.0 * served / (served + lost) : 100.0;

            var statsGo = new GameObject("Stats", typeof(Text), typeof(LayoutElement));
            statsGo.transform.SetParent(panelGo.transform, false);
            var statsText = statsGo.GetComponent<Text>();
            statsText.font = Resources.Load<Font>("Fonts/Fredoka");
            statsText.fontSize = 22;
            statsText.alignment = TextAnchor.MiddleCenter;
            statsText.color = new Color(0.35f, 0.35f, 0.35f);
            statsText.text = $"Ruf: {state.Reputation:0}/100  (x{Reputation.FlowMultiplier(state.Reputation):F2} Gaestestrom)"
                + $"\nBedient: {served}    Verloren: {lost}    Quote: {quota:0}%";
            statsGo.GetComponent<LayoutElement>().preferredHeight = 66;

            var muted = PlayerPrefs.GetInt(MutedPrefKey, 0) == 1;
            var soundButton = CreateButton(panelGo.transform, muted ? "Ton: Aus" : "Ton: An", () => { }, preferredHeight: 70);
            soundButton.GetComponent<Image>().color = AffordableButtonColor;
            soundButton.onClick.AddListener(() =>
            {
                var nowMuted = PlayerPrefs.GetInt(MutedPrefKey, 0) == 1;
                var willBeMuted = !nowMuted;
                PlayerPrefs.SetInt(MutedPrefKey, willBeMuted ? 1 : 0);
                AudioListener.volume = willBeMuted ? 0f : 1f;
                soundButton.GetComponentInChildren<Text>().text = willBeMuted ? "Ton: Aus" : "Ton: An";
            });

            var resetButton = CreateButton(panelGo.transform, "Spielstand zuruecksetzen", () => { }, preferredHeight: 70);
            resetButton.GetComponent<Image>().color = new Color(0.85f, 0.4f, 0.35f);
            resetButton.onClick.AddListener(() =>
                ShowConfirmDialog(
                    "Spielstand zuruecksetzen?",
                    "Das kann nicht rueckgaengig gemacht werden.",
                    "Ja, loeschen",
                    ResetSave));

            CreateButton(panelGo.transform, "Schliessen", () => Destroy(backdrop), preferredHeight: 70);
        }

        /// <summary>Generisches Ja/Nein-Modal fuer destruktive Aktionen -- aktuell nur vom Reset-Button aus OpenSettings verwendet.</summary>
        private void ShowConfirmDialog(string title, string message, string confirmLabel, UnityEngine.Events.UnityAction onConfirm)
        {
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                return;
            }

            var backdrop = new GameObject("ConfirmBackdrop", typeof(Image));
            backdrop.transform.SetParent(canvas.transform, false);
            StretchToFillParent(backdrop.GetComponent<RectTransform>());
            backdrop.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.8f);

            var panelGo = new GameObject("ConfirmPanel", typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panelGo.transform.SetParent(backdrop.transform, false);
            var panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(700, 0);

            var panelImage = panelGo.GetComponent<Image>();
            var panelSprite = Resources.Load<Sprite>("UI/panel-rectangle");
            if (panelSprite != null)
            {
                panelImage.sprite = panelSprite;
                panelImage.type = Image.Type.Sliced;
            }

            panelImage.color = Color.white;

            var layout = panelGo.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(40, 40, 40, 40);
            layout.spacing = 16;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childAlignment = TextAnchor.MiddleCenter;

            panelGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var titleGo = new GameObject("Title", typeof(Text), typeof(LayoutElement));
            titleGo.transform.SetParent(panelGo.transform, false);
            var titleText = titleGo.GetComponent<Text>();
            titleText.font = Resources.Load<Font>("Fonts/Fredoka");
            titleText.fontSize = 30;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = Color.black;
            titleText.text = title;
            titleGo.GetComponent<LayoutElement>().preferredHeight = 50;

            var messageGo = new GameObject("Message", typeof(Text), typeof(LayoutElement));
            messageGo.transform.SetParent(panelGo.transform, false);
            var messageText = messageGo.GetComponent<Text>();
            messageText.font = Resources.Load<Font>("Fonts/Fredoka");
            messageText.fontSize = 22;
            messageText.alignment = TextAnchor.MiddleCenter;
            messageText.color = new Color(0.4f, 0.4f, 0.4f);
            messageText.text = message;
            messageGo.GetComponent<LayoutElement>().preferredHeight = 40;

            var confirmButton = CreateButton(panelGo.transform, confirmLabel, () =>
            {
                onConfirm();
                Destroy(backdrop);
            }, preferredHeight: 70);
            confirmButton.GetComponent<Image>().color = new Color(0.85f, 0.4f, 0.35f);

            CreateButton(panelGo.transform, "Abbrechen", () => Destroy(backdrop), preferredHeight: 70);
        }

        /// <summary>Loescht nur den lokalen Save (siehe SaveSystem.DeleteSaveFile) und laedt die Szene neu, damit InitializeGame() sauber mit einem frischen GameState startet.</summary>
        private void ResetSave()
        {
            SaveSystem.DeleteSaveFile();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>
        /// Nutzer-Feedback ("Ich bleibe dabei, dass ich die buttons unten
        /// nicht mehr haben will. Das soll alles ueber Dialogmenues
        /// geschehen, also durch Klicken"): ersetzt die vorherige
        /// dauerhafte Kaufen/Ausstattung/Manager-Zeile pro Station in der
        /// Liste. Ausgeloest durch HandleStationTap, wenn an der
        /// angetippten Station kein Gast wartet (wartet einer, bedient der
        /// Tap ihn stattdessen, siehe K2). Kauf-Buttons bauen den Dialog
        /// bewusst komplett neu auf (Destroy + erneuter Aufruf) statt die
        /// Werte in-place zu aktualisieren -- einfacher und robuster als
        /// ein Live-Refresh-Pfad nur fuer diesen einen Dialog.
        /// </summary>
        private void OpenStationDialog(int stationIndex)
        {
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                return;
            }

            var def = StationCatalog.All[stationIndex];
            var station = state.Stations[stationIndex];

            var backdrop = new GameObject("StationDialogBackdrop", typeof(Image));
            backdrop.transform.SetParent(canvas.transform, false);
            StretchToFillParent(backdrop.GetComponent<RectTransform>());
            backdrop.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

            var panelGo = new GameObject("StationDialogPanel", typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panelGo.transform.SetParent(backdrop.transform, false);
            var panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(780, 0);

            var panelImage = panelGo.GetComponent<Image>();
            var panelSprite = Resources.Load<Sprite>("UI/panel-rectangle");
            if (panelSprite != null)
            {
                panelImage.sprite = panelSprite;
                panelImage.type = Image.Type.Sliced;
            }

            panelImage.color = Color.white;

            var layout = panelGo.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(40, 32, 40, 40);
            layout.spacing = 14;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childAlignment = TextAnchor.MiddleCenter;

            panelGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var titleGo = new GameObject("Title", typeof(Text), typeof(LayoutElement));
            titleGo.transform.SetParent(panelGo.transform, false);
            var titleText = titleGo.GetComponent<Text>();
            titleText.font = Resources.Load<Font>("Fonts/Fredoka");
            titleText.fontSize = 34;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = Color.black;
            titleText.text = def.Name;
            titleGo.GetComponent<LayoutElement>().preferredHeight = 54;

            var icon = Resources.Load<Sprite>($"Icons/{StationIconNames[stationIndex]}");
            if (icon != null)
            {
                var iconGo = new GameObject("Icon", typeof(Image), typeof(LayoutElement));
                iconGo.transform.SetParent(panelGo.transform, false);
                var iconImage = iconGo.GetComponent<Image>();
                iconImage.sprite = icon;
                iconImage.preserveAspect = true;
                iconGo.GetComponent<LayoutElement>().preferredHeight = 90;
            }

            var infoText = station.IsUnlocked
                ? $"Preis Lv.{station.PriceLevel} / Ausstattung Lv.{station.EquipmentLevel}"
                    + $"\nErtrag/Verkauf: {NumberFormat.Format(station.YieldPerSale(def))}  |  Zyklus: {station.CycleSeconds(def):0.0}s"
                    + (station.HasManager ? "\nManager: aktiv" : $"\nManager: {NumberFormat.Format(def.ManagerCost)}")
                : "Noch nicht freigeschaltet";

            var infoGo = new GameObject("Info", typeof(Text), typeof(LayoutElement));
            infoGo.transform.SetParent(panelGo.transform, false);
            var infoTextComponent = infoGo.GetComponent<Text>();
            infoTextComponent.font = Resources.Load<Font>("Fonts/Fredoka");
            infoTextComponent.fontSize = 22;
            infoTextComponent.alignment = TextAnchor.MiddleCenter;
            infoTextComponent.color = new Color(0.35f, 0.35f, 0.35f);
            infoTextComponent.text = infoText;
            infoGo.GetComponent<LayoutElement>().preferredHeight = station.IsUnlocked ? 76 : 32;

            void Reopen()
            {
                Destroy(backdrop);
                OpenStationDialog(stationIndex);
            }

            var buyCost = station.IsUnlocked ? station.NextPriceUpgradeCost(def) : station.UnlockCost(def);
            var buyLabel = station.IsUnlocked ? $"Preis erhoehen ({NumberFormat.Format(buyCost)})" : $"Kaufen ({NumberFormat.Format(buyCost)})";
            var buyButton = CreateButton(panelGo.transform, buyLabel, () =>
            {
                BuyStation(stationIndex);
                Reopen();
            }, preferredHeight: 70);
            var canAffordBuy = revenue >= buyCost;
            buyButton.interactable = canAffordBuy;
            buyButton.GetComponent<Image>().color = !station.IsUnlocked && stationIndex == 0
                ? GuidedButtonColor
                : (canAffordBuy ? AffordableButtonColor : DefaultButtonColor);

            if (station.IsUnlocked)
            {
                var equipCost = station.NextEquipmentUpgradeCost(def);
                var equipButton = CreateButton(panelGo.transform, $"Ausstattung ({NumberFormat.Format(equipCost)})", () =>
                {
                    UpgradeEquipment(stationIndex);
                    Reopen();
                }, preferredHeight: 70);
                var canAffordEquip = revenue >= equipCost;
                equipButton.interactable = canAffordEquip;
                equipButton.GetComponent<Image>().color = canAffordEquip ? AffordableButtonColor : DefaultButtonColor;

                if (!station.HasManager)
                {
                    var managerButton = CreateButton(panelGo.transform, $"Manager ({NumberFormat.Format(def.ManagerCost)})", () =>
                    {
                        BuyManager(stationIndex);
                        Reopen();
                    }, preferredHeight: 70);
                    var canAffordManager = revenue >= def.ManagerCost;
                    managerButton.interactable = canAffordManager;
                    managerButton.GetComponent<Image>().color = canAffordManager ? AffordableButtonColor : DefaultButtonColor;
                }
            }

            var closeButton = CreateButton(panelGo.transform, "Schliessen", () => Destroy(backdrop), preferredHeight: 60);
            closeButton.GetComponent<Image>().color = DefaultButtonColor;
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

            canvasTransform = canvasObject.transform;
            canvasRect = canvasObject.GetComponent<RectTransform>();

            new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));

            BuildTopBar();
            BuildGoalBar();
            BuildRushBanner();
            BuildBottomBar();
        }

        /// <summary>
        /// Kopfleiste: Umsatz gross, Kontext klein, Einstellungen als Ecke.
        /// Loest den zentralen Layoutfehler der Vorversion -- eine
        /// Scroll-Liste ueber der unteren Bildschirmhaelfte, die 55 % der
        /// Flaeche fuer statischen Text verbraucht hat, waehrend das
        /// eigentliche Spiel (die 3D-Szene, PLANv2.md Abschnitt 1.2: "Die
        /// Szene ist das UI") in den Rest gequetscht war.
        /// </summary>
        private void BuildTopBar()
        {
            var bar = CreateHudPanel("TopBar", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-24f, 280f), new Vector2(0f, -14f));

            headerLabel = CreateHudText(bar, "Money", new Vector2(0f, 0.44f), new Vector2(1f, 1f),
                new Vector2(34f, 0f), new Vector2(-210f, -14f), fontSize: 76, TextAnchor.MiddleLeft, Color.black);

            statsLabel = CreateHudText(bar, "Stats", new Vector2(0f, 0f), new Vector2(1f, 0.44f),
                new Vector2(36f, 14f), new Vector2(-34f, 0f), fontSize: 24, TextAnchor.MiddleLeft,
                new Color(0.35f, 0.35f, 0.38f));

            var settingsButton = CreateButton(bar, "Optionen", OpenSettings, preferredHeight: 84);
            settingsButton.GetComponent<Image>().color = DefaultButtonColor;
            var settingsRect = settingsButton.GetComponent<RectTransform>();
            settingsRect.anchorMin = new Vector2(1f, 1f);
            settingsRect.anchorMax = new Vector2(1f, 1f);
            settingsRect.pivot = new Vector2(1f, 1f);
            settingsRect.sizeDelta = new Vector2(176f, 84f);
            settingsRect.anchoredPosition = new Vector2(-22f, -18f);
            settingsButton.GetComponentInChildren<Text>().fontSize = 24;
        }

        private void BuildGoalBar()
        {
            var bar = CreateHudPanel("GoalBar", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-24f, 96f), new Vector2(0f, -306f));

            var trackGo = new GameObject("Track", typeof(Image));
            trackGo.transform.SetParent(bar, false);
            var trackRect = trackGo.GetComponent<RectTransform>();
            trackRect.anchorMin = Vector2.zero;
            trackRect.anchorMax = Vector2.one;
            trackRect.offsetMin = new Vector2(18f, 16f);
            trackRect.offsetMax = new Vector2(-18f, -16f);
            var track = trackGo.GetComponent<Image>();
            track.color = new Color(0f, 0f, 0f, 0.12f);
            track.raycastTarget = false;

            var fillGo = new GameObject("Fill", typeof(Image));
            fillGo.transform.SetParent(trackGo.transform, false);
            var fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            goalFill = fillGo.GetComponent<Image>();
            // Ein Image OHNE Sprite ignoriert fillAmount und zeichnet immer
            // die volle Flaeche -- der Balken stand deshalb im ersten
            // Testlauf bei 0 Umsatz schon komplett auf Gruen. Irgendein
            // Sprite muss also gesetzt sein, damit Image.Type.Filled
            // ueberhaupt greift.
            goalFill.sprite = Resources.Load<Sprite>("UI/panel-rectangle");
            goalFill.type = Image.Type.Filled;
            goalFill.fillMethod = Image.FillMethod.Horizontal;
            goalFill.color = new Color(0.45f, 0.8f, 0.45f, 0.9f);
            goalFill.raycastTarget = false;

            goalLabel = CreateHudText(bar, "GoalText", Vector2.zero, Vector2.one,
                new Vector2(26f, 0f), new Vector2(-26f, 0f), fontSize: 26, TextAnchor.MiddleCenter, Color.black);
        }

        private void BuildRushBanner()
        {
            var bar = CreateHudPanel("RushBanner", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(720f, 78f), new Vector2(0f, -424f));
            bar.GetComponent<Image>().color = new Color(1f, 0.78f, 0.35f, 0.96f);

            CreateHudText(bar, "RushText", Vector2.zero, Vector2.one, new Vector2(20f, 0f), new Vector2(-20f, 0f),
                fontSize: 32, TextAnchor.MiddleCenter, new Color(0.35f, 0.2f, 0f)).text = "RUSH HOUR -- alle Plaetze besetzen!";

            rushBanner = bar.gameObject;
            rushBanner.SetActive(false);
        }

        /// <summary>
        /// Zwei globale Aktionen am unteren Rand -- bewusst NUR globale.
        /// Alles, was eine einzelne Station betrifft, laeuft ausschliesslich
        /// ueber das Antippen der Station und den daraufhin geoeffneten
        /// Dialog (Nutzer-Feedback, siehe HandleStationTap/OpenStationDialog).
        /// </summary>
        private void BuildBottomBar()
        {
            var bar = CreateHudPanel("BottomBar", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-24f, 176f), new Vector2(0f, 16f));

            var layout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 16, 16);
            layout.spacing = 16;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            marketingButtonRef = CreateButton(bar, "Marketing", BuyMarketing, preferredHeight: 144);
            marketingButtonImage = marketingButtonRef.GetComponent<Image>();
            marketingButtonLabel = marketingButtonRef.GetComponentInChildren<Text>();

            prestigeButtonRef = CreateButton(bar, "Renovieren", PrestigeReset, preferredHeight: 144);
            prestigeButtonImage = prestigeButtonRef.GetComponent<Image>();
            prestigeButtonLabel = prestigeButtonRef.GetComponentInChildren<Text>();
        }

        /// <summary>Kenney-9-Slice-Karte als HUD-Flaeche -- gleiches Grundmuster wie CreateButton (Image aussen, Inhalt innen), aber frei positioniert statt in einer Layout-Gruppe.</summary>
        private RectTransform CreateHudPanel(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta, Vector2 anchoredPosition)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(canvasTransform, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = sizeDelta;
            rect.anchoredPosition = anchoredPosition;

            var image = go.GetComponent<Image>();
            var sprite = Resources.Load<Sprite>("UI/panel-rectangle");
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
            }

            image.color = new Color(1f, 1f, 1f, 0.96f);
            return rect;
        }

        private static Text CreateHudText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, int fontSize, TextAnchor alignment, Color color)
        {
            var go = new GameObject(name, typeof(Text));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            var text = go.GetComponent<Text>();
            text.font = Resources.Load<Font>("Fonts/Fredoka");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private static void StretchToFillParent(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick, float preferredHeight)
        {
            var go = new GameObject(label, typeof(Image), typeof(Button), typeof(LayoutElement), typeof(ButtonPunch));
            go.transform.SetParent(parent, false);

            // PLANv3.md Phase E: Kenney UI Pack statt flacher Farbflaeche --
            // 9-Slice (Image.Type.Sliced) haelt die runden Ecken/den Tiefen-
            // Rand unverzerrt, egal wie breit der Button durch das Auto-
            // Layout am Ende wird. Faellt die Sprite-Zuweisung aus
            // irgendeinem Grund aus (Asset fehlt), bleibt die reine
            // Farbflaeche als Fallback -- kein kaputter, unsichtbarer Button.
            var buttonImage = go.GetComponent<Image>();
            var buttonSprite = Resources.Load<Sprite>("UI/button-rectangle");
            if (buttonSprite != null)
            {
                buttonImage.sprite = buttonSprite;
                buttonImage.type = Image.Type.Sliced;
            }

            buttonImage.color = new Color(0.8f, 0.8f, 0.8f);

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
