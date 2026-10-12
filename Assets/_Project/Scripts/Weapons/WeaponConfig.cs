using UnityEngine;

namespace TankBrawl.Weapons
{
    /// <summary>Tunable numbers of one projectile type. Units: 1 unit = 1D (hull diameter), see tank_balance_v0_2.md.</summary>
    [CreateAssetMenu(menuName = "TankBrawl/Weapon Config", fileName = "WeaponConfig")]
    public class WeaponConfig : ScriptableObject
    {
        [Tooltip("Name shown on the ammo button.")]
        public string displayName = "ПРОСТОЙ";

        [Header("Projectile")]
        public Projectile projectilePrefab;

        [Tooltip("Flight speed, units/s. Balance v0.2 armor-piercing: 10")]
        [Min(0f)] public float speed = 10f;

        [Tooltip("Distance from the hull centre to the muzzle, units. The projectile starts here.")]
        [Min(0f)] public float muzzleOffset = 0.5f;

        [Tooltip("How many times the projectile bounces off steel before the next hit destroys it. 0 = never bounces.")]
        [Min(0)] public int maxBounces = 2;

        [Header("Damage")]
        [Tooltip("Direct damage to a tank, HP. Balance v0.2 armor-piercing: 50")]
        [Min(0f)] public float damage = 50f;

        [Tooltip("Durability taken from a destructible block. Balance v0.2 armor-piercing: 2")]
        [Min(0)] public int blockDamage = 2;

        [Header("Explosion (0 radius = plain projectile)")]
        [Tooltip("Blast radius, units. The projectile explodes on a tank, a wall or an enemy projectile. Balance v0.2: fragment 1.4")]
        [Min(0f)] public float explosionRadius;

        [Tooltip("Damage of the blast to every other enemy tank in the radius (the tank hit directly takes only the direct damage), HP. Balance v0.2: fragment 25, explosive power-up 30")]
        [Min(0f)] public float explosionDamage;

        [Tooltip("Durability taken from every destructible block in the radius. Balance v0.2: 1")]
        [Min(0)] public int explosionBlockDamage = 1;

        [Tooltip("Optional blast visual, spawned at the explosion point and scaled to the radius.")]
        public ExplosionVisual explosionEffect;

        [Header("Fire rate")]
        [Tooltip("Cooldown after a shot, s. Balance v0.2: 0.9")]
        [Min(0.05f)] public float fireInterval = 0.9f;

        [Header("Hitboxes")]
        [Tooltip("Radius of the hitbox against tanks, units. Matches the visual (0.56D diameter).")]
        [Min(0.01f)] public float tankHitRadius = 0.28f;

        [Tooltip("Radius of the hitbox against walls, units. Smaller than the tank one so the shot does not catch corners.")]
        [Min(0.01f)] public float wallHitRadius = 0.12f;

        public bool IsExplosive => explosionRadius > 0f;
    }
}
