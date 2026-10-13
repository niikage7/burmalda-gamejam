using UnityEngine;

namespace TankBrawl.Match
{
    public enum MatchModeKind
    {
        /// <summary>Team against team, no respawns inside a round, the match goes to N round wins.</summary>
        TeamElimination,

        /// <summary>Team against team, hold the flag zone in the centre, respawn after a delay.</summary>
        CapturePoint,

        /// <summary>Free-for-all of four tanks, respawn, points for kills and survival.</summary>
        Deathmatch
    }

    /// <summary>Numbers of one game mode. The rules themselves live in the <see cref="IMatchMode"/> that <see cref="kind"/> picks.</summary>
    [CreateAssetMenu(menuName = "TankBrawl/Match Mode Config", fileName = "MatchModeConfig")]
    public class MatchModeConfig : ScriptableObject
    {
        public MatchModeKind kind = MatchModeKind.TeamElimination;

        [Header("Menu")]
        public string displayName = "Команда на команду";

        [TextArea(2, 4)] public string description = "3 на 3, без возрождений. Раунд до уничтожения команды врага.";

        [Header("Spawn")]
        [Tooltip("Immunity (with blinking) after every spawn, round start included, s. 0 = none.")]
        [Min(0f)] public float spawnProtection = 2f;

        [Tooltip("Seconds between a death and the return to the map. 0 = no respawn inside a round.")]
        [Min(0f)] public float respawnDelay;

        [Tooltip("Respawn on a random free-for-all point away from the enemies (deathmatch) instead of at the team base.")]
        public bool randomSpawn;

        [Tooltip("A random respawn point must be at least this far from every living enemy, units.")]
        [Min(0f)] public float safeSpawnDistance = 6f;

        [Header("Limits")]
        [Tooltip("Match length, s. When it ends the leader wins (a tie goes on until someone leads). 0 = no limit.")]
        [Min(0f)] public float timeLimit;

        [Tooltip("Points that win the match at once (point and deathmatch modes). 0 = none.")]
        [Min(0)] public int scoreLimit;

        [Header("Rounds (TeamElimination)")]
        [Tooltip("A team that wins this many rounds wins the match (the design says 3 or 5).")]
        [Min(1)] public int roundsToWin = 3;

        [Tooltip("How long the 'ROUND N' banner stays at the start of a round, s.")]
        [Min(0f)] public float roundIntroSeconds = 2f;

        [Tooltip("How long the round result is shown before the next round starts, s.")]
        [Min(0f)] public float roundEndSeconds = 3f;

        [Header("Point (CapturePoint)")]
        [Tooltip("Points per second for the team that alone has a tank in the zone.")]
        [Min(0f)] public float pointsPerSecond = 1f;

        [Header("Deathmatch")]
        [Tooltip("Points for destroying an enemy.")]
        [Min(0)] public int killPoints = 10;

        [Tooltip("After this many seconds alive without dying a tank starts earning survival points, s.")]
        [Min(0f)] public float survivalStartSeconds = 25f;

        [Tooltip("Then it gets survivalPoints every this many seconds, s.")]
        [Min(0.5f)] public float survivalIntervalSeconds = 5f;

        [Min(0)] public int survivalPoints = 1;

        [Header("Extra goal (gears on top of the kills and the win; paid only when the match is won)")]
        [Tooltip("Text shown in the results. The condition itself is in the mode: Elimination = the human never died, Point = held the zone for the time below, Deathmatch = the kill count below.")]
        public string extraGoalText = "Без потерь: ни разу не погибнуть";

        [Min(0)] public int extraGoalGears = 20;

        [Tooltip("Point mode: seconds the human must stand in the zone alive.")]
        [Min(0f)] public float extraGoalHoldSeconds = 30f;

        [Tooltip("Deathmatch: kills the human must make.")]
        [Min(0)] public int extraGoalKills = 5;
    }
}
