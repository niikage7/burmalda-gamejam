using System.Collections.Generic;
using TankBrawl.Audio;
using TankBrawl.Core;
using TankBrawl.Map;
using UnityEngine;

namespace TankBrawl.Weapons
{
    /// <summary>
    /// Straight-flying projectile without colliders of its own. Everything is swept each frame:
    /// the large hitbox (<see cref="WeaponConfig.tankHitRadius"/>) against units of the other team,
    /// the small one (<see cref="WeaponConfig.wallHitRadius"/>) against terrain, and the large one
    /// against projectiles of the other team (both are destroyed). The nearest hit wins.
    /// Steel (<see cref="RicochetSurface"/>) bounces the projectile up to <see cref="WeaponConfig.maxBounces"/> times;
    /// after the first bounce the shooter can be hit as well.
    /// There is no lifetime: the projectile flies until it hits something, so arenas must be closed.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        const int MaxHits = 8;
        const int MaxStepsPerFrame = 8;
        const float MinStep = 0.0001f;

        // How far past the contact point a terrain hit is reported, so it lands inside the struck tile.
        const float SurfacePenetration = 0.02f;

        // Push-off after a bounce, so the next cast does not start inside the wall.
        const float BounceSkin = 0.01f;

        static readonly Color SparkColor = new Color(1f, 0.85f, 0.5f, 1f);
        static readonly Color ClashColor = new Color(1f, 1f, 1f, 1f);

        // Scratch buffers, overwritten before every use: nothing to reset between Play Mode runs.
        static readonly RaycastHit2D[] UnitHits = new RaycastHit2D[MaxHits];
        static readonly RaycastHit2D[] TerrainHits = new RaycastHit2D[MaxHits];

        static readonly List<Projectile> Active = new List<Projectile>();

        // Water blocks tanks but not projectiles, so it is excluded from the casts.
        // Built lazily: layer names cannot be resolved while Unity loads a MonoBehaviour type.
        static ContactFilter2D solidFilter;
        static bool solidFilterReady;

        WeaponConfig config;
        GameObject owner;
        Team team;
        float speed;
        float damage;
        float blastDamage;
        Vector2 direction;
        int bounces;
        bool finished;

        /// <summary>Every projectile in flight. Read-only, for bots dodging shots (index loop, no allocations).</summary>
        public static IReadOnlyList<Projectile> InFlight => Active;

        public Team Team => team;
        public Vector2 Direction => direction;
        public WeaponConfig Config => config;
        public bool IsFinished => finished;

        /// <summary>Flight speed of this projectile, units/s (the config speed plus the shooter's weapon upgrades).</summary>
        public float Speed => speed;

        /// <summary>The tank that fired it (its root object). After the first bounce the projectile can hit it too.</summary>
        public GameObject Owner => owner;

        /// <summary>Bounces off steel so far.</summary>
        public int Bounces => bounces;

