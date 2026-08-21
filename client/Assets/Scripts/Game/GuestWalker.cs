using UnityEngine;
using UnityEngine.UI;

namespace RestaurantIdle.Game
{
    /// <summary>
    /// Erster Schritt Richtung "echte Bewegung" statt nur Zahlen (Nutzer-
    /// wunsch: "wie sieht es mit richtigen Bewegungen von Figuren aus").
    /// Ein Gast laeuft ueber die Stage und respawnt am gegenueberliegenden
    /// Rand. Bewusst noch NICHT an Gaestestrom/Stationen angebunden -- das
    /// waere ein eigener, groesserer Schritt (Pfade zu Stationen, Warten,
    /// Wegehen nach Bedienung). Erstmal ueberhaupt etwas Bewegtes, visuell
    /// bestaetigt statt nur behauptet.
    /// </summary>
    public class GuestWalker : MonoBehaviour
    {
        private const float SpeedPixelsPerSecond = 120f;
        private const float FrameSeconds = 0.15f;

        private RectTransform stageRect;
        private RectTransform selfRect;
        private Image image;
        private Sprite[] runFrames;
        private int frameIndex;
        private float frameTimer;

        private void Awake()
        {
            selfRect = (RectTransform)transform;
            stageRect = (RectTransform)transform.parent;
            image = GetComponent<Image>();
            runFrames = new[]
            {
                Resources.Load<Sprite>("Characters/guest-run0"),
                Resources.Load<Sprite>("Characters/guest-run1"),
                Resources.Load<Sprite>("Characters/guest-run2"),
            };
        }

        private void Update()
        {
            var halfStageWidth = stageRect.rect.width / 2f;
            var selfWidth = selfRect.rect.width;

            var pos = selfRect.anchoredPosition;
            pos.x += SpeedPixelsPerSecond * Time.deltaTime;
            if (pos.x > halfStageWidth + selfWidth)
            {
                pos.x = -halfStageWidth - selfWidth;
            }

            selfRect.anchoredPosition = pos;

            frameTimer += Time.deltaTime;
            if (frameTimer < FrameSeconds)
            {
                return;
            }

            frameTimer = 0f;
            frameIndex = (frameIndex + 1) % runFrames.Length;
            if (runFrames[frameIndex] != null)
            {
                image.sprite = runFrames[frameIndex];
            }
        }
    }
}
