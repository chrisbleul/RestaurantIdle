using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace RestaurantIdle.Editor
{
    /// <summary>
    /// Stellt den Game-View auf das Zielformat des Spiels (Portrait 1080x1920,
    /// dieselbe Referenzaufloesung wie GameManager.BuildUi setzt).
    ///
    /// Ohne das laeuft der Game-View auf "Free Aspect" -- im Editor-Fenster
    /// also querformatig. Jede visuelle Pruefung des Layouts findet dann in
    /// einem Seitenverhaeltnis statt, das auf dem Zielgeraet nie vorkommt:
    /// die mitwachsende Kamera (GameManager.RecomputeCameraTarget) rechnet
    /// den Zoom direkt aus camera.aspect, und die HUD-Leisten decken je nach
    /// Hoehe einen voellig anderen Anteil des Bildes ab. Ein im Querformat
    /// beurteiltes Layout sagt ueber das Spiel auf dem Handy nichts aus.
    ///
    /// Unity bietet dafuer keine oeffentliche API -- GameViewSizes und
    /// GameView sind intern, deshalb Reflection. Bewusst als Editor-Werkzeug
    /// ohne Laufzeitbezug, faellt bei fehlenden internen Typen still zurueck
    /// (Warnung), statt den Editor-Start zu gefaehrden.
    /// </summary>
    public static class GameViewPortrait
    {
        /// <summary>
        /// Zweite, kleine Groesse im selben Seitenverhaeltnis (9:16). Grund:
        /// bei 1080x1920 passt der Game-View nicht ins Editor-Fenster und
        /// wird mit 0.25x angezeigt -- diese Verkleinerung wirft jede
        /// Kantenglaettung weg, bevor ein Screenshot sie zeigen koennte.
        /// Eine Pruefung, ob MSAA wirklich greift, ist damit unmoeglich.
        /// 405x720 laesst sich mit Skalierung 1x darstellen, also Pixel fuer
        /// Pixel so, wie gerendert wurde.
        /// </summary>
        [MenuItem("RestaurantIdle/Game-View auf Portrait klein (405x720)")]
        public static void ApplySmall() => Apply("RestaurantIdle Portrait klein", 405, 720);

        [MenuItem("RestaurantIdle/Game-View auf Portrait (1080x1920)")]
        public static void Apply() => Apply("RestaurantIdle Portrait", 1080, 1920);

        private static void Apply(string sizeLabel, int targetWidth, int targetHeight)
        {
            try
            {
                var editorAssembly = typeof(UnityEditor.Editor).Assembly;
                var sizesType = editorAssembly.GetType("UnityEditor.GameViewSizes");
                var singletonType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                var sizes = singletonType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static)
                    ?.GetValue(null);
                var group = sizesType.GetProperty("currentGroup")?.GetValue(sizes);
                if (group == null)
                {
                    Debug.LogWarning("Game-View-Groessen nicht erreichbar -- Portrait-Umschaltung uebersprungen.");
                    return;
                }

                var groupType = group.GetType();
                var index = IndexOfExistingSize(groupType, group, sizeLabel);
                if (index < 0)
                {
                    var sizeType = editorAssembly.GetType("UnityEditor.GameViewSize");
                    var sizeTypeEnum = editorAssembly.GetType("UnityEditor.GameViewSizeType");
                    var constructor = sizeType.GetConstructor(new[] { sizeTypeEnum, typeof(int), typeof(int), typeof(string) });
                    var newSize = constructor.Invoke(new[]
                    {
                        Enum.Parse(sizeTypeEnum, "FixedResolution"),
                        (object)targetWidth,
                        targetHeight,
                        sizeLabel,
                    });

                    groupType.GetMethod("AddCustomSize").Invoke(group, new[] { newSize });
                    index = IndexOfExistingSize(groupType, group, sizeLabel);
                }

                var gameViewType = editorAssembly.GetType("UnityEditor.GameView");
                var window = EditorWindow.GetWindow(gameViewType, false, "Game", false);
                gameViewType
                    .GetMethod("SizeSelectionCallback", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.Invoke(window, new object[] { index, null });

                Debug.Log($"Game-View auf {targetWidth}x{targetHeight} (Index {index}) gesetzt.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Game-View konnte nicht auf Portrait gestellt werden: {e.Message}");
            }
        }

        private static int IndexOfExistingSize(Type groupType, object group, string sizeLabel)
        {
            var totalCount = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
            var getSize = groupType.GetMethod("GetGameViewSize");
            for (var i = 0; i < totalCount; i++)
            {
                var size = getSize.Invoke(group, new object[] { i });
                var name = size.GetType().GetProperty("baseText")?.GetValue(size) as string;
                if (name == sizeLabel)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
