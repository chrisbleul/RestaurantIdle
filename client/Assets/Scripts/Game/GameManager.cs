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

        private void BuildUi()
        {
            var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // Ohne das skaliert die UI nicht mit der tatsaechlichen Aufloesung
            // (Unity-Default ist "Constant Pixel Size", 1 UI-Einheit = 1
            // Bildschirmpixel) -- auf einem Handy-Canvas landet dann nur ein
            // Ausschnitt der fuer 1080x1920 gedachten Positionen im Sichtfeld.
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            var eventSystemObject = new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
            eventSystemObject.transform.SetParent(null);

            headerLabel = CreateLabel(canvasObject.transform, new Vector2(0, 480), 500, 140);

            var marketingButton = CreateButton(canvasObject.transform, "Marketing kaufen", new Vector2(0, 390), BuyMarketing);
            marketingButtonRef = marketingButton;

            var y = 320f;
            for (var i = 0; i < StationCatalog.All.Count; i++)
            {
                var index = i; // lokale Kopie fuer die Closures unten
                var label = CreateLabel(canvasObject.transform, new Vector2(-150, y), 300, 70);
                var buyButton = CreateButton(canvasObject.transform, "Kaufen", new Vector2(120, y + 15), () => BuyStation(index), width: 140, height: 30);
                var produceButton = CreateButton(canvasObject.transform, "Produzieren", new Vector2(280, y + 15), () => ProduceNow(index), width: 140, height: 30);
                var managerButton = CreateButton(canvasObject.transform, "Manager", new Vector2(120, y - 20), () => BuyManager(index), width: 140, height: 30);

                rows.Add(new StationRow
                {
                    Label = label,
                    BuyButton = buyButton,
                    ProduceButton = produceButton,
                    ManagerButton = managerButton,
                });

                y -= 80f;
            }
        }

        private static Text CreateLabel(Transform parent, Vector2 anchoredPosition, float width, float height)
        {
            var go = new GameObject("Label", typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            // Per Code erzeugte RectTransforms sind NICHT zentriert verankert
            // (anders als ueber das Editor-Menue) -- ohne das hier landet jedes
            // Element an einer unerwarteten Stelle relativ zur Ecke des Parents.
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = anchoredPosition;

            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 14;
            text.alignment = TextAnchor.UpperLeft;
            text.color = Color.black;
            return text;
        }

        private static Button CreateButton(Transform parent, string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction onClick, float width = 260, float height = 60)
        {
            var go = new GameObject(label, typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = anchoredPosition;
            go.GetComponent<Image>().color = new Color(0.85f, 0.85f, 0.85f);

            var button = go.GetComponent<Button>();
            button.onClick.AddListener(onClick);

            var labelText = CreateLabel(go.transform, Vector2.zero, width, height);
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.text = label;

            return button;
        }
    }
}
