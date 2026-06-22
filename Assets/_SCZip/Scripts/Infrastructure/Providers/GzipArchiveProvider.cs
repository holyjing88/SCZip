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
    /// <summary>Single-file .gz compress/decompress using BCL GZipStream.</summary>
    public sealed class GzipArchiveProvider : IArchiveProvider
    {
        public ArchiveFormat Format => ArchiveFormat.Gzip;
        public bool CanList => true;
        public bool CanExtract => true;
        public bool CanCreate => true;
        public bool SupportsEncryption => false;

        public Task<IReadOnlyList<Domain.ArchiveEntry>> ListEntriesAsync(string path, ArchiveOpenOptions options,
            CancellationToken ct)
        {
            return Task.Run(() =>
            {
                var name = Path.GetFileName(path);
                if (name.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) && name.Length > 3)
                    name = name[..^3];

                long size = 0;
                try
                {
                    using var fs = File.OpenRead(path);
                    using var gz = new GZipStream(fs, CompressionMode.Decompress, leaveOpen: true);
                    using var ms = new MemoryStream();
                    gz.CopyTo(ms);
                    size = ms.Length;
                }
                catch
                {
                    size = 0;
                }

                return (IReadOnlyList<Domain.ArchiveEntry>)new List<Domain.ArchiveEntry>
                {
                    new()
                    {
                        Path = name,
                        Name = name,
                        IsDirectory = false,
                        SizeBytes = size
                    }
                };
            }, ct);
        }

        public Task ExtractAsync(ExtractOptions options, IProgress<ArchiveProgress> progress, CancellationToken ct)
        {
            return Task.Run(() =>
            {
                var archiveName = Path.GetFileName(options.ArchivePath);
                var outName = archiveName.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) && archiveName.Length > 3
                    ? archiveName[..^3]
                    : archiveName + ".out";

                Directory.CreateDirectory(options.DestinationDirectory);
                var dest = Path.Combine(options.DestinationDirectory, outName);
                using var input = File.OpenRead(options.ArchivePath);
                using var gz = new GZipStream(input, CompressionMode.Decompress);
                using var output = File.Create(dest);
                gz.CopyTo(output);
                progress?.Report(new ArchiveProgress { Progress = 1f, CurrentItem = outName });
            }, ct);
        }

        public Task CreateAsync(CreateArchiveOptions options, IProgress<ArchiveProgress> progress, CancellationToken ct)
        {
            return Task.Run(() =>
            {
                var sources = ArchiveSourceCollector.CollectFiles(options.SourcePaths);
                if (sources.Count == 0)
                    throw new InvalidOperationException("No files to compress.");

                if (sources.Count > 1)
                    throw new InvalidOperationException("GZ supports a single source file. Use TAR.GZ for folders.");

                var src = sources[0];
                using var input = File.OpenRead(src.FullPath);
                using var output = File.Create(options.OutputPath);
                using var gz = new GZipStream(output, MapLevel(options.Level), leaveOpen: false);
                input.CopyTo(gz);
                progress?.Report(new ArchiveProgress { Progress = 1f, CurrentItem = Path.GetFileName(options.OutputPath) });
            }, ct);
        }

        private static System.IO.Compression.CompressionLevel MapLevel(ArchiveCompressionLevel level) => level switch
        {
            ArchiveCompressionLevel.Store => System.IO.Compression.CompressionLevel.NoCompression,
            ArchiveCompressionLevel.Fast => System.IO.Compression.CompressionLevel.Fastest,
            _ => System.IO.Compression.CompressionLevel.Optimal
        };
    }
}
