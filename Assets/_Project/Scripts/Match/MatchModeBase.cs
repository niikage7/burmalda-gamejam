using System.Collections.Generic;
using TankBrawl.Core;
using UnityEngine;

namespace TankBrawl.Match
{
    /// <summary>
    /// What every mode needs: the cached HUD strings with change detection, the match clock, and the "respawn in N" hint.
    /// Strings are rebuilt only when the number inside changes, so the HUD can read them every frame for free.
    /// </summary>
    public abstract class MatchModeBase : IMatchMode
    {
        protected readonly MatchModeConfig config;
        protected MatchManager match;

        readonly List<ScoreEntry> entries = new List<ScoreEntry>();
        int lastClockSeconds = -1;
        int lastHintSeconds = -1;
        string timerText = string.Empty;
        string hintText = string.Empty;
        string bannerText = string.Empty;
        string titleText = string.Empty;
        string goalText = string.Empty;
        string statusText = string.Empty;
        Color statusColor = Color.white;

        protected MatchModeBase(MatchModeConfig config)
        {
            this.config = config;
        }

        public string TitleText => titleText;
        public string GoalText => goalText;
        public string StatusText => statusText;
        public Color StatusColor => statusColor;
        public string TimerText => timerText;
        public string BannerText => bannerText;
        public string HintText => hintText;
        public int ScoreVersion { get; private set; }
        public IReadOnlyList<ScoreEntry> Scores => entries;

        protected List<ScoreEntry> Entries => entries;

        public abstract void Begin(MatchManager manager);
        public abstract void Tick();
        public abstract void OnDied(MatchParticipant victim, MatchParticipant killer);

        protected void SetTitle(string text) => titleText = text;

        protected void SetGoal(string text) => goalText = text;

        protected void SetStatus(string text, Color color)
        {
            statusText = text ?? string.Empty;
            statusColor = color;
        }

        protected void SetBanner(string text) => bannerText = text ?? string.Empty;

        protected void SetHint(string text)
        {
            hintText = text ?? string.Empty;
            lastHintSeconds = -1;
        }

        /// <summary>Tells the HUD that the entries changed.</summary>
        protected void BumpScore() => ScoreVersion++;

        /// <summary>Shows "m:ss" for the remaining seconds; the string is rebuilt once per second.</summary>
        protected void SetClock(float secondsLeft)
        {
            int seconds = Mathf.Max(0, Mathf.CeilToInt(secondsLeft));
            if (seconds == lastClockSeconds)
                return;

            lastClockSeconds = seconds;
            timerText = (seconds / 60) + ":" + (seconds % 60).ToString("00");
        }

        protected void ClearClock()
        {
            lastClockSeconds = -1;
            timerText = string.Empty;
        }

        /// <summary>Shows "prefix N suffix" as the hint for the seconds left; rebuilt once per second. Hides the hint when not positive.</summary>
        protected void SetCountdownHint(string prefix, float secondsLeft)
        {
            int seconds = Mathf.CeilToInt(secondsLeft);
            if (seconds == lastHintSeconds)
                return;

            lastHintSeconds = seconds;
            hintText = seconds > 0 ? prefix + seconds : string.Empty;
        }

        /// <summary>A score entry for a team; the first call decides the order on the board.</summary>
        protected ScoreEntry AddEntry(Team team, string label, bool isLocal)
        {
            var entry = new ScoreEntry { Team = team, Label = label, IsLocal = isLocal };
            entries.Add(entry);
            return entry;
        }

        /// <summary>Between two equally good results the mode plays on; this tells whether the first of two scores leads.</summary>
        protected static bool HasUniqueLeader(IReadOnlyList<int> values, out int leaderIndex)
        {
            leaderIndex = -1;
            int best = int.MinValue;
            int bestCount = 0;
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] > best)
                {
                    best = values[i];
                    bestCount = 1;
                    leaderIndex = i;
                }
                else if (values[i] == best)
                {
                    bestCount++;
                }
            }

            return bestCount == 1;
        }
    }
}
