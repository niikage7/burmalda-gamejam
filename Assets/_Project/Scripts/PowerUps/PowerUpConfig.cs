using TankBrawl.Weapons;
using UnityEngine;

namespace TankBrawl.PowerUps
{
    public enum PowerUpKind
    {
        RapidFire,
        ExplosiveShells,
        Laser,
        Shield
    }

    /// <summary>
    /// Tunable numbers of one power-up. One asset per pick-up on the map; only the section of its <see cref="kind"/> is used.
    /// Units: 1 unit = 1D (hull diameter), see tank_balance_v0_2.md.
    /// </summary>
    [CreateAssetMenu(menuName = "TankBrawl/Power-Up Config", fileName = "PowerUpConfig")]
    public class PowerUpConfig : ScriptableObject
    {
        public PowerUpKind kind;

        [Tooltip("Name shown next to the timer.")]
        public string displayName = "УСИЛЕНИЕ";

        [Tooltip("How long the effect lasts, s. A second pick-up of the same kind refills it. Balance v0.2: explosive 15, laser 4. Rapid fire and shield are our own proposals.")]
        [Min(0.1f)] public float duration = 10f;

        [Header("Pick-up look (placeholder until real art)")]
        public Color pickupColor = Color.white;

        [Header("Rapid fire")]
        [Tooltip("Multiplier of the time between shots. 0.5 = twice as often. Not in the balance doc yet.")]
        [Range(0.1f, 1f)] public float fireIntervalScale = 0.5f;

        [Header("Explosive shells")]
        [Tooltip("Replaces the tank's projectile while the effect lasts. Balance v0.2: 50 HP direct, 30 HP blast.")]
        public WeaponConfig explosiveWeapon;

        [Header("Laser")]
        [Tooltip("Damage of one tick to one unit in the beam, HP. Balance v0.2: 20")]
        [Min(0f)] public float laserDamage = 20f;

        [Tooltip("Pause between two ticks on the same unit, s. Balance v0.2: 0.5")]
        [Min(0.05f)] public float laserInterval = 0.5f;

        [Tooltip("Longest the beam can be, units. Arenas are closed, so this only guards against an open edge.")]
        [Min(1f)] public float laserRange = 40f;

        [Tooltip("Half-width of the beam, units (for hits and for walls).")]
        [Min(0.01f)] public float laserBeamRadius = 0.15f;

        [Tooltip("Tank speed multiplier while the laser works. Concept doc: it slows the tank; the number is our proposal.")]
        [Range(0.1f, 1f)] public float laserSpeedMultiplier = 0.6f;

        [Header("Shield")]
        [Tooltip("The ring blinks for this long before the shield expires, s.")]
        [Min(0f)] public float shieldBlinkTime = 2f;
    }
}
