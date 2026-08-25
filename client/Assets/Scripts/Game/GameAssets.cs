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

        /// <summary>
        /// Heisst bewusst nicht schlicht "Sprite": eine Methode mit dem
        /// Namen eines Typs verdeckt diesen Typ innerhalb der Klasse. Ein
        /// Aufruf von Sprite.Create() weiter unten loeste dadurch auf die
        /// Methode statt auf UnityEngine.Sprite auf und liess sich nicht
        /// kompilieren.
        /// </summary>
        public static Sprite LoadSprite(string resourcePath)
        {
            if (Sprites.TryGetValue(resourcePath, out var cached) && cached != null)
            {
                return cached;
            }

            var sprite = Resources.Load<Sprite>(resourcePath);
            Sprites[resourcePath] = sprite;
            return sprite;
        }

        private static readonly Dictionary<string, GameObject> CharacterPrefabs = new();

        /// <summary>
        /// Nutzer-Feedback ("nichts passt optisch zusammen"): Gaeste/Personal
        /// waren flache 2D-Billboard-Sprites (Kenney Toon Characters) mitten
        /// in einer echten 3D-Szene mit Kenney-Furniture-Kit-Moebeln -- ein
        /// Stilbruch unabhaengig vom gewaehlten 2D-Set. Kenney Mini Characters
        /// (Resources/Characters3D) ist derselbe Baukasten-Stil wie das
        /// Furniture Kit. Zwoelf Varianten fuer sichtbare Abwechslung
        /// zwischen Gaesten statt eines einzigen, nur eingefaerbten Sprites.
        /// </summary>
        private static readonly string[] CharacterModelNames =
        {
            "character-female-a", "character-female-b", "character-female-c",
            "character-female-d", "character-female-e", "character-female-f",
            "character-male-a", "character-male-b", "character-male-c",
            "character-male-d", "character-male-e", "character-male-f",
        };

        /// <summary>
        /// Instanziert ein zufaelliges 3D-Charaktermodell als Kind von
        /// <paramref name="parent"/> und skaliert es auf targetHeight --
        /// dieselbe "gemessen statt angenommen"-Logik wie
        /// CIBuild.InstantiateModel fuers Mobiliar (die Modelle sind
        /// untereinander nicht zwingend exakt gleich hoch). Optionale
        /// Einfaerbung (z. B. VIP-Gaeste) ueber MaterialPropertyBlock statt
        /// <c>renderer.material</c> -- Letzteres legt pro Aufruf eine neue
        /// Material-Instanz an, die beim Zerstoeren des Gastes NICHT
        /// mitzerstoert wird (siehe Klassen-Remarks zum Partikel-Material).
        /// </summary>
        public static Transform InstantiateRandomCharacter(Transform parent, float targetHeight, Color? tint = null)
        {
            var name = CharacterModelNames[UnityEngine.Random.Range(0, CharacterModelNames.Length)];
            if (!CharacterPrefabs.TryGetValue(name, out var prefab) || prefab == null)
            {
                prefab = Resources.Load<GameObject>($"Characters3D/{name}");
                CharacterPrefabs[name] = prefab;
            }

            if (prefab == null)
            {
                return null;
            }

            var instance = Object.Instantiate(prefab, parent);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                foreach (var r in renderers)
                {
                    bounds.Encapsulate(r.bounds);
                }

                if (bounds.size.y > 0.0001f)
                {
                    instance.transform.localScale = Vector3.one * (targetHeight / bounds.size.y);
                }

                if (tint.HasValue)
                {
                    var block = new MaterialPropertyBlock();
                    block.SetColor(BaseColorId, tint.Value);
                    foreach (var r in renderers)
                    {
                        r.SetPropertyBlock(block);
                    }
                }
            }

            return instance.transform;
        }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

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

        private static Sprite chefHatSprite;

        /// <summary>
        /// Prozedural erzeugte Kochmuetze -- echte Silhouetten-
        /// Unterscheidung zwischen Personal und Gast (siehe
        /// GameManager.AttachChefHat), ohne ein zweites Kenney-Charakterset
        /// zu brauchen (im Projekt liegen nur die vier Gast-Sprites). Band
        /// + Puff als zwei Kreisformen uebereinander, gleiches
        /// Alpha-Verlauf-Verfahren wie BlobShadowSprite.
        /// </summary>
        public static Sprite ChefHatSprite
        {
            get
            {
                if (chefHatSprite != null)
                {
                    return chefHatSprite;
                }

                const int width = 48;
                const int height = 56;
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    name = "ChefHat",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                };

                var pixels = new Color32[width * height];
                var white = new Color32(250, 250, 248, 255);
                var outline = new Color32(60, 60, 60, 255);

                // Band unten (breiter, flacher Ellipsen-Ausschnitt) + Puff
                // oben (hoehere Ellipse) -- zwei ueberlappende Ellipsen
                // ergeben die typische Kochmuetzen-Silhouette, ohne Text/
                // Bezier-Kurven nachbauen zu muessen.
                var bandCenter = new Vector2(width * 0.5f, height * 0.28f);
                var bandRadius = new Vector2(width * 0.46f, height * 0.24f);
                var puffCenter = new Vector2(width * 0.5f, height * 0.62f);
                var puffRadius = new Vector2(width * 0.4f, height * 0.42f);

                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        var point = new Vector2(x + 0.5f, y + 0.5f);
                        var inBand = EllipseDistance(point, bandCenter, bandRadius) <= 1f;
                        var inPuff = EllipseDistance(point, puffCenter, puffRadius) <= 1f;

                        if (!inBand && !inPuff)
                        {
                            pixels[(y * width) + x] = default;
                            continue;
                        }

                        var edgeBand = EllipseDistance(point, bandCenter, bandRadius);
                        var edgePuff = EllipseDistance(point, puffCenter, puffRadius);
                        var nearEdge = (inBand && edgeBand > 0.88f) || (inPuff && edgePuff > 0.9f);
                        pixels[(y * width) + x] = nearEdge ? outline : white;
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply();

                chefHatSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.15f), 100f);
                return chefHatSprite;
            }
        }

        private static float EllipseDistance(Vector2 point, Vector2 center, Vector2 radius) =>
            Mathf.Sqrt(Mathf.Pow((point.x - center.x) / radius.x, 2f) + Mathf.Pow((point.y - center.y) / radius.y, 2f));

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
            CharacterPrefabs.Clear();
            Clips.Clear();
            sfxSource = null;
            uiFont = null;
            blobShadowSprite = null;
            chefHatSprite = null;
            particleMaterial = null;
            mainCamera = null;
        }
    }
}
