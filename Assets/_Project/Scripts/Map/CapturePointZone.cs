using System.Collections.Generic;
using UnityEngine;

namespace TankBrawl.Map
{
    /// <summary>
    /// The flag zone of the point-holding mode: a circle on the map. A tank counts as holding it when its centre is inside.
    /// Scoring belongs to <c>CapturePointMode</c>; bots read the position and radius to go there and stay.
    /// A scene without an active zone is a scene without that mode, so bots then ignore the point.
    /// The zone draws itself as a ring and a faint disc (placeholder art) tinted by <see cref="SetTint"/>.
    /// </summary>
    public class CapturePointZone : MonoBehaviour
    {
        const int RingSegments = 64;
        const int SortingOrder = 4;

        static readonly List<CapturePointZone> Active = new List<CapturePointZone>();

        [Tooltip("Radius of the zone, units (1 unit = 1D).")]
        [SerializeField, Min(0.5f)] float radius = 3f;

        [Tooltip("Colour of the ring while nobody holds the zone.")]
        [SerializeField] Color neutralColor = new Color(1f, 1f, 1f, 0.55f);

        LineRenderer ring;
        SpriteRenderer disc;
        Color tint;

        /// <summary>The first active zone in the scene, or null. The mode has one flag.</summary>
        public static CapturePointZone Primary => Active.Count > 0 ? Active[0] : null;

        public Vector2 Center => transform.position;

        public float Radius => radius;

        public bool Contains(Vector2 point) => (point - Center).sqrMagnitude <= radius * radius;

        /// <summary>Tints the ring and the disc (the owner's colour). Pass the neutral colour for "nobody".</summary>
        public void SetTint(Color color)
        {
            tint = color;
            ApplyTint();
        }

        public void SetNeutral()
        {
            SetTint(neutralColor);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Active.Clear();
        }

        void Awake()
        {
            BuildVisual();
            SetNeutral();
        }

        void OnEnable()
        {
            Active.Add(this);
        }

        void OnDisable()
        {
            Active.Remove(this);
        }

        void Update()
        {
            // A slow pulse so the zone reads as "active".
            if (ring != null)
            {
                float pulse = 0.9f + 0.1f * Mathf.Sin(Time.time * 3f);
                ring.widthMultiplier = 0.14f * pulse;
            }
        }

        void BuildVisual()
        {
            Shader shader = Shader.Find("Sprites/Default");
            var material = new Material(shader);

            var ringObject = new GameObject("Ring");
            ringObject.transform.SetParent(transform, false);
            ring = ringObject.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = RingSegments;
            ring.widthMultiplier = 0.14f;
            ring.sharedMaterial = material;
            ring.sortingOrder = SortingOrder;
            for (int i = 0; i < RingSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / RingSegments;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius);
            }

            var discObject = new GameObject("Disc");
            discObject.transform.SetParent(transform, false);
            discObject.transform.localScale = Vector3.one * radius * 2f;
            disc = discObject.AddComponent<SpriteRenderer>();
            disc.sprite = BuildDiscSprite();
            disc.sortingOrder = SortingOrder - 1;
        }

        // A soft round sprite generated once, so the zone needs no art file.
        static Sprite discSprite;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSprite()
        {
            discSprite = null;
        }

        static Sprite BuildDiscSprite()
        {
            if (discSprite != null)
                return discSprite;

            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Mathf.Sqrt((x - half + 0.5f) * (x - half + 0.5f) + (y - half + 0.5f) * (y - half + 0.5f)) / half;
                    float alpha = distance >= 1f ? 0f : Mathf.Lerp(0.55f, 1f, distance) * (1f - Mathf.Clamp01((distance - 0.9f) / 0.1f));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha) * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            discSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return discSprite;
        }

        void ApplyTint()
        {
            if (ring != null)
            {
                ring.startColor = tint;
                ring.endColor = tint;
            }

            if (disc != null)
                disc.color = new Color(tint.r, tint.g, tint.b, 0.16f);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
            Vector3 centre = transform.position;
            const int segments = 48;
            Vector3 previous = centre + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector3 next = centre + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }
}
