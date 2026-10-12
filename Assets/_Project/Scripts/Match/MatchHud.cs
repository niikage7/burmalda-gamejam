using UnityEngine;
using UnityEngine.UI;

namespace TankBrawl.Match
{
    /// <summary>
    /// Score line and big banner, built by <see cref="MatchManager"/> in code (plain UI Text, no scene wiring).
    /// Below the touch controls (sorting order 100) and never catches touches.
    /// </summary>
    public class MatchHud : MonoBehaviour
    {
        const int SortingOrder = 90;

        MatchManager match;
        Text scoreLabel;
        Text bannerLabel;

        public static MatchHud Create(MatchManager owner)
        {
            var root = new GameObject("MatchHud");
            root.transform.SetParent(owner.transform, false);

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            var hud = root.AddComponent<MatchHud>();
            hud.match = owner;
            hud.scoreLabel = CreateLabel(root.transform, "Score", 38, FontStyle.Bold, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(1400f, 70f), TextAnchor.UpperCenter);
            hud.bannerLabel = CreateLabel(root.transform, "Banner", 96, FontStyle.Bold, new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(1600f, 300f), TextAnchor.MiddleCenter);
            return hud;
        }

        static Text CreateLabel(Transform parent, string objectName, int size, FontStyle style, Vector2 anchor, Vector2 position, Vector2 dimensions, TextAnchor alignment)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;

            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.text = string.Empty;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);
            return text;
        }

        // The mode hands out cached strings, so the comparison is a reference check and nothing is allocated.
        void LateUpdate()
        {
            IMatchMode mode = match != null ? match.Mode : null;
            if (mode == null)
                return;

            string score = mode.ScoreText ?? string.Empty;
            if (scoreLabel.text != score)
                scoreLabel.text = score;

            string banner = mode.BannerText ?? string.Empty;
            if (bannerLabel.text != banner)
                bannerLabel.text = banner;
        }
    }
}
