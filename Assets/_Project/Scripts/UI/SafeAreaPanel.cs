using UnityEngine;

namespace TankBrawl.UI
{
    /// <summary>
    /// Stretches this RectTransform over <c>Screen.safeArea</c> (display cutouts, rounded corners,
    /// gesture bars). Put it under a Canvas and place the UI inside it. Re-applies on rotation / resolution change.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaPanel : MonoBehaviour
    {
        RectTransform rectTransform;
        Rect appliedSafeArea;
        int appliedWidth;
        int appliedHeight;

        void Awake()
        {
            rectTransform = (RectTransform)transform;
        }

        void OnEnable()
        {
            Apply();
        }

        void Update()
        {
            if (Screen.safeArea != appliedSafeArea || Screen.width != appliedWidth || Screen.height != appliedHeight)
                Apply();
        }

        void Apply()
        {
            appliedSafeArea = Screen.safeArea;
            appliedWidth = Screen.width;
            appliedHeight = Screen.height;

            if (appliedWidth <= 0 || appliedHeight <= 0)
                return;

            rectTransform.anchorMin = new Vector2(appliedSafeArea.xMin / appliedWidth, appliedSafeArea.yMin / appliedHeight);
            rectTransform.anchorMax = new Vector2(appliedSafeArea.xMax / appliedWidth, appliedSafeArea.yMax / appliedHeight);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }
}
