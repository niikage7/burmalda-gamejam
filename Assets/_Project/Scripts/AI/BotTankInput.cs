using System.Collections.Generic;
using TankBrawl.Core;
using TankBrawl.Map;
using TankBrawl.Tank;
using TankBrawl.Weapons;
using UnityEngine;

namespace TankBrawl.AI
{
    public enum BotState
    {
        /// <summary>No target.</summary>
        Idle,

        /// <summary>Goes along a path to the target or to where it was last seen.</summary>
        Chase,

        /// <summary>Sees the target within shooting range: aims, shoots, closes in to the stop distance.</summary>
        Engage
    }

    /// <summary>
    /// Drives a tank like a player would: only through <see cref="ITankInput"/>, so the tank has the same limits
    /// (the gun looks where the hull looks, the weapon has its cooldown). Chases the nearest enemy, shoots when the
    /// line of sight is clear, cannot see enemies standing in bushes, and backs off when stuck.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(TankController), typeof(TankWeapon))]
    [RequireComponent(typeof(TeamMember))]
    public class BotTankInput : MonoBehaviour, ITankInput
    {
        const int MaxWallHits = 4;

        // Stick deflection for turning in place, as a fraction of TankConfig.driveThreshold (below it the hull only turns).
        const float TurnOnlyScale = 0.5f;

        // Bots share one list of all tanks instead of each scanning the scene.
        const float CandidateRefreshInterval = 0.5f;

        static readonly List<TeamMember> Candidates = new List<TeamMember>();
        static float candidatesRefreshAt;

        [SerializeField] BotProfile profile;

        Rigidbody2D body;
        TankController tank;
        TankWeapon weapon;
        TeamMember member;
        BotNavGrid nav;
        ContactFilter2D wallFilter;
        readonly RaycastHit2D[] wallHits = new RaycastHit2D[MaxWallHits];
        readonly List<Vector2> path = new List<Vector2>();

        Vector2 move;
        bool fire;
        float turnOnlyMagnitude;

        TeamMember target;
        Transform targetTransform;
        Rigidbody2D targetBody;
        TankConcealment targetConcealment;
        Vector2 targetPosition;
        float targetDistance;
        float retargetAt;
        float nextPerceptionAt;
        float reactionReadyAt;
        bool targetVisible;
        bool hasLineOfSight;
        bool directClear;
        bool hasLastKnown;
        Vector2 lastKnownPosition;
        float aimOffsetDegrees;

        bool hasPath;
        int pathIndex;
        int pathVersion;
        Vector2 pathGoal;
        float nextRepathAt;

        bool stuckWindowOpen;
        float stuckWindowStart;
        Vector2 stuckWindowPosition;
        float reverseUntil;
        Vector2 reverseDirection;

        public Vector2 Move => move;
        public bool FireHeld => fire;
        public bool NitroHeld => false;
        public bool CycleAmmoPressed => false;

        public BotState State { get; private set; }
        public TeamMember Target => target;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Candidates.Clear();
            candidatesRefreshAt = 0f;
        }

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            tank = GetComponent<TankController>();
            weapon = GetComponent<TankWeapon>();
            member = GetComponent<TeamMember>();

            wallFilter = new ContactFilter2D();
            wallFilter.useTriggers = false;
            wallFilter.useLayerMask = true;
            wallFilter.layerMask = LayerMask.GetMask("Wall");

