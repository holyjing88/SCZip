using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace SCZip.UI
{
    /// <summary>
    /// Project uses Input System package — keep a single EventSystem with InputSystemUIInputModule.
    /// </summary>
    public static class UiEventSystemSetup
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AfterSceneLoad() => Ensure();

        public static void Ensure()
        {
            var systems = UnityEngine.Object.FindObjectsOfType<EventSystem>(true);
            var primary = PickPrimary(systems);

            if (primary == null)
            {
                var go = new GameObject("EventSystem");
                primary = go.AddComponent<EventSystem>();
            }

            for (var i = 0; i < systems.Length; i++)
            {
                var candidate = systems[i];
                if (candidate == null || candidate == primary)
                    continue;

                DestroyObject(candidate.gameObject);
            }

            ConfigureModules(primary);
        }

        private static EventSystem PickPrimary(EventSystem[] systems)
        {
            if (systems == null || systems.Length == 0)
                return null;

#if ENABLE_INPUT_SYSTEM
            foreach (var system in systems)
            {
                if (system != null && system.GetComponent<InputSystemUIInputModule>() != null)
                    return system;
            }
#endif

            if (EventSystem.current != null)
                return EventSystem.current;

            return systems[0];
        }

        private static void ConfigureModules(EventSystem es)
        {
#if ENABLE_INPUT_SYSTEM
            var legacy = es.GetComponent<StandaloneInputModule>();
            if (legacy != null)
                DestroyObject(legacy);

            if (es.GetComponent<InputSystemUIInputModule>() == null)
                es.gameObject.AddComponent<InputSystemUIInputModule>();

            var module = es.GetComponent<InputSystemUIInputModule>();
            if (module != null && module.actionsAsset == null)
                module.AssignDefaultActions();
#else
            if (es.GetComponent<StandaloneInputModule>() == null)
                es.gameObject.AddComponent<StandaloneInputModule>();
#endif
        }

        private static void DestroyObject(UnityEngine.Object obj)
        {
            if (obj == null)
                return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(obj);
            else
                UnityEngine.Object.DestroyImmediate(obj);
        }

        public static void PrepareForHierarchyRebuild(Transform root)
        {
            // Drop stale hover/press refs before deactivating or destroying rows under the pointer.
            ClearUiSelection();

            if (root != null)
            {
                for (var i = 0; i < root.childCount; i++)
                {
                    var child = root.GetChild(i).gameObject;
                    if (child != null && child.activeSelf)
                        child.SetActive(false);
                }
            }
        }

        /// <summary>Clears EventSystem selection and stale pointer press/hover state.</summary>
        public static void ClearUiSelection()
        {
            ClearInputSystemPointerState();

            var es = EventSystem.current;
            if (es != null)
                es.SetSelectedGameObject(null);
        }

        /// <summary>
        /// InputSystemUIInputModule keeps pointerEnter/hovered across frames; clear before destroying UI under the pointer.
        /// </summary>
        public static void ClearInputSystemPointerState()
        {
#if ENABLE_INPUT_SYSTEM
            var es = EventSystem.current;
            if (es == null)
                return;

            var module = es.GetComponent<InputSystemUIInputModule>();
            if (module == null || !module.enabled)
                return;

            try
            {
                WipePointerHoverState(module);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SCZip] Failed to reset UI pointers: {ex.Message}");
            }
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static void WipePointerHoverState(InputSystemUIInputModule module)
        {
            var statesField = typeof(InputSystemUIInputModule).GetField(
                "m_PointerStates", BindingFlags.Instance | BindingFlags.NonPublic);
            if (statesField == null)
                return;

            var states = statesField.GetValue(module);
            if (states == null)
                return;

            var statesType = states.GetType();
            var lengthField = statesType.GetField("length");
            var firstValueField = statesType.GetField("firstValue");
            if (lengthField == null || firstValueField == null)
                return;

            var count = (int)lengthField.GetValue(states);
            var additionalField = statesType.GetField("additionalValues");
            var additional = additionalField?.GetValue(states) as System.Array;

            for (var i = 0; i < count; i++)
            {
                var model = i == 0 ? firstValueField.GetValue(states) : additional?.GetValue(i - 1);
                if (model == null)
                    continue;

                var modelType = model.GetType();
                WipeButtonState(modelType.GetField("leftButton")?.GetValue(model));
                WipeButtonState(modelType.GetField("rightButton")?.GetValue(model));
                WipeButtonState(modelType.GetField("middleButton")?.GetValue(model));

                if (modelType.GetField("eventData")?.GetValue(model) is not PointerEventData eventData)
                    continue;

                for (var h = eventData.hovered.Count - 1; h >= 0; h--)
                {
                    if (!eventData.hovered[h])
                        eventData.hovered.RemoveAt(h);
                }

                eventData.hovered.Clear();
                eventData.pointerEnter = null;
                eventData.pointerPress = null;
                eventData.pointerDrag = null;
                eventData.rawPointerPress = null;
                eventData.pointerCurrentRaycast = default;
                eventData.pointerPressRaycast = default;
            }
        }

        private static void WipeButtonState(object buttonState)
        {
            if (buttonState == null)
                return;

            var t = buttonState.GetType();
            foreach (var fieldName in new[]
                     {
                         "m_PressObject", "m_RawPressObject", "m_LastPressObject", "m_DragObject"
                     })
            {
                t.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(buttonState, null);
            }

            t.GetField("m_Dragging", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(buttonState, false);
        }
#endif
    }
}
