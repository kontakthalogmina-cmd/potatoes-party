using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace PotatoParty.EditorTools
{
    /// <summary>
    /// Przy pierwszym otwarciu projektu: tworzy scenę startową, dodaje ją do Build Settings,
    /// tworzy i włącza URP oraz materiały bazowe. Można też uruchomić ręcznie z menu
    /// "Potatoes Party / Napraw ustawienia projektu".
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        const string ScenePath = "Assets/Scenes/PotatoesParty.unity";
        const string SettingsDir = "Assets/Settings";
        const string ResourcesDir = "Assets/Resources/PotatoParty";
        const string DoneKey = "PotatoParty.SetupDone.v4";

        static ProjectSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (SessionState.GetBool(DoneKey, false)) return;
                SessionState.SetBool(DoneKey, true);
                Run(false);
            };
        }

        [MenuItem("Potatoes Party/Napraw ustawienia projektu")]
        static void RunFromMenu() { Run(true); }

        [MenuItem("Potatoes Party/Otwórz scenę gry")]
        static void OpenScene()
        {
            if (File.Exists(ScenePath) && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        static void Run(bool verbose)
        {
            try
            {
                EnsureFolder("Assets/Scenes");
                EnsureFolder(SettingsDir);
                EnsureFolder("Assets/Resources");
                EnsureFolder(ResourcesDir);

                EnsureUrp();
                EnsureMaterials();
                EnsureScene();

                PlayerSettings.productName = "Potatoes Party";
                PlayerSettings.companyName = "Gminiak";
                AssetDatabase.SaveAssets();
                if (verbose) Debug.Log("[Potatoes Party] Ustawienia projektu są gotowe. Wciśnij Play!");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Potatoes Party] Nie udało się dokończyć automatycznej konfiguracji: " + e.Message +
                                 "\nGra i tak zadziała po wciśnięciu Play.");
            }
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static void EnsureUrp()
        {
            if (GraphicsSettings.defaultRenderPipeline != null)
            {
                TuneUrp(GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset);
                return;
            }
            const string assetPath = SettingsDir + "/PotatoURP.asset";
            const string rendererPath = SettingsDir + "/PotatoURP_Renderer.asset";

            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
            if (asset == null)
            {
                var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, rendererPath);
                asset = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(asset, assetPath);
                asset.shadowDistance = 60f;
                asset.msaaSampleCount = 4;
                EditorUtility.SetDirty(asset);
            }
            TuneUrp(asset);
            GraphicsSettings.defaultRenderPipeline = asset;
            Debug.Log("[Potatoes Party] Włączono Universal Render Pipeline (" + assetPath + ").");
        }

        /// <summary>Gładki obraz: MSAA 4x, HDR, miękkie cienie w wysokiej rozdzielczości.</summary>
        static void TuneUrp(UniversalRenderPipelineAsset asset)
        {
            if (asset == null) return;
            var so = new SerializedObject(asset);
            SetInt(so, "m_MSAA", 4);
            SetFloat(so, "m_RenderScale", 1f);
            SetBool(so, "m_SupportsHDR", true);
            SetFloat(so, "m_ShadowDistance", 70f);
            SetInt(so, "m_ShadowCascadeCount", 2);
            SetInt(so, "m_MainLightShadowmapResolution", 4096);
            SetBool(so, "m_MainLightShadowsSupported", true);
            SetBool(so, "m_SoftShadowsSupported", true);
            // Dużo kolorowych świateł punktowych (pochodnie, kryształy): liczone na piksel, do 8 na obiekt
            SetInt(so, "m_AdditionalLightsRenderingMode", 1);
            SetInt(so, "m_AdditionalLightsPerObjectLimit", 8);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            EnsurePostProcessData(asset);
        }

        /// <summary>
        /// Bez PostProcessData renderer URP pomija bloom, tonemapping i wygładzanie SMAA.
        /// Renderer utworzony z kodu nie ma go przypisanego, więc dopinamy domyślny z pakietu URP.
        /// </summary>
        static void EnsurePostProcessData(UniversalRenderPipelineAsset asset)
        {
            var so = new SerializedObject(asset);
            var list = so.FindProperty("m_RendererDataList");
            if (list == null) return;
            PostProcessData data = AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
            if (data == null)
            {
                foreach (var guid in AssetDatabase.FindAssets("t:PostProcessData"))
                {
                    data = AssetDatabase.LoadAssetAtPath<PostProcessData>(AssetDatabase.GUIDToAssetPath(guid));
                    if (data != null) break;
                }
            }
            if (data == null) { Debug.LogWarning("[Potatoes Party] Nie znaleziono PostProcessData w pakiecie URP."); return; }
            for (int i = 0; i < list.arraySize; i++)
            {
                var renderer = list.GetArrayElementAtIndex(i).objectReferenceValue as UniversalRendererData;
                if (renderer == null) continue;
                // Forward+ : brak limitu świateł na obiekt, więc każda pochodnia oświetla arenę
                var rso = new SerializedObject(renderer);
                var mode = rso.FindProperty("m_RenderingMode");
                if (mode != null && mode.intValue != 2)
                {
                    mode.intValue = 2;
                    rso.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(renderer);
                    Debug.Log("[Potatoes Party] Włączono renderowanie Forward+ (dużo świateł na scenie).");
                }
                if (renderer.postProcessData != null) continue;
                renderer.postProcessData = data;
                EditorUtility.SetDirty(renderer);
                Debug.Log("[Potatoes Party] Włączono efekty obrazu (bloom, kolory, wygładzanie krawędzi).");
            }
        }

        static void SetInt(SerializedObject so, string name, int v) { var p = so.FindProperty(name); if (p != null) p.intValue = v; }
        static void SetFloat(SerializedObject so, string name, float v) { var p = so.FindProperty(name); if (p != null) p.floatValue = v; }
        static void SetBool(SerializedObject so, string name, bool v) { var p = so.FindProperty(name); if (p != null) p.boolValue = v; }

        static void EnsureMaterials()
        {
            MakeMaterial(ResourcesDir + "/BaseLit.mat", "Universal Render Pipeline/Lit");
            MakeMaterial(ResourcesDir + "/BaseStandard.mat", "Standard");
        }

        static void MakeMaterial(string path, string shaderName)
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            var shader = Shader.Find(shaderName);
            if (shader == null) return;
            var m = new Material(shader);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.25f);
            AssetDatabase.CreateAsset(m, path);
        }

        static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                var active = SceneManager.GetActiveScene();
                bool canReplace = string.IsNullOrEmpty(active.path) && !active.isDirty;
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, canReplace ? NewSceneMode.Single : NewSceneMode.Additive);
                var info = new GameObject("Wcisnij Play - gra buduje sie sama");
                info.transform.position = Vector3.zero;
                EditorSceneManager.SaveScene(scene, ScenePath);
                if (!canReplace) EditorSceneManager.CloseScene(scene, true);
                Debug.Log("[Potatoes Party] Utworzono scenę " + ScenePath);
            }

            var scenes = EditorBuildSettings.scenes;
            bool listed = false;
            foreach (var s in scenes) if (s.path == ScenePath) listed = true;
            if (!listed)
            {
                var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(scenes);
                list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = list.ToArray();
            }

            var current = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(current.path) && !current.isDirty)
                EditorSceneManager.OpenScene(ScenePath);
        }
    }
}
