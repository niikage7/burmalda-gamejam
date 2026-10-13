using System.Collections.Generic;
using TankBrawl.Audio;
using TankBrawl.Core;
using TankBrawl.Map;
using UnityEngine;

namespace TankBrawl.Weapons
{
    /// <summary>
    /// A blast of an explosive projectile: every enemy unit in the radius takes <see cref="WeaponConfig.explosionDamage"/>
    /// (once, except the one that was hit directly), every destructible block in the radius takes
    /// <see cref="WeaponConfig.explosionBlockDamage"/>. Units of the shooter's team are never hurt.
    /// </summary>
    public static class Explosion
    {
        const int MaxColliders = 32;

        // Scratch buffers, overwritten before every use: nothing to reset between Play Mode runs.
        static readonly Collider2D[] Overlaps = new Collider2D[MaxColliders];
        static readonly List<IDamageable> Hurt = new List<IDamageable>();
        static readonly List<DestructibleTilemap> Broken = new List<DestructibleTilemap>();

        static ContactFilter2D filter;
        static bool filterReady;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            filterReady = false;
            Hurt.Clear();
            Broken.Clear();
        }

        /// <param name="blastDamage">HP every other enemy in the radius takes (the config value plus the shooter's weapon upgrades).</param>
        /// <param name="directTarget">The unit that was hit by the projectile itself: it already took the direct damage.</param>
        public static void Detonate(WeaponConfig config, float blastDamage, Vector2 center, GameObject owner, Team team, IDamageable directTarget)
        {
            if (config == null || !config.IsExplosive)
                return;

            if (!filterReady)
            {
                filter = new ContactFilter2D { useTriggers = false };
                filterReady = true;
            }

            AudioManager.PlayAt(Sfx.ExplosionBlast, center);
            ScreenShake.AddAt(center, 0.3f);
            Fx.Burst(center, new Color(1f, 0.7f, 0.25f, 1f), 14, 5f, 0.45f, 0.2f);

            if (config.explosionEffect != null)
            {
                ExplosionVisual effect = Object.Instantiate(config.explosionEffect, center, Quaternion.identity);
                effect.Play(config.explosionRadius);
            }

            Hurt.Clear();
            Broken.Clear();

            int count = Physics2D.OverlapCircle(center, config.explosionRadius, filter, Overlaps);
            for (int i = 0; i < count; i++)
            {
                Collider2D hitCollider = Overlaps[i];
                IDamageable target = hitCollider.GetComponentInParent<IDamageable>();
                if (target == null)
                    continue;

                if (target.Kind == DamageableKind.Terrain)
                {
                    if (target is DestructibleTilemap bricks && !Broken.Contains(bricks))
                    {
                        Broken.Add(bricks);
                        bricks.DamageArea(center, config.explosionRadius, config.explosionBlockDamage);
                    }

                    continue;
                }

                if (ReferenceEquals(target, directTarget) || Hurt.Contains(target))
                    continue;

                TeamMember member = hitCollider.GetComponentInParent<TeamMember>();
                if (member != null && member.Team == team)
                    continue;

                Hurt.Add(target);
                Vector2 away = (Vector2)hitCollider.bounds.center - center;
                Vector2 direction = away.sqrMagnitude > Mathf.Epsilon ? away.normalized : Vector2.up;
                target.TakeDamage(new DamageInfo(blastDamage, 0, center, direction, owner));
            }
        }
    }
}
