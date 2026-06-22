#if UNITY_EDITOR
using SCZip.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SCZip.Editor
{
    public static class UguiSceneBuilder
    {
        private static readonly Color Primary = new(0.082f, 0.396f, 0.753f);
        private static readonly Color Bg = new(0.96f, 0.96f, 0.96f);
        private static readonly Color GrayText = new(0.46f, 0.46f, 0.46f);
        private const float DrawerWidth = 280f;
        private const float AppBarHeight = 56f;
        private const float BreadcrumbHeight = 40f;
        private const float ActionBarHeight = 56f;

        public static AppShellView BuildUiCanvas()
        {
            var existing = GameObject.Find("UICanvas");
            if (existing != null)
                Object.DestroyImmediate(existing);

            var canvasGo = new GameObject("UICanvas", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = UiCanvasConfig.ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();
            canvasGo.AddComponent<UiCanvasConfig>();

            var view = canvasGo.AddComponent<AppShellView>();
            var root = canvasGo.GetComponent<RectTransform>();
            Stretch(root);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            view.drawerOverlay = CreatePanel(root, "DrawerOverlay", new Color(0, 0, 0, 0.4f));
            Stretch(view.drawerOverlay.GetComponent<RectTransform>());
            view.drawerOverlay.SetActive(false);
            view.drawerOverlay.GetComponent<Image>().raycastTarget = false;

            view.drawer = CreateDrawer(root, font, view);
            view.shell = CreateShell(root, font, view);
            view.settingsPanel = CreateSettingsPanel(root, font, view);
            view.pathPicker = CreatePathPicker(root, font, view);
            view.dialogOverlay = CreateDialog(root, font, view);
            view.toast = CreateToast(root, font, view);

            view.drawer.anchoredPosition = new Vector2(-DrawerWidth, 0);
            view.settingsPanel.SetActive(false);
            view.pathPicker.SetActive(false);
            view.dialogOverlay.SetActive(false);
            view.toast.SetActive(false);
            view.actionBar.SetActive(false);

            return view;
        }

        private static RectTransform CreateDrawer(Transform parent, Font font, AppShellView view)
        {
            var drawer = CreateRect(parent, "Drawer");
            drawer.anchorMin = new Vector2(0, 0);
            drawer.anchorMax = new Vector2(0, 1);
            drawer.pivot = new Vector2(0, 0.5f);
            drawer.sizeDelta = new Vector2(DrawerWidth, 0);

            var bg = drawer.gameObject.AddComponent<Image>();
            bg.color = new Color(0.98f, 0.98f, 0.98f);

            var header = CreateRect(drawer, "Header");
            header.anchorMin = new Vector2(0, 1);
            header.anchorMax = new Vector2(1, 1);
            header.pivot = new Vector2(0.5f, 1);
            header.sizeDelta = new Vector2(0, 120);
            var headerImg = header.gameObject.AddComponent<Image>();
            headerImg.color = Primary;
            AddText(header, "SCZip", 22, Color.white, TextAnchor.LowerLeft, new Vector2(16, 40));
            AddText(header, "v0.1.0", 12, new Color(1, 1, 1, 0.85f), TextAnchor.LowerLeft, new Vector2(16, 16));

            var nav = CreateRect(drawer, "Nav");
            nav.anchorMin = new Vector2(0, 0);
            nav.anchorMax = new Vector2(1, 1);
            nav.offsetMin = new Vector2(0, 0);
            nav.offsetMax = new Vector2(0, -120);
            var layout = nav.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.spacing = 0;

            view.navRecent = CreateNavButton(nav, "Recent", font);
            view.navMyFiles = CreateNavButton(nav, "My Files", font);
            view.navStorage = CreateNavButton(nav, "Storage", font, true);
            view.navPhotos = CreateNavButton(nav, "Photos", font);
            view.navMusic = CreateNavButton(nav, "Music", font);
            CreateSpacer(nav, 8);
            view.navSettings = CreateNavButton(nav, "Settings", font);
            view.navExit = CreateNavButton(nav, "Exit", font);

            return drawer;
        }

        private static GameObject CreateShell(Transform parent, Font font, AppShellView view)
        {
            var shell = CreatePanel(parent, "Shell", Bg);
            Stretch(shell.GetComponent<RectTransform>());

            var appBar = CreateRect(shell.transform, "AppBar");
            appBar.anchorMin = new Vector2(0, 1);
            appBar.anchorMax = new Vector2(1, 1);
            appBar.pivot = new Vector2(0.5f, 1);
            appBar.sizeDelta = new Vector2(0, AppBarHeight);
            var appBarImg = appBar.gameObject.AddComponent<Image>();
            appBarImg.color = Primary;
            appBarImg.raycastTarget = false;

            var titles = CreateRect(appBar, "Titles");
            titles.anchorMin = new Vector2(0, 0);
            titles.anchorMax = new Vector2(1, 1);
            titles.offsetMin = new Vector2(56, 4);
            titles.offsetMax = new Vector2(-152, -4);
            var titlesGroup = titles.gameObject.AddComponent<CanvasGroup>();
            titlesGroup.blocksRaycasts = false;
            view.titleLabel = AddText(titles, "Storage", 18, Color.white, TextAnchor.MiddleLeft, Vector2.zero);
            view.subtitleLabel = AddText(titles, "", 12, new Color(1, 1, 1, 0.85f), TextAnchor.LowerLeft,
                new Vector2(0, 0));

            view.btnMenu = CreateBarButton(appBar, "≡", font);
            view.btnMenu.GetComponent<RectTransform>().anchorMin = new Vector2(0, 0.5f);
            view.btnMenu.GetComponent<RectTransform>().anchorMax = new Vector2(0, 0.5f);
            view.btnMenu.GetComponent<RectTransform>().anchoredPosition = new Vector2(28, 0);

            view.btnBack = CreateBarButton(appBar, "←", font);
            var backRt = view.btnBack.GetComponent<RectTransform>();
            backRt.anchorMin = new Vector2(0, 0.5f);
            backRt.anchorMax = new Vector2(0, 0.5f);
            backRt.anchoredPosition = new Vector2(28, 0);
            view.btnBack.gameObject.SetActive(false);

            view.btnOverflow = CreateAppBarActionButton(appBar, AppBarActionButtonVisual.IconOverflow,
                AppBarActionButtonVisual.CaptionOverflow, font);
            var overflowRt = view.btnOverflow.GetComponent<RectTransform>();
            overflowRt.anchorMin = new Vector2(1, 0.5f);
            overflowRt.anchorMax = new Vector2(1, 0.5f);
            overflowRt.anchoredPosition = new Vector2(-124, 0);

            view.btnBrowseFolder = CreateAppBarActionButton(appBar, AppBarActionButtonVisual.IconBrowse,
                AppBarActionButtonVisual.CaptionBrowse, font);
            var browseRt = view.btnBrowseFolder.GetComponent<RectTransform>();
            browseRt.anchorMin = new Vector2(1, 0.5f);
            browseRt.anchorMax = new Vector2(1, 0.5f);
            browseRt.anchoredPosition = new Vector2(-76, 0);

            view.btnSelectAll = CreateAppBarActionButton(appBar, AppBarSelectAllVisual.LabelNone,
                AppBarActionButtonVisual.CaptionSelectAll, font);
            var selectRt = view.btnSelectAll.GetComponent<RectTransform>();
            selectRt.anchorMin = new Vector2(1, 0.5f);
            selectRt.anchorMax = new Vector2(1, 0.5f);
            selectRt.anchoredPosition = new Vector2(-28, 0);

            view.btnSelectAll.transform.SetAsLastSibling();
            view.btnBrowseFolder.transform.SetAsLastSibling();
            view.btnOverflow.transform.SetAsLastSibling();
            view.btnBack.transform.SetAsLastSibling();
            view.btnMenu.transform.SetAsLastSibling();

            var breadcrumbRt = CreateRect(shell.transform, "Breadcrumb");
            breadcrumbRt.anchorMin = new Vector2(0, 1);
            breadcrumbRt.anchorMax = new Vector2(1, 1);
            breadcrumbRt.pivot = new Vector2(0.5f, 1);
            breadcrumbRt.anchoredPosition = new Vector2(0, -AppBarHeight);
            breadcrumbRt.sizeDelta = new Vector2(0, BreadcrumbHeight);
            breadcrumbRt.gameObject.AddComponent<Image>().color = Color.white;
            view.breadcrumb = AddText(breadcrumbRt, "Storage", 13, GrayText, TextAnchor.MiddleLeft, new Vector2(16, 0));

            var scrollGo = new GameObject("FileScroll", typeof(RectTransform));
            scrollGo.transform.SetParent(shell.transform, false);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(0, ActionBarHeight);
            scrollRt.offsetMax = new Vector2(0, -(AppBarHeight + BreadcrumbHeight));

            var scrollImg = scrollGo.AddComponent<Image>();
            scrollImg.color = Color.white;
            scrollImg.raycastTarget = false;
            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;

            var viewport = CreateRect(scrollRt, "Viewport");
            Stretch(viewport);
            viewport.gameObject.AddComponent<Image>().color = Color.white;
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            var content = CreateRect(viewport, "FileList");
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = new Vector2(0, content.offsetMin.y);
            content.offsetMax = new Vector2(0, content.offsetMax.y);
            content.sizeDelta = new Vector2(0, 0);
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childControlHeight = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;
            view.fileListContent = content;

            view.emptyLabel = CreateLabelOverlay(shell.transform, "EmptyLabel", "Folder is empty", font);
            view.emptyLabelText = view.emptyLabel.GetComponentInChildren<Text>();
            view.emptyLabel.SetActive(false);

            view.errorLabel = CreateLabelOverlay(shell.transform, "ErrorLabel", "", font);
            view.errorLabelText = view.errorLabel.GetComponentInChildren<Text>();
            view.errorLabel.GetComponentInChildren<Text>().color = new Color(0.83f, 0.18f, 0.18f);
            view.errorLabel.SetActive(false);

            view.loadingLabel = CreateLabelOverlay(shell.transform, "LoadingLabel", "Loading...", font);
            view.loadingLabel.SetActive(false);

            view.actionBar = CreateActionBar(shell.transform, font, view);

            breadcrumbRt.SetAsLastSibling();
            appBar.SetAsLastSibling();

            view.shell = shell;
            return shell;
        }

        private static GameObject CreateActionBar(Transform parent, Font font, AppShellView view)
        {
            var bar = CreatePanel(parent, "ActionBar", Color.white);
            var rt = bar.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.sizeDelta = new Vector2(0, ActionBarHeight);

            var hlg = bar.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = 8;
            hlg.padding = new RectOffset(8, 8, 4, 4);
            hlg.childForceExpandWidth = true;

            view.actUnzip = CreateActionButton(bar.transform, "Unzip", Primary, font);
            view.actCompress = CreateActionButton(bar.transform, "Compress", new Color(0.15f, 0.65f, 0.6f), font);
            view.actShare = CreateActionButton(bar.transform, "Share", new Color(0.93f, 0.93f, 0.93f), font,
                Color.black);
            view.actDelete = CreateActionButton(bar.transform, "Delete", new Color(0.93f, 0.93f, 0.93f), font,
                new Color(0.83f, 0.18f, 0.18f));
            view.actMore = CreateActionButton(bar.transform, "More", new Color(0.93f, 0.93f, 0.93f), font, Color.black);
            return bar;
        }

        private static GameObject CreateSettingsPanel(Transform parent, Font font, AppShellView view)
        {
            var panel = CreatePanel(parent, "SettingsPanel", Bg);
            Stretch(panel.GetComponent<RectTransform>());

            var appBar = CreateRect(panel.transform, "AppBar");
            appBar.anchorMin = new Vector2(0, 1);
            appBar.anchorMax = new Vector2(1, 1);
            appBar.pivot = new Vector2(0.5f, 1);
            appBar.sizeDelta = new Vector2(0, AppBarHeight);
            appBar.gameObject.AddComponent<Image>().color = Primary;
            view.settingsBack = CreateBarButton(appBar, "←", font);
            view.settingsBack.GetComponent<RectTransform>().anchoredPosition = new Vector2(28, 0);
            AddText(appBar, "Settings", 18, Color.white, TextAnchor.MiddleLeft, new Vector2(56, 0));

            var scrollGo = new GameObject("SettingsScroll", typeof(RectTransform));
            scrollGo.transform.SetParent(panel.transform, false);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            Stretch(scrollRt);
            scrollRt.offsetMax = new Vector2(0, -AppBarHeight);
            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            var viewport = CreateRect(scrollRt, "Viewport");
            Stretch(viewport);
            viewport.gameObject.AddComponent<Image>().color = Bg;
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var content = CreateRect(viewport, "Content");
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = new Vector2(0, 600);
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childControlHeight = true;
            vlg.childForceExpandHeight = false;
            scroll.viewport = viewport;
            scroll.content = content;

            AddGroupLabel(content, "COMPRESSION", font);
            view.setFormatZip = CreateSettingsRow(content, "Default format: ZIP", font);
            view.setFormatTgz = CreateSettingsRow(content, "Default format: TAR.GZ", font);
            view.setLevelNormal = CreateSettingsRow(content, "Level: Normal", font);
            view.setLevelMax = CreateSettingsRow(content, "Level: Maximum", font);
            AddGroupLabel(content, "GENERAL", font);
            view.setClearCache = CreateSettingsRow(content, "Clear local cache", font);
            AddGroupLabel(content, "ABOUT", font);
            CreateSettingsRow(content, "Version 0.1.0", font, false);

            return panel;
        }

        private static GameObject CreatePathPicker(Transform parent, Font font, AppShellView view)
        {
            var panel = CreatePanel(parent, "PathPicker", Bg);
            Stretch(panel.GetComponent<RectTransform>());

            var appBar = CreateRect(panel.transform, "AppBar");
            appBar.anchorMin = new Vector2(0, 1);
            appBar.anchorMax = new Vector2(1, 1);
            appBar.pivot = new Vector2(0.5f, 1);
            appBar.sizeDelta = new Vector2(0, AppBarHeight);
            appBar.gameObject.AddComponent<Image>().color = Primary;
            view.pickerBack = CreateBarButton(appBar, "←", font);
            var pickerBackRt = view.pickerBack.GetComponent<RectTransform>();
            pickerBackRt.anchorMin = new Vector2(0f, 0.5f);
            pickerBackRt.anchorMax = new Vector2(0f, 0.5f);
            pickerBackRt.pivot = new Vector2(0f, 0.5f);
            pickerBackRt.anchoredPosition = new Vector2(4f, 0f);
            view.pickerTitle = AddText(appBar, "Select folder", 18, Color.white, TextAnchor.MiddleLeft,
                new Vector2(56, 0));

            var crumbRt = CreateRect(panel.transform, "PickerBreadcrumb");
            crumbRt.anchorMin = new Vector2(0, 1);
            crumbRt.anchorMax = new Vector2(1, 1);
            crumbRt.pivot = new Vector2(0.5f, 1);
            crumbRt.anchoredPosition = new Vector2(0, -AppBarHeight);
            crumbRt.sizeDelta = new Vector2(0, BreadcrumbHeight);
            crumbRt.gameObject.AddComponent<Image>().color = Color.white;
            view.pickerBreadcrumb = AddText(crumbRt, "", 13, GrayText, TextAnchor.MiddleLeft, new Vector2(16, 0));

            var quickRootsRt = CreateRect(panel.transform, "PickerQuickRoots");
            quickRootsRt.anchorMin = new Vector2(0, 1);
            quickRootsRt.anchorMax = new Vector2(1, 1);
            quickRootsRt.pivot = new Vector2(0.5f, 1);
            quickRootsRt.anchoredPosition = new Vector2(0, -(AppBarHeight + BreadcrumbHeight));
            quickRootsRt.sizeDelta = new Vector2(0, 44);
            quickRootsRt.gameObject.AddComponent<Image>().color = new Color(0.94f, 0.94f, 0.94f);
            var quickHlg = quickRootsRt.gameObject.AddComponent<HorizontalLayoutGroup>();
            quickHlg.padding = new RectOffset(8, 8, 6, 6);
            quickHlg.spacing = 8;
            quickHlg.childAlignment = TextAnchor.MiddleLeft;
            quickHlg.childControlWidth = true;
            quickHlg.childControlHeight = true;
            quickHlg.childForceExpandWidth = false;
            quickHlg.childForceExpandHeight = false;
            view.pickerQuickRoots = quickRootsRt;

            var scrollGo = new GameObject("PickerScroll", typeof(RectTransform));
            scrollGo.transform.SetParent(panel.transform, false);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(0, 80);
            scrollRt.offsetMax = new Vector2(0, -(AppBarHeight + BreadcrumbHeight + 44));
            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            var viewport = CreateRect(scrollRt, "Viewport");
            Stretch(viewport);
            viewport.gameObject.AddComponent<Image>().color = Color.white;
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var content = CreateRect(viewport, "PickerList");
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = new Vector2(0, 0);
            var listVlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            listVlg.childControlWidth = true;
            listVlg.childForceExpandWidth = true;
            listVlg.childControlHeight = true;
            listVlg.childForceExpandHeight = false;
            listVlg.spacing = 1;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            view.pickerListContent = content;

            var confirmGo = CreatePanel(panel.transform, "Confirm", Primary);
            var confirmRt = confirmGo.GetComponent<RectTransform>();
            confirmRt.anchorMin = new Vector2(0, 0);
            confirmRt.anchorMax = new Vector2(0.5f, 0);
            confirmRt.pivot = new Vector2(0.5f, 0);
            confirmRt.sizeDelta = new Vector2(-24, 48);
            confirmRt.anchoredPosition = new Vector2(0, 16);
            view.pickerConfirm = confirmGo.AddComponent<Button>();
            view.pickerConfirmLabel = AddText(confirmGo.transform, "Confirm Here", 16, Color.white,
                TextAnchor.MiddleCenter, Vector2.zero);

            var nativeGo = CreatePanel(panel.transform, "NativeBrowse", Color.white);
            var nativeRt = nativeGo.GetComponent<RectTransform>();
            nativeRt.anchorMin = new Vector2(0.5f, 0);
            nativeRt.anchorMax = new Vector2(1, 0);
            nativeRt.pivot = new Vector2(0.5f, 0);
            nativeRt.sizeDelta = new Vector2(-24, 48);
            nativeRt.anchoredPosition = new Vector2(0, 16);
            view.pickerNativeBrowse = nativeGo.AddComponent<Button>();
            var nativeImg = nativeGo.GetComponent<Image>();
            nativeImg.color = new Color(0.93f, 0.93f, 0.93f);
            view.pickerNativeBrowse.targetGraphic = nativeImg;
            AddText(nativeGo.transform, "系统浏览...", 16, Primary, TextAnchor.MiddleCenter, Vector2.zero)
                .raycastTarget = false;

            return panel;
        }

        private static GameObject CreateDialog(Transform parent, Font font, AppShellView view)
        {
            var overlay = CreatePanel(parent, "DialogOverlay", new Color(0, 0, 0, 0.5f));
            Stretch(overlay.GetComponent<RectTransform>());

            var dialog = CreatePanel(overlay.transform, "Dialog", Color.white);
            var dialogRt = dialog.GetComponent<RectTransform>();
            dialogRt.anchorMin = new Vector2(0.5f, 0.5f);
            dialogRt.anchorMax = new Vector2(0.5f, 0.5f);
            dialogRt.sizeDelta = new Vector2(320, 280);
            dialog.transform.SetAsLastSibling();

            var vlg = dialog.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(16, 16, 16, 16);
            vlg.spacing = 8;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            view.dialogTitle = CreateLayoutText(dialog.transform, "Dialog", 18, Color.black, font, 28);
            view.dialogMessage = CreateLayoutText(dialog.transform, "", 14, GrayText, font, 68);
            view.dialogMessage.horizontalOverflow = HorizontalWrapMode.Wrap;
            view.dialogMessage.verticalOverflow = VerticalWrapMode.Overflow;
            view.dialogMessage.lineSpacing = 1.15f;

            var inputRowGo = new GameObject("DialogInput", typeof(RectTransform));
            inputRowGo.transform.SetParent(dialog.transform, false);
            var inputRowRt = inputRowGo.GetComponent<RectTransform>();
            inputRowRt.anchorMin = new Vector2(0, 0.5f);
            inputRowRt.anchorMax = new Vector2(1, 0.5f);
            inputRowRt.pivot = new Vector2(0.5f, 0.5f);
            inputRowRt.sizeDelta = new Vector2(0, 36);
            var inputRowLayout = inputRowGo.AddComponent<LayoutElement>();
            inputRowLayout.preferredHeight = 36;
            inputRowLayout.flexibleWidth = 1;
            var inputRowImg = inputRowGo.AddComponent<Image>();
            inputRowImg.color = new Color(0.95f, 0.95f, 0.95f);
            inputRowImg.raycastTarget = false;

            view.dialogInput = CreateDialogInputField(inputRowGo.transform, font);

            var formatGo = new GameObject("DialogFormatSelector", typeof(RectTransform));
            formatGo.transform.SetParent(dialog.transform, false);
            formatGo.AddComponent<LayoutElement>().preferredHeight = 64;
            view.dialogFormatSelector = formatGo.AddComponent<ArchiveFormatSelector>();
            view.dialogFormatSelector.Build(font);

            var buttons = new GameObject("Buttons", typeof(RectTransform));
            buttons.transform.SetParent(dialog.transform, false);
            buttons.AddComponent<LayoutElement>().preferredHeight = 40;
            var hlg = buttons.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8;
            hlg.childForceExpandWidth = true;
            view.dialogCancel = CreateActionButton(buttons.transform, "Cancel", new Color(0.93f, 0.93f, 0.93f), font,
                Color.black);
            view.dialogOk = CreateActionButton(buttons.transform, "OK", Primary, font);

            return overlay;
        }

        private static InputField CreateDialogInputField(Transform parent, Font font)
        {
            var inputGo = new GameObject("Input", typeof(RectTransform));
            inputGo.transform.SetParent(parent, false);
            Stretch(inputGo.GetComponent<RectTransform>());

            var inputImg = inputGo.AddComponent<Image>();
            inputImg.color = Color.white;
            inputImg.raycastTarget = true;

            var field = inputGo.AddComponent<InputField>();
            field.targetGraphic = inputImg;
            field.lineType = InputField.LineType.SingleLine;
            inputGo.AddComponent<InputFieldInputSystemBridge>();

            var inputText = CreateInputText(inputGo.transform, "", 14, Color.black, font);
            field.textComponent = inputText;
            field.text = "";
            field.placeholder = CreateInputPlaceholder(inputGo.transform, "输入名称", font);

            return field;
        }

        private static Text CreateInputText(Transform parent, string text, int size, Color color, Font font)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>());
            var rt = go.GetComponent<RectTransform>();
            rt.offsetMin = new Vector2(8, 0);
            rt.offsetMax = new Vector2(-8, 0);
            var label = go.AddComponent<Text>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAnchor.MiddleLeft;
            label.supportRichText = false;
            label.raycastTarget = false;
            return label;
        }

        private static Text CreateInputPlaceholder(Transform parent, string text, Font font)
        {
            var go = new GameObject("Placeholder", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>());
            var rt = go.GetComponent<RectTransform>();
            rt.offsetMin = new Vector2(8, 0);
            rt.offsetMax = new Vector2(-8, 0);
            var label = go.AddComponent<Text>();
            label.font = font;
            label.text = text;
            label.fontSize = 14;
            label.color = new Color(0.6f, 0.6f, 0.6f);
            label.fontStyle = FontStyle.Italic;
            label.alignment = TextAnchor.MiddleLeft;
            label.raycastTarget = false;
            return label;
        }

        private static GameObject CreateToast(Transform parent, Font font, AppShellView view)
        {
            var toast = CreatePanel(parent, "Toast", new Color(0.2f, 0.2f, 0.2f, 0.9f));
            var rt = toast.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0);
            rt.anchorMax = new Vector2(0.5f, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.sizeDelta = new Vector2(400, 48);
            rt.anchoredPosition = new Vector2(0, 80);
            view.toastLabel = AddText(toast.transform, "", 14, Color.white, TextAnchor.MiddleCenter, Vector2.zero);
            return toast;
        }

        private static GameObject CreatePanel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = color;
            return go;
        }

        private static RectTransform CreateRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static Text AddText(Transform parent, string text, int size, Color color, TextAnchor anchor,
            Vector2 pos)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            if (pos == Vector2.zero)
            {
                Stretch(rt);
            }
            else if (anchor == TextAnchor.LowerLeft || anchor == TextAnchor.LowerRight)
            {
                rt.anchorMin = new Vector2(0, 0);
                rt.anchorMax = new Vector2(1, 0);
                rt.pivot = new Vector2(0, 0);
                rt.anchoredPosition = pos;
                rt.sizeDelta = new Vector2(0, size + 8);
            }
            else
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(pos.x, 0);
                rt.offsetMax = Vector2.zero;
            }

            var label = go.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = anchor;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }

        private static Text CreateLayoutText(Transform parent, string text, int size, Color color, Font font,
            float height)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<LayoutElement>().preferredHeight = height;
            var label = go.AddComponent<Text>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAnchor.MiddleLeft;
            label.raycastTarget = false;
            return label;
        }

        private static Button CreateNavButton(Transform parent, string label, Font font, bool active = false)
        {
            var go = new GameObject(label, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.sizeDelta = new Vector2(0, 48);
            go.AddComponent<LayoutElement>().preferredHeight = 48;
            var img = go.AddComponent<Image>();
            img.color = active ? new Color(0.88f, 0.94f, 1f) : Color.white;
            var btn = go.AddComponent<Button>();
            var text = AddText(go.transform, label, 15, active ? Primary : Color.black, TextAnchor.MiddleLeft,
                new Vector2(16, 0));
            text.raycastTarget = false;
            return btn;
        }

        private static Button CreateAppBarActionButton(Transform parent, string icon, string caption, Font font)
        {
            var go = new GameObject($"{icon}_{caption}", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(48, 48);
            var img = go.AddComponent<Image>();
            img.color = new Color(1, 1, 1, 0.01f);
            var btn = go.AddComponent<Button>();
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = btn.colors;
            colors.selectedColor = colors.normalColor;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.08f);
            btn.colors = colors;
            AppBarActionButtonVisual.Ensure(btn, icon, caption, font);
            return btn;
        }

        private static Button CreateBarButton(Transform parent, string label, Font font)
        {
            var go = new GameObject(label, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(48, 48);
            var img = go.AddComponent<Image>();
            img.color = new Color(1, 1, 1, 0.01f);
            var btn = go.AddComponent<Button>();
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = btn.colors;
            colors.selectedColor = colors.normalColor;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.08f);
            btn.colors = colors;
            AddText(go.transform, label, 20, Color.white, TextAnchor.MiddleCenter, Vector2.zero).raycastTarget = false;
            return btn;
        }

        private static Button CreateActionButton(Transform parent, string label, Color bg, Font font,
            Color? textColor = null)
        {
            var go = new GameObject(label, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<LayoutElement>().preferredHeight = 40;
            go.AddComponent<Image>().color = bg;
            var btn = go.AddComponent<Button>();
            AddText(go.transform, label, 14, textColor ?? Color.white, TextAnchor.MiddleCenter, Vector2.zero)
                .raycastTarget = false;
            return btn;
        }

        private static void CreateSpacer(Transform parent, float height)
        {
            var go = new GameObject("Spacer", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<LayoutElement>().preferredHeight = height;
        }

        private static void AddGroupLabel(Transform parent, string text, Font font)
        {
            var go = new GameObject(text, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<LayoutElement>().preferredHeight = 32;
            var label = go.AddComponent<Text>();
            label.font = font;
            label.text = text;
            label.fontSize = 12;
            label.color = GrayText;
            label.alignment = TextAnchor.MiddleLeft;
            go.GetComponent<RectTransform>().offsetMin = new Vector2(16, 0);
        }

        private static Button CreateSettingsRow(Transform parent, string text, Font font, bool interactive = true)
        {
            var go = new GameObject(text, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<LayoutElement>().preferredHeight = 56;
            go.AddComponent<Image>().color = Color.white;
            Button btn = null;
            if (interactive)
            {
                btn = go.AddComponent<Button>();
                btn.targetGraphic = go.GetComponent<Image>();
            }

            var label = AddText(go.transform, text, 15, interactive ? Color.black : GrayText, TextAnchor.MiddleLeft,
                new Vector2(16, 0));
            label.raycastTarget = false;
            return btn;
        }

        private static GameObject CreateLabelOverlay(Transform parent, string name, string text, Font font)
        {
            var go = CreatePanel(parent, name, new Color(1, 1, 1, 0));
            Stretch(go.GetComponent<RectTransform>());
            AddText(go.transform, text, 14, GrayText, TextAnchor.MiddleCenter, Vector2.zero);
            return go;
        }
    }
}
#endif
