using System.Collections;
using System.Linq;
using NUnit.Framework;
using SCZip.Core;
using SCZip.Domain;
using SCZip.Infrastructure;
using SCZip.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace SCZip.Tests.PlayMode
{
    public sealed class CreateArchiveFormatPlayModeTests
    {
        [UnityTest]
        public IEnumerator Format_dropdown_switches_to_tar_gz_without_input_system_error()
        {
            yield return AppShellPlayModeHarness.LoadMainSceneAndWait();

            var view = AppShellPlayModeHarness.RequireView();
            Assert.NotNull(view.dialogFormat, "dialogFormat missing");

            var creatable = AppServices.Archive.GetCreatableFormats();
            var labels = creatable.Select(ArchiveFormatRegistry.GetDisplayLabel).ToArray();
            var tarGzIndex = creatable.ToList().IndexOf(ArchiveFormat.TarGzip);
            Assert.GreaterOrEqual(tarGzIndex, 0, "TAR.GZ should be creatable");

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var dropdown = SafeDropdown.Migrate(view.dialogFormat);
            UiDropdownBuilder.Ensure(dropdown, font, labels);
            DropdownInputSystemBridge.EnsureOn(dropdown);
            dropdown.alphaFadeSpeed = 0f;

            dropdown.gameObject.SetActive(true);
            dropdown.SetValueWithoutNotify(0);
            dropdown.RefreshShownValue();
            view.dialogInput.text = "archive_test.zip";
            yield return null;

            dropdown.value = tarGzIndex;
            yield return null;
            yield return null;

            UiEventSystemSetup.SanitizeStaleHoverTargets();

            if (AppServices.FeatureGate.CanCreate(ArchiveFormat.TarGzip))
            {
                Assert.AreEqual(tarGzIndex, dropdown.value);
                Assert.IsTrue(view.dialogInput.text.EndsWith(".tar.gz"),
                    $"expected .tar.gz extension, got '{view.dialogInput.text}'");
            }
            else
            {
                Assert.AreEqual(0, dropdown.value);
            }
        }

        [UnityTest]
        public IEnumerator Compress_dialog_shows_format_dropdown()
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
            Assert.IsTrue(view.dialogFormat.gameObject.activeSelf, "format dropdown should be visible");
            Assert.IsTrue(view.dialogFormat is SafeDropdown, "dropdown should migrate to SafeDropdown");

            var creatable = AppServices.Archive.GetCreatableFormats();
            if (creatable.Count > 1)
            {
                view.dialogFormat.value = 1;
                yield return null;
                yield return null;
                UiEventSystemSetup.SanitizeStaleHoverTargets();
                Assert.AreNotEqual(-1, view.dialogFormat.value);
            }
        }
    }
}
