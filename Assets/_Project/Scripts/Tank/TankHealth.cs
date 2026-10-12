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
        TankShield shield;
        float invulnerableUntil;
        bool spritesShown = true;

        public float Current { get; private set; }
        public float Max => config.maxHealth;
        public bool IsDead => Current <= 0f;
        public bool IsInvulnerable => Time.time < invulnerableUntil;
        public DamageableKind Kind => DamageableKind.Unit;

        public event Action<TankHealth> Died;

        void Awake()
        {
            renderers = TankHealthBar.FindTankRenderers(this);
            TryGetComponent(out shield);
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

            float left = invulnerableUntil - Time.time;
            bool show = left <= 0f || (int)(left / config.hitBlinkInterval) % 2 == 0;
            SetSpritesShown(show);
        }

        public void TakeDamage(in DamageInfo info)
        {
            if (config == null || IsDead || Time.time < invulnerableUntil)
                return;

            // The shield eats one hit; the tank still gets the usual invulnerability window, so a second
            // near-simultaneous hit (a blast next to a direct hit) does not slip through the gone shield.
            if (info.Amount > 0f && shield != null && shield.TryAbsorb())
            {
                invulnerableUntil = Time.time + config.hitInvulnerability;
                return;
            }

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
