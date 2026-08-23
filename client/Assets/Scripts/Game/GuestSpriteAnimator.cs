using UnityEngine;

namespace RestaurantIdle.Game
{
    /// <summary>
    /// PLANv3.md Abschnitt 5 ("Charaktere statt Kapseln. Kenney Toon
    /// Characters liegen bereits im Projekt ... hoechste Wirkung pro
    /// Aufwand im gesamten Plan"): ersetzt das eingefaerbte Kapsel-
    /// Primitive durch ein Billboard-Sprite mit einfachem Lauf-Zyklus.
    /// GuestWalker.cs (der urspruengliche, im Plan erwaehnte erste Versuch)
    /// war 2D-UI-RectTransform-basiert und liess sich nicht in die 3D-Szene
    /// uebernehmen -- wurde deshalb entfernt statt "verdrahtet" (siehe
    /// PLANv3 Phase C).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer), typeof(GuestMover))]
    public class GuestSpriteAnimator : MonoBehaviour
    {
        private const float FrameSeconds = 0.15f;

        /// <summary>
        /// Ab welchem Anteil verbrauchter Geduld der wartende Gast anfaengt,
        /// unruhig zu werden -- darunter steht er ruhig.
        /// </summary>
        private const float FidgetThreshold = 0.45f;
        private const float FidgetSlowSeconds = 0.5f;
        private const float FidgetFastSeconds = 0.14f;

        /// <summary>
        /// 0 = gerade angekommen, 1 = geht gleich unbedient. Wird vom
        /// GameManager aus derselben Geduld gespeist, die auch der
        /// Geduldsbalken ueber der Station anzeigt (StationBadge).
        ///
        /// Bisher sah ein Gast, dessen Geduld fast aufgebraucht war, exakt
        /// aus wie einer, der gerade erst angekommen ist -- die Information
        /// stand ausschliesslich im UI-Schild. In einem Spiel, dessen
        /// aktive Entscheidung "wo tippe ich als naechstes hin?" lautet,
        /// gehoert sie an die Figur selbst.
        /// </summary>
        public float Impatience { get; set; }

        private SpriteRenderer spriteRenderer;
        private GuestMover mover;
        private Sprite idleSprite;
        private Sprite[] runFrames;
        private int frameIndex;
        private float frameTimer;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            mover = GetComponent<GuestMover>();
            idleSprite = GameAssets.Sprite("Characters/guest-idle");
            runFrames = new[]
            {
                GameAssets.Sprite("Characters/guest-run0"),
                GameAssets.Sprite("Characters/guest-run1"),
                GameAssets.Sprite("Characters/guest-run2"),
            };
        }

        private void Update()
        {
            var interval = FrameSeconds;

            if (mover.CurrentPhase == GuestMover.Phase.Waiting)
            {
                if (Impatience < FidgetThreshold)
                {
                    if (idleSprite != null)
                    {
                        spriteRenderer.sprite = idleSprite;
                    }

                    frameTimer = 0f;
                    return;
                }

                // Je knapper die Geduld, desto schneller das Zappeln -- ein
                // stetiger Uebergang statt eines harten Zustandswechsels,
                // damit die Dringlichkeit ablesbar ist und nicht nur ihr
                // Vorhandensein.
                var urgency = Mathf.InverseLerp(FidgetThreshold, 1f, Impatience);
                interval = Mathf.Lerp(FidgetSlowSeconds, FidgetFastSeconds, urgency);
            }

            frameTimer += Time.deltaTime;
            if (frameTimer < interval)
            {
                return;
            }

            frameTimer = 0f;
            frameIndex = (frameIndex + 1) % runFrames.Length;
            if (runFrames[frameIndex] != null)
            {
                spriteRenderer.sprite = runFrames[frameIndex];
            }
        }
    }
}
