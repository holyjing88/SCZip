using UnityEngine;

namespace SCZip.UI
{
    /// <summary>
    /// Chinese caption font bundled under Resources/Fonts (Noto Sans SC).
    /// </summary>
    public static class AppBarCaptionFont
    {
        private const string ResourcePath = "Fonts/NotoSansSC-Regular";

        private static Font _cached;

        public static Font Get(Font fallback = null)
        {
            if (_cached != null)
                return _cached;

            _cached = Resources.Load<Font>(ResourcePath);
            if (_cached == null)
            {
                UnityEngine.Debug.LogWarning(
                    "[SCZip] Caption font missing at Resources/Fonts/NotoSansSC-Regular — using fallback");
            }

            return _cached != null
                ? _cached
                : fallback ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
