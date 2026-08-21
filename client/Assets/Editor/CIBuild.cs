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
            // Winkel, 45 Grad Drehung.
            //
            // Nutzer-Feedback ("das design gefaellt mir weiterhin nicht"):
            // 30 Grad Neigung ist flach genug, dass Himmel UND Boden bis
            // zum Horizont ins Bild kommen -- bei der engen Fruehspiel-
            // Kamera (wenig Inhalt) blieb dadurch mehr leere Flaeche als
            // Inhalt sichtbar, egal wie die einzelnen Modelle skaliert
            // waren. 55 Grad (steilerer, mehr top-down Blick, wie bei
            // Eatventure/Cat Snack Bar) zeigt stattdessen ueberwiegend die
            // Arbeitsflaeche selbst -- der eigentliche Hebel war der
            // Kamerawinkel, nicht die Wand-Skalierung.
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 4f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.82f, 0.92f);
            cameraObject.transform.rotation = Quaternion.Euler(55f, 45f, 0f);
            // Zielpunkt ungefaehr in der Mitte der Location-1-Objekte, nicht
            // Weltursprung -- die Theke selbst hat ihren Pivot in einer Ecke.
            var lookTarget = new Vector3(2.8f, 0.4f, 0f);
            cameraObject.transform.position = lookTarget + cameraObject.transform.rotation * new Vector3(0, 0, -15f);

            var lightObject = new GameObject("Directional Light", typeof(Light));
            var light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // War vorher 0.75/0.75/0.8 -- zusammen mit dem Directional Light
            // hat das jede Materialfarbe stark Richtung Weiss verwaschen
            // (sichtbar beim Location-Farbwechsel: sattes Grau kam als
            // blasses Lavendel an). Deutlich gedaempft, damit Basisfarben
            // erkennbar bleiben.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.35f, 0.35f, 0.4f);

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
        // Kenney-Furniture-Kit-Modelle sind nativ ~4x zu gross (Thekenhoehe
        // 4.2 Einheiten statt der erwarteten ~1) -- 0.25 bringt sie auf
        // plausible Meter-Massstab (Theke ~1.05m hoch), gemessen per
        // MeshRenderer.bounds ueber MCP statt geraten.
        private const float FurnitureScale = 0.25f;

        private static void BuildLocation1Placeholder()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = new Vector3(4f, 0f, 0f);
            ground.transform.localScale = new Vector3(1.5f, 1f, 1f);
            var groundRenderer = ground.GetComponent<MeshRenderer>();
            groundRenderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                color = new Color(0.55f, 0.75f, 0.45f),
            };

            // Theke: Pivot liegt an einer Ecke, nicht in der Mitte (Kenney-
            // Konvention fuers Aneinanderreihen von Modulen) -- deshalb bei
            // 0/0/0 platziert statt zentriert.
            InstantiateModel("Assets/Models/Furniture/kitchenBar.fbx", "Station_Kaffeemaschine_Theke",
                Vector3.zero, Quaternion.identity, FurnitureScale);

            // Auf der Thekenoberflaeche (Thekenhoehe 1.05m nach Skalierung),
            // ungefaehr mittig ueber der Theke (deren eigener Mittelpunkt
            // liegt bei x=0.54/z=0.26 relativ zum Eck-Pivot).
            InstantiateModel("Assets/Models/Furniture/kitchenCoffeeMachine.fbx", "Station_Kaffeemaschine",
                new Vector3(0.5f, 1.05f, 0.25f), Quaternion.identity, FurnitureScale, stationIndex: 0);

            InstantiateModel("Assets/Models/Furniture/stoolBar.fbx", "Hocker",
                new Vector3(0.5f, 0f, -0.6f), Quaternion.identity, FurnitureScale);

            // Restliche 6 Stationen (StationCatalog.All Index 1-6) als Reihe
            // auf dem Boden -- noch ohne eigene Theke, reine Platzierung wie
            // bei Station 0. Modelle sind Annaeherungen (kein 1:1-Match zu
            // jedem Stationsnamen im Kenney-Kit vorhanden).
            var remainingStations = new (string Model, string StationName)[]
            {
                ("kitchenStoveElectric.fbx", "Fritteuse"),
                ("kitchenStove.fbx", "Grill"),
                ("kitchenMicrowave.fbx", "Pizzaofen"),
                ("kitchenSink.fbx", "Sushi-Bar"),
                ("kitchenFridgeSmall.fbx", "Patisserie"),
                ("tableRound.fbx", "Chefs Table"),
            };

            for (var i = 0; i < remainingStations.Length; i++)
            {
                var (model, stationName) = remainingStations[i];
                var x = 1.6f + i * 0.85f;
                InstantiateModel($"Assets/Models/Furniture/{model}", $"Station_{stationName}",
                    new Vector3(x, 0f, 0f), Quaternion.identity, FurnitureScale, stationIndex: i + 1);
            }

            BuildBackWall();
        }

        /// <summary>
        /// Restaurant-Rueckwand hinter der Stationsreihe (Kenney Modular
        /// Buildings, CC0) -- Nutzerwunsch: der Hintergrund soll wie ein
        /// Restaurant aussehen statt nur gruene Wiese. Selber Massstab-
        /// Ansatz wie bei Location1Placeholder (FurnitureScale), noch nicht
        /// per MeshRenderer.bounds vermessen -- ggf. nach dem ersten
        /// Screenshot nachjustieren.
        /// </summary>
        // PLANv3-Nachfolge, Nutzer-Feedback ("das spiel muss viel besser
        // werden"): die Rueckwand nutzte bisher FurnitureScale (0.25) --
        // genau der Platzhalter, den der urspruengliche Kommentar hier
        // ("noch nicht per MeshRenderer.bounds vermessen") als provisorisch
        // markiert hatte. Gemessen: building-block.fbx ist bei Scale 1
        // 1.0 x 0.625 x 1.0 -- bei 0.25 also nur 0.156 Einheiten hoch,
        // praktisch eine unsichtbare Bodenleiste. Die komplette Rueckwand
        // war dadurch effektiv nie sichtbar. Eigene, echt vermessene
        // BuildingScale statt FurnitureScale.
        //
        // 3.2 (Zielhoehe ~2 Einheiten, deutlich hoeher als die Stationen)
        // war beim ersten Screenshot-Abgleich massiv zu gross fuer die
        // mitwachsende Kamera aus dem Fun-Pass -- bei minimalem Zoom
        // (Fruehspiel, ein bis zwei Stationen sichtbar) fuellte die Wand
        // fast den ganzen Bildschirm. 1.0 (Wandhoehe = native 0.625, in
        // derselben Groessenordnung wie die Stationen) passt sich der
        // engen Fruehspiel-Kamera unter, ohne dass die Wand komplett
        // verschwindet -- Kompromiss statt einer fuer die feste alte
        // Kamera "richtig" gemessenen, aber fuer die neue dynamische
        // Kamera zu grossen Zahl.
        private const float BuildingScale = 1.0f;

        private static void BuildBackWall()
        {
            const float wallZ = 1.5f;
            const float segmentSpacing = BuildingScale; // native Breite 1.0 -> Segmente stossen nahtlos aneinander.
            var wallSegments = new[] { "building-block", "building-door-window", "building-block", "building-window" };

            for (var i = 0; i < wallSegments.Length; i++)
            {
                var x = -1f + i * segmentSpacing;
                InstantiateModel($"Assets/Models/Building/{wallSegments[i]}.fbx", $"Wall_{i}",
                    new Vector3(x, 0f, wallZ), Quaternion.Euler(0f, 180f, 0f), BuildingScale);
            }

            // Bewusst HOEHER als die architektonisch "korrekte" Position
            // auf der Wandoberkante (native Wandhoehe 0.625 * BuildingScale):
            // beim Screenshot-Abgleich verschwand die Awning dort fast
            // komplett hinter der vorderen Station. Weiter oben sitzend
            // zeichnet sie eine klar sichtbare Dachlinie gegen den Himmel
            // -- das liest sich als "Restaurant" deutlich staerker als
            // physikalische Genauigkeit an dieser Stelle bringt.
            InstantiateModel("Assets/Models/Building/roof-flat-awning-a.fbx", "Awning",
                new Vector3(0.5f, 1.3f, 0.9f), Quaternion.identity, BuildingScale);
        }

        private static void InstantiateModel(string assetPath, string instanceName, Vector3 position, Quaternion rotation,
            float scale = 1f, int? stationIndex = null)
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
            instance.transform.localScale = Vector3.one * scale;

            if (stationIndex.HasValue)
            {
                // MeshCollider statt BoxCollider -- passt sich automatisch
                // der tatsaechlichen Modellform an, kein manuelles Vermessen
                // noetig (reicht fuer reines Raycast-Antippen, siehe
                // GameManager.HandleStationTap).
                instance.AddComponent<MeshCollider>();
                var hotspot = instance.AddComponent<StationHotspot>();
                hotspot.StationIndex = stationIndex.Value;
            }
        }
    }

    internal class BuildFailedException : System.Exception
    {
        public BuildFailedException(string message) : base(message)
        {
        }
    }
}
