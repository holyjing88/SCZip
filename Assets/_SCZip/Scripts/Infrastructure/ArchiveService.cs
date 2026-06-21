using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SCZip.Domain;
using SCZip.Infrastructure.Providers;
using SCZip.Services;

namespace SCZip.Infrastructure
{
    public sealed class ArchiveService : IArchiveService
    {
        private readonly Dictionary<ArchiveFormat, IArchiveProvider> _providers;
        private readonly IFeatureGate _featureGate;

        public ArchiveService(IFeatureGate featureGate)
        {
            _featureGate = featureGate;
            _providers = new Dictionary<ArchiveFormat, IArchiveProvider>
            {
                [ArchiveFormat.Zip] = new ZipArchiveProvider(),
                [ArchiveFormat.TarGzip] = new TarGzipArchiveProvider()
            };
        }

        public ArchiveFormat DetectFormat(string path) => ArchiveFormatRegistry.DetectByPath(path);

        public bool IsSupportedArchive(string path)
        {
            var fmt = DetectFormat(path);
            return fmt != ArchiveFormat.Unknown && _providers.ContainsKey(fmt);
        }

        public IArchiveProvider GetProvider(ArchiveFormat format) =>
            _providers.TryGetValue(format, out var p) ? p : null;

        public IReadOnlyList<ArchiveFormat> GetCreatableFormats() =>
            _providers.Values.Where(p => p.CanCreate && _featureGate.CanCreate(p.Format)).Select(p => p.Format).ToList();

        public IReadOnlyList<ArchiveFormat> GetExtractableFormats() =>
            _providers.Values.Where(p => p.CanExtract && _featureGate.CanExtract(p.Format)).Select(p => p.Format).ToList();

        public Task<IReadOnlyList<ArchiveEntry>> ListEntriesAsync(string path, ArchiveOpenOptions options,
            CancellationToken ct)
        {
            var provider = GetProvider(DetectFormat(path));
            return provider?.ListEntriesAsync(path, options, ct) ??
                   Task.FromResult<IReadOnlyList<ArchiveEntry>>(new List<ArchiveEntry>());
        }

        public Task ExtractAsync(ExtractOptions options, IProgress<ArchiveProgress> progress, CancellationToken ct)
        {
            var provider = GetProvider(DetectFormat(options.ArchivePath));
            return provider.ExtractAsync(options, progress, ct);
        }

        public Task CreateAsync(CreateArchiveOptions options, IProgress<ArchiveProgress> progress, CancellationToken ct)
        {
            var provider = GetProvider(options.Format);
            return provider.CreateAsync(options, progress, ct);
        }
    }
}
