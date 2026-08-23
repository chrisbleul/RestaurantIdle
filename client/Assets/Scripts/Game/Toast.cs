using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RestaurantIdle.Game
{
    /// <summary>
    /// Kurze Einblendung fuer Ereignisse, die sonst unbemerkt bleiben: neue
    /// Station freigeschaltet, Rush Hour gestartet, Gast unbedient gegangen.
    /// Vor allem Letzteres war bisher voellig stumm -- der Gast verschwand
    /// einfach, obwohl es ihn jetzt Ruf kostet (BalancingCore.Reputation).
    ///
    /// Mehrere Toasts stapeln sich nach unten statt sich zu ueberdecken; ein
    /// identischer Text direkt hintereinander wird zusammengefasst, damit
    /// eine Welle abgewanderter Gaeste nicht den halben Bildschirm fuellt.
    /// </summary>
    public class Toast : MonoBehaviour
    {
        private const float LifetimeSeconds = 2.6f;
        private const float FadeSeconds = 0.4f;
        private const float SlotHeight = 74f;
        private const int MaxVisible = 3;

        private static readonly List<Toast> Active = new();

        private float elapsed;
        private Image background;
        private Text label;
        private RectTransform rect;
        private string message;
        private int repeatCount = 1;

        public static void Show(Transform canvas, string message, Color? tint = null)
        {
            if (canvas == null)
            {
                return;
            }

            Active.RemoveAll(t => t == null);

            var newest = Active.Count > 0 ? Active[Active.Count - 1] : null;
            if (newest != null && newest.message == message)
            {
                newest.repeatCount++;
                newest.elapsed = 0f;
                newest.label.text = $"{message}  x{newest.repeatCount}";
                return;
            }

            while (Active.Count >= MaxVisible)
            {
                var oldest = Active[0];
                Active.RemoveAt(0);
                if (oldest != null)
                {
                    Destroy(oldest.gameObject);
                }
            }

            var go = new GameObject("Toast", typeof(Image), typeof(Toast));
            go.transform.SetParent(canvas, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(820, 64);

            var background = go.GetComponent<Image>();
            var sprite = Resources.Load<Sprite>("UI/panel-rectangle");
            if (sprite != null)
            {
                background.sprite = sprite;
                background.type = Image.Type.Sliced;
            }

            background.color = tint ?? new Color(1f, 1f, 1f, 0.95f);
            background.raycastTarget = false;

            var textGo = new GameObject("Text", typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(20, 6);
            textRect.offsetMax = new Vector2(-20, -6);

            var text = textGo.GetComponent<Text>();
            text.font = Resources.Load<Font>("Fonts/Fredoka");
            text.fontSize = 26;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.black;
            text.text = message;
            text.raycastTarget = false;

            var toast = go.GetComponent<Toast>();
            toast.background = background;
            toast.label = text;
            toast.rect = rect;
            toast.message = message;

            Active.Add(toast);
            Reflow();
        }

        private static void Reflow()
        {
            for (var i = 0; i < Active.Count; i++)
            {
                if (Active[i] != null)
                {
                    Active[i].rect.anchoredPosition = new Vector2(0f, -420f - i * SlotHeight);
                }
            }
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (elapsed >= LifetimeSeconds)
            {
                Active.Remove(this);
                Destroy(gameObject);
                Reflow();
                return;
            }

            var remaining = LifetimeSeconds - elapsed;
            if (remaining >= FadeSeconds)
            {
                return;
            }

            var alpha = remaining / FadeSeconds;
            background.color = new Color(background.color.r, background.color.g, background.color.b, alpha * 0.95f);
            label.color = new Color(label.color.r, label.color.g, label.color.b, alpha);
        }

        private void OnDestroy() => Active.Remove(this);
    }
}
