using System.Collections.Generic;
using TankBrawl.Core;
using TankBrawl.Map;
using TankBrawl.PowerUps;
using TankBrawl.Tank;
using TankBrawl.Weapons;
using UnityEngine;

namespace TankBrawl.AI
{
    public enum BotState
    {
        /// <summary>No enemy to go for: wanders between random points of the map.</summary>
        Patrol,

        /// <summary>Goes along a path to where the enemy is (or was last seen), or to a power-up.</summary>
        Seek,

        /// <summary>Has a clear shot within range: circles the target at its preferred distance, turns to aim when the gun is about to be ready.</summary>
        Attack,

        /// <summary>A rush on nitro into the target at close range.</summary>
        Ram,

        /// <summary>Low health: runs to a power-up or to cover out of the enemy's sight.</summary>
        Retreat,

        /// <summary>A holder in a scene with a flag zone, without a clear shot: goes into the zone and wanders inside it.</summary>
        Hold,

        /// <summary>A short side step away from an incoming shot. Interrupts any other state; <see cref="BotTankInput.State"/> reports it while it lasts.</summary>
        Dodge
    }

    /// <summary>
    /// Drives a tank like a player would: only through <see cref="ITankInput"/>, so the tank has the same limits
    /// (the gun looks where the hull looks, the weapon has its cooldown, bushes hide enemies from it).
    /// A small FSM (Patrol / Seek / Attack / Ram / Retreat) decides every <see cref="BotProfile.decisionInterval"/> seconds;
    /// steering and shooting run every frame; Dodge interrupts everything. Parts: this file (core, attack, ram, patrol,
    /// paths), BotTankInput.Dodge.cs, BotTankInput.Retreat.cs (retreat and power-ups).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(TankController), typeof(TankWeapon))]
    [RequireComponent(typeof(TeamMember))]
    public partial class BotTankInput : MonoBehaviour, ITankInput
    {
        const int MaxWallHits = 4;

        // Stick deflection for turning in place, as a fraction of TankConfig.driveThreshold (below it the hull only turns).
        const float TurnOnlyScale = 0.5f;

        // Bots share one list of all tanks instead of each scanning the scene.
        const float CandidateRefreshInterval = 0.5f;

        // Decisions of different bots should not all land on the same frame: the interval is randomised by this share.
        const float DecisionJitter = 0.15f;

        // Closer than this to the last known place of an enemy that is out of sight = arrived, the trail ends.
        const float ArriveDistance = 1f;

        // While circling, the bot looks this far ahead for a wall and turns the other way if there is one.
        const float StrafeProbeDistance = 1.2f;

        // A ram is dropped if the target gets farther than this multiple of the ram distance.
        const float RamAbortDistanceFactor = 1.5f;

        // How the circling is steered: with the pull to the preferred distance, pure circling (a wall is in the way
        // of the pull), or straight at the target (everything else is blocked).
        const int StrafeNormal = 0;
        const int StrafeCircleOnly = 1;
        const int StrafeCloseIn = 2;

        static readonly List<TeamMember> Candidates = new List<TeamMember>();
        static float candidatesRefreshAt;

        // Slow drift of the aim (see BotProfile.aimWobbleDegrees): Perlin noise speed, 1/s.
        const float AimWobbleSpeed = 0.7f;

        [SerializeField] BotProfile profile;

        // The profile above scaled by the difficulty level of the match (a private copy, destroyed with the bot).
        BotProfile scaledProfile;
        float turnRateScale = 1f;
        float wobbleSeed;

        Rigidbody2D body;
        TankController tank;
        TankWeapon weapon;
        TeamMember member;
        BotNavGrid nav;
        TankNitro nitro;
        TankHealth health;
        TankPowerUps powerUps;
        float bodyRadius;
        ContactFilter2D wallFilter;
        readonly RaycastHit2D[] wallHits = new RaycastHit2D[MaxWallHits];
        readonly List<Vector2> path = new List<Vector2>();
        readonly List<Vector2> scratchPath = new List<Vector2>();
        BushTilemap bushes;

