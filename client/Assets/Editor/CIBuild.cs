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

            // PLANv2.md Abschnitt 4: orthografische Kamera in isometrischem
            // Winkel (ca. 30 Grad Neigung, 45 Grad Drehung) statt der
            // bisherigen reinen UI-Kamera ohne eigenes Rendering.
            var cameraObject = new GameObject("Main Camera", typeof(Camera));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.82f, 0.92f);
            cameraObject.transform.rotation = Quaternion.Euler(30f, 45f, 0f);
            cameraObject.transform.position = cameraObject.transform.rotation * new Vector3(0, 0, -15f);

            var lightObject = new GameObject("Directional Light", typeof(Light));
            var light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.75f, 0.75f, 0.8f);

            BuildLocation1Placeholder();

            var gameManagerObject = new GameObject("GameManager");
            gameManagerObject.AddComponent<GameManager>();

            EditorSceneManager.SaveScene(scene, ScenePath);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        /// <summary>
        /// PLANv2.md Abschnitt 6/8: erster sichtbarer Baustein von Location 1
        /// (Limonadenstand) -- Kenney Furniture Kit (CC0). Noch reine
        /// Platzierung, keine Anbindung an GameManager/Balancing (folgt in
        /// einem eigenen Schritt, sobald die Optik steht).
        /// </summary>
        private static void BuildLocation1Placeholder()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(3f, 1f, 3f);
            var groundRenderer = ground.GetComponent<MeshRenderer>();
            groundRenderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                color = new Color(0.55f, 0.75f, 0.45f),
            };

            InstantiateModel("Assets/Models/Furniture/kitchenBar.fbx", "Station_Kaffeemaschine_Theke",
                new Vector3(0f, 0f, 0f), Quaternion.identity);
            InstantiateModel("Assets/Models/Furniture/kitchenCoffeeMachine.fbx", "Station_Kaffeemaschine",
                new Vector3(0f, 0.9f, 0f), Quaternion.identity);
            InstantiateModel("Assets/Models/Furniture/stoolBar.fbx", "Hocker",
                new Vector3(0f, 0f, -1.2f), Quaternion.identity);
        }

        private static void InstantiateModel(string assetPath, string instanceName, Vector3 position, Quaternion rotation)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
            {
                Debug.LogWarning($"Modell nicht gefunden: {assetPath}");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = instanceName;
            instance.transform.position = position;
            instance.transform.rotation = rotation;
        }
    }

    internal class BuildFailedException : System.Exception
    {
        public BuildFailedException(string message) : base(message)
        {
        }
    }
}
