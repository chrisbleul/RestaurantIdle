using UnityEngine;

namespace RestaurantIdle.Game
{
    /// <summary>
    /// PLANv3.md Abschnitt 4 ("Kein Fortschrittsring, kein Fertig-Signal --
    /// der Spieler kann einer Station nicht ansehen, ob sie arbeitet"):
    /// kleiner Dauer-Dampfeffekt, solange ein Gast an einer Station bedient
    /// wird (siehe GameManager.UpdateGuestVisits). Anders als CoinBurst kein
    /// Einmal-Burst, sondern ein Loop, den der Aufrufer per Destroy beendet,
    /// sobald die Bedienung endet (serviert oder Gast geht unbedient).
    /// </summary>
    public static class SteamEffect
    {
        public static GameObject SpawnLoopingAt(Vector3 position)
        {
            var go = new GameObject("SteamEffect", typeof(ParticleSystem));
            go.transform.position = position + Vector3.up * 0.35f;

            var ps = go.GetComponent<ParticleSystem>();
            // AddComponent<ParticleSystem> startet wegen playOnAwake=true
            // sofort -- main.loop/duration laesst sich waehrend des
            // Abspielens nicht setzen, deshalb erst stoppen (siehe CoinBurst).
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.startLifetime = 1.0f;
            main.startSpeed = 0.35f;
            main.startSize = 0.1f;
            main.startColor = new Color(1f, 1f, 1f, 0.45f);
            main.gravityModifier = -0.05f; // leichtes Aufsteigen statt Fallen.

            var emission = ps.emission;
            emission.rateOverTime = 5f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = 0.04f;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));

            ps.Play();
            return go;
        }
    }
}
