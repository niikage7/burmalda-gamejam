using UnityEngine;

namespace TankBrawl.AI
{
    /// <summary>
    /// Character and tuning of a bot's behaviour. Tank stats (HP, speed) live in TankConfig, shot stats in WeaponConfig;
    /// see tank_balance_v0_2.md, section 11. Units: 1 unit = 1D (hull diameter).
    /// Different characters are different assets of this class (aggressor, cautious, ...).
    /// </summary>
    [CreateAssetMenu(menuName = "TankBrawl/Bot Profile", fileName = "BotProfile")]
    public class BotProfile : ScriptableObject
    {
        [Header("Reaction and aim")]
        [Tooltip("Delay between first seeing the target and the first shot, s (random in the range).")]
        [Min(0f)] public float reactionDelayMin = 0.2f;

        [Tooltip("Upper bound of the reaction delay, s. Kept not below the minimum.")]
        [Min(0f)] public float reactionDelayMax = 0.4f;

        [Tooltip("The bot aims off by a random angle within ± this value, deg. Re-rolled after every shot. Balance v0.2: 8")]
        [Min(0f)] public float aimSpreadDegrees = 8f;

        [Tooltip("The aim also drifts slowly around the target by up to ± this value, deg (like an unsteady hand). 0 = none. Set by the difficulty level.")]
        [Min(0f)] public float aimWobbleDegrees;

        [Tooltip("The bot shoots when the hull is off the aim direction by no more than this, deg.")]
        [Range(0f, 45f)] public float fireAlignTolerance = 2f;

        [Tooltip("How much of the target's movement the bot takes into account when aiming. 0 = aims at the current position, 1 = full lead.")]
        [Range(0f, 1f)] public float leadFactor = 0.5f;

        [Header("Attack: close in and circle")]
        [Tooltip("The bot shoots and fights only inside this distance, units.")]
        [Min(0f)] public float fireRange = 9f;

        [Tooltip("Distance the bot tries to keep while circling the target, units.")]
        [Min(0f)] public float preferredDistance = 4f;

        [Tooltip("Over this distance from the preferred one the bot blends from pure circling to closing in (too far) or backing off (too close), units.")]
        [Min(0.1f)] public float distanceBand = 1f;

        [Tooltip("The bot changes the circling direction at a random time in this range, s.")]
        [Min(0.2f)] public float strafeSwitchMin = 1.5f;

        [Min(0.2f)] public float strafeSwitchMax = 3.5f;

        [Tooltip("While reloading the bot circles; it turns to aim this long before the gun is ready, s.")]
        [Min(0f)] public float aimLeadTime = 0.3f;

        [Tooltip("Does not shoot if a steel wall is closer than this in front of the muzzle: the ricochet would come back, units.")]
        [Min(0f)] public float steelPointBlankDistance = 1.5f;

        [Header("Character")]
        [Tooltip("Chance to start a ram each time the bot decides and the conditions are right (nitro, a clear straight way). 0 = never rams, 1 = always.")]
        [Range(0f, 1f)] public float aggression = 0.5f;

        [Tooltip("How often the bot re-thinks (state, circling side, power-up, retreat), s. Steering and shooting run every frame.")]
        [Min(0.1f)] public float decisionInterval = 0.3f;

        [Header("Ram (state Ram)")]
        [Tooltip("The bot burns nitro and drives into the target when it is closer than this and the straight way is clear, units. 0 = never rams.")]
        [Min(0f)] public float nitroRamDistance = 4.5f;

        [Tooltip("Nitro is switched on only with at least this fraction of the stock.")]
        [Range(0f, 1f)] public float nitroStartStock = 0.5f;

        [Tooltip("Nitro is switched off when the stock falls to this fraction.")]
        [Range(0f, 1f)] public float nitroStopStock = 0.1f;

        [Tooltip("A ram starts only if the hull points at the target within this angle, deg.")]
        [Range(0f, 90f)] public float nitroAlignTolerance = 15f;

        [Tooltip("A ram lasts at most this long, s.")]
        [Min(0.2f)] public float ramMaxDuration = 2.5f;

