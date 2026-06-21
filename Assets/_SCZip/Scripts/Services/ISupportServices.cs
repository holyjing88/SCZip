using System.Collections.Generic;
using SCZip.Domain;

namespace SCZip.Services
{
    public interface IRecentService
    {
        IReadOnlyList<FileEntry> GetRecent();
        void AddRecent(string archivePath, ArchiveFormat format);
        void RemoveRecent(string archivePath);
    }

    public interface ISettingsService
    {
        ArchiveFormat DefaultFormat { get; set; }
        ArchiveCompressionLevel CompressionLevel { get; set; }
        void ClearLocalCache();
    }

    public interface IFeatureGate
    {
        bool IsPro { get; }
        bool CanCreate(ArchiveFormat format);
        bool CanExtract(ArchiveFormat format);
        bool CanUseEncryption { get; }
        bool CanUseMail { get; }
    }
}
