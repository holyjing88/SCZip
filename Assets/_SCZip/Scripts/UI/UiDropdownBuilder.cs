using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace SCZip.UI
{
    /// <summary>Builds a standard uGUI Dropdown (caption, arrow, template) when scene assets are incomplete.</summary>
    public static class UiDropdownBuilder
    {
        private const float ItemHeight = 32f;
        private const float MaxVisibleItems = 6f;
        private const float ScrollbarWidth = 18f;

        public static void Ensure(Dropdown dropdown, Font font, IReadOnlyList<string> optionLabels,
            Color? textColor = null)
        {
            if (dropdown == null || font == null)
                return;

            var color = textColor ?? Color.black;

            if (optionLabels != null && optionLabels.Count > 0)
                dropdown.options = optionLabels.Select(label => new Dropdown.OptionData(label)).ToList();

            if (dropdown.captionText == null)
            {
                var labelGo = new GameObject("Label", typeof(RectTransform));
                labelGo.transform.SetParent(dropdown.transform, false);
                var labelRt = labelGo.GetComponent<RectTransform>();
                labelRt.anchorMin = Vector2.zero;
                labelRt.anchorMax = Vector2.one;
                labelRt.offsetMin = new Vector2(12f, 2f);
                labelRt.offsetMax = new Vector2(-28f, -2f);
                var label = labelGo.AddComponent<Text>();
                label.font = font;
                label.fontSize = 14;
                label.color = color;
                label.alignment = TextAnchor.MiddleLeft;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.raycastTarget = false;
                dropdown.captionText = label;
            }

            if (dropdown.transform.Find("Arrow") == null)
            {
                var arrowGo = new GameObject("Arrow", typeof(RectTransform));
                arrowGo.transform.SetParent(dropdown.transform, false);
                var arrowRt = arrowGo.GetComponent<RectTransform>();
                arrowRt.anchorMin = new Vector2(1f, 0.5f);
                arrowRt.anchorMax = new Vector2(1f, 0.5f);
                arrowRt.pivot = new Vector2(1f, 0.5f);
                arrowRt.anchoredPosition = new Vector2(-10f, 0f);
                arrowRt.sizeDelta = new Vector2(16f, 16f);
                var arrow = arrowGo.AddComponent<Text>();
                arrow.font = font;
                arrow.text = "▼";
                arrow.fontSize = 12;
                arrow.color = color;
                arrow.alignment = TextAnchor.MiddleCenter;
                arrow.raycastTarget = false;
            }

            if (dropdown.template == null)
            {
                dropdown.template = CreateTemplate(dropdown.transform, font, color, out var itemText);
                dropdown.itemText = itemText;
            }
            else if (dropdown.itemText == null)
            {
                var itemLabel = dropdown.template.GetComponentInChildren<Text>(true);
                if (itemLabel != null)
                    dropdown.itemText = itemLabel;
            }

            EnsureScrollableTemplate(dropdown.template, font, color);

            var graphic = dropdown.GetComponent<Image>();
            if (graphic != null && dropdown.targetGraphic == null)
                dropdown.targetGraphic = graphic;

            dropdown.RefreshShownValue();
        }

        private static void EnsureScrollableTemplate(RectTransform template, Font font, Color textColor)
        {
            if (template == null)
                return;

            template.sizeDelta = new Vector2(0f, ItemHeight * MaxVisibleItems);

            var scrollRect = template.GetComponentInChildren<ScrollRect>(true);
            if (scrollRect == null)
            {
                var scrollGo = new GameObject("Scroll", typeof(RectTransform));
                scrollGo.transform.SetParent(template, false);
                Stretch(scrollGo.GetComponent<RectTransform>());
                scrollRect = scrollGo.AddComponent<ScrollRect>();
            }

            scrollRect.horizontal = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 24f;

            var viewport = scrollRect.viewport != null
                ? scrollRect.viewport
                : scrollRect.transform.Find("Viewport") as RectTransform;
            if (viewport == null)
            {
                var viewportGo = new GameObject("Viewport", typeof(RectTransform));
                viewportGo.transform.SetParent(scrollRect.transform, false);
                viewport = viewportGo.GetComponent<RectTransform>();
                Stretch(viewport);
                var viewportImage = viewportGo.GetComponent<Image>() ?? viewportGo.AddComponent<Image>();
                viewportImage.color = Color.white;
                var mask = viewportGo.GetComponent<Mask>() ?? viewportGo.AddComponent<Mask>();
                mask.showMaskGraphic = false;
            }

            viewport.offsetMax = new Vector2(-ScrollbarWidth, 0f);
            scrollRect.viewport = viewport;

            var content = scrollRect.content != null
                ? scrollRect.content
                : viewport.Find("Content") as RectTransform;
            if (content == null)
            {
                var contentGo = new GameObject("Content", typeof(RectTransform));
                contentGo.transform.SetParent(viewport, false);
                content = contentGo.GetComponent<RectTransform>();
                content.anchorMin = new Vector2(0f, 1f);
                content.anchorMax = new Vector2(1f, 1f);
                content.pivot = new Vector2(0.5f, 1f);
                content.anchoredPosition = Vector2.zero;
                content.sizeDelta = new Vector2(0f, ItemHeight);
            }

            var layout = content.GetComponent<VerticalLayoutGroup>() ?? content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = content.GetComponent<ContentSizeFitter>() ??
                         content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = content;

            var scrollbar = scrollRect.verticalScrollbar != null
                ? scrollRect.verticalScrollbar
                : CreateVerticalScrollbar(scrollRect.transform, font);
            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        }

        private static Scrollbar CreateVerticalScrollbar(Transform parent, Font font)
        {
            var scrollbarGo = new GameObject("Scrollbar Vertical", typeof(RectTransform));
            scrollbarGo.transform.SetParent(parent, false);
            var scrollbarRt = scrollbarGo.GetComponent<RectTransform>();
            scrollbarRt.anchorMin = new Vector2(1f, 0f);
            scrollbarRt.anchorMax = new Vector2(1f, 1f);
            scrollbarRt.pivot = new Vector2(1f, 1f);
            scrollbarRt.anchoredPosition = Vector2.zero;
            scrollbarRt.sizeDelta = new Vector2(ScrollbarWidth, 0f);

            var trackImage = scrollbarGo.AddComponent<Image>();
            trackImage.color = new Color(0.9f, 0.9f, 0.9f);

            var scrollbar = scrollbarGo.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            var slidingAreaGo = new GameObject("Sliding Area", typeof(RectTransform));
            slidingAreaGo.transform.SetParent(scrollbarGo.transform, false);
            Stretch(slidingAreaGo.GetComponent<RectTransform>());

            var handleGo = new GameObject("Handle", typeof(RectTransform));
            handleGo.transform.SetParent(slidingAreaGo.transform, false);
            var handleRt = handleGo.GetComponent<RectTransform>();
            handleRt.anchorMin = Vector2.zero;
            handleRt.anchorMax = Vector2.one;
            handleRt.offsetMin = new Vector2(4f, 4f);
            handleRt.offsetMax = new Vector2(-4f, -4f);

            var handleImage = handleGo.AddComponent<Image>();
            handleImage.color = new Color(0.55f, 0.55f, 0.55f);
            scrollbar.handleRect = handleRt;
            scrollbar.targetGraphic = handleImage;

            return scrollbar;
        }

        private static RectTransform CreateTemplate(Transform parent, Font font, Color textColor, out Text itemText)
        {
            var template = new GameObject("Template", typeof(RectTransform));
            template.transform.SetParent(parent, false);
            var templateRt = template.GetComponent<RectTransform>();
            templateRt.anchorMin = new Vector2(0f, 0f);
            templateRt.anchorMax = new Vector2(1f, 0f);
            templateRt.pivot = new Vector2(0.5f, 1f);
            templateRt.anchoredPosition = new Vector2(0f, 2f);
            templateRt.sizeDelta = new Vector2(0f, ItemHeight * MaxVisibleItems);
            template.SetActive(false);

            var scrollGo = new GameObject("Scroll", typeof(RectTransform));
            scrollGo.transform.SetParent(template.transform, false);
            Stretch(scrollGo.GetComponent<RectTransform>());
            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var viewportRt = viewportGo.GetComponent<RectTransform>();
            Stretch(viewportRt);
            viewportRt.offsetMax = new Vector2(-ScrollbarWidth, 0f);
            viewportGo.AddComponent<Image>().color = Color.white;
            viewportGo.AddComponent<Mask>().showMaskGraphic = false;
            scroll.viewport = viewportRt;

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewportGo.transform, false);
            var contentRt = contentGo.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = new Vector2(0f, ItemHeight);
            var contentLayout = contentGo.AddComponent<VerticalLayoutGroup>();
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            var contentFitter = contentGo.AddComponent<ContentSizeFitter>();
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = contentRt;

            var itemGo = new GameObject("Item", typeof(RectTransform));
            itemGo.transform.SetParent(contentGo.transform, false);
            var itemRt = itemGo.GetComponent<RectTransform>();
            itemRt.anchorMin = new Vector2(0f, 0.5f);
            itemRt.anchorMax = new Vector2(1f, 0.5f);
            itemRt.sizeDelta = new Vector2(0f, ItemHeight);
            var itemLayout = itemGo.AddComponent<LayoutElement>();
            itemLayout.preferredHeight = ItemHeight;
            var toggle = itemGo.AddComponent<Toggle>();
            var itemBg = itemGo.AddComponent<Image>();
            itemBg.color = new Color(0.95f, 0.95f, 0.95f);
            toggle.targetGraphic = itemBg;
            toggle.isOn = true;

            var itemLabelGo = new GameObject("Item Label", typeof(RectTransform));
            itemLabelGo.transform.SetParent(itemGo.transform, false);
            var itemLabelRt = itemLabelGo.GetComponent<RectTransform>();
            itemLabelRt.anchorMin = Vector2.zero;
            itemLabelRt.anchorMax = Vector2.one;
            itemLabelRt.offsetMin = new Vector2(12f, 1f);
            itemLabelRt.offsetMax = new Vector2(-12f, -1f);
            itemText = itemLabelGo.AddComponent<Text>();
            itemText.font = font;
            itemText.fontSize = 14;
            itemText.color = textColor;
            itemText.alignment = TextAnchor.MiddleLeft;
            itemText.raycastTarget = true;

            scroll.verticalScrollbar = CreateVerticalScrollbar(scrollGo.transform, font);
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

            return templateRt;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
