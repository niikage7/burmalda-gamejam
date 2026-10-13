using UnityEngine;

namespace TankBrawl.Tank
{
    /// <summary>
    /// Nitro and ram numbers. Separate from <see cref="TankConfig"/> because they are tuned together and later upgraded
    /// as one block (balance v0.2, section 8: an upgrade raises <see cref="capacity"/> only, the speed stays).
    /// Units: 1 unit = 1D (hull diameter). The ram numbers are not in the balance document yet.
    /// </summary>
    [CreateAssetMenu(menuName = "TankBrawl/Nitro Config", fileName = "NitroConfig")]
    public class NitroConfig : ScriptableObject
    {
        [Header("Nitro (balance v0.2, section 8)")]
        [Tooltip("Seconds of continuous boost in a full tank. Balance v0.2: 3")]
        [Min(0.1f)] public float capacity = 3f;

        [Tooltip("Added to the capacity per nitro upgrade level, s. Balance v0.2: 0.25")]
        [Min(0f)] public float capacityPerLevel = 0.25f;

        [Tooltip("Top speed on nitro, units/s. Balance v0.2: 5.0")]
        [Min(0f)] public float speed = 5f;

        [Tooltip("Passive refill while the button is not used, as a fraction of the full stock per second. Balance v0.2: 0.07")]
        [Range(0f, 1f)] public float regenPerSecond = 0.07f;

        [Tooltip("After the stock hits zero, nitro stays off until this fraction of the stock has refilled (anti-flicker). Not in balance, proposal: 0.1")]
        [Range(0f, 1f)] public float reactivateStock = 0.1f;

        [Tooltip("The stock drains only while the tank actually moves at least this fast, units/s, so pushing against a wall burns nothing. Proposal: 1")]
        [Min(0f)] public float minDrainSpeed = 1f;

        [Header("Ram (CLAUDE.md, 'Решённое'; proposals, not in balance yet)")]
        [Tooltip("The ram works only while nitro is on and the tank is at least this fast, units/s. Between the normal (2.8) and the nitro (5.0) speed.")]
        [Min(0f)] public float ramMinSpeed = 4f;

        [Tooltip("The ram counts only if the obstacle is within this angle of the hull direction, deg. Sliding along a wall does not ram.")]
        [Range(0f, 90f)] public float ramMaxAngle = 45f;

        [Tooltip("HP taken from the rammed tank. CLAUDE.md: 25")]
        [Min(0f)] public float ramDamage = 25f;

        [Tooltip("The same tank can be rammed again only after this long, s. CLAUDE.md: ~0.6")]
        [Min(0f)] public float ramCooldown = 0.6f;

        [Tooltip("Initial speed of the shove given to the rammed tank, units/s. It fades out linearly.")]
        [Min(0f)] public float knockbackSpeed = 8f;

        [Tooltip("How long the shove lasts, s.")]
        [Min(0.02f)] public float knockbackDuration = 0.3f;

        [Tooltip("Durability taken from a brick on contact: enough to break it at once. Must be at least the brick durability (2).")]
        [Min(1)] public int ramBlockDamage = 99;

        [Tooltip("Bricks are broken across the width of the hull: at the contact point and this far to each side, units. Tank collider radius is 0.45.")]
        [Min(0f)] public float brickBreakHalfWidth = 0.4f;
    }
}
