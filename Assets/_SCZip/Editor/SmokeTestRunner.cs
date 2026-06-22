#if UNITY_EDITOR
using System.IO;
using SCZip.Core;
using SCZip.Infrastructure.Testing;
using SCZip.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SCZip.Editor
{
    public static class SmokeTestRunner
    {
        public static void Run() => RunInternal(exitOnComplete: true);

        /// <summary>Editor menu — run archive roundtrip smoke without quitting Unity.</summary>
        public static void RunFromMenu()
        {
            try
            {
                RunInternal(exitOnComplete: false);
                EditorUtility.DisplayDialog("SCZip", "打包/解包冒烟测试通过。\n详见 Console 中的 SMOKE_PASS。", "确定");
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[SCZip] SMOKE_FAIL: " + ex);
                EditorUtility.DisplayDialog("SCZip", "冒烟测试失败：\n" + ex.Message, "确定");
            }
        }

        private static void RunInternal(bool exitOnComplete)
        {
            try
            {
                AppServices.Initialize();
                RunFileSystemSmoke();
                ArchiveRoundtripHarness.RunAll(AppServices.FileSystem.MyFilesRoot);
                SceneBootstrapper.SetupMainScene();
                ValidateMainScene();
                Debug.Log("[SCZip] SMOKE_PASS all archive roundtrip checks OK");
                if (exitOnComplete)
                    EditorApplication.Exit(0);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[SCZip] SMOKE_FAIL: " + ex);
                if (exitOnComplete)
                    EditorApplication.Exit(1);
                throw;
            }
        }

        private static void RunFileSystemSmoke()
        {
            var fs = AppServices.FileSystem;
            var testDir = Path.Combine(fs.MyFilesRoot, "_smoke");
            if (Directory.Exists(testDir))
                Directory.Delete(testDir, true);
            Directory.CreateDirectory(testDir);

            var f1 = Path.Combine(testDir, "a.txt");
            File.WriteAllText(f1, "hello");
            var entries = fs.ListDirectoryAsync(testDir, Domain.NavigationSource.MyFiles).GetAwaiter().GetResult();
            if (!System.Linq.Enumerable.Any(entries, e => e.Name == "a.txt"))
                throw new System.Exception("ListDirectory missing a.txt");
        }

        private static void ValidateMainScene()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_SCZip/Scenes/Main.unity");
            if (!scene.IsValid())
                throw new System.Exception("Main scene invalid");

            var app = GameObject.Find("SCZipApp");
            if (app == null)
                throw new System.Exception("SCZipApp GameObject not found in Main scene");

            var canvas = GameObject.Find("UICanvas");
            if (canvas == null)
                throw new System.Exception("UICanvas not found in Main scene");

            var view = canvas.GetComponent<AppShellView>();
            if (view == null)
                throw new System.Exception("AppShellView missing on UICanvas");

            if (canvas.GetComponent<Canvas>() == null)
                throw new System.Exception("Canvas component missing on UICanvas");

            if (view.fileListContent == null)
                throw new System.Exception("fileListContent is not assigned");

            if (view.dialogInput == null)
                throw new System.Exception("dialogInput is not assigned");

            DialogInputBinding.Resolve(view);

            if (view.dialogInput.gameObject.name != "Input")
                throw new System.Exception("dialogInput should be DialogInput/Input");
        }
    }
}
#endif