        Vector2 move;
        bool fire;
        bool nitroHeld;
        float turnOnlyMagnitude;
        float driveThresholdSqr;

        TeamMember target;
        float targetSetAt;
        Transform targetTransform;
        Rigidbody2D targetBody;
        TankConcealment targetConcealment;
        TankHealth targetHealth;
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
        bool splashClear;

        // Searching the bushes where the enemy vanished
        bool searching;
        Vector2 searchCenter;
        Vector2 searchPoint;
        float searchUntil;
        int searchesLeft;
        bool searchShotDone;
        float aimOffsetDegrees;

        BotState baseState;
        float nextDecisionAt;

        // Attack
        float strafeSign = 1f;
        int strafeRadialMode = StrafeNormal;
        float nextStrafeSwitchAt;

        // Ram
        float ramStartedAt;
        float nextRamAt;

        // Seek is skipped until this time after the way to the enemy turned out to be blocked
        float seekBlockedUntil;

        // Patrol
        bool hasPatrolPoint;
        Vector2 patrolPoint;
        float patrolPickedAt;

        bool hasPath;
        bool pathFailed;
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
        public bool FirePressed => false;
        public bool NitroHeld => nitroHeld;
        public bool CycleAmmoPressed => false;

        /// <summary>What the bot is doing now; <see cref="BotState.Dodge"/> while a dodge interrupts the FSM.</summary>
        public BotState State => dodging ? BotState.Dodge : baseState;

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
            TryGetComponent(out nitro);
            TryGetComponent(out health);
            TryGetComponent(out powerUps);

            bodyRadius = TryGetComponent(out CircleCollider2D circle) ? circle.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y) : 0.45f;

            wallFilter = new ContactFilter2D();
            wallFilter.useTriggers = false;
            wallFilter.useLayerMask = true;
            wallFilter.layerMask = LayerMask.GetMask("Wall");

