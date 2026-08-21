using UnityEngine;

namespace RestaurantIdle.Game
{
    /// <summary>
    /// PLANv2.md Abschnitt 8/11 ("Partikel: Muenz-Bursts, Meilenstein-
    /// Effekte") -- deutlich groesserer, blau-weisser Burst fuer den Moment,
    /// in dem PriceLevel oder EquipmentLevel einer Station eine
    /// Meilenstein-Schwelle (BalancingCore.Milestones.DefaultThresholds)
    /// erreicht. Optisch bewusst von CoinBurst (klein, golden, jede
    /// Produktion) abgesetzt, damit der seltene, groessere Sprung auch
    /// seltener/groesser wirkt.
    /// </summary>
    public static class MilestoneEffect
    {
        public static void SpawnAt(Vector3 position)
        {
            var go = new GameObject("MilestoneEffect", typeof(ParticleSystem));
            go.transform.position = position;

            var ps = go.GetComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.6f;
            main.loop = false;
            main.startLifetime = 1.0f;
            main.startSpeed = 4f;
            main.startSize = 0.14f;
            main.startColor = new Color(0.65f, 0.85f, 1f);
            main.gravityModifier = 0.8f;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 30) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));

            ps.Play();
        }
    }
}
