using SCZip.Core;
using SCZip.UI;
using UnityEngine;

namespace SCZip.Core
{
    [DefaultExecutionOrder(-1000)]
    public sealed class AppBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            UiEventSystemSetup.Ensure();
            _ = UnityMainThreadDispatcher.Instance;
            AppServices.EnsureInitialized();
        }
    }
}
