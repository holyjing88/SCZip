using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SCZip.Domain;

namespace SCZip.Infrastructure
{
    public static class ArchiveFormatRegistry
    {
        private static readonly Dictionary<string, ArchiveFormat> ExtensionMap = new(StringComparer.OrdinalIgnoreCase)
        {
            [".zip"] = ArchiveFormat.Zip,
            [".cbz"] = ArchiveFormat.Zip,
            [".tar.gz"] = ArchiveFormat.TarGzip,
            [".tgz"] = ArchiveFormat.TarGzip,
            [".gz"] = ArchiveFormat.Gzip
        };

        public static ArchiveFormat DetectByPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return ArchiveFormat.Unknown;

            var lower = path.ToLowerInvariant();
            if (lower.EndsWith(".tar.gz", StringComparison.Ordinal) || lower.EndsWith(".tgz", StringComparison.Ordinal))
                return ArchiveFormat.TarGzip;

            var ext = Path.GetExtension(lower);
            return ExtensionMap.TryGetValue(ext, out var fmt) ? fmt : ArchiveFormat.Unknown;
        }

        public static bool IsArchiveExtension(string path) => DetectByPath(path) != ArchiveFormat.Unknown;

        public static string GetDefaultExtension(ArchiveFormat format) => format switch
        {
            ArchiveFormat.TarGzip => ".tar.gz",
            ArchiveFormat.Gzip => ".gz",
            _ => ".zip"
        };
    }
}
