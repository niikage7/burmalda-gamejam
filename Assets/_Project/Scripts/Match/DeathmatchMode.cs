using System.Collections.Generic;
using TankBrawl.Audio;
using TankBrawl.Core;
using UnityEngine;

namespace TankBrawl.Match
{
    /// <summary>
    /// Mode 3: everyone against everyone (the human and three bots, one team each). A kill gives
    /// <see cref="MatchModeConfig.killPoints"/>; a tank that stays alive for <see cref="MatchModeConfig.survivalStartSeconds"/>
    /// then earns <see cref="MatchModeConfig.survivalPoints"/> every <see cref="MatchModeConfig.survivalIntervalSeconds"/>
    /// until it dies. First to <see cref="MatchModeConfig.scoreLimit"/> wins; when the time runs out the leader wins
    /// (a tie for first plays on). Respawn at a random free point after <see cref="MatchModeConfig.respawnDelay"/>.
    /// Extra goal: the human made <see cref="MatchModeConfig.extraGoalKills"/> kills.
    /// </summary>
    public sealed class DeathmatchMode : MatchModeBase
    {
        const float IntroSeconds = 2.5f;

        readonly List<ScoreEntry> byParticipant = new List<ScoreEntry>();
        readonly List<int> scoreList = new List<int>();

        // Per participant, in the manager's order: since when the tank has been alive, and when its next survival point is due.
        float[] aliveSince;
        float[] nextSurvivalAt;
        float startedAt;
        float introEndsAt;
        float humanRespawnAt;
        bool overtime;

        public DeathmatchMode(MatchModeConfig config) : base(config)
        {
        }

        public override void Begin(MatchManager manager)
        {
            match = manager;
            int count = match.Participants.Count;
            aliveSince = new float[count];
            nextSurvivalAt = new float[count];

            for (int i = 0; i < count; i++)
            {
                MatchParticipant p = match.Participants[i];
                p.Score = 0;
                ScoreEntry entry = AddEntry(p.Team, p.Name, p.IsHuman);
                entry.Participant = p;
                byParticipant.Add(entry);
                aliveSince[i] = -1f;
            }

            match.RespawnAll();
            startedAt = Time.time;
            introEndsAt = startedAt + IntroSeconds;
            SetTitle("ВСЕ ПРОТИВ ВСЕХ");
            SetGoal("ДО " + config.scoreLimit);
            SetBanner("ВСЕ ПРОТИВ ВСЕХ");
            AudioManager.Play(Sfx.RoundStart);
            Refresh();
        }

        public override void OnDied(MatchParticipant victim, MatchParticipant killer)
        {
            int index = IndexOf(victim);
            if (index >= 0)
                aliveSince[index] = -1f;

            if (killer != null && killer != victim)
            {
                killer.Score += config.killPoints;
                Refresh();
            }

            match.ScheduleRespawn(victim, config.respawnDelay);
            if (victim.IsHuman)
                humanRespawnAt = Time.time + config.respawnDelay;
        }

        public override void Tick()
        {
            float now = Time.time;
            if (BannerText.Length > 0 && now >= introEndsAt)
                SetBanner(string.Empty);

            bool changed = false;
            for (int i = 0; i < match.Participants.Count; i++)
            {
                MatchParticipant p = match.Participants[i];
                if (!p.IsAlive)
                    continue;

                // A tank that just came back starts its survival clock.
                if (aliveSince[i] < 0f)
                {
                    aliveSince[i] = now;
                    nextSurvivalAt[i] = now + config.survivalStartSeconds;
                }
                else if (now >= nextSurvivalAt[i])
                {
                    nextSurvivalAt[i] += config.survivalIntervalSeconds;
                    p.Score += config.survivalPoints;
                    changed = true;
                }
            }

            if (changed)
                Refresh();

            if (config.timeLimit > 0f && !overtime)
                SetClock(config.timeLimit - (now - startedAt));
            else if (overtime)
                SetClock(0f);

            MatchParticipant human = match.Human;
            if (human != null && !human.IsAlive)
                SetCountdownHint("Возрождение через ", humanRespawnAt - now);
            else if (HintText.Length > 0)
                SetHint(string.Empty);

            CheckEnd(now);
        }

        void CheckEnd(float now)
        {
            scoreList.Clear();
            for (int i = 0; i < match.Participants.Count; i++)
                scoreList.Add(match.Participants[i].Score);

            HasUniqueLeader(scoreList, out int leader);
            bool limitReached = config.scoreLimit > 0 && scoreList[leader] >= config.scoreLimit;
            bool timeUp = config.timeLimit > 0f && now - startedAt >= config.timeLimit;
            if (!limitReached && !timeUp)
                return;

            // A tie for first place plays on until somebody leads.
            if (!HasUniqueLeader(scoreList, out leader))
            {
                if (!overtime)
                {
                    overtime = true;
                    SetBanner("ДОПОЛНИТЕЛЬНОЕ ВРЕМЯ");
                    introEndsAt = now + IntroSeconds;
                }

                return;
            }

            MatchParticipant winner = match.Participants[leader];
            MatchParticipant human = match.Human;
            bool humanWon = winner == human;
            int place = 1;
            for (int i = 0; i < match.Participants.Count; i++)
            {
                if (match.Participants[i].Score > human.Score)
                    place++;
            }

            match.FinishMatch(new MatchOutcome
            {
                HumanWon = humanWon,
                ExtraGoalDone = human.Kills >= config.extraGoalKills,
                Summary = place + " место · " + human.Score + " очков"
            });
            SetBanner(humanWon ? "ПОБЕДА В МАТЧЕ!" : winner.Name + " ПОБЕЖДАЕТ");
        }

        int IndexOf(MatchParticipant p)
        {
            for (int i = 0; i < match.Participants.Count; i++)
            {
                if (match.Participants[i] == p)
                    return i;
            }

            return -1;
        }

        void Refresh()
        {
            scoreList.Clear();
            for (int i = 0; i < match.Participants.Count; i++)
                scoreList.Add(match.Participants[i].Score);
            bool unique = HasUniqueLeader(scoreList, out int leader);

            for (int i = 0; i < byParticipant.Count; i++)
            {
                ScoreEntry entry = byParticipant[i];
                int points = match.Participants[i].Score;
                if (entry.Points != points || entry.Value.Length == 0)
                    entry.Value = points.ToString();
                entry.Points = points;
                entry.Progress = config.scoreLimit > 0 ? Mathf.Clamp01((float)points / config.scoreLimit) : 0f;
                entry.Highlight = unique && i == leader && points > 0;
            }

            BumpScore();
        }
    }
}
