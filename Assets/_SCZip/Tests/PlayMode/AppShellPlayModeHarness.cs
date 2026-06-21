using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SCZip.Core;
using SCZip.Domain;
using SCZip.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace SCZip.Tests.PlayMode
{
    internal static class AppShellPlayModeHarness
    {
        public static IEnumerator LoadMainSceneAndWait()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            UiEventSystemSetup.Ensure();
            _ = UnityMainThreadDispatcher.Instance;
        }

        public static IEnumerator LoadMainSceneWithFolderFixture()
        {
            yield return LoadMainSceneAndWait();
            FolderNavFixture.Prepare();

            var shell = RequireShell();
            shell.Browser.ReloadDirectorySyncForTests();
            AssertBrowserHasEntry(shell, "ChildFolder");
            shell.ForceSyncRefreshForTests();
            yield return null;
        }

        public static AppShellController RequireShell()
        {
            var shell = Object.FindObjectOfType<AppShellController>();
            Assert.NotNull(shell, "AppShellController missing — SCZipApp not in Main scene");
            Assert.NotNull(shell.Browser, "FileBrowserViewModel missing");
            return shell;
        }

        public static AppShellView RequireView()
        {
            var canvas = GameObject.Find("UICanvas");
            Assert.NotNull(canvas, "UICanvas missing");
            var view = canvas.GetComponent<AppShellView>();
            Assert.NotNull(view, "AppShellView missing");
            return view;
        }

        public static void AssertBrowserHasEntry(AppShellController shell, string entryName)
        {
            foreach (var item in shell.Browser.Items)
            {
                if (item.Name == entryName)
                    return;
            }

            var names = string.Join(", ", shell.Browser.Items.Select(i => i.Name));
            Assert.Fail($"Browser missing '{entryName}'. items=[{names}]");
        }

        public static FileListRowView RequireRow(AppShellView view, string entryName)
        {
            foreach (var row in view.fileListContent.GetComponentsInChildren<FileListRowView>(false))
            {
                if (row.nameText.text == entryName)
                    return row;
            }

            Assert.Fail($"File row '{entryName}' not found");
            return null;
        }

        public static IEnumerator ClickRowWithPointerFocus(FileListRowView row)
        {
            Assert.NotNull(row?.rowButton, "rowButton missing");

            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(row.rowButton.gameObject);

            row.rowButton.onClick.Invoke();
            yield return null;
        }

        public static void AssertRaycastHitsButton(Button button)
        {
            var canvas = button.GetComponentInParent<Canvas>();
            var es = EventSystem.current;
            Assert.NotNull(canvas);
            Assert.NotNull(es);

            var rt = button.GetComponent<RectTransform>();
            var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var screenPoint = RectTransformUtility.WorldToScreenPoint(cam, rt.TransformPoint(rt.rect.center));
            var data = new PointerEventData(es) { position = screenPoint };
            var hits = new System.Collections.Generic.List<RaycastResult>();
            canvas.GetComponent<GraphicRaycaster>().Raycast(data, hits);

            foreach (var hit in hits)
            {
                if (hit.gameObject == button.gameObject || hit.gameObject.transform.IsChildOf(button.transform))
                    return;
            }

            var names = string.Join(", ", hits.ConvertAll(h => h.gameObject.name));
            Assert.Fail($"Raycast at back button did not hit it. Top hits: [{names}]");
        }

        public static IEnumerator ClickButton(Button button)
        {
            Assert.NotNull(button, "button missing");
            Assert.IsTrue(button.gameObject.activeInHierarchy, "button must be active");
            Assert.IsTrue(button.interactable, "button must be interactable");

            var es = EventSystem.current;
            Assert.NotNull(es, "EventSystem missing");
            es.SetSelectedGameObject(button.gameObject);

            var rt = button.GetComponent<RectTransform>();
            var canvas = button.GetComponentInParent<Canvas>();
            Assert.NotNull(canvas, "button must live under a Canvas");

            var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var screenPoint = RectTransformUtility.WorldToScreenPoint(cam, rt.TransformPoint(rt.rect.center));
            var data = new PointerEventData(es)
            {
                position = screenPoint,
                pressPosition = screenPoint,
                button = PointerEventData.InputButton.Left,
                clickCount = 1,
                eligibleForClick = true
            };

            var raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
            {
                var hits = new System.Collections.Generic.List<RaycastResult>();
                raycaster.Raycast(data, hits);
                if (hits.Count > 0)
                    data.pointerCurrentRaycast = hits[0];
            }

            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
            yield return null;
        }

        public static IEnumerator WaitForDeferredFolderEnter(AppShellController shell, AppShellView view)
        {
            yield return null;
            yield return null;
            UnityMainThreadDispatcher.FlushPending();

            var deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline)
            {
                UnityMainThreadDispatcher.FlushPending();
                if (!shell.Browser.IsLoading && shell.Browser.CanGoBack)
                {
                    shell.ForceSyncRefreshForTests();
                    if (view.btnBack.gameObject.activeSelf)
                        yield break;
                }

                yield return null;
            }

            Assert.Fail("Timed out waiting for deferred folder-enter UI refresh");
        }

        public static IEnumerator WaitForDeferredNavigation(AppShellView view, bool backVisible)
        {
            yield return null;
            yield return null;
            UnityMainThreadDispatcher.FlushPending();

            var deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline)
            {
                UnityMainThreadDispatcher.FlushPending();
                if (view.btnBack.gameObject.activeSelf == backVisible &&
                    (view.loadingLabel == null || !view.loadingLabel.activeSelf))
                    yield break;

                yield return null;
            }

            Assert.Fail($"Timed out waiting for back button visible={backVisible}");
        }

        public static IEnumerator SyncShellUi(AppShellController shell)
        {
            shell.Browser.ReloadDirectorySyncForTests();
            UnityMainThreadDispatcher.FlushPending();
            shell.ForceSyncRefreshForTests();
            yield return null;
        }

        public static IEnumerator WaitForInitialLoad(AppShellController shell, AppShellView view)
        {
            yield return null;
            yield return null;
            UnityMainThreadDispatcher.FlushPending();
            shell.EnsureAppBarRaycastFixForTests();

            var deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline)
            {
                UnityMainThreadDispatcher.FlushPending();
                if (!shell.Browser.IsLoading &&
                    (view.loadingLabel == null || !view.loadingLabel.activeSelf))
                    yield break;

                yield return null;
            }

            Assert.Fail("Timed out waiting for initial Storage load");
        }

        public static IEnumerator WaitForRecentLoad(AppShellController shell, AppShellView view)
        {
            var deadline = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < deadline)
            {
                UnityMainThreadDispatcher.FlushPending();
                if (!shell.Browser.IsLoading &&
                    shell.Browser.CurrentSource == NavigationSource.Recent &&
                    (view.loadingLabel == null || !view.loadingLabel.activeSelf))
                {
                    shell.ForceSyncRefreshForTests();
                    yield break;
                }

                yield return null;
            }

            Assert.Fail("Timed out waiting for Recent async load");
        }

        public static void AssertAppBarButtonsReceiveRaycast(AppShellView view, AppShellController shell)
        {
            shell.EnsureAppBarRaycastFixForTests();
            AssertShellDoesNotBlockRaycasts(view);

            Assert.NotNull(view.btnMenu, "btnMenu missing");
            Assert.NotNull(view.btnBrowseFolder, "btnBrowseFolder missing");
            Assert.NotNull(view.btnSelectAll, "btnSelectAll missing");

            AssertRaycastHitsButton(view.btnMenu);
            AssertRaycastHitsButton(view.btnBrowseFolder);
            AssertRaycastHitsButton(view.btnSelectAll);
        }

        public static void AssertShellDoesNotBlockRaycasts(AppShellView view)
        {
            Assert.NotNull(view.shell, "shell missing");
            var shellImg = view.shell.GetComponent<Image>();
            Assert.NotNull(shellImg, "shell Image missing");
            Assert.IsFalse(shellImg.raycastTarget,
                "Shell background must not intercept AppBar clicks — call EnsureAppBarRaycastFix");
        }
    }

    internal static class FolderNavFixture
    {
        private const string FolderName = "ChildFolder";
        private const string MarkerFile = "_playmode_nav.txt";

        public static void Prepare()
        {
            AppServices.EnsureInitialized();
            var root = AppServices.FileSystem.StorageRoot;
            var child = Path.Combine(root, FolderName);

            if (Directory.Exists(child))
                Directory.Delete(child, true);

            Directory.CreateDirectory(child);
            File.WriteAllText(Path.Combine(root, MarkerFile), "playmode");
        }

        public static void Cleanup()
        {
            AppServices.EnsureInitialized();
            var root = AppServices.FileSystem.StorageRoot;
            var child = Path.Combine(root, FolderName);
            var marker = Path.Combine(root, MarkerFile);

            if (Directory.Exists(child))
                Directory.Delete(child, true);
            if (File.Exists(marker))
                File.Delete(marker);
        }
    }
}
