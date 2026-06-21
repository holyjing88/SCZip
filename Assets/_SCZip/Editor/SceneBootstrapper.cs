#if UNITY_EDITOR
using SCZip.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SCZip.Editor
{
    public static class SceneBootstrapper
    {
        private const string ScenePath = "Assets/_SCZip/Scenes/Main.unity";
        private const string BootstrapScript = "Assets/_SCZip/Scripts/Core/AppBootstrap.cs";
        private const string ShellScript = "Assets/_SCZip/Scripts/UI/AppShellController.cs";

        public static void SetupMainScene()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[SCZip] SetupMainScene cannot run during Play mode. Stop Play first.");
                return;
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            RemoveLegacyUiToolkitComponents();

            var app = GameObject.Find("SCZipApp") ?? new GameObject("SCZipApp");
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(app);

            var view = UguiSceneBuilder.BuildUiCanvas();

            AddComponentIfMissing(app, BootstrapScript);
            var shell = AddComponentIfMissing<SCZip.UI.AppShellController>(app, ShellScript);
            var soShell = new SerializedObject(shell);
            soShell.FindProperty("_view").objectReferenceValue = view;
            soShell.ApplyModifiedPropertiesWithoutUndo();

            EnsureCamera();
            EnsureEventSystem();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            var mainScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (mainScene != null)
                EditorSceneManager.playModeStartScene = mainScene;

            Debug.Log("[SCZip] Main scene bootstrapped with uGUI Canvas OK");
        }

        private static void RemoveLegacyUiToolkitComponents()
        {
            foreach (var go in Object.FindObjectsOfType<GameObject>(true))
            {
                if (go == null) continue;
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);

                foreach (var mb in go.GetComponents<MonoBehaviour>())
                {
                    if (mb == null) continue;
                    var typeName = mb.GetType().FullName;
                    if (typeName == "UnityEngine.UIElements.UIDocument" || typeName == "SCZip.UI.AppUiBootstrap")
                        Object.DestroyImmediate(mb, true);
                }
            }
        }

        private static T AddComponentIfMissing<T>(GameObject go, string scriptPath) where T : Component
        {
            var existing = go.GetComponent<T>();
            if (existing != null) return existing;

            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
            if (script == null || script.GetClass() == null)
                throw new System.Exception("Cannot load script: " + scriptPath);

            return (T)go.AddComponent(script.GetClass());
        }

        private static void AddComponentIfMissing(GameObject go, string scriptPath)
        {
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
            if (script == null || script.GetClass() == null)
                throw new System.Exception("Cannot load script: " + scriptPath);
            if (go.GetComponent(script.GetClass()) == null)
                go.AddComponent(script.GetClass());
        }

        private static void EnsureCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }

            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.96f, 0.96f, 0.96f);
            cam.orthographic = true;
            cam.depth = -1;
        }

        private static void EnsureEventSystem()
        {
            UiEventSystemSetup.Ensure();
        }
    }
}
#endif
