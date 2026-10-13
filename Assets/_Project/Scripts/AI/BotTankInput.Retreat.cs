using System.Collections.Generic;
using TankBrawl.PowerUps;
using UnityEngine;

namespace TankBrawl.AI
{
    // Retreat (low health: to a power-up or to cover) and the power-up hunt that Seek and Retreat share.
    public partial class BotTankInput
    {
        // Cover rating: out of the enemy's line of fire counts most, then distance from the enemy, then a short way there.
        const float CoverHiddenScore = 10f;
        const float CoverAwayWeight = 0.5f;
        const float CoverCostWeight = 0.4f;

        // Do not keep a list of unreachable power-ups longer than this.
        const int MaxUnreachableEntries = 8;

        // For how long a power-up that could not be reached is forgotten, s.
        const float UnreachableForgetTime = 5f;

        // Retreat
        float retreatStartedAt;
        float retreatLockUntil;
        float nextCoverPlanAt;
        Vector2 retreatGoal;
        PowerUpPickup retreatPickup;
        bool retreatGoalWasPickup;

        // Power-up hunt
        PowerUpPickup pickupGoal;
        float pickupCommittedAt;
        readonly Dictionary<PowerUpPickup, float> unreachableUntil = new Dictionary<PowerUpPickup, float>();

        void ResetRetreat()
        {
            retreatLockUntil = 0f;
            nextCoverPlanAt = 0f;
            retreatPickup = null;
            retreatGoalWasPickup = false;
            ReleasePickup();
            unreachableUntil.Clear();
        }

        // ---- Retreat ----

        bool ShouldRetreat(float now)
        {
            return health != null
                   && profile.retreatHealthFraction > 0f
                   && now >= retreatLockUntil
                   && target != null
                   && hasLastKnown
                   && health.Current <= health.Max * profile.retreatHealthFraction;
        }

        // Picks where to run. Returns false when there is nowhere to go (then the bot just keeps fighting for a while).
        bool BeginRetreat(float now, Vector2 position)
        {
            retreatStartedAt = now;
            ReleasePickup();
            if (PlanRetreat(now, position))
                return true;

            retreatLockUntil = now + profile.coverReplanInterval;
            return false;
        }

        bool ContinueRetreat(float now, Vector2 position)
        {
            if (target == null || now - retreatStartedAt > profile.retreatDuration)
                return false;

            // Cornered: an enemy with a clear line right here. Running is pointless, fight.
            if (targetVisible && hasLineOfSight && targetDistance <= profile.retreatCornerDistance)
                return false;

            // The power-up was taken (by us or by someone else): look for cover now.
            if (retreatPickup == null && retreatGoalWasPickup)
                return PlanRetreat(now, position);

            // On the way to cover while the enemy still sees the bot: choose better cover now and then (keeps the old one if none is found).
            if (!retreatGoalWasPickup && hasLineOfSight && now >= nextCoverPlanAt)
                PlanRetreat(now, position);

            return true;
        }

        void EndRetreat(float now)
        {
            retreatLockUntil = now + profile.retreatLockout;
            ReleasePickup();
            retreatPickup = null;
            retreatGoalWasPickup = false;
        }

        bool PlanRetreat(float now, Vector2 position)
        {
            nextCoverPlanAt = now + profile.coverReplanInterval;
            if (nav == null)
                return false;

            Vector2 enemy = hasLastKnown ? lastKnownPosition : targetPosition;

            // A power-up within reach beats any cover, if the bot gets there first and the enemy is not standing on it.
            PowerUpPickup pickup = FindPickup(position, profile.retreatPickupDistance, true, enemy);
            if (pickup != null && TryClaimPickup(now, position, pickup))
            {
                retreatPickup = pickup;
                retreatGoalWasPickup = true;
                retreatGoal = pickup.transform.position;
                return true;
            }

            retreatPickup = null;
            retreatGoalWasPickup = false;
            ReleasePickup();

            // Already out of sight: stay.
            if (!IsLineClear(position, enemy))
            {
                retreatGoal = position;
                return true;
            }

            bool found = false;
            float bestScore = float.MinValue;
            Vector2 best = position;
            for (int i = 0; i < profile.coverSamples; i++)
            {
                if (!nav.TryGetRandomPoint(position, profile.coverMinDistance, profile.coverMaxDistance, out Vector2 candidate))
                    continue;

                float score = (IsLineClear(candidate, enemy) ? 0f : CoverHiddenScore)
                              + Vector2.Distance(candidate, enemy) * CoverAwayWeight
                              - Vector2.Distance(candidate, position) * CoverCostWeight;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                    found = true;
                }
            }

            if (!found || !BuildPath(now, position, best))
                return false;

            retreatGoal = best;
            return true;
        }

