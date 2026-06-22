using SCZip.Infrastructure;
using SCZip.Services;

namespace SCZip.Core
{
    public static class AppServices
    {
        private static bool _initialized;

        public static IFileSystemService FileSystem { get; private set; }
        public static IArchiveService Archive { get; private set; }
        public static IRecentService Recent { get; private set; }
        public static ISettingsService Settings { get; private set; }
        public static IFeatureGate FeatureGate { get; private set; }

        public static void EnsureInitialized()
        {
            if (_initialized) return;
            Initialize();
        }

        public static void Initialize()
        {
            if (_initialized) return;

            FeatureGate = new FeatureGate();
            SharpCompressBootstrap.EnsureInitialized();
            FileSystem = new LocalFileSystemService();
            Archive = new ArchiveService(FeatureGate);
            Recent = new RecentService();
            Settings = new SettingsService();
            FileSystem.EnsureAppDirectories();
            _initialized = true;
        }
    }
}
