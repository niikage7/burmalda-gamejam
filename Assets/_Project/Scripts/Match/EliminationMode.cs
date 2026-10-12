using TankBrawl.Core;
using UnityEngine;

namespace TankBrawl.Match
{
    /// <summary>
    /// Mode 1: team against team, no respawns inside a round. A round ends when at most one team has living tanks;
    /// that team gets the point (nobody left = a draw, no point). The match goes to <see cref="MatchModeConfig.roundsToWin"/>
    /// round wins, then a new match starts after a pause. Texts are Russian, they are what the HUD shows.
    /// </summary>
    public sealed class EliminationMode : IMatchMode
    {
        enum Phase
        {
            Intro,
            Playing,
            RoundEnd,
            MatchEnd
        }

        readonly MatchModeConfig config;
        readonly int[] scores = new int[4];

        MatchManager match;
        Phase phase;
        float phaseEndsAt;
        int round;
        bool checkRound;
        int countdownShown;
        string roundResult;
        string scoreText = string.Empty;
        string bannerText = string.Empty;

        public EliminationMode(MatchModeConfig config)
        {
            this.config = config;
        }

        public string ScoreText => scoreText;
        public string BannerText => bannerText;

        public void Begin(MatchManager manager)
        {
            match = manager;
            StartMatch();
        }

        public void OnDied(MatchParticipant victim, MatchParticipant killer)
        {
            checkRound = true;
            RebuildScore();
        }

        public void Tick()
        {
            switch (phase)
            {
                case Phase.Intro:
                    if (checkRound)
                        EvaluateRound();
                    if (phase == Phase.Intro && Time.time >= phaseEndsAt)
                    {
                        phase = Phase.Playing;
                        bannerText = string.Empty;
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

                case Phase.MatchEnd:
                    if (Time.time >= phaseEndsAt)
                        StartMatch();
                    else
                        UpdateMatchEndCountdown();
                    break;
            }
        }

        void StartMatch()
        {
            System.Array.Clear(scores, 0, scores.Length);
            round = 0;
            StartRound();
        }

        void StartRound()
        {
            round++;
            checkRound = false;
            match.RespawnAll();

            phase = Phase.Intro;
            phaseEndsAt = Time.time + config.roundIntroSeconds;
            bannerText = "РАУНД " + round;
            RebuildScore();
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

            Team local = TeamMember.LocalTeam;
            if (survivor.HasValue)
            {
                Team winner = survivor.Value;
                scores[(int)winner]++;
                bool ours = winner == local;

                if (scores[(int)winner] >= config.roundsToWin)
                {
                    phase = Phase.MatchEnd;
                    phaseEndsAt = Time.time + config.matchEndSeconds;
                    roundResult = ours ? "ПОБЕДА В МАТЧЕ!" : "ПОРАЖЕНИЕ В МАТЧЕ";
                    countdownShown = -1;
                    UpdateMatchEndCountdown();
                    RebuildScore();
                    return;
                }

                roundResult = ours ? "РАУНД ВЫИГРАН" : "РАУНД ПРОИГРАН";
            }
            else
            {
                roundResult = "НИЧЬЯ";
            }

            phase = Phase.RoundEnd;
            phaseEndsAt = Time.time + config.roundEndSeconds;
            bannerText = roundResult;
            RebuildScore();
        }

        // The second line changes once a second, so the string is rebuilt only then.
        void UpdateMatchEndCountdown()
        {
            int left = Mathf.CeilToInt(phaseEndsAt - Time.time);
            if (left == countdownShown)
                return;

            countdownShown = left;
            bannerText = roundResult + "\nНовый матч через " + left;
        }

        // "ROUND 2   YOU 1 : 0 ENEMY   (to 3)   alive 3 : 2" for two teams; the local team is on the left.
        void RebuildScore()
        {
            Team local = TeamMember.LocalTeam;
            Team other = local;
            for (int i = 0; i < match.Teams.Count; i++)
            {
                if (match.Teams[i] != local)
                {
                    other = match.Teams[i];
                    break;
                }
            }

            scoreText = "РАУНД " + round
                        + "     ВЫ " + scores[(int)local] + " : " + scores[(int)other] + " ВРАГ"
                        + "     до " + config.roundsToWin + " побед"
                        + "     живы " + match.CountAlive(local) + " : " + match.CountAlive(other);
        }
    }
}
