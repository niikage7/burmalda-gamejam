using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TankBrawl.UI
{
    /// <summary>
    /// White-on-black cartoon lettering: a thick outline made of copies of the text shifted in eight directions, plus an
    /// optional hard drop under it (the text-shadow stack of tanks_art_style.html). Put it next to a <see cref="Text"/>.
    /// Alpha follows the text, so a fading label fades with its outline.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public class UiTextOutline : BaseMeshEffect
    {
        const float DiagonalShare = 0.78f;

        static readonly List<UIVertex> Source = new List<UIVertex>(512);
        static readonly List<UIVertex> Output = new List<UIVertex>(4096);

        // 8 ring directions, unit length.
        static readonly Vector2[] Ring =
        {
            new Vector2(1f, 0f), new Vector2(-1f, 0f), new Vector2(0f, 1f), new Vector2(0f, -1f),
            new Vector2(DiagonalShare, DiagonalShare), new Vector2(-DiagonalShare, DiagonalShare),
            new Vector2(DiagonalShare, -DiagonalShare), new Vector2(-DiagonalShare, -DiagonalShare)
        };

        [SerializeField] float width = 3f;
        [SerializeField] float drop;
        [SerializeField] Color color = UiPlate.Ink;

        public float Width
        {
            get => width;
            set
            {
                if (Mathf.Approximately(width, value))
                    return;

                width = value;
                Refresh();
            }
        }

        public float Drop
        {
            get => drop;
            set
            {
                if (Mathf.Approximately(drop, value))
                    return;

                drop = value;
                Refresh();
            }
        }

        public Color Color
        {
            get => color;
            set
            {
                if (color == value)
                    return;

                color = value;
                Refresh();
            }
        }

        void Refresh()
        {
            if (graphic != null)
                graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0 || (width <= 0f && drop <= 0f))
                return;

            vh.GetUIVertexStream(Source);
            Output.Clear();

            // The drop first (it sits lowest), then the ring, then the text itself on top.
            if (drop > 0f)
            {
                AddCopy(new Vector2(0f, -drop));
                AddCopy(new Vector2(0f, -drop * 0.5f));
                AddCopy(new Vector2(width * 0.7f, -drop));
                AddCopy(new Vector2(-width * 0.7f, -drop));
            }

            if (width > 0f)
            {
                for (int i = 0; i < Ring.Length; i++)
                    AddCopy(Ring[i] * width);
            }

            Output.AddRange(Source);
            vh.Clear();
            vh.AddUIVertexTriangleStream(Output);
        }

        void AddCopy(Vector2 shift)
        {
            for (int i = 0; i < Source.Count; i++)
            {
                UIVertex vertex = Source[i];
                vertex.position += new Vector3(shift.x, shift.y, 0f);
                Color32 original = vertex.color;
                vertex.color = new Color32((byte)(color.r * 255f), (byte)(color.g * 255f), (byte)(color.b * 255f), (byte)(original.a * color.a));
                Output.Add(vertex);
            }
        }
    }
}
