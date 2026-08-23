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
            var lookTarget = RestaurantLayout.StationPosition(1) + new Vector3(0f, 0.4f, 0f);
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
            RenderSettings.ambientLight = new Color(0.52f, 0.52f, 0.56f);

            BuildLocation1Placeholder();

            var gameManagerObject = new GameObject("GameManager");
            gameManagerObject.AddComponent<GameManager>();

            EditorSceneManager.SaveScene(scene, ScenePath);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        /// <summary>
        /// Location 1 (Limonadenstand/Cafe) -- Kenney Furniture Kit (CC0).
        ///
        /// Der Grundriss steht in RestaurantLayout und wird von der
        /// Gast-Simulation genauso gelesen wie hier: Theke entlang der
        /// Bildschirm-Senkrechten (Weltdiagonale 1,0,1), Gastraum links
        /// davon, Wand rechts dahinter.
        /// </summary>
        /// <remarks>
        /// Alle Modelle werden ueber eine gemessene ZIELGROESSE platziert,
        /// nicht ueber einen gemeinsamen Skalierungsfaktor. Der bisherige
        /// Ansatz (ein FurnitureScale fuer alles) setzte voraus, dass die
        /// Modelle des Kits untereinander massstabsgetreu sind -- sie sind
        /// es nicht: die Theke misst nativ 4.2 Einheiten, ein Wandsegment
        /// 10, ein Muelleimer ebenfalls ein Vielfaches seiner realen
        /// Groesse. Mit einem einzigen Faktor ueberragte der Muelleimer die
        /// Theke, und jede Korrektur war reines Probieren am Screenshot.
        /// </remarks>
        private enum FitAxis
        {
            Height,
            Width,
        }

        /// <summary>Abstand der Rueckwand von der Thekenlinie, Richtung Wandseite (im Bild rechts).</summary>
        private const float WallOffset = 1.5f;

        private const float CounterHeight = 1.05f;
        private const float WallHeight = 2.4f;

        private static Vector3 WallSide => -RestaurantLayout.GuestSide;

        private static void BuildLocation1Placeholder()
        {
            BuildGround();
            BuildStations();
            BuildBackWall();
            BuildGuestArea();
        }

        private static void BuildGround()
        {
            var center = RestaurantLayout.StationPosition(3);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = new Vector3(center.x, 0f, center.z);
            ground.transform.localScale = new Vector3(2f, 1f, 2f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                color = new Color(0.55f, 0.75f, 0.45f),
            };

            // Innenboden als eigene Flaeche knapp ueber dem Rasen: ohne ihn
            // stehen Theke und Geraete direkt auf der Wiese -- das liest
            // sich als Picknick, nicht als Lokal.
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "InteriorFloor";
            floor.transform.position = new Vector3(center.x, 0.012f, center.z)
                + RestaurantLayout.GuestSide * 1.3f
                - RestaurantLayout.CounterDirection * 1.4f;
            floor.transform.rotation = RestaurantLayout.CounterRotation;
            // Plane ist nativ 10x10 Einheiten: lokale X-Achse laeuft nach
            // dem Drehen entlang der Theke, lokale Z-Achse quer dazu.
            floor.transform.localScale = new Vector3(1.3f, 1f, 0.75f);
            floor.GetComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                color = new Color(0.82f, 0.71f, 0.56f),
            };

            // Weder Rasen noch Innenboden duerfen Taps abfangen --
            // GameManager.HandleStationTap wertet jeden Raycast-Treffer aus
            // und wuerde sonst nie die Station dahinter erreichen.
            Object.DestroyImmediate(floor.GetComponent<MeshCollider>());
            Object.DestroyImmediate(ground.GetComponent<MeshCollider>());
        }

        private static void BuildStations()
        {
            var rotation = RestaurantLayout.CounterRotation;

            // Theke unter der ersten Station. Pivot liegt an einer Ecke,
            // nicht in der Mitte (Kenney-Konvention fuers Aneinanderreihen
            // von Modulen).
            var counterBase = RestaurantLayout.StationPosition(0)
                - RestaurantLayout.CounterDirection * 0.55f
                - RestaurantLayout.GuestSide * 0.28f;
            InstantiateModel("Assets/Models/Furniture/kitchenBar.fbx", "Station_Kaffeemaschine_Theke",
                counterBase, rotation, CounterHeight);
            InstantiateModel("Assets/Models/Furniture/kitchenBarEnd.fbx", "Theke_Abschluss",
                counterBase - RestaurantLayout.CounterDirection * 1.1f, rotation, CounterHeight);

            InstantiateModel("Assets/Models/Furniture/kitchenCoffeeMachine.fbx", "Station_Kaffeemaschine",
                RestaurantLayout.StationPosition(0) + new Vector3(0f, CounterHeight, 0f),
                rotation, 0.45f, FitAxis.Height, stationIndex: 0);

            var stoolSpot = RestaurantLayout.GuestStandPosition(RestaurantLayout.StationPosition(0));
            InstantiateModel("Assets/Models/Furniture/stoolBar.fbx", "Hocker",
                new Vector3(stoolSpot.x, 0f, stoolSpot.z), rotation, 0.7f);

            // Restliche 6 Stationen (StationCatalog.All Index 1-6) --
            // Modelle sind Annaeherungen (kein 1:1-Match zu jedem
            // Stationsnamen im Kenney-Kit vorhanden).
            var remainingStations = new (string Model, string StationName, float Height)[]
            {
                ("kitchenStoveElectric.fbx", "Fritteuse", 0.95f),
                ("kitchenStove.fbx", "Grill", 0.95f),
                ("kitchenMicrowave.fbx", "Pizzaofen", 0.55f),
                ("kitchenSink.fbx", "Sushi-Bar", 0.9f),
                ("kitchenFridgeSmall.fbx", "Patisserie", 1.1f),
                ("tableRound.fbx", "Chefs Table", 0.8f),
            };

            for (var i = 0; i < remainingStations.Length; i++)
            {
                var (model, stationName, height) = remainingStations[i];
                var position = RestaurantLayout.StationPosition(i + 1);
                InstantiateModel($"Assets/Models/Furniture/{model}", $"Station_{stationName}",
                    position, rotation, height, FitAxis.Height, stationIndex: i + 1);

                // Haengeschrank an der Wand hinter jeder zweiten Station --
                // fuellt die sonst voellig leere Wandflaeche, ohne den Blick
                // auf die Station selbst zu nehmen.
                if (i % 2 == 0)
                {
                    InstantiateModel("Assets/Models/Furniture/kitchenCabinetUpper.fbx", $"Haengeschrank_{i}",
                        position + WallSide * (WallOffset - 0.3f) + new Vector3(0f, 1.5f, 0f),
                        rotation, 0.6f);
                }
            }

            InstantiateModel("Assets/Models/Furniture/hoodModern.fbx", "Dunstabzug",
                RestaurantLayout.StationPosition(2) + new Vector3(0f, 1.55f, 0f), rotation, 0.6f);
        }

        /// <summary>
        /// Rueckwand parallel zur Theke. Segmentbreite wird am bereits auf
        /// Zielhoehe skalierten Modell gemessen -- Raten war beim
        /// vorherigen Wandversuch die Fehlerquelle (die Wand war am Ende
        /// 0.156 Einheiten hoch und damit unsichtbar).
        /// </summary>
        private static void BuildBackWall()
        {
            var segmentWidth = MeasureScaledWidth("Assets/Models/Furniture/wall.fbx", WallHeight);
            if (segmentWidth <= 0.01f)
            {
                Debug.LogWarning("Wandbreite nicht messbar -- Rueckwand uebersprungen.");
                return;
            }

            var length = RestaurantLayout.StationSpacing * 9f;
            var count = Mathf.CeilToInt(length / segmentWidth);
            var start = RestaurantLayout.StationPosition(0)
                + WallSide * WallOffset
                - RestaurantLayout.CounterDirection * 2.4f;

            for (var i = 0; i < count; i++)
            {
                // Jedes zweite Segment mit Fenster: eine geschlossene Wand
                // ueber die volle Laenge wirkt wie eine Mauer, Fenster
                // machen daraus einen Gastraum.
                var model = i % 2 == 1 ? "wallWindow" : "wall";
                InstantiateModel($"Assets/Models/Furniture/{model}.fbx", $"Wall_{i}",
                    start + RestaurantLayout.CounterDirection * (i * segmentWidth),
                    RestaurantLayout.CounterRotation, WallHeight);
            }

            Debug.Log($"Rueckwand: {count} Segmente a {segmentWidth:F2} Einheiten.");
        }

        /// <summary>
        /// Gastbereich links der Theke -- genau der Streifen, durch den die
        /// Gaeste laufen und in dem sie anstehen (RestaurantLayout.Entrance
        /// und QueueSlot). Der Bereich war vorher leerer Rasen und hat rund
        /// die Haelfte des Bildes gefuellt, ohne etwas zu zeigen.
        /// </summary>
        private static void BuildGuestArea()
        {
            var rotation = RestaurantLayout.CounterRotation;
            var entrance = new Vector3(RestaurantLayout.Entrance.x, 0f, RestaurantLayout.Entrance.z);

            InstantiateModel("Assets/Models/Furniture/rugRectangle.fbx", "Eingangsteppich",
                entrance + new Vector3(0f, 0.02f, 0f), rotation, 1.5f, FitAxis.Width);

            // Sitzgruppen weiter links, ausserhalb der Laufwege: die
            // Warteschlange muss lesbar bleiben.
            for (var i = 0; i < 3; i++)
            {
                var anchor = RestaurantLayout.StationPosition(i * 2)
                    + RestaurantLayout.GuestSide * 2.4f
                    - RestaurantLayout.CounterDirection * 0.5f;

                InstantiateModel("Assets/Models/Furniture/tableCloth.fbx", $"Gasttisch_{i}",
                    anchor, rotation, 0.75f);
                InstantiateModel("Assets/Models/Furniture/chair.fbx", $"Gaststuhl_{i}a",
                    anchor - RestaurantLayout.CounterDirection * 0.6f, rotation, 0.85f);
                InstantiateModel("Assets/Models/Furniture/chairCushion.fbx", $"Gaststuhl_{i}b",
                    anchor + RestaurantLayout.CounterDirection * 0.6f,
                    rotation * Quaternion.Euler(0f, 180f, 0f), 0.85f);
            }

            InstantiateModel("Assets/Models/Furniture/pottedPlant.fbx", "Pflanze_Eingang",
                entrance + RestaurantLayout.GuestSide * 1.3f, rotation, 0.9f);
            InstantiateModel("Assets/Models/Furniture/pottedPlant.fbx", "Pflanze_Ende",
                RestaurantLayout.StationPosition(6) + RestaurantLayout.GuestSide * 1.5f, rotation, 0.9f);
            InstantiateModel("Assets/Models/Furniture/plantSmall2.fbx", "Pflanze_Theke",
                RestaurantLayout.StationPosition(0)
                    - RestaurantLayout.CounterDirection * 0.8f
                    + new Vector3(0f, CounterHeight, 0f),
                rotation, 0.25f);
            InstantiateModel("Assets/Models/Furniture/lampSquareFloor.fbx", "Stehlampe",
                RestaurantLayout.StationPosition(4) + RestaurantLayout.GuestSide * 3.2f, rotation, 1.5f);
            InstantiateModel("Assets/Models/Furniture/trashcan.fbx", "Muelleimer",
                RestaurantLayout.StationPosition(0) + WallSide * 0.9f - RestaurantLayout.CounterDirection * 1.8f,
                rotation, 0.55f);
        }

        /// <summary>
        /// Instanziert ein Modell und skaliert es auf eine gemessene
        /// Zielgroesse (Hoehe oder Breite in Weltmasse). Siehe
        /// Klassen-Remarks: die Kit-Modelle sind untereinander nicht
        /// massstabsgetreu, ein gemeinsamer Faktor fuehrt zwangslaeufig zu
        /// Muelleimern in Thekengroesse.
        /// </summary>
        private static void InstantiateModel(string assetPath, string instanceName, Vector3 position, Quaternion rotation,
            float targetSize, FitAxis axis = FitAxis.Height, int? stationIndex = null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
            {
                Debug.LogWarning($"Modell nicht gefunden: {assetPath}");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = instanceName;
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            var nativeSize = MeasureBounds(instance).size;
            var reference = axis == FitAxis.Height ? nativeSize.y : Mathf.Max(nativeSize.x, nativeSize.z);
            var scale = reference > 0.0001f ? targetSize / reference : 1f;

            instance.transform.localScale = Vector3.one * scale;
            instance.transform.rotation = rotation;
            instance.transform.position = position;

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

        /// <summary>Breite eines auf Zielhoehe skalierten Modells -- fuer nahtlose Kachelabstaende der Wand.</summary>
        private static float MeasureScaledWidth(string assetPath, float targetHeight)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
            {
                return 0f;
            }

            var probe = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            probe.transform.position = Vector3.zero;
            probe.transform.rotation = Quaternion.identity;
            probe.transform.localScale = Vector3.one;

            var size = MeasureBounds(probe).size;
            Object.DestroyImmediate(probe);

            if (size.y <= 0.0001f)
            {
                return 0f;
            }

            return Mathf.Max(size.x, size.z) * (targetHeight / size.y);
        }

        private static Bounds MeasureBounds(GameObject instance)
        {
            var renderers = instance.GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length == 0)
            {
                return new Bounds(instance.transform.position, Vector3.one);
            }

            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }

            return bounds;
        }
    }

    internal class BuildFailedException : System.Exception
    {
        public BuildFailedException(string message) : base(message)
        {
        }
    }
}
