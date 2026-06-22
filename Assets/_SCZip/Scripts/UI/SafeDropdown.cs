using System.Collections.Generic;
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

            if (legacy is SafeDropdown safeDropdown)
                return safeDropdown;

            var go = legacy.gameObject;
            var target = legacy.targetGraphic;

            Object.DestroyImmediate(legacy);

            var migrated = go.AddComponent<SafeDropdown>();
            if (migrated == null)
            {
                Debug.LogError("[SCZip] Failed to migrate DialogFormat to SafeDropdown.");
                return null;
            }

            if (target != null)
                migrated.targetGraphic = target;

            migrated.alphaFadeSpeed = 0f;
            return migrated;
        }

        public void ApplySavedState(IReadOnlyList<Dropdown.OptionData> options, int value)
        {
            if (options != null && options.Count > 0)
                this.options = new List<Dropdown.OptionData>(options);

            if (this.options.Count == 0)
                return;

            var clamped = Mathf.Clamp(value, 0, this.options.Count - 1);
            SetValueWithoutNotify(clamped);
            RefreshShownValue();
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
