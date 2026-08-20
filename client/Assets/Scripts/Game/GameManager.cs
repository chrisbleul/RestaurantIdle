using System;
using System.Collections.Generic;
using BalancingCore;
using BreakInfinity;
using UnityEngine;
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
        private GameState state;
        private BigDouble revenue;
        private BigDouble lifetimeRevenue;

        private Text headerLabel;
        private Button marketingButtonRef;
        private readonly List<StationRow> rows = new();

        private struct StationRow
        {
            public Text Label;
            public Button BuyButton;
            public Button ProduceButton;
            public Button ManagerButton;
        }

        private void Start()
        {
            state = SaveSystem.LoadOrCreate();
            revenue = BigDouble.Parse(state.RevenueString);
            lifetimeRevenue = BigDouble.Parse(state.LifetimeRevenueString);

            ApplyOfflineEarnings();
            BuildUi();
            RefreshUi();
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

        private void ApplyOfflineEarnings()
        {
            if (state.LastSavedAtUnixSeconds == 0)
            {
                return;
            }

            var lastSeen = DateTimeOffset.FromUnixTimeSeconds(state.LastSavedAtUnixSeconds);
            var offlineDuration = DateTimeOffset.UtcNow - lastSeen;
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

        private void Update()
        {
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
        }

        private void OnApplicationQuit() => Persist();

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                Persist();
            }
        }

        private void Persist()
        {
            state.RevenueString = revenue.ToString();
            state.LifetimeRevenueString = lifetimeRevenue.ToString();
            SaveSystem.Save(state);
        }

        private void ProduceNow(int i)
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
        }

        private void RefreshUi()
        {
            var guestFlow = GuestFlow.GuestFlowAt(state.MarketingLevel);
            var potential = SumManagedYieldPerSecond();
            var factor = CurrentCapacityFactor();
            var marketingCost = GuestFlow.NextMarketingCost(state.MarketingLevel);

            headerLabel.text = $"Umsatz: {revenue}\nLifetime: {lifetimeRevenue}"
                + $"\nGaestestrom: {guestFlow}  (Auslastung: {factor:P0} von {potential}/s)"
                + $"\nMarketing Stufe {state.MarketingLevel} -- naechste Stufe: {marketingCost}";
            marketingButtonRef.interactable = revenue >= marketingCost;

            for (var i = 0; i < rows.Count; i++)
            {
                var def = StationCatalog.All[i];
                var station = state.Stations[i];
                var row = rows[i];

                row.Label.text = $"{def.Name}: {station.OwnedCount}x"
                    + $"\nKosten: {station.NextCost(def)}  |  Ertrag/Zyklus: {station.YieldPerCycle(def)} ({def.CycleSeconds}s)"
                    + (station.HasManager ? "\nManager: aktiv" : $"\nManager: {def.ManagerCost}");

                row.BuyButton.interactable = revenue >= station.NextCost(def);
                row.ProduceButton.interactable = station.OwnedCount > 0;
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
            StretchToFillParent(scrollViewGo.GetComponent<RectTransform>());
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

            for (var i = 0; i < StationCatalog.All.Count; i++)
            {
                var index = i; // lokale Kopie fuer die Closures unten
                var label = CreateLabel(contentGo.transform, preferredHeight: 80);
                var buyButton = CreateButton(contentGo.transform, "Kaufen", () => BuyStation(index), preferredHeight: 60);
                var produceButton = CreateButton(contentGo.transform, "Produzieren", () => ProduceNow(index), preferredHeight: 60);
                var managerButton = CreateButton(contentGo.transform, "Manager", () => BuyManager(index), preferredHeight: 60);

                rows.Add(new StationRow
                {
                    Label = label,
                    BuyButton = buyButton,
                    ProduceButton = produceButton,
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
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 28;
            text.alignment = TextAnchor.UpperLeft;
            text.color = Color.black;

            var layoutElement = go.GetComponent<LayoutElement>();
            layoutElement.preferredHeight = preferredHeight;
            layoutElement.flexibleWidth = 1;

            return text;
        }

        private static Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick, float preferredHeight)
        {
            var go = new GameObject(label, typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.8f, 0.8f, 0.8f);

            var button = go.GetComponent<Button>();
            button.onClick.AddListener(onClick);

            var layoutElement = go.GetComponent<LayoutElement>();
            layoutElement.preferredHeight = preferredHeight;
            layoutElement.flexibleWidth = 1;

            var labelGo = new GameObject("Text", typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            StretchToFillParent(labelGo.GetComponent<RectTransform>());
            var labelText = labelGo.GetComponent<Text>();
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelText.fontSize = 28;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = Color.black;
            labelText.text = label;

            return button;
        }
    }
}
