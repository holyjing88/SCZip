using SCZip.Domain;
using SCZip.Services;

namespace SCZip.Infrastructure
{
    /// <summary>All archive formats enabled for create/extract in dev builds.</summary>
    public sealed class FeatureGate : IFeatureGate
    {
        public bool IsPro { get; set; }

        public bool CanCreate(ArchiveFormat format) => format != ArchiveFormat.Unknown;

        public bool CanExtract(ArchiveFormat format) => format != ArchiveFormat.Unknown;

        public bool CanUseEncryption => true;
        public bool CanUseMail => true;
    }
}
