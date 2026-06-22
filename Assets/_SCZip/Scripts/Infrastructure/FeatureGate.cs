using SCZip.Domain;
using SCZip.Services;

namespace SCZip.Infrastructure
{
    /// <summary>Free tier for MVP dev builds; Pro gates encryption, mail, and exotic formats.</summary>
    public sealed class FeatureGate : IFeatureGate
    {
        public bool IsPro { get; set; }

        public bool CanCreate(ArchiveFormat format) => format switch
        {
            ArchiveFormat.Zip => true,
            ArchiveFormat.TarGzip => true,
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
