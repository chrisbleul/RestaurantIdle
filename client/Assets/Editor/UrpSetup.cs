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

        [InitializeOnLoadMethod]
        private static void EnsureUrpActive()
        {
            EditorApplication.delayCall += ApplySettings;
        }

        private static void ApplySettings()
        {
            if (GraphicsSettings.defaultRenderPipeline != null)
            {
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
        }
    }
}
