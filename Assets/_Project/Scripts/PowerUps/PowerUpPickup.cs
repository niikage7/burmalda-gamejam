using System.Collections.Generic;
using TankBrawl.Audio;
using TankBrawl.Core;
using UnityEngine;

namespace TankBrawl.PowerUps
{
    /// <summary>
    /// A power-up lying on the map. The first tank (any team) that drives into the trigger takes it and the object goes away.
    /// Can be placed in a scene by hand (assign <see cref="config"/>) or created by <see cref="PowerUpSpawner"/>.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class PowerUpPickup : MonoBehaviour
    {
        [SerializeField] PowerUpConfig config;

        [Tooltip("Tinted with the config colour.")]
        [SerializeField] SpriteRenderer sprite;

        [Tooltip("Gentle pulse so the pick-up is noticed, share of the size. 0 = none.")]
        [SerializeField, Range(0f, 0.5f)] float pulseAmount = 0.1f;

        [SerializeField, Min(0.1f)] float pulsePeriod = 1f;

        static readonly List<PowerUpPickup> Active = new List<PowerUpPickup>();

        Vector3 baseScale;
        bool taken;

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

        /// <summary>Sets what this pick-up gives. For pick-ups created from code.</summary>
        public void Configure(PowerUpConfig newConfig)
        {
            config = newConfig;
            ApplyLook();
        }

        void Awake()
        {
            baseScale = transform.localScale;
            ApplyLook();
        }

        void Update()
        {
            if (pulseAmount <= 0f)
                return;

            float pulse = 1f + pulseAmount * Mathf.Sin(Time.time * (Mathf.PI * 2f / pulsePeriod));
            transform.localScale = baseScale * pulse;
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
                sprite.color = config.pickupColor;
        }
    }
}
