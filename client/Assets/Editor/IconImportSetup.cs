using UnityEditor;

namespace RestaurantIdle.Editor
{
    /// <summary>
    /// Die Kenney-Food-Kit-Icons (CC0, Assets/Resources/Icons) muessen als
    /// UI-Sprite importiert werden, nicht als generische Default-Textur --
    /// sonst kann kein Image-Component sie referenzieren. Laeuft automatisch
    /// beim Editor-Laden/Neukompilieren (gleiches Muster wie
    /// CIBuild.AutoCreateSceneIfMissing), idempotent: setzt nur um, was noch
    /// nicht stimmt, statt jedes Mal neu zu importieren.
    /// </summary>
    public static class IconImportSetup
    {
        private const string IconsFolder = "Assets/Resources/Icons";

        [InitializeOnLoadMethod]
        private static void EnsureSpriteImportSettings()
        {
            EditorApplication.delayCall += ApplySettings;
        }

        private static void ApplySettings()
        {
            if (!AssetDatabase.IsValidFolder(IconsFolder))
            {
                return;
            }

            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { IconsFolder }))
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
                importer.SaveAndReimport();
            }
        }
    }
}
