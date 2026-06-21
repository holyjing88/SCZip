using System.Collections;
using NUnit.Framework;
using SCZip.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace SCZip.Tests.PlayMode
{
    public sealed class FolderNavigationPlayModeTests
    {
        [TearDown]
        public void TearDown() => FolderNavFixture.Cleanup();

        [UnityTest]
        public IEnumerator Menu_button_opens_drawer_above_shell()
        {
            yield return AppShellPlayModeHarness.LoadMainSceneAndWait();

            var view = AppShellPlayModeHarness.RequireView();
            var shell = AppShellPlayModeHarness.RequireShell();
            shell.EnsureAppBarRaycastFixForTests();
            shell.EnsureDrawerLayoutForTests();

            Assert.IsFalse(view.drawerOverlay.activeSelf);
            AppShellPlayModeHarness.AssertRaycastHitsButton(view.btnMenu);
            yield return AppShellPlayModeHarness.ClickButton(view.btnMenu);

            Assert.IsTrue(view.drawerOverlay.activeSelf, "drawer overlay should show");
            Assert.AreEqual(0f, view.drawer.anchoredPosition.x, 0.01f, "drawer should slide in");
            Assert.Greater(
                view.drawer.GetSiblingIndex(),
                view.shell.transform.GetSiblingIndex(),
                "drawer must render above shell");
            var navLabel = view.navStorage.GetComponentInChildren<Text>();
            Assert.NotNull(navLabel);
            Assert.AreEqual("Storage", navLabel.text);
            Assert.Greater(navLabel.rectTransform.rect.height, 1f, "nav label needs visible height");
        }

        [UnityTest]
        public IEnumerator Enter_folder_rebuilds_list_and_enables_back_without_errors()
        {
            yield return AppShellPlayModeHarness.LoadMainSceneWithFolderFixture();

            var view = AppShellPlayModeHarness.RequireView();
            var shell = AppShellPlayModeHarness.RequireShell();
            var childRow = AppShellPlayModeHarness.RequireRow(view, "ChildFolder");

            yield return AppShellPlayModeHarness.ClickRowWithPointerFocus(childRow);
            yield return AppShellPlayModeHarness.WaitForDeferredFolderEnter(shell, view);

            Assert.IsTrue(view.btnBack.gameObject.activeSelf, "back button should appear after entering folder");
            Assert.IsTrue(
                view.breadcrumb.text.Contains("ChildFolder"),
                $"breadcrumb should contain ChildFolder, got '{view.breadcrumb.text}'");
        }

        [UnityTest]
        public IEnumerator Enter_and_back_navigation_keeps_event_system_stable()
        {
            yield return AppShellPlayModeHarness.LoadMainSceneWithFolderFixture();

            var view = AppShellPlayModeHarness.RequireView();
            var shell = AppShellPlayModeHarness.RequireShell();
            var childRow = AppShellPlayModeHarness.RequireRow(view, "ChildFolder");

            yield return AppShellPlayModeHarness.ClickRowWithPointerFocus(childRow);
            yield return AppShellPlayModeHarness.WaitForDeferredFolderEnter(shell, view);

            shell.EnsureAppBarRaycastFixForTests();
            AppShellPlayModeHarness.AssertRaycastHitsButton(view.btnBack);
            yield return AppShellPlayModeHarness.ClickButton(view.btnBack);
            yield return AppShellPlayModeHarness.SyncShellUi(shell);

            Assert.IsFalse(shell.Browser.CanGoBack, "navigation stack should be at root after back");
            Assert.IsFalse(view.btnBack.gameObject.activeSelf, "back button should hide at root");
            Assert.IsFalse(
                view.breadcrumb.text.Contains("ChildFolder"),
                $"breadcrumb should be back at Storage, got '{view.breadcrumb.text}'");
            AppShellPlayModeHarness.RequireRow(view, "ChildFolder");
        }

        [UnityTest]
        public IEnumerator Deferred_clear_children_does_not_break_input_module()
        {
            yield return AppShellPlayModeHarness.LoadMainSceneWithFolderFixture();

            var view = AppShellPlayModeHarness.RequireView();
            var row = AppShellPlayModeHarness.RequireRow(view, "ChildFolder");
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(row.rowButton.gameObject);

            UiEventSystemSetup.PrepareForHierarchyRebuild(view.fileListContent);
            for (var i = view.fileListContent.childCount - 1; i >= 0; i--)
                Object.Destroy(view.fileListContent.GetChild(i).gameObject);

            UiEventSystemSetup.ClearInputSystemPointerState();
            yield return null;
            yield return null;

            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }
    }
}
