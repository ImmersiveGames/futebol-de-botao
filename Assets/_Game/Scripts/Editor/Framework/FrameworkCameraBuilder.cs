using System.IO;
using Immersive.Framework.Authoring;
using Immersive.Framework.Camera;
using Immersive.Framework.CameraAuthoring;
using Immersive.Framework.Editor.CameraAuthoring;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;

namespace FutebolDeBotao.Editor
{
    /// <summary>
    /// Monta a câmera da mesa no formato do Immersive Framework (Camera Output prefab)
    /// e liga o prefab na Camera Session da Game Application.
    /// </summary>
    public static class FrameworkCameraBuilder
    {
        private const string Folder = "Assets/_Game/App/Camera";
        private const string OutputPath = Folder + "/Mesa.asset";
        private const string FixedPath = Folder + "/Mesa Fixa.asset";
        private const string PrefabPath = Folder + "/CameraOutputMesa.prefab";

        // Mesmo enquadramento da câmera da cena de teste (PrototypeSceneBuilder).
        private const float OrthoSize = 6.9f;
        private static readonly Vector3 CameraPosition = new Vector3(0f, 0f, -10f);
        private static readonly Color Background = new Color(0.08f, 0.08f, 0.1f);

        [MenuItem("Futebol de Botão/Criar câmera do framework")]
        public static void Build()
        {
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();

            var output = LoadOrCreate<CameraOutputDefinition>(OutputPath);
            CameraDefinitionIdentityEditorUtility.GenerateMissingId(output);
            var fixedBehavior = LoadOrCreate<FixedCameraRigBehaviorDefinition>(FixedPath);
            AssetDatabase.SaveAssets();

            var prefab = BuildPrefab(output, fixedBehavior);
            if (prefab == null) return;

            LinkToGameApplication(prefab);
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static GameObject BuildPrefab(CameraOutputDefinition output, FixedCameraRigBehaviorDefinition fixedBehavior)
        {
            var root = new GameObject("CameraOutputMesa") { tag = "MainCamera" };
            try
            {
                root.transform.position = CameraPosition;

                var cam = root.AddComponent<UnityEngine.Camera>();
                cam.orthographic = true;
                cam.orthographicSize = OrthoSize;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Background;
                root.AddComponent<AudioListener>();
                var brain = root.AddComponent<CinemachineBrain>();

                var fallback = new GameObject("Fallback");
                fallback.transform.SetParent(root.transform, false);
                var cmCam = fallback.AddComponent<CinemachineCamera>();
                SetLens(cmCam);
                var composer = fallback.AddComponent<CameraRigComposer>();
                var composerSo = new SerializedObject(composer);
                composerSo.FindProperty("behaviorDefinition").objectReferenceValue = fixedBehavior;
                composerSo.FindProperty("cinemachineCamera").objectReferenceValue = cmCam;
                composerSo.ApplyModifiedPropertiesWithoutUndo();

                var rebuild = CameraRigComposerApplyRebuildUtility.ApplyOrRebuild(composer, true, false);
                if (!rebuild.Succeeded)
                {
                    Debug.LogWarning($"[Futebol de Botão] Apply / Rebuild do Fallback avisou: {rebuild.BlockingIssue}. " +
                                     "Abra o prefab e use o botão Apply / Rebuild no Camera Rig Composer.");
                }

                // O Apply / Rebuild pode trocar a câmera Cinemachine; garante o enquadramento da mesa.
                if (composer.CinemachineCamera != null)
                {
                    composer.CinemachineCamera.transform.position = CameraPosition;
                    SetLens(composer.CinemachineCamera);
                }

                var authoring = root.AddComponent<CameraOutputAuthoring>();
                var authoringSo = new SerializedObject(authoring);
                authoringSo.FindProperty("outputDefinition").objectReferenceValue = output;
                authoringSo.FindProperty("unityCamera").objectReferenceValue = cam;
                authoringSo.FindProperty("cinemachineBrain").objectReferenceValue = brain;
                authoringSo.FindProperty("fallbackCameraRig").objectReferenceValue = composer;
                authoringSo.ApplyModifiedPropertiesWithoutUndo();

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);
                if (!saved)
                {
                    Debug.LogError($"[Futebol de Botão] Não consegui salvar {PrefabPath}.");
                    return null;
                }
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void SetLens(CinemachineCamera cmCam)
        {
            var lens = cmCam.Lens;
            lens.OrthographicSize = OrthoSize;
            cmCam.Lens = lens;
        }

        private static void LinkToGameApplication(GameObject prefab)
        {
            var guids = AssetDatabase.FindAssets("t:GameApplicationAsset");
            if (guids.Length == 0)
            {
                Debug.LogWarning($"[Futebol de Botão] Prefab criado em {PrefabPath}, mas nenhuma Game Application foi encontrada. " +
                                 "Arraste o prefab em Camera Session > Output Prefabs.");
                EditorGUIUtility.PingObject(prefab);
                return;
            }
            if (guids.Length > 1)
                Debug.LogWarning("[Futebol de Botão] Há mais de uma Game Application; usei a primeira encontrada.");

            var app = AssetDatabase.LoadAssetAtPath<GameApplicationAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));
            var so = new SerializedObject(app);
            var list = so.FindProperty("cameraSession.outputPrefabs");
            if (list == null)
            {
                Debug.LogError("[Futebol de Botão] Não achei Camera Session > Output Prefabs na Game Application (versão do framework mudou?).");
                return;
            }

            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == prefab)
                {
                    Debug.Log($"[Futebol de Botão] Prefab da câmera recriado; '{app.name}' já usava ele.");
                    Selection.activeObject = app;
                    return;
                }
            }

            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = prefab;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(app);
            AssetDatabase.SaveAssets();

            Selection.activeObject = app;
            Debug.Log($"[Futebol de Botão] Câmera da mesa criada em {PrefabPath} e ligada em '{app.name}'. Agora clique em Validate.");
        }
    }
}
