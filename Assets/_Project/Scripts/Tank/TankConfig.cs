using UnityEngine;

namespace TankBrawl.Tank
{
    /// <summary>Tunable tank numbers. Units: 1 unit = 1D (hull diameter), see tank_balance_v0_2.md.</summary>
    [CreateAssetMenu(menuName = "TankBrawl/Tank Config", fileName = "TankConfig")]
    public class TankConfig : ScriptableObject
    {
        [Header("Movement")]
        [Tooltip("Top speed without nitro, units/s. Balance v0.2: 2.8")]
        [Min(0f)] public float moveSpeed = 2.8f;

        [Tooltip("How fast the tank reaches top speed, units/s².")]
        [Min(0f)] public float acceleration = 20f;

        [Tooltip("How fast the tank stops when the stick is released, units/s².")]
        [Min(0f)] public float deceleration = 25f;

        [Tooltip("Added to the top speed per upgrade level (engine for the player, threat rank for bots), units/s. Balance v0.2: player 0.15, bot 0.01")]
        [Min(0f)] public float speedPerLevel;

        [Header("Turning")]
        [Tooltip("Hull turn speed, deg/s.")]
        [Min(0f)] public float rotationSpeed = 450f;

        [Tooltip("Stick deflection below this only turns the hull in place; above it the tank drives.")]
        [Range(0f, 1f)] public float driveThreshold = 0.5f;

        [Tooltip("If the hull is off the stick direction by more than this, the tank turns first and drives after, deg.")]
        [Range(0f, 180f)] public float maxDriveAngle = 60f;

        [Header("Health")]
        [Tooltip("Balance v0.2: 100 HP. No regeneration.")]
        [Min(1f)] public float maxHealth = 100f;

        [Tooltip("Added to the max health per upgrade level (hull for the player, threat rank for bots), HP. Balance v0.2: player 10, bot 4")]
        [Min(0f)] public float healthPerLevel;

        [Tooltip("After a hit the tank ignores further damage for this long, s. Balance v0.2: 0.3")]
        [Min(0f)] public float hitInvulnerability = 0.3f;

        [Tooltip("Blink period of the sprites while invulnerable, s. Visual only.")]
        [Min(0.01f)] public float hitBlinkInterval = 0.05f;

        [Tooltip("Blink period of the sprites during spawn protection (slower than the hit blink, so the two read differently), s. Visual only.")]
        [Min(0.01f)] public float spawnBlinkInterval = 0.15f;
    }
}
