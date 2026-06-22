#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SCZip.Editor
{
    public static class BuildPlayer
    {
        private static readonly string[] Scenes = { "Assets/_SCZip/Scenes/Main.unity" };

        public static void BuildAndroid()
        {
            var output = GetArg("-customBuildPath") ?? Path.Combine("Builds", "Android", "SCZip.apk");
            var dir = Path.GetDirectoryName(output);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            Run(new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = output,
                target = BuildTarget.Android,
                options = BuildOptions.None
            });
        }

        public static void BuildIOS()
        {
            var output = GetArg("-customBuildPath") ?? Path.Combine("Builds", "iOS");
            Directory.CreateDirectory(output);

            Run(new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = output,
                target = BuildTarget.iOS,
                options = BuildOptions.None
            });
        }

        private static void Run(BuildPlayerOptions options)
        {
            var report = UnityEditor.BuildPipeline.BuildPlayer(options);
            Debug.Log($"[SCZip] Build {options.target}: {report.summary.result} " +
                      $"errors={report.summary.totalErrors} warnings={report.summary.totalWarnings} " +
                      $"output={options.locationPathName}");

            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception($"Build failed: {report.summary.result}");

            EditorApplication.Exit(0);
        }

        private static string GetArg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                    return args[i + 1];
            }

            return null;
        }
    }
}
#endif
