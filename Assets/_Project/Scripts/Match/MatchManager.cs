using System.Collections.Generic;
using TankBrawl.Core;
using TankBrawl.Input;
using TankBrawl.Tank;
using TankBrawl.Weapons;
using UnityEngine;

namespace TankBrawl.Match
{
    /// <summary>
    /// Runs a match in a scene: collects the tanks, places them on the <see cref="SpawnPoint"/>s, relays deaths to the
    /// <see cref="IMatchMode"/> chosen by <see cref="MatchModeConfig.kind"/>, respawns tanks and keeps the camera on the human
    /// (or on a living ally while the human is dead). One per scene; scenes without it behave as before.
    /// </summary>
    public class MatchManager : MonoBehaviour
    {
        struct PendingRespawn
        {
            public MatchParticipant Participant;
            public float Time;
        }

        [SerializeField] MatchModeConfig config;

        readonly List<MatchParticipant> participants = new List<MatchParticipant>();
        readonly List<SpawnPoint> spawnPoints = new List<SpawnPoint>();
        readonly List<Team> teams = new List<Team>();
        readonly List<PendingRespawn> pending = new List<PendingRespawn>();

        IMatchMode mode;
        MatchParticipant human;
        CameraFollow cameraFollow;

        public static MatchManager Instance { get; private set; }

        public MatchModeConfig Config => config;
        public IMatchMode Mode => mode;
        public IReadOnlyList<MatchParticipant> Participants => participants;

        /// <summary>The teams that have tanks in this match, in order of appearance.</summary>
        public IReadOnlyList<Team> Teams => teams;

        public MatchParticipant Human => human;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError($"{name}: a second MatchManager, it is disabled.", this);
                enabled = false;
                return;
            }

            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Start()
        {
            if (!enabled)
                return;

            if (config == null)
            {
                Debug.LogError($"{name}: MatchModeConfig is not assigned.", this);
                enabled = false;
                return;
            }

            mode = CreateMode(config);
            if (mode == null)
            {
                Debug.LogError($"{name}: mode {config.kind} is not implemented yet.", this);
                enabled = false;
                return;
            }

            CollectParticipants();
            spawnPoints.AddRange(FindObjectsByType<SpawnPoint>(FindObjectsInactive.Exclude));
            cameraFollow = FindAnyObjectByType<CameraFollow>();
            MatchHud.Create(this);

            foreach (MatchParticipant p in participants)
                p.Health.Died += OnTankDied;

            mode.Begin(this);
        }

        void OnDisable()
        {
            foreach (MatchParticipant p in participants)
            {
                if (p.Health != null)
                    p.Health.Died -= OnTankDied;
            }
        }

        void OnEnable()
        {
            // Re-enabled after a disable: the subscriptions were dropped in OnDisable.
            if (mode == null)
                return;

            foreach (MatchParticipant p in participants)
                p.Health.Died += OnTankDied;
        }

        void Update()
        {
            if (mode == null)
                return;

            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (Time.time < pending[i].Time)
                    continue;

                MatchParticipant p = pending[i].Participant;
                pending.RemoveAt(i);
                Respawn(p);
            }

