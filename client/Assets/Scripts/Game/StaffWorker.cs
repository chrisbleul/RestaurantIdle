using UnityEngine;

namespace RestaurantIdle.Game
{
    /// <summary>
    /// Personal an einer Station mit Manager (PLANv2.md Abschnitt 9:
    /// "Personal an Stationen, Animation an Zykluszeit gekoppelt"). Echte
    /// Stations-Zykluszeiten reichen von 2s bis 900s -- fuers Auge auf einen
    /// wahrnehmbaren Bereich geklemmt, sonst waeren spaete Stationen
    /// praktisch bewegungslos.
    ///
    /// Bewegt sich auf zwei Arten: Auf-/Ab-Wippen (schon vorher) UND der
    /// Bildfolge des Kenney-Sprites. Nur zu wippen war ein Rest aus der
    /// Zeit, als das Personal noch eine weisse Kapsel ohne Einzelbilder war
    /// -- als Charakter-Sprite stand es unbewegt an der Station und wirkte
    /// eher abgestellt als arbeitend.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class StaffWorker : MonoBehaviour
    {
        private const float BobAmplitude = 0.06f;

        private float periodSeconds;
        private Vector3 basePosition;
        private SpriteRenderer spriteRenderer;
        private Sprite[] workFrames;
        private float frameInterval;
        private float frameTimer;
        private int frameIndex;

        public void Init(Vector3 position, double stationCycleSeconds)
        {
            basePosition = position;
            transform.position = position;
            periodSeconds = Mathf.Clamp((float)stationCycleSeconds, 0.4f, 1.6f);

            spriteRenderer = GetComponent<SpriteRenderer>();
            workFrames = new[]
            {
                GameAssets.LoadSprite("Characters/guest-run0"),
                GameAssets.LoadSprite("Characters/guest-run1"),
                GameAssets.LoadSprite("Characters/guest-run2"),
            };

            // Eine volle Bildfolge je Wipp-Periode: Bewegung und Bildwechsel
            // laufen damit im selben Takt wie der Produktionszyklus der
            // Station, statt zufaellig gegeneinander.
            frameInterval = periodSeconds / workFrames.Length;
        }

        private void Update()
        {
            var bob = Mathf.Sin(Time.time * (2f * Mathf.PI / periodSeconds)) * BobAmplitude;
            transform.position = basePosition + new Vector3(0f, bob, 0f);

            if (workFrames == null || frameInterval <= 0f)
            {
                return;
            }

            frameTimer += Time.deltaTime;
            if (frameTimer < frameInterval)
            {
                return;
            }

            frameTimer = 0f;
            frameIndex = (frameIndex + 1) % workFrames.Length;
            if (workFrames[frameIndex] != null)
            {
                spriteRenderer.sprite = workFrames[frameIndex];
            }
        }
    }
}
