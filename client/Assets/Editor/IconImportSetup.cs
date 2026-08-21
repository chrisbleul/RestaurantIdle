using UnityEditor;
using UnityEngine;

namespace RestaurantIdle.Editor
{
    /// <summary>
    /// Alle Kenney-CC0-Sprites unter Assets/Resources (Icons, Characters, ...)
    /// muessen als UI-Sprite importiert werden, nicht als generische Default-
    /// Textur -- sonst kann kein Image-Component sie referenzieren. Laeuft
    /// automatisch beim Editor-Laden/Neukompilieren (gleiches Muster wie
    /// CIBuild.AutoCreateSceneIfMissing), idempotent: setzt nur um, was noch
    /// nicht stimmt, statt jedes Mal neu zu importieren. Bewusst der ganze
    /// Resources-Baum statt einzelner Unterordner, damit ein neuer Ordner
    /// (z.B. fuer weitere Charaktere) nicht wieder vergessen wird.
    /// </summary>
    public static class IconImportSetup
    {
        private const string ResourcesFolder = "Assets/Resources";

        /// <summary>
        /// PLANv3.md Phase E ("Kenney UI Pack einbinden, Buttons ... skinnen"):
        /// Sprites unter Resources/UI/ sind 9-Slice-Buttons (Kenney UI Pack,
        /// 192x64px), brauchen einen Border, sonst verzerren die runden Ecken
        /// beim Strecken auf Button-Groesse. Werte per Augenmass am
        /// Original-PNG abgelesen (abgerundete Ecke + Tiefen-Schatten unten),
        /// keine Pixel-exakte Vermessung -- 9-Slice verzeiht kleine
        /// Abweichungen.
        /// </summary>
        private const string UiSpriteFolder = "Assets/Resources/UI";
        private static readonly Vector4 UiSpriteBorder = new Vector4(18, 24, 18, 18);

        [InitializeOnLoadMethod]
        private static void EnsureSpriteImportSettings()
        {
            EditorApplication.delayCall += ApplySettings;
        }

        private static void ApplySettings()
        {
            if (!AssetDatabase.IsValidFolder(ResourcesFolder))
            {
                return;
            }

            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ResourcesFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                {
                    continue;
                }

                if (importer.textureType == TextureImporterType.Sprite)
                {
                    continue;
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;

                if (path.StartsWith(UiSpriteFolder))
                {
                    importer.spriteBorder = UiSpriteBorder;
                }

                importer.SaveAndReimport();
            }
        }
    }
}
