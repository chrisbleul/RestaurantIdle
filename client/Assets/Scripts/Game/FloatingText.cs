using UnityEngine;
using UnityEngine.UI;

namespace RestaurantIdle.Game
{
    /// <summary>
    /// Aufsteigender "+12" -Text an der Station, die gerade bezahlt hat.
    /// Der Muenz-Burst (CoinBurst) sagt bisher nur DASS etwas passiert ist,
    /// nicht WIE VIEL -- gerade der Vergleich zwischen einer schnell und
    /// einer spaet bedienten Station (Trinkgeld, siehe BalancingCore.Service)
    /// ist ohne Zahl am Ort des Geschehens gar nicht wahrnehmbar.
    ///
    /// Bewusst UI-Text am projizierten Bildschirmpunkt statt eines
    /// World-Space-TextMesh: die Schriftgroesse bleibt so unabhaengig vom
    /// Kamera-Zoom lesbar, der sich im Spielverlauf deutlich aendert
    /// (GameManager.RecomputeCameraTarget).
    /// </summary>
    public class FloatingText : MonoBehaviour
    {
        private const float LifetimeSeconds = 1.1f;
        private const float RiseScreenPixelsPerSecond = 110f;

        private Vector3 worldPosition;
        private float elapsed;
        private Text label;
        private RectTransform rect;

        public static void Spawn(Transform canvas, Vector3 worldPosition, string message, Color color, int fontSize = 40)
        {
            if (canvas == null)
            {
                return;
            }

            var go = new GameObject("FloatingText", typeof(Text), typeof(Outline), typeof(FloatingText));
            go.transform.SetParent(canvas, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(400, 70);

            var text = go.GetComponent<Text>();
            text.font = GameAssets.UiFont;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.text = message;
            text.raycastTarget = false;

            var outline = go.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.75f);
            outline.effectDistance = new Vector2(2.5f, -2.5f);

            var floating = go.GetComponent<FloatingText>();
            floating.worldPosition = worldPosition;
            floating.label = text;
            floating.rect = rect;
            floating.UpdatePosition(0f);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (elapsed >= LifetimeSeconds)
            {
                Destroy(gameObject);
                return;
            }

            UpdatePosition(elapsed);

            var alpha = 1f - Mathf.Clamp01(elapsed / LifetimeSeconds);
            var color = label.color;
            label.color = new Color(color.r, color.g, color.b, alpha);
        }

        private void UpdatePosition(float age)
        {
            var canvasRect = rect.parent as RectTransform;
            if (canvasRect == null)
            {
                return;
            }

            var basePosition = UiProjection.WorldToCanvas(canvasRect, worldPosition);
            rect.anchoredPosition = basePosition + new Vector2(0f, age * RiseScreenPixelsPerSecond);
        }
    }
}