        bool ActRetreat(float now, Vector2 position)
        {
            TryOpportunisticFire(now, position);

            Vector2 goal = retreatPickup != null ? (Vector2)retreatPickup.transform.position : retreatGoal;
            move = PathHeading(now, position, goal);
            if (move != Vector2.zero)
                return true;

            // In cover: wait there with the gun turned to where the enemy was.
            Vector2 toEnemy = (hasLastKnown ? lastKnownPosition : targetPosition) - position;
            if (toEnemy.sqrMagnitude > ArriveDistance * ArriveDistance)
                move = toEnemy.normalized * turnOnlyMagnitude;

            return false;
        }

        // ---- Power-ups ----

        // Decides whether the bot is on its way to a power-up. Returns true when it is (pickupGoal is set).
        bool UpdatePowerUpGoal(float now, Vector2 position, bool canShoot)
        {
            if (profile.powerUpGreed <= 0f)
            {
                ReleasePickup();
                return false;
            }

            if (pickupGoal != null)
            {
                bool giveUp = now - pickupCommittedAt > profile.powerUpGiveUpTime;
                if (!giveUp && IsWanted(pickupGoal))
                    return true;

                if (giveUp)
                    MarkUnreachable(now, pickupGoal);
                ReleasePickup();
            }

            float limit = (canShoot ? profile.combatPowerUpDistance : profile.powerUpMaxDistance) * profile.powerUpGreed;
            PowerUpPickup best = FindPickup(position, limit);
            return best != null && TryClaimPickup(now, position, best);
        }

        // Nearest wanted pick-up within `limit` (straight line): not claimed by another bot, not a kind we already have,
        // not known to be unreachable. With `safeFromEnemy` the bot must be nearer to it than the enemy, and the enemy
        // must not be within the corner distance of it (used when retreating).
        PowerUpPickup FindPickup(Vector2 position, float limit, bool safeFromEnemy = false, Vector2 enemy = default)
        {
            PowerUpPickup best = null;
            float bestSqr = limit * limit;

            var onMap = PowerUpPickup.OnMap;
            for (int i = 0; i < onMap.Count; i++)
            {
                PowerUpPickup candidate = onMap[i];
                if (candidate == null || !IsWanted(candidate) || IsClaimedByOther(candidate) || IsUnreachable(candidate))
                    continue;

                Vector2 at = candidate.transform.position;
                if (!InView(position, at))
                    continue;

                float sqr = (at - position).sqrMagnitude;
                if (sqr >= bestSqr)
                    continue;

                if (safeFromEnemy)
                {
                    float enemySqr = (at - enemy).sqrMagnitude;
                    if (enemySqr < sqr || enemySqr < profile.retreatCornerDistance * profile.retreatCornerDistance)
                        continue;
                }

                bestSqr = sqr;
                best = candidate;
            }

            return best;
        }

        // Takes the pick-up as the goal if a path to it exists (the path is built right here and used by the next steering call).
        bool TryClaimPickup(float now, Vector2 position, PowerUpPickup pickup)
        {
            if (nav != null && !BuildPath(now, position, pickup.transform.position))
            {
                MarkUnreachable(now, pickup);
                return false;
            }

            ReleasePickup();
            pickupGoal = pickup;
            pickupCommittedAt = now;
            pickup.ClaimedBy = this;
            return true;
        }

        void ReleasePickup()
        {
            if (pickupGoal != null && pickupGoal.ClaimedBy == this)
                pickupGoal.ClaimedBy = null;

            pickupGoal = null;
        }

        // Instant kinds are wanted only when they would help now; the others when the bot does not have that effect yet.
        bool IsWanted(PowerUpPickup pickup)
        {
            PowerUpConfig config = pickup.Config;
            if (config == null)
                return false;

            switch (config.kind)
            {
                case PowerUpKind.Repair:
                    return health != null && health.Current < health.Max * profile.repairWantedBelow;
                case PowerUpKind.NitroRefill:
                    return nitro != null && nitro.Normalized < profile.nitroRefillWantedBelow;
                default:
                    return powerUps == null || !powerUps.IsActive(config.kind);
            }
        }

        bool IsClaimedByOther(PowerUpPickup pickup)
        {
            Component claimer = pickup.ClaimedBy;
            return claimer != null && claimer != this && claimer.gameObject.activeInHierarchy;
        }

        bool IsUnreachable(PowerUpPickup pickup)
        {
            return unreachableUntil.TryGetValue(pickup, out float until) && Time.time < until;
        }

        void MarkUnreachable(float now, PowerUpPickup pickup)
        {
            if (unreachableUntil.Count >= MaxUnreachableEntries)
                unreachableUntil.Clear();

            unreachableUntil[pickup] = now + UnreachableForgetTime;
        }
    }
}
