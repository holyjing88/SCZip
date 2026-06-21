using SCZip.Domain;
using SCZip.Services;

namespace SCZip.Infrastructure
{
    /// <summary>Free tier for MVP dev builds; toggle IsPro for testing.</summary>
    public sealed class FeatureGate : IFeatureGate
    {
        public bool IsPro { get; set; }

        public bool CanCreate(ArchiveFormat format) => format switch
        {
            ArchiveFormat.Zip => true,
            ArchiveFormat.TarGzip => IsPro,
            _ => IsPro
        };

        public bool CanExtract(ArchiveFormat format) => format switch
        {
            ArchiveFormat.Zip => true,
            ArchiveFormat.TarGzip => true,
            ArchiveFormat.Gzip => true,
            _ => IsPro
        };

        public bool CanUseEncryption => IsPro;
        public bool CanUseMail => IsPro;
    }
}