            mode.Tick();
        }

        // The place for the modes that are added later: one switch case each.
        static IMatchMode CreateMode(MatchModeConfig modeConfig)
        {
            switch (modeConfig.kind)
            {
                case MatchModeKind.TeamElimination:
                    return new EliminationMode(modeConfig);
                default:
                    return null;
            }
        }

        void CollectParticipants()
        {
            participants.Clear();
            teams.Clear();

            TeamMember[] members = FindObjectsByType<TeamMember>(FindObjectsInactive.Include);
            foreach (TeamMember member in members)
            {
                if (!member.TryGetComponent(out TankHealth health))
                    continue;

                bool isHuman = member.TryGetComponent(out PlayerTankInput _);
                var participant = new MatchParticipant(member, health, isHuman);
                participants.Add(participant);
                if (isHuman)
                    human = participant;
                if (!teams.Contains(member.Team))
                    teams.Add(member.Team);
            }

            // Spawn numbers: the human first inside its team, the rest in the order found.
            foreach (Team team in teams)
            {
                int index = 0;
                foreach (MatchParticipant p in participants)
                {
                    if (p.Team == team && p.IsHuman)
                        p.SpawnIndex = index++;
                }

                foreach (MatchParticipant p in participants)
                {
                    if (p.Team == team && !p.IsHuman)
                        p.SpawnIndex = index++;
                }
            }

            if (human != null)
                TeamMember.LocalTeam = human.Team;
        }

        // ---- Queries for the modes ----

        public int CountAlive(Team team)
        {
            int count = 0;
            for (int i = 0; i < participants.Count; i++)
            {
                if (participants[i].Team == team && participants[i].IsAlive)
                    count++;
            }

            return count;
        }

        public int CountAll(Team team)
        {
            int count = 0;
            for (int i = 0; i < participants.Count; i++)
            {
                if (participants[i].Team == team)
                    count++;
            }

            return count;
        }

        // ---- Spawning ----

        /// <summary>Puts every tank, living or dead, on its spawn point with full health, and clears the projectiles in flight.</summary>
        public void RespawnAll()
        {
            pending.Clear();
            Projectile.ClearAll();
            foreach (MatchParticipant p in participants)
                Respawn(p);
        }

        /// <summary>Brings the tank back after <paramref name="delay"/> seconds (at once if it is not positive).</summary>
        public void ScheduleRespawn(MatchParticipant participant, float delay)
        {
            if (delay <= 0f)
            {
                Respawn(participant);
                return;
            }

            pending.Add(new PendingRespawn { Participant = participant, Time = Time.time + delay });
        }

        /// <summary>
        /// Places the tank on its spawn point (or where it stood at the start) and activates it with full health and the
        /// spawn protection from the config. A tank that is alive is reset the same way, which also clears its power-ups.
        /// </summary>
        public void Respawn(MatchParticipant participant)
        {
            GameObject tankObject = participant.Object;
            if (tankObject.activeSelf)
                tankObject.SetActive(false);

            Vector3 position = participant.StartPosition;
            float angle = participant.StartAngle;
            if (TryGetSpawnPoint(participant, out SpawnPoint point))
            {
                position = point.transform.position;
                angle = point.transform.eulerAngles.z;
            }

            tankObject.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, angle));
            tankObject.SetActive(true);

            // The body reads the transform on activation; this also guarantees it holds no velocity from the previous life.
            if (participant.Body != null)
            {
                participant.Body.position = position;
                participant.Body.rotation = angle;
                participant.Body.linearVelocity = Vector2.zero;
            }

            participant.Health.GrantSpawnProtection(config.spawnProtection);
            RefreshCamera();
        }

        // The tank's number among its team's tanks picks the point among the team's points (wrapping around).
        bool TryGetSpawnPoint(MatchParticipant participant, out SpawnPoint result)
        {
            result = null;
            int total = 0;
            for (int i = 0; i < spawnPoints.Count; i++)
            {
                if (spawnPoints[i].Team == participant.Team)
                    total++;
            }

            if (total == 0)
                return false;

            int wanted = participant.SpawnIndex % total;
            for (int i = 0; i < spawnPoints.Count; i++)
            {
                if (spawnPoints[i].Team != participant.Team)
                    continue;

                if (wanted-- == 0)
                {
                    result = spawnPoints[i];
                    return true;
                }
            }

            return false;
        }

        // ---- Deaths and camera ----

        void OnTankDied(TankHealth victim, GameObject killerObject)
        {
            MatchParticipant dead = null;
            MatchParticipant killer = null;
            for (int i = 0; i < participants.Count; i++)
            {
                MatchParticipant p = participants[i];
                if (p.Health == victim)
                    dead = p;
                else if (killerObject != null && p.Object == killerObject)
                    killer = p;
            }

            if (dead == null)
                return;

            dead.Deaths++;
            if (killer != null && killer.Team != dead.Team)
                killer.Kills++;

            mode.OnDied(dead, killer);
            RefreshCamera();
        }

        /// <summary>The camera follows the human; while the human is dead, a living ally; with no ally it stays where it is.</summary>
        void RefreshCamera()
        {
            if (cameraFollow == null || human == null)
                return;

            if (human.IsAlive)
            {
                cameraFollow.Target = human.Object.transform;
                return;
            }

            for (int i = 0; i < participants.Count; i++)
            {
                MatchParticipant p = participants[i];
                if (p != human && p.Team == human.Team && p.IsAlive)
                {
                    cameraFollow.Target = p.Object.transform;
                    return;
                }
            }
        }
    }
}
