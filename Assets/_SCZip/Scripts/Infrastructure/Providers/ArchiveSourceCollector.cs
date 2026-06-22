using System.Collections.Generic;
using System.IO;

namespace SCZip.Infrastructure.Providers
{
    internal readonly struct ArchiveSourceFile
    {
        public ArchiveSourceFile(string key, string fullPath)
        {
            Key = key;
            FullPath = fullPath;
        }

        public string Key { get; }
        public string FullPath { get; }
    }

    internal static class ArchiveSourceCollector
    {
        public static List<ArchiveSourceFile> CollectFiles(IReadOnlyList<string> sourcePaths)
        {
            var files = new List<ArchiveSourceFile>();
            foreach (var src in sourcePaths ?? System.Array.Empty<string>())
            {
                if (File.Exists(src))
                    files.Add(new ArchiveSourceFile(Path.GetFileName(src), src));
                else if (Directory.Exists(src))
                {
                    foreach (var f in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
                    {
                        var rel = f.Substring(src.Length)
                            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                            .Replace('\\', '/');
                        files.Add(new ArchiveSourceFile(rel, f));
                    }
                }
            }

            return files;
        }
    }
}
