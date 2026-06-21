using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SCZip.Domain;
using SCZip.Services;
using UnityEngine;

namespace SCZip.Infrastructure
{
    public sealed class LocalFileSystemService : IFileSystemService
    {
        public string MyFilesRoot { get; private set; }
        public string StorageRoot { get; private set; }
        public string PhotosRoot { get; private set; }
        public string MusicRoot { get; private set; }

        public LocalFileSystemService()
        {
            MyFilesRoot = Path.Combine(Application.persistentDataPath, "SCZip", "MyFiles");
            StorageRoot = Path.Combine(Application.persistentDataPath, "SCZip", "Storage");
            PhotosRoot = Path.Combine(Application.persistentDataPath, "SCZip", "Photos");
            MusicRoot = Path.Combine(Application.persistentDataPath, "SCZip", "Music");
        }

        public void EnsureAppDirectories()
        {
            foreach (var dir in new[] { MyFilesRoot, StorageRoot, PhotosRoot, MusicRoot })
            {
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
            }

            SeedDemoContentIfEmpty();
        }

        private void SeedDemoContentIfEmpty()
        {
            var readme = Path.Combine(StorageRoot, "Welcome.txt");
            if (!File.Exists(readme))
            {
                File.WriteAllText(readme,
                    "Welcome to SCZip!\n\nUse the menu to browse folders, select files, then Compress or Unzip.\n");
            }

            var docs = Path.Combine(StorageRoot, "Documents");
            if (!Directory.Exists(docs))
                Directory.CreateDirectory(docs);
        }

        public Task<IReadOnlyList<FileEntry>> ListDirectoryAsync(string path, NavigationSource source)
        {
            return Task.FromResult(ListDirectory(path, source));
        }

        public IReadOnlyList<FileEntry> ListDirectory(string path, NavigationSource source)
        {
            if (!Directory.Exists(path))
                return Array.Empty<FileEntry>();

            var entries = new List<FileEntry>();

            foreach (var dir in Directory.GetDirectories(path))
            {
                var info = new DirectoryInfo(dir);
                entries.Add(ToFileEntry(info.FullName, info.Name, true, source, info.LastWriteTimeUtc));
            }

            foreach (var file in Directory.GetFiles(path))
            {
                var info = new FileInfo(file);
                var fmt = ArchiveFormatRegistry.DetectByPath(file);
                entries.Add(ToFileEntry(info.FullName, info.Name, false, source, info.LastWriteTimeUtc, fmt));
            }

            return entries
                .OrderByDescending(e => e.IsDirectory)
                .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static FileEntry ToFileEntry(string fullPath, string name, bool isDir, NavigationSource source,
            DateTime modified, ArchiveFormat fmt = ArchiveFormat.Unknown)
        {
            long size = 0;
            if (!isDir)
            {
                try { size = new FileInfo(fullPath).Length; }
                catch { /* ignore */ }
            }

            var isArchive = !isDir && fmt != ArchiveFormat.Unknown;
            return new FileEntry
            {
                Id = fullPath,
                Name = name,
                FullPath = fullPath,
                IsDirectory = isDir,
                IsArchive = isArchive,
                ArchiveFormat = isArchive ? fmt : ArchiveFormat.Unknown,
                SizeBytes = size,
                ModifiedUtc = modified,
                Source = source
            };
        }

        public Task CopyAsync(string src, string dest) =>
            Task.Run(() => File.Copy(src, dest, true));

        public Task MoveAsync(string src, string dest) =>
            Task.Run(() => File.Move(src, dest));

        public Task DeleteAsync(string path) => Task.Run(() =>
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
            else if (File.Exists(path)) File.Delete(path);
        });

        public Task CreateDirectoryAsync(string path) =>
            Task.Run(() => Directory.CreateDirectory(path));

        public Task RenameAsync(string src, string dest) =>
            Task.Run(() =>
            {
                if (Directory.Exists(src)) Directory.Move(src, dest);
                else File.Move(src, dest);
            });

        public bool Exists(string path) => Directory.Exists(path) || File.Exists(path);

        public string GetParent(string path)
        {
            var parent = Directory.GetParent(path);
            return parent?.FullName;
        }

        public string Combine(string a, string b) => Path.Combine(a, b);
    }
}
