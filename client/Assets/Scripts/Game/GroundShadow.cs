using UnityEngine;

namespace RestaurantIdle.Game
{
    /// <summary>
    /// Weicher Schattenfleck auf dem Boden unter einer Figur.
    ///
    /// Gaeste und Personal sind Billboard-Sprites (siehe
    /// GameManager.SpawnGuest): ein Sprite, das sich zur Kamera dreht, kann
    /// keinen sinnvollen Schlagschatten werfen -- ein echter Schatten davon
    /// waere die Silhouette einer senkrecht stehenden Flaeche. Die Figuren
    /// standen dadurch ohne jede Bodenhaftung in der Szene und wirkten
    /// aufgeklebt, waehrend jedes Moebelstueck daneben einen Schatten hat.
    ///
    /// Bewusst KEIN Kind der Figur: die Figur traegt die Billboard-Rotation
    /// der Kamera, ein Kind wuerde sie erben und mit aufgestellt. Der Fleck
    /// folgt der Figur stattdessen in x/z und liegt flach auf dem Boden.
    /// </summary>
    public class GroundShadow : MonoBehaviour
    {
        /// <summary>Knapp ueber dem Innenboden (y = 0.012 in CIBuild), sonst z-fightet der Fleck mit ihm.</summary>
        private const float GroundY = 0.03f;

        /// <summary>
        /// Versatz des Flecks vom Fusspunkt weg, entlang der Lichtrichtung.
        ///
        /// Ohne ihn lag der Fleck genau unter der Figur -- und damit
        /// vollstaendig hinter ihr: das Billboard steht senkrecht, sein
        /// unterer Rand reicht bis auf den Boden und verdeckt aus der
        /// Kameraperspektive (55 Grad Neigung) genau die Flaeche, auf der
        /// der Fleck liegt. Im Testlauf waren die Flecken nachweislich da
        /// (vier Gaeste, vier Objekte in der Hierarchie), aber unsichtbar.
        ///
        /// Der Versatz ist ausserdem physikalisch richtig: der Schatten
        /// einer stehenden Figur faellt vom Licht weg, genau wie die
        /// Schlagschatten der Moebel daneben.
        /// </summary>
        private const float LightOffsetDistance = 0.22f;

        private static Vector3 lightOffset;
        private static bool lightOffsetResolved;

        private Transform target;

        public static void Attach(Transform target, float width, float opacity)
        {
            var sprite = GameAssets.BlobShadowSprite;
            if (target == null || sprite == null)
            {
                return;
            }

            var go = new GameObject("GroundShadow", typeof(SpriteRenderer), typeof(GroundShadow));

            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = new Color(0f, 0f, 0f, opacity);
            // Unter die Figuren sortieren -- sonst legt sich der Fleck bei
            // gleicher Tiefe ueber die Fuesse.
            renderer.sortingOrder = -1;

            // Flach auf den Boden kippen. Das Sprite-Shader-Material rendert
            // beidseitig, die Drehrichtung ist deshalb egal.
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var spriteWidth = sprite.bounds.size.x;
            go.transform.localScale = Vector3.one * (spriteWidth > 0.001f ? width / spriteWidth : 1f);

            var shadow = go.GetComponent<GroundShadow>();
            shadow.target = target;
            shadow.Follow();
        }

        /// <summary>
        /// Bodenrichtung, in die Schatten fallen -- aus dem gerichteten
        /// Licht der Szene abgeleitet, damit die Flecken automatisch mit
        /// jeder Aenderung am Lichtwinkel in CIBuild mitwandern. Einmal
        /// aufgeloest und gemerkt: die Suche pro Gast waere unnoetig.
        /// </summary>
        private static Vector3 LightOffset()
        {
            if (lightOffsetResolved)
            {
                return lightOffset;
            }

            var sun = RenderSettings.sun != null ? RenderSettings.sun : FindFirstObjectByType<Light>();
            var direction = sun != null ? sun.transform.forward : new Vector3(-0.57f, 0f, 0.82f);
            direction.y = 0f;

            lightOffset = direction.sqrMagnitude > 0.0001f
                ? direction.normalized * LightOffsetDistance
                : Vector3.zero;
            lightOffsetResolved = true;
            return lightOffset;
        }

        /// <summary>Statische Felder ueberleben ohne "Reload Domain" den Play-Modus (siehe GameAssets).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            lightOffset = Vector3.zero;
            lightOffsetResolved = false;
        }

        private void LateUpdate()
        {
            // Die Figur wird zerstoert, wenn der Gast die Szene verlaesst --
            // der Fleck muss dann mitgehen, sonst bleiben dunkle Punkte auf
            // dem Boden zurueck.
            if (target == null)
            {
                Destroy(gameObject);
                return;
            }

            Follow();
        }

        private void Follow()
        {
            var position = target.position + LightOffset();
            transform.position = new Vector3(position.x, GroundY, position.z);
        }
    }
}
