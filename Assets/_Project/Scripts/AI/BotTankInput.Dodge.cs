using TankBrawl.Map;
using TankBrawl.Weapons;
using UnityEngine;

namespace TankBrawl.AI
{
    // Dodge: a short side step away from a shot that would hit. The bot only reacts to what a player could see:
    // projectiles are visible everywhere on the screen (bushes hide only tanks), so the limit is the screen-sized
    // dodgeSightRange, and the bot reacts with a human delay and not always (dodgeChance). One bounce off steel is
    // predicted, so the bot also steps away from a ricochet, its own shell included. It keeps shooting while it dodges
    // if the hull happens to point at the target.
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
                    ApplyDodge(now, position);
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
                        ApplyDodge(now, position);
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

        // Would this shot hit the hull within the lookahead if the bot stayed on its course? Walls in between stop it;
        // steel with a bounce left reflects it once more. Shells of the own team are a threat only when they are the bot's
        // own and can come back off steel (a projectile spares its shooter until its first bounce).
        bool TryEvaluateThreat(Projectile shot, Vector2 position, out float timeToHit)
        {
            timeToHit = 0f;
            if (shot == null || shot.IsFinished)
                return false;

            bool own = shot.Team == member.Team;
            if (own && shot.Owner != gameObject)
                return false;

            WeaponConfig shotConfig = shot.Config;
            if (shotConfig == null || shot.Speed <= 0f)
                return false;

            Vector2 origin = shot.transform.position;
            if ((position - origin).sqrMagnitude > profile.dodgeSightRange * profile.dodgeSightRange)
                return false;

            // How far the shot flies within the lookahead.
            float budget = shot.Speed * profile.dodgeLookahead;
            Vector2 direction = shot.Direction;
            if ((!own || shot.Bounces > 0) && LegHits(origin, direction, position, shotConfig, budget, out float contact))
            {
                timeToHit = contact / shot.Speed;
                return true;
            }

            if (shot.BouncesLeft > 0
                && TryBounce(origin, direction, shotConfig.wallHitRadius, budget, out Vector2 bounce, out Vector2 reflected, out float travelled)
                && LegHits(bounce, reflected, position, shotConfig, budget - travelled, out float rest))
            {
                timeToHit = (travelled + rest) / shot.Speed;
                return true;
            }

            return false;
        }

        // Does a shot leaving `origin` along `direction` touch the hull within `budget` units, with no wall before it?
        bool LegHits(Vector2 origin, Vector2 direction, Vector2 position, WeaponConfig shotConfig, float budget, out float contact)
        {
            contact = 0f;
            Vector2 offset = position - origin;
            float along = Vector2.Dot(offset, direction);
            if (along <= 0f)
                return false;

            float side = direction.x * offset.y - direction.y * offset.x;
            float reach = bodyRadius + shotConfig.tankHitRadius + profile.dodgeMargin;
            if (Mathf.Abs(side) >= reach)
                return false;

            contact = Mathf.Max(0f, along - Mathf.Sqrt(reach * reach - side * side));
            if (contact > budget)
                return false;

            return contact <= 0f || !WallBetween(origin, direction, contact, shotConfig.wallHitRadius);
        }

        // The first wall within `budget`: when it is steel, where the shot's centre bounces and where it flies next.
        bool TryBounce(Vector2 origin, Vector2 direction, float radius, float budget, out Vector2 bounce, out Vector2 reflected, out float travelled)
        {
            bounce = default;
            reflected = default;
            travelled = 0f;

            int count = Physics2D.CircleCast(origin, radius, direction, wallFilter, wallHits, budget);
            int nearest = -1;
            for (int i = 0; i < count; i++)
            {
                if (Vector2.Dot(direction, wallHits[i].normal) <= 0f && (nearest < 0 || wallHits[i].distance < wallHits[nearest].distance))
                    nearest = i;
            }

            if (nearest < 0 || wallHits[nearest].collider.GetComponentInParent<RicochetSurface>() == null)
                return false;

            RaycastHit2D hit = wallHits[nearest];
            travelled = hit.distance;
            bounce = hit.centroid;
            reflected = Vector2.Reflect(direction, hit.normal);
            return true;
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

        // The side step drives; the gun fires on the way if the hull happens to point at the target.
        void ApplyDodge(float now, Vector2 position)
        {
            move = dodgeDirection;
            nitroHeld = dodgeUsesNitro;
            fire = false;
            TryOpportunisticFire(now, position);
        }
    }
}
