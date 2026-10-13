using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TankBrawl.UI
{
    /// <summary>
    /// A flat cartoon plate drawn as a mesh, no sprite needed: rounded corners, slanted sides, a thick dark outline like
    /// the tanks and tiles have, an optional progress fill and a light band on top. The graphic's <see cref="Graphic.color"/>
    /// is the plate itself. Edges are smoothed with a one-pixel transparent fringe, so it stays crisp on any screen.
    /// The slanted sides stay inside the rectangle (the plate is narrowed by the slant).
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class UiShape : MaskableGraphic
    {
        const int CornerSegments = 4;
        const float GlossFrom = 0.1f;
        const float ShadeHeight = 0.24f;
        const float ShadeStrength = 0.9f;
        const float MinPointGap = 0.01f;

        static readonly List<Vector2> Outer = new List<Vector2>(32);
        static readonly List<Vector2> Inner = new List<Vector2>(32);
        static readonly List<Vector2> Clipped = new List<Vector2>(40);

        float radius = 10f;
        float slopeLeft;
        float slopeRight;
        float outlineWidth = 4f;
        Color outlineColor = new Color(0.07f, 0.08f, 0.13f, 1f);
        float gloss = 0.14f;
        float fillAmount = -1f;
        Color fillColor = Color.white;
        bool fillFromRight;

        /// <summary>Corner radius of the outer edge, canvas units.</summary>
        public float Radius
        {
            get => radius;
            set => Set(ref radius, value);
        }

        /// <summary>Slant of the left side: how far its top is shifted to the right per unit of height (negative = "\").</summary>
        public float SlopeLeft
        {
            get => slopeLeft;
            set => Set(ref slopeLeft, value);
        }

        /// <summary>Slant of the right side, same meaning as <see cref="SlopeLeft"/>.</summary>
        public float SlopeRight
        {
            get => slopeRight;
            set => Set(ref slopeRight, value);
        }

        public float OutlineWidth
        {
            get => outlineWidth;
            set => Set(ref outlineWidth, value);
        }

        public Color OutlineColor
        {
            get => outlineColor;
            set
            {
                if (outlineColor == value)
                    return;

                outlineColor = value;
                SetVerticesDirty();
            }
        }

        /// <summary>Strength of the light band on the upper part and of the shade at the bottom, 0 = flat.</summary>
        public float Gloss
        {
            get => gloss;
            set => Set(ref gloss, value);
        }

        /// <summary>Progress fill inside the outline, 0..1. Negative = no fill.</summary>
        public float FillAmount
        {
            get => fillAmount;
            set => Set(ref fillAmount, value);
        }

        public Color FillColor
        {
            get => fillColor;
            set
            {
                if (fillColor == value)
                    return;

                fillColor = value;
                SetVerticesDirty();
            }
        }

        /// <summary>The fill grows from the right edge to the left instead of left to right.</summary>
        public bool FillFromRight
        {
            get => fillFromRight;
            set
            {
                if (fillFromRight == value)
                    return;

                fillFromRight = value;
                SetVerticesDirty();
            }
        }

        public static UiShape Create(Transform parent, string name, Color color, float radius = 10f, float outline = 4f)
        {
            RectTransform rect = UiKit.CreateRect(name, parent);
            var shape = rect.gameObject.AddComponent<UiShape>();
            shape.color = color;
            shape.radius = radius;
            shape.outlineWidth = outline;
            shape.raycastTarget = false;
            return shape;
        }

        /// <summary>Both sides slanted the same way (a parallelogram).</summary>
        public void SetSlope(float slope)
        {
            SetSlope(slope, slope);
        }

        public void SetSlope(float left, float right)
        {
            slopeLeft = left;
            slopeRight = right;
            SetVerticesDirty();
        }

        void Set(ref float field, float value)
        {
            if (Mathf.Approximately(field, value))
                return;

            field = value;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            // One screen pixel in canvas units: the width of the smoothing fringe.
            Canvas owner = canvas;
            float fringe = owner != null && owner.scaleFactor > 0f ? 1f / owner.scaleFactor : 1f;

            Color back = color;
            if (outlineWidth > 0f)
            {
                Color edge = outlineColor;
                edge.a *= back.a;
                BuildOutline(Outer, rect, 0f);
                AddPolygon(vh, Outer, edge, fringe);
            }

            BuildOutline(Inner, rect, Mathf.Max(0f, outlineWidth));
            if (Inner.Count < 3)
                return;

            AddPolygon(vh, Inner, back, fringe);

            float centerY = rect.center.y;
            if (fillAmount > 0f)
            {
                Color fill = fillColor;
                fill.a *= back.a;
                if (fillAmount >= 0.999f)
                {
                    AddPolygon(vh, Inner, fill, fringe);
                }
                else
                {
                    // The cut is parallel to the slanted sides.
                    float leftPad = Mathf.Abs(slopeLeft) * rect.height * 0.5f;
                    float rightPad = Mathf.Abs(slopeRight) * rect.height * 0.5f;
                    float xMin = rect.xMin + leftPad + outlineWidth;
                    float xMax = rect.xMax - rightPad - outlineWidth;
                    float t = fillFromRight ? 1f - fillAmount : fillAmount;
                    float cutX = Mathf.Lerp(xMin, xMax, t);
                    float slope = Mathf.Lerp(slopeLeft, slopeRight, t);
                    float sign = fillFromRight ? -1f : 1f;
                    Clip(Inner, Clipped, sign, -sign * slope, sign * (cutX - slope * centerY));
                    AddPolygon(vh, Clipped, fill, fringe);
                }
            }

            if (gloss > 0f)
            {
                Clip(Inner, Clipped, 0f, -1f, -(centerY + rect.height * GlossFrom));
                AddPolygon(vh, Clipped, new Color(1f, 1f, 1f, gloss * back.a), 0f);

                Clip(Inner, Clipped, 0f, 1f, rect.yMin + rect.height * ShadeHeight);
                AddPolygon(vh, Clipped, new Color(0f, 0f, 0f, gloss * ShadeStrength * back.a), 0f);
            }
        }

        // The outline of the plate shrunk by `inset`, counter-clockwise from the bottom-left corner.
        void BuildOutline(List<Vector2> points, Rect rect, float inset)
        {
            points.Clear();
            float leftPad = Mathf.Abs(slopeLeft) * rect.height * 0.5f;
            float rightPad = Mathf.Abs(slopeRight) * rect.height * 0.5f;
            float spanMin = rect.xMin + leftPad;
            float span = rect.width - leftPad - rightPad;
            float xMin = spanMin + inset;
            float xMax = rect.xMax - rightPad - inset;
            float yMin = rect.yMin + inset;
            float yMax = rect.yMax - inset;
            if (span <= 0f || xMax <= xMin || yMax <= yMin)
                return;

            float r = Mathf.Max(0f, radius - inset);
            r = Mathf.Min(r, Mathf.Min((xMax - xMin) * 0.5f, (yMax - yMin) * 0.5f));

            AddCorner(points, xMin + r, yMin + r, r, 180f);
            AddCorner(points, xMax - r, yMin + r, r, 270f);
            AddCorner(points, xMax - r, yMax - r, r, 0f);
            AddCorner(points, xMin + r, yMax - r, r, 90f);
            if (points.Count > 1 && (points[points.Count - 1] - points[0]).sqrMagnitude < MinPointGap * MinPointGap)
                points.RemoveAt(points.Count - 1);

            // Slant: the further from the middle line, the bigger the shift; the amount blends from the left side to the right.
            float centerY = rect.center.y;
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 p = points[i];
                float u = Mathf.Clamp01((p.x - spanMin) / span);
                p.x += Mathf.Lerp(slopeLeft, slopeRight, u) * (p.y - centerY);
                points[i] = p;
            }
        }

        static void AddCorner(List<Vector2> points, float centerX, float centerY, float r, float startAngle)
        {
            if (r < 0.05f)
            {
                AddPoint(points, new Vector2(centerX, centerY));
                return;
            }

            for (int i = 0; i <= CornerSegments; i++)
            {
                float angle = (startAngle + 90f * i / CornerSegments) * Mathf.Deg2Rad;
                AddPoint(points, new Vector2(centerX + Mathf.Cos(angle) * r, centerY + Mathf.Sin(angle) * r));
            }
        }

        static void AddPoint(List<Vector2> points, Vector2 point)
        {
            if (points.Count > 0 && (points[points.Count - 1] - point).sqrMagnitude < MinPointGap * MinPointGap)
                return;

            points.Add(point);
        }

        // Keeps the part of a convex polygon where a*x + b*y <= c.
        static void Clip(List<Vector2> source, List<Vector2> result, float a, float b, float c)
        {
            result.Clear();
            int count = source.Count;
            for (int i = 0; i < count; i++)
            {
                Vector2 p = source[i];
                Vector2 q = source[(i + 1) % count];
                float dp = a * p.x + b * p.y - c;
                float dq = a * q.x + b * q.y - c;
                if (dp <= 0f)
                    AddPoint(result, p);
                if ((dp < 0f && dq > 0f) || (dp > 0f && dq < 0f))
                    AddPoint(result, Vector2.Lerp(p, q, dp / (dp - dq)));
            }

            if (result.Count > 1 && (result[result.Count - 1] - result[0]).sqrMagnitude < MinPointGap * MinPointGap)
                result.RemoveAt(result.Count - 1);
        }

        // A convex counter-clockwise polygon as a triangle fan, plus a transparent rim of width `fringe` around it.
        static void AddPolygon(VertexHelper vh, List<Vector2> points, Color color, float fringe)
        {
            int count = points.Count;
            if (count < 3)
                return;

            Color32 solid = color;
            int start = vh.currentVertCount;
            for (int i = 0; i < count; i++)
                vh.AddVert(points[i], solid, Vector4.zero);
            for (int i = 1; i < count - 1; i++)
                vh.AddTriangle(start, start + i, start + i + 1);

            if (fringe <= 0f)
                return;

            Color32 clear = new Color(color.r, color.g, color.b, 0f);
            int rim = vh.currentVertCount;
            for (int i = 0; i < count; i++)
            {
                Vector2 p = points[i];
                Vector2 before = EdgeNormal(points[(i + count - 1) % count], p);
                Vector2 after = EdgeNormal(p, points[(i + 1) % count]);
                Vector2 normal = before + after;
                float length = normal.magnitude;
                normal = length > 0.0001f ? normal / length : before;

                // Keep the rim one pixel wide at sharp corners too.
                float scale = 1f / Mathf.Max(0.5f, Vector2.Dot(normal, before));
                vh.AddVert(p + normal * (fringe * scale), clear, Vector4.zero);
            }

            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                vh.AddTriangle(start + i, start + next, rim + next);
                vh.AddTriangle(start + i, rim + next, rim + i);
            }
        }

        // Outward normal of an edge of a counter-clockwise polygon.
        static Vector2 EdgeNormal(Vector2 from, Vector2 to)
        {
            Vector2 d = to - from;
            float length = d.magnitude;
            return length > 0.0001f ? new Vector2(d.y, -d.x) / length : Vector2.zero;
        }
    }
}
