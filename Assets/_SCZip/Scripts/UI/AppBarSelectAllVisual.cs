using SCZip.ViewModels;
using UnityEngine;
using UnityEngine.UI;

namespace SCZip.UI
{
    /// <summary>
    /// AppBar select-all uses ASCII labels + a dedicated highlight image
    /// (LegacyRuntime font does not render ☑/☐ reliably; Button ColorTint must not drive state).
    /// </summary>
    public static class AppBarSelectAllVisual
    {
        public const string LabelNone = "[]";
        public const string LabelSome = "~";
        public const string LabelAll = "ALL";

        private static readonly Color HitTargetColor = new(1f, 1f, 1f, 0.01f);
        private static readonly Color ActiveColor = new(1f, 1f, 1f, 0.38f);
        private static readonly Color PartialColor = new(1f, 1f, 1f, 0.22f);
        private static readonly Color InactiveHighlight = new(1f, 1f, 1f, 0f);

        public static void Configure(Button button)
        {
            if (button == null)
                return;

            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = Color.white;
            colors.selectedColor = Color.white;
            button.colors = colors;

            var hit = button.GetComponent<Image>();
            if (hit != null)
            {
                hit.raycastTarget = true;
                hit.color = HitTargetColor;
            }

            var rt = button.GetComponent<RectTransform>();
            if (rt != null)
                rt.sizeDelta = new Vector2(56f, 56f);

            EnsureHighlight(button);
            AppBarActionButtonVisual.EnsureCaptionOnly(button, AppBarActionButtonVisual.CaptionSelectAll);
            AppBarActionButtonVisual.EnsureIconLayout(button);
        }

        public static Text GetIconText(Button button) => AppBarActionButtonVisual.GetIconText(button);

        public static void Apply(Button button, SelectAllState state)
        {
            if (button == null)
                return;

            Configure(button);

            var label = GetIconText(button);
            if (label != null)
            {
                label.fontStyle = state == SelectAllState.All ? FontStyle.Bold : FontStyle.Normal;
                label.text = state switch
                {
                    SelectAllState.All => LabelAll,
                    SelectAllState.Some => LabelSome,
                    _ => LabelNone
                };
            }

            var highlight = EnsureHighlight(button);
            if (highlight != null)
            {
                highlight.color = state switch
                {
                    SelectAllState.All => ActiveColor,
                    SelectAllState.Some => PartialColor,
                    _ => InactiveHighlight
                };
            }
        }

        public static Image EnsureHighlight(Button button)
        {
            if (button == null)
                return null;

            var existing = button.transform.Find("Highlight")?.GetComponent<Image>();
            if (existing != null)
                return existing;

            var go = new GameObject("Highlight", typeof(RectTransform));
            go.transform.SetParent(button.transform, false);
            go.transform.SetAsFirstSibling();

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            img.color = InactiveHighlight;
            return img;
        }

        public static float HighlightAlpha(Button button)
        {
            var highlight = button?.transform.Find("Highlight")?.GetComponent<Image>();
            return highlight != null ? highlight.color.a : 0f;
        }
    }
}
