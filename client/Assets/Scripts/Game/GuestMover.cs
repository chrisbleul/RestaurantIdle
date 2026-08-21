using UnityEngine;

namespace RestaurantIdle.Game
{
    /// <summary>
    /// Bewegt einen gespawnten Gast linear von Start- zu Zielposition und
    /// zerstoert sich danach selbst (PLANv2.md Abschnitt 9: Gast-Spawner).
    /// Vereinfachter Ersatz fuer echte Wegfindung in dieser ersten
    /// Phase-9-Iteration -- Eingang/Warteschlange/Theke/Ausgang als eigene
    /// Zustaende folgen spaeter, sobald die Grundmechanik steht.
    /// </summary>
    public class GuestMover : MonoBehaviour
    {
        private Vector3 start;
        private Vector3 end;
        private float duration;
        private float elapsed;

        public void Init(Vector3 startPos, Vector3 endPos, float moveDuration)
        {
            start = startPos;
            end = endPos;
            duration = moveDuration;
            transform.position = start;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / duration);
            transform.position = Vector3.Lerp(start, end, t);

            if (t >= 1f)
            {
                Destroy(gameObject);
            }
        }
    }
}
