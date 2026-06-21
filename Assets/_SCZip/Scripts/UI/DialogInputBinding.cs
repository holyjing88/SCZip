using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SCZip.UI
{
    public static class DialogInputBinding
    {
        public static void Resolve(AppShellView view)
        {
            if (view == null)
                return;

            var childInput = view.dialogInput != null
                ? view.dialogInput.transform.Find("Input")?.GetComponent<InputField>()
                : null;
            if (childInput != null)
            {
                view.dialogInput = childInput;
                SanitizeRow(view.dialogInput.transform.parent);
                return;
            }

            if (view.dialogInput != null && view.dialogInput.gameObject.name == "Input")
            {
                SanitizeRow(view.dialogInput.transform.parent);
                return;
            }

            Transform row = null;
            if (view.dialogOverlay != null)
                row = view.dialogOverlay.transform.Find("Dialog/DialogInput");

            if (row == null && view.dialogInput != null)
                row = view.dialogInput.transform.parent;

            if (row == null)
                return;

            var input = row.Find("Input")?.GetComponent<InputField>();
            if (input != null)
                view.dialogInput = input;

            SanitizeRow(row);
        }

        public static void SanitizeRow(Transform row)
        {
            if (row == null)
                return;

            ClearEventSystemForRow(row);

            var containerField = row.GetComponent<InputField>();
            if (containerField != null)
                containerField.enabled = false;

            var bridgeOnContainer = row.GetComponent<InputFieldInputSystemBridge>();
            if (bridgeOnContainer != null)
                bridgeOnContainer.enabled = false;

            for (var i = 0; i < row.childCount; i++)
            {
                var child = row.GetChild(i);
                if (child.name == "Input")
                    continue;

                if (child.name is "Text" or "Placeholder")
                    child.gameObject.SetActive(false);
            }
        }

        public static void ClearEventSystemForRow(Transform row)
        {
            if (row == null || EventSystem.current == null)
                return;

            var es = EventSystem.current;
            var selected = es.currentSelectedGameObject;
            if (selected != null && selected.transform.IsChildOf(row))
            {
                if (selected.TryGetComponent<InputField>(out var field))
                    field.DeactivateInputField();

                es.SetSelectedGameObject(null);
            }
        }

        public static GameObject RowObject(InputField field)
        {
            if (field == null)
                return null;

            var row = field.transform.parent;
            return row != null ? row.gameObject : field.gameObject;
        }
    }
}