            if (profile == null)
                Debug.LogError($"{name}: BotProfile is not assigned, the bot will stand still.", this);
        }

        void OnEnable()
        {
            nav = FindAnyObjectByType<BotNavGrid>();
            if (nav == null)
                Debug.LogError($"{name}: no BotNavGrid in the scene, the bot will drive straight at its target.", this);

            if (tank.Config != null)
                turnOnlyMagnitude = tank.Config.driveThreshold * TurnOnlyScale;

            weapon.Fired += OnFired;
            ResetState();
        }

        void OnDisable()
        {
            weapon.Fired -= OnFired;
            Stop();
        }

        void Update()
        {
            if (profile == null || weapon.Config == null)
            {
                Stop();
                return;
            }

            float now = Time.time;
            Vector2 position = body.position;

            if (now >= retargetAt)
            {
                retargetAt = now + profile.retargetInterval;
                PickTarget(position);
            }

            Perceive(now, position);

            if (now < reverseUntil)
            {
                move = reverseDirection;
                fire = false;
                stuckWindowOpen = false;
                return;
            }

            bool drives = Think(now, position);
            UpdateStuck(now, position, drives);
        }

        void OnFired()
        {
            RollAim();
        }

        void ResetState()
        {
            Stop();
            State = BotState.Idle;
            target = null;
            targetTransform = null;
            targetBody = null;
            targetConcealment = null;
            retargetAt = 0f;
            nextPerceptionAt = 0f;
            targetVisible = hasLineOfSight = directClear = hasLastKnown = false;
            hasPath = false;
            nextRepathAt = 0f;
            stuckWindowOpen = false;
            reverseUntil = 0f;
            RollAim();
        }

        void Stop()
        {
            move = Vector2.zero;
            fire = false;
        }

        void RollAim()
        {
            aimOffsetDegrees = profile != null ? Random.Range(-profile.aimSpreadDegrees, profile.aimSpreadDegrees) : 0f;
        }

        // ---- Target and perception ----

        static void RefreshCandidates()
        {
            if (Time.time < candidatesRefreshAt)
                return;

            candidatesRefreshAt = Time.time + CandidateRefreshInterval;
            Candidates.Clear();
            Candidates.AddRange(FindObjectsByType<TeamMember>(FindObjectsInactive.Exclude));
        }

        void PickTarget(Vector2 position)
        {
            RefreshCandidates();

            TeamMember best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < Candidates.Count; i++)
            {
                TeamMember candidate = Candidates[i];
                if (candidate == null || candidate.Team == member.Team || !candidate.gameObject.activeInHierarchy)
                    continue;

                float sqr = ((Vector2)candidate.transform.position - position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    best = candidate;
                    bestSqr = sqr;
                }
            }

            if (best != target)
                SetTarget(best);
        }

        void SetTarget(TeamMember newTarget)
        {
            target = newTarget;
            targetTransform = newTarget != null ? newTarget.transform : null;
            targetBody = null;
            targetConcealment = null;
            if (newTarget != null)
            {
                newTarget.TryGetComponent(out targetBody);
                newTarget.TryGetComponent(out targetConcealment);
            }

            targetVisible = hasLineOfSight = directClear = hasLastKnown = false;
            hasPath = false;
        }

        void Perceive(float now, Vector2 position)
        {
            // A dead tank is deactivated.
            if (target != null && !target.gameObject.activeInHierarchy)
                SetTarget(null);

            if (target == null)
                return;

            targetPosition = targetTransform.position;
            targetDistance = Vector2.Distance(position, targetPosition);
            targetVisible = targetConcealment == null || !targetConcealment.IsHiddenFrom(member.Team);

            if (targetVisible)
            {
                lastKnownPosition = targetPosition;
                hasLastKnown = true;
            }
            else
            {
                hasLineOfSight = directClear = false;
            }

            if (now < nextPerceptionAt)
                return;
            nextPerceptionAt = now + profile.perceptionInterval;
            if (!targetVisible)
                return;

            bool seen = IsLineClear(position, targetPosition);
            if (seen && !hasLineOfSight)
            {
                reactionReadyAt = now + Random.Range(profile.reactionDelayMin, profile.reactionDelayMax);
                RollAim();
            }

            hasLineOfSight = seen;
            directClear = seen && (nav == null || nav.IsSegmentWalkable(position, targetPosition));
        }

        // The projectile's wall hitbox swept along the line: if it gets through, the shot gets through.
        bool IsLineClear(Vector2 from, Vector2 to)
        {
            Vector2 offset = to - from;
            float distance = offset.magnitude;
            if (distance < Mathf.Epsilon)
                return true;

            int count = Physics2D.CircleCast(from, weapon.Config.wallHitRadius, offset / distance, wallFilter, wallHits, distance);
            return count == 0;
        }

        // ---- Decisions ----

        // Sets move/fire for this frame. Returns true when the bot wants to drive (used to detect being stuck).
        bool Think(float now, Vector2 position)
        {
            move = Vector2.zero;
            fire = false;

            if (target != null && hasLineOfSight && targetDistance <= profile.fireRange)
            {
                State = BotState.Engage;
                Vector2 aim = AimDirection(position);
                bool close = targetDistance <= profile.stopDistance;

                Vector2 heading = aim;
                if (!close && !directClear)
                {
                    Vector2 along = PathHeading(now, position, targetPosition);
                    if (along != Vector2.zero)
                        heading = along;
                }

                move = close ? heading * turnOnlyMagnitude : heading;

                bool aligned = Vector2.Angle(tank.Forward, aim) <= profile.fireAlignTolerance;
                fire = aligned && now >= reactionReadyAt && !SteelTooClose(position);
                return !close;
            }

            if (hasLastKnown)
            {
                State = BotState.Chase;
                move = PathHeading(now, position, lastKnownPosition);
                return move != Vector2.zero;
            }

            State = BotState.Idle;
            return false;
        }

        // Where the hull should point to hit the target: its position, partly led by its velocity, then the random spread.
        Vector2 AimDirection(Vector2 position)
        {
            Vector2 point = targetPosition;
            float speed = weapon.Config.speed;
            if (profile.leadFactor > 0f && targetBody != null && speed > 0f)
                point += targetBody.linearVelocity * (targetDistance / speed * profile.leadFactor);

            Vector2 direction = point - position;
            direction = direction.sqrMagnitude > Mathf.Epsilon ? direction.normalized : tank.Forward;
            return Quaternion.Euler(0f, 0f, aimOffsetDegrees) * direction;
        }

        // A shot that hits steel right in front of the muzzle bounces back at the shooter.
        bool SteelTooClose(Vector2 position)
        {
            float reach = Mathf.Min(profile.steelPointBlankDistance, targetDistance);
            int count = Physics2D.CircleCast(position, weapon.Config.wallHitRadius, tank.Forward, wallFilter, wallHits, reach);

            int nearest = -1;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (wallHits[i].distance < nearestDistance)
                {
                    nearest = i;
                    nearestDistance = wallHits[i].distance;
                }
            }

            return nearest >= 0 && wallHits[nearest].collider.GetComponentInParent<RicochetSurface>() != null;
        }

        // ---- Path following ----

        // Unit vector to the next waypoint of the path to `goal`; zero when there is no path or the bot has arrived.
        Vector2 PathHeading(float now, Vector2 position, Vector2 goal)
        {
            if (nav == null)
            {
                Vector2 straight = goal - position;
                return straight.sqrMagnitude > profile.waypointRadius * profile.waypointRadius ? straight.normalized : Vector2.zero;
            }

            EnsurePath(now, position, goal);
            if (!hasPath)
                return Vector2.zero;

            float radiusSqr = profile.waypointRadius * profile.waypointRadius;
            while (pathIndex < path.Count && (path[pathIndex] - position).sqrMagnitude <= radiusSqr)
                pathIndex++;

            return pathIndex < path.Count ? (path[pathIndex] - position).normalized : Vector2.zero;
        }

        void EnsurePath(float now, Vector2 position, Vector2 goal)
        {
            if (now < nextRepathAt)
                return;

            float goalShift = profile.repathGoalDistance;
            bool needed = !hasPath
                          || pathVersion != nav.Version
                          || (goal - pathGoal).sqrMagnitude > goalShift * goalShift;
            if (!needed)
                return;

            nextRepathAt = now + profile.repathInterval;
            hasPath = nav.TryFindPath(position, goal, path);
            pathIndex = 0;
            pathGoal = goal;
            pathVersion = nav.Version;
        }

        // ---- Unstuck ----

        void UpdateStuck(float now, Vector2 position, bool drives)
        {
            if (!drives)
            {
                stuckWindowOpen = false;
                return;
            }

            if (!stuckWindowOpen)
            {
                stuckWindowOpen = true;
                stuckWindowStart = now;
                stuckWindowPosition = position;
                return;
            }

            if (now - stuckWindowStart < profile.stuckTime)
                return;

            bool stuck = (position - stuckWindowPosition).sqrMagnitude < profile.stuckMinDistance * profile.stuckMinDistance;
            stuckWindowStart = now;
            stuckWindowPosition = position;
            if (stuck)
                BeginReverse(now);
        }

        // The hull only drives forward, so "backing off" means turning away and driving off in the opposite direction.
        void BeginReverse(float now)
        {
            float spread = Random.Range(-profile.reverseAngleSpread, profile.reverseAngleSpread);
            reverseDirection = Quaternion.Euler(0f, 0f, 180f + spread) * tank.Forward;
            reverseUntil = now + profile.reverseDuration;

            // Rebuild the path from wherever the bot ends up.
            hasPath = false;
            nextRepathAt = reverseUntil;
        }

        void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying || !isActiveAndEnabled)
                return;

            Gizmos.color = Color.cyan;
            Vector3 previous = transform.position;
            for (int i = pathIndex; i < path.Count; i++)
            {
                Gizmos.DrawLine(previous, path[i]);
                previous = path[i];
            }

            if (target != null)
            {
                Gizmos.color = hasLineOfSight ? Color.red : Color.gray;
                Gizmos.DrawLine(transform.position, targetTransform.position);
            }
        }
    }
}
