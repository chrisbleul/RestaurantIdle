using UnityEngine;

namespace RestaurantIdle.Game
{
    /// <summary>
    /// Kleine Bewegung an einer Station mit Manager (PLANv2.md Abschnitt 9:
    /// "Personal an Stationen, Animation an Zykluszeit gekoppelt"). Bewusst
    /// simpel (Auf-/Ab-Wippen statt echtem Charaktermodell/Rig) -- signalisiert
    /// "hier arbeitet automatisch jemand". Echte Stations-Zykluszeiten reichen
    /// von 2s bis 900s -- fuers Auge auf einen wahrnehmbaren Bereich geklemmt,
    /// sonst waere spaete Stationen praktisch bewegungslos.
    /// </summary>
    public class StaffWorker : MonoBehaviour
    {
        private float periodSeconds;
        private Vector3 basePosition;

        public void Init(Vector3 position, double stationCycleSeconds)
        {
            basePosition = position;
            transform.position = position;
            periodSeconds = Mathf.Clamp((float)stationCycleSeconds, 0.4f, 1.6f);
        }

        private void Update()
        {
            var bob = Mathf.Sin(Time.time * (2f * Mathf.PI / periodSeconds)) * 0.06f;
            transform.position = basePosition + new Vector3(0f, bob, 0f);
        }
    }
}
