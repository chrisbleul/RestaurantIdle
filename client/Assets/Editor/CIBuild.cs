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
        /// Location 1 (Limonadenstand/Cafe) -- Kenney Furniture Kit (CC0).
        ///
        /// Vorher bestand die Szene aus einer Theke, sieben frei auf dem
        /// Rasen stehenden Kuechengeraeten und vier Bloecken aus dem
        /// Modular-Buildings-Kit, die zusammen nur das linke Drittel der
        /// Reihe abdeckten -- im Bild ein weisser Kasten neben der Theke,
        /// nicht als Gebaeude lesbar. Nutzer-Feedback dazu zweimal: "das
        /// Design gefaellt mir weiterhin nicht".
        ///
        /// Jetzt ein zusammenhaengender Raum: Innenboden unter der ganzen
        /// Reihe, durchgehende Rueckwand mit Fenstern, Gastbereich mit
        /// Tischen/Stuehlen dort, wo die Gaeste tatsaechlich laufen und
        /// anstehen (siehe GameManager.GuestEntrance/QueueSlotPosition),
        /// plus Deko an den Raendern.
        /// </summary>
        // Kenney-Furniture-Kit-Modelle sind nativ ~4x zu gross (Thekenhoehe
        // 4.2 Einheiten statt der erwarteten ~1) -- 0.25 bringt sie auf
        // plausible Meter-Massstab (Theke ~1.05m hoch), gemessen per
        // MeshRenderer.bounds ueber MCP statt geraten.
        private const float FurnitureScale = 0.25f;

        // Raumgrenzen. Die Stationsreihe laeuft von x=0 (Theke) bis x=5.85
        // (siehe RemainingStations), die Gaeste betreten die Szene links
        // davon und stehen bis x=-2.2 an -- der Raum muss beides fassen.
        private const float RoomMinX = -3.2f;
        private const float RoomMaxX = 7.2f;
        private const float RoomBackZ = 1.7f;
        private const float RoomFrontZ = -2.9f;

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

            BuildInteriorFloor();
            BuildStations();
            BuildBackWall();
            BuildGuestArea();
        }

        /// <summary>
        /// Innenboden als eigene Flaeche knapp ueber dem Rasen. Ohne ihn
        /// stehen Theke und Geraete direkt auf der Wiese -- das liest sich
        /// als Picknick, nicht als Lokal. Bewusst eine eingefaerbte Plane
        /// statt gekachelter floorFull-Modelle: fuer eine einfarbige Flaeche
        /// waeren rund 50 zusaetzliche GameObjects reine Verschwendung.
        /// </summary>
        private static void BuildInteriorFloor()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "InteriorFloor";
            // Plane ist nativ 10x10 Einheiten -- Skalierung entsprechend.
            var width = RoomMaxX - RoomMinX;
            var depth = RoomBackZ - RoomFrontZ;
            floor.transform.position = new Vector3((RoomMinX + RoomMaxX) / 2f, 0.01f, (RoomFrontZ + RoomBackZ) / 2f);
            floor.transform.localScale = new Vector3(width / 10f, 1f, depth / 10f);
            floor.GetComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                color = new Color(0.78f, 0.66f, 0.5f),
            };

            // Der Boden darf keine Taps abfangen -- GameManager.HandleStationTap
            // wertet jeden Raycast-Treffer aus und wuerde sonst nie die
            // Station dahinter erreichen.
            Object.DestroyImmediate(floor.GetComponent<MeshCollider>());
        }

        private static void BuildStations()
        {
            // Theke: Pivot liegt an einer Ecke, nicht in der Mitte (Kenney-
            // Konvention fuers Aneinanderreihen von Modulen) -- deshalb bei
            // 0/0/0 platziert statt zentriert.
            InstantiateModel("Assets/Models/Furniture/kitchenBar.fbx", "Station_Kaffeemaschine_Theke",
                Vector3.zero, Quaternion.identity, FurnitureScale);
            InstantiateModel("Assets/Models/Furniture/kitchenBarEnd.fbx", "Theke_Abschluss",
                new Vector3(-1.05f, 0f, 0f), Quaternion.identity, FurnitureScale);

            // Auf der Thekenoberflaeche (Thekenhoehe 1.05m nach Skalierung),
            // ungefaehr mittig ueber der Theke (deren eigener Mittelpunkt
            // liegt bei x=0.54/z=0.26 relativ zum Eck-Pivot).
            InstantiateModel("Assets/Models/Furniture/kitchenCoffeeMachine.fbx", "Station_Kaffeemaschine",
                new Vector3(0.5f, 1.05f, 0.25f), Quaternion.identity, FurnitureScale, stationIndex: 0);

            InstantiateModel("Assets/Models/Furniture/stoolBar.fbx", "Hocker",
                new Vector3(0.5f, 0f, -0.75f), Quaternion.identity, FurnitureScale);

            // Restliche 6 Stationen (StationCatalog.All Index 1-6) als Reihe
            // auf dem Boden -- Modelle sind Annaeherungen (kein 1:1-Match zu
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

                // Haengeschrank ueber jeder zweiten Station -- fuellt die
                // sonst voellig leere Wandflaeche zwischen Geraetereihe und
                // Wandoberkante, ohne den Blick auf die Station zu nehmen.
                if (i % 2 == 0)
                {
                    InstantiateModel("Assets/Models/Furniture/kitchenCabinetUpper.fbx", $"Haengeschrank_{i}",
                        new Vector3(x, 1.35f, RoomBackZ - 0.15f), Quaternion.identity, FurnitureScale);
                }
            }

            InstantiateModel("Assets/Models/Furniture/hoodModern.fbx", "Dunstabzug",
                new Vector3(2.45f, 1.3f, 0.1f), Quaternion.identity, FurnitureScale);
        }

        /// <summary>
        /// Durchgehende Rueckwand ueber die ganze Raumbreite. Die Breite
        /// eines Segments wird am Modell gemessen statt geschaetzt -- genau
        /// diese Schaetzerei war beim vorherigen Wandversuch (Modular
        /// Buildings mit FurnitureScale) die Fehlerquelle: die Wand war
        /// 0.156 Einheiten hoch und damit unsichtbar.
        /// </summary>
        private static void BuildBackWall()
        {
            var segmentWidth = MeasureWidth("Assets/Models/Furniture/wall.fbx") * FurnitureScale;
            if (segmentWidth <= 0.01f)
            {
                Debug.LogWarning("Wandbreite nicht messbar -- Rueckwand uebersprungen.");
                return;
            }

            var count = Mathf.CeilToInt((RoomMaxX - RoomMinX) / segmentWidth);
            for (var i = 0; i < count; i++)
            {
                // Jedes zweite Segment mit Fenster: eine geschlossene Wand
                // ueber die volle Breite wirkt wie eine Mauer, Fenster
                // machen daraus einen Gastraum.
                var model = i % 2 == 1 ? "wallWindow" : "wall";
                InstantiateModel($"Assets/Models/Furniture/{model}.fbx", $"Wall_{i}",
                    new Vector3(RoomMinX + i * segmentWidth, 0f, RoomBackZ), Quaternion.identity, FurnitureScale);
            }

            Debug.Log($"Rueckwand: {count} Segmente a {segmentWidth:F2} Einheiten.");
        }

        /// <summary>
        /// Gastbereich vor der Theke -- exakt der Streifen, durch den die
        /// Gaeste laufen und in dem sie anstehen (GameManager.GuestEntrance
        /// liegt bei z=-1.2, die Warteplaetze links davon). Der Bereich war
        /// bisher leerer Rasen und hat rund die Haelfte des Bildes gefuellt,
        /// ohne irgendetwas zu zeigen.
        /// </summary>
        private static void BuildGuestArea()
        {
            InstantiateModel("Assets/Models/Furniture/doorwayFront.fbx", "Eingang",
                new Vector3(RoomMinX + 0.2f, 0f, -1.2f), Quaternion.Euler(0f, 90f, 0f), FurnitureScale);
            InstantiateModel("Assets/Models/Furniture/rugRectangle.fbx", "Eingangsteppich",
                new Vector3(-1.6f, 0.02f, -1.2f), Quaternion.identity, FurnitureScale);

            // Zwei Sitzgruppen im vorderen Streifen. Bewusst nur zwei: der
            // Bereich muss frei genug bleiben, dass die laufenden Gaeste und
            // die Warteschlange lesbar bleiben.
            var tableX = new[] { 1.4f, 4.2f };
            for (var i = 0; i < tableX.Length; i++)
            {
                var x = tableX[i];
                InstantiateModel("Assets/Models/Furniture/tableCloth.fbx", $"Gasttisch_{i}",
                    new Vector3(x, 0f, -2.3f), Quaternion.identity, FurnitureScale);
                InstantiateModel("Assets/Models/Furniture/chair.fbx", $"Gaststuhl_{i}a",
                    new Vector3(x - 0.55f, 0f, -2.3f), Quaternion.Euler(0f, 90f, 0f), FurnitureScale);
                InstantiateModel("Assets/Models/Furniture/chairCushion.fbx", $"Gaststuhl_{i}b",
                    new Vector3(x + 0.55f, 0f, -2.3f), Quaternion.Euler(0f, -90f, 0f), FurnitureScale);
            }

            InstantiateModel("Assets/Models/Furniture/pottedPlant.fbx", "Pflanze_Links",
                new Vector3(RoomMinX + 0.35f, 0f, RoomBackZ - 0.5f), Quaternion.identity, FurnitureScale);
            InstantiateModel("Assets/Models/Furniture/pottedPlant.fbx", "Pflanze_Rechts",
                new Vector3(RoomMaxX - 0.5f, 0f, RoomBackZ - 0.5f), Quaternion.identity, FurnitureScale);
            InstantiateModel("Assets/Models/Furniture/plantSmall2.fbx", "Pflanze_Theke",
                new Vector3(-0.45f, 1.05f, 0.25f), Quaternion.identity, FurnitureScale);
            InstantiateModel("Assets/Models/Furniture/lampSquareFloor.fbx", "Stehlampe",
                new Vector3(RoomMaxX - 0.6f, 0f, -2.2f), Quaternion.identity, FurnitureScale);
            InstantiateModel("Assets/Models/Furniture/trashcan.fbx", "Muelleimer",
                new Vector3(RoomMinX + 0.5f, 0f, -2.4f), Quaternion.identity, FurnitureScale);
        }

        /// <summary>Breite eines Modells in Weltmasse bei Scale 1 -- fuer Kachelabstaende, damit keine Segmentbreite geraten werden muss.</summary>
        private static float MeasureWidth(string assetPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
            {
                return 0f;
            }

            var probe = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            probe.transform.position = Vector3.zero;
            probe.transform.localScale = Vector3.one;

            var renderers = probe.GetComponentsInChildren<MeshRenderer>();
            var width = 0f;
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers)
                {
                    bounds.Encapsulate(renderer.bounds);
                }

                width = bounds.size.x;
            }

            Object.DestroyImmediate(probe);
            return width;
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
