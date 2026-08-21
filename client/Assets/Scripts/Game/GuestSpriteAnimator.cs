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
            idleSprite = Resources.Load<Sprite>("Characters/guest-idle");
            runFrames = new[]
            {
                Resources.Load<Sprite>("Characters/guest-run0"),
                Resources.Load<Sprite>("Characters/guest-run1"),
                Resources.Load<Sprite>("Characters/guest-run2"),
            };
        }

        private void Update()
        {
            if (mover.CurrentPhase == GuestMover.Phase.Waiting)
            {
                if (idleSprite != null)
                {
                    spriteRenderer.sprite = idleSprite;
                }

                return;
            }

            frameTimer += Time.deltaTime;
            if (frameTimer < FrameSeconds)
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
