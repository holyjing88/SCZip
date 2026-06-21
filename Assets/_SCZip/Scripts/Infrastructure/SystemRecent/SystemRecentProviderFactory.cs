using SCZip.Services;
using UnityEngine;

namespace SCZip.Infrastructure.SystemRecent
{
    public static class SystemRecentProviderFactory
    {
        public static ISystemRecentProvider Create()
        {
            if (Application.platform == RuntimePlatform.OSXEditor ||
                Application.platform == RuntimePlatform.OSXPlayer)
                return new MacSystemRecentProvider();

            return new NullSystemRecentProvider();
        }
    }
}
