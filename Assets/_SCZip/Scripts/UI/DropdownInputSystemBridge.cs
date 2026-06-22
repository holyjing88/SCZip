using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SCZip.UI
{
    /// <summary>
    /// Clears Input System hover/press state when a uGUI Dropdown closes its popup list.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DropdownInputSystemBridge : MonoBehaviour
    {
        private Dropdown _dropdown;
        private bool _listWasOpen;

        public static void EnsureOn(Dropdown dropdown)
        {
            if (dropdown == null)
                return;

            if (dropdown.GetComponent<DropdownInputSystemBridge>() == null)
                dropdown.gameObject.AddComponent<DropdownInputSystemBridge>();
        }

        private void Awake()
        {
            _dropdown = GetComponent<Dropdown>();
            if (_dropdown != null)
                _dropdown.onValueChanged.AddListener(OnValueChanged);
        }

        private void OnDestroy()
        {
            if (_dropdown != null)
                _dropdown.onValueChanged.RemoveListener(OnValueChanged);
        }

        private void OnValueChanged(int _)
        {
            UiEventSystemSetup.ClearUiSelection();
            StartCoroutine(ClearAfterDropdownHide());
        }

        private void LateUpdate()
        {
            if (_dropdown == null)
                return;

            var listOpen = IsDropdownListOpen();
            if (_listWasOpen && !listOpen)
                UiEventSystemSetup.ClearUiSelection();

            _listWasOpen = listOpen;
        }

        private bool IsDropdownListOpen()
        {
            var canvas = _dropdown.GetComponentInParent<Canvas>();
            if (canvas == null)
                return false;

            var root = canvas.rootCanvas != null ? canvas.rootCanvas.transform : canvas.transform;
            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child != null && child.gameObject.activeInHierarchy &&
                    child.name.StartsWith("Dropdown List"))
                    return true;
            }

            return false;
        }

        private static IEnumerator ClearAfterDropdownHide()
        {
            yield return null;
            UiEventSystemSetup.ClearUiSelection();
            yield return null;
            UiEventSystemSetup.SanitizeStaleHoverTargets();
        }
    }
}
