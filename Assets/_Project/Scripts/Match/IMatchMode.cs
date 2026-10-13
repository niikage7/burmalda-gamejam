using System.Collections.Generic;
using TankBrawl.Core;
using UnityEngine;

namespace TankBrawl.Match
{
    /// <summary>One side of the score board at the top of the screen (a team, or one tank in the free-for-all). The mode updates the instances in place and bumps <see cref="IMatchMode.ScoreVersion"/>.</summary>
    public sealed class ScoreEntry
    {
        public Team Team;
        public string Label = string.Empty;
        public string Value = string.Empty;

        /// <summary>The number behind <see cref="Value"/> (round wins, points).</summary>
        public int Points;

        /// <summary>How close the side is to winning the match, 0..1.</summary>
        public float Progress;

        /// <summary>The side stands out right now: it holds the point, or leads the free-for-all.</summary>
        public bool Highlight;

        /// <summary>This entry is the human's own team or tank: the HUD highlights it.</summary>
        public bool IsLocal;

        /// <summary>The tank this entry is about when every tank scores for itself; null for a team.</summary>
        public MatchParticipant Participant;
    }

    /// <summary>
    /// Rules of one game mode. <see cref="MatchManager"/> owns the tanks, spawn points and respawning; the mode decides
    /// when rounds start and end, who scores and what the HUD says. Texts must be cached strings that are rebuilt
    /// only when their content changes (the HUD reads them every frame without allocating).
    /// </summary>
    public interface IMatchMode
    {
        /// <summary>Called once, after the manager has collected the tanks. The mode starts the first round here.</summary>
        void Begin(MatchManager match);

        /// <summary>Called every frame while the match runs (not after it finished, not on pause).</summary>
        void Tick();

        /// <summary>A tank died. <paramref name="killer"/> is null when unknown. Called inside the damage callback: only record, act in <see cref="Tick"/>.</summary>
        void OnDied(MatchParticipant victim, MatchParticipant killer);

        /// <summary>What is going on now ("ROUND 2"). The score board shows it in the middle when there is no clock.</summary>
        string TitleText { get; }

        /// <summary>What wins the match, short ("TO 3 WINS"): the small caption in the middle of the score board.</summary>
        string GoalText { get; }

        /// <summary>A short state line on the tab under the score board ("WE HOLD THE POINT"); empty = no tab.</summary>
        string StatusText { get; }

        /// <summary>Colour of the status tab.</summary>
        Color StatusColor { get; }

        /// <summary>Match clock ("7:42"), empty when the mode has no time limit.</summary>
        string TimerText { get; }

        /// <summary>Big message in the middle of the screen; empty = nothing.</summary>
        string BannerText { get; }

        /// <summary>Small hint line under the banner ("Respawn in 4").</summary>
        string HintText { get; }

        /// <summary>Raised by one whenever <see cref="Scores"/> changed.</summary>
        int ScoreVersion { get; }

        IReadOnlyList<ScoreEntry> Scores { get; }
    }
}
