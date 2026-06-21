#if UNITY_EDITOR
using System.IO;
using System.Reflection;
using SCZip.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SCZip.Editor
{
    [InitializeOnLoad]
    internal static class PlayModeSmokeHooks
    {
        private const string PendingKey = "SCZip_PlayModeSmokePending";
        private static readonly string ResultPath =
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "playmode-result.txt"));
        private static int _frames;
        private static bool _armedThisSession;

        static PlayModeSmokeHooks()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorPrefs.DeleteKey(PendingKey);
        }

        public static void Arm()
        {
            EditorSceneManager.OpenScene("Assets/_SCZip/Scenes/Main.unity", OpenSceneMode.Single);

            var resultPath = ResultPath;
            var dir = Path.GetDirectoryName(resultPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            if (File.Exists(resultPath))
                File.Delete(resultPath);

            _frames = 0;
            _armedThisSession = true;
            EditorPrefs.SetBool(PendingKey, true);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (!_armedThisSession || !EditorPrefs.GetBool(PendingKey, false))
                return;

            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                _frames = 0;
                EditorApplication.update += PollPlayMode;
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update -= PollPlayMode;
                _armedThisSession = false;
                EditorPrefs.SetBool(PendingKey, false);
                FinishAndExit();
            }
        }

        private static void PollPlayMode()
        {
            if (!EditorApplication.isPlaying)
                return;

            _frames++;
            if (_frames < 30)
                return;

            EditorApplication.update -= PollPlayMode;

            try
            {
                var canvas = GameObject.Find("UICanvas");
                if (canvas == null)
                    throw new System.Exception("UICanvas missing");

                var view = canvas.GetComponent<AppShellView>();
                if (view == null)
                    throw new System.Exception("AppShellView missing on UICanvas");

                var canvasComp = canvas.GetComponent<Canvas>();
                if (canvasComp == null)
                    throw new System.Exception("Canvas component missing");

                if (view.fileListContent == null)
                    throw new System.Exception("fileListContent missing");
                if (view.titleLabel == null)
                    throw new System.Exception("titleLabel missing");
                if (view.shell == null || !view.shell.activeSelf)
                    throw new System.Exception("shell should be visible on start");
                if (view.settingsPanel != null && view.settingsPanel.activeSelf)
                    throw new System.Exception("settings-panel should be hidden on start");

                ValidateEventSystem();
                ValidateDialogInput(view);

                File.WriteAllText(ResultPath, "PASS");
                Debug.Log("[SCZip] PLAYMODE_PASS uGUI + dialog input OK");
            }
            catch (System.Exception ex)
            {
                File.WriteAllText(ResultPath, "FAIL: " + ex.Message);
                Debug.LogError("[SCZip] PLAYMODE_FAIL: " + ex.Message);
            }

            EditorApplication.isPlaying = false;
        }

        private static void ValidateEventSystem()
        {
            var systems = Object.FindObjectsOfType<EventSystem>(true);
            if (systems.Length != 1)
                throw new System.Exception($"expected 1 EventSystem, found {systems.Length}");

            if (systems[0].GetComponent<InputSystemUIInputModule>() == null)
                throw new System.Exception("InputSystemUIInputModule missing");
        }

        private static void ValidateDialogInput(AppShellView view)
        {
            DialogInputBinding.Resolve(view);

            if (view.dialogInput == null)
                throw new System.Exception("dialogInput missing");

            if (view.dialogInput.gameObject.name != "Input")
                throw new System.Exception("dialogInput should be DialogInput/Input");

            if (view.dialogInput.GetComponent<InputFieldInputSystemBridge>() == null)
                InputFieldInputSystemBridge.EnsureOn(view.dialogInput);

            view.dialogOverlay.SetActive(true);
            var row = DialogInputBinding.RowObject(view.dialogInput);
            if (row != null)
                row.SetActive(true);
            view.dialogInput.gameObject.SetActive(true);
            view.dialogInput.text = "NewFolder";
            view.dialogInput.interactable = true;

            if (view.dialogInput.textComponent != null)
                view.dialogInput.textComponent.raycastTarget = false;

            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(view.dialogInput.gameObject);

            view.dialogInput.Select();
            view.dialogInput.ActivateInputField();

            if (!view.dialogInput.isFocused)
                throw new System.Exception("dialogInput failed to focus");

            InputFieldInputSystemBridge.InjectForTest(view.dialogInput, 'X');
            InputFieldInputSystemBridge.InjectForTest(view.dialogInput, 'Y');

            if (!view.dialogInput.text.EndsWith("XY"))
                throw new System.Exception(
                    $"dialogInput text not updated after inject, got '{view.dialogInput.text}'");

            view.dialogOverlay.SetActive(false);
        }

        private static void FinishAndExit()
        {
            var resultPath = ResultPath;
            var pass = File.Exists(resultPath) && File.ReadAllText(resultPath).StartsWith("PASS");

            if (pass)
                Debug.Log("[SCZip] PLAYMODE_PASS confirmed");
            else
                Debug.LogError("[SCZip] PLAYMODE_FAIL: " +
                               (File.Exists(resultPath) ? File.ReadAllText(resultPath) : "no result file"));

            EditorApplication.Exit(pass ? 0 : 1);
        }
    }

    public static class PlayModeSmokeRunner
    {
        public static void Run()
        {
            PlayModeSmokeHooks.Arm();
            EditorApplication.EnterPlaymode();
        }
    }
}
#endif
