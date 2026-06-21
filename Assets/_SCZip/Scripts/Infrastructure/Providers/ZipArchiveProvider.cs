using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SCZip.Domain;
using SCZip.Services;

namespace SCZip.Infrastructure.Providers
{
    public sealed class ZipArchiveProvider : IArchiveProvider
    {
        public ArchiveFormat Format => ArchiveFormat.Zip;
        public bool CanList => true;
        public bool CanExtract => true;
        public bool CanCreate => true;
        public bool SupportsEncryption => false;

        public Task<IReadOnlyList<ArchiveEntry>> ListEntriesAsync(string path, ArchiveOpenOptions options,
            CancellationToken ct)
        {
            return Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                using var stream = File.OpenRead(path);
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
                var list = new List<ArchiveEntry>();
                foreach (var entry in archive.Entries)
                {
                    var isDir = string.IsNullOrEmpty(entry.Name) && entry.FullName.EndsWith("/");
                    list.Add(new ArchiveEntry
                    {
                        Path = entry.FullName.TrimEnd('/'),
                        Name = isDir ? entry.FullName.Trim('/').Split('/').Last() : entry.Name,
                        IsDirectory = isDir,
                        SizeBytes = entry.Length,
                        ModifiedUtc = entry.LastWriteTime.UtcDateTime
                    });
                }

                return (IReadOnlyList<ArchiveEntry>)list
                    .Where(e => !string.IsNullOrEmpty(e.Path))
                    .OrderByDescending(e => e.IsDirectory)
                    .ThenBy(e => e.Name)
                    .ToList();
            }, ct);
        }

        public Task ExtractAsync(ExtractOptions options, IProgress<ArchiveProgress> progress, CancellationToken ct)
        {
            return Task.Run(() =>
            {
                using var stream = File.OpenRead(options.ArchivePath);
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
                var entries = archive.Entries.Where(e => !string.IsNullOrEmpty(e.Name)).ToList();
                if (options.EntryPaths != null && options.EntryPaths.Count > 0)
                {
                    var set = new HashSet<string>(options.EntryPaths);
                    entries = entries.Where(e => set.Contains(e.FullName.TrimEnd('/')) || set.Contains(e.Name)).ToList();
                }

                var total = Math.Max(entries.Count, 1);
                for (var i = 0; i < entries.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var entry = entries[i];
                    var destPath = Path.Combine(options.DestinationDirectory, entry.FullName);
                    var destDir = Path.GetDirectoryName(destPath);
                    if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                        Directory.CreateDirectory(destDir);

                    entry.ExtractToFile(destPath, true);
                    progress?.Report(new ArchiveProgress
                    {
                        Progress = (i + 1) / (float)total,
                        CurrentItem = entry.Name
                    });
                }
            }, ct);
        }

        public Task CreateAsync(CreateArchiveOptions options, IProgress<ArchiveProgress> progress, CancellationToken ct)
        {
            return Task.Run(() =>
            {
                var level = options.Level switch
                {
                    ArchiveCompressionLevel.Store => System.IO.Compression.CompressionLevel.NoCompression,
                    ArchiveCompressionLevel.Fast => System.IO.Compression.CompressionLevel.Fastest,
                    ArchiveCompressionLevel.Maximum => System.IO.Compression.CompressionLevel.Optimal,
                    _ => System.IO.Compression.CompressionLevel.Optimal
                };

                if (File.Exists(options.OutputPath))
                    File.Delete(options.OutputPath);

                var sources = options.SourcePaths ?? Array.Empty<string>();
                var flat = new List<string>();
                foreach (var src in sources)
                {
                    if (Directory.Exists(src))
                        flat.AddRange(Directory.GetFiles(src, "*", SearchOption.AllDirectories));
                    else if (File.Exists(src))
                        flat.Add(src);
                }

                var total = Math.Max(flat.Count, 1);
                using var zipStream = File.Create(options.OutputPath);
                using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create);

                for (var i = 0; i < flat.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var file = flat[i];
                    var basePath = GetCommonBase(sources, file);
                    var entryName = string.IsNullOrEmpty(basePath)
                        ? Path.GetFileName(file)
                        : file.Substring(basePath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                            .Replace('\\', '/');

                    archive.CreateEntryFromFile(file, entryName, level);
                    progress?.Report(new ArchiveProgress
                    {
                        Progress = (i + 1) / (float)total,
                        CurrentItem = Path.GetFileName(file)
                    });
                }
            }, ct);
        }

        private static string GetCommonBase(IReadOnlyList<string> sources, string file)
        {
            foreach (var src in sources)
            {
                if (Directory.Exists(src) && file.StartsWith(src, StringComparison.Ordinal))
                    return src;
            }

            return Path.GetDirectoryName(file);
        }
    }
}
