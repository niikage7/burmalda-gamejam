using UnityEngine;

namespace TankBrawl.UI
{
    /// <summary>
    /// One-line placement of a rectangle by a corner or the centre, in canvas units. The name says which point of the parent the
    /// offsets are measured from and which point of the rectangle sits there (the same for both): <c>LT</c> = left top, offsets
    /// right and down; <c>RT</c> = right top, offsets left and down; <c>LB</c>, <c>RB</c> the same from the bottom; <c>Mid</c> =
    /// centre, offsets right and up; <c>TopMid</c> / <c>BottomMid</c> = middle of the top / bottom edge, offset right and down / up.
    /// Every call returns the rectangle.
    /// </summary>
    public static class UiLayout
    {
        public static RectTransform LT(RectTransform rect, float left, float top, float width, float height)
        {
            return Set(rect, new Vector2(0f, 1f), new Vector2(left, -top), width, height);
        }

        public static RectTransform RT(RectTransform rect, float right, float top, float width, float height)
        {
            return Set(rect, new Vector2(1f, 1f), new Vector2(-right, -top), width, height);
        }

        public static RectTransform LB(RectTransform rect, float left, float bottom, float width, float height)
        {
            return Set(rect, new Vector2(0f, 0f), new Vector2(left, bottom), width, height);
        }

        public static RectTransform RB(RectTransform rect, float right, float bottom, float width, float height)
        {
            return Set(rect, new Vector2(1f, 0f), new Vector2(-right, bottom), width, height);
        }

        public static RectTransform Mid(RectTransform rect, float x, float y, float width, float height)
        {
            return Set(rect, new Vector2(0.5f, 0.5f), new Vector2(x, y), width, height);
        }

        public static RectTransform TopMid(RectTransform rect, float x, float top, float width, float height)
        {
            return Set(rect, new Vector2(0.5f, 1f), new Vector2(x, -top), width, height);
        }

        public static RectTransform BottomMid(RectTransform rect, float x, float bottom, float width, float height)
        {
            return Set(rect, new Vector2(0.5f, 0f), new Vector2(x, bottom), width, height);
        }

        /// <summary>A rectangle whose centre is at (x, y) measured from the parent's left top corner, y down.</summary>
        public static RectTransform CenterFromLT(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        /// <summary>Stretches the rectangle over its parent, shrunk by the margins.</summary>
        public static RectTransform Fill(RectTransform rect, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        static RectTransform Set(RectTransform rect, Vector2 anchor, Vector2 position, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }
    }
}
