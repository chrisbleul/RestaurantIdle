using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RestaurantIdle.Editor
{
    /// <summary>
    /// PLANv2.md Abschnitt 4/8: URP war zwar als Paket installiert, aber nie
    /// als aktive Render-Pipeline zugewiesen (GraphicsSettings zeigte auf
    /// die eingebaute Pipeline) -- das Spiel lief bisher rein per UGUI
    /// ScreenSpaceOverlay, wo das nie aufgefallen ist. Fuer die isometrische
    /// 3D-Szene (Phase 8) muss URP aktiv sein. Legt Asset + Universal
    /// Renderer einmalig an und weist sie zu, falls noch nichts zugewiesen
    /// ist -- idempotent, gleiches Muster wie CIBuild/IconImportSetup.
    /// </summary>
    public static class UrpSetup
    {
        private const string SettingsFolder = "Assets/Settings";
        private const string RendererPath = SettingsFolder + "/UniversalRendererData.asset";
        private const string PipelineAssetPath = SettingsFolder + "/UniversalRenderPipelineAsset.asset";
        private const string PostProcessProfilePath = SettingsFolder + "/PostProcessProfile.asset";

        [InitializeOnLoadMethod]
        private static void EnsureUrpActive()
        {
            EditorApplication.delayCall += ApplySettings;
        }

        /// <summary>
        /// Oeffentlicher, synchroner Einstiegspunkt fuer CIBuild.BuildWebGl.
        /// Der delayCall oben deckt nur die interaktive Editor-Nutzung ab --
        /// im game-ci/unity-builder-Batch-Build (-executeMethod) lief er nie:
        /// der WebGL-Build lief durch ohne jede "URP aktiviert"/"URP-
        /// Bildqualitaet gesetzt"-Logzeile, GraphicsSettings.defaultRenderPipeline
        /// blieb null, URP-Shadervarianten wurden beim Stripping fuer WebGL
        /// entfernt, und jedes Material mit "Universal Render Pipeline/Lit"
        /// kam pink aus dem Build -- ohne dass der Build selbst fehlschlug
        /// oder irgendwo eine Fehlermeldung stand. CIBuild ruft das hier
        /// deshalb direkt auf, bevor BuildPipeline.BuildPlayer laeuft.
        /// </summary>
        public static void EnsureActive() => ApplySettings();

        // Nutzer-Feedback ("alles sehr kantig und die Schatten sind zu
        // extrem"): beide Beschwerden hatten dieselbe Wurzel -- das
        // Pipeline-Asset wurde einmalig mit Unitys Standardwerten angelegt
        // und danach nie wieder angefasst. Standard heisst hier: MSAA aus
        // (jede Silhouette hart getreppt) und m_SoftShadowsSupported = 0,
        // also HARTE Schatten, egal was am Licht selbst eingestellt ist --
        // CIBuild setzte LightShadows.Soft, das lief ins Leere.
        //
        // Die Werte stehen deshalb ab jetzt hier im Code statt nur im
        // generierten Asset: das Asset liegt nicht unter Versionskontrolle
        // (es entsteht auf jedem Rechner neu), eine Einstellung darin waere
        // also auf der naechsten Maschine wieder weg.
        private const int MsaaSamples = 4;

        /// <summary>
        /// 50 Einheiten Schattenreichweite auf einer 2048er Shadowmap bei
        /// einer Kaskade -- das Lokal misst keine 12 Einheiten, der Rest der
        /// Aufloesung ging an leeren Rasen. Daher die grobstufigen,
        /// ausgefransten Schattenkanten. 22 vervielfacht die Texeldichte im
        /// tatsaechlich sichtbaren Bereich.
        /// </summary>
        private const float ShadowDistance = 22f;

        private static void ApplySettings()
        {
            var existing = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (existing != null)
            {
                ApplyQualitySettings(existing);
                return;
            }

            if (!AssetDatabase.IsValidFolder(SettingsFolder))
            {
                Directory.CreateDirectory(SettingsFolder);
                AssetDatabase.Refresh();
            }

            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(rendererData, RendererPath);

            var pipelineAsset = UniversalRenderPipelineAsset.Create(rendererData);
            AssetDatabase.CreateAsset(pipelineAsset, PipelineAssetPath);

            GraphicsSettings.defaultRenderPipeline = pipelineAsset;
            QualitySettings.renderPipeline = pipelineAsset;

            AssetDatabase.SaveAssets();
            Debug.Log("URP aktiviert: " + PipelineAssetPath);

            ApplyQualitySettings(pipelineAsset);
        }

        /// <summary>
        /// Setzt die Bildqualitaets-Werte am Pipeline-Asset. Ueber
        /// SerializedObject statt ueber die C#-Eigenschaften, weil ein Teil
        /// davon (u.a. supportsSoftShadows) in URP nur einen Getter hat --
        /// die Felder selbst sind serialisiert und damit im Editor sicher
        /// erreichbar. Idempotent: schreibt nur, was noch nicht stimmt.
        /// </summary>
        private static void ApplyQualitySettings(UniversalRenderPipelineAsset asset)
        {
            var serialized = new SerializedObject(asset);
            var changed = false;

            changed |= SetInt(serialized, "m_MSAA", MsaaSamples);
            changed |= SetBool(serialized, "m_SoftShadowsSupported", true);
            changed |= SetFloat(serialized, "m_ShadowDistance", ShadowDistance);

            if (!changed)
            {
                return;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Debug.Log($"URP-Bildqualitaet gesetzt: MSAA {MsaaSamples}x, weiche Schatten, Schattenreichweite {ShadowDistance}.");
        }

        /// <summary>
        /// Bildbearbeitungs-Profil fuer die Szene. Ohne Post-Processing geht
        /// das gerenderte Bild voellig unbearbeitet auf den Schirm --
        /// lineare Helligkeiten, keine Tonwertkurve, keine Randabdunklung.
        /// Genau das laesst eine Low-Poly-Szene "roh" und hart wirken,
        /// unabhaengig von Kantenglaettung und Schatten.
        ///
        /// Bewusst zurueckhaltend dosiert: das Spiel laeuft auf dem Handy,
        /// Bloom ist der teuerste Posten davon und soll die Szene nur
        /// anwaermen, nicht leuchten lassen.
        ///
        /// Legt das Profil an, falls es fehlt, und liefert es zurueck --
        /// CIBuild haengt es an ein globales Volume in der Szene.
        /// </summary>
        public static VolumeProfile EnsurePostProcessProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostProcessProfilePath);
            if (profile == null)
            {
                if (!AssetDatabase.IsValidFolder(SettingsFolder))
                {
                    Directory.CreateDirectory(SettingsFolder);
                    AssetDatabase.Refresh();
                }

                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, PostProcessProfilePath);
            }

            // Werte bei JEDEM Aufruf setzen, nicht nur beim Anlegen: beim
            // ersten Versuch wurde das Profil nur erzeugt, falls es fehlte --
            // eine spaetere Korrektur der Zahlen hier waere auf jedem
            // Rechner, der das Asset schon hatte, wirkungslos geblieben.
            // Genau das ist beim Nachjustieren der Helligkeit passiert.

            // Neutral statt ACES: ACES zieht die Farben kraeftig ins
            // Filmische und wuerde die flachen CC0-Materialfarben
            // verfaelschen; Neutral bringt nur die Tonwertkurve mit.
            Get<Tonemapping>(profile).mode.Override(TonemappingMode.Neutral);

            var colorAdjustments = Get<ColorAdjustments>(profile);
            // Negativ: die Tonwertkurve hebt die ohnehin hellen Sand- und
            // Weisstoene der Szene sichtbar an -- ohne Gegensteuern wirkte
            // der Innenboden im Testbild ausgewaschen statt warm.
            colorAdjustments.postExposure.Override(-0.35f);
            colorAdjustments.contrast.Override(18f);
            colorAdjustments.saturation.Override(4f);

            var bloom = Get<Bloom>(profile);
            // Schwelle deutlich ueber Weiss: bei 1.05 fing die grosse helle
            // Bodenflaeche selbst an zu glimmen, was das Bild zusaetzlich
            // flach gemacht hat. Jetzt greift Bloom nur noch auf echten
            // Lichtern (Fenster, Muenz-Partikel).
            bloom.threshold.Override(1.3f);
            bloom.intensity.Override(0.22f);
            bloom.scatter.Override(0.55f);

            // Randabdunklung zieht den Blick in die Bildmitte -- dorthin,
            // wo Theke und Warteschlange stehen.
            var vignette = Get<Vignette>(profile);
            vignette.intensity.Override(0.34f);
            vignette.smoothness.Override(0.45f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        /// <summary>Holt eine Volume-Komponente aus dem Profil oder legt sie an -- macht EnsurePostProcessProfile wiederholbar.</summary>
        private static T Get<T>(VolumeProfile profile) where T : VolumeComponent
        {
            return profile.TryGet<T>(out var component) ? component : profile.Add<T>(true);
        }

        private static bool SetInt(SerializedObject serialized, string field, int value)
        {
            var property = Find(serialized, field);
            if (property == null || property.intValue == value)
            {
                return false;
            }

            property.intValue = value;
            return true;
        }

        private static bool SetBool(SerializedObject serialized, string field, bool value)
        {
            var property = Find(serialized, field);
            if (property == null || property.boolValue == value)
            {
                return false;
            }

            property.boolValue = value;
            return true;
        }

        private static bool SetFloat(SerializedObject serialized, string field, float value)
        {
            var property = Find(serialized, field);
            if (property == null || Mathf.Approximately(property.floatValue, value))
            {
                return false;
            }

            property.floatValue = value;
            return true;
        }

        /// <summary>Fehlt ein Feld (andere URP-Version), wird es uebersprungen statt den Editor-Start zu gefaehrden.</summary>
        private static SerializedProperty Find(SerializedObject serialized, string field)
        {
            var property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogWarning($"URP-Feld '{field}' nicht gefunden -- Einstellung uebersprungen.");
            }

            return property;
        }
    }
}
