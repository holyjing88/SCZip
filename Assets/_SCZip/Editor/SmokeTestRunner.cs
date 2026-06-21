#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Threading;
using SCZip.Core;
using SCZip.Domain;
using SCZip.Services;
using SCZip.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SCZip.Editor
{
    public static class SmokeTestRunner
    {
        public static void Run() => RunInternal(exitOnComplete: true);

        /// <summary>Editor menu — run ZIP/TAR smoke without quitting Unity.</summary>
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
                RunZipSmoke();
                RunTarGzipSmoke();
                SceneBootstrapper.SetupMainScene();
                ValidateMainScene();
                Debug.Log("[SCZip] SMOKE_PASS all checks OK");
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
            var entries = fs.ListDirectoryAsync(testDir, NavigationSource.MyFiles).GetAwaiter().GetResult();
            if (!entries.Any(e => e.Name == "a.txt"))
                throw new System.Exception("ListDirectory missing a.txt");
        }

        private static void RunZipSmoke()
        {
            var fs = AppServices.FileSystem;
            var dir = Path.Combine(fs.MyFilesRoot, "_smoke_zip");
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);

            var src = Path.Combine(dir, "src.txt");
            File.WriteAllText(src, "zip-content");
            var zip = Path.Combine(dir, "test.zip");
            var extractDir = Path.Combine(dir, "out");

            AppServices.Archive.CreateAsync(new CreateArchiveOptions
            {
                Format = ArchiveFormat.Zip,
                OutputPath = zip,
                SourcePaths = new[] { src },
                Level = ArchiveCompressionLevel.Normal
            }, null, CancellationToken.None).GetAwaiter().GetResult();

            if (!File.Exists(zip))
                throw new System.Exception("ZIP not created");

            var list = AppServices.Archive.ListEntriesAsync(zip, new ArchiveOpenOptions(), CancellationToken.None)
                .GetAwaiter().GetResult();
            if (list.Count == 0)
                throw new System.Exception("ZIP list empty");

            Directory.CreateDirectory(extractDir);
            AppServices.Archive.ExtractAsync(new ExtractOptions
            {
                ArchivePath = zip,
                DestinationDirectory = extractDir
            }, null, CancellationToken.None).GetAwaiter().GetResult();

            if (!File.Exists(Path.Combine(extractDir, "src.txt")))
                throw new System.Exception("ZIP extract failed");
        }

        private static void RunTarGzipSmoke()
        {
            var fs = AppServices.FileSystem;
            var dir = Path.Combine(fs.MyFilesRoot, "_smoke_tgz");
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);

            var src = Path.Combine(dir, "data.txt");
            File.WriteAllText(src, "tgz");
            var tgz = Path.Combine(dir, "test.tar.gz");
            var extractDir = Path.Combine(dir, "out");

            AppServices.Archive.CreateAsync(new CreateArchiveOptions
            {
                Format = ArchiveFormat.TarGzip,
                OutputPath = tgz,
                SourcePaths = new[] { src }
            }, null, CancellationToken.None).GetAwaiter().GetResult();

            var list = AppServices.Archive.ListEntriesAsync(tgz, new ArchiveOpenOptions(), CancellationToken.None)
                .GetAwaiter().GetResult();
            if (list.Count == 0)
                throw new System.Exception("TAR.GZ list empty");

            Directory.CreateDirectory(extractDir);
            AppServices.Archive.ExtractAsync(new ExtractOptions
            {
                ArchivePath = tgz,
                DestinationDirectory = extractDir
            }, null, CancellationToken.None).GetAwaiter().GetResult();

            if (!File.Exists(Path.Combine(extractDir, "data.txt")))
                throw new System.Exception("TAR.GZ extract failed");
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

            var view = canvas.GetComponent<SCZip.UI.AppShellView>();
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
