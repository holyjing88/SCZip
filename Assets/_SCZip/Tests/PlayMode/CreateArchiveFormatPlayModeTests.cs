using System.Collections;
using NUnit.Framework;
using SCZip.Core;
using SCZip.Domain;
using SCZip.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace SCZip.Tests.PlayMode
{
    public sealed class CreateArchiveFormatPlayModeTests
    {
        [UnityTest]
        public IEnumerator Format_selector_switches_to_tar_gz_without_input_system_error()
        {
            yield return AppShellPlayModeHarness.LoadMainSceneAndWait();

            var view = AppShellPlayModeHarness.RequireView();
            Assert.NotNull(view.dialogFormat, "legacy dialogFormat should exist in Main scene");

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var selector = ArchiveFormatSelector.MigrateFromDropdown(view.dialogFormat, font);
            Assert.NotNull(selector);

            selector.gameObject.SetActive(true);
            selector.SetFormat(ArchiveFormat.Zip, false);
            view.dialogInput.text = "archive_test.zip";
            yield return null;

            Assert.NotNull(selector.TarGzButton, "TAR.GZ button missing");
            selector.TarGzButton.onClick.Invoke();
            yield return null;
            yield return null;

            UiEventSystemSetup.SanitizeStaleHoverTargets();

            if (AppServices.FeatureGate.CanCreate(ArchiveFormat.TarGzip))
            {
                Assert.AreEqual(ArchiveFormat.TarGzip, selector.SelectedFormat);
                Assert.IsTrue(view.dialogInput.text.EndsWith(".tar.gz"),
                    $"expected .tar.gz extension, got '{view.dialogInput.text}'");
            }
            else
            {
                Assert.AreEqual(ArchiveFormat.Zip, selector.SelectedFormat);
                Assert.IsTrue(view.dialogInput.text.EndsWith(".zip"),
                    $"expected .zip when Pro disabled, got '{view.dialogInput.text}'");
            }
        }

        [UnityTest]
        public IEnumerator Compress_dialog_uses_format_selector_not_dropdown()
        {
            yield return AppShellPlayModeHarness.LoadMainSceneWithFolderFixture();

            var shell = AppShellPlayModeHarness.RequireShell();
            var view = AppShellPlayModeHarness.RequireView();
            yield return AppShellPlayModeHarness.WaitForInitialLoad(shell, view);

            var row = AppShellPlayModeHarness.RequireRow(view, "ChildFolder");
            row.checkButton.onClick.Invoke();
            yield return null;

            view.actCompress.onClick.Invoke();
            yield return null;

            Assert.IsTrue(view.pathPicker.activeSelf, "path picker should open");
            view.pickerConfirm.onClick.Invoke();
            yield return null;

            Assert.IsTrue(view.dialogOverlay.activeSelf, "create archive dialog should open");
            Assert.IsFalse(view.dialogFormat.gameObject.activeSelf, "legacy dropdown must stay hidden");

            var selector = view.dialogFormatSelector;
            Assert.NotNull(selector, "ArchiveFormatSelector should be created at runtime");
            Assert.IsTrue(selector.gameObject.activeSelf, "format selector should be visible");

            selector.TarGzButton.onClick.Invoke();
            yield return null;
            yield return null;

            UiEventSystemSetup.SanitizeStaleHoverTargets();
            Assert.IsNotNull(selector.SelectedFormat);
        }
    }
}
