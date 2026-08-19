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
