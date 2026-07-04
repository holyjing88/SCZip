using System.Collections;
using NUnit.Framework;
using SCZip.Core;
using SCZip.Domain;
using SCZip.UI;
using SCZip.ViewModels;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace SCZip.Tests.PlayMode
{
    /// <summary>
    /// Regression: AppBar buttons must stay clickable (no main-thread blocking / raycast traps).
    /// </summary>
    public sealed class AppBarInteractionPlayModeTests
    {
        [UnityTest]
        public IEnumerator Startup_shows_executable_directory()
        {
            yield return AppShellPlayModeHarness.LoadMainSceneAndWait();

            var view = AppShellPlayModeHarness.RequireView();
            var shell = AppShellPlayModeHarness.RequireShell();
            yield return AppShellPlayModeHarness.WaitForInitialLoad(shell, view);

            Assert.AreEqual(AppServices.FileSystem.ExecutableDirectory, shell.Browser.CurrentFolderPath);
            Assert.Greater(shell.Browser.Items.Count, 0, "Startup folder should list at least one entry");
            Assert.Greater(view.fileListContent.childCount, 0, "Startup rows should be painted in the file list");
        }

        [UnityTest]
        public IEnumerator Select_all_shows_active_state_after_click()
        {
            yield return AppShellPlayModeHarness.LoadMainSceneWithFolderFixture();

            var view = AppShellPlayModeHarness.RequireView();
            var shell = AppShellPlayModeHarness.RequireShell();
            yield return AppShellPlayModeHarness.WaitForInitialLoad(shell, view);

            yield return AppShellPlayModeHarness.ClickButton(view.btnSelectAll);
            UnityMainThreadDispatcher.FlushPending();
            shell.ForceSyncRefreshForTests();
            yield return null;

            Assert.AreEqual(SelectAllState.All, shell.Browser.SelectAllState);
            Assert.AreEqual(AppBarSelectAllVisual.LabelAll,
                AppBarSelectAllVisual.GetIconText(view.btnSelectAll).text);
            Assert.Greater(shell.GetSelectAllHighlightAlphaForTests(), 0.2f,
                "Select-all highlight should stay visible while all items are selected");
        }

        [UnityTest]
        public IEnumerator Select_all_button_clears_on_folder_enter()
        {
            yield return AppShellPlayModeHarness.LoadMainSceneWithFolderFixture();

            var view = AppShellPlayModeHarness.RequireView();
            var shell = AppShellPlayModeHarness.RequireShell();

            yield return AppShellPlayModeHarness.ClickButton(view.btnSelectAll);
            Assert.AreEqual(SelectAllState.All, shell.Browser.SelectAllState);
            Assert.AreEqual(AppBarSelectAllVisual.LabelAll,
                AppBarSelectAllVisual.GetIconText(view.btnSelectAll).text);
            Assert.Greater(shell.GetSelectAllHighlightAlphaForTests(), 0.2f);

            var row = AppShellPlayModeHarness.RequireRow(view, "ChildFolder");
            yield return AppShellPlayModeHarness.ClickRowWithPointerFocus(row);
            yield return AppShellPlayModeHarness.WaitForDeferredFolderEnter(shell, view);

            Assert.AreEqual(SelectAllState.None, shell.Browser.SelectAllState);
            var label = AppBarSelectAllVisual.GetIconText(view.btnSelectAll);
            Assert.AreEqual(AppBarSelectAllVisual.LabelNone, label.text);
            Assert.Less(shell.GetSelectAllHighlightAlphaForTests(), 0.05f,
                "Select-all highlight should clear after folder enter");

            shell.ForceSyncRefreshForTests();
            var btn = view.btnSelectAll;
            Assert.AreEqual(btn.colors.normalColor, btn.targetGraphic.color,
                "Select-all button should not keep Selected tint after folder enter");
        }

        [UnityTest]
        public IEnumerator App_bar_buttons_receive_clicks_after_play_starts()
        {
            yield return AppShellPlayModeHarness.LoadMainSceneAndWait();

            var view = AppShellPlayModeHarness.RequireView();
            var shell = AppShellPlayModeHarness.RequireShell();
            yield return AppShellPlayModeHarness.WaitForInitialLoad(shell, view);

            AppShellPlayModeHarness.AssertAppBarButtonsReceiveRaycast(view, shell);
        }

        [UnityTest]
        public IEnumerator Menu_button_opens_drawer_after_app_bar_raycast_fix()
        {
            yield return AppShellPlayModeHarness.LoadMainSceneAndWait();

            var view = AppShellPlayModeHarness.RequireView();
            var shell = AppShellPlayModeHarness.RequireShell();
            yield return AppShellPlayModeHarness.WaitForInitialLoad(shell, view);

            AppShellPlayModeHarness.AssertRaycastHitsButton(view.btnMenu);
            yield return AppShellPlayModeHarness.ClickButton(view.btnMenu);

            Assert.IsTrue(view.drawerOverlay.activeSelf, "menu button should open drawer");
        }

        [UnityTest]
        public IEnumerator Recent_navigation_keeps_app_bar_clickable_and_clears_loading()
        {
            yield return AppShellPlayModeHarness.LoadMainSceneAndWait();

            var view = AppShellPlayModeHarness.RequireView();
            var shell = AppShellPlayModeHarness.RequireShell();
            yield return AppShellPlayModeHarness.WaitForInitialLoad(shell, view);

            shell.Browser.ShowRecent();
            yield return AppShellPlayModeHarness.WaitForRecentLoad(shell, view);

            Assert.IsFalse(shell.Browser.IsLoading, "Recent load must clear IsLoading");
            Assert.IsFalse(view.loadingLabel.activeSelf, "loading overlay must hide after Recent");
            Assert.AreEqual(NavigationSource.Recent, shell.Browser.CurrentSource);
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            Assert.Greater(shell.Browser.Items.Count, 0,
                "Recent should include macOS system recents (mdfind query)");
            Assert.Greater(view.fileListContent.childCount, 0, "Recent rows should be painted");
#endif
            AppShellPlayModeHarness.AssertAppBarButtonsReceiveRaycast(view, shell);
        }

        [UnityTest]
        public IEnumerator GetRecent_on_main_thread_fails_fast_in_editor()
        {
            yield return AppShellPlayModeHarness.LoadMainSceneAndWait();
            MainThreadGuard.CaptureMainThread();

            Assert.Throws<System.InvalidOperationException>(() => AppServices.Recent.GetRecent());
        }
    }
}
