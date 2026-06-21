using System;

namespace SCZip.Domain
{
    public sealed class ArchiveEntry
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public bool IsDirectory { get; set; }
        public long SizeBytes { get; set; }
        public DateTime ModifiedUtc { get; set; }
    }
}
