using System.Collections.Generic;
using UnityEngine;

namespace RestaurantIdle.Game
{
    /// <summary>
    /// Zwischenspeicher fuer Dinge, die bisher pro erzeugtem Objekt neu
    /// beschafft wurden. Betrifft ausgerechnet die Pfade, die am haeufigsten
    /// laufen: pro Gast ein Sprite-Satz (GuestSpriteAnimator), pro Verkauf
    /// ein Muenz-Burst, pro schwebendem Betrag eine Schriftart.
    ///
    /// Der teuerste Fall war das Partikel-Material: CoinBurst, SteamEffect
    /// und MilestoneEffect legten pro Instanz ein <c>new Material(...)</c>
    /// an und suchten den Shader jedes Mal ueber <c>Shader.Find</c>. Ein per
    /// <c>new</c> erzeugtes und an einen Renderer gehaengtes Material wird
    /// beim Zerstoeren des Objekts NICHT mitzerstoert -- bei einem Burst pro
    /// Verkauf sammelt sich das ueber eine Spielsitzung sichtbar an.
    /// </summary>
    public static class GameAssets
    {
        private static readonly Dictionary<string, Sprite> Sprites = new();
        private static Font uiFont;
        private static Material particleMaterial;
        private static Camera mainCamera;

        /// <summary>
        /// Kamera der Szene. <c>Camera.main</c> ist kein Feldzugriff --
        /// wird hier einmal gemerkt und nur nachgeschlagen, wenn die
        /// Referenz fehlt oder zerstoert wurde (Unitys ueberladener
        /// ==-Operator erkennt das). Wichtig, weil pro Frame ein Aufruf je
        /// Stations-Schild und je schwebendem Betrag anfaellt.
        /// </summary>
        public static Camera MainCamera
        {
            get
            {
                if (mainCamera == null)
                {
                    mainCamera = Camera.main;
                }

                return mainCamera;
            }
        }

        public static Font UiFont
        {
            get
            {
                if (uiFont == null)
                {
                    uiFont = Resources.Load<Font>("Fonts/Fredoka");
                }

                return uiFont;
            }
        }

        public static Sprite Sprite(string resourcePath)
        {
            if (Sprites.TryGetValue(resourcePath, out var cached) && cached != null)
            {
                return cached;
            }

            var sprite = Resources.Load<Sprite>(resourcePath);
            Sprites[resourcePath] = sprite;
            return sprite;
        }

        private static readonly Dictionary<string, AudioClip> Clips = new();
        private static AudioSource sfxSource;

        /// <summary>
        /// Eine dauerhafte AudioSource fuer alle Klick-/Kauf-/Verkaufstoene.
        /// <c>AudioSource.PlayClipAtPoint</c> legt pro Ton ein eigenes
        /// GameObject an und zerstoert es wieder -- bei einem Ton pro
        /// Antippen ist das in der aktivsten Spielphase die haeufigste
        /// Objekterzeugung im ganzen Spiel, fuer einen Effekt, der ohnehin
        /// nicht raeumlich ist (2D-UI-Sound).
        /// </summary>
        public static void PlaySfx(string resourceName)
        {
            if (!Clips.TryGetValue(resourceName, out var clip) || clip == null)
            {
                clip = Resources.Load<AudioClip>($"Audio/{resourceName}");
                Clips[resourceName] = clip;
            }

            if (clip == null)
            {
                return;
            }

            if (sfxSource == null)
            {
                var host = new GameObject("SfxSource", typeof(AudioSource));
                Object.DontDestroyOnLoad(host);
                sfxSource = host.GetComponent<AudioSource>();
                sfxSource.playOnAwake = false;
                // 2D: die Toene gehoeren zur Bedienung, nicht zu einem Ort
                // in der Szene.
                sfxSource.spatialBlend = 0f;
            }

            sfxSource.PlayOneShot(clip);
        }

        private static Sprite blobShadowSprite;

        /// <summary>
        /// Weicher runder Schattenfleck fuer den Boden unter Gaesten und
        /// Personal (siehe GroundShadow). Die Figuren sind Billboard-Sprites
        /// und werfen deshalb keinen echten Schlagschatten -- ohne diesen
        /// Fleck wirken sie auf den Boden geklebt statt darauf zu stehen.
        ///
        /// Prozedural erzeugt statt als PNG mitgeliefert: es ist ein
        /// radialer Alpha-Verlauf, dafuer lohnt kein Asset mit
        /// Import-Einstellungen.
        /// </summary>
        public static Sprite BlobShadowSprite
        {
            get
            {
                if (blobShadowSprite != null)
                {
                    return blobShadowSprite;
                }

                const int size = 64;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "BlobShadow",
                    wrapMode = TextureWrapMode.Clamp,
                };

                var center = (size - 1) * 0.5f;
                var pixels = new Color32[size * size];
                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        var distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
                        // Quadriert: harter Kern, weich auslaufender Rand --
                        // ein linearer Verlauf sieht aus wie ein grauer
                        // Kreis, nicht wie ein Schatten.
                        var alpha = Mathf.Clamp01(1f - distance);
                        alpha *= alpha;
                        pixels[(y * size) + x] = new Color32(0, 0, 0, (byte)(alpha * 255f));
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply();

                blobShadowSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
                return blobShadowSprite;
            }
        }

        /// <summary>Gemeinsames Material fuer alle Partikeleffekte -- als sharedMaterial zuweisen, nie veraendern.</summary>
        public static Material ParticleMaterial
        {
            get
            {
                if (particleMaterial == null)
                {
                    particleMaterial = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"))
                    {
                        name = "SharedParticleUnlit",
                    };
                }

                return particleMaterial;
            }
        }

        /// <summary>
        /// Statische Felder ueberleben in Unity das Verlassen des
        /// Play-Modus, wenn "Reload Domain" abgeschaltet ist -- dann zeigen
        /// sie beim naechsten Start auf zerstoerte Objekte. Die
        /// Null-Pruefungen oben fangen das zwar ab; dieser Haken macht das
        /// Zuruecksetzen explizit statt es dem Zufall zu ueberlassen.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Sprites.Clear();
            Clips.Clear();
            sfxSource = null;
            uiFont = null;
            blobShadowSprite = null;
            particleMaterial = null;
            mainCamera = null;
        }
    }
}
