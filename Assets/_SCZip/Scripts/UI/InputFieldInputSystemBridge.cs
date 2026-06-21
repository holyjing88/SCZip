using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SCZip.UI
{
    /// <summary>
    /// uGUI InputField relies on IMGUI keyboard events; with Input System-only projects those never arrive.
    /// Forwards <see cref="Keyboard"/> text and editing keys into <see cref="InputField.ProcessEvent"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(InputField))]
    public sealed class InputFieldInputSystemBridge : MonoBehaviour
    {
        private InputField _field;
        private bool _hooked;

        private void Awake() => _field = GetComponent<InputField>();

        public static void EnsureOn(InputField field)
        {
            if (field == null)
                return;

            if (field.GetComponent<InputFieldInputSystemBridge>() == null)
                field.gameObject.AddComponent<InputFieldInputSystemBridge>();

            if (field.textComponent != null)
                field.textComponent.raycastTarget = false;

            if (field.placeholder != null)
                field.placeholder.raycastTarget = false;

            var img = field.GetComponent<Image>();
            if (img != null)
            {
                if (field.targetGraphic == null)
                    field.targetGraphic = img;
                img.raycastTarget = true;
            }
        }

        private void OnDisable() => Unhook();

        private void LateUpdate()
        {
            if (_field == null)
                return;

            var keyboard = Keyboard.current;
            if (keyboard == null || !_field.isFocused || !_field.interactable)
            {
                Unhook();
                return;
            }

            if (!_hooked)
            {
                keyboard.onTextInput += OnTextInput;
                _hooked = true;
            }

            if (keyboard.backspaceKey.wasPressedThisFrame)
                ForwardKey(KeyCode.Backspace);
            if (keyboard.deleteKey.wasPressedThisFrame)
                ForwardKey(KeyCode.Delete);
            if (keyboard.leftArrowKey.wasPressedThisFrame)
                ForwardKey(KeyCode.LeftArrow, keyboard.leftShiftKey.isPressed);
            if (keyboard.rightArrowKey.wasPressedThisFrame)
                ForwardKey(KeyCode.RightArrow, keyboard.leftShiftKey.isPressed);
            if (keyboard.homeKey.wasPressedThisFrame)
                ForwardKey(KeyCode.Home, keyboard.leftShiftKey.isPressed);
            if (keyboard.endKey.wasPressedThisFrame)
                ForwardKey(KeyCode.End, keyboard.leftShiftKey.isPressed);
        }

        private void OnTextInput(char character)
        {
            if (_field == null || !_field.isFocused || !_field.interactable)
                return;

            ForwardCharacter(character);
        }

        private void ForwardCharacter(char character)
        {
            var evt = new Event
            {
                type = EventType.KeyDown,
                character = character
            };
            _field.ProcessEvent(evt);
        }

        private void ForwardKey(KeyCode keyCode, bool shift = false)
        {
            var evt = new Event
            {
                type = EventType.KeyDown,
                keyCode = keyCode,
                modifiers = shift ? EventModifiers.Shift : EventModifiers.None
            };
            _field.ProcessEvent(evt);
        }

        private void Unhook()
        {
            if (!_hooked)
                return;

            var keyboard = Keyboard.current;
            if (keyboard != null)
                keyboard.onTextInput -= OnTextInput;

            _hooked = false;
        }

        /// <summary>Used by automated play-mode acceptance tests.</summary>
        public static void InjectForTest(InputField field, char character)
        {
            if (field == null)
                return;

            var bridge = field.GetComponent<InputFieldInputSystemBridge>();
            if (bridge != null)
                bridge.ForwardCharacter(character);
            else
            {
                var evt = new Event { type = EventType.KeyDown, character = character };
                field.ProcessEvent(evt);
            }
        }
    }
}
