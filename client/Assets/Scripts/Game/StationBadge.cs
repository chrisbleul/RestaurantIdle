using UnityEngine;
using UnityEngine.UI;

namespace RestaurantIdle.Game
{
    /// <summary>
    /// Schwebendes Schild ueber einer Station. Loest den groessten
    /// Lesbarkeitsmangel des Tap-Layers: dass an einer Station gerade ein
    /// Gast wartet, war ausschliesslich am Dampfeffekt zu erkennen -- und
    /// WIE LANGE er noch wartet (GameManager.GuestPatienceSeconds) gar
    /// nicht. Ohne diese Information ist die Entscheidung "wo tippe ich als
    /// naechstes hin?" reines Raten, obwohl genau sie der Kern der aktiven
    /// Spielphase ist.
    ///
    /// Zeigt drei Zustaende: wartender Gast (Geduldsbalken + Ertrag), Station
    /// gesperrt aber leistbar (Kaufhinweis), sonst unsichtbar. Folgt der
    /// Station per Projektion statt als World-Space-Canvas, damit die
    /// Schriftgroesse vom Kamera-Zoom unabhaengig bleibt.
    /// </summary>
    public class StationBadge : MonoBehaviour
    {
        private static readonly Color CalmColor = new Color(0.45f, 0.78f, 0.42f);
        private static readonly Color HurryColor = new Color(0.95f, 0.72f, 0.2f);
        private static readonly Color PanicColor = new Color(0.9f, 0.3f, 0.25f);
        private static readonly Color LockedColor = new Color(0.98f, 0.85f, 0.4f);

        private RectTransform canvasRect;
        private RectTransform rect;
        private Image background;
        private Image patienceFill;
        private RectTransform patienceRow;
        private Text label;
        private Vector3 worldPosition;

        public static StationBadge Create(RectTransform canvasRect, Vector3 worldPosition)
        {
            var go = new GameObject("StationBadge", typeof(Image), typeof(StationBadge));
            go.transform.SetParent(canvasRect, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(220, 84);

            var background = go.GetComponent<Image>();
            var sprite = Resources.Load<Sprite>("UI/panel-rectangle");
            if (sprite != null)
            {
                background.sprite = sprite;
                background.type = Image.Type.Sliced;
            }

            background.raycastTarget = false;

            var labelGo = new GameObject("Label", typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0.32f);
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(10, 0);
            labelRect.offsetMax = new Vector2(-10, -8);

            var label = labelGo.GetComponent<Text>();
            label.font = Resources.Load<Font>("Fonts/Fredoka");
            label.fontSize = 26;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.black;
            label.raycastTarget = false;

            // Geduldsbalken: Hintergrund + gefuellter Vordergrund. Image.Type
            // .Filled statt einer Breitenanimation -- so bleibt der Balken
            // unabhaengig vom Layout exakt proportional zur Restgeduld.
            var barBgGo = new GameObject("PatienceBg", typeof(Image));
            barBgGo.transform.SetParent(go.transform, false);
            var barBgRect = barBgGo.GetComponent<RectTransform>();
            barBgRect.anchorMin = new Vector2(0f, 0f);
            barBgRect.anchorMax = new Vector2(1f, 0.32f);
            barBgRect.offsetMin = new Vector2(14, 12);
            barBgRect.offsetMax = new Vector2(-14, -2);
            var barBg = barBgGo.GetComponent<Image>();
            barBg.color = new Color(0f, 0f, 0f, 0.25f);
            barBg.raycastTarget = false;

            var fillGo = new GameObject("PatienceFill", typeof(Image));
            fillGo.transform.SetParent(barBgGo.transform, false);
            var fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fill = fillGo.GetComponent<Image>();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.raycastTarget = false;

            var badge = go.GetComponent<StationBadge>();
            badge.canvasRect = canvasRect;
            badge.rect = rect;
            badge.background = background;
            badge.label = label;
            badge.patienceFill = fill;
            badge.patienceRow = barBgRect;
            badge.worldPosition = worldPosition;
            badge.Hide();
            return badge;
        }

        public void SetWorldPosition(Vector3 position) => worldPosition = position;

        /// <param name="patienceFraction">1 = volle Geduld, 0 = Gast geht jetzt.</param>
        public void ShowWaitingGuest(string valueText, float patienceFraction, bool isVip)
        {
            gameObject.SetActive(true);
            patienceRow.gameObject.SetActive(true);
            patienceFill.fillAmount = Mathf.Clamp01(patienceFraction);
            patienceFill.color = patienceFraction > 0.5f ? CalmColor : patienceFraction > 0.25f ? HurryColor : PanicColor;
            background.color = isVip ? new Color(1f, 0.93f, 0.6f, 0.97f) : new Color(1f, 1f, 1f, 0.95f);
            label.text = valueText;
        }

        public void ShowHint(string text)
        {
            gameObject.SetActive(true);
            patienceRow.gameObject.SetActive(false);
            background.color = LockedColor;
            label.text = text;
        }

        public void Hide()
        {
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            // LateUpdate statt Update: die Kamera bewegt sich in
            // GameManager.ApplyCameraFraming waehrend Update: erst danach
            // ist die Projektion fuer diesen Frame gueltig, sonst haengt das
            // Schild beim Zoomen sichtbar hinterher.
            rect.anchoredPosition = UiProjection.WorldToCanvas(canvasRect, worldPosition);
        }
    }
}
