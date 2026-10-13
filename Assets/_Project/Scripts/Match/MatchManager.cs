using System;
using System.Collections.Generic;
using TankBrawl.AI;
using TankBrawl.Audio;
using TankBrawl.Core;
using TankBrawl.Input;
using TankBrawl.Map;
using TankBrawl.PowerUps;
using TankBrawl.Progress;
using TankBrawl.Tank;
using TankBrawl.UI;
using TankBrawl.Weapons;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TankBrawl.Match
{
    /// <summary>
    /// Runs a match in a scene: collects the tanks, sets the sides and colours for the chosen mode, places the tanks on
    /// the <see cref="SpawnPoint"/>s, relays deaths to the <see cref="IMatchMode"/> picked by <see cref="MatchModeConfig.kind"/>,
    /// respawns tanks, pays gears, pauses, finishes the match and keeps the camera on the human (or on a living ally while
    /// the human is dead). One per scene; scenes without it behave as before.
    /// The mode comes from <see cref="MatchSession"/> (the menu's choice) or, when the scene is opened directly, from <see cref="config"/>.
    /// </summary>
    public class MatchManager : MonoBehaviour
    {
        enum State
        {
            Running,
            Finishing,
            Finished
        }

        struct PendingRespawn
        {
            public MatchParticipant Participant;
            public float Time;
        }

        // The slow-motion pause between the deciding moment and the result screen, in real seconds.
        const float FinishingRealSeconds = 1.4f;
        const float FinishingTimeScale = 0.25f;

        // A free respawn point is skipped when a living tank stands this close.
        const float OccupiedRadius = 1.3f;

        [Tooltip("The mode when the scene is opened directly, not through the menu.")]
        [SerializeField] MatchModeConfig config;

        [Tooltip("Skin per team, in the order of Team: Player (blue), Enemy (red), Third (green), Fourth (yellow).")]
        [SerializeField] TankSkin[] teamSkins;

        [Tooltip("The flag zone object: switched on only in the point-holding mode.")]
        [SerializeField] GameObject capturePoint;

        [Tooltip("The base pads under the team spawn points: hidden in the free-for-all (nobody has a base there).")]
        [SerializeField] GameObject basePads;

        readonly List<MatchParticipant> participants = new List<MatchParticipant>();
        readonly List<SpawnPoint> spawnPoints = new List<SpawnPoint>();
        readonly List<Team> teams = new List<Team>();
        readonly List<PendingRespawn> pending = new List<PendingRespawn>();
        readonly List<MatchParticipant> scratch = new List<MatchParticipant>();

        IMatchMode mode;
        MatchParticipant human;
        CameraFollow cameraFollow;
        PowerUpSpawner powerUpSpawner;
        DestructibleTilemap[] bricks;
        State state;
        float finishingEndsAt;
        bool paused;
        bool subscribed;
        MatchOutcome pendingOutcome;

        public static MatchManager Instance { get; private set; }

        public MatchModeConfig Config => config;
        public IMatchMode Mode => mode;
        public IReadOnlyList<MatchParticipant> Participants => participants;

        /// <summary>The teams that have tanks in this match, in order of appearance.</summary>
        public IReadOnlyList<Team> Teams => teams;

        public MatchParticipant Human => human;
        public bool IsPaused => paused;
        public bool IsFinished => state == State.Finished;

        /// <summary>The match is decided (or its result screen is up): the mode no longer scores.</summary>
        public bool IsOver => state != State.Running;

        /// <summary>The result of the finished match, null until then.</summary>
        public MatchResult Result { get; private set; }

        /// <summary>Gears the human earned for kills in this match so far (already in the wallet).</summary>
        public int KillGears { get; private set; }

        /// <summary>Raised after a death: the victim and the killer (null = unknown).</summary>
        public event Action<MatchParticipant, MatchParticipant> KillRegistered;

        /// <summary>Raised when the result screen should be shown.</summary>
        public event Action<MatchResult> MatchFinished;

        public event Action<bool> PauseChanged;

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
            Time.timeScale = 1f;
            if (Instance == this)
                Instance = null;
        }

        void Start()
        {
            if (!enabled)
                return;

            if (MatchSession.Selected != null)
                config = MatchSession.Selected;

            if (config == null)
            {
                Debug.LogError($"{name}: MatchModeConfig is not assigned.", this);
                enabled = false;
                return;
            }

            mode = CreateMode(config);
            if (mode == null)
            {
                Debug.LogError($"{name}: mode {config.kind} is not implemented.", this);
                enabled = false;
                return;
            }

            Time.timeScale = 1f;
            CollectParticipants();
            spawnPoints.AddRange(FindObjectsByType<SpawnPoint>(FindObjectsInactive.Exclude));
            cameraFollow = FindAnyObjectByType<CameraFollow>();
            powerUpSpawner = FindAnyObjectByType<PowerUpSpawner>();
            bricks = FindObjectsByType<DestructibleTilemap>(FindObjectsInactive.Include);
            if (capturePoint != null)
                capturePoint.SetActive(config.kind == MatchModeKind.CapturePoint);
            if (basePads != null)
                basePads.SetActive(config.kind != MatchModeKind.Deathmatch);

            Subscribe();
            MatchHud.Create(this);
            mode.Begin(this);
        }

        void OnEnable()
        {
            // Re-enabled after a disable: the subscriptions were dropped in OnDisable.
            if (mode != null)
                Subscribe();
        }

        void OnDisable()
        {
            if (!subscribed)
                return;

            foreach (MatchParticipant p in participants)
            {
                if (p.Health != null)
                    p.Health.Died -= OnTankDied;
            }

            subscribed = false;
        }

        void Subscribe()
        {
            if (subscribed)
                return;

            foreach (MatchParticipant p in participants)
                p.Health.Died += OnTankDied;
            subscribed = true;
        }

        void Update()
        {
            if (mode == null)
                return;

            if (state == State.Finishing)
            {
                if (Time.unscaledTime >= finishingEndsAt)
                    ShowResult();
                return;
            }

            if (state != State.Running || paused)
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
                case MatchModeKind.CapturePoint:
                    return new CapturePointMode(modeConfig);
                case MatchModeKind.Deathmatch:
                    return new DeathmatchMode(modeConfig);
                default:
                    return null;
            }
        }

        // ---- Setup ----

        void CollectParticipants()
        {
            participants.Clear();
            teams.Clear();

            TeamMember[] members = FindObjectsByType<TeamMember>(FindObjectsInactive.Include);
            Array.Sort(members, (a, b) => string.CompareOrdinal(a.name, b.name));

            bool deathmatch = config.kind == MatchModeKind.Deathmatch;
            int botsKept = 0;
            foreach (TeamMember member in members)
            {
                if (!member.TryGetComponent(out TankHealth health))
                    continue;

                bool isHuman = member.TryGetComponent(out PlayerTankInput _);

                // Free-for-all has the human and three bots; the scene's extra tanks (the allies) sit it out.
                if (deathmatch && !isHuman && botsKept >= 3)
                {
                    member.gameObject.SetActive(false);
                    continue;
                }

                if (!isHuman)
                    botsKept++;

                participants.Add(new MatchParticipant(member, health, isHuman));
            }

            // Human first, then the bots in name order: it makes the numbering and the team assignment stable.
            participants.Sort((a, b) => a.IsHuman == b.IsHuman ? 0 : (a.IsHuman ? -1 : 1));

            int botNumber = 0;
            int ffaTeam = 0;
            foreach (MatchParticipant p in participants)
            {
                if (p.IsHuman)
                    human = p;
                else
                    p.Name = "Бот " + (++botNumber);

                if (deathmatch)
                    p.Member.SetTeam((Team)ffaTeam++);

                if (!teams.Contains(p.Team))
                    teams.Add(p.Team);

                ApplySkin(p);
            }

            if (human != null)
            {
                human.Name = "Я";
                TeamMember.LocalTeam = human.Team;
            }

            // Spawn numbers: the human first inside its team, the rest in the order found.
            foreach (Team team in teams)
            {
                int index = 0;
                foreach (MatchParticipant p in participants)
                {
                    if (p.Team == team)
                        p.SpawnIndex = index++;
                }
            }
        }

        void ApplySkin(MatchParticipant p)
        {
            int index = (int)p.Team;
            if (teamSkins == null || index >= teamSkins.Length || teamSkins[index] == null)
                return;

            if (p.Member.TryGetComponent(out TankVisuals visuals))
                visuals.SetSkin(teamSkins[index]);
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

        public MatchParticipant FindParticipant(GameObject tankObject)
        {
            for (int i = 0; i < participants.Count; i++)
            {
                if (participants[i].Object == tankObject)
                    return participants[i];
            }

            return null;
        }

        // ---- Spawning ----

        /// <summary>
        /// A new round: clears projectiles, bricks and power-ups, recharges the nitro points, then puts every tank, living or dead, on a spawn point with
        /// full health, power-ups, nitro and the spawn protection.
        /// </summary>
        public void RespawnAll()
        {
            pending.Clear();
            ResetArena();
            foreach (MatchParticipant p in participants)
                Respawn(p);

            if (cameraFollow != null)
                cameraFollow.Snap();
        }

        void ResetArena()
        {
            Projectile.ClearAll();
            for (int i = 0; i < bricks.Length; i++)
            {
                if (bricks[i] != null)
                    bricks[i].ResetAll();
            }

            if (powerUpSpawner != null)
                powerUpSpawner.ResetSpawner();
            NitroStation.ResetAll();
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
        /// Places the tank on a spawn point (or where it stood at the start) and activates it with full health and the
        /// spawn protection from the config. A tank that is alive is reset the same way, which also clears its power-ups.
        /// </summary>
        public void Respawn(MatchParticipant participant)
        {
            GameObject tankObject = participant.Object;
            if (tankObject.activeSelf)
                tankObject.SetActive(false);

            Vector3 position = participant.StartPosition;
            float angle = participant.StartAngle;
            if (TryChooseSpawnPoint(participant, out SpawnPoint point))
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
            Fx.Burst(position, TeamColors.Of(participant.Team), 12, 3.5f, 0.5f, 0.14f);
            if (participant.IsHuman)
                AudioManager.Play(Sfx.Respawn);

            RefreshCamera();
        }

        bool TryChooseSpawnPoint(MatchParticipant participant, out SpawnPoint result)
        {
            result = null;
            return config.randomSpawn ? TryChooseFreePoint(participant, out result) : TryChooseBasePoint(participant, out result);
        }

        // Team base: the tank's number among its team's tanks picks the point (wrapping around). A respawn in the middle of
        // a round skips points where a living tank stands.
        bool TryChooseBasePoint(MatchParticipant participant, out SpawnPoint result)
        {
            result = null;
            int total = 0;
            for (int i = 0; i < spawnPoints.Count; i++)
            {
                if (!spawnPoints[i].FreeForAll && spawnPoints[i].Team == participant.Team)
                    total++;
            }

            if (total == 0)
                return false;

            int wanted = participant.SpawnIndex % total;
            SpawnPoint fallback = null;
            for (int i = 0; i < total; i++)
            {
                SpawnPoint candidate = NthBasePoint(participant.Team, (wanted + i) % total);
                if (fallback == null)
                    fallback = candidate;
                if (!IsOccupied(candidate.transform.position, participant))
                {
                    result = candidate;
                    return true;
                }
            }

            result = fallback;
            return true;
        }

        SpawnPoint NthBasePoint(Team team, int n)
        {
            for (int i = 0; i < spawnPoints.Count; i++)
            {
                if (spawnPoints[i].FreeForAll || spawnPoints[i].Team != team)
                    continue;

                if (n-- == 0)
                    return spawnPoints[i];
            }

            return null;
        }

        // Free-for-all: a random point that is far enough from every living enemy; if none is, the one farthest from them.
        bool TryChooseFreePoint(MatchParticipant participant, out SpawnPoint result)
        {
            result = null;
            float bestDistance = -1f;
            int safeCount = 0;
            for (int i = 0; i < spawnPoints.Count; i++)
            {
                if (!spawnPoints[i].FreeForAll || IsOccupied(spawnPoints[i].transform.position, participant))
                    continue;

                float distance = DistanceToNearestEnemy(spawnPoints[i].transform.position, participant);
                if (distance >= config.safeSpawnDistance)
                {
                    // Reservoir sampling: every safe point has the same chance, no list needed.
                    safeCount++;
                    if (UnityEngine.Random.Range(0, safeCount) == 0)
                        result = spawnPoints[i];
                }
                else if (safeCount == 0 && distance > bestDistance)
                {
                    bestDistance = distance;
                    result = spawnPoints[i];
                }
            }

            return result != null;
        }

        bool IsOccupied(Vector2 point, MatchParticipant except)
        {
            for (int i = 0; i < participants.Count; i++)
            {
                MatchParticipant p = participants[i];
                if (p != except && p.IsAlive && ((Vector2)p.Object.transform.position - point).sqrMagnitude < OccupiedRadius * OccupiedRadius)
                    return true;
            }

            return false;
        }

        float DistanceToNearestEnemy(Vector2 point, MatchParticipant self)
        {
            float best = float.MaxValue;
            for (int i = 0; i < participants.Count; i++)
            {
                MatchParticipant p = participants[i];
                if (p == self || p.Team == self.Team || !p.IsAlive)
                    continue;

                best = Mathf.Min(best, Vector2.Distance(p.Object.transform.position, point));
            }

            return best;
        }

        // ---- Deaths, rewards, camera ----

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

            // A tank that dies from its own shell (a ricochet) or has no known killer scores for nobody.
            if (killer == dead)
                killer = null;

            dead.Deaths++;
            if (killer != null && killer.Team != dead.Team)
            {
                killer.Kills++;
                if (killer.IsHuman)
                    PayKill();
            }

            if (state == State.Running)
                mode.OnDied(dead, killer);

            KillRegistered?.Invoke(dead, killer);
            RefreshCamera();
        }

        // Balance v0.2, section 12: paid at once for every enemy the human destroys.
        void PayKill()
        {
            ProgressionConfig progression = PlayerProgress.Config;
            int reward = progression != null ? progression.gearsPerKill : 0;
            if (reward <= 0)
                return;

            KillGears += reward;
            PlayerProgress.AddGears(reward);
            AudioManager.Play(Sfx.KillConfirm);
            AudioManager.Play(Sfx.GearGain);
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

        /// <summary>True while the camera follows someone other than the human (the human is dead and watching an ally).</summary>
        public bool IsSpectating => human != null && !human.IsAlive;

        // ---- Finishing, pause, leaving ----

        /// <summary>The mode decided the match. A short slow-motion, then the result screen.</summary>
        public void FinishMatch(MatchOutcome outcome)
        {
            if (state != State.Running)
                return;

            state = State.Finishing;
            pendingOutcome = outcome;
            finishingEndsAt = Time.unscaledTime + FinishingRealSeconds;
            Time.timeScale = FinishingTimeScale;
            pending.Clear();
            AudioManager.Play(outcome.HumanWon ? Sfx.MatchWin : Sfx.MatchLose);
        }

        void ShowResult()
        {
            state = State.Finished;
            Time.timeScale = 0f;
            Result = BuildResult(pendingOutcome);
            MatchFinished?.Invoke(Result);
        }

        MatchResult BuildResult(MatchOutcome outcome)
        {
            ProgressionConfig progression = PlayerProgress.Config;
            var result = new MatchResult
            {
                ModeName = config.displayName,
                HumanWon = outcome.HumanWon,
                Headline = outcome.HumanWon ? "ПОБЕДА" : "ПОРАЖЕНИЕ",
                Summary = outcome.Summary,
                DifficultyName = DifficultyConfig.NameOf(DifficultyConfig.CurrentIndex),
                ShowScoreColumn = config.kind == MatchModeKind.Deathmatch
            };

            // Rewards (balance v0.2, section 12): kills were paid when they happened, the goal is paid now.
            if (KillGears > 0)
                result.Rewards.Add(new RewardLine { Label = "Уничтожено врагов: " + (human != null ? human.Kills : 0), Gears = KillGears });

            int total = KillGears;
            if (outcome.HumanWon && progression != null && progression.gearsPerWin > 0)
            {
                result.Rewards.Add(new RewardLine { Label = "Победа в матче", Gears = progression.gearsPerWin });
                total += progression.gearsPerWin;
            }

            // Balance v0.2 §12: a lost match keeps only the gears already paid for kills, the extra goal included.
            if (outcome.HumanWon && outcome.ExtraGoalDone && config.extraGoalGears > 0)
            {
                result.Rewards.Add(new RewardLine { Label = config.extraGoalText, Gears = config.extraGoalGears });
                total += config.extraGoalGears;
            }

            int unpaid = total - KillGears;
            if (unpaid > 0)
                PlayerProgress.AddGears(unpaid);
            PlayerProgress.RegisterMatch(outcome.HumanWon);
            if (outcome.HumanWon)
            {
                int opened = PlayerProgress.UnlockNextDifficulty((int)config.kind, DifficultyConfig.CurrentIndex, DifficultyConfig.Count);
                if (opened >= 0)
                    result.UnlockText = "Открыта сложность: " + DifficultyConfig.NameOf(opened);
            }

            result.TotalGears = total;

            // The table: the human's team first, inside a team by kills; deathmatch by points.
            scratch.Clear();
            scratch.AddRange(participants);
            Team local = TeamMember.LocalTeam;
            bool byScore = result.ShowScoreColumn;
            scratch.Sort((a, b) =>
            {
                if (byScore && a.Score != b.Score)
                    return b.Score.CompareTo(a.Score);
                if (!byScore && a.Team != b.Team)
                    return a.Team == local ? -1 : (b.Team == local ? 1 : a.Team.CompareTo(b.Team));
                return b.Kills.CompareTo(a.Kills);
            });

            foreach (MatchParticipant p in scratch)
            {
                result.Rows.Add(new ResultRow
                {
                    Name = p.Name,
                    Team = p.Team,
                    Kills = p.Kills,
                    Deaths = p.Deaths,
                    Score = p.Score,
                    IsHuman = p.IsHuman
                });
            }

            return result;
        }

        /// <summary>Back key / menu button: pauses a running match, leaves a finished one.</summary>
        public void OnBackPressed()
        {
            if (state == State.Finished)
                ExitToMenu();
            else
                SetPaused(!paused);
        }

        public void SetPaused(bool value)
        {
            if (state == State.Finished || paused == value)
                return;

            paused = value;
            Time.timeScale = value ? 0f : (state == State.Finishing ? FinishingTimeScale : 1f);
            AudioManager.Play(value ? Sfx.UiOpen : Sfx.UiBack);
            PauseChanged?.Invoke(value);
        }

        public void RestartMatch()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void ExitToMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneNavigator.MenuSceneName);
        }
    }
}