        [Tooltip("Pause after a ram before the next one, s.")]
        [Min(0f)] public float ramCooldown = 3f;

        [Header("Retreat")]
        [Tooltip("The bot retreats when its health falls to this fraction of the maximum. 0 = never.")]
        [Range(0f, 1f)] public float retreatHealthFraction = 0.3f;

        [Tooltip("The bot retreats for at most this long, s. Health does not regenerate, so a retreat has to end by itself.")]
        [Min(0.5f)] public float retreatDuration = 6f;

        [Tooltip("After a retreat the bot does not retreat again for this long, s.")]
        [Min(0f)] public float retreatLockout = 10f;

        [Tooltip("An enemy with a clear line this close corners the bot: it stops running and fights, units.")]
        [Min(0f)] public float retreatCornerDistance = 3f;

        [Tooltip("A power-up this close (straight line) is preferred to cover when retreating, units.")]
        [Min(0f)] public float retreatPickupDistance = 12f;

        [Tooltip("How many random points are rated when looking for cover.")]
        [Range(2, 24)] public int coverSamples = 10;

        [Tooltip("Cover is looked for between these distances from the bot, units.")]
        [Min(1f)] public float coverMinDistance = 4f;

        [Min(2f)] public float coverMaxDistance = 10f;

        [Tooltip("Cover is chosen again this often while the bot is not hidden yet, s.")]
        [Min(0.3f)] public float coverReplanInterval = 1.5f;

        [Header("Power-ups")]
        [Tooltip("How much the bot wants power-ups, 0..1. 0 = ignores them, 1 = goes for any on the map within the max distance.")]
        [Range(0f, 1f)] public float powerUpGreed = 0.6f;

        [Tooltip("Power-ups farther than this × greed (straight line) are not worth a trip, units.")]
        [Min(0f)] public float powerUpMaxDistance = 14f;

        [Tooltip("While fighting (a clear shot at the target) only a power-up closer than this × greed is worth leaving the fight for, units.")]
        [Min(0f)] public float combatPowerUpDistance = 4f;

        [Tooltip("The bot gives up on a power-up after this long, s.")]
        [Min(1f)] public float powerUpGiveUpTime = 8f;

        [Tooltip("A repair pick-up is worth a trip only below this share of the maximum health.")]
        [Range(0f, 1f)] public float repairWantedBelow = 0.85f;

        [Tooltip("A nitro refill pick-up is worth a trip only below this share of the nitro stock.")]
        [Range(0f, 1f)] public float nitroRefillWantedBelow = 0.5f;

        [Header("Holding the point (only in scenes with a CapturePointZone)")]
        [Tooltip("Chance that this bot is a holder, rolled each time it spawns: a holder goes to the flag zone and defends it, the others hunt as usual. In a team of 3 the average number of holders is 3 × this.")]
        [Range(0f, 1f)] public float pointDuty = 0.7f;

        [Tooltip("A holder moves to a new random spot inside the zone this often, so it does not stand still, s.")]
        [Min(1f)] public float holdSpotInterval = 4f;

        [Tooltip("While fighting, a holder outside the zone is pulled back towards it, the harder the farther it is. This is the distance beyond the zone edge at which the pull is full, units.")]
        [Min(0.5f)] public float pointLeash = 3f;

        [Tooltip("Strength of that pull relative to the circling movement. 0 = none.")]
        [Min(0f)] public float pointPull = 1.5f;

        [Header("Choosing the target (several enemies)")]
        [Tooltip("A target is kept at least this long before the bot may switch to another, s.")]
        [Min(0f)] public float targetHoldTime = 2f;

        [Tooltip("The bot switches only if the new target's cost (the way to it, lowered for wounded and aiming enemies) is below the current one's times this (0.7 = at least 30% better). A target that is lost in a bush is dropped at once.")]
        [Range(0.1f, 1f)] public float retargetDistanceRatio = 0.7f;

