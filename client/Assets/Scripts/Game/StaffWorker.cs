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
    /// Bewegt sich auf zwei Arten: Auf-/Ab-Wippen UND ein leichtes Drehen
    /// des 3D-Modells (Nutzer-Feedback "nichts passt optisch zusammen" --
    /// siehe GuestSpriteAnimator-Kommentar zum Wechsel auf Kenney Mini
    /// Characters; das Paket bringt keine Animationsclips mit, das Drehen
    /// ersetzt die fruehere Sprite-Bildfolge als "arbeitet gerade"-Signal).
    /// </summary>
    public class StaffWorker : MonoBehaviour
    {
        private const float BobAmplitude = 0.06f;
        private const float WiggleDegrees = 14f;

        private float periodSeconds;
        private Vector3 basePosition;
        private Transform model;

        public void Init(Vector3 position, Transform model, double stationCycleSeconds)
        {
            basePosition = position;
            transform.position = position;
            this.model = model;
            periodSeconds = Mathf.Clamp((float)stationCycleSeconds, 0.4f, 1.6f);
        }

        private void Update()
        {
            var phase = Time.time * (2f * Mathf.PI / periodSeconds);
            transform.position = basePosition + new Vector3(0f, Mathf.Sin(phase) * BobAmplitude, 0f);

            if (model != null)
            {
                model.localRotation = Quaternion.Euler(0f, Mathf.Sin(phase) * WiggleDegrees, 0f);
            }
        }
    }
}
