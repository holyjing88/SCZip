using SCZip.Domain;
using SCZip.Services;
using UnityEngine;

namespace SCZip.Infrastructure
{
    public sealed class SettingsService : ISettingsService
    {
        private const string KeyFormat = "sczip.defaultFormat";
        private const string KeyLevel = "sczip.compressionLevel";

        public ArchiveFormat DefaultFormat
        {
            get => (ArchiveFormat)PlayerPrefs.GetInt(KeyFormat, (int)ArchiveFormat.Zip);
            set => PlayerPrefs.SetInt(KeyFormat, (int)value);
        }

        public ArchiveCompressionLevel CompressionLevel
        {
            get => (ArchiveCompressionLevel)PlayerPrefs.GetInt(KeyLevel, (int)ArchiveCompressionLevel.Normal);
            set => PlayerPrefs.SetInt(KeyLevel, (int)value);
        }

        public void ClearLocalCache()
        {
            var cache = System.IO.Path.Combine(Application.persistentDataPath, "SCZip", "Cache");
            if (System.IO.Directory.Exists(cache))
                System.IO.Directory.Delete(cache, true);
        }
    }
}
