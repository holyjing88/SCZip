namespace SCZip.Domain
{
    public enum ArchiveFormat
    {
        Unknown,
        Zip,
        TarGzip,
        Gzip,
        SevenZip,
        Rar,
        Bzip2,
        TarBzip2,
        Xz,
        TarXz,
        Zstd,
        TarZstd
    }

    public enum NavigationSource
    {
        Recent,
        MyFiles,
        Storage,
        Photos,
        Music,
        ArchiveInner
    }

    public enum ArchiveCompressionLevel
    {
        Store,
        Fast,
        Normal,
        Maximum
    }

    public enum FileAction
    {
        Unzip,
        Compress,
        Share,
        Delete,
        Mail,
        Copy,
        Rename,
        More
    }
}
