using TankBrawl.Audio;
using TankBrawl.Core;
using TankBrawl.Map;
using UnityEngine;

namespace TankBrawl.Match
{
    /// <summary>
    /// Mode 2: two teams fight for the flag zone in the centre. Every second that exactly one team has a living tank in the
    /// zone gives that team <see cref="MatchModeConfig.pointsPerSecond"/> points (tanks of both teams inside = contested, nobody
    /// scores). The first to <see cref="MatchModeConfig.scoreLimit"/> wins; when <see cref="MatchModeConfig.timeLimit"/> runs out the
    /// leader wins (a tie plays on). Dead tanks come back at their base after <see cref="MatchModeConfig.respawnDelay"/>.
    /// Extra goal: the human spent <see cref="MatchModeConfig.extraGoalHoldSeconds"/> in the zone while his team was scoring.
    /// </summary>
    public sealed class CapturePointMode : MatchModeBase
    {
        const float IntroSeconds = 2.5f;

        static readonly Color ContestedColor = new Color(1f, 0.82f, 0.25f, 1f);
        static readonly Color NeutralColor = new Color(0.45f, 0.5f, 0.6f, 1f);

        readonly float[] scores = new float[4];
        readonly int[] shown = new int[4];

        CapturePointZone zone;
        ScoreEntry ours;
        ScoreEntry theirs;
        Team ourTeam;
        Team theirTeam;
        float startedAt;
        float introEndsAt;
        float humanRespawnAt;
        float humanHoldSeconds;
        bool overtime;
        bool contested;
        bool lastContested;
        int lastOwner = -1;

        public CapturePointMode(MatchModeConfig config) : base(config)
        {
        }

        public override void Begin(MatchManager manager)
        {
            match = manager;
            zone = CapturePointZone.Primary;
            if (zone == null)
                Debug.LogError("CapturePointMode: no CapturePointZone in the scene, nobody can score.");

            ourTeam = TeamMember.LocalTeam;
            theirTeam = ourTeam;
            for (int i = 0; i < match.Teams.Count; i++)
            {
                if (match.Teams[i] != ourTeam)
                {
                    theirTeam = match.Teams[i];
                    break;
                }
            }

            ours = AddEntry(ourTeam, "МЫ", true);
            theirs = AddEntry(theirTeam, "ВРАГ", false);

            match.RespawnAll();
            startedAt = Time.time;
            introEndsAt = startedAt + IntroSeconds;
            SetTitle("УДЕРЖАНИЕ ТОЧКИ");
            SetGoal("ДО " + config.scoreLimit);
            SetBanner("ДЕРЖИТЕ ТОЧКУ В ЦЕНТРЕ");
            AudioManager.Play(Sfx.RoundStart);
            Refresh();
        }

        public override void OnDied(MatchParticipant victim, MatchParticipant killer)
        {
            match.ScheduleRespawn(victim, config.respawnDelay);
            if (victim.IsHuman)
                humanRespawnAt = Time.time + config.respawnDelay;
        }

        public override void Tick()
        {
            float now = Time.time;
            if (BannerText.Length > 0 && now >= introEndsAt)
                SetBanner(string.Empty);

            float dt = Time.deltaTime;
            int ownerTeam = -1;
            if (zone != null)
                ownerTeam = UpdateZone(dt);

            if (ownerTeam != lastOwner || contested != lastContested)
            {
                if (ownerTeam != lastOwner && ownerTeam == (int)ourTeam)
                    AudioManager.Play(Sfx.ZoneCapture);
                lastOwner = ownerTeam;
                lastContested = contested;
                Refresh();
            }

            UpdateClockAndHint(now);
            CheckEnd(now);
        }

