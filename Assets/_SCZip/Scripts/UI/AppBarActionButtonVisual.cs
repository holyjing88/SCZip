using UnityEngine;
using UnityEngine.UI;

namespace SCZip.UI
{
    /// <summary>
    /// AppBar icon buttons: glyph on top, functional caption underneath (LegacyRuntime-safe icons).
    /// </summary>
    public static class AppBarActionButtonVisual
    {
        public const string IconBrowse = "DIR";
        public const string IconOverflow = "+";

        public const string CaptionSelectAll = "全选";
        public const string CaptionBrowse = "浏览";
        public const string CaptionOverflow = "新建";

        private const int CaptionFontSize = 20;
        private const float CaptionAreaRatio = 0.58f;
        private static readonly Color CaptionColor = new(1f, 1f, 1f, 0.82f);
        private static readonly Color HitTargetColor = new(1f, 1f, 1f, 0.01f);

        public static void Ensure(Button button, string iconText, string caption, Font font = null)
        {
            if (button == null)
                return;

            font ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EnsureHitTarget(button);
            var icon = EnsureIcon(button, font);
            icon.text = iconText;
            EnsureCaption(button, caption, font);
        }

        public static Text GetIconText(Button button)
        {
            if (button == null)
                return null;

            MigrateLegacyLabel(button);
            return button.transform.Find("Icon")?.GetComponent<Text>();
        }

        public static void EnsureCaptionOnly(Button button, string caption, Font font = null)
        {
            if (button == null)
                return;

            font ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            MigrateLegacyLabel(button);
            EnsureCaption(button, caption, font);
        }

        public static void EnsureIconLayout(Button button, Font font = null)
        {
            if (button == null)
                return;

            font ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            MigrateLegacyLabel(button);
            EnsureIcon(button, font);
        }

        private static void EnsureHitTarget(Button button)
        {
            var rt = button.GetComponent<RectTransform>();
            if (rt != null)
                rt.sizeDelta = new Vector2(56f, 56f);

            var img = button.GetComponent<Image>();
            if (img != null)
            {
                img.raycastTarget = true;
                img.color = HitTargetColor;
            }

            button.navigation = new Navigation { mode = Navigation.Mode.None };
        }

        private static Text EnsureIcon(Button button, Font font)
        {
            MigrateLegacyLabel(button);

            var iconTransform = button.transform.Find("Icon");
            if (iconTransform == null)
            {
                var go = new GameObject("Icon", typeof(RectTransform));
                go.transform.SetParent(button.transform, false);
                iconTransform = go.transform;
            }

            var rt = iconTransform as RectTransform;
            rt.anchorMin = new Vector2(0f, CaptionAreaRatio);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(0f, 2f);
            rt.offsetMax = new Vector2(0f, -4f);

            var text = iconTransform.GetComponent<Text>();
            if (text == null)
                text = iconTransform.gameObject.AddComponent<Text>();

            text.font = font;
            text.fontSize = 16;
            text.color = Color.white;
            text.alignment = TextAnchor.UpperCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static Text EnsureCaption(Button button, string caption, Font font)
        {
            var captionTransform = button.transform.Find("Caption");
            if (captionTransform == null)
            {
                var go = new GameObject("Caption", typeof(RectTransform));
                go.transform.SetParent(button.transform, false);
                captionTransform = go.transform;
            }

            var rt = captionTransform as RectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, CaptionAreaRatio);
            rt.offsetMin = new Vector2(0f, 0f);
            rt.offsetMax = new Vector2(0f, 2f);

            var text = captionTransform.GetComponent<Text>();
            if (text == null)
                text = captionTransform.gameObject.AddComponent<Text>();

            text.font = AppBarCaptionFont.Get(font);
            text.fontSize = CaptionFontSize;
            text.color = CaptionColor;
            text.alignment = TextAnchor.LowerCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            text.text = caption;
            return text;
        }

        private static void MigrateLegacyLabel(Button button)
        {
            if (button.transform.Find("Icon") != null)
                return;

            foreach (Transform child in button.transform)
            {
                if (child.name is "Highlight" or "Caption")
                    continue;

                var legacy = child.GetComponent<Text>();
                if (legacy == null)
                    continue;

                child.name = "Icon";
                return;
            }
        }
    }
}
