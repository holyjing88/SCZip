using UnityEngine;
using UnityEngine.UI;

namespace SCZip.UI
{
    /// <summary>
    /// Enforces 720x1280 reference resolution on UICanvas for all platforms.
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class UiCanvasConfig : MonoBehaviour
    {
        public static readonly Vector2 ReferenceResolution = new(720, 1280);

        private void Awake()
        {
            var scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }
    }
}