        /// <summary>Bounces still possible: the wall after the last one destroys the projectile.</summary>
        public int BouncesLeft => config != null ? Mathf.Max(0, config.maxBounces - bounces) : 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Active.Clear();
            solidFilterReady = false;
        }

        /// <summary>Removes every projectile in flight without exploding it (a new round starts).</summary>
        public static void ClearAll()
        {
            for (int i = Active.Count - 1; i >= 0; i--)
                Active[i].Finish();
        }

        void OnEnable()
        {
            if (!solidFilterReady)
            {
                solidFilter = new ContactFilter2D();
                solidFilter.useTriggers = false;
                solidFilter.useLayerMask = true;
                solidFilter.layerMask = ~LayerMask.GetMask("Water");
                solidFilterReady = true;
            }

            Active.Add(this);
        }

        void OnDisable()
        {
            Active.Remove(this);
        }

        /// <summary>
        /// Starts the flight at <paramref name="origin"/> (the shooter's centre) and immediately sweeps to the muzzle,
        /// so a shot fired point-blank at a wall or a tank still hits it.
        /// </summary>
        public void Launch(WeaponConfig weaponConfig, GameObject shooter, Team shooterTeam, Vector2 origin, Vector2 shotDirection, int weaponLevel = 0)
        {
            config = weaponConfig;
            owner = shooter;
            team = shooterTeam;
            speed = config.SpeedAt(weaponLevel);
            damage = config.DamageAt(weaponLevel);
            blastDamage = config.ExplosionDamageAt(weaponLevel);
            direction = shotDirection.normalized;
            bounces = 0;
            transform.position = origin;
            FaceDirection();
            Advance(config.muzzleOffset);
        }

        // Update, not FixedUpdate: the projectile has no body, and a 50 Hz step would stutter at 60 FPS.
        void Update()
        {
            if (finished || config == null)
                return;

            Advance(speed * Time.deltaTime);
        }

        void Advance(float distance)
        {
            for (int i = 0; i < MaxStepsPerFrame && distance > MinStep && !finished; i++)
                distance = Step(distance);
        }

        // Moves up to `distance` and returns what is left of it (non-zero only after a bounce).
        float Step(float distance)
        {
            Vector2 start = transform.position;

            RaycastHit2D unitHit = default;
            IDamageable unitTarget = null;
            float unitDistance = float.MaxValue;

            int count = Physics2D.CircleCast(start, config.tankHitRadius, direction, solidFilter, UnitHits, distance);
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = UnitHits[i];
                if (hit.distance >= unitDistance || !TryGetUnit(hit.collider, out IDamageable target) || IgnoresUnit(hit.collider))
                    continue;

                unitHit = hit;
                unitTarget = target;
                unitDistance = hit.distance;
            }

            RaycastHit2D terrainHit = default;
            float terrainDistance = float.MaxValue;

            count = Physics2D.CircleCast(start, config.wallHitRadius, direction, solidFilter, TerrainHits, distance);
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = TerrainHits[i];
                if (hit.distance >= terrainDistance || TryGetUnit(hit.collider, out _))
                    continue;

                // Right after a bounce the wall we left may still touch the cast; it is behind us.
                if (Vector2.Dot(direction, hit.normal) > 0f)
                    continue;

                terrainHit = hit;
                terrainDistance = hit.distance;
            }

            Projectile otherProjectile = null;
            float projectileDistance = float.MaxValue;

            for (int i = 0; i < Active.Count; i++)
            {
                Projectile other = Active[i];
                if (other == this || other.finished || other.config == null || other.team == team)
                    continue;

                float reach = config.tankHitRadius + other.config.tankHitRadius;
                if (SweepCircle(start, direction, distance, other.transform.position, reach, out float t) && t < projectileDistance)
                {
                    otherProjectile = other;
                    projectileDistance = t;
                }
            }

            // Whatever is hit first wins; on a tie: projectile, then unit, then terrain.
            float nearest = Mathf.Min(unitDistance, terrainDistance, projectileDistance);
            if (nearest == float.MaxValue)
            {
                transform.position = start + direction * distance;
                return 0f;
            }

            transform.position = start + direction * nearest;

            if (projectileDistance <= unitDistance && projectileDistance <= terrainDistance)
            {
                // Two shells cancel each other, but an explosive one still goes off at the contact point.
                AudioManager.PlayAt(Sfx.ShellClash, transform.position);
                Fx.Sparks(transform.position, -direction, ClashColor);
                Fx.Sparks(transform.position, direction, ClashColor);
                otherProjectile.Explode(transform.position, null);
                otherProjectile.Finish();
                Explode(transform.position, null);
                Finish();
                return 0f;
            }

            if (unitDistance <= terrainDistance)
            {
                unitTarget.TakeDamage(new DamageInfo(damage, config.blockDamage, unitHit.point, direction, owner));
                Explode(transform.position, unitTarget);
                Finish();
                return 0f;
            }

            bool canBounce = bounces < config.maxBounces
                             && terrainHit.normal.sqrMagnitude > 0f
                             && terrainHit.collider.GetComponentInParent<RicochetSurface>() != null;
            if (canBounce)
            {
                AudioManager.PlayAt(Sfx.Ricochet, terrainHit.point);
                Fx.Sparks(terrainHit.point, terrainHit.normal, SparkColor);
                direction = Vector2.Reflect(direction, terrainHit.normal);
                bounces++;
                transform.position += (Vector3)(terrainHit.normal * BounceSkin);
                FaceDirection();
                return distance - terrainDistance;
            }

            // A brick plays its own sound when it breaks; any other wall (steel without a bounce left, the border) clangs.
            IDamageable terrainTarget = terrainHit.collider.GetComponentInParent<IDamageable>();
            if (terrainTarget == null)
                AudioManager.PlayAt(Sfx.HitWall, terrainHit.point);
            Fx.Sparks(terrainHit.point, terrainHit.normal, SparkColor);

            if (terrainTarget != null)
            {
                Vector2 inward = terrainHit.normal.sqrMagnitude > 0f ? -terrainHit.normal : direction;
                Vector2 point = terrainHit.point + inward * SurfacePenetration;
                terrainTarget.TakeDamage(new DamageInfo(damage, config.blockDamage, point, direction, owner));
            }

            Explode(transform.position, null);
            Finish();
            return 0f;
        }

        // Does nothing for a plain projectile; must be called before Finish() (it needs the config and the team).
        void Explode(Vector2 point, IDamageable directTarget)
        {
            Explosion.Detonate(config, blastDamage, point, owner, team, directTarget);
        }

        static bool TryGetUnit(Collider2D hitCollider, out IDamageable unit)
        {
            unit = hitCollider.GetComponentInParent<IDamageable>();
            return unit != null && unit.Kind == DamageableKind.Unit;
        }

        // The shooter is safe until its own shot has bounced; units of the shooter's team are always safe.
        bool IgnoresUnit(Collider2D hitCollider)
        {
            Rigidbody2D body = hitCollider.attachedRigidbody;
            if (body != null && body.gameObject == owner)
                return bounces == 0;

            TeamMember member = hitCollider.GetComponentInParent<TeamMember>();
            return member != null && member.Team == team;
        }

        // Distance along the ray at which a circle moving from `from` first touches a static circle, if within `length`.
        static bool SweepCircle(Vector2 from, Vector2 dir, float length, Vector2 center, float radius, out float distance)
        {
            Vector2 offset = from - center;
            float c = offset.sqrMagnitude - radius * radius;
            if (c <= 0f)
            {
                distance = 0f;
                return true;
            }

            float b = Vector2.Dot(offset, dir);
            distance = 0f;
            if (b >= 0f)
                return false;

            float discriminant = b * b - c;
            if (discriminant < 0f)
                return false;

            distance = -b - Mathf.Sqrt(discriminant);
            return distance <= length;
        }

        void FaceDirection()
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        void Finish()
        {
            finished = true;
            Destroy(gameObject);
        }
    }
}
