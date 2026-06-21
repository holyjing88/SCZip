namespace SCZip.Domain
{
    public enum ArchiveFormat
    {
        Unknown,
        Zip,
        TarGzip,
        Gzip
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