        [Header("Choosing the target: what makes an enemy a better target")]
        [Tooltip("How much a wounded enemy is preferred: its cost (the length of the way to it) shrinks by this share at zero health. 0 = health does not matter.")]
        [Range(0f, 0.9f)] public float woundedTargetPreference = 0.3f;

        [Tooltip("How much an enemy that is aiming at the bot is preferred: its cost shrinks by this share. 0 = ignore.")]
        [Range(0f, 0.9f)] public float threatTargetPreference = 0.3f;

        [Tooltip("An enemy counts as aiming at the bot when its hull points at it within this angle, deg.")]
        [Range(1f, 45f)] public float threatAngle = 12f;

        [Header("Explosive shells")]
        [Tooltip("With explosive shells the bot also shoots at cover next to an enemy when the burst would reach it: the burst point must be this share of the blast radius from the enemy or closer. 0 = never.")]
        [Range(0f, 1f)] public float splashReach = 0.8f;

        [Header("Searching bushes")]
        [Tooltip("When the enemy vanished where it was last seen, the bot checks this many nearby bushes (drives up and shoots into each) before patrolling. 0 = no search.")]
        [Min(0)] public int searchBushes = 2;

        [Tooltip("Bushes farther than this from where the enemy vanished are not searched, units.")]
        [Min(1f)] public float searchRadius = 5f;

        [Tooltip("The whole search ends after this long, s.")]
        [Min(1f)] public float searchTime = 8f;

        [Tooltip("From this distance or closer the bot shoots into the bush instead of driving into it, units.")]
        [Min(0.5f)] public float searchShotDistance = 4f;

        [Header("Patrol")]
        [Tooltip("A patrol point is at least this far from the bot, units.")]
        [Min(1f)] public float patrolMinDistance = 6f;

        [Tooltip("A patrol point is dropped and a new one picked after this long, s.")]
        [Min(1f)] public float patrolMaxTime = 12f;

        [Header("Dodge")]
        [Tooltip("Chance to try to dodge a shot that would hit. 0 = never dodges, 1 = every time.")]
        [Range(0f, 1f)] public float dodgeChance = 0.7f;

        [Tooltip("A shot counts as a threat if it reaches the hull within this time, s.")]
        [Min(0.1f)] public float dodgeLookahead = 0.5f;

        [Tooltip("Shots farther than this are not noticed, units. About the half-width of the screen: the bot only reacts to what a player could see.")]
        [Min(1f)] public float dodgeSightRange = 9f;

        [Tooltip("Delay between noticing the threat and starting to dodge, s (random in the range).")]
        [Min(0f)] public float dodgeReactionMin = 0.08f;

        [Min(0f)] public float dodgeReactionMax = 0.2f;

        [Tooltip("A dodge lasts at most this long, s.")]
        [Min(0.1f)] public float dodgeDuration = 0.5f;

        [Tooltip("The side step is checked for free space this far, units.")]
        [Min(0.5f)] public float dodgeDistance = 1.5f;

        [Tooltip("Extra clearance around the hull when deciding that a shot would hit, units.")]
        [Min(0f)] public float dodgeMargin = 0.1f;

        [Tooltip("If the shot reaches the bot sooner than this, the dodge uses nitro (when available), s. 0 = never.")]
        [Min(0f)] public float dodgeNitroTime = 0.3f;

        [Header("Vision (what a player would see on the screen)")]
        [Tooltip("The bot notices enemies and pick-ups only this far sideways from itself, units: half the width of the player's screen (~9).")]
        [Min(1f)] public float viewHalfWidth = 9f;

        [Tooltip("The same up and down, units: half the height of the player's screen (~5).")]
        [Min(1f)] public float viewHalfHeight = 5f;

        [Header("Perception")]
        [Tooltip("How often the line of sight and incoming shots are re-checked, s.")]
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
            if (strafeSwitchMax < strafeSwitchMin)
                strafeSwitchMax = strafeSwitchMin;
            if (dodgeReactionMax < dodgeReactionMin)
                dodgeReactionMax = dodgeReactionMin;
            if (coverMaxDistance < coverMinDistance)
                coverMaxDistance = coverMinDistance;
        }
    }
}
