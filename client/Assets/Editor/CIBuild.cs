using System.IO;
using RestaurantIdle.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RestaurantIdle.Editor
{
    /// <summary>
    /// Baut Szene und Build programmatisch statt eine .unity-Datei von Hand als
    /// YAML zu pflegen -- robuster fuer CI (game-ci/unity-builder, siehe
    /// .github/workflows/webgl-build-deploy.yml), weil Unity selbst
    /// serialisiert statt eine handgeschriebene Szenen-Datei zu riskieren.
    /// </summary>
    public static class CIBuild
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";

        public static void BuildWebGl()
        {
            EnsureMainScene();

            var report = BuildPipeline.BuildPlayer(
                new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = "Build/WebGL",
                    target = BuildTarget.WebGL,
                    options = BuildOptions.None,
                });

            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                throw new BuildFailedException($"WebGL-Build fehlgeschlagen: {report.summary.result}");
            }
        }

        /// <summary>
        /// Fuer lokale Editor-Entwicklung: erzeugt/ueberschreibt die Szene manuell,
        /// ohne einen vollen WebGL-Build anzustossen. CI braucht das nicht (baut
        /// ueber BuildWebGl selbst), aber wer das Projekt frisch im Editor
        /// oeffnet, findet sonst keine Assets/Scenes/Main.unity zum Bearbeiten/
        /// Abspielen -- die entsteht bisher nur waehrend BuildWebGl.
        /// </summary>
        [MenuItem("RestaurantIdle/Szene fuer Editor erzeugen")]
        public static void EnsureMainSceneMenuItem() => EnsureMainScene();

        /// <summary>
        /// Erzeugt die Szene automatisch beim Laden/Neukompilieren des Editors,
        /// falls sie noch fehlt -- damit niemand den Menuepunkt von Hand
        /// anklicken muss, nur weil das Projekt frisch ausgecheckt wurde.
        /// Ueber delayCall verzoegert, weil ein Szenenwechsel waehrend der
        /// Editor-Initialisierung selbst nicht sicher ist.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void AutoCreateSceneIfMissing()
        {
            if (File.Exists(ScenePath))
            {
                return;
            }

            EditorApplication.delayCall += EnsureMainScene;
        }

        private static void EnsureMainScene()
        {
            Directory.CreateDirectory("Assets/Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var gameManagerObject = new GameObject("GameManager");
            gameManagerObject.AddComponent<GameManager>();

            EditorSceneManager.SaveScene(scene, ScenePath);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }

    internal class BuildFailedException : System.Exception
    {
        public BuildFailedException(string message) : base(message)
        {
        }
    }
}
