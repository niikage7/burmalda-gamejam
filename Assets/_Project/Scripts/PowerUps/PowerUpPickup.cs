using System.Collections.Generic;
using TankBrawl.Audio;
using TankBrawl.Core;
using UnityEngine;

namespace TankBrawl.PowerUps
{
    /// <summary>
    /// A power-up lying on the map. The first tank (any team) that drives into the trigger takes it and the object goes away.
    /// Can be placed in a scene by hand (assign <see cref="config"/>) or created by <see cref="PowerUpSpawner"/>.
    /// A spawned pick-up disappears if nobody takes it in time (<see cref="ExpireAfter"/>), blinking before it goes;
    /// a hand-placed one stays. A small label above it says what it does (<see cref="PowerUpConfig.MapLabel"/>).
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class PowerUpPickup : MonoBehaviour
    {
        [SerializeField] PowerUpConfig config;

        [Tooltip("Shows the config sprite (tinted with the config colour only when it has none).")]
        [SerializeField] SpriteRenderer sprite;

        [Tooltip("Gentle pulse so the pick-up is noticed, share of the size. 0 = none.")]
        [SerializeField, Range(0f, 0.5f)] float pulseAmount = 0.1f;

        [SerializeField, Min(0.1f)] float pulsePeriod = 1f;

        [Header("Label above the pick-up")]
        [Tooltip("Height of the label text, units (1 unit = 1D).")]
        [SerializeField, Min(0.05f)] float labelHeight = 0.22f;

        [Tooltip("How far above the pick-up centre the label sits, units.")]
        [SerializeField] float labelOffset = 0.55f;

        [Header("Expiry")]
        [Tooltip("Before it disappears the pick-up blinks for this long, s.")]
        [SerializeField, Min(0f)] float expireWarning = 3f;

        [SerializeField, Min(0.02f)] float expireBlinkInterval = 0.15f;

        static readonly List<PowerUpPickup> Active = new List<PowerUpPickup>();

        // Above tanks (9-14) and their health bars (15).
        const int LabelOrder = 17;
        const int LabelFontSize = 64;
        static readonly Color LabelColor = Color.white;
        static readonly Color LabelShadowColor = new Color(0f, 0f, 0f, 0.75f);
        static readonly Vector3 ShadowShift = new Vector3(0.02f, -0.02f, 0f);

        Vector3 baseScale;
        Transform labelRoot;
        TextMesh label;
        TextMesh labelShadow;
        bool taken;
        float expiresAt = float.PositiveInfinity;

        /// <summary>Pick-ups lying on the map now. Read-only, for bots (index loop, no allocations).</summary>
        public static IReadOnlyList<PowerUpPickup> OnMap => Active;

        public PowerUpConfig Config => config;

        /// <summary>The bot that is driving to this pick-up, so two bots do not go for the same one. Informational, not a lock.</summary>
        public Component ClaimedBy { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Active.Clear();
        }

        void OnEnable()
        {
            Active.Add(this);
        }

        void OnDisable()
        {
            Active.Remove(this);
            ClaimedBy = null;
        }

        /// <summary>Makes the pick-up disappear after <paramref name="seconds"/> unless someone takes it first.</summary>
        public void ExpireAfter(float seconds)
        {
            expiresAt = seconds > 0f ? Time.time + seconds : float.PositiveInfinity;
        }

        /// <summary>Sets what this pick-up gives. For pick-ups created from code.</summary>
        public void Configure(PowerUpConfig newConfig)
        {
            config = newConfig;
            ApplyLook();
        }

        void Awake()
        {
            baseScale = transform.localScale;
            BuildLabel();
            ApplyLook();
            PlaceLabel(1f);
        }

        void Update()
        {
            float left = expiresAt - Time.time;
            if (left <= 0f)
            {
                Fx.Burst(transform.position, config != null ? config.pickupColor : Color.white, 8, 2f, 0.4f, 0.12f);
                Destroy(gameObject);
                return;
            }

            bool shown = left > expireWarning || (int)(left / expireBlinkInterval) % 2 == 0;
            if (sprite != null)
                sprite.enabled = shown;
            if (labelRoot != null && labelRoot.gameObject.activeSelf != shown)
                labelRoot.gameObject.SetActive(shown);

            if (pulseAmount <= 0f)
                return;

            float pulse = 1f + pulseAmount * Mathf.Sin(Time.time * (Mathf.PI * 2f / pulsePeriod));
            transform.localScale = baseScale * pulse;
            PlaceLabel(pulse);
        }

        // The label is a child, so it would pulse with the pick-up: its local scale and offset undo the pick-up's scale.
        void PlaceLabel(float pulse)
        {
            if (labelRoot == null)
                return;

            float sx = Mathf.Max(Mathf.Abs(baseScale.x) * pulse, 0.0001f);
            float sy = Mathf.Max(Mathf.Abs(baseScale.y) * pulse, 0.0001f);
            labelRoot.localScale = new Vector3(1f / sx, 1f / sy, 1f);
            labelRoot.localPosition = new Vector3(0f, labelOffset / sy, 0f);
        }

        void BuildLabel()
        {
            var root = new GameObject("Label");
            root.transform.SetParent(transform, false);
            labelRoot = root.transform;
            labelShadow = CreateText(labelRoot, "Shadow", LabelShadowColor, LabelOrder - 1, ShadowShift);
            label = CreateText(labelRoot, "Text", LabelColor, LabelOrder, Vector3.zero);
        }

        TextMesh CreateText(Transform parent, string objectName, Color color, int order, Vector3 shift)
        {
            var host = new GameObject(objectName);
            host.transform.SetParent(parent, false);
            host.transform.localPosition = shift;

            Font font = UI.UiKit.Font;
            var text = host.AddComponent<TextMesh>();
            text.font = font;
            text.fontSize = LabelFontSize;
            // A TextMesh line is about characterSize * fontSize / 10 units tall.
            text.characterSize = labelHeight * 10f / LabelFontSize;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = color;

            var renderer = host.GetComponent<MeshRenderer>();
            if (font != null)
                renderer.sharedMaterial = font.material;
            renderer.sortingOrder = order;
            return text;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (taken || config == null)
                return;

            TankPowerUps tank = other.GetComponentInParent<TankPowerUps>();
            if (tank == null)
                return;

            taken = true;
            tank.Apply(config);
            AudioManager.PlayAt(Sfx.PowerUpPickup, transform.position);
            Fx.Burst(transform.position, config.pickupColor, 14, 4f, 0.5f, 0.15f);
            Destroy(gameObject);
        }

        void ApplyLook()
        {
            if (sprite != null && config != null)
            {
                bool hasArt = config.pickupSprite != null;
                if (hasArt)
                    sprite.sprite = config.pickupSprite;
                sprite.color = hasArt ? Color.white : config.pickupColor;
            }

            string line = config != null ? config.MapLabel : string.Empty;
            if (label != null)
                label.text = line;
            if (labelShadow != null)
                labelShadow.text = line;
        }
    }
}
