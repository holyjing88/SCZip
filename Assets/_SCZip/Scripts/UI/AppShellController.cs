using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SCZip.Core;
using SCZip.Domain;
using SCZip.Infrastructure;
using SCZip.ViewModels;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SCZip.UI
{
    public sealed class AppShellController : MonoBehaviour
    {
        [SerializeField] private AppShellView _view;

        private FileBrowserViewModel _vm;

        public FileBrowserViewModel Browser => _vm;
        private bool _drawerOpen;
        private PathPickerMode _pickerMode;
        private string _pickerPath;
        private Action _dialogOkAction;
        private Font _font;
        private Coroutine _fileListRebuildCoroutine;
        private readonly Dictionary<NavigationSource, Button> _navButtons = new();

        private enum PathPickerMode { None, CompressDestination, ExtractDestination, BrowseFolder }

        private const float AppBarHeight = 56f;
        private const float BreadcrumbHeight = 40f;
        private const float ActionBarHeight = 56f;
        private const float PickerQuickRootsHeight = 44f;
        private const float PickerBottomBarHeight = 80f;
        private const float DrawerWidth = 280f;
        private static readonly Color AppBarPrimary = new(0.082f, 0.396f, 0.753f, 1f);
        private static readonly Color AppBarHitTargetColor = new(1f, 1f, 1f, 0.01f);
        private static readonly Color FileCheckIdleBg = new(0.94f, 0.94f, 0.94f, 1f);
        private static readonly Color FileCheckSelectedBg = new(0.82f, 0.91f, 1f, 1f);
        private static readonly Color FileCheckMarkColor = new(0.082f, 0.396f, 0.753f, 1f);

        private void Awake()
        {
            AppServices.EnsureInitialized();
            if (_view == null)
                _view = FindObjectOfType<AppShellView>();

            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _vm = new FileBrowserViewModel();
            _vm.StateChanged += RefreshUi;
        }

        private void Start()
        {
            if (_view == null)
            {
                Debug.LogError("[SCZip] AppShellView missing — run SCZip/Setup Main Scene");
                return;
            }

            CacheNavButtons();
            ResolveMissingViewRefs();
            EnsureAppBarRaycastFix();
            EnsureShellChromeLayout();
            EnsureDrawerLayout();
            WireEvents();
            EnsureInitialOverlayState();
            _vm.NavigateToSource(NavigationSource.Storage);
            UpdateDrawerHighlight(NavigationSource.Storage);
            StartCoroutine(EnsureInitialListPainted());
        }

        private IEnumerator EnsureInitialListPainted()
        {
            yield return null;
            UnityMainThreadDispatcher.FlushPending();

            var deadline = Time.realtimeSinceStartup + 5f;
            while (_vm.IsLoading && Time.realtimeSinceStartup < deadline)
            {
                UnityMainThreadDispatcher.FlushPending();
                yield return null;
            }

            UnityMainThreadDispatcher.FlushPending();
            ForceSyncRefreshForTests();
        }

        private void ResolveMissingViewRefs()
        {
            ResolveDialogInputRef();

            if (_view.pickerConfirmLabel == null && _view.pickerConfirm != null)
                _view.pickerConfirmLabel = _view.pickerConfirm.GetComponentInChildren<Text>();

            if (_view.btnBrowseFolder == null && _view.shell != null)
            {
                var appBar = _view.shell.transform.Find("AppBar");
                if (appBar != null)
                {
                    foreach (Transform child in appBar)
                    {
                        var btn = child.GetComponent<Button>();
                        var label = child.GetComponentInChildren<Text>();
                        if (btn == null || label == null) continue;

                        if (_view.btnBrowseFolder == null &&
                            (label.text == AppBarActionButtonVisual.IconBrowse ||
                             label.text == "📁" ||
                             label.text == "DIR"))
                            _view.btnBrowseFolder = btn;
                        if (_view.btnBack == null && label.text == "←")
                            _view.btnBack = btn;
                    }
                }
            }
        }

        private void ResolveDialogInputRef() => DialogInputBinding.Resolve(_view);

        private void SetDialogInputVisible(bool visible)
        {
            if (_view.dialogInput == null)
                return;

            SetVisible(DialogInputBinding.RowObject(_view.dialogInput), visible);
        }

        private void CacheNavButtons()
        {
            _navButtons[NavigationSource.Recent] = _view.navRecent;
            _navButtons[NavigationSource.MyFiles] = _view.navMyFiles;
            _navButtons[NavigationSource.Storage] = _view.navStorage;
            _navButtons[NavigationSource.Photos] = _view.navPhotos;
            _navButtons[NavigationSource.Music] = _view.navMusic;
        }

        private void EnsureInitialOverlayState()
        {
            SetVisible(_view.shell, true);
            SetVisible(_view.settingsPanel, false);
            SetVisible(_view.pathPicker, false);
            SetVisible(_view.dialogOverlay, false);
            SetVisible(_view.toast, false);
            SetVisible(_view.drawerOverlay, false);
            SetVisible(_view.actionBar, false);
            SetVisible(_view.emptyLabel, false);
            SetVisible(_view.errorLabel, false);
            SetVisible(_view.loadingLabel, false);
            SetLoadingOverlayRaycast(false);
            CloseDrawerImmediate();
            SetOverlayRaycast(false);
            RestoreDrawerLayering();
        }

        private void WireEvents()
        {
            if (_view.btnMenu != null)
                _view.btnMenu.onClick.AddListener(ToggleDrawer);
            else
                Debug.LogWarning("[SCZip] btnMenu missing — run SCZip/Setup Main Scene");

            if (_view.btnBack != null)
                _view.btnBack.onClick.AddListener(OnGoBack);

            WireBreadcrumbBack();
            AddClick(_view.drawerOverlay, CloseDrawer);
            if (_view.btnSelectAll != null)
                _view.btnSelectAll.onClick.AddListener(OnSelectAllClicked);
            if (_view.btnOverflow != null)
                _view.btnOverflow.onClick.AddListener(ShowOverflowMenu);
            if (_view.btnBrowseFolder != null)
                _view.btnBrowseFolder.onClick.AddListener(OnBrowseFolder);
            else
                Debug.LogWarning("[SCZip] btnBrowseFolder missing — run SCZip/Setup Main Scene");

            _view.navRecent.onClick.AddListener(() => Nav(NavigationSource.Recent));
            _view.navMyFiles.onClick.AddListener(() => Nav(NavigationSource.MyFiles));
            _view.navStorage.onClick.AddListener(() => Nav(NavigationSource.Storage));
            _view.navPhotos.onClick.AddListener(() => Nav(NavigationSource.Photos));
            _view.navMusic.onClick.AddListener(() => Nav(NavigationSource.Music));
            _view.navSettings.onClick.AddListener(OpenSettings);
            _view.navExit.onClick.AddListener(ExitApp);

            _view.actUnzip.onClick.AddListener(OnUnzip);
            _view.actCompress.onClick.AddListener(OnCompress);
            _view.actShare.onClick.AddListener(() => ShowToast("Share: use platform bridge in v0.2"));
            _view.actDelete.onClick.AddListener(OnDelete);
            if (_view.actMore != null)
                _view.actMore.onClick.AddListener(ShowMoreMenu);

            _view.settingsBack.onClick.AddListener(CloseSettings);
            _view.setFormatZip.onClick.AddListener(() => SetFormat(ArchiveFormat.Zip));
            _view.setFormatTgz.onClick.AddListener(() => SetFormat(ArchiveFormat.TarGzip));
            _view.setLevelNormal.onClick.AddListener(() => SetLevel(ArchiveCompressionLevel.Normal));
            _view.setLevelMax.onClick.AddListener(() => SetLevel(ArchiveCompressionLevel.Maximum));
            _view.setClearCache.onClick.AddListener(() =>
            {
                AppServices.Settings.ClearLocalCache();
                ShowToast("Cache cleared");
            });

            _view.pickerBack.onClick.AddListener(ClosePathPicker);
            _view.pickerConfirm.onClick.AddListener(OnPathPickerConfirm);
            if (_view.pickerNativeBrowse != null)
                _view.pickerNativeBrowse.onClick.AddListener(OnNativeFolderBrowse);
            _view.dialogCancel.onClick.AddListener(HideDialog);
            _view.dialogOk.onClick.AddListener(OnDialogOk);

            if (_view.dialogFormat.options.Count == 0)
            {
                _view.dialogFormat.options = new List<Dropdown.OptionData>
                {
                    new("ZIP (.zip)"),
                    new("TAR.GZ (.tar.gz)")
                };
            }

            _view.dialogFormat.value = 0;
        }

        private static void AddClick(GameObject go, UnityEngine.Events.UnityAction action)
        {
            var btn = go.GetComponent<Button>();
            if (btn == null)
                btn = go.AddComponent<Button>();
            btn.onClick.AddListener(action);
        }

        private static void ExitApp()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void WireBreadcrumbBack()
        {
            if (_view.breadcrumb == null) return;
            var bar = _view.breadcrumb.transform.parent;
            if (bar == null) return;

            var btn = bar.GetComponent<Button>();
            if (btn == null)
            {
                btn = bar.gameObject.AddComponent<Button>();
                var img = bar.GetComponent<Image>();
                if (img != null)
                    btn.targetGraphic = img;
            }

            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                if (_vm.CanGoBack)
                    OnGoBack();
            });
        }

        private void OnSelectAllClicked()
        {
            _vm.ToggleSelectAll();
            UpdateSelectAllButton();
            UnityMainThreadDispatcher.FlushPending();
        }

        private void UpdateSelectAllButton()
        {
            AppBarSelectAllVisual.Apply(_view.btnSelectAll, _vm.SelectAllState);
        }

        private void ShowOverflowMenu()
        {
            ReleaseAppBarButtonFocus();

            if (_vm.IsArchiveInner)
            {
                ShowToast("请先返回文件夹再新建目录");
                return;
            }

            ShowDialog("新建文件夹", "请输入文件夹名称", "NewFolder", true, RunCreateFolder);
        }

        private void OnGoBack()
        {
            if (!_vm.GoBackAndReloadSync())
                return;

            ReleaseAppBarButtonFocus();
            RefreshUiNow();
            UpdateDrawerHighlight(_vm.CurrentSource);
            EnsureAppBarRaycastFix();
            UpdateSelectAllButton();
        }

        private void RefreshUiNow()
        {
            UnityMainThreadDispatcher.FlushPending();
            ForceSyncRefreshForTests();
        }

        private void Nav(NavigationSource source)
        {
            CloseDrawer();
            if (source == NavigationSource.Recent)
                _vm.ShowRecent();
            else
                _vm.NavigateToSource(source);
            UpdateDrawerHighlight(source);
            ReleaseAppBarButtonFocus();
        }

        private void UpdateDrawerHighlight(NavigationSource source)
        {
            var activeColor = new Color(0.88f, 0.94f, 1f);
            var normalColor = Color.white;
            var activeText = new Color(0.082f, 0.396f, 0.753f);

            foreach (var kv in _navButtons)
            {
                var img = kv.Value.GetComponent<Image>();
                var text = kv.Value.GetComponentInChildren<Text>();
                var active = kv.Key == source;
                if (img != null)
                    img.color = active ? activeColor : normalColor;
                if (text != null)
                    text.color = active ? activeText : Color.black;
            }
        }

        private void ToggleDrawer()
        {
            _drawerOpen = !_drawerOpen;
            if (_drawerOpen)
            {
                _view.drawer.anchoredPosition = Vector2.zero;
                SetDrawerRaycast(true);
                SetVisible(_view.drawerOverlay, true);
                SetOverlayRaycast(true);
                BringDrawerToFront();
            }
            else
            {
                CloseDrawer();
            }
        }

        private void CloseDrawer()
        {
            _drawerOpen = false;
            CloseDrawerImmediate();
            SetVisible(_view.drawerOverlay, false);
            SetOverlayRaycast(false);
            RestoreDrawerLayering();
        }

        private void CloseDrawerImmediate()
        {
            _view.drawer.anchoredPosition = new Vector2(-DrawerWidth, 0);
            SetDrawerRaycast(false);
        }

        private void BringDrawerToFront()
        {
            if (_view.drawerOverlay != null)
                _view.drawerOverlay.transform.SetAsLastSibling();
            if (_view.drawer != null)
                _view.drawer.transform.SetAsLastSibling();
        }

        private void RestoreDrawerLayering()
        {
            if (_view.drawerOverlay != null)
                _view.drawerOverlay.transform.SetAsFirstSibling();
            if (_view.drawer != null)
                _view.drawer.transform.SetSiblingIndex(1);
        }

        private void SetOverlayRaycast(bool enabled)
        {
            if (_view.drawerOverlay == null)
                return;

            var img = _view.drawerOverlay.GetComponent<Image>();
            if (img != null)
                img.raycastTarget = enabled;
        }

        private void SetDrawerRaycast(bool enabled)
        {
            var img = _view.drawer != null ? _view.drawer.GetComponent<Image>() : null;
            if (img != null)
                img.raycastTarget = enabled;
        }

        private void RefreshUi()
        {
            UnityMainThreadDispatcher.Instance.Enqueue(DoRefreshUi);
        }

        private void DoRefreshUi()
        {
            if (_view == null) return;

            _view.titleLabel.text = _vm.SelectedCount > 0 ? $"Selected {_vm.SelectedCount}" : _vm.Title;
            _view.subtitleLabel.text = _vm.IsArchiveInner ? _vm.Subtitle : "";
            _view.breadcrumb.text = _vm.Breadcrumb;

            SetVisible(_view.loadingLabel, _vm.IsLoading);
            SetLoadingOverlayRaycast(_vm.IsLoading);
            SetVisible(_view.errorLabel, !string.IsNullOrEmpty(_vm.ErrorMessage));
            _view.errorLabelText.text = _vm.ErrorMessage ?? "";

            RebuildFileList();
            RefreshActionBar();
            UpdateAppBarNavigation();
            EnsureAppBarRaycastFix();
            UpdateSelectAllButton();
        }

        /// <summary>
        /// Keeps AppBar buttons (≡ / ← / 📁 / ☐ / ⋮) clickable.
        /// Checklist when changing shell layout:
        /// 1. Shell / FileScroll / AppBar backgrounds: raycastTarget = false
        /// 2. All AppBar Button images: raycastTarget = true, size ≥ 56×56
        /// 3. AppBar + Breadcrumb rendered above scroll; AppBar buttons last within AppBar
        /// 4. Loading overlay raycast only while IsLoading (see SetLoadingOverlayRaycast)
        /// 5. Heavy IO (Recent/mdfind) on background thread only — never block main thread
        /// </summary>
        private void EnsureAppBarRaycastFix()
        {
            if (_view.shell != null)
            {
                var shell = _view.shell.transform;
                var shellImg = _view.shell.GetComponent<Image>();
                if (shellImg != null)
                    shellImg.raycastTarget = false;

                var appBar = shell.Find("AppBar");
                if (appBar != null)
                {
                    var barImg = appBar.GetComponent<Image>();
                    if (barImg != null)
                    {
                        barImg.color = AppBarPrimary;
                        barImg.raycastTarget = false;
                    }
                }

                var scroll = shell.Find("FileScroll");
                if (scroll != null)
                {
                    var scrollImg = scroll.GetComponent<Image>();
                    if (scrollImg != null)
                        scrollImg.raycastTarget = false;
                }

                EnsureShellChromeLayout();
                BringChromeAboveScroll(shell, appBar, shell.Find("Breadcrumb"));
                BringAppBarButtonsToFront(appBar);
            }

            if (_view.titleLabel != null)
            {
                var titles = _view.titleLabel.transform.parent;
                if (titles != null)
                {
                    var cg = titles.GetComponent<CanvasGroup>();
                    if (cg == null)
                        cg = titles.gameObject.AddComponent<CanvasGroup>();
                    cg.blocksRaycasts = false;
                }
            }

            EnsureNavButtonHitTarget(_view.btnBack);
            EnsureNavButtonHitTarget(_view.btnMenu);
            EnsureNavButtonHitTarget(_view.btnBrowseFolder);
            AppBarActionButtonVisual.Ensure(_view.btnBrowseFolder, AppBarActionButtonVisual.IconBrowse,
                AppBarActionButtonVisual.CaptionBrowse);
            EnsureNavButtonHitTarget(_view.btnOverflow);
            AppBarActionButtonVisual.Ensure(_view.btnOverflow, AppBarActionButtonVisual.IconOverflow,
                AppBarActionButtonVisual.CaptionOverflow);
            AppBarSelectAllVisual.Configure(_view.btnSelectAll);

            if (_view.btnBack != null && _view.btnBack.gameObject.activeSelf)
                _view.btnBack.transform.SetAsLastSibling();
            else if (_view.btnMenu != null && _view.btnMenu.gameObject.activeSelf)
                _view.btnMenu.transform.SetAsLastSibling();
        }

        private static void BringAppBarButtonsToFront(Transform appBar)
        {
            if (appBar == null)
                return;

            for (var i = 0; i < appBar.childCount; i++)
                appBar.GetChild(i).SetAsLastSibling();
        }

        private void SetLoadingOverlayRaycast(bool block)
        {
            if (_view.loadingLabel == null)
                return;

            var img = _view.loadingLabel.GetComponent<Image>();
            if (img != null)
                img.raycastTarget = block;
        }

        private void EnsureShellChromeLayout()
        {
            if (_view?.shell == null)
                return;

            var shell = _view.shell.transform;
            var appBar = shell.Find("AppBar") as RectTransform;
            if (appBar != null)
            {
                appBar.anchorMin = new Vector2(0f, 1f);
                appBar.anchorMax = new Vector2(1f, 1f);
                appBar.pivot = new Vector2(0.5f, 1f);
                appBar.anchoredPosition = Vector2.zero;
                appBar.sizeDelta = new Vector2(0f, AppBarHeight);

                var titles = appBar.Find("Titles") as RectTransform;
                if (titles != null)
                {
                    titles.anchorMin = Vector2.zero;
                    titles.anchorMax = Vector2.one;
                    titles.offsetMin = new Vector2(56f, 4f);
                    titles.offsetMax = new Vector2(-152f, -4f);
                }
            }

            var breadcrumb = shell.Find("Breadcrumb") as RectTransform;
            if (breadcrumb != null)
            {
                breadcrumb.anchorMin = new Vector2(0f, 1f);
                breadcrumb.anchorMax = new Vector2(1f, 1f);
                breadcrumb.pivot = new Vector2(0.5f, 1f);
                breadcrumb.anchoredPosition = new Vector2(0f, -AppBarHeight);
                breadcrumb.sizeDelta = new Vector2(0f, BreadcrumbHeight);
            }

            EnsureFileScrollLayout(shell.Find("FileScroll") as RectTransform);
        }

        private static void EnsureNavButtonHitTarget(Button button)
        {
            if (button == null)
                return;

            var rt = button.GetComponent<RectTransform>();
            if (rt != null)
                rt.sizeDelta = new Vector2(56f, 56f);

            var img = button.GetComponent<Image>();
            if (img != null)
            {
                img.raycastTarget = true;
                img.color = AppBarHitTargetColor;
            }

            button.navigation = new Navigation { mode = Navigation.Mode.None };

            var colors = button.colors;
            colors.selectedColor = colors.normalColor;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.08f);
            button.colors = colors;
        }

        private void ReleaseAppBarButtonFocus()
        {
            UiEventSystemSetup.ClearUiSelection();
        }

        private static void BringChromeAboveScroll(Transform shell, Transform appBar, Transform breadcrumb)
        {
            if (shell == null)
                return;

            if (breadcrumb != null)
                breadcrumb.SetAsLastSibling();
            if (appBar != null)
                appBar.SetAsLastSibling();
        }

        private static void EnsureFileScrollLayout(RectTransform fileScroll)
        {
            if (fileScroll == null)
                return;

            fileScroll.anchorMin = Vector2.zero;
            fileScroll.anchorMax = Vector2.one;
            fileScroll.offsetMin = new Vector2(0f, ActionBarHeight);
            fileScroll.offsetMax = new Vector2(0f, -(AppBarHeight + BreadcrumbHeight));
        }

        private void EnsureDrawerLayout()
        {
            if (_view?.drawer == null)
                return;

            if (_font == null)
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            FixDrawerHeader(_view.drawer.Find("Header"));
            var nav = _view.drawer.Find("Nav") as RectTransform;
            if (nav != null)
                FixDrawerNav(nav);
        }

        private void FixDrawerHeader(Transform header)
        {
            if (header == null)
                return;

            foreach (var label in header.GetComponentsInChildren<Text>(true))
            {
                if (label.font == null)
                    label.font = _font;

                var rt = label.rectTransform;
                if (rt.sizeDelta.y > 1f)
                    continue;

                rt.anchorMin = new Vector2(0, 0);
                rt.anchorMax = new Vector2(1, 0);
                rt.pivot = new Vector2(0, 0);
                rt.sizeDelta = new Vector2(0, label.fontSize + 8);
            }
        }

        private void FixDrawerNav(RectTransform nav)
        {
            var layout = nav.GetComponent<VerticalLayoutGroup>();
            if (layout != null)
            {
                layout.childControlWidth = true;
                layout.childForceExpandWidth = true;
            }

            for (var i = 0; i < nav.childCount; i++)
            {
                var child = nav.GetChild(i) as RectTransform;
                if (child == null)
                    continue;

                child.anchorMin = new Vector2(0, 1);
                child.anchorMax = new Vector2(1, 1);
                child.pivot = new Vector2(0.5f, 1);
                if (child.sizeDelta.y < 1f)
                    child.sizeDelta = new Vector2(0, 48);

                var le = child.GetComponent<LayoutElement>();
                if (le != null)
                    le.preferredHeight = 48;

                for (var j = 0; j < child.childCount; j++)
                {
                    var textRt = child.GetChild(j) as RectTransform;
                    if (textRt == null)
                        continue;

                    textRt.anchorMin = Vector2.zero;
                    textRt.anchorMax = Vector2.one;
                    textRt.offsetMin = new Vector2(16, 0);
                    textRt.offsetMax = Vector2.zero;

                    var label = textRt.GetComponent<Text>();
                    if (label != null && label.font == null)
                        label.font = _font;
                }
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(nav);
        }

        private void UpdateAppBarNavigation()
        {
            var canBack = _vm.CanGoBack;
            if (_view.btnBack != null)
            {
                SetVisible(_view.btnBack.gameObject, canBack);
                _view.btnBack.interactable = canBack;
                if (canBack)
                    _view.btnBack.transform.SetAsLastSibling();
            }

            if (_view.btnMenu != null)
            {
                SetVisible(_view.btnMenu.gameObject, !canBack);
                if (!canBack)
                    _view.btnMenu.transform.SetAsLastSibling();
            }
        }

        /// <summary>PlayMode tests — read select-all highlight alpha.</summary>
        public float GetSelectAllHighlightAlphaForTests() =>
            AppBarSelectAllVisual.HighlightAlpha(_view.btnSelectAll);

        private void RebuildFileList()
        {
            if (_fileListRebuildCoroutine != null)
                StopCoroutine(_fileListRebuildCoroutine);

            _fileListRebuildCoroutine = StartCoroutine(RebuildFileListDeferred());
        }

        /// <summary>PlayMode tests — apply the same AppBar raycast / z-order fixes as runtime.</summary>
        public void EnsureAppBarRaycastFixForTests()
        {
            EnsureAppBarRaycastFix();
        }

        /// <summary>PlayMode tests — fix drawer nav label layout like runtime.</summary>
        public void EnsureDrawerLayoutForTests()
        {
            EnsureDrawerLayout();
        }

        /// <summary>PlayMode tests — rebuild list immediately without end-of-frame deferral.</summary>
        public void ForceSyncRefreshForTests()
        {
            if (_view == null || _vm == null)
                return;

            if (_fileListRebuildCoroutine != null)
            {
                StopCoroutine(_fileListRebuildCoroutine);
                _fileListRebuildCoroutine = null;
            }

            _view.titleLabel.text = _vm.SelectedCount > 0 ? $"Selected {_vm.SelectedCount}" : _vm.Title;
            _view.subtitleLabel.text = _vm.IsArchiveInner ? _vm.Subtitle : "";
            _view.breadcrumb.text = _vm.Breadcrumb;
            SetVisible(_view.loadingLabel, _vm.IsLoading);
            SetLoadingOverlayRaycast(_vm.IsLoading);
            SetVisible(_view.errorLabel, !string.IsNullOrEmpty(_vm.ErrorMessage));
            _view.errorLabelText.text = _vm.ErrorMessage ?? "";
            PopulateFileList();
            RefreshActionBar();
            UpdateAppBarNavigation();
            EnsureAppBarRaycastFix();
            UpdateSelectAllButton();
        }

        private IEnumerator RebuildFileListDeferred()
        {
            yield return null;
            UiEventSystemSetup.ClearInputSystemPointerState();

            _fileListRebuildCoroutine = null;
            if (_view == null || _view.fileListContent == null)
                yield break;

            PopulateFileList();
        }

        private void PopulateFileList()
        {
            ClearChildren(_view.fileListContent);

            if (_vm.IsArchiveInner)
            {
                var empty = _vm.ArchiveItems.Count == 0 && !_vm.IsLoading;
                SetVisible(_view.emptyLabel, empty);
                if (empty)
                    _view.emptyLabelText.text = "Archive is empty";

                foreach (var entry in _vm.ArchiveItems)
                    BuildArchiveRow(entry);
            }
            else
            {
                var empty = _vm.Items.Count == 0 && !_vm.IsLoading;
                SetVisible(_view.emptyLabel, empty);
                if (empty)
                {
                    _view.emptyLabelText.text = _vm.CurrentSource == NavigationSource.Recent
                        ? "No recent items"
                        : "Folder is empty";
                }

                foreach (var entry in _vm.Items)
                    BuildFileRow(entry);
            }
        }

        private void BuildFileRow(FileEntry entry)
        {
            var row = CreateRowBase();
            var selected = _vm.SelectedIds.Contains(entry.Id);
            if (selected)
                row.background.color = new Color(0.88f, 0.94f, 1f);

            ApplyCheckVisual(row.checkButton, selected);
            row.checkButton.onClick.AddListener(() => _vm.ToggleSelect(entry.Id));
            row.iconText.text = GetIcon(entry);
            row.nameText.text = entry.Name;
            row.metaText.text = entry.IsDirectory ? "Folder" : entry.DateLabel;
            row.sizeText.text = entry.IsDirectory ? "" : entry.SizeLabel;
            row.metaText.gameObject.SetActive(true);
            row.rowButton.onClick.AddListener(() => _vm.OpenEntry(entry));
        }

        private void BuildArchiveRow(ArchiveEntry entry)
        {
            var row = CreateRowBase();
            var selected = _vm.SelectedIds.Contains(entry.Path);
            if (selected)
                row.background.color = new Color(0.88f, 0.94f, 1f);

            ApplyCheckVisual(row.checkButton, selected);
            row.checkButton.onClick.AddListener(() => _vm.ToggleSelect(entry.Path));
            row.iconText.text = entry.IsDirectory ? "📁" : "📄";
            row.nameText.text = entry.Name;
            row.metaText.text = entry.IsDirectory ? "Folder" : "";
            row.sizeText.text = entry.IsDirectory ? "" : FileEntry.FormatSize(entry.SizeBytes);
            row.metaText.gameObject.SetActive(entry.IsDirectory);
            row.rowButton.interactable = false;
        }

        private FileListRowView CreateRowBase()
        {
            var go = new GameObject("FileRow", typeof(RectTransform));
            go.transform.SetParent(_view.fileListContent, false);
            var rowLe = go.AddComponent<LayoutElement>();
            rowLe.preferredHeight = UiListMetrics.FileRowHeight;
            rowLe.flexibleWidth = 1;

            var row = go.AddComponent<FileListRowView>();
            row.background = go.AddComponent<Image>();
            row.background.color = Color.white;
            row.background.raycastTarget = false;

            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(10, 14, 8, 8);
            hlg.spacing = 10;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false;
            hlg.childControlWidth = true;

            row.checkButton = CreateCheckButton(go.transform);
            row.iconText = CreateInlineText(go.transform, "📄", UiListMetrics.FileIconFontSize, 36);

            var body = new GameObject("Body", typeof(RectTransform));
            body.transform.SetParent(go.transform, false);
            var bodyLe = body.AddComponent<LayoutElement>();
            bodyLe.flexibleWidth = 1;
            bodyLe.minWidth = 120;
            var bodyVlg = body.AddComponent<VerticalLayoutGroup>();
            bodyVlg.spacing = 3;
            bodyVlg.childAlignment = TextAnchor.MiddleLeft;
            bodyVlg.childControlWidth = true;
            bodyVlg.childForceExpandWidth = true;

            row.nameText = CreateInlineText(body.transform, "", UiListMetrics.FileNameFontSize, 0, true);
            row.nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
            row.metaText = CreateInlineText(body.transform, "", UiListMetrics.FileMetaFontSize, 0, true);
            row.metaText.color = new Color(0.46f, 0.46f, 0.46f);
            row.metaText.horizontalOverflow = HorizontalWrapMode.Overflow;

            row.sizeText = CreateInlineText(go.transform, "", UiListMetrics.FileSizeFontSize, UiListMetrics.FileSizeColumnWidth);
            row.sizeText.alignment = TextAnchor.MiddleRight;
            row.sizeText.horizontalOverflow = HorizontalWrapMode.Overflow;

            var hit = new GameObject("RowHit", typeof(RectTransform));
            hit.transform.SetParent(body.transform, false);
            var hitRt = hit.GetComponent<RectTransform>();
            Stretch(hitRt);
            var hitLe = hit.AddComponent<LayoutElement>();
            hitLe.ignoreLayout = true;
            var hitImg = hit.AddComponent<Image>();
            hitImg.color = AppBarHitTargetColor;
            row.rowButton = hit.AddComponent<Button>();
            row.rowButton.targetGraphic = hitImg;
            hit.transform.SetAsLastSibling();
            hitImg.raycastTarget = true;

            return row;
        }

        private Button CreateCheckButton(Transform parent)
        {
            var go = new GameObject("Check", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = UiListMetrics.FileCheckWidth;
            le.preferredHeight = UiListMetrics.FileCheckWidth;

            var img = go.AddComponent<Image>();
            img.color = FileCheckIdleBg;
            img.raycastTarget = true;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var labelGo = new GameObject("Mark", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            Stretch(labelGo.GetComponent<RectTransform>());
            var label = labelGo.AddComponent<Text>();
            label.font = _font;
            label.fontSize = UiListMetrics.FileCheckFontSize;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = FileCheckMarkColor;
            label.raycastTarget = false;

            ApplyCheckVisual(btn, false);
            return btn;
        }

        private static void ApplyCheckVisual(Button checkButton, bool selected)
        {
            if (checkButton == null)
                return;

            var img = checkButton.targetGraphic as Image;
            if (img != null)
                img.color = selected ? FileCheckSelectedBg : FileCheckIdleBg;

            var label = checkButton.GetComponentInChildren<Text>();
            if (label != null)
                label.text = selected ? "\u2713" : "";
        }

        private Text CreateInlineText(Transform parent, string text, int size, float width, bool expand = false)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            if (expand)
                le.flexibleWidth = 1;
            else
                le.preferredWidth = width;

            var label = go.AddComponent<Text>();
            label.font = _font;
            label.text = text;
            label.fontSize = size;
            label.color = Color.black;
            label.alignment = TextAnchor.MiddleLeft;
            return label;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static string GetIcon(FileEntry entry)
        {
            if (entry.IsDirectory) return "📁";
            return entry.ArchiveFormat switch
            {
                ArchiveFormat.Zip => "📦",
                ArchiveFormat.TarGzip => "📦",
                ArchiveFormat.Gzip => "📦",
                _ when entry.Name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                         entry.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase) => "🖼",
                _ when entry.Name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) => "📄",
                _ => "📄"
            };
        }

        private void RefreshActionBar()
        {
            var actions = _vm.AvailableActions;
            var visible = actions.Count > 0;
            SetVisible(_view.actionBar, visible);
            SetVisible(_view.actUnzip.gameObject, actions.Contains(FileAction.Unzip));
            SetVisible(_view.actCompress.gameObject, actions.Contains(FileAction.Compress));
            SetVisible(_view.actShare.gameObject, actions.Contains(FileAction.Share));
            SetVisible(_view.actDelete.gameObject, actions.Contains(FileAction.Delete));
            if (_view.actMore != null)
                SetVisible(_view.actMore.gameObject, actions.Contains(FileAction.More));
        }

        private void OnBrowseFolder()
        {
            if (_vm.IsArchiveInner)
            {
                ShowToast("Cannot browse folders inside an archive");
                ReleaseAppBarButtonFocus();
                return;
            }

            _pickerMode = PathPickerMode.BrowseFolder;
            _pickerPath = _vm.CurrentFolderPath ?? AppServices.FileSystem.StorageRoot;
            if (!Directory.Exists(_pickerPath))
                _pickerPath = AppServices.FileSystem.StorageRoot;
            OpenPathPicker();
            ReleaseAppBarButtonFocus();
        }

        private void OnCompress()
        {
            if (!AppServices.FeatureGate.CanCreate(AppServices.Settings.DefaultFormat) &&
                AppServices.Settings.DefaultFormat != ArchiveFormat.Zip)
            {
                ShowProDialog("Creating TAR.GZ requires SCZip Pro in this build. Using ZIP.");
            }

            _pickerMode = PathPickerMode.CompressDestination;
            _pickerPath = AppServices.FileSystem.StorageRoot;
            _view.pickerTitle.text = "Select save location";
            OpenPathPicker();
        }

        private void OnUnzip()
        {
            _pickerMode = PathPickerMode.ExtractDestination;
            _pickerPath = AppServices.FileSystem.StorageRoot;
            _view.pickerTitle.text = "Select extract location";
            OpenPathPicker();
        }

        private void OnDelete()
        {
            if (_vm.IsArchiveInner)
            {
                ShowToast("Delete inside archive: coming in v0.2");
                return;
            }

            ShowDialog("Delete", $"Delete {_vm.SelectedCount} item(s)? This cannot be undone.", "", false,
                () => RunDeleteSelected());
        }

        private async void RunDeleteSelected()
        {
            await _vm.DeleteSelectedAsync();
            ShowToast("Deleted");
        }

        private async void RunCreateFolder()
        {
            var name = _view.dialogInput.text?.Trim();
            if (!string.IsNullOrEmpty(name))
            {
                await _vm.CreateFolderAsync(name);
                ShowToast("Folder created");
            }
        }

        private void ShowMoreMenu()
        {
            if (ActionBarResolver.CanRename(_vm.GetSelectedEntries()))
            {
                var entry = _vm.GetSelectedEntries()[0];
                ShowDialog("Rename", "New name:", entry.Name, true, () => RunRename(entry));
            }
            else
            {
                ShowToast("Rename requires single selection");
            }
        }

        private async void RunRename(FileEntry entry)
        {
            var name = _view.dialogInput.text?.Trim();
            if (string.IsNullOrEmpty(name)) return;
            var dir = Path.GetDirectoryName(entry.FullPath);
            var dest = Path.Combine(dir, name);
            await AppServices.FileSystem.RenameAsync(entry.FullPath, dest);
            await _vm.LoadCurrentAsync();
            ShowToast("Renamed");
        }

        private void OpenSettings()
        {
            CloseDrawer();
            SetVisible(_view.settingsPanel, true);
        }

        private void CloseSettings() => SetVisible(_view.settingsPanel, false);

        private void SetFormat(ArchiveFormat format)
        {
            AppServices.Settings.DefaultFormat = format;
            ShowToast($"Default format: {format}");
        }

        private void SetLevel(ArchiveCompressionLevel level)
        {
            AppServices.Settings.CompressionLevel = level;
            ShowToast($"Compression level: {level}");
        }

        private void OpenPathPicker()
        {
            CloseDrawerImmediate();
            SetVisible(_view.shell, false);
            SetVisible(_view.drawerOverlay, false);
            UpdatePickerChrome();
            EnsurePathPickerLayout();
            BringPathPickerToFront();
            SetVisible(_view.pathPicker, true);
            RefreshPickerQuickRoots();
            RefreshPickerList();
        }

        private void BringPathPickerToFront()
        {
            if (_view.pathPicker == null)
                return;

            var picker = _view.pathPicker.transform;
            if (_view.dialogOverlay != null)
                picker.SetSiblingIndex(_view.dialogOverlay.transform.GetSiblingIndex());
            else
                picker.SetAsLastSibling();
        }

        private void EnsurePathPickerLayout()
        {
            if (_view.pathPicker == null)
                return;

            var topInset = AppBarHeight + BreadcrumbHeight + PickerQuickRootsHeight;

            if (_view.pickerBack != null)
            {
                var backRt = _view.pickerBack.GetComponent<RectTransform>();
                backRt.anchorMin = new Vector2(0f, 0.5f);
                backRt.anchorMax = new Vector2(0f, 0.5f);
                backRt.pivot = new Vector2(0f, 0.5f);
                backRt.anchoredPosition = new Vector2(4f, 0f);
                backRt.sizeDelta = new Vector2(48f, 48f);
                _view.pickerBack.transform.SetAsFirstSibling();
            }

            if (_view.pickerTitle != null)
            {
                var titleRt = _view.pickerTitle.GetComponent<RectTransform>();
                titleRt.anchorMin = Vector2.zero;
                titleRt.anchorMax = Vector2.one;
                titleRt.pivot = new Vector2(0.5f, 0.5f);
                titleRt.anchoredPosition = Vector2.zero;
                titleRt.sizeDelta = Vector2.zero;
                titleRt.offsetMin = new Vector2(52f, 0f);
                titleRt.offsetMax = Vector2.zero;
            }

            if (_view.pickerBreadcrumb != null)
            {
                _view.pickerBreadcrumb.horizontalOverflow = HorizontalWrapMode.Overflow;
                _view.pickerBreadcrumb.verticalOverflow = VerticalWrapMode.Truncate;
            }

            if (_view.pickerQuickRoots != null)
            {
                var quickRt = _view.pickerQuickRoots;
                quickRt.anchorMin = new Vector2(0f, 1f);
                quickRt.anchorMax = new Vector2(1f, 1f);
                quickRt.pivot = new Vector2(0.5f, 1f);
                quickRt.anchoredPosition = new Vector2(0f, -(AppBarHeight + BreadcrumbHeight));
                quickRt.sizeDelta = new Vector2(0f, PickerQuickRootsHeight);

                var hlg = quickRt.GetComponent<HorizontalLayoutGroup>();
                if (hlg != null)
                {
                    hlg.childForceExpandHeight = false;
                    hlg.childControlHeight = true;
                }

                quickRt.SetAsLastSibling();
            }

            var scroll = _view.pathPicker.transform.Find("PickerScroll") as RectTransform;
            if (scroll != null)
            {
                scroll.anchorMin = Vector2.zero;
                scroll.anchorMax = Vector2.one;
                scroll.pivot = new Vector2(0.5f, 0.5f);
                scroll.anchoredPosition = Vector2.zero;
                scroll.sizeDelta = Vector2.zero;
                scroll.offsetMin = new Vector2(0f, PickerBottomBarHeight);
                scroll.offsetMax = new Vector2(0f, -topInset);
                scroll.SetSiblingIndex(_view.pickerQuickRoots != null
                    ? _view.pickerQuickRoots.GetSiblingIndex()
                    : scroll.GetSiblingIndex());
            }
        }

        private void UpdatePickerChrome()
        {
            if (_view.pickerTitle != null)
            {
                _view.pickerTitle.text = _pickerMode switch
                {
                    PathPickerMode.BrowseFolder => "选择文件夹",
                    PathPickerMode.CompressDestination => "选择保存位置",
                    PathPickerMode.ExtractDestination => "选择解压位置",
                    _ => "选择文件夹"
                };
            }

            if (_view.pickerConfirmLabel != null)
            {
                _view.pickerConfirmLabel.text = _pickerMode switch
                {
                    PathPickerMode.BrowseFolder => "打开此文件夹",
                    PathPickerMode.CompressDestination => "下一步",
                    PathPickerMode.ExtractDestination => "解压到此",
                    _ => "确定"
                };
            }

            if (_view.pickerNativeBrowse != null)
                SetVisible(_view.pickerNativeBrowse.gameObject, true);
        }

        private void RefreshPickerQuickRoots()
        {
            if (_view.pickerQuickRoots == null)
                return;

            ClearChildren(_view.pickerQuickRoots);

            var fs = AppServices.FileSystem;
            AddQuickRootButton("Storage", fs.StorageRoot);
            AddQuickRootButton("My Files", fs.MyFilesRoot);
            AddQuickRootButton("Photos", fs.PhotosRoot);
            AddQuickRootButton("Music", fs.MusicRoot);
        }

        private void AddQuickRootButton(string label, string path)
        {
            var go = new GameObject(label, typeof(RectTransform));
            go.transform.SetParent(_view.pickerQuickRoots, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 112;
            le.preferredHeight = 32;
            le.minHeight = 32;
            go.AddComponent<Image>().color = Color.white;
            var btn = go.AddComponent<Button>();
            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            Stretch(textGo.GetComponent<RectTransform>());
            var text = textGo.AddComponent<Text>();
            text.font = _font;
            text.text = label;
            text.fontSize = 14;
            text.color = new Color(0.082f, 0.396f, 0.753f);
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            btn.onClick.AddListener(() =>
            {
                _pickerPath = path;
                RefreshPickerList();
            });
        }

        private void ClosePathPicker()
        {
            SetVisible(_view.pathPicker, false);
            _pickerMode = PathPickerMode.None;
            SetVisible(_view.shell, true);
            EnsureAppBarRaycastFix();
        }

        private void RefreshPickerList()
        {
            if (_view.pickerBreadcrumb != null)
                _view.pickerBreadcrumb.text = ShortenPathForDisplay(_pickerPath);

            ClearChildren(_view.pickerListContent);

            var rowCount = 0;
            var parent = AppServices.FileSystem.GetParent(_pickerPath);
            if (!string.IsNullOrEmpty(parent))
            {
                BuildPickerRow("..", parent);
                rowCount++;
            }

            if (Directory.Exists(_pickerPath))
            {
                foreach (var dir in Directory.GetDirectories(_pickerPath))
                {
                    BuildPickerRow(Path.GetFileName(dir), dir);
                    rowCount++;
                }
            }

            if (rowCount == 0)
                BuildPickerHintRow("此文件夹为空，可点上方标签切换或「系统浏览」");

            if (_view.pickerListContent != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_view.pickerListContent);
        }

        private void BuildPickerHintRow(string message)
        {
            var go = new GameObject("PickerHint", typeof(RectTransform));
            go.transform.SetParent(_view.pickerListContent, false);
            go.AddComponent<LayoutElement>().preferredHeight = UiListMetrics.PickerRowHeight;
            var label = go.AddComponent<Text>();
            label.font = _font;
            label.text = message;
            label.fontSize = UiListMetrics.PickerRowFontSize;
            label.color = new Color(0.46f, 0.46f, 0.46f);
            label.alignment = TextAnchor.MiddleLeft;
            var rt = label.GetComponent<RectTransform>();
            rt.offsetMin = new Vector2(16f, 0f);
            label.raycastTarget = false;
        }

        private static string ShortenPathForDisplay(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "";

            const int maxLen = 52;
            return path.Length <= maxLen ? path : "…" + path.Substring(path.Length - (maxLen - 1));
        }

        private void BuildPickerRow(string name, string path)
        {
            var go = new GameObject("PickerRow", typeof(RectTransform));
            go.transform.SetParent(_view.pickerListContent, false);
            go.AddComponent<LayoutElement>().preferredHeight = UiListMetrics.PickerRowHeight;
            var img = go.AddComponent<Image>();
            img.color = Color.white;
            var btn = go.AddComponent<Button>();
            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            Stretch(textGo.GetComponent<RectTransform>());
            var label = textGo.AddComponent<Text>();
            label.font = _font;
            label.text = "📁 " + name;
            label.fontSize = UiListMetrics.PickerRowFontSize;
            label.alignment = TextAnchor.MiddleLeft;
            var labelRt = textGo.GetComponent<RectTransform>();
            labelRt.offsetMin = new Vector2(16, 0);
            label.raycastTarget = false;
            btn.onClick.AddListener(() =>
            {
                _pickerPath = path;
                RefreshPickerList();
            });
        }

        private void OnPathPickerConfirm()
        {
            if (_pickerMode == PathPickerMode.BrowseFolder)
            {
                if (!Directory.Exists(_pickerPath))
                {
                    ShowToast("请选择有效文件夹");
                    return;
                }

                var title = Path.GetFileName(_pickerPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (string.IsNullOrEmpty(title))
                    title = _pickerPath;

                var source = GuessSourceForPath(_pickerPath);
                ClosePathPicker();
                _vm.NavigateToFolder(_pickerPath, title, source);
                UpdateDrawerHighlight(source);
                ShowToast($"已打开: {title}");
                return;
            }

            if (_pickerMode == PathPickerMode.CompressDestination)
            {
                var format = AppServices.Settings.DefaultFormat;
                if (!AppServices.FeatureGate.CanCreate(format))
                    format = ArchiveFormat.Zip;

                var ext = ArchiveFormatRegistry.GetDefaultExtension(format);
                var defaultName = $"archive_{DateTime.Now:yyyyMMdd_HHmmss}{ext}";
                ClosePathPicker();
                _view.dialogFormat.value = format == ArchiveFormat.TarGzip ? 1 : 0;
                ShowCreateDialog(_pickerPath, defaultName);
            }
            else if (_pickerMode == PathPickerMode.ExtractDestination)
            {
                var dest = _pickerPath;
                ClosePathPicker();
                RunExtract(dest);
            }
        }

        private static NavigationSource GuessSourceForPath(string path)
        {
            var fs = AppServices.FileSystem;
            if (PathsEqual(path, fs.StorageRoot)) return NavigationSource.Storage;
            if (PathsEqual(path, fs.MyFilesRoot)) return NavigationSource.MyFiles;
            if (PathsEqual(path, fs.PhotosRoot)) return NavigationSource.Photos;
            if (PathsEqual(path, fs.MusicRoot)) return NavigationSource.Music;
            if (path.StartsWith(fs.StorageRoot, StringComparison.OrdinalIgnoreCase)) return NavigationSource.Storage;
            if (path.StartsWith(fs.MyFilesRoot, StringComparison.OrdinalIgnoreCase)) return NavigationSource.MyFiles;
            if (path.StartsWith(fs.PhotosRoot, StringComparison.OrdinalIgnoreCase)) return NavigationSource.Photos;
            if (path.StartsWith(fs.MusicRoot, StringComparison.OrdinalIgnoreCase)) return NavigationSource.Music;
            return NavigationSource.Storage;
        }

        private static bool PathsEqual(string a, string b) =>
            string.Equals(Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);

        private void OnNativeFolderBrowse()
        {
#if UNITY_EDITOR
            var start = Directory.Exists(_pickerPath) ? _pickerPath : AppServices.FileSystem.StorageRoot;
            var picked = UnityEditor.EditorUtility.OpenFolderPanel("选择文件夹", start, "");
            if (string.IsNullOrEmpty(picked))
                return;

            _pickerPath = picked;
            RefreshPickerList();

            if (_pickerMode == PathPickerMode.BrowseFolder)
            {
                var title = Path.GetFileName(picked.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (string.IsNullOrEmpty(title))
                    title = picked;
                ClosePathPicker();
                var source = GuessSourceForPath(picked);
                _vm.NavigateToFolder(picked, title, source);
                UpdateDrawerHighlight(source);
                ShowToast($"已打开: {title}");
            }
#else
            ShowToast("请使用下方列表选择文件夹");
#endif
        }

        private void ShowCreateDialog(string dir, string defaultName)
        {
            EnsureDialogInputReady();

            var format = AppServices.Settings.DefaultFormat;
            _view.dialogFormat.value = format == ArchiveFormat.TarGzip ? 1 : 0;
            _view.dialogTitle.text = "New archive";
            _view.dialogMessage.text = $"Save to:\n{dir}";
            _view.dialogInput.text = defaultName;
            SetDialogInputVisible(true);
            SetVisible(_view.dialogFormat.gameObject, true);
            _dialogOkAction = () =>
            {
                var name = _view.dialogInput.text?.Trim();
                if (string.IsNullOrEmpty(name)) return;
                if (!name.Contains('.'))
                    name += ArchiveFormatRegistry.GetDefaultExtension(
                        _view.dialogFormat.value == 1 ? ArchiveFormat.TarGzip : ArchiveFormat.Zip);

                var outFormat = _view.dialogFormat.value == 1 ? ArchiveFormat.TarGzip : ArchiveFormat.Zip;
                if (!AppServices.FeatureGate.CanCreate(outFormat))
                    outFormat = ArchiveFormat.Zip;

                RunCreateArchive(Path.Combine(dir, name), outFormat, name);
            };
            SetVisible(_view.dialogOverlay, true);

            StartCoroutine(FocusDialogInputNextFrame());
        }

        private async void RunCreateArchive(string output, ArchiveFormat format, string displayName)
        {
            try
            {
                await _vm.CreateArchiveAsync(output, format);
                ShowToast($"Created {displayName}");
            }
            catch (Exception ex)
            {
                ShowDialog("Error", ex.Message, "", false, null);
            }
        }

        private async void RunExtract(string dest)
        {
            try
            {
                await _vm.ExtractSelectedAsync(dest);
                ShowToast($"Extracted to {Path.GetFileName(dest)}");
            }
            catch (Exception ex)
            {
                ShowDialog("Error", ex.Message, "", false, null);
            }
        }

        private void ShowDialog(string title, string message, string defaultInput, bool showInput, Action onOk)
        {
            EnsureDialogInputReady();

            _view.dialogTitle.text = title;
            _view.dialogMessage.text = message;
            _dialogOkAction = onOk;
            _view.dialogInput.text = defaultInput ?? "";
            SetDialogInputVisible(showInput);
            SetVisible(_view.dialogFormat.gameObject, false);
            SetVisible(_view.dialogOverlay, true);

            if (showInput)
                StartCoroutine(FocusDialogInputNextFrame());
        }

        private void EnsureDialogInputReady()
        {
            if (_view.dialogInput == null)
                return;

            var img = _view.dialogInput.GetComponent<Image>();
            if (img != null)
            {
                if (_view.dialogInput.targetGraphic == null)
                    _view.dialogInput.targetGraphic = img;
                img.raycastTarget = true;
            }

            if (_view.dialogInput.textComponent != null)
                _view.dialogInput.textComponent.raycastTarget = false;

            if (_view.dialogInput.placeholder != null)
                _view.dialogInput.placeholder.raycastTarget = false;

            _view.dialogInput.interactable = true;
            _view.dialogInput.readOnly = false;
            _view.dialogInput.lineType = InputField.LineType.SingleLine;

            InputFieldInputSystemBridge.EnsureOn(_view.dialogInput);

            var row = _view.dialogInput.transform.parent;
            var le = row != null ? row.GetComponent<LayoutElement>() : _view.dialogInput.GetComponent<LayoutElement>();
            if (le != null)
            {
                le.flexibleWidth = 1;
                if (le.preferredHeight <= 0)
                    le.preferredHeight = 36;
            }

            var inputRt = _view.dialogInput.GetComponent<RectTransform>();
            if (inputRt != null)
            {
                inputRt.anchorMin = Vector2.zero;
                inputRt.anchorMax = Vector2.one;
                inputRt.offsetMin = Vector2.zero;
                inputRt.offsetMax = Vector2.zero;
            }

            if (row != null)
            {
                var rowRt = row.GetComponent<RectTransform>();
                if (rowRt != null && rowRt.sizeDelta.x > 0)
                {
                    rowRt.anchorMin = new Vector2(0, 0.5f);
                    rowRt.anchorMax = new Vector2(1, 0.5f);
                    rowRt.pivot = new Vector2(0.5f, 0.5f);
                    rowRt.sizeDelta = new Vector2(0, le != null && le.preferredHeight > 0 ? le.preferredHeight : 36);
                }
            }

            var dialog = row != null ? row.parent : _view.dialogInput.transform.parent;
            if (dialog != null)
            {
                var vlg = dialog.GetComponent<VerticalLayoutGroup>();
                if (vlg != null)
                {
                    vlg.childControlWidth = true;
                    vlg.childForceExpandWidth = true;
                    LayoutRebuilder.ForceRebuildLayoutImmediate(dialog.GetComponent<RectTransform>());
                }
            }
        }

        private IEnumerator FocusDialogInputNextFrame()
        {
            yield return null;
            if (_view.dialogInput == null || !_view.dialogInput.gameObject.activeInHierarchy)
                yield break;

            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_view.dialogInput.gameObject);

            _view.dialogInput.Select();
            _view.dialogInput.ActivateInputField();
        }

        private void ShowProDialog(string message) => ShowDialog("SCZip Pro", message, "", false, null);

        private void HideDialog()
        {
            if (_view.dialogInput != null)
            {
                if (_view.dialogInput.isFocused)
                    _view.dialogInput.DeactivateInputField();

                DialogInputBinding.ClearEventSystemForRow(_view.dialogInput.transform.parent);
            }

            SetVisible(_view.dialogOverlay, false);
            _dialogOkAction = null;
        }

        private void OnDialogOk()
        {
            var action = _dialogOkAction;
            HideDialog();
            action?.Invoke();
        }

        private void ShowToast(string message)
        {
            _view.toastLabel.text = message;
            SetVisible(_view.toast, true);
            StopAllCoroutines();
            StartCoroutine(HideToastAfter(2f));
        }

        private IEnumerator HideToastAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            SetVisible(_view.toast, false);
        }

        private static void SetVisible(GameObject go, bool visible)
        {
            if (go != null)
                go.SetActive(visible);
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null)
                return;

            UiEventSystemSetup.PrepareForHierarchyRebuild(parent);

            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i).gameObject;
                if (child != null)
                    Destroy(child);
            }

            UiEventSystemSetup.ClearInputSystemPointerState();
        }
    }
}
