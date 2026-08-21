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
        // Weltposition pro Station, fuer den Muenz-Burst bei UI-Button-Klicks
        // (dort gibt es keinen Raycast-Treffpunkt wie beim 3D-Tap). Einmalig
        // aus den StationHotspot-Objekten in der Szene aufgebaut.
        private readonly Dictionary<int, Vector3> stationWorldPositions = new();

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
            public Button ManagerButton;
        }

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

            foreach (var hotspot in FindObjectsByType<StationHotspot>(FindObjectsSortMode.None))
            {
                stationWorldPositions[hotspot.StationIndex] = hotspot.transform.position;
            }

            RefreshUi();

            // Deckt insbesondere den Umzug eines bisher rein lokalen
            // Spielstands aufs Backend ab (erster Start nach dieser Aenderung).
            Persist();

            isInitialized = true;
        }

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

        private double CurrentCapacityFactor() =>
            GuestFlow.CapacityFactor(SumManagedYieldPerSecond(), GuestFlow.GuestFlowAt(state.MarketingLevel));

        /// <summary>Serverautoritative Variante -- offlineDuration kommt vom Backend, nicht von der lokalen Systemuhr (PLAN.md Abschnitt 8).</summary>
        private void ApplyOfflineEarnings(TimeSpan offlineDuration)
        {
            if (offlineDuration <= TimeSpan.Zero)
            {
                return;
            }

            var effectivePerSecond = SumManagedYieldPerSecond() * CurrentCapacityFactor();
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

            var factor = CurrentCapacityFactor();
            var earnedThisFrame = BigDouble.Zero;

            for (var i = 0; i < state.Stations.Count; i++)
            {
                earnedThisFrame += state.Stations[i].Tick(StationCatalog.All[i], Time.deltaTime);
            }

            if (earnedThisFrame > BigDouble.Zero)
            {
                var effective = earnedThisFrame * factor;
                revenue += effective;
                lifetimeRevenue += effective;
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
        /// Tap-Layer (PLANv2.md Abschnitt 1.3): Klick auf eine Station in der
        /// 3D-Szene loest ProduceNow aus -- der einzige Weg, manuell zu
        /// produzieren (der separate "Produzieren"-Listenbutton wurde
        /// entfernt, sobald der Tap-Layer stand: redundant, "Kaufen" und
        /// "Manager" bleiben als Listenbuttons, dafuer gibt es keine
        /// Tap-Geste). EventSystem-Check zuerst, damit Klicks auf die UI
        /// (untere Bildschirmhaelfte) nicht zusaetzlich einen 3D-Raycast
        /// auf die Szene ausloesen.
        /// </summary>
        private void HandleStationTap()
        {
            if (!Input.GetMouseButtonDown(0) || EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            if (Camera.main == null)
            {
                return;
            }

            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
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
        private const float GuestWalkDurationSeconds = 6f;
        private static readonly Vector3 GuestSpawnStart = new Vector3(-1.5f, 0.4f, -1.2f);
        private static readonly Vector3 GuestSpawnEnd = new Vector3(7f, 0.4f, -1.2f);

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

        private static void SpawnGuest()
        {
            var guest = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            guest.name = "Guest";
            guest.transform.localScale = new Vector3(0.25f, 0.4f, 0.25f);
            // Rein optisch -- ohne Collider koennten Gaeste sich zufaellig
            // vor eine Station schieben und HandleStationTap's Raycast
            // blockieren.
            Destroy(guest.GetComponent<Collider>());

            var renderer = guest.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                color = Color.HSVToRGB(UnityEngine.Random.value, 0.6f, 0.9f),
            };

            var mover = guest.AddComponent<GuestMover>();
            mover.Init(GuestSpawnStart, GuestSpawnEnd, GuestWalkDurationSeconds);
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

        private void ProduceNow(int i, Vector3? burstPosition = null)
        {
            var earned = state.Stations[i].ProduceNow(StationCatalog.All[i]);
            if (earned <= BigDouble.Zero)
            {
                return;
            }

            var effective = earned * CurrentCapacityFactor();
            revenue += effective;
            lifetimeRevenue += effective;
            RefreshUi();
            FlashHeader();
            PlaySfx("sfx-produce");

            var position = burstPosition ?? (stationWorldPositions.TryGetValue(i, out var pos) ? pos : (Vector3?)null);
            if (position.HasValue)
            {
                CoinBurst.SpawnAt(position.Value);
            }
        }

        private void BuyStation(int i)
        {
            var cost = state.Stations[i].NextCost(StationCatalog.All[i]);
            if (revenue < cost)
            {
                return;
            }

            revenue -= cost;
            state.Stations[i].Buy();
            RefreshUi();
            FlashHeader();
            PlaySfx("sfx-purchase");
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
        /// Renovierung (PLANv2.md Abschnitt 1.1/7): Reset der laufenden
        /// Runde gegen Renovierungspunkte -- mathematisch identisch zum
        /// urspruenglichen Michelin-Sterne-Prestige (PLAN.md Phase 6), nur
        /// umgedeutet. Lifetime-Umsatz bleibt bewusst erhalten -- die
        /// Punkte-Formel rechnet auf dem kumulierten Gesamtwert, nicht auf
        /// einem pro-Run-Wert (siehe Prestige.StarsGainedFromReset). Ein
        /// sichtbarer Ortswechsel (neue Location statt nur Zahlen-Reset)
        /// folgt in Phase 10, hier erstmal nur das Wording.
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
            // Gibt wie beim allerersten Start die erste Station gratis --
            // sonst waere nach dem Reset buchstaeblich kein Kauf mehr moeglich.
            SaveSystem.Normalize(state);

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
            var potential = SumManagedYieldPerSecond();
            var factor = CurrentCapacityFactor();
            var marketingCost = GuestFlow.NextMarketingCost(state.MarketingLevel);

            headerLabel.text = $"Umsatz: {NumberFormat.Format(revenue)}\nLifetime: {NumberFormat.Format(lifetimeRevenue)}"
                + $"\nGaestestrom: {NumberFormat.Format(guestFlow)}  (Auslastung: {factor:P0} von {NumberFormat.Format(potential)}/s)"
                + $"\nMarketing Stufe {state.MarketingLevel} -- naechste Stufe: {NumberFormat.Format(marketingCost)}";
            marketingButtonRef.interactable = revenue >= marketingCost;

            var prestigeGain = Prestige.StarsGainedFromReset(lifetimeRevenue, PrestigeK, prestigeStars);
            prestigeLabel.text = $"Renovierungspunkte: {NumberFormat.Format(prestigeStars)}\nNaechste Renovierung bringt: +{NumberFormat.Format(prestigeGain)}";
            prestigeButtonRef.interactable = prestigeGain > BigDouble.Zero;

            for (var i = 0; i < rows.Count; i++)
            {
                var def = StationCatalog.All[i];
                var station = state.Stations[i];
                var row = rows[i];

                row.Label.text = $"{def.Name}: {station.OwnedCount}x"
                    + $"\nKosten: {NumberFormat.Format(station.NextCost(def))}  |  Ertrag/Zyklus: {NumberFormat.Format(station.YieldPerCycle(def))} ({def.CycleSeconds}s)"
                    + (station.HasManager ? "\nManager: aktiv" : $"\nManager: {NumberFormat.Format(def.ManagerCost)}");

                row.BuyButton.interactable = revenue >= station.NextCost(def);
                row.ManagerButton.gameObject.SetActive(!station.HasManager);
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

            headerLabel = CreateLabel(contentGo.transform, preferredHeight: 130);
            marketingButtonRef = CreateButton(contentGo.transform, "Marketing kaufen", BuyMarketing, preferredHeight: 70);
            prestigeLabel = CreateLabel(contentGo.transform, preferredHeight: 80);
            prestigeButtonRef = CreateButton(contentGo.transform, "Renovieren", PrestigeReset, preferredHeight: 70);

            for (var i = 0; i < StationCatalog.All.Count; i++)
            {
                var index = i; // lokale Kopie fuer die Closures unten
                var icon = Resources.Load<Sprite>($"Icons/{StationIconNames[index]}");
                var label = CreateStationHeader(contentGo.transform, icon, preferredHeight: 80);
                var buyButton = CreateButton(contentGo.transform, "Kaufen", () => BuyStation(index), preferredHeight: 60);
                var managerButton = CreateButton(contentGo.transform, "Manager", () => BuyManager(index), preferredHeight: 60);

                rows.Add(new StationRow
                {
                    Label = label,
                    BuyButton = buyButton,
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
