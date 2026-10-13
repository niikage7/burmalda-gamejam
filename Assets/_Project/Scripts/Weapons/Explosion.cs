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
    /// Walls shield from the blast: a tank is hurt only if a straight line from the centre reaches its hull, a block
    /// only if it is the first wall on the way (see <see cref="DestructibleTilemap.DamageArea"/>). Water does not shield.
    /// </summary>
    public static class Explosion
    {
        const int MaxColliders = 32;

        // A tank behind a corner still takes the blast if the line reaches the edge of its hull; share of the hull radius.
        const float HullEdgeShare = 0.8f;

        // Scratch buffers, overwritten before every use: nothing to reset between Play Mode runs.
        static readonly Collider2D[] Overlaps = new Collider2D[MaxColliders];
        static readonly List<IDamageable> Hurt = new List<IDamageable>();
        static readonly List<DestructibleTilemap> Broken = new List<DestructibleTilemap>();

        static ContactFilter2D filter;
        static int wallMask;
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

            EnsureFilter();

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

                Vector2 hull = hitCollider.bounds.center;
                Vector2 away = hull - center;
                Vector2 direction = away.sqrMagnitude > Mathf.Epsilon ? away.normalized : Vector2.up;
                if (!Reaches(center, hull, direction, hitCollider.bounds.extents.x * HullEdgeShare))
                    continue;

                Hurt.Add(target);
                target.TakeDamage(new DamageInfo(blastDamage, 0, center, direction, owner));
            }
        }

        static void EnsureFilter()
        {
            if (filterReady)
                return;

            filter = new ContactFilter2D { useTriggers = false };
            wallMask = LayerMask.GetMask("Wall");
            filterReady = true;
        }

        /// <summary>
        /// True when a wall does not stand between a blast at <paramref name="center"/> and a hull at <paramref name="hull"/>:
        /// the line to its centre or to either side (<paramref name="halfWidth"/>) is clear. Bots use it to aim at cover.
        /// </summary>
        public static bool Reaches(Vector2 center, Vector2 hull, Vector2 direction, float halfWidth)
        {
            EnsureFilter();
            Vector2 side = new Vector2(-direction.y, direction.x) * halfWidth;
            return IsClear(center, hull) || IsClear(center, hull + side) || IsClear(center, hull - side);
        }

        static bool IsClear(Vector2 from, Vector2 to)
        {
            return Physics2D.Linecast(from, to, wallMask).collider == null;
        }
    }
}
