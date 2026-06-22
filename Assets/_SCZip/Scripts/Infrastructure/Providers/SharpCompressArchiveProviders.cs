using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SCZip.Domain;
using SCZip.Infrastructure;
using SCZip.Services;
using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;
using SharpCompress.Common.Options;
using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;
using SharpCompress.Compressors.Xz;
using SharpCompress.Providers;
using SharpCompress.Readers;
using SharpCompress.Writers;
using SharpCompress.Writers.SevenZip;

namespace SCZip.Infrastructure.Providers
{
    internal static class SharpCompressArchiveHelper
    {
        public static ReaderOptions BuildReaderOptions(string password) =>
            string.IsNullOrEmpty(password) ? new ReaderOptions() : new ReaderOptions { Password = password };

        public static WriterOptions BuildWriterOptions(CompressionType compression) =>
            new(compression) { LeaveStreamOpen = false };

        public static List<Domain.ArchiveEntry> MapEntries(IEnumerable<IArchiveEntry> entries) =>
            entries.Select(e => new Domain.ArchiveEntry
                {
                    Path = e.Key,
                    Name = string.IsNullOrEmpty(e.Key) ? e.Key : Path.GetFileName(e.Key.TrimEnd('/')),
                    IsDirectory = e.IsDirectory,
                    SizeBytes = e.Size
                })
                .OrderByDescending(e => e.IsDirectory)
                .ThenBy(e => e.Name)
                .ToList();

        public static void ExtractAll(string archivePath, string destinationDirectory, string password,
            IReadOnlyList<string> entryPaths, IProgress<ArchiveProgress> progress)
        {
            using var archive = OpenArchive(archivePath, password);
            var entries = archive.Entries.Where(e => !e.IsDirectory).ToList();
            if (entryPaths != null && entryPaths.Count > 0)
            {
                var set = new HashSet<string>(entryPaths);
                entries = entries.Where(e => set.Contains(e.Key) || set.Contains(e.Key.TrimEnd('/'))).ToList();
            }

            var total = Math.Max(entries.Count, 1);
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                entry.WriteToDirectory(destinationDirectory, new ExtractionOptions
                {
                    ExtractFullPath = true,
                    Overwrite = true
                });
                progress?.Report(new ArchiveProgress
                {
                    Progress = (i + 1) / (float)total,
                    CurrentItem = entry.Key
                });
            }
        }

        public static void WriteArchive(IWriter writer, IReadOnlyList<string> sourcePaths,
            IProgress<ArchiveProgress> progress)
        {
            var files = ArchiveSourceCollector.CollectFiles(sourcePaths);
            var total = Math.Max(files.Count, 1);
            for (var i = 0; i < files.Count; i++)
            {
                var file = files[i];
                using var fs = File.OpenRead(file.FullPath);
                writer.Write(file.Key, fs, File.GetLastWriteTimeUtc(file.FullPath));
                progress?.Report(new ArchiveProgress
                {
                    Progress = (i + 1) / (float)total,
                    CurrentItem = file.Key
                });
            }
        }

        public static void FinishStream(Stream stream)
        {
            if (stream is IFinishable finishable)
                finishable.Finish();
        }

        public static void EnsureArchiveFileReady(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                throw new FileNotFoundException("找不到压缩包。", path);

            if (new FileInfo(path).Length == 0)
                throw new InvalidOperationException("压缩包为空或已损坏，请删除后重新压缩。");

            if (ArchiveFormatRegistry.DetectByPath(path) == ArchiveFormat.SevenZip && !LooksLikeSevenZip(path))
                throw new InvalidOperationException("7Z 文件头无效，压缩包可能已损坏。");
        }

        public static IArchive OpenArchive(string archivePath, string password)
        {
            EnsureArchiveFileReady(archivePath);
            var options = BuildReaderOptions(password);
            if (ArchiveFormatRegistry.DetectByPath(archivePath) == ArchiveFormat.SevenZip)
                return SevenZipArchive.OpenArchive(archivePath, options);

            try
            {
                return ArchiveFactory.OpenArchive(archivePath, options);
            }
            catch (ArchiveOperationException ex) when (ex.Message.Contains("Cannot determine compressed stream type"))
            {
                throw new InvalidOperationException("无法识别压缩包格式，文件可能为空或已损坏。", ex);
            }
        }

