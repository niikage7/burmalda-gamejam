using TankBrawl.Weapons;
using UnityEngine;

namespace TankBrawl.AI
{
    // Dodge: a short side step away from a shot that would hit. The bot only reacts to what a player could see:
    // projectiles are visible everywhere on the screen (bushes hide only tanks), so the limit is the screen-sized
    // dodgeSightRange, and the bot reacts with a human delay and not always (dodgeChance).
    public partial class BotTankInput
    {
        // Stay this long out of the scan after a failed roll or an impossible dodge, s.
        const float DodgeRetryPause = 0.3f;

        bool dodging;
        float dodgeUntil;
        Vector2 dodgeDirection;
        bool dodgeUsesNitro;
        Projectile dodgeThreat;

        bool dodgePending;
        float dodgeReactAt;
        Projectile pendingThreat;

        float nextDodgeScanAt;
        float dodgeIgnoreUntil;

        void ResetDodge()
        {
            dodging = false;
            dodgePending = false;
            dodgeThreat = null;
            pendingThreat = null;
            nextDodgeScanAt = 0f;
            dodgeIgnoreUntil = 0f;
        }

        // Returns true when the dodge is driving the tank this frame (move/fire/nitro are set).
        bool UpdateDodge(float now, Vector2 position)
        {
            if (profile.dodgeChance <= 0f)
            {
                dodging = false;
                return false;
            }

            if (dodging)
            {
                if (now < dodgeUntil && dodgeThreat != null && !dodgeThreat.IsFinished && !HasPassed(dodgeThreat, position))
                {
                    ApplyDodge();
                    return true;
                }

                dodging = false;
            }

            if (now >= nextDodgeScanAt)
            {
                nextDodgeScanAt = now + profile.perceptionInterval;
                if (!dodgePending && now >= dodgeIgnoreUntil)
                    ScanThreats(now, position);
            }

            if (dodgePending && now >= dodgeReactAt)
            {
                dodgePending = false;
                if (pendingThreat != null && TryEvaluateThreat(pendingThreat, position, out float timeToHit))
                {
                    if (BeginDodge(now, position, pendingThreat, timeToHit))
                    {
                        ApplyDodge();
                        return true;
                    }

                    dodgeIgnoreUntil = now + DodgeRetryPause;
                }
            }

            return false;
        }

        // Finds the shot that arrives soonest; if there is one, rolls the dice and starts the reaction delay.
        void ScanThreats(float now, Vector2 position)
        {
            Projectile soonest = null;
            float soonestTime = float.MaxValue;

            var flying = Projectile.InFlight;
            for (int i = 0; i < flying.Count; i++)
            {
                if (TryEvaluateThreat(flying[i], position, out float timeToHit) && timeToHit < soonestTime)
                {
                    soonest = flying[i];
                    soonestTime = timeToHit;
                }
            }

            if (soonest == null)
                return;

            if (Random.value > profile.dodgeChance)
            {
                dodgeIgnoreUntil = now + profile.dodgeLookahead;
                return;
            }

            dodgePending = true;
            pendingThreat = soonest;
            dodgeReactAt = now + Random.Range(profile.dodgeReactionMin, profile.dodgeReactionMax);
        }

        // Would this shot hit the hull within the lookahead if the bot stayed on its course? Walls in between stop it
        // (a ricochet that comes back is not predicted).
        bool TryEvaluateThreat(Projectile shot, Vector2 position, out float timeToHit)
        {
            timeToHit = 0f;
            if (shot == null || shot.IsFinished || shot.Team == member.Team)
                return false;

            WeaponConfig shotConfig = shot.Config;
            if (shotConfig == null || shot.Speed <= 0f)
                return false;

            Vector2 origin = shot.transform.position;
            Vector2 offset = position - origin;
            if (offset.sqrMagnitude > profile.dodgeSightRange * profile.dodgeSightRange)
                return false;

            Vector2 direction = shot.Direction;
            float along = Vector2.Dot(offset, direction);
            if (along <= 0f)
                return false;

            float side = direction.x * offset.y - direction.y * offset.x;
            float reach = bodyRadius + shotConfig.tankHitRadius + profile.dodgeMargin;
            if (Mathf.Abs(side) >= reach)
                return false;

            float contact = Mathf.Max(0f, along - Mathf.Sqrt(reach * reach - side * side));
            timeToHit = contact / shot.Speed;
            if (timeToHit > profile.dodgeLookahead)
                return false;

            return contact <= 0f || !WallBetween(origin, direction, contact, shotConfig.wallHitRadius);
        }

        // Same rule as the projectile itself: a wall behind the shot (the one it just left) does not count.
        bool WallBetween(Vector2 origin, Vector2 direction, float distance, float radius)
        {
            int count = Physics2D.CircleCast(origin, radius, direction, wallFilter, wallHits, distance);
            for (int i = 0; i < count; i++)
            {
                if (Vector2.Dot(direction, wallHits[i].normal) <= 0f)
                    return true;
            }

            return false;
        }

        bool HasPassed(Projectile shot, Vector2 position)
        {
            return Vector2.Dot(position - (Vector2)shot.transform.position, shot.Direction) < -bodyRadius;
        }

        // Picks the side step: perpendicular to the shot's line, towards the side with free space, preferring the side
        // the bot is already on and the one that needs less turning.
        bool BeginDodge(float now, Vector2 position, Projectile shot, float timeToHit)
        {
            Vector2 direction = shot.Direction;
            Vector2 normal = new Vector2(-direction.y, direction.x);
            Vector2 offset = position - (Vector2)shot.transform.position;
            float side = direction.x * offset.y - direction.y * offset.x;

            bool found = false;
            float bestScore = float.MinValue;
            for (int s = -1; s <= 1; s += 2)
            {
                Vector2 candidate = normal * s;
                if (nav != null && !nav.IsSegmentWalkable(position, position + candidate * profile.dodgeDistance))
                    continue;

                float score = (side * s >= 0f ? 0.5f : 0f) + 0.5f * Vector2.Dot(candidate, tank.Forward) + Random.Range(0f, 0.1f);
                if (score > bestScore)
                {
                    bestScore = score;
                    dodgeDirection = candidate;
                    found = true;
                }
            }

            if (!found)
                return false;

            dodging = true;
            dodgeThreat = shot;
            dodgeUntil = now + profile.dodgeDuration;
            dodgeUsesNitro = nitro != null && profile.dodgeNitroTime > 0f && timeToHit < profile.dodgeNitroTime
                             && !nitro.IsLocked && nitro.Normalized > profile.nitroStopStock;
            return true;
        }

        void ApplyDodge()
        {
            move = dodgeDirection;
            fire = false;
            nitroHeld = dodgeUsesNitro;
        }
    }
}
