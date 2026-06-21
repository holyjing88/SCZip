using System.Collections.Generic;
using SCZip.Services;

namespace SCZip.Infrastructure.SystemRecent
{
    public sealed class NullSystemRecentProvider : ISystemRecentProvider
    {
        public IReadOnlyList<SystemRecentItem> GetRecentItems(int maxItems) =>
            System.Array.Empty<SystemRecentItem>();
    }
}
