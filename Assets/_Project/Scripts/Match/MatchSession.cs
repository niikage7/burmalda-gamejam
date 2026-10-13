using UnityEngine;

namespace TankBrawl.Match
{
    /// <summary>
    /// What the menu picked, handed to the arena scene. Static because a scene load destroys everything else.
    /// A scene opened directly (from the editor or a debug list) has nothing selected and falls back to the manager's own config.
    /// </summary>
    public static class MatchSession
    {
        public static MatchModeConfig Selected { get; set; }

        /// <summary>Scene name of the map picked in the menu, so the menu shows the same map after a match. Null = the default arena.</summary>
        public static string MapScene { get; set; }

        /// <summary>The difficulty last picked in the menu per game mode (index of <see cref="MatchModeKind"/>), -1 = not picked yet.</summary>
        public static readonly int[] LastDifficulty = { -1, -1, -1 };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Selected = null;
            MapScene = null;
            for (int i = 0; i < LastDifficulty.Length; i++)
                LastDifficulty[i] = -1;
        }
    }
}
