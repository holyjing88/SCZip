using System;
using System.Collections.Generic;

namespace SCZip.Services
{
    public sealed class SystemRecentItem
    {
        public string Path { get; set; }
        public DateTime LastUsedUtc { get; set; }
    }

    public interface ISystemRecentProvider
    {
        IReadOnlyList<SystemRecentItem> GetRecentItems(int maxItems);
    }
}
