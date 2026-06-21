using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SCZip.Core;
using SCZip.Domain;
using SCZip.Infrastructure.SystemRecent;
using SCZip.Services;
using UnityEngine;

namespace SCZip.Infrastructure
{
    public sealed class RecentService : IRecentService
    {
        private const int MaxItems = 30;
        private readonly string _path;
        private readonly ISystemRecentProvider _systemRecent;
        private List<RecentRecord> _records = new();

        public RecentService(ISystemRecentProvider systemRecent = null)
        {
            _systemRecent = systemRecent ?? SystemRecentProviderFactory.Create();
            _path = Path.Combine(Application.persistentDataPath, "SCZip", "recent.json");
            Load();
        }

        public IReadOnlyList<FileEntry> GetRecent()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            MainThreadGuard.AssertBackgroundThread(nameof(GetRecent));
#endif
            var merged = new List<FileEntry>(MaxItems);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var record in _records
                         .Where(r => File.Exists(r.ArchivePath))
                         .OrderByDescending(r => r.LastOpenedUtc))
            {
                if (!seen.Add(record.ArchivePath))
                    continue;

                merged.Add(ToFileEntry(record.ArchivePath, record.Format, record.LastOpenedUtc));
            }

            var remaining = MaxItems - merged.Count;
            if (remaining <= 0)
                return merged;

            foreach (var item in _systemRecent.GetRecentItems(MaxItems))
            {
                if (!seen.Add(item.Path))
                    continue;

                merged.Add(ToFileEntry(item));
                if (merged.Count >= MaxItems)
                    break;
            }

            return merged;
        }

        public void AddRecent(string archivePath, ArchiveFormat format)
        {
            _records.RemoveAll(r => r.ArchivePath == archivePath);
            _records.Insert(0, new RecentRecord
            {
                ArchivePath = archivePath,
                Format = format,
                LastOpenedTicks = System.DateTime.UtcNow.Ticks
            });
            if (_records.Count > MaxItems)
                _records = _records.Take(MaxItems).ToList();
            Save();
            MacSystemRecentProvider.InvalidateCache();
        }

        public void RemoveRecent(string archivePath)
        {
            _records.RemoveAll(r => r.ArchivePath == archivePath);
            Save();
        }

        private static FileEntry ToFileEntry(SystemRecentItem item)
        {
            var path = item.Path;
            var isDir = Directory.Exists(path);
            var fmt = ArchiveFormatRegistry.DetectByPath(path);
            var isArchive = !isDir && fmt != ArchiveFormat.Unknown;
            long size = 0;

            if (!isDir)
            {
                try { size = new FileInfo(path).Length; }
                catch { /* ignore */ }
            }

            return new FileEntry
            {
                Id = path,
                Name = Path.GetFileName(path),
                FullPath = path,
                IsDirectory = isDir,
                IsArchive = isArchive,
                ArchiveFormat = isArchive ? fmt : ArchiveFormat.Unknown,
                SizeBytes = size,
                ModifiedUtc = item.LastUsedUtc,
                Source = NavigationSource.Recent
            };
        }

        private static FileEntry ToFileEntry(string archivePath, ArchiveFormat format, System.DateTime lastOpenedUtc) =>
            new FileEntry
            {
                Id = archivePath,
                Name = Path.GetFileName(archivePath),
                FullPath = archivePath,
                IsDirectory = false,
                IsArchive = true,
                ArchiveFormat = format,
                ModifiedUtc = lastOpenedUtc,
                Source = NavigationSource.Recent
            };

        private void Load()
        {
            try
            {
                if (!File.Exists(_path)) return;
                var json = File.ReadAllText(_path);
                var wrapper = JsonUtility.FromJson<RecentWrapper>(json);
                _records = wrapper?.items ?? new List<RecentRecord>();
            }
            catch
            {
                _records = new List<RecentRecord>();
            }
        }

        private void Save()
        {
            var dir = Path.GetDirectoryName(_path);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var wrapper = new RecentWrapper { items = _records };
            File.WriteAllText(_path, JsonUtility.ToJson(wrapper));
        }

        [System.Serializable]
        private class RecentWrapper
        {
            public List<RecentRecord> items = new();
        }

        [System.Serializable]
        private class RecentRecord
        {
            public string ArchivePath;
            public ArchiveFormat Format;
            public long LastOpenedTicks;

            public System.DateTime LastOpenedUtc
            {
                get => new System.DateTime(LastOpenedTicks, System.DateTimeKind.Utc);
                set => LastOpenedTicks = value.Ticks;
            }
        }
    }
}
