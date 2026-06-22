using UnityEngine;
using UnityEngine.UI;

namespace SCZip.UI
{
    /// <summary>
    /// uGUI Dropdown that clears Input System hover state before destroying popup items.
    /// </summary>
    public sealed class SafeDropdown : Dropdown
    {
        public static SafeDropdown Migrate(Dropdown legacy)
        {
            if (legacy == null)
                return null;

            if (legacy is SafeDropdown safe)
                return safe;

            var go = legacy.gameObject;
            var options = legacy.options;
            var value = legacy.value;
            var target = legacy.targetGraphic;
            Destroy(legacy);

            safe = go.AddComponent<SafeDropdown>();
            safe.targetGraphic = target;
            safe.options = options;
            safe.value = value;
            safe.alphaFadeSpeed = 0f;
            return safe;
        }

        protected override void DestroyItem(DropdownItem item)
        {
            UiEventSystemSetup.SanitizeStaleHoverTargets();
            base.DestroyItem(item);
        }

        protected override void DestroyDropdownList(GameObject dropdownList)
        {
            UiEventSystemSetup.ClearUiSelection();
            base.DestroyDropdownList(dropdownList);
            UiEventSystemSetup.SanitizeStaleHoverTargets();
        }
    }
}
