using System.Collections.Generic;
using TankBrawl.Core;

namespace TankBrawl.Match
{
    /// <summary>One cell of the score strip at the top of the screen. The mode updates the instances in place and bumps <see cref="IMatchMode.ScoreVersion"/>.</summary>
    public sealed class ScoreEntry
    {
        public Team Team;
        public string Label = string.Empty;
        public string Value = string.Empty;

        /// <summary>Small second line (who is alive, the leader's mark).</summary>
        public string Detail = string.Empty;

        /// <summary>This entry is the human's own team or tank: the HUD highlights it.</summary>
        public bool IsLocal;
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

        /// <summary>Small caption above the score strip ("ROUND 2 / to 3 wins").</summary>
        string TitleText { get; }

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
