using System;
using TankBrawl.Audio;
using TankBrawl.Core;
using TankBrawl.Input;
using UnityEngine;

namespace TankBrawl.Tank
{
    /// <summary>
    /// Tank hit points. After a hit the tank blinks and ignores damage for
    /// <see cref="TankConfig.hitInvulnerability"/> seconds. A separate, longer spawn protection
    /// (<see cref="GrantSpawnProtection"/>, given by the match manager) makes the tank immune and blinks it slower.
    /// On death the object is deactivated; re-activating it restores full health and clears the protection.
    /// </summary>
    public class TankHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] TankConfig config;

        SpriteRenderer[] renderers;
        TankShield shield;
        TankStats stats;
        TeamMember member;
        bool isHuman;
        float invulnerableUntil;
        float spawnProtectedUntil;
        bool spritesShown = true;

        public float Current { get; private set; }
        public float Max => config.maxHealth + (stats != null ? config.healthPerLevel * stats.HullLevel : 0f);
        public bool IsDead => Current <= 0f;

        /// <summary>Immune to damage for any reason: the short window after a hit or the spawn protection.</summary>
        public bool IsInvulnerable => Time.time < invulnerableUntil || Time.time < spawnProtectedUntil;

        /// <summary>Spawn protection is on. Bots do not shoot at such a tank (the hit window after a damage is not included).</summary>
        public bool HasSpawnProtection => Time.time < spawnProtectedUntil;

        public DamageableKind Kind => DamageableKind.Unit;

        /// <summary>The tank died: the victim and who dealt the killing damage (the shooter's root object; null if unknown). Raised before the object is deactivated.</summary>
        public event Action<TankHealth, GameObject> Died;

        /// <summary>The tank lost health (not raised for a hit the shield or an immunity absorbed): the damage and who dealt it.</summary>
        public event Action<TankHealth, float, GameObject> Damaged;

        /// <summary>A hit was absorbed by the shield.</summary>
        public event Action<TankHealth> ShieldAbsorbed;

        /// <summary>Restores <paramref name="amount"/> HP, never above <see cref="Max"/> (the repair power-up). A dead tank is not healed.</summary>
        public void Heal(float amount)
        {
            if (config == null || IsDead || amount <= 0f)
                return;

            Current = Mathf.Min(Max, Current + amount);
        }

        /// <summary>Makes the tank immune for <paramref name="seconds"/> (and blink). Call after activating it.</summary>
        public void GrantSpawnProtection(float seconds)
        {
            spawnProtectedUntil = seconds > 0f ? Time.time + seconds : 0f;
        }

        void Awake()
        {
            renderers = TankHealthBar.FindTankRenderers(this);
            TryGetComponent(out shield);
            TryGetComponent(out stats);
            TryGetComponent(out member);
            isHuman = TryGetComponent(out PlayerTankInput _);
            if (config == null)
                Debug.LogError($"{name}: TankConfig is not assigned.", this);
        }

        void OnEnable()
        {
            if (config == null)
                return;

            Current = Max;
            invulnerableUntil = 0f;
            spawnProtectedUntil = 0f;
            SetSpritesShown(true);
        }

        void Update()
        {
            if (config == null)
                return;

            float now = Time.time;
            float hitLeft = invulnerableUntil - now;
            float spawnLeft = spawnProtectedUntil - now;
            bool hitShow = hitLeft <= 0f || (int)(hitLeft / config.hitBlinkInterval) % 2 == 0;
            bool spawnShow = spawnLeft <= 0f || (int)(spawnLeft / config.spawnBlinkInterval) % 2 == 0;
            SetSpritesShown(hitShow && spawnShow);
        }

        public void TakeDamage(in DamageInfo info)
        {
            if (config == null || IsDead || IsInvulnerable)
                return;

            // The shield eats one hit; the tank still gets the usual invulnerability window, so a second
            // near-simultaneous hit (a blast next to a direct hit) does not slip through the gone shield.
            if (info.Amount > 0f && shield != null && shield.TryAbsorb())
            {
                invulnerableUntil = Time.time + config.hitInvulnerability;
                AudioManager.PlayAt(Sfx.ShieldBlock, transform.position);
                ShieldAbsorbed?.Invoke(this);
                return;
            }

            Current = Mathf.Max(0f, Current - info.Amount);
            invulnerableUntil = Time.time + config.hitInvulnerability;
            if (info.Amount > 0f)
            {
                PlayHitEffects(info);
                Damaged?.Invoke(this, info.Amount, info.Source);
            }

#if UNITY_EDITOR
            Debug.Log($"{name}: -{info.Amount} HP, {Current}/{Max}", this);
#endif

            if (IsDead)
            {
                PlayDeathEffects();
                Died?.Invoke(this, info.Source);
                gameObject.SetActive(false);
            }
        }

        // Sparks, sound and shake. The human feels its own hits (shake, vibration) more than other tanks' hits.
        void PlayHitEffects(in DamageInfo info)
        {
            Vector2 position = transform.position;
            Color color = member != null ? TeamColors.Of(member.Team) : Color.white;
            Fx.Sparks(position, -info.Direction, color);

            if (isHuman)
            {
                AudioManager.Play(Sfx.HitSelf);
                ScreenShake.Add(Mathf.Clamp01(info.Amount / Max) * 0.8f + 0.1f);
                Fx.Haptic();
            }
            else
            {
                AudioManager.PlayAt(Sfx.HitTank, position);
            }
        }

        void PlayDeathEffects()
        {
            Vector2 position = transform.position;
            Color color = member != null ? TeamColors.Of(member.Team) : Color.white;
            Fx.TankExplosion(position, color);
            AudioManager.PlayAt(Sfx.TankExplode, position);
            ScreenShake.AddAt(position, isHuman ? 0.7f : 0.4f);
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
