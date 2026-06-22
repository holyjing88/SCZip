using System;
using SCZip.Core;
using SCZip.Domain;
using SCZip.Infrastructure;
using UnityEngine;
using UnityEngine.UI;

namespace SCZip.UI
{
    /// <summary>
    /// Two-option archive format picker (ZIP / TAR.GZ). Avoids uGUI Dropdown popup destroy issues with Input System.
    /// </summary>
    public sealed class ArchiveFormatSelector : MonoBehaviour
    {
        private static readonly Color Primary = new(0.082f, 0.396f, 0.753f, 1f);
        private static readonly Color IdleBg = new(0.93f, 0.93f, 0.93f, 1f);

        private Button _zipButton;
        private Button _tgzButton;
        private ArchiveFormat _selected = ArchiveFormat.Zip;

        public event Action<ArchiveFormat> FormatChanged;

        public ArchiveFormat SelectedFormat => _selected;

        public Button ZipButton => _zipButton;
        public Button TarGzButton => _tgzButton;

        public static ArchiveFormatSelector MigrateFromDropdown(Dropdown legacyDropdown, Font font)
        {
            if (legacyDropdown == null)
                return null;

            var parent = legacyDropdown.transform.parent;
            var index = legacyDropdown.transform.GetSiblingIndex();
            legacyDropdown.gameObject.SetActive(false);

            var go = new GameObject("DialogFormatSelector", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.SetSiblingIndex(index);

            var selector = go.AddComponent<ArchiveFormatSelector>();
            selector.Build(font);
            return selector;
        }

        public void Build(Font font)
        {
            if (_zipButton != null)
                return;

            var rootLe = gameObject.GetComponent<LayoutElement>();
            if (rootLe == null)
                rootLe = gameObject.AddComponent<LayoutElement>();
            rootLe.preferredHeight = 64;

            var rootVlg = gameObject.AddComponent<VerticalLayoutGroup>();
            rootVlg.spacing = 6;
            rootVlg.childControlWidth = true;
            rootVlg.childForceExpandWidth = true;
            rootVlg.childControlHeight = true;
            rootVlg.childForceExpandHeight = false;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(transform, false);
            labelGo.AddComponent<LayoutElement>().preferredHeight = 20;
            var label = labelGo.AddComponent<Text>();
            label.font = font;
            label.text = "压缩格式";
            label.fontSize = 14;
            label.color = new Color(0.2f, 0.2f, 0.2f);
            label.alignment = TextAnchor.MiddleLeft;
            label.raycastTarget = false;

            var rowGo = new GameObject("Buttons", typeof(RectTransform));
            rowGo.transform.SetParent(transform, false);
            rowGo.AddComponent<LayoutElement>().preferredHeight = 36;
            var hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8;
            hlg.childForceExpandWidth = true;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;

            _zipButton = CreateOptionButton(rowGo.transform, "ZIP (.zip)", font);
            _tgzButton = CreateOptionButton(rowGo.transform, "TAR.GZ (.tar.gz)", font);

            _zipButton.onClick.AddListener(() => SelectFormat(ArchiveFormat.Zip));
            _tgzButton.onClick.AddListener(() => SelectFormat(ArchiveFormat.TarGzip));

            ApplyVisuals();
        }

        public void SetFormat(ArchiveFormat format, bool notify = true)
        {
            if (!AppServices.FeatureGate.CanCreate(format) && format != ArchiveFormat.Zip)
                format = ArchiveFormat.Zip;

            var changed = _selected != format;
            _selected = format;
            ApplyVisuals();

            if (notify && changed)
                FormatChanged?.Invoke(_selected);
        }

        private void SelectFormat(ArchiveFormat format)
        {
            if (!AppServices.FeatureGate.CanCreate(format) && format == ArchiveFormat.TarGzip)
            {
                SetFormat(ArchiveFormat.Zip, true);
                return;
            }

            SetFormat(format, true);
        }

        private void ApplyVisuals()
        {
            if (_zipButton == null || _tgzButton == null)
                return;

            StyleButton(_zipButton, _selected == ArchiveFormat.Zip);
            StyleButton(_tgzButton, _selected == ArchiveFormat.TarGzip);
        }

        private static Button CreateOptionButton(Transform parent, string caption, Font font)
        {
            var go = new GameObject(caption, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<LayoutElement>().flexibleWidth = 1;
            var img = go.AddComponent<Image>();
            img.color = IdleBg;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var rt = textGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var text = textGo.AddComponent<Text>();
            text.font = font;
            text.text = caption;
            text.fontSize = 13;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return btn;
        }

        private static void StyleButton(Button button, bool selected)
        {
            var img = button.GetComponent<Image>();
            if (img != null)
                img.color = selected ? Primary : IdleBg;

            var text = button.GetComponentInChildren<Text>();
            if (text != null)
                text.color = selected ? Color.white : Color.black;
        }
    }
}
