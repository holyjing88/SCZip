using System;

namespace SCZip.Domain
{
    [Serializable]
    public sealed class FileEntry
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string FullPath { get; set; }
        public bool IsDirectory { get; set; }
        public bool IsArchive { get; set; }
        public ArchiveFormat ArchiveFormat { get; set; }
        public long SizeBytes { get; set; }
        public DateTime ModifiedUtc { get; set; }
        public NavigationSource Source { get; set; }

        public string SizeLabel => IsDirectory ? "" : FormatSize(SizeBytes);
        public string DateLabel => ModifiedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

        public static string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
        }
    }
}
