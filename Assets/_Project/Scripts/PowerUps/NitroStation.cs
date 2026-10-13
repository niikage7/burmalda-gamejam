using System.Collections.Generic;
using TankBrawl.Audio;
using TankBrawl.Core;
using TankBrawl.Tank;
using UnityEngine;

namespace TankBrawl.PowerUps
{
    /// <summary>
    /// A nitro charging point on the map (balance v0.2 §8), separate from the power-ups: a tank of any team that drives
    /// onto it gets a full nitro stock, then the point recharges for a while. A tank whose nitro is already full does not
    /// use it up. Needs no prefab: the trigger circle and the placeholder look (a pad and a core that grows back while
    /// recharging) are made in code. <c>MapBuilder</c> places these from the 'n' markers of a layout.
    /// </summary>
    [RequireComponent(typeof(CircleCollider2D))]
    public class NitroStation : MonoBehaviour
    {
        const int SortingOrder = 5;
        const int TextureSize = 64;

        // Nitro above this share counts as full.
        const float FullShare = 0.999f;

        static readonly List<NitroStation> Active = new List<NitroStation>();
        static Sprite discSprite;

        [Tooltip("Seconds until the point works again after it charged a tank (random in the range). Balance v0.2 §8: 12-16")]
        [SerializeField] Vector2 rechargeTime = new Vector2(12f, 16f);

        [Tooltip("Radius of the pad and of its trigger, units (1 unit = 1D).")]
        [SerializeField, Min(0.1f)] float radius = 0.45f;

        [SerializeField] Color readyColor = new Color(0.9f, 0.45f, 1f, 1f);
        [SerializeField] Color padColor = new Color(0.12f, 0.12f, 0.16f, 0.75f);

        SpriteRenderer pad;
        SpriteRenderer core;
        float readyAt;
        float rechargeStartedAt;

        /// <summary>Charging points of the scene. Read-only, for future bot use (index loop, no allocations).</summary>
        public static IReadOnlyList<NitroStation> All => Active;

        public bool IsReady => Time.time >= readyAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Active.Clear();
            discSprite = null;
        }

        /// <summary>Makes every charging point ready at once (a new round starts).</summary>
        public static void ResetAll()
        {
            for (int i = 0; i < Active.Count; i++)
                Active[i].readyAt = 0f;
        }

        void Awake()
        {
            var trigger = GetComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = radius;
            pad = CreateDisc("Pad", radius * 2f, SortingOrder);
            core = CreateDisc("Core", radius * 1.2f, SortingOrder + 1);
        }

        void OnEnable()
        {
            Active.Add(this);
            readyAt = 0f;
        }

        void OnDisable()
        {
            Active.Remove(this);
        }

        // Stay as well as Enter: a tank waiting on the pad is charged the moment the point is ready again.
        void OnTriggerEnter2D(Collider2D other)
        {
            TryCharge(other);
        }

        void OnTriggerStay2D(Collider2D other)
        {
            TryCharge(other);
        }

        void TryCharge(Collider2D other)
        {
            if (!IsReady)
                return;

            TankNitro nitro = other.GetComponentInParent<TankNitro>();
            if (nitro == null || nitro.Normalized >= FullShare)
                return;

            nitro.Refill();
            rechargeStartedAt = Time.time;
            readyAt = Time.time + Random.Range(rechargeTime.x, rechargeTime.y);
            AudioManager.PlayAt(Sfx.PowerUpPickup, transform.position);
            Fx.Burst(transform.position, readyColor, 12, 3.5f, 0.45f, 0.13f);
        }

        void Update()
        {
            // Ready: a bright pulsing core. Recharging: a grey core that grows back to full size.
            float scale;
            Color color;
            if (IsReady)
            {
                scale = 1f + 0.08f * Mathf.Sin(Time.time * 4f);
                color = readyColor;
            }
            else
            {
                float total = Mathf.Max(readyAt - rechargeStartedAt, 0.01f);
                float progress = Mathf.Clamp01((Time.time - rechargeStartedAt) / total);
                scale = Mathf.Lerp(0.2f, 1f, progress);
                color = Color.Lerp(padColor, readyColor, 0.25f);
            }

            core.transform.localScale = Vector3.one * radius * 1.2f * scale;
            core.color = color;
            pad.color = padColor;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = readyColor;
            Gizmos.DrawWireSphere(transform.position, radius);
        }

        SpriteRenderer CreateDisc(string childName, float diameter, int order)
        {
            var host = new GameObject(childName);
            host.transform.SetParent(transform, false);
            host.transform.localScale = Vector3.one * diameter;
            var renderer = host.AddComponent<SpriteRenderer>();
            renderer.sprite = DiscSprite();
            renderer.sortingOrder = order;
            return renderer;
        }

        // A soft white disc generated once, so the point needs no art file (placeholder until real art).
        static Sprite DiscSprite()
        {
            if (discSprite != null)
                return discSprite;

            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[TextureSize * TextureSize];
            float half = TextureSize * 0.5f;
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float distance = Mathf.Sqrt((x - half + 0.5f) * (x - half + 0.5f) + (y - half + 0.5f) * (y - half + 0.5f)) / half;
                    float alpha = 1f - Mathf.Clamp01((distance - 0.88f) / 0.12f);
                    pixels[y * TextureSize + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            discSprite = Sprite.Create(texture, new Rect(0, 0, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), TextureSize);
            discSprite.hideFlags = HideFlags.HideAndDontSave;
            return discSprite;
        }
    }
}
