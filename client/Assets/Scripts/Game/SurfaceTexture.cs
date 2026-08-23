using UnityEngine;

namespace RestaurantIdle.Game
{
    /// <summary>
    /// Prozedurale Strukturen fuer die beiden grossen Bodenflaechen.
    ///
    /// Innenboden und Rasen waren einfarbige Flaechen -- im Hochformat
    /// fuellt allein der Innenboden gut ein Drittel des Bildes, ohne
    /// irgendeine Information zu tragen. Eine gleichmaessige Farbflaeche
    /// dieser Groesse laesst die Szene leer wirken und nimmt ihr jeden
    /// Groessenbezug: ohne Fugenraster fehlt dem Auge der Massstab, an dem
    /// es ablesen koennte, wie gross der Raum eigentlich ist.
    ///
    /// Erzeugt statt mitgeliefert, weil beide Muster reine Rechenergebnisse
    /// sind (Raster bzw. Rauschen) -- ein PNG dafuer waere Ballast, und die
    /// Groesse liesse sich nicht mehr an den Raum anpassen. Die Texturen
    /// sind bewusst neutral hell gehalten: eingefaerbt wird ueber
    /// _BaseColor aus dem LocationTheme, sodass eine Renovierung weiterhin
    /// nur die Farbe wechselt und das Muster behaelt.
    /// </summary>
    public static class SurfaceTexture
    {
        /// <summary>Kantenlaenge einer Fliese in Weltmasse -- bestimmt zusammen mit TilesPerSide die Texturkachelung.</summary>
        public const float TileWorldSize = 0.62f;

        /// <summary>Kantenlaenge eines Rausch-Ausschnitts in Weltmasse.</summary>
        public const float GrainWorldSize = 5f;

        private const int TilesPerSide = 4;
        private const int PixelsPerTile = 64;

        private static Texture2D tiles;
        private static Texture2D grain;

        /// <summary>Fliesenraster mit Fugen und leichter Helligkeitsstreuung je Fliese.</summary>
        public static Texture2D Tiles
        {
            get
            {
                if (tiles != null)
                {
                    return tiles;
                }

                const int size = TilesPerSide * PixelsPerTile;
                const int groutWidth = 3;

                tiles = CreateTexture("FloorTiles", size);
                var pixels = new Color32[size * size];

                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        var inTileX = x % PixelsPerTile;
                        var inTileY = y % PixelsPerTile;
                        var isGrout = inTileX < groutWidth || inTileY < groutWidth;

                        // Streuung je Fliese, nicht je Pixel: sonst wird aus
                        // dem Muster ein Rauschteppich. Deterministisch aus
                        // den Fliesenkoordinaten, damit die Textur bei jedem
                        // Start identisch aussieht.
                        var tileX = x / PixelsPerTile;
                        var tileY = y / PixelsPerTile;
                        var variation = (Hash(tileX, tileY) - 0.5f) * 0.08f;

                        var value = isGrout ? 0.74f : 1f + variation;
                        pixels[(y * size) + x] = ToColor(value);
                    }
                }

                tiles.SetPixels32(pixels);
                tiles.Apply();
                return tiles;
            }
        }

        /// <summary>Feines, weiches Rauschen fuer die Aussenflaeche -- bricht die Einfarbigkeit, ohne als Muster aufzufallen.</summary>
        public static Texture2D Grain
        {
            get
            {
                if (grain != null)
                {
                    return grain;
                }

                const int size = 128;
                grain = CreateTexture("GroundGrain", size);
                var pixels = new Color32[size * size];

                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        // Zwei Perlin-Frequenzen uebereinander: die grobe
                        // erzeugt Flecken, die feine bricht deren Raender.
                        // Perlin statt Zufall, weil zufaellige Einzelpixel
                        // beim Verkleinern zu Griesel werden.
                        var coarse = Mathf.PerlinNoise(x / (float)size * 4f, y / (float)size * 4f);
                        var fine = Mathf.PerlinNoise(x / (float)size * 13f, y / (float)size * 13f);
                        var value = 1f + ((coarse * 0.7f + fine * 0.3f) - 0.5f) * 0.14f;
                        pixels[(y * size) + x] = ToColor(value);
                    }
                }

                grain.SetPixels32(pixels);
                grain.Apply();
                return grain;
            }
        }

        private static Texture2D CreateTexture(string name, int size) =>
            new Texture2D(size, size, TextureFormat.RGBA32, mipChain: true)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };

        private static Color32 ToColor(float value)
        {
            var channel = (byte)(Mathf.Clamp01(value) * 255f);
            return new Color32(channel, channel, channel, 255);
        }

        /// <summary>Deterministischer Pseudozufall 0..1 aus zwei ganzzahligen Koordinaten.</summary>
        private static float Hash(int x, int y)
        {
            var value = Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f;
            return value - Mathf.Floor(value);
        }

        /// <summary>Setzt statische Felder zurueck -- ohne "Reload Domain" ueberleben sie den Play-Modus (siehe GameAssets).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            tiles = null;
            grain = null;
        }
    }
}
