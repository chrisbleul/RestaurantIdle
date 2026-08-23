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
        private const string SizeLabel = "RestaurantIdle Portrait";
        private const int TargetWidth = 1080;
        private const int TargetHeight = 1920;

        [MenuItem("RestaurantIdle/Game-View auf Portrait (1080x1920)")]
        public static void Apply()
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
                var index = IndexOfExistingSize(groupType, group);
                if (index < 0)
                {
                    var sizeType = editorAssembly.GetType("UnityEditor.GameViewSize");
                    var sizeTypeEnum = editorAssembly.GetType("UnityEditor.GameViewSizeType");
                    var constructor = sizeType.GetConstructor(new[] { sizeTypeEnum, typeof(int), typeof(int), typeof(string) });
                    var newSize = constructor.Invoke(new[]
                    {
                        Enum.Parse(sizeTypeEnum, "FixedResolution"),
                        (object)TargetWidth,
                        TargetHeight,
                        SizeLabel,
                    });

                    groupType.GetMethod("AddCustomSize").Invoke(group, new[] { newSize });
                    index = IndexOfExistingSize(groupType, group);
                }

                var gameViewType = editorAssembly.GetType("UnityEditor.GameView");
                var window = EditorWindow.GetWindow(gameViewType, false, "Game", false);
                gameViewType
                    .GetMethod("SizeSelectionCallback", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.Invoke(window, new object[] { index, null });

                Debug.Log($"Game-View auf {TargetWidth}x{TargetHeight} (Index {index}) gesetzt.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Game-View konnte nicht auf Portrait gestellt werden: {e.Message}");
            }
        }

        private static int IndexOfExistingSize(Type groupType, object group)
        {
            var totalCount = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
            var getSize = groupType.GetMethod("GetGameViewSize");
            for (var i = 0; i < totalCount; i++)
            {
                var size = getSize.Invoke(group, new object[] { i });
                var name = size.GetType().GetProperty("baseText")?.GetValue(size) as string;
                if (name == SizeLabel)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
