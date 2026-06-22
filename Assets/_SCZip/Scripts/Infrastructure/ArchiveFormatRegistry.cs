using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SCZip.Domain;

namespace SCZip.Infrastructure
{
    public static class ArchiveFormatRegistry
    {
        private static readonly (string suffix, ArchiveFormat format)[] CompoundSuffixes =
        {
            (".tar.zst", ArchiveFormat.TarZstd),
            (".tzst", ArchiveFormat.TarZstd),
            (".tar.xz", ArchiveFormat.TarXz),
            (".txz", ArchiveFormat.TarXz),
            (".tar.bz2", ArchiveFormat.TarBzip2),
            (".tbz2", ArchiveFormat.TarBzip2),
            (".tar.gz", ArchiveFormat.TarGzip),
            (".tgz", ArchiveFormat.TarGzip)
        };

        private static readonly Dictionary<string, ArchiveFormat> ExtensionMap = new(StringComparer.OrdinalIgnoreCase)
        {
            [".zip"] = ArchiveFormat.Zip,
            [".cbz"] = ArchiveFormat.Zip,
            [".7z"] = ArchiveFormat.SevenZip,
            [".rar"] = ArchiveFormat.Rar,
            [".cbr"] = ArchiveFormat.Rar,
            [".gz"] = ArchiveFormat.Gzip,
            [".bz2"] = ArchiveFormat.Bzip2,
            [".xz"] = ArchiveFormat.Xz,
            [".zst"] = ArchiveFormat.Zstd
        };

        private static readonly Dictionary<ArchiveFormat, string> DefaultExtensions = new()
        {
            [ArchiveFormat.Zip] = ".zip",
            [ArchiveFormat.TarGzip] = ".tar.gz",
            [ArchiveFormat.Gzip] = ".gz",
            [ArchiveFormat.SevenZip] = ".7z",
            [ArchiveFormat.Rar] = ".rar",
            [ArchiveFormat.Bzip2] = ".bz2",
            [ArchiveFormat.TarBzip2] = ".tar.bz2",
            [ArchiveFormat.Xz] = ".xz",
            [ArchiveFormat.TarXz] = ".tar.xz",
            [ArchiveFormat.Zstd] = ".zst",
            [ArchiveFormat.TarZstd] = ".tar.zst"
        };

        private static readonly Dictionary<ArchiveFormat, string> DisplayLabels = new()
        {
            [ArchiveFormat.Zip] = "ZIP (.zip)",
            [ArchiveFormat.TarGzip] = "TAR.GZ (.tar.gz)",
            [ArchiveFormat.Gzip] = "GZ (.gz)",
            [ArchiveFormat.SevenZip] = "7Z (.7z)",
            [ArchiveFormat.Rar] = "RAR (.rar)",
            [ArchiveFormat.Bzip2] = "BZ2 (.bz2)",
            [ArchiveFormat.TarBzip2] = "TAR.BZ2 (.tar.bz2)",
            [ArchiveFormat.Xz] = "XZ (.xz)",
            [ArchiveFormat.TarXz] = "TAR.XZ (.tar.xz)",
            [ArchiveFormat.Zstd] = "ZST (.zst)",
            [ArchiveFormat.TarZstd] = "TAR.ZST (.tar.zst)"
        };

        public static ArchiveFormat DetectByPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return ArchiveFormat.Unknown;

            var lower = path.ToLowerInvariant();
            foreach (var (suffix, format) in CompoundSuffixes)
            {
                if (lower.EndsWith(suffix, StringComparison.Ordinal))
                    return format;
            }

            var ext = Path.GetExtension(lower);
            return ExtensionMap.TryGetValue(ext, out var fmt) ? fmt : ArchiveFormat.Unknown;
        }

        public static bool IsArchiveExtension(string path) => DetectByPath(path) != ArchiveFormat.Unknown;

        public static string GetDefaultExtension(ArchiveFormat format) =>
            DefaultExtensions.TryGetValue(format, out var ext) ? ext : ".zip";

        public static string GetDisplayLabel(ArchiveFormat format) =>
            DisplayLabels.TryGetValue(format, out var label) ? label : format.ToString();

        public static IReadOnlyList<ArchiveFormat> GetCreatableDisplayOrder() => new[]
        {
            ArchiveFormat.Zip,
            ArchiveFormat.SevenZip,
            ArchiveFormat.Rar,
            ArchiveFormat.TarGzip,
            ArchiveFormat.TarBzip2,
            ArchiveFormat.TarXz,
            ArchiveFormat.TarZstd,
            ArchiveFormat.Gzip,
            ArchiveFormat.Bzip2,
            ArchiveFormat.Xz,
            ArchiveFormat.Zstd
        };

        public static string StripKnownExtension(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return name;

            var trimmed = name.Trim();
            var lower = trimmed.ToLowerInvariant();
            foreach (var (suffix, _) in CompoundSuffixes.OrderByDescending(s => s.suffix.Length))
            {
                if (lower.EndsWith(suffix, StringComparison.Ordinal))
                    return trimmed[..^suffix.Length];
            }

            var ext = Path.GetExtension(lower);
            if (!string.IsNullOrEmpty(ext) && ExtensionMap.ContainsKey(ext))
                return trimmed[..^ext.Length];

            return trimmed;
        }
    }
}
