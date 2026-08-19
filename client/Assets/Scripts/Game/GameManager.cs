using System;
using BalancingCore;
using BreakInfinity;
using UnityEngine;
using UnityEngine.UI;

namespace RestaurantIdle.Game
{
    /// <summary>
    /// Phase-2-Entscheidungspunkt (PLAN.md Abschnitt 7): "Macht die Kurve grau
    /// Spass?" -- eine Station, ein Upgrade, Tick-Loop mit Delta-Time, lokaler
    /// Save. Baut sein UI zur Laufzeit selbst auf (kein Art-Pass, das kommt erst
    /// in Phase 4) statt eine Szene mit verdrahteten Referenzen vorauszusetzen.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        private GameState state;
        private BigDouble revenue;
        private BigDouble lifetimeRevenue;

        private Text revenueLabel;
        private Text stationLabel;
        private Button produceButton;
        private Button buyStationButton;
        private Button buyRecipeButton;

        private void Start()
        {
            state = SaveSystem.LoadOrCreate();
            revenue = BigDouble.Parse(state.RevenueString);
            lifetimeRevenue = BigDouble.Parse(state.LifetimeRevenueString);

            ApplyOfflineEarnings();
            BuildUi();
            RefreshUi();
        }

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

            var earned = OfflineEarnings.Calculate(state.Station.YieldPerSecond, offlineDuration);
            if (earned > BigDouble.Zero)
            {
                revenue += earned;
                lifetimeRevenue += earned;
                Debug.Log($"Offline-Ertrag ({offlineDuration.TotalMinutes:F0} Min.): {earned}");
            }
        }

        private void Update()
        {
            var earned = state.Station.Tick(Time.deltaTime);
            if (earned > BigDouble.Zero)
            {
                revenue += earned;
                lifetimeRevenue += earned;
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

        private void ProduceNow()
        {
            if (state.Station.OwnedCount == 0)
            {
                return;
            }

            revenue += state.Station.YieldPerCycle;
            lifetimeRevenue += state.Station.YieldPerCycle;
            RefreshUi();
        }

        private void BuyStation()
        {
            var cost = state.Station.OwnedCount == 0 ? StationDefinition.BaseCost : state.Station.NextCost;
            if (revenue < cost)
            {
                return;
            }

            revenue -= cost;
            state.Station.Buy();
            RefreshUi();
        }

        private void BuyRecipeUpgrade()
        {
            if (state.Station.HasRecipeUpgrade || revenue < StationDefinition.RecipeUpgradeCost)
            {
                return;
            }

            revenue -= StationDefinition.RecipeUpgradeCost;
            state.Station.HasRecipeUpgrade = true;
            RefreshUi();
        }

        private void RefreshUi()
        {
            revenueLabel.text = $"Umsatz: {revenue}\nLifetime: {lifetimeRevenue}";
            stationLabel.text = $"{StationDefinition.Name}: {state.Station.OwnedCount}x"
                + $"\nNaechste Kosten: {state.Station.NextCost}"
                + $"\nErtrag/Zyklus: {state.Station.YieldPerCycle}";

            produceButton.interactable = state.Station.OwnedCount > 0;
            buyStationButton.interactable = revenue >= (state.Station.OwnedCount == 0 ? StationDefinition.BaseCost : state.Station.NextCost);
            buyRecipeButton.gameObject.SetActive(!state.Station.HasRecipeUpgrade);
            buyRecipeButton.interactable = revenue >= StationDefinition.RecipeUpgradeCost;
        }

        // -- UI-Aufbau: grauer Prototyp, kein Art-Pass (PLAN.md Abschnitt 4/7). --

        private void BuildUi()
        {
            var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var eventSystemObject = new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
            eventSystemObject.transform.SetParent(null);

            revenueLabel = CreateLabel(canvasObject.transform, new Vector2(0, 200), 320, 80);
            stationLabel = CreateLabel(canvasObject.transform, new Vector2(0, 80), 320, 100);

            produceButton = CreateButton(canvasObject.transform, "Jetzt produzieren", new Vector2(0, -40), ProduceNow);
            buyStationButton = CreateButton(canvasObject.transform, $"{StationDefinition.Name} kaufen", new Vector2(0, -120), BuyStation);
            buyRecipeButton = CreateButton(canvasObject.transform, "Rezept kaufen (x2)", new Vector2(0, -200), BuyRecipeUpgrade);
        }

        private static Text CreateLabel(Transform parent, Vector2 anchoredPosition, float width, float height)
        {
            var go = new GameObject("Label", typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = anchoredPosition;

            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.black;
            return text;
        }

        private static Button CreateButton(Transform parent, string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label, typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(260, 60);
            rect.anchoredPosition = anchoredPosition;
            go.GetComponent<Image>().color = new Color(0.85f, 0.85f, 0.85f);

            var button = go.GetComponent<Button>();
            button.onClick.AddListener(onClick);

            var labelText = CreateLabel(go.transform, Vector2.zero, 260, 60);
            labelText.text = label;

            return button;
        }
    }
}