        // Counts the living tanks of every team in the zone. Returns the team that alone holds it, or -1
        // (then `contested` tells whether several teams are inside or nobody is).
        int UpdateZone(float dt)
        {
            int owner = -1;
            int teamsInside = 0;
            for (int t = 0; t < match.Teams.Count; t++)
            {
                Team team = match.Teams[t];
                bool inside = false;
                for (int i = 0; i < match.Participants.Count; i++)
                {
                    MatchParticipant p = match.Participants[i];
                    if (p.Team == team && p.IsAlive && zone.Contains(p.Object.transform.position))
                    {
                        inside = true;
                        break;
                    }
                }

                if (inside)
                {
                    teamsInside++;
                    owner = (int)team;
                }
            }

            contested = teamsInside > 1;
            if (teamsInside != 1)
                return -1;

            scores[owner] += config.pointsPerSecond * dt;
            int whole = Mathf.FloorToInt(scores[owner]);
            if (whole != shown[owner])
            {
                shown[owner] = whole;
                Refresh();
            }

            MatchParticipant human = match.Human;
            if (human != null && owner == (int)human.Team && human.IsAlive && zone.Contains(human.Object.transform.position))
                humanHoldSeconds += dt;

            return owner;
        }

        void UpdateClockAndHint(float now)
        {
            if (config.timeLimit > 0f && !overtime)
                SetClock(config.timeLimit - (now - startedAt));
            else if (overtime)
                SetClock(0f);

            MatchParticipant human = match.Human;
            if (human != null && !human.IsAlive)
                SetCountdownHint("Возрождение через ", humanRespawnAt - now);
            else if (HintText.Length > 0)
                SetHint(string.Empty);
        }

        void CheckEnd(float now)
        {
            int ourScore = shown[(int)ourTeam];
            int theirScore = shown[(int)theirTeam];

            bool limitReached = config.scoreLimit > 0 && (ourScore >= config.scoreLimit || theirScore >= config.scoreLimit);
            bool timeUp = config.timeLimit > 0f && now - startedAt >= config.timeLimit;

            if (!limitReached && !timeUp)
                return;

            // Time is up with a tie: play on until somebody leads.
            if (!limitReached && ourScore == theirScore)
            {
                if (!overtime)
                {
                    overtime = true;
                    SetBanner("ДОПОЛНИТЕЛЬНОЕ ВРЕМЯ");
                    introEndsAt = now + IntroSeconds;
                }

                return;
            }

            bool weWon = ourScore > theirScore;
            match.FinishMatch(new MatchOutcome
            {
                HumanWon = weWon,
                ExtraGoalDone = humanHoldSeconds >= config.extraGoalHoldSeconds,
                Summary = ourScore + " : " + theirScore
            });
            SetBanner(weWon ? "ПОБЕДА В МАТЧЕ!" : "ПОРАЖЕНИЕ В МАТЧЕ");
        }

        void Refresh()
        {
            Fill(ours, ourTeam);
            Fill(theirs, theirTeam);
            BumpScore();

            if (lastOwner == (int)ourTeam)
                SetStatus("МЫ ДЕРЖИМ ТОЧКУ", TeamColors.Of(ourTeam));
            else if (lastOwner == (int)theirTeam)
                SetStatus("ВРАГ ДЕРЖИТ ТОЧКУ", TeamColors.Of(theirTeam));
            else if (lastContested)
                SetStatus("СПОР ЗА ТОЧКУ", ContestedColor);
            else
                SetStatus("ТОЧКА СВОБОДНА", NeutralColor);

            // The zone shows who holds it.
            if (zone != null)
            {
                if (lastOwner >= 0)
                    zone.SetTint(TeamColors.Of((Team)lastOwner));
                else
                    zone.SetNeutral();
            }
        }

        void Fill(ScoreEntry entry, Team team)
        {
            int points = shown[(int)team];
            if (entry.Points != points || entry.Value.Length == 0)
                entry.Value = points.ToString();
            entry.Points = points;
            entry.Progress = config.scoreLimit > 0 ? Mathf.Clamp01((float)points / config.scoreLimit) : 0f;
            entry.Highlight = lastOwner == (int)team;
        }
    }
}
