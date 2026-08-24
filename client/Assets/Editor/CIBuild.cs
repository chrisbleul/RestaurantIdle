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

        /// <summary>
        /// Manueller Aufruf desselben Builds, den auch CI nutzt (ci.yml:
        /// buildMethod RestaurantIdle.Editor.CIBuild.BuildWebGl) -- fuer
        /// lokale Validierung ohne Deploy: Editor-Play-Mode strippt nie
        /// Shader, ein echter Build kann sich davon unterscheiden (siehe
        /// UrpSetup.EnsureActive-Kommentar zum Pink-Bug).
        /// </summary>
        [MenuItem("RestaurantIdle/Lokalen WebGL-Build erzeugen")]
        public static void BuildWebGl()
        {
            // Muss vor allem anderen laufen: der [InitializeOnLoadMethod]-Weg in
            // UrpSetup (delayCall) feuert im -executeMethod-Batch-Build nicht,
            // siehe EnsureActive-Kommentar dort. Ohne aktive URP-Pipeline
            // stripped der WebGL-Build alle URP-Shadervarianten weg -- Ergebnis
            // sind pinke Materialien, ohne dass der Build fehlschlaegt.
            UrpSetup.EnsureActive();

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
            // Zielpunkt ungefaehr in der Mitte der Stationsreihe -- Station 0
            // liegt per RowOffset am linken Rand, nicht in der Mitte.
            var lookTarget = RestaurantLayout.StationPosition(0) + new Vector3(0f, 0.4f, 0f);
            cameraObject.transform.position = lookTarget + cameraObject.transform.rotation * new Vector3(0, 0, -15f);

            // Nutzer-Feedback ("die Schatten sind zu extrem"): drei
            // Ursachen zusammen.
            //
            // 1. shadowStrength wurde nie gesetzt und stand damit auf 1.0 --
            //    jeder Schatten war voll deckend, also praktisch schwarz.
            // 2. Bei 50 Grad Neigung wirft ein 1 Einheit hohes Moebelstueck
            //    einen fast ebenso langen Schlagschatten quer ueber den
            //    Boden; im Portrait-Ausschnitt waren das dunkle Baender
            //    ueber die halbe Bildflaeche. Steiler = kuerzer.
            // 3. LightShadows.Soft blieb wirkungslos, solange die Pipeline
            //    weiche Schatten gar nicht unterstuetzte (siehe UrpSetup).
            var lightObject = new GameObject("Directional Light", typeof(Light));
            var light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.95f;
            // Leicht warm statt reinweiss -- reines Weiss auf den ohnehin
            // hellen Kenney-Materialien liess Theke und Waende ausbrennen.
            light.color = new Color(1f, 0.96f, 0.9f);
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.45f;
            light.shadowNormalBias = 0.4f;
            lightObject.transform.rotation = Quaternion.Euler(62f, -35f, 0f);

            // War vorher 0.75/0.75/0.8 -- zusammen mit dem Directional Light
            // hat das jede Materialfarbe stark Richtung Weiss verwaschen
            // (sichtbar beim Location-Farbwechsel: sattes Grau kam als
            // blasses Lavendel an). Deutlich gedaempft, damit Basisfarben
            // erkennbar bleiben.
            // Gradient statt Flat: ein einziger Umgebungston beleuchtet
            // jede Flaeche gleich stark, wodurch die Facetten der Low-Poly-
            // Modelle nur ueber das harte Sonnenlicht auseinandergehalten
            // werden -- das laesst alles kantig wirken. Mit Himmel-, Horizont-
            // und Bodenton bekommen nach oben, zur Seite und nach unten
            // gerichtete Flaechen unterschiedliche Grundhelligkeit, und die
            // Kanten trennen sich auch dort, wo kein direktes Licht hinfaellt.
            // Globales Volume mit dem Bildbearbeitungs-Profil (siehe
            // UrpSetup.EnsurePostProcessProfile) und Post-Processing an der
            // Kamera einschalten -- ohne beides bleibt das Profil wirkungslos.
            var volumeObject = new GameObject("Global Volume", typeof(UnityEngine.Rendering.Volume));
            var volume = volumeObject.GetComponent<UnityEngine.Rendering.Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = UrpSetup.EnsurePostProcessProfile();

            var cameraData = cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;
            // Keine zusaetzliche Nachbearbeitungs-Kantenglaettung: MSAA 4x
            // laeuft bereits ueber die Pipeline (UrpSetup), beides
            // uebereinander kostet doppelt und bringt nichts.
            cameraData.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.68f, 0.76f);
            RenderSettings.ambientEquatorColor = new Color(0.56f, 0.55f, 0.53f);
            RenderSettings.ambientGroundColor = new Color(0.4f, 0.36f, 0.33f);

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

        /// <summary>Abstand kleiner Wanddeko (Haengeschrank, Muelleimer) von der Stationsreihe, Richtung Kamera.</summary>
        private const float WallDepthOffset = 1.15f;

        /// <summary>
        /// Abstand der Rueckwand von der Stationsreihe, jenseits des
        /// gesamten Gastraums (nicht auf der Kamera-Seite): eine Wand
        /// zwischen Kamera und Kuechengeraeten -- der erste Versuch --
        /// verdeckt bei diesem Kamerawinkel (55 Grad) die kuerzeren Geraete
        /// dahinter komplett. Als reiner Hintergrund hinter allem statt als
        /// Kulisse direkt hinter dem Personal gibt es dieses Problem nicht.
        /// </summary>
        private const float WallFarDepthOffset = 4.4f;

        private const float WallHeight = 2.4f;

        private static void BuildLocation1Placeholder()
        {
            BuildGround();
            BuildCounter();
            BuildStations();
            BuildBackWall();
            BuildGuestArea();
        }

        private static void BuildGround()
        {
            // Station 3 (die mittlere von 7) liegt per RowOffset exakt im
            // Reihen-Ursprung -- dieselbe Mitte fuer Rasen/Innenboden wie
            // zuvor, nur jetzt tatsaechlich die geometrische Mitte der
            // gesamten Reihe statt nur der ersten Station.
            var center = RestaurantLayout.StationPosition(3);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = new Vector3(center.x, 0f, center.z);
            ground.transform.localScale = new Vector3(2.2f, 1f, 2.2f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                color = new Color(0.55f, 0.75f, 0.45f),
            };

            // Innenboden als eigene Flaeche knapp ueber dem Rasen: ohne ihn
            // stehen Theke und Geraete direkt auf der Wiese -- das liest
            // sich als Picknick, nicht als Lokal. Deckt jetzt Kueche UND
            // Gastraum ab (Eatventure-Layout: beide Zonen sind ein
            // durchgehender Innenraum, nicht mehr Theke + separater
            // Aussenbereich).
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "InteriorFloor";
            // Deckt von der Kuechenseite (kleine negative Tiefe, Deko wie
            // Haengeschrank/Muelleimer) bis kurz vor die Hintergrundwand
            // (WallFarDepthOffset) ab -- sonst zeigt der Streifen zwischen
            // Gastraum-Ende und Wand Rasen statt Innenboden.
            floor.transform.position = new Vector3(center.x, 0.012f, center.z)
                + RestaurantLayout.DepthDirection * (WallFarDepthOffset * 0.5f);
            floor.transform.rotation = RestaurantLayout.RowRotation;
            // Plane ist nativ 10x10 Einheiten: lokale X-Achse laeuft nach
            // dem Drehen entlang der Stationsreihe (RowDirection), lokale
            // Z-Achse entlang der Tiefe (DepthDirection).
            floor.transform.localScale = new Vector3(1.1f, 1f, (WallFarDepthOffset + 1f) / 10f);
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

        /// <summary>
        /// EIN durchgehender Tresen ueber die ganze Stationsreihe (Nutzer-
        /// Feedback, Eatventure-Vorbild) -- vorher stand nur unter Station 0
        /// ein einzelnes Thekenstueck, der Rest der Reihe hatte gar keinen
        /// Tresen. Segmentbreite wird gemessen wie bei der Rueckwand.
        /// </summary>
        private static void BuildCounter()
        {
            var segmentWidth = MeasureScaledWidth("Assets/Models/Furniture/kitchenBar.fbx", RestaurantLayout.CounterHeight);
            if (segmentWidth <= 0.01f)
            {
                Debug.LogWarning("Thekenbreite nicht messbar -- Tresen uebersprungen.");
                return;
            }

            // Ueberstand von einer halben Stationsbreite an jedem Ende --
            // der Tresen soll sichtbar ueber die aeusseren Stationen
            // hinausragen, nicht buendig mit ihrer Mitte abschliessen.
            var length = RestaurantLayout.StationSpacing * RestaurantLayout.StationCount + RestaurantLayout.StationSpacing;
            var count = Mathf.CeilToInt(length / segmentWidth);
            var start = RestaurantLayout.StationPosition(0) - RestaurantLayout.RowDirection * (length * 0.5f);

            for (var i = 0; i < count; i++)
            {
                InstantiateModel("Assets/Models/Furniture/kitchenBar.fbx", $"Tresen_{i}",
                    start + RestaurantLayout.RowDirection * (i * segmentWidth),
                    RestaurantLayout.RowRotation, RestaurantLayout.CounterHeight);
            }

            InstantiateModel("Assets/Models/Furniture/kitchenBarEnd.fbx", "Tresen_Abschluss_Links",
                start - RestaurantLayout.RowDirection * 0.05f, RestaurantLayout.RowRotation, RestaurantLayout.CounterHeight);
            InstantiateModel("Assets/Models/Furniture/kitchenBarEnd.fbx", "Tresen_Abschluss_Rechts",
                start + RestaurantLayout.RowDirection * (count * segmentWidth + 0.05f),
                RestaurantLayout.RowRotation * Quaternion.Euler(0f, 180f, 0f), RestaurantLayout.CounterHeight);

            Debug.Log($"Tresen: {count} Segmente a {segmentWidth:F2} Einheiten.");
        }

        private static void BuildStations()
        {
            var rotation = RestaurantLayout.RowRotation;

            // Tatsaechliche Theken-Oberkante messen statt CounterHeight
            // anzunehmen -- siehe MeasureScaledTopY-Kommentar.
            var counterTopY = MeasureScaledTopY("Assets/Models/Furniture/kitchenBar.fbx", RestaurantLayout.CounterHeight);

            InstantiateModel("Assets/Models/Furniture/kitchenCoffeeMachine.fbx", "Station_Kaffeemaschine",
                RestaurantLayout.StationPosition(0) + new Vector3(0f, counterTopY, 0f),
                rotation, 0.45f, FitAxis.Height, stationIndex: 0);

            // Nicht auf den Warteplatz selbst: dort steht der bediente
            // Gast, Hocker und Sprite lagen im Testlauf uebereinander. Der
            // Hocker steht jetzt auf der Kuechenseite (Personal), nicht mehr
            // auf der Gastseite -- im Eatventure-Layout sitzt/steht das
            // Personal an der Theke, die Gaeste stehen jenseits davon.
            var stoolSpot = RestaurantLayout.StationPosition(0) - RestaurantLayout.DepthDirection * 0.6f;
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
                var position = RestaurantLayout.StationPosition(i + 1) + new Vector3(0f, counterTopY, 0f);
                InstantiateModel($"Assets/Models/Furniture/{model}", $"Station_{stationName}",
                    position, rotation, height, FitAxis.Height, stationIndex: i + 1);

                // Haengeschrank an der Rueckwand hinter jeder zweiten
                // Station -- fuellt die sonst voellig leere Wandflaeche,
                // ohne den Blick auf die Station selbst zu nehmen.
                if (i % 2 == 0)
                {
                    InstantiateModel("Assets/Models/Furniture/kitchenCabinetUpper.fbx", $"Haengeschrank_{i}",
                        RestaurantLayout.StationPosition(i + 1)
                            - RestaurantLayout.DepthDirection * (WallDepthOffset - 0.3f)
                            + new Vector3(0f, 1.5f, 0f),
                        rotation, 0.6f);
                }
            }

            InstantiateModel("Assets/Models/Furniture/hoodModern.fbx", "Dunstabzug",
                RestaurantLayout.StationPosition(2) + new Vector3(0f, 1.55f, 0f), rotation, 0.6f);
        }

        /// <summary>
        /// Hintergrund-Wand jenseits des gesamten Gastraums, parallel zur
        /// Stationsreihe (RowDirection). Nicht direkt hinter der Kueche --
        /// siehe WallFarDepthOffset-Kommentar, eine hohe Wand naeher an der
        /// Kamera als die Geraete wuerde sie verdecken. Segmentbreite wird
        /// am bereits auf Zielhoehe skalierten Modell gemessen -- Raten war
        /// beim vorherigen Wandversuch die Fehlerquelle (die Wand war am
        /// Ende 0.156 Einheiten hoch und damit unsichtbar).
        /// </summary>
        private static void BuildBackWall()
        {
            var segmentWidth = MeasureScaledWidth("Assets/Models/Furniture/wall.fbx", WallHeight);
            if (segmentWidth <= 0.01f)
            {
                Debug.LogWarning("Wandbreite nicht messbar -- Rueckwand uebersprungen.");
                return;
            }

            var length = RestaurantLayout.StationSpacing * RestaurantLayout.StationCount + RestaurantLayout.StationSpacing;
            var count = Mathf.CeilToInt(length / segmentWidth);
            var start = RestaurantLayout.StationPosition(0)
                - RestaurantLayout.RowDirection * (length * 0.5f)
                + RestaurantLayout.DepthDirection * WallFarDepthOffset;

            for (var i = 0; i < count; i++)
            {
                // Jedes zweite Segment mit Fenster: eine geschlossene Wand
                // ueber die volle Laenge wirkt wie eine Mauer, Fenster
                // machen daraus eine Kueche mit Tageslicht.
                var model = i % 2 == 1 ? "wallWindow" : "wall";
                InstantiateModel($"Assets/Models/Furniture/{model}.fbx", $"Wall_{i}",
                    start + RestaurantLayout.RowDirection * (i * segmentWidth),
                    RestaurantLayout.RowRotation, WallHeight);
            }

            Debug.Log($"Rueckwand: {count} Segmente a {segmentWidth:F2} Einheiten.");
        }

        /// <summary>
        /// Gastraum jenseits des Tresens (positive DepthDirection) -- der
        /// Streifen, durch den die Gaeste laufen und in dem sie anstehen
        /// (RestaurantLayout.Entrance und QueueSlot). Vorher lag dieser
        /// Bereich seitlich der Theke, jetzt dahinter -- Eatventure-Layout.
        /// </summary>
        private static void BuildGuestArea()
        {
            var rotation = RestaurantLayout.RowRotation;
            var entrance = new Vector3(RestaurantLayout.Entrance.x, 0f, RestaurantLayout.Entrance.z);

            InstantiateModel("Assets/Models/Furniture/rugRectangle.fbx", "Eingangsteppich",
                entrance + new Vector3(0f, 0.02f, 0f), rotation, 1.5f, FitAxis.Width);

            // Sitzgruppen im Gastraum, weiter von der Theke entfernt als der
            // direkte Warteplatz -- ausserhalb der Laufwege der Schlange.
            for (var i = 0; i < 3; i++)
            {
                var anchor = RestaurantLayout.StationPosition(i * 2 - 1)
                    + RestaurantLayout.DepthDirection * 2.2f;

                InstantiateModel("Assets/Models/Furniture/tableCloth.fbx", $"Gasttisch_{i}",
                    anchor, rotation, 0.75f);
                InstantiateModel("Assets/Models/Furniture/chair.fbx", $"Gaststuhl_{i}a",
                    anchor - RestaurantLayout.RowDirection * 0.6f, rotation, 0.85f);
                InstantiateModel("Assets/Models/Furniture/chairCushion.fbx", $"Gaststuhl_{i}b",
                    anchor + RestaurantLayout.RowDirection * 0.6f,
                    rotation * Quaternion.Euler(0f, 180f, 0f), 0.85f);
            }

            InstantiateModel("Assets/Models/Furniture/pottedPlant.fbx", "Pflanze_Eingang",
                entrance + RestaurantLayout.DepthDirection * 1.3f, rotation, 0.9f);
            InstantiateModel("Assets/Models/Furniture/pottedPlant.fbx", "Pflanze_Ende",
                RestaurantLayout.StationPosition(0) - RestaurantLayout.RowDirection * 1.5f
                    + RestaurantLayout.DepthDirection * 1.6f,
                rotation, 0.9f);
            InstantiateModel("Assets/Models/Furniture/plantSmall2.fbx", "Pflanze_Theke",
                RestaurantLayout.StationPosition(0)
                    - RestaurantLayout.RowDirection * 0.8f
                    + new Vector3(0f, RestaurantLayout.CounterHeight, 0f),
                rotation, 0.25f);
            InstantiateModel("Assets/Models/Furniture/lampSquareFloor.fbx", "Stehlampe",
                RestaurantLayout.StationPosition(4) + RestaurantLayout.DepthDirection * 2.6f, rotation, 1.5f);
            InstantiateModel("Assets/Models/Furniture/trashcan.fbx", "Muelleimer",
                RestaurantLayout.StationPosition(0) - RestaurantLayout.DepthDirection * (WallDepthOffset - 0.3f)
                    - RestaurantLayout.RowDirection * 1.2f,
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

        /// <summary>
        /// Hoehe der Modell-OBERKANTE ueber dem Pivot, wenn das Modell auf
        /// targetHeight skaliert ist -- nicht dasselbe wie targetHeight
        /// selbst. Nutzer-Feedback ("Geraete stehen komisch"): die Theke
        /// wurde bei Y=0 platziert und Geraete pauschal bei Y=CounterHeight
        /// obendrauf gesetzt, in der Annahme, der Thekenpivot liege an der
        /// Basis und ihre Oberkante folglich exakt bei CounterHeight. Die
        /// Kenney-Module haben ihren Pivot aber an einer ECKE, nicht
        /// zwingend an der niedrigsten Stelle (siehe Klassen-Remarks) --
        /// Geraete schwebten sichtbar ueber der tatsaechlichen Thekenkante.
        /// </summary>
        private static float MeasureScaledTopY(string assetPath, float targetHeight)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
            {
                return targetHeight;
            }

            var probe = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            probe.transform.position = Vector3.zero;
            probe.transform.rotation = Quaternion.identity;
            probe.transform.localScale = Vector3.one;

            var bounds = MeasureBounds(probe);
            Object.DestroyImmediate(probe);

            if (bounds.size.y <= 0.0001f)
            {
                return targetHeight;
            }

            var scale = targetHeight / bounds.size.y;
            return bounds.max.y * scale;
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
