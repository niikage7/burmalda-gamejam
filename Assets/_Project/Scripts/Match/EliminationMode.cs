using TankBrawl.Audio;
using TankBrawl.Core;
using UnityEngine;

namespace TankBrawl.Match
{
    /// <summary>
    /// Mode 1: team against team, no respawns inside a round. A round ends when at most one team has living tanks;
    /// that team gets the point (nobody left = a draw, no point). The match goes to <see cref="MatchModeConfig.roundsToWin"/>
    /// round wins. Texts are Russian, they are what the HUD shows. Extra goal: the human never died in the match.
    /// </summary>
    public sealed class EliminationMode : MatchModeBase
    {
        enum Phase
        {
            Intro,
            Playing,
            RoundEnd
        }

        readonly int[] scores = new int[4];

        ScoreEntry ours;
        ScoreEntry theirs;
        Team ourTeam;
        Team theirTeam;
        Phase phase;
        float phaseEndsAt;
        int round;
        bool checkRound;

        public EliminationMode(MatchModeConfig config) : base(config)
        {
        }

        public override void Begin(MatchManager manager)
        {
            match = manager;
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
            SetGoal("ДО " + config.roundsToWin + " ПОБЕД");
            StartRound();
        }

        public override void OnDied(MatchParticipant victim, MatchParticipant killer)
        {
            checkRound = true;
            RefreshEntries();
        }

        public override void Tick()
        {
            switch (phase)
            {
                case Phase.Intro:
                    if (checkRound)
                        EvaluateRound();
                    if (phase == Phase.Intro && Time.time >= phaseEndsAt)
                    {
                        phase = Phase.Playing;
                        SetBanner(string.Empty);
                    }

                    break;

                case Phase.Playing:
                    if (checkRound)
                        EvaluateRound();
                    break;

                case Phase.RoundEnd:
                    if (Time.time >= phaseEndsAt)
                        StartRound();
                    break;
            }
        }

        void StartRound()
        {
            round++;
            checkRound = false;
            match.RespawnAll();

            phase = Phase.Intro;
            phaseEndsAt = Time.time + config.roundIntroSeconds;
            SetBanner("РАУНД " + round);
            SetTitle("РАУНД " + round);
            AudioManager.Play(Sfx.RoundStart);
            RefreshEntries();
        }

        // The round is over when fewer than two teams have living tanks.
        void EvaluateRound()
        {
            checkRound = false;

            Team? survivor = null;
            int teamsAlive = 0;
            for (int i = 0; i < match.Teams.Count; i++)
            {
                Team team = match.Teams[i];
                if (match.CountAlive(team) > 0)
                {
                    teamsAlive++;
                    survivor = team;
                }
            }

            if (teamsAlive >= 2)
                return;

            string result;
            if (survivor.HasValue)
            {
                Team winner = survivor.Value;
                scores[(int)winner]++;
                bool weWon = winner == ourTeam;
                RefreshEntries();

                if (scores[(int)winner] >= config.roundsToWin)
                {
                    match.FinishMatch(new MatchOutcome
                    {
                        HumanWon = weWon,
                        ExtraGoalDone = weWon && match.Human != null && match.Human.Deaths == 0,
                        Summary = scores[(int)ourTeam] + " : " + scores[(int)theirTeam]
                    });
                    SetBanner(weWon ? "ПОБЕДА В МАТЧЕ!" : "ПОРАЖЕНИЕ В МАТЧЕ");
                    return;
                }

                result = weWon ? "РАУНД ВЫИГРАН" : "РАУНД ПРОИГРАН";
                AudioManager.Play(weWon ? Sfx.RoundWin : Sfx.RoundLose);
            }
            else
            {
                result = "НИЧЬЯ";
            }

            phase = Phase.RoundEnd;
            phaseEndsAt = Time.time + config.roundEndSeconds;
            SetBanner(result);
        }

        void RefreshEntries()
        {
            Fill(ours, scores[(int)ourTeam]);
            Fill(theirs, scores[(int)theirTeam]);
            BumpScore();
        }

        void Fill(ScoreEntry entry, int wins)
        {
            if (entry.Points != wins || entry.Value.Length == 0)
                entry.Value = wins.ToString();
            entry.Points = wins;
            entry.Progress = Mathf.Clamp01((float)wins / config.roundsToWin);
        }
    }
}
