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

        [Header("Turning")]
        [Tooltip("Hull turn speed, deg/s.")]
        [Min(0f)] public float rotationSpeed = 450f;

        [Tooltip("Stick deflection below this only turns the hull in place; above it the tank drives.")]
        [Range(0f, 1f)] public float driveThreshold = 0.5f;

        [Tooltip("If the hull is off the stick direction by more than this, the tank turns first and drives after, deg.")]
        [Range(0f, 180f)] public float maxDriveAngle = 60f;
    }
}
