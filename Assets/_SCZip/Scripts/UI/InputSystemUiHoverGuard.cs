using UnityEngine;

#if ENABLE_INPUT_SYSTEM
namespace SCZip.UI
{
    /// <summary>
    /// Runs before EventSystem and strips destroyed UI objects from Input System hover state.
    /// Prevents MissingReferenceException when uGUI Dropdown closes and destroys list items.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    internal sealed class InputSystemUiHoverGuard : MonoBehaviour
    {
        private void Update()
        {
            UiEventSystemSetup.SanitizeStaleHoverTargets();
        }
    }
}
#endif
