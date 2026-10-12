using TankBrawl.Map;
using UnityEngine;

namespace TankBrawl.AI
{
    // Hold: defending the flag zone of the point-holding mode. Only bots that rolled "holder" (BotProfile.pointDuty) take part,
    // and only in scenes that have a CapturePointZone. A holder drives into the zone, wanders between random spots inside it
    // (a stationary tank is an easy target) with the gun turned to where the enemy was, and when it has a clear shot it fights
    // as usual (Attack) but is pulled back towards the zone.
    public partial class BotTankInput
    {
        // Hold spots are picked within this share of the zone radius, so the bot stays well inside.
        const float HoldSpotRadiusFactor = 0.7f;

        bool holdsPoint;
        bool hasHoldSpot;
        Vector2 holdSpot;
        float holdSpotPickedAt;

        void ResetHold()
        {
            holdsPoint = profile != null && Random.value < profile.pointDuty;
            hasHoldSpot = false;
        }

        bool WantsToHold => holdsPoint && CapturePointZone.Primary != null;

        bool ActHold(float now, Vector2 position)
        {
            CapturePointZone zone = CapturePointZone.Primary;
            if (zone == null)
                return false;

            if (!hasHoldSpot || now - holdSpotPickedAt > profile.holdSpotInterval)
                PickHoldSpot(now, zone);

            move = PathHeading(now, position, holdSpot);
            TryOpportunisticFire(now, position);
            if (move != Vector2.zero)
                return true;

            // Arrived (or the spot cannot be reached: pick another next frame). Face where the enemy was.
            if (pathFailed)
                hasHoldSpot = false;

            if (hasLastKnown)
            {
                Vector2 toEnemy = lastKnownPosition - position;
                if (toEnemy.sqrMagnitude > ArriveDistance * ArriveDistance)
                    move = toEnemy.normalized * turnOnlyMagnitude;
            }

            return false;
        }

        void PickHoldSpot(float now, CapturePointZone zone)
        {
            holdSpotPickedAt = now;
            hasHoldSpot = true;
            holdSpot = zone.Center;

            if (nav != null && nav.TryGetPointAround(zone.Center, 0f, zone.Radius * HoldSpotRadiusFactor, out Vector2 spot))
                holdSpot = spot;

            InvalidatePath();
        }

        // While fighting a holder drifts out of the zone (circling the target); this bends its heading back towards the zone.
        Vector2 PullToPoint(Vector2 position, Vector2 heading)
        {
            CapturePointZone zone = CapturePointZone.Primary;
            if (!holdsPoint || zone == null || profile.pointPull <= 0f)
                return heading;

            Vector2 toCenter = zone.Center - position;
            float distance = toCenter.magnitude;
            float over = distance - zone.Radius;
            if (over <= 0f)
                return heading;

            float weight = Mathf.Clamp01(over / profile.pointLeash) * profile.pointPull;
            return (heading + toCenter / distance * weight).normalized;
        }
    }
}
