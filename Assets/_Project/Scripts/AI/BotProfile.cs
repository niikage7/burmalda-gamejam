using UnityEngine;

namespace TankBrawl.AI
{
    /// <summary>
    /// Character and tuning of a bot's behaviour. Tank stats (HP, speed) live in TankConfig, shot stats in WeaponConfig;
    /// see tank_balance_v0_2.md, section 11. Units: 1 unit = 1D (hull diameter).
    /// </summary>
    [CreateAssetMenu(menuName = "TankBrawl/Bot Profile", fileName = "BotProfile")]
    public class BotProfile : ScriptableObject
    {
        [Header("Reaction and aim")]
        [Tooltip("Delay between first seeing the target and the first shot, s (random in the range).")]
        [Min(0f)] public float reactionDelayMin = 0.2f;

        [Min(0f)] public float reactionDelayMax = 0.4f;

        [Tooltip("The bot aims off by a random angle within ± this value, deg. Re-rolled after every shot. Balance v0.2: 8")]
        [Min(0f)] public float aimSpreadDegrees = 8f;

        [Tooltip("The bot shoots when the hull is off the aim direction by no more than this, deg.")]
        [Range(0f, 45f)] public float fireAlignTolerance = 2f;

        [Tooltip("How much of the target's movement the bot takes into account when aiming. 0 = aims at the current position, 1 = full lead.")]
        [Range(0f, 1f)] public float leadFactor = 0.5f;

        [Header("Combat")]
        [Tooltip("The bot shoots and fights only inside this distance, units.")]
        [Min(0f)] public float fireRange = 9f;

        [Tooltip("Closer than this the bot stops driving and only turns to aim, units.")]
        [Min(0f)] public float stopDistance = 3f;

        [Tooltip("Does not shoot if a steel wall is closer than this in front of the muzzle: the ricochet would come back, units.")]
        [Min(0f)] public float steelPointBlankDistance = 1.5f;

        [Header("Nitro and ram (minimal; the Ram state with a rev-up is stage 6)")]
        [Tooltip("The bot burns nitro and drives into the target when it is closer than this and the straight way is clear, units. 0 = never uses nitro.")]
        [Min(0f)] public float nitroRamDistance = 4.5f;

        [Tooltip("Nitro is switched on only with at least this fraction of the stock.")]
        [Range(0f, 1f)] public float nitroStartStock = 0.5f;

        [Tooltip("Nitro is switched off when the stock falls to this fraction.")]
        [Range(0f, 1f)] public float nitroStopStock = 0.1f;

        [Tooltip("Nitro is switched on only if the hull points at the target within this angle, deg.")]
        [Range(0f, 90f)] public float nitroAlignTolerance = 15f;

        [Header("Perception")]
        [Tooltip("How often the line of sight is re-checked, s.")]
        [Min(0.02f)] public float perceptionInterval = 0.1f;

        [Tooltip("How often the bot picks the nearest enemy again, s.")]
        [Min(0.1f)] public float retargetInterval = 1f;

        [Header("Navigation")]
        [Tooltip("A waypoint counts as reached at this distance, units.")]
        [Min(0.05f)] public float waypointRadius = 0.2f;

        [Tooltip("The path is rebuilt no more often than this, s.")]
        [Min(0.05f)] public float repathInterval = 0.5f;

        [Tooltip("The path is rebuilt when the goal has moved farther than this from where the path was built, units.")]
        [Min(0.1f)] public float repathGoalDistance = 1f;

        [Header("Unstuck")]
        [Tooltip("Wants to drive but has not moved for this long -> backs off and rebuilds the path, s.")]
        [Min(0.1f)] public float stuckTime = 1f;

        [Tooltip("Moving less than this over the stuck time counts as not moved, units.")]
        [Min(0f)] public float stuckMinDistance = 0.25f;

        [Tooltip("How long the bot backs off, s.")]
        [Min(0.1f)] public float reverseDuration = 0.6f;

        [Tooltip("The back-off direction is random within ± this angle around the opposite of the hull direction, deg.")]
        [Range(0f, 90f)] public float reverseAngleSpread = 30f;

        void OnValidate()
        {
            if (reactionDelayMax < reactionDelayMin)
                reactionDelayMax = reactionDelayMin;
        }
    }
}
