using System.Collections;
using UnityEngine;

namespace RestaurantIdle.Game
{
    /// <summary>
    /// Kurzes Skalier-Feedback bei jedem Klick (PLAN.md Abschnitt 6: "Buttons
    /// federn"). Haengt an jedem per GameManager.CreateButton erzeugten
    /// Button, damit jeder Klick automatisch dieses Feedback bekommt, ohne
    /// dass jeder Aufrufer sich selbst darum kuemmern muss. Kein DOTween --
    /// PLAN.md selbst sagt "~20 Zeilen, 80% des Effekts", das erreicht eine
    /// simple Coroutine ohne zusaetzliche Paketabhaengigkeit.
    /// </summary>
    public class ButtonPunch : MonoBehaviour
    {
        private const float Duration = 0.12f;
        private const float ScaleMultiplier = 1.15f;

        private Coroutine running;

        public void Punch()
        {
            if (running != null)
            {
                StopCoroutine(running);
            }

            running = StartCoroutine(PunchRoutine());
        }

        private IEnumerator PunchRoutine()
        {
            var t = (RectTransform)transform;
            var original = Vector3.one;
            var peak = original * ScaleMultiplier;
            var half = Duration / 2f;

            var elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                t.localScale = Vector3.Lerp(original, peak, elapsed / half);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                t.localScale = Vector3.Lerp(peak, original, elapsed / half);
                yield return null;
            }

            t.localScale = original;
            running = null;
        }
    }
}
