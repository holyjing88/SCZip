#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace SCZip.Editor
{
    public static class SceneBootstrapperMenu
    {
        [MenuItem("SCZip/Setup Main Scene", true)]
        public static bool SetupFromMenuValidate() => !EditorApplication.isPlaying;

        [MenuItem("SCZip/Setup Main Scene")]
        public static void SetupFromMenu()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog(
                    "SCZip",
                    "请先停止 Play 模式，再执行 Setup Main Scene。",
                    "确定");
                return;
            }

            SceneBootstrapper.SetupMainScene();
        }

        [MenuItem("SCZip/Run Archive Smoke Tests", true)]
        public static bool RunArchiveSmokeValidate() => !EditorApplication.isPlaying;

        [MenuItem("SCZip/Run Archive Smoke Tests")]
        public static void RunArchiveSmoke()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("SCZip", "请先停止 Play 模式。", "确定");
                return;
            }

            SmokeTestRunner.RunFromMenu();
        }

        [MenuItem("SCZip/Run Play Mode Smoke", true)]
        public static bool RunPlayModeSmokeValidate() => !EditorApplication.isPlaying;

        [MenuItem("SCZip/Run Play Mode Smoke")]
        public static void RunPlayModeSmoke()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("SCZip", "请先停止 Play 模式。", "确定");
                return;
            }

            PlayModeSmokeRunner.Run();
        }

        [MenuItem("SCZip/Run Play Mode Tests", true)]
        public static bool RunPlayModeTestsValidate() => !EditorApplication.isPlaying;

        [MenuItem("SCZip/Run Play Mode Tests")]
        public static void RunPlayModeTestsFromMenu()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("SCZip", "请先停止 Play 模式。", "确定");
                return;
            }

            UnityEditor.TestTools.TestRunner.Api.TestRunnerApi api =
                ScriptableObject.CreateInstance<UnityEditor.TestTools.TestRunner.Api.TestRunnerApi>();
            api.Execute(new UnityEditor.TestTools.TestRunner.Api.ExecutionSettings(
                new UnityEditor.TestTools.TestRunner.Api.Filter
                {
                    testMode = UnityEditor.TestTools.TestRunner.Api.TestMode.PlayMode,
                    assemblyNames = new[] { "SCZip.Tests" }
                }));
        }
    }
}
#endif
