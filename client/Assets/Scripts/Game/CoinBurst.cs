using UnityEngine;

namespace RestaurantIdle.Game
{
    /// <summary>
    /// Kurzer Muenz-Partikel-Burst bei Produktion (PLANv2.md Abschnitt 8/11:
    /// "Partikel: Muenz-Bursts, Meilenstein-Effekte"). Rein prozedural per
    /// ParticleSystem-API konfiguriert -- keine externen Sprite-Assets
    /// noetig, kleine gold-farbene Kugeln lesen sich in einem Low-Poly-Look
    /// bereits als "Muenzen".
    /// </summary>
    public static class CoinBurst
    {
        public static void SpawnAt(Vector3 position)
        {
            var go = new GameObject("CoinBurst", typeof(ParticleSystem));
            go.transform.position = position;

            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.4f;
            main.loop = false;
            main.startLifetime = 0.6f;
            main.startSpeed = 2.5f;
            main.startSize = 0.08f;
            main.startColor = new Color(0.95f, 0.8f, 0.2f);
            main.gravityModifier = 1.5f;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 8) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = 0.05f;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));

            ps.Play();
        }
    }
}
