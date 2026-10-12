using UnityEngine;

namespace TankBrawl.Match
{
    public enum MatchModeKind
    {
        /// <summary>Team against team, no respawns inside a round, the match goes to N round wins.</summary>
        TeamElimination,

        /// <summary>Not implemented yet: hold the flag zone in the centre, respawn after a delay.</summary>
        CapturePoint,

        /// <summary>Not implemented yet: free-for-all of four tanks, respawn, points for kills and survival.</summary>
        Deathmatch
    }

    /// <summary>Numbers of one game mode. The rules themselves live in the <see cref="IMatchMode"/> that <see cref="kind"/> picks.</summary>
    [CreateAssetMenu(menuName = "TankBrawl/Match Mode Config", fileName = "MatchModeConfig")]
    public class MatchModeConfig : ScriptableObject
    {
        public MatchModeKind kind = MatchModeKind.TeamElimination;

        [Header("Rounds (TeamElimination)")]
        [Tooltip("A team that wins this many rounds wins the match (the design says 3 or 5).")]
        [Min(1)] public int roundsToWin = 3;

        [Tooltip("How long the 'ROUND N' banner stays at the start of a round, s.")]
        [Min(0f)] public float roundIntroSeconds = 2f;

        [Tooltip("How long the round result is shown before the next round starts, s.")]
        [Min(0f)] public float roundEndSeconds = 3f;

        [Tooltip("How long the match result is shown before a new match starts, s.")]
        [Min(0f)] public float matchEndSeconds = 6f;

        [Header("Spawn")]
        [Tooltip("Immunity (with blinking) after every spawn, round start included, s. 0 = none.")]
        [Min(0f)] public float spawnProtection = 2f;
    }
}
