using System;
using System.Collections.Generic;
using System.Threading;
using SCZip.Domain;

namespace SCZip.Services
{
    public sealed class ArchiveOpenOptions
    {
        public string Password { get; set; }
    }

    public sealed class CreateArchiveOptions
    {
        public ArchiveFormat Format { get; set; } = ArchiveFormat.Zip;
        public string OutputPath { get; set; }
        public IReadOnlyList<string> SourcePaths { get; set; }
        public ArchiveCompressionLevel Level { get; set; } = ArchiveCompressionLevel.Normal;
        public string Password { get; set; }
    }

    public sealed class ExtractOptions
    {
        public string ArchivePath { get; set; }
        public string DestinationDirectory { get; set; }
        public IReadOnlyList<string> EntryPaths { get; set; }
        public string Password { get; set; }
    }

    public sealed class ArchiveProgress
    {
        public float Progress { get; set; }
        public string CurrentItem { get; set; }
    }

    public interface IArchiveProvider
    {
        ArchiveFormat Format { get; }
        bool CanList { get; }
        bool CanExtract { get; }
        bool CanCreate { get; }
        bool SupportsEncryption { get; }

        System.Threading.Tasks.Task<IReadOnlyList<ArchiveEntry>> ListEntriesAsync(
            string path, ArchiveOpenOptions options, CancellationToken ct);

        System.Threading.Tasks.Task ExtractAsync(
            ExtractOptions options, IProgress<ArchiveProgress> progress, CancellationToken ct);

        System.Threading.Tasks.Task CreateAsync(
            CreateArchiveOptions options, IProgress<ArchiveProgress> progress, CancellationToken ct);
    }

    public interface IArchiveService
    {
        ArchiveFormat DetectFormat(string path);
        bool IsSupportedArchive(string path);
        IArchiveProvider GetProvider(ArchiveFormat format);
        IReadOnlyList<ArchiveFormat> GetCreatableFormats();
        IReadOnlyList<ArchiveFormat> GetExtractableFormats();

        System.Threading.Tasks.Task<IReadOnlyList<ArchiveEntry>> ListEntriesAsync(
            string path, ArchiveOpenOptions options, CancellationToken ct);

        System.Threading.Tasks.Task ExtractAsync(
            ExtractOptions options, IProgress<ArchiveProgress> progress, CancellationToken ct);

        System.Threading.Tasks.Task CreateAsync(
            CreateArchiveOptions options, IProgress<ArchiveProgress> progress, CancellationToken ct);
    }
}
