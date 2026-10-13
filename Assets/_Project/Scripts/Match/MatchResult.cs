using System.Collections.Generic;
using TankBrawl.Core;

namespace TankBrawl.Match
{
    /// <summary>What a mode reports when the match is decided.</summary>
    public sealed class MatchOutcome
    {
        /// <summary>The human achieved the main goal (their team won, or they placed first).</summary>
        public bool HumanWon;

        /// <summary>The extra goal (see <see cref="MatchModeConfig.extraGoalText"/>) was achieved. It pays only together with a win.</summary>
        public bool ExtraGoalDone;

        /// <summary>One line with the final score ("3 : 1", "112 : 87").</summary>
        public string Summary = string.Empty;
    }

    /// <summary>One line of the final table.</summary>
    public sealed class ResultRow
    {
        public string Name;
        public Team Team;
        public int Kills;
        public int Deaths;
        public int Score;
        public bool IsHuman;
    }

    /// <summary>One line of the reward breakdown.</summary>
    public sealed class RewardLine
    {
        public string Label;
        public int Gears;
    }

    /// <summary>Everything the end screen shows, built by <see cref="MatchManager"/> when the match finishes.</summary>
    public sealed class MatchResult
    {
        public string ModeName;
        public bool HumanWon;
        public string Headline;
        public string Summary;

        /// <summary>Name of the difficulty the match was played on.</summary>
        public string DifficultyName;

        /// <summary>Set when this win opened the next difficulty ("Открыта сложность: Сложная").</summary>
        public string UnlockText;

        public bool ShowScoreColumn;
        public readonly List<ResultRow> Rows = new List<ResultRow>();
        public readonly List<RewardLine> Rewards = new List<RewardLine>();
        public int TotalGears;
    }
}