        public static void TryDeleteFailedOutput(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // best effort cleanup after failed create
            }
        }

        private static bool LooksLikeSevenZip(string path)
        {
            var header = new byte[6];
            using var fs = File.OpenRead(path);
            if (fs.Read(header, 0, header.Length) < header.Length)
                return false;

            return header[0] == 0x37 && header[1] == 0x7a && header[2] == 0xbc && header[3] == 0xaf;
        }
    }

    public abstract class SingleStreamArchiveProvider : IArchiveProvider
    {
        public abstract ArchiveFormat Format { get; }
        public abstract bool CanCreate { get; }
        public bool CanList => true;
        public bool CanExtract => true;
        public bool SupportsEncryption => false;

        protected abstract Stream OpenCompressStream(Stream output);
        protected abstract Stream OpenDecompressStream(Stream input);

        protected virtual string ResolveOutputName(string archivePath)
        {
            var ext = ArchiveFormatRegistry.GetDefaultExtension(Format);
            var name = Path.GetFileName(archivePath);
            if (!string.IsNullOrEmpty(ext) && name.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                return name[..^ext.Length];
            return Path.GetFileNameWithoutExtension(name) + ".out";
        }

        public Task<IReadOnlyList<Domain.ArchiveEntry>> ListEntriesAsync(string path, ArchiveOpenOptions options,
            CancellationToken ct)
        {
            return Task.Run(() =>
            {
                var name = ResolveOutputName(path);
                return (IReadOnlyList<Domain.ArchiveEntry>)new List<Domain.ArchiveEntry>
                {
                    new() { Path = name, Name = name, IsDirectory = false, SizeBytes = 0 }
                };
            }, ct);
        }

        public Task ExtractAsync(ExtractOptions options, IProgress<ArchiveProgress> progress, CancellationToken ct)
        {
            return Task.Run(() =>
            {
                Directory.CreateDirectory(options.DestinationDirectory);
                var dest = Path.Combine(options.DestinationDirectory, ResolveOutputName(options.ArchivePath));
                using var input = File.OpenRead(options.ArchivePath);
                using var decompress = OpenDecompressStream(input);
                using var output = File.Create(dest);
                decompress.CopyTo(output);
                progress?.Report(new ArchiveProgress { Progress = 1f, CurrentItem = Path.GetFileName(dest) });
            }, ct);
        }

        public Task CreateAsync(CreateArchiveOptions options, IProgress<ArchiveProgress> progress, CancellationToken ct)
        {
            if (!CanCreate)
                return Task.FromException(new NotSupportedException($"{Format} creation is not supported."));

            return Task.Run(() =>
            {
                var files = ArchiveSourceCollector.CollectFiles(options.SourcePaths);
                if (files.Count != 1)
                    throw new InvalidOperationException($"{Format} supports a single source file. Use the TAR variant for folders.");

                using var input = File.OpenRead(files[0].FullPath);
                using var output = File.Create(options.OutputPath);
                using var compress = OpenCompressStream(output);
                input.CopyTo(compress);
                SharpCompressArchiveHelper.FinishStream(compress);
                progress?.Report(new ArchiveProgress { Progress = 1f, CurrentItem = Path.GetFileName(options.OutputPath) });
            }, ct);
        }
    }

    public sealed class Bzip2ArchiveProvider : SingleStreamArchiveProvider
    {
        public override ArchiveFormat Format => ArchiveFormat.Bzip2;
        public override bool CanCreate => true;
        protected override Stream OpenCompressStream(Stream output) =>
            BZip2Stream.Create(output, CompressionMode.Compress, false);
        protected override Stream OpenDecompressStream(Stream input) =>
            BZip2Stream.Create(input, CompressionMode.Decompress, false);
    }

    public sealed class XzArchiveProvider : SingleStreamArchiveProvider
    {
        public override ArchiveFormat Format => ArchiveFormat.Xz;
        public override bool CanCreate => false;
        protected override Stream OpenCompressStream(Stream output) =>
            throw new NotSupportedException("XZ creation is not supported by SharpCompress 0.49.");
        protected override Stream OpenDecompressStream(Stream input) => new XZStream(input);

        public new Task ExtractAsync(ExtractOptions options, IProgress<ArchiveProgress> progress, CancellationToken ct)
        {
            return Task.Run(() =>
            {
                Directory.CreateDirectory(options.DestinationDirectory);
                using var stream = File.OpenRead(options.ArchivePath);
                using var reader = ReaderFactory.OpenReader(stream,
                    SharpCompressArchiveHelper.BuildReaderOptions(options.Password));
                while (reader.MoveToNextEntry())
                {
                    if (reader.Entry.IsDirectory)
                        continue;
                    reader.WriteEntryToDirectory(options.DestinationDirectory, new ExtractionOptions
                    {
                        ExtractFullPath = true,
                        Overwrite = true
                    });
                    progress?.Report(new ArchiveProgress { Progress = 1f, CurrentItem = reader.Entry.Key });
                }
            }, ct);
        }
    }

    public sealed class ZstdArchiveProvider : SingleStreamArchiveProvider
    {
        public override ArchiveFormat Format => ArchiveFormat.Zstd;
        public override bool CanCreate => true;
        protected override Stream OpenCompressStream(Stream output) => new ZstdSharp.CompressionStream(output);
        protected override Stream OpenDecompressStream(Stream input) => new ZstdSharp.DecompressionStream(input);
    }

    public abstract class TarCompressedArchiveProvider : IArchiveProvider
    {
        public abstract ArchiveFormat Format { get; }
        protected abstract CompressionType CompressionType { get; }
        public abstract bool CanCreate { get; }
        public bool CanList => true;
        public bool CanExtract => true;
        public bool SupportsEncryption => false;

        public Task<IReadOnlyList<Domain.ArchiveEntry>> ListEntriesAsync(string path, ArchiveOpenOptions options,
            CancellationToken ct)
        {
            return Task.Run(() =>
            {
                using var archive = ArchiveFactory.OpenArchive(path,
                    SharpCompressArchiveHelper.BuildReaderOptions(options.Password));
                return (IReadOnlyList<Domain.ArchiveEntry>)SharpCompressArchiveHelper.MapEntries(archive.Entries);
            }, ct);
        }

        public Task ExtractAsync(ExtractOptions options, IProgress<ArchiveProgress> progress, CancellationToken ct)
        {
            return Task.Run(() =>
            {
                Directory.CreateDirectory(options.DestinationDirectory);
                SharpCompressArchiveHelper.ExtractAll(options.ArchivePath, options.DestinationDirectory,
                    options.Password, options.EntryPaths, progress);
            }, ct);
        }

        public Task CreateAsync(CreateArchiveOptions options, IProgress<ArchiveProgress> progress, CancellationToken ct)
        {
            if (!CanCreate)
                return Task.FromException(new NotSupportedException($"{Format} creation is not supported."));

            return Task.Run(() =>
            {
                using var stream = File.Create(options.OutputPath);
                using var writer = WriterFactory.OpenWriter(stream, ArchiveType.Tar,
                    SharpCompressArchiveHelper.BuildWriterOptions(CompressionType));
                SharpCompressArchiveHelper.WriteArchive(writer, options.SourcePaths, progress);
            }, ct);
        }
    }

    public sealed class TarBzip2ArchiveProvider : IArchiveProvider
    {
        public ArchiveFormat Format => ArchiveFormat.TarBzip2;
        public bool CanList => true;
        public bool CanExtract => true;
        public bool CanCreate => true;
        public bool SupportsEncryption => false;

        private static byte[] ReadOuter(string path)
        {
            using var input = File.OpenRead(path);
            using var bz = BZip2Stream.Create(input, CompressionMode.Decompress, false);
            using var ms = new MemoryStream();
            bz.CopyTo(ms);
            return ms.ToArray();
        }

        private static void WriteOuter(string path, byte[] tar)
        {
            using var output = File.Create(path);
            using var bz = BZip2Stream.Create(output, CompressionMode.Compress, false);
            bz.Write(tar, 0, tar.Length);
            SharpCompressArchiveHelper.FinishStream(bz);
        }

        public Task<IReadOnlyList<Domain.ArchiveEntry>> ListEntriesAsync(string path, ArchiveOpenOptions options,
            CancellationToken ct)
        {
            return Task.Run(() =>
                (IReadOnlyList<Domain.ArchiveEntry>)TarArchiveUtility.ReadEntries(ReadOuter(path)), ct);
        }

        public Task ExtractAsync(ExtractOptions options, IProgress<ArchiveProgress> progress, CancellationToken ct)
        {
            return Task.Run(() =>
            {
                var data = TarArchiveUtility.ReadFileData(ReadOuter(options.ArchivePath));
                var keys = data.Keys.ToList();
                if (options.EntryPaths != null && options.EntryPaths.Count > 0)
                {
                    var set = new HashSet<string>(options.EntryPaths);
                    keys = keys.Where(k => set.Contains(k)).ToList();
                }

                Directory.CreateDirectory(options.DestinationDirectory);
                var total = Math.Max(keys.Count, 1);
                for (var i = 0; i < keys.Count; i++)
                {
                    var key = keys[i];
                    var dest = Path.Combine(options.DestinationDirectory,
                        key.Replace('/', Path.DirectorySeparatorChar));
                    var dir = Path.GetDirectoryName(dest);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                    File.WriteAllBytes(dest, data[key]);
                    progress?.Report(new ArchiveProgress { Progress = (i + 1) / (float)total, CurrentItem = key });
                }
            }, ct);
        }

        public Task CreateAsync(CreateArchiveOptions options, IProgress<ArchiveProgress> progress, CancellationToken ct)
        {
            return Task.Run(() =>
            {
                var files = ArchiveSourceCollector.CollectFiles(options.SourcePaths);
                var tar = TarArchiveUtility.WriteTar(files);
                WriteOuter(options.OutputPath, tar);
                progress?.Report(new ArchiveProgress { Progress = 1f, CurrentItem = Path.GetFileName(options.OutputPath) });
            }, ct);
        }
    }

    public sealed class TarXzArchiveProvider : IArchiveProvider
    {
        public ArchiveFormat Format => ArchiveFormat.TarXz;
        public bool CanList => true;
        public bool CanExtract => true;
        public bool CanCreate => false;
        public bool SupportsEncryption => false;

        private static byte[] ReadOuter(string path)
        {
            using var input = File.OpenRead(path);
            using var xz = new XZStream(input);
            using var ms = new MemoryStream();
            xz.CopyTo(ms);
            return ms.ToArray();
        }

        public Task<IReadOnlyList<Domain.ArchiveEntry>> ListEntriesAsync(string path, ArchiveOpenOptions options,
            CancellationToken ct)
        {
            return Task.Run(() =>
                (IReadOnlyList<Domain.ArchiveEntry>)TarArchiveUtility.ReadEntries(ReadOuter(path)), ct);
        }

        public Task ExtractAsync(ExtractOptions options, IProgress<ArchiveProgress> progress, CancellationToken ct)
        {
            return Task.Run(() =>
            {
                var data = TarArchiveUtility.ReadFileData(ReadOuter(options.ArchivePath));
                var keys = data.Keys.ToList();
                if (options.EntryPaths != null && options.EntryPaths.Count > 0)
                {
                    var set = new HashSet<string>(options.EntryPaths);
                    keys = keys.Where(k => set.Contains(k)).ToList();
                }

                Directory.CreateDirectory(options.DestinationDirectory);
                var total = Math.Max(keys.Count, 1);
                for (var i = 0; i < keys.Count; i++)
                {
                    var key = keys[i];
                    var dest = Path.Combine(options.DestinationDirectory,
                        key.Replace('/', Path.DirectorySeparatorChar));
                    var dir = Path.GetDirectoryName(dest);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                    File.WriteAllBytes(dest, data[key]);
                    progress?.Report(new ArchiveProgress { Progress = (i + 1) / (float)total, CurrentItem = key });
                }
            }, ct);
        }

        public Task CreateAsync(CreateArchiveOptions options, IProgress<ArchiveProgress> progress, CancellationToken ct) =>
            Task.FromException(new NotSupportedException("TAR.XZ creation is not supported by SharpCompress 0.49."));
    }

    public sealed class TarZstdArchiveProvider : IArchiveProvider
    {
        public ArchiveFormat Format => ArchiveFormat.TarZstd;
        public bool CanList => true;
        public bool CanExtract => true;
        public bool CanCreate => true;
        public bool SupportsEncryption => false;

        private static byte[] ReadOuter(string path)
        {
            using var input = File.OpenRead(path);
            using var zst = new ZstdSharp.DecompressionStream(input);
            using var ms = new MemoryStream();
            zst.CopyTo(ms);
            return ms.ToArray();
        }

        private static void WriteOuter(string path, byte[] tar)
        {
            using var output = File.Create(path);
            using var zst = new ZstdSharp.CompressionStream(output);
            zst.Write(tar, 0, tar.Length);
        }

        public Task<IReadOnlyList<Domain.ArchiveEntry>> ListEntriesAsync(string path, ArchiveOpenOptions options,
            CancellationToken ct)
        {
            return Task.Run(() =>
                (IReadOnlyList<Domain.ArchiveEntry>)TarArchiveUtility.ReadEntries(ReadOuter(path)), ct);
        }

        public Task ExtractAsync(ExtractOptions options, IProgress<ArchiveProgress> progress, CancellationToken ct)
        {
            return Task.Run(() =>
            {
                var data = TarArchiveUtility.ReadFileData(ReadOuter(options.ArchivePath));
                var keys = data.Keys.ToList();
                if (options.EntryPaths != null && options.EntryPaths.Count > 0)
                {
                    var set = new HashSet<string>(options.EntryPaths);
                    keys = keys.Where(k => set.Contains(k)).ToList();
                }

                Directory.CreateDirectory(options.DestinationDirectory);
                var total = Math.Max(keys.Count, 1);
                for (var i = 0; i < keys.Count; i++)
                {
                    var key = keys[i];
                    var dest = Path.Combine(options.DestinationDirectory,
                        key.Replace('/', Path.DirectorySeparatorChar));
                    var dir = Path.GetDirectoryName(dest);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                    File.WriteAllBytes(dest, data[key]);
                    progress?.Report(new ArchiveProgress { Progress = (i + 1) / (float)total, CurrentItem = key });
                }
            }, ct);
        }

        public Task CreateAsync(CreateArchiveOptions options, IProgress<ArchiveProgress> progress, CancellationToken ct)
        {
            return Task.Run(() =>
            {
                var files = ArchiveSourceCollector.CollectFiles(options.SourcePaths);
                var tar = TarArchiveUtility.WriteTar(files);
                WriteOuter(options.OutputPath, tar);
                progress?.Report(new ArchiveProgress { Progress = 1f, CurrentItem = Path.GetFileName(options.OutputPath) });
            }, ct);
        }
    }

    public sealed class SevenZipArchiveProvider : IArchiveProvider
    {
        public ArchiveFormat Format => ArchiveFormat.SevenZip;
        public bool CanList => true;
        public bool CanExtract => true;
        public bool CanCreate => true;
        public bool SupportsEncryption => true;

        public Task<IReadOnlyList<Domain.ArchiveEntry>> ListEntriesAsync(string path, ArchiveOpenOptions options,
            CancellationToken ct)
        {
            return Task.Run(() =>
            {
                using var archive = SharpCompressArchiveHelper.OpenArchive(path, options.Password);
                return (IReadOnlyList<Domain.ArchiveEntry>)SharpCompressArchiveHelper.MapEntries(archive.Entries);
            }, ct);
        }

        public Task ExtractAsync(ExtractOptions options, IProgress<ArchiveProgress> progress, CancellationToken ct)
        {
            return Task.Run(() =>
            {
                Directory.CreateDirectory(options.DestinationDirectory);
                SharpCompressArchiveHelper.ExtractAll(options.ArchivePath, options.DestinationDirectory,
                    options.Password, options.EntryPaths, progress);
            }, ct);
        }

        public Task CreateAsync(CreateArchiveOptions options, IProgress<ArchiveProgress> progress, CancellationToken ct)
        {
            return Task.Run(() =>
            {
                var files = ArchiveSourceCollector.CollectFiles(options.SourcePaths);
                if (files.Count == 0)
                    throw new InvalidOperationException("没有可压缩的文件。");

                var outputPath = options.OutputPath;
                try
                {
                    using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.ReadWrite);
                    using var writer = new SevenZipWriter(stream, new SevenZipWriterOptions(CompressionType.LZMA2));
                    SharpCompressArchiveHelper.WriteArchive(writer, options.SourcePaths, progress);
                }
                catch
                {
                    SharpCompressArchiveHelper.TryDeleteFailedOutput(outputPath);
                    throw;
                }

                if (new FileInfo(outputPath).Length == 0)
                {
                    SharpCompressArchiveHelper.TryDeleteFailedOutput(outputPath);
                    throw new InvalidOperationException("7Z 压缩失败：输出文件为空。");
                }
            }, ct);
        }
    }

    public sealed class RarArchiveProvider : IArchiveProvider
    {
        public ArchiveFormat Format => ArchiveFormat.Rar;
        public bool CanList => true;
        public bool CanExtract => true;
        public bool CanCreate => false;
        public bool SupportsEncryption => true;

        public Task<IReadOnlyList<Domain.ArchiveEntry>> ListEntriesAsync(string path, ArchiveOpenOptions options,
            CancellationToken ct)
        {
            return Task.Run(() =>
            {
                using var archive = ArchiveFactory.OpenArchive(path,
                    SharpCompressArchiveHelper.BuildReaderOptions(options.Password));
                return (IReadOnlyList<Domain.ArchiveEntry>)SharpCompressArchiveHelper.MapEntries(archive.Entries);
            }, ct);
        }

        public Task ExtractAsync(ExtractOptions options, IProgress<ArchiveProgress> progress, CancellationToken ct)
        {
            return Task.Run(() =>
            {
                Directory.CreateDirectory(options.DestinationDirectory);
                SharpCompressArchiveHelper.ExtractAll(options.ArchivePath, options.DestinationDirectory,
                    options.Password, options.EntryPaths, progress);
            }, ct);
        }

        public Task CreateAsync(CreateArchiveOptions options, IProgress<ArchiveProgress> progress, CancellationToken ct) =>
            Task.FromException(new NotSupportedException("RAR 创建受 RARLAB 许可限制，当前仅支持解压。"));
    }
}
