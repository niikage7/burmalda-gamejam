using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TankBrawl.UI
{
    /// <summary>
    /// The chunky cartoon plate of the menu style (tanks_art_style.html), drawn as a mesh so it needs no sprite and stays crisp
    /// at any size: a thick near-black outline, a flat body colour with a light band on top and a darker "face" at the bottom,
    /// and a hard drop shadow under it (<see cref="Depth"/>). A radius of at least half the size makes a circle or a pill.
    /// The graphic's <see cref="Graphic.color"/> is the body. The drop shadow is drawn below the rectangle, so leave room for it.
    /// Pressing moves the plate down towards its shadow (<see cref="Pressed"/>); <see cref="UiButton"/> does that.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class UiPlate : MaskableGraphic
    {
        public static readonly Color Ink = new Color(0.078f, 0.063f, 0.059f, 1f);

        const float PressShare = 0.75f;
        const float DisabledGray = 0.8f;
        const float DisabledDark = 0.86f;
        const float MinPointGap = 0.01f;

        static readonly List<Vector2> Outer = new List<Vector2>(96);
        static readonly List<Vector2> Inner = new List<Vector2>(96);
        static readonly List<Vector2> Moved = new List<Vector2>(96);
        static readonly List<Vector2> Clipped = new List<Vector2>(128);
        static readonly List<Vector2> Scratch = new List<Vector2>(128);

        float radius = 14f;
        float outline = 4f;
        float depth;
        float faceHeight;
        float hiHeight;
        Color hiColor = new Color(1f, 1f, 1f, 0.35f);
        Color faceColor = new Color(0f, 0f, 0f, 0.25f);
        Color outlineColor = Ink;
        float pressed;
        bool disabled;

        public float Radius
        {
            get => radius;
            set => Set(ref radius, value);
        }

        public float OutlineWidth
        {
            get => outline;
            set => Set(ref outline, value);
        }

        /// <summary>How far the hard shadow sticks out below the plate, canvas units. 0 = no shadow.</summary>
        public float Depth
        {
            get => depth;
            set => Set(ref depth, value);
        }

        /// <summary>Height of the darker band at the bottom of the body. 0 = none.</summary>
        public float FaceHeight
        {
            get => faceHeight;
            set => Set(ref faceHeight, value);
        }

        /// <summary>Height of the light band at the top of the body. 0 = none.</summary>
        public float HiHeight
        {
            get => hiHeight;
            set => Set(ref hiHeight, value);
        }

        public Color HiColor
        {
            get => hiColor;
            set => Set(ref hiColor, value);
        }

        public Color FaceColor
        {
            get => faceColor;
            set => Set(ref faceColor, value);
        }

        public Color OutlineColor
        {
            get => outlineColor;
            set => Set(ref outlineColor, value);
        }

        /// <summary>0 = standing, 1 = pushed down into its shadow.</summary>
        public float Pressed
        {
            get => pressed;
            set => Set(ref pressed, Mathf.Clamp01(value));
        }

        /// <summary>A greyed-out look for a button that cannot be used.</summary>
        public bool Disabled
        {
            get => disabled;
            set
            {
                if (disabled == value)
                    return;

                disabled = value;
                SetVerticesDirty();
            }
        }

        /// <summary>How far the face is pushed down right now, canvas units (content on the plate follows it).</summary>
        public float PressOffset => pressed * depth * PressShare;

        public static UiPlate Create(Transform parent, string name)
        {
            RectTransform rect = UiKit.CreateRect(name, parent);
            var plate = rect.gameObject.AddComponent<UiPlate>();
            plate.raycastTarget = false;
            return plate;
        }

        public void SetTone(UiStyle.Tone tone)
        {
            color = tone.Body;
            hiColor = tone.Hi;
            faceColor = tone.Face;
            SetVerticesDirty();
        }

        void Set(ref float field, float value)
        {
            if (Mathf.Approximately(field, value))
                return;

            field = value;
            SetVerticesDirty();
        }

        void Set(ref Color field, Color value)
        {
            if (field == value)
                return;

            field = value;
            SetVerticesDirty();
        }

        Color Look(Color source)
        {
            if (!disabled)
                return source;

            float grey = source.r * 0.3f + source.g * 0.59f + source.b * 0.11f;
            Color result = Color.Lerp(source, new Color(grey, grey, grey, source.a), DisabledGray) * DisabledDark;
            result.a = source.a;
            return result;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            Canvas owner = canvas;
            float fringe = owner != null && owner.scaleFactor > 0f ? 1f / owner.scaleFactor : 1f;
            float alpha = color.a;

            Color ink = outlineColor;
            ink.a *= alpha;
            if (depth > 0f)
            {
                BuildRoundedRect(Outer, rect, radius, 0f, -depth);
                AddPolygon(vh, Outer, ink, fringe);
            }

            float drop = PressOffset;
            if (outline > 0f)
            {
                BuildRoundedRect(Outer, rect, radius, 0f, -drop);
                AddPolygon(vh, Outer, ink, fringe);
            }

            BuildRoundedRect(Inner, rect, radius, outline, -drop);
            if (Inner.Count < 3)
                return;

            Color body = Look(color);
            if (faceHeight > 0f)
            {
                Color face = Look(faceColor);
                face.a *= alpha;
                AddPolygon(vh, Inner, face, fringe);

                // The body is the same shape pushed up by the face height, cut to the outline.
                BuildRoundedRect(Moved, rect, radius, outline, -drop + faceHeight);
                Intersect(Moved, Inner, Clipped);
                AddPolygon(vh, Clipped, body, 0f);
            }
            else
            {
                AddPolygon(vh, Inner, body, fringe);
            }

            if (hiHeight > 0f)
            {
                float top = rect.yMax - outline - drop;
                Clip(Inner, Clipped, 0f, -1f, -(top - hiHeight));
                Color hi = Look(hiColor);
                hi.a *= alpha;
                AddPolygon(vh, Clipped, hi, 0f);
            }
        }

        // Counter-clockwise outline of the rectangle shrunk by `inset`, moved by `dy`.
        static void BuildRoundedRect(List<Vector2> points, Rect rect, float cornerRadius, float inset, float dy)
        {
            points.Clear();
            float xMin = rect.xMin + inset;
            float xMax = rect.xMax - inset;
            float yMin = rect.yMin + inset + dy;
            float yMax = rect.yMax - inset + dy;
            if (xMax <= xMin || yMax <= yMin)
                return;

            float r = Mathf.Max(0f, cornerRadius - inset);
            r = Mathf.Min(r, Mathf.Min((xMax - xMin) * 0.5f, (yMax - yMin) * 0.5f));
            int segments = Mathf.Clamp(Mathf.CeilToInt(r / 5f), 3, 24);

            AddCorner(points, xMin + r, yMin + r, r, 180f, segments);
            AddCorner(points, xMax - r, yMin + r, r, 270f, segments);
            AddCorner(points, xMax - r, yMax - r, r, 0f, segments);
            AddCorner(points, xMin + r, yMax - r, r, 90f, segments);
            if (points.Count > 1 && (points[points.Count - 1] - points[0]).sqrMagnitude < MinPointGap * MinPointGap)
                points.RemoveAt(points.Count - 1);
        }

        static void AddCorner(List<Vector2> points, float centerX, float centerY, float r, float startAngle, int segments)
        {
            if (r < 0.05f)
            {
                AddPoint(points, new Vector2(centerX, centerY));
                return;
            }

            for (int i = 0; i <= segments; i++)
            {
                float angle = (startAngle + 90f * i / segments) * Mathf.Deg2Rad;
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

        // The part of convex polygon `subject` inside convex counter-clockwise polygon `clipper`.
        static void Intersect(List<Vector2> subject, List<Vector2> clipper, List<Vector2> result)
        {
            result.Clear();
            result.AddRange(subject);
            int count = clipper.Count;
            for (int i = 0; i < count && result.Count > 2; i++)
            {
                Vector2 p = clipper[i];
                Vector2 edge = clipper[(i + 1) % count] - p;
                // Inside is the left of the edge: cross(edge, point - p) >= 0, i.e. a*x + b*y <= c with the normal pointing out.
                float a = edge.y;
                float b = -edge.x;
                float c = a * p.x + b * p.y;

                Scratch.Clear();
                Clip(result, Scratch, a, b, c);
                result.Clear();
                result.AddRange(Scratch);
            }
        }

        // A convex counter-clockwise polygon as a triangle fan, plus a transparent rim `fringe` wide for smooth edges.
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

        static Vector2 EdgeNormal(Vector2 from, Vector2 to)
        {
            Vector2 d = to - from;
            float length = d.magnitude;
            return length > 0.0001f ? new Vector2(d.y, -d.x) / length : Vector2.zero;
        }
    }
}
