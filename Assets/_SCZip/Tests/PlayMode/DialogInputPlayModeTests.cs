using System.Collections;
using NUnit.Framework;
using SCZip.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace SCZip.Tests.PlayMode
{
    public sealed class DialogInputPlayModeTests
    {
        [UnityTest]
        public IEnumerator Dialog_input_focuses_and_accepts_characters()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;

            UiEventSystemSetup.Ensure();

            var canvas = GameObject.Find("UICanvas");
            Assert.NotNull(canvas, "UICanvas missing");

            var view = canvas.GetComponent<AppShellView>();
            Assert.NotNull(view, "AppShellView missing");

            DialogInputBinding.Resolve(view);
            Assert.NotNull(view.dialogInput, "dialogInput missing");

            InputFieldInputSystemBridge.EnsureOn(view.dialogInput);
            Assert.NotNull(
                view.dialogInput.GetComponent<InputFieldInputSystemBridge>(),
                "InputFieldInputSystemBridge missing");

            var systems = Object.FindObjectsOfType<EventSystem>(true);
            Assert.AreEqual(1, systems.Length, "expected exactly one EventSystem");
            Assert.NotNull(systems[0].GetComponent<InputSystemUIInputModule>());

            view.dialogOverlay.SetActive(true);
            DialogInputBinding.RowObject(view.dialogInput)?.SetActive(true);
            view.dialogInput.gameObject.SetActive(true);
            view.dialogInput.text = "NewFolder";
            view.dialogInput.interactable = true;

            if (view.dialogInput.textComponent != null)
                view.dialogInput.textComponent.raycastTarget = false;

            EventSystem.current.SetSelectedGameObject(view.dialogInput.gameObject);
            view.dialogInput.Select();
            view.dialogInput.ActivateInputField();
            yield return null;

            Assert.IsTrue(view.dialogInput.isFocused, "dialogInput should be focused");

            InputFieldInputSystemBridge.InjectForTest(view.dialogInput, 'T');
            InputFieldInputSystemBridge.InjectForTest(view.dialogInput, '1');

            Assert.IsTrue(
                view.dialogInput.text.EndsWith("T1"),
                $"expected text to end with T1, got '{view.dialogInput.text}'");
        }
    }
}
