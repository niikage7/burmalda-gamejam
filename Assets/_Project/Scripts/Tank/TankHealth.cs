using System;
using TankBrawl.Core;
using UnityEngine;

namespace TankBrawl.Tank
{
    /// <summary>
    /// Tank hit points. After a hit the tank blinks and ignores damage for
    /// <see cref="TankConfig.hitInvulnerability"/> seconds. On death the object is deactivated;
    /// re-activating it restores full health.
    /// </summary>
    public class TankHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] TankConfig config;

        SpriteRenderer[] renderers;
        float invulnerableUntil;
        bool spritesShown = true;

        /// <summary>Hit points left, 0..<see cref="Max"/>.</summary>
        public float Current { get; private set; }

        public float Max => config.maxHealth;
        public bool IsDead => Current <= 0f;

        /// <summary>Inside the post-hit window: <see cref="TakeDamage"/> is ignored.</summary>
        public bool IsInvulnerable => Time.time < invulnerableUntil;

        /// <inheritdoc/>
        public DamageableKind Kind => DamageableKind.Unit;

        /// <summary>Raised once when HP reaches zero, right before the object is deactivated.</summary>
        public event Action<TankHealth> Died;

        void Awake()
        {
            renderers = GetComponentsInChildren<SpriteRenderer>();
            if (config == null)
                Debug.LogError($"{name}: TankConfig is not assigned.", this);
        }

        void OnEnable()
        {
            if (config == null)
                return;

            Current = config.maxHealth;
            invulnerableUntil = 0f;
            SetSpritesShown(true);
        }

        void Update()
        {
            if (config == null)
                return;

            // Blink: sprites alternate every hitBlinkInterval while the invulnerability lasts.
            float left = invulnerableUntil - Time.time;
            bool show = left <= 0f || (int)(left / config.hitBlinkInterval) % 2 == 0;
            SetSpritesShown(show);
        }

        /// <summary>Takes <see cref="DamageInfo.Amount"/> HP unless dead or invulnerable; kills the tank at zero.</summary>
        public void TakeDamage(in DamageInfo info)
        {
            if (config == null || IsDead || Time.time < invulnerableUntil)
                return;

            Current = Mathf.Max(0f, Current - info.Amount);
            invulnerableUntil = Time.time + config.hitInvulnerability;

#if UNITY_EDITOR
            Debug.Log($"{name}: -{info.Amount} HP, {Current}/{Max}", this);
#endif

            if (IsDead)
            {
                Died?.Invoke(this);
                gameObject.SetActive(false);
            }
        }

        void SetSpritesShown(bool shown)
        {
            if (shown == spritesShown)
                return;

            spritesShown = shown;
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].enabled = shown;
        }
    }
}