            if (profile == null)
                Debug.LogError($"{name}: BotProfile is not assigned, the bot will stand still.", this);
            else
                ApplyDifficulty();
        }

        // The difficulty of the match scales the character's numbers; the asset itself stays untouched.
        void ApplyDifficulty()
        {
            DifficultyConfig.Level level = DifficultyConfig.Current;
            wobbleSeed = Random.value * 100f;
            if (level == null)
                return;

            scaledProfile = level.CreateProfile(profile);
            profile = scaledProfile;
            turnRateScale = level.turnRateScale;
        }

        void OnDestroy()
        {
            if (scaledProfile != null)
                Destroy(scaledProfile);
        }

        void OnEnable()
        {
            nav = FindAnyObjectByType<BotNavGrid>();
            bushes = FindAnyObjectByType<BushTilemap>();
            if (nav == null)
                Debug.LogError($"{name}: no BotNavGrid in the scene, the bot will drive straight at its target.", this);

            if (tank.Config != null)
            {
                turnOnlyMagnitude = tank.Config.driveThreshold * TurnOnlyScale;
                driveThresholdSqr = tank.Config.driveThreshold * tank.Config.driveThreshold;
            }

            tank.TurnRateScale = turnRateScale;
            weapon.Fired += OnFired;
            ResetState();
        }

        void OnDisable()
        {
            weapon.Fired -= OnFired;
            ReleasePickup();
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

            // A shot flying at the hull beats everything else.
            if (UpdateDodge(now, position))
            {
                UpdateStuck(now, position, true);
                return;
            }

            if (now < reverseUntil)
            {
                move = reverseDirection;
                fire = false;
                nitroHeld = false;
                stuckWindowOpen = false;
                return;
            }

            if (now >= nextDecisionAt)
            {
                nextDecisionAt = now + profile.decisionInterval * Random.Range(1f - DecisionJitter, 1f + DecisionJitter);
                Decide(now, position);
            }

            bool drives = Act(now, position);
            UpdateStuck(now, position, drives);
        }

        void OnFired()
        {
            RollAim();
            if (searching)
                searchShotDone = true;
        }

        void ResetState()
        {
            Stop();
            baseState = BotState.Patrol;
            nextDecisionAt = 0f;
            target = null;
            targetTransform = null;
            targetBody = null;
            targetConcealment = null;
            targetHealth = null;
            retargetAt = 0f;
            nextPerceptionAt = 0f;
            targetVisible = hasLineOfSight = directClear = splashClear = hasLastKnown = false;
            hasPath = false;
            pathFailed = false;
            nextRepathAt = 0f;
            stuckWindowOpen = false;
            reverseUntil = 0f;
            hasPatrolPoint = false;
            seekBlockedUntil = 0f;
            searching = false;
            splashClear = false;
            nextRamAt = 0f;
            strafeSign = Random.value < 0.5f ? -1f : 1f;
            strafeRadialMode = StrafeNormal;
            nextStrafeSwitchAt = 0f;
            ResetDodge();
            ResetRetreat();
            ResetHold();
            RollAim();
        }

        void Stop()
        {
            move = Vector2.zero;
            fire = false;
            nitroHeld = false;
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

        // The nearest enemy that the bot can see (an enemy standing in a bush is not a candidate: the bot would otherwise know
        // where it is). With several enemies the bot sticks to its target: it switches only after targetHoldTime and only to
        // an enemy clearly closer (retargetDistanceRatio), so it does not flip between two enemies at similar distances.
        // A target that went into a bush is dropped for any visible enemy; if nobody is visible, the bot keeps it and
        // goes to where it was last seen.
        void PickTarget(Vector2 position)
        {
            RefreshCandidates();

            TeamMember best = null;
            float bestCost = float.MaxValue;
            for (int i = 0; i < Candidates.Count; i++)
            {
                TeamMember candidate = Candidates[i];
                if (candidate == null || candidate.Team == member.Team || !candidate.gameObject.activeInHierarchy)
                    continue;

                if (candidate.TryGetComponent(out TankConcealment hiding) && hiding.IsHiddenFrom(member.Team))
                    continue;

                if (!InView(position, candidate.transform.position))
                    continue;

                float cost = TargetCost(position, candidate);
                if (cost < bestCost)
                {
                    best = candidate;
                    bestCost = cost;
                }
            }

            bool currentAlive = target != null && target.gameObject.activeInHierarchy;
            if (best == null)
            {
                if (!currentAlive)
                    SetTarget(null);
                return;
            }

            if (best == target)
                return;

            if (currentAlive && targetVisible)
            {
                bool held = Time.time - targetSetAt >= profile.targetHoldTime;
                if (!held || bestCost >= TargetCost(position, target) * profile.retargetDistanceRatio)
                    return;
            }

            SetTarget(best);
        }

        // Lower is better: the length of the way to the enemy (an enemy behind a long wall is farther than it looks),
        // shrunk for a wounded enemy and for one that is aiming at the bot right now.
        float TargetCost(Vector2 position, TeamMember enemy)
        {
            Vector2 at = enemy.transform.position;
            float cost = Vector2.Distance(position, at);
            if (nav != null && !nav.IsSegmentWalkable(position, at) && nav.TryFindPath(position, at, scratchPath))
                cost = PathLength(position, scratchPath);

            if (enemy.TryGetComponent(out TankHealth enemyHealth) && enemyHealth.Max > 0f)
                cost *= 1f - profile.woundedTargetPreference * (1f - Mathf.Clamp01(enemyHealth.Current / enemyHealth.Max));

            Vector2 toMe = position - at;
            if (toMe.sqrMagnitude > Mathf.Epsilon && Vector2.Angle(enemy.transform.up, toMe) <= profile.threatAngle)
                cost *= 1f - profile.threatTargetPreference;

            return cost;
        }

        static float PathLength(Vector2 from, List<Vector2> points)
        {
            float length = 0f;
            Vector2 previous = from;
            for (int i = 0; i < points.Count; i++)
            {
                length += Vector2.Distance(previous, points[i]);
                previous = points[i];
            }

            return length;
        }

        void SetTarget(TeamMember newTarget)
        {
            searching = false;
            targetSetAt = Time.time;
            target = newTarget;
            targetTransform = newTarget != null ? newTarget.transform : null;
            targetBody = null;
            targetConcealment = null;
            targetHealth = null;
            if (newTarget != null)
            {
                newTarget.TryGetComponent(out targetBody);
                newTarget.TryGetComponent(out targetConcealment);
                newTarget.TryGetComponent(out targetHealth);
            }

            targetVisible = hasLineOfSight = directClear = splashClear = hasLastKnown = false;
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
            targetVisible = (targetConcealment == null || !targetConcealment.IsHiddenFrom(member.Team)) && InView(position, targetPosition);

            if (targetVisible)
            {
                lastKnownPosition = targetPosition;
                hasLastKnown = true;
            }
            else
            {
                hasLineOfSight = directClear = splashClear = false;
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
            splashClear = !seen && CanSplash(position);
        }

        // With explosive shells an enemy behind cover is still in reach when a shell bursting on that cover would catch it.
        // The blast passes no walls, so this works at corners and edges of cover, not through a thick wall.
        bool CanSplash(Vector2 position)
        {
            WeaponConfig shot = weapon.Config;
            if (shot == null || !shot.IsExplosive || profile.splashReach <= 0f || targetDistance > profile.fireRange || targetDistance < 0.01f)
                return false;

            Vector2 direction = (targetPosition - position) / targetDistance;
            int count = Physics2D.CircleCast(position, shot.wallHitRadius, direction, wallFilter, wallHits, targetDistance);
            int nearest = -1;
            for (int i = 0; i < count; i++)
            {
                if (nearest < 0 || wallHits[i].distance < wallHits[nearest].distance)
                    nearest = i;
            }

            if (nearest < 0)
                return false;

            Vector2 burst = wallHits[nearest].centroid;
            Vector2 toTarget = targetPosition - burst;
            if (toTarget.magnitude > shot.explosionRadius * profile.splashReach)
                return false;

            return Explosion.Reaches(burst, targetPosition, toTarget.normalized, bodyRadius * 0.8f);
        }

        // Honest information: the bot knows only what a player would see on a screen centred on its tank. Walls do not hide
        // (the view is from above), bushes do (checked separately).
        bool InView(Vector2 position, Vector2 point)
        {
            return Mathf.Abs(point.x - position.x) <= profile.viewHalfWidth && Mathf.Abs(point.y - position.y) <= profile.viewHalfHeight;
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

        // ---- Decisions (every decisionInterval) ----

        void Decide(float now, Vector2 position)
        {
            BotState previous = baseState;
            baseState = ChooseState(now, position);
            if (baseState == previous)
            {
                if (baseState == BotState.Attack)
                    UpdateStrafe(now, position);
                return;
            }

            // The goal changes with the state, so the old path is useless.
            InvalidatePath();
            if (baseState == BotState.Attack)
            {
                ScheduleStrafeSwitch(now);
                UpdateStrafe(now, position);
            }
        }

        BotState ChooseState(float now, Vector2 position)
        {
            // Retreat and ram are sticky: once begun they run until their own end conditions.
            if (baseState == BotState.Retreat)
            {
                if (ContinueRetreat(now, position))
                    return BotState.Retreat;
                EndRetreat(now);
            }
            else if (ShouldRetreat(now) && BeginRetreat(now, position))
            {
                return BotState.Retreat;
            }

            if (baseState == BotState.Ram)
            {
                if (ContinueRam(now))
                    return BotState.Ram;
                nextRamAt = now + profile.ramCooldown;
            }

            bool canShoot = target != null && (hasLineOfSight || splashClear) && targetDistance <= profile.fireRange;

            if (UpdatePowerUpGoal(now, position, canShoot))
                return BotState.Seek;

            if (canShoot)
            {
                if (WantsRam(now, position))
                {
                    ramStartedAt = now;
                    return BotState.Ram;
                }

                return BotState.Attack;
            }

            // A holder in a scene with a flag zone stays at the point instead of chasing.
            if (WantsToHold)
                return BotState.Hold;

            if (searching && ContinueSearch(now, position))
                return BotState.Seek;

            if (hasLastKnown && now >= seekBlockedUntil)
            {
                // The enemy is out of sight and the bot has reached where it was last seen: the trail ends, but first
                // the bot checks the bushes nearby, as a player would.
                if (!targetVisible && (lastKnownPosition - position).sqrMagnitude < ArriveDistance * ArriveDistance)
                {
                    hasLastKnown = false;
                    if (BeginSearch(now, position))
                        return BotState.Seek;
                }
                else
                {
                    return BotState.Seek;
                }
            }

            return BotState.Patrol;
        }

        // ---- Acting (every frame) ----

        // Sets move/fire/nitro for this frame. Returns true when the bot wants to drive (used to detect being stuck).
        bool Act(float now, Vector2 position)
        {
            move = Vector2.zero;
            fire = false;
            nitroHeld = false;

            switch (baseState)
            {
                case BotState.Attack:
                    return target != null && ActAttack(now, position);
                case BotState.Ram:
                    return target != null && ActRam(now, position);
                case BotState.Retreat:
                    return ActRetreat(now, position);
                case BotState.Hold:
                    return ActHold(now, position);
                case BotState.Seek:
                    return ActSeek(now, position);
                default:
                    return ActPatrol(now, position);
            }
        }

        bool ActAttack(float now, Vector2 position)
        {
            Vector2 aim = AimDirection(position);

            // The laser burns while the beam points at the target, so a laser tank keeps aiming and closing in.
            bool laser = weapon.Suppressed;
            bool aiming = laser || weapon.CooldownLeft <= profile.aimLeadTime;

            if (aiming)
            {
                if (targetDistance > profile.preferredDistance + profile.distanceBand)
                    move = directClear ? aim : WithFallback(PathHeading(now, position, targetPosition), aim);
                else
                    move = laser && targetDistance > profile.preferredDistance ? aim : aim * turnOnlyMagnitude;
            }
            else
            {
                move = PullToPoint(position, CircleHeading((targetPosition - position) / Mathf.Max(targetDistance, Mathf.Epsilon)));
            }

            fire = CanFire(now, position, aim);
            return move.sqrMagnitude >= driveThresholdSqr;
        }

        // The gun is fixed along the hull, so circling means driving at an angle to the target and turning to it only to shoot.
        // The radial part pulls the bot to the preferred distance (blended over the distance band).
        Vector2 CircleHeading(Vector2 toTarget)
        {
            Vector2 tangent = new Vector2(-toTarget.y, toTarget.x) * strafeSign;
            float bias = Mathf.Clamp((targetDistance - profile.preferredDistance) / profile.distanceBand, -1f, 1f);
            if (strafeRadialMode == StrafeCircleOnly)
                bias = 0f;
            else if (strafeRadialMode == StrafeCloseIn)
                bias = 1f;
            return (tangent + toTarget * bias).normalized;
        }

        // Keeps the circling direction for a while, but a wall ahead changes the plan: first the other side, then circling
        // without the pull towards the preferred distance, and when everything is blocked, closing in.
        void UpdateStrafe(float now, Vector2 position)
        {
            if (target == null)
                return;

            if (now >= nextStrafeSwitchAt)
            {
                strafeSign = -strafeSign;
                ScheduleStrafeSwitch(now);
            }

            if (nav == null)
                return;

            // The preferred plan first (circle with the pull to the preferred distance).
            Vector2 toTarget = (targetPosition - position) / Mathf.Max(targetDistance, Mathf.Epsilon);
            strafeRadialMode = StrafeNormal;
            if (IsStrafeFree(position, toTarget))
                return;

            // Then: the other side; the same side without the pull; the other side without the pull.
            float sign = strafeSign;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                strafeSign = attempt == 1 ? sign : -sign;
                strafeRadialMode = attempt == 0 ? StrafeNormal : StrafeCircleOnly;
                if (IsStrafeFree(position, toTarget))
                {
                    ScheduleStrafeSwitch(now);
                    return;
                }
            }

            // Nothing is free: the only way out is towards the target.
            strafeSign = sign;
            strafeRadialMode = StrafeCloseIn;
        }

        bool IsStrafeFree(Vector2 position, Vector2 toTarget)
        {
            Vector2 heading = CircleHeading(toTarget);
            return nav.IsSegmentWalkable(position, position + heading * StrafeProbeDistance);
        }

        void ScheduleStrafeSwitch(float now)
        {
            nextStrafeSwitchAt = now + Random.Range(profile.strafeSwitchMin, profile.strafeSwitchMax);
        }

        // A freshly respawned target is immune: shooting at it only wastes the shell.
        bool TargetProtected => targetHealth != null && targetHealth.HasSpawnProtection;

        // Fire when the hull points at the aim point, the reaction delay has passed and steel is not right in front of the muzzle.
        bool CanFire(float now, Vector2 position, Vector2 aim)
        {
            return now >= reactionReadyAt
                   && !TargetProtected
                   && Vector2.Angle(tank.Forward, aim) <= profile.fireAlignTolerance
                   && !SteelTooClose(position);
        }

        // Shoots while doing something else (driving to a power-up, running away) when the hull happens to point at the target.
        void TryOpportunisticFire(float now, Vector2 position)
        {
            if (target == null || !targetVisible || !hasLineOfSight || targetDistance > profile.fireRange)
                return;

            fire = CanFire(now, position, AimDirection(position));
        }

        // ---- Ram ----

        // Starts a ram: nitro and a clear straight way, the target close, the hull already pointing at it, and the dice (aggression).
        bool WantsRam(float now, Vector2 position)
        {
            if (nitro == null || nitro.IsLocked || profile.nitroRamDistance <= 0f || now < nextRamAt || TargetProtected)
                return false;
            if (!directClear || targetDistance > profile.nitroRamDistance || nitro.Normalized < profile.nitroStartStock)
                return false;
            if (Vector2.Angle(tank.Forward, targetPosition - position) > profile.nitroAlignTolerance)
                return false;

            return Random.value < profile.aggression;
        }

        bool ContinueRam(float now)
        {
            return nitro != null && !nitro.IsLocked && nitro.Normalized > profile.nitroStopStock
                   && target != null && targetVisible && directClear
                   && targetDistance <= profile.nitroRamDistance * RamAbortDistanceFactor
                   && now - ramStartedAt < profile.ramMaxDuration;
        }

        // Straight at the target. Nitro goes on only while the hull points at it, so the bot never revs on the spot.
        bool ActRam(float now, Vector2 position)
        {
            Vector2 heading = (targetPosition - position).normalized;
            move = heading;
            nitroHeld = nitro != null && Vector2.Angle(tank.Forward, heading) <= profile.nitroAlignTolerance;
            fire = CanFire(now, position, AimDirection(position));
            return true;
        }

        // ---- Seek and Patrol ----

        bool ActSeek(float now, Vector2 position)
        {
            // A pick-up taken by someone else a moment ago: wait for the next decision.
            if (pickupGoal == null && !hasLastKnown && !searching)
                return false;

            if (pickupGoal == null && searching && ActSearch(position))
                return false;

            Vector2 goal = pickupGoal != null ? (Vector2)pickupGoal.transform.position : (searching ? searchPoint : lastKnownPosition);
            move = PathHeading(now, position, goal);
            TryOpportunisticFire(now, position);

            // No way to the goal (the enemy sits in a pocket the map does not connect): do not stand and wait, patrol for a while.
            if (move == Vector2.zero && pathFailed)
            {
                if (pickupGoal != null)
                {
                    MarkUnreachable(now, pickupGoal);
                    ReleasePickup();
                }
                else if (searching)
                {
                    searching = false;
                }
                else
                {
                    seekBlockedUntil = now + UnreachableForgetTime;
                }
            }

            return move != Vector2.zero;
        }

        // ---- Searching bushes ----

        bool BeginSearch(float now, Vector2 position)
        {
            if (profile.searchBushes <= 0 || bushes == null)
                return false;

            searchCenter = lastKnownPosition;
            searchUntil = now + profile.searchTime;
            searchesLeft = profile.searchBushes;
            searching = PickSearchPoint(position);
            return searching;
        }

        // Keeps searching until the time is up, an enemy shows up or the bushes run out. True while the search goes on.
        bool ContinueSearch(float now, Vector2 position)
        {
            if (targetVisible || now >= searchUntil)
            {
                searching = false;
                return false;
            }

            bool done = searchShotDone || (searchPoint - position).sqrMagnitude < ArriveDistance * ArriveDistance;
            if (done && (--searchesLeft <= 0 || !PickSearchPoint(position)))
            {
                searching = false;
                return false;
            }

            return true;
        }

        bool PickSearchPoint(Vector2 position)
        {
            searchShotDone = false;
            InvalidatePath();
            return bushes.TryFindNear(searchCenter, profile.searchRadius, position, ArriveDistance, out searchPoint);
        }

        // Close to the bush with a clear line: turn to it and shoot into it instead of driving in. True while doing that.
        bool ActSearch(Vector2 position)
        {
            Vector2 toBush = searchPoint - position;
            float distance = toBush.magnitude;
            if (distance > profile.searchShotDistance || distance < 0.01f || !IsLineClear(position, searchPoint))
                return false;

            Vector2 direction = toBush / distance;
            move = direction * turnOnlyMagnitude;
            fire = Vector2.Angle(tank.Forward, direction) <= profile.fireAlignTolerance * 2f && !SteelTooClose(position);
            return true;
        }

        bool ActPatrol(float now, Vector2 position)
        {
            if (nav == null)
                return false;

            if (!hasPatrolPoint || now - patrolPickedAt > profile.patrolMaxTime)
            {
                hasPatrolPoint = nav.TryGetRandomPoint(position, profile.patrolMinDistance, float.MaxValue, out patrolPoint);
                patrolPickedAt = now;
                InvalidatePath();
                if (!hasPatrolPoint)
                    return false;
            }

            move = PathHeading(now, position, patrolPoint);
            if (move == Vector2.zero && (hasPath || pathFailed))
                hasPatrolPoint = false; // arrived, or the point cannot be reached: pick another next frame

            return move != Vector2.zero;
        }

        // Where the hull should point to hit the target: its position, partly led by its velocity, then the random spread.
        Vector2 AimDirection(Vector2 position)
        {
            Vector2 point = targetPosition;
            float speed = weapon.ProjectileSpeed;
            if (profile.leadFactor > 0f && targetBody != null && speed > 0f)
                point += targetBody.linearVelocity * (targetDistance / speed * profile.leadFactor);

            Vector2 direction = point - position;
            direction = direction.sqrMagnitude > Mathf.Epsilon ? direction.normalized : tank.Forward;

            float error = aimOffsetDegrees;
            if (profile.aimWobbleDegrees > 0f)
                error += (Mathf.PerlinNoise(Time.time * AimWobbleSpeed, wobbleSeed) * 2f - 1f) * profile.aimWobbleDegrees;
            return Quaternion.Euler(0f, 0f, error) * direction;
        }

        // A shot that hits steel right in front of the muzzle bounces back at the shooter.
        bool SteelTooClose(Vector2 position)
        {
            // A shell that does not bounce (explosive kinds) cannot come back at the shooter.
            if (weapon.Config.maxBounces == 0)
                return false;

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

        static Vector2 WithFallback(Vector2 preferred, Vector2 fallback)
        {
            return preferred != Vector2.zero ? preferred : fallback;
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

            BuildPath(now, position, goal);
        }

        bool BuildPath(float now, Vector2 position, Vector2 goal)
        {
            nextRepathAt = now + profile.repathInterval;
            hasPath = nav.TryFindPath(position, goal, path);
            pathFailed = !hasPath;
            pathIndex = 0;
            pathGoal = goal;
            pathVersion = nav.Version;
            return hasPath;
        }

        void InvalidatePath()
        {
            hasPath = false;
            pathFailed = false;
            nextRepathAt = 0f;
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

            if (baseState == BotState.Retreat)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(retreatGoal, 0.4f);
            }
            else if (baseState == BotState.Patrol && hasPatrolPoint)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(patrolPoint, 0.3f);
            }
            else if (baseState == BotState.Hold && hasHoldSpot)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(holdSpot, 0.3f);
            }
        }
    }
}
