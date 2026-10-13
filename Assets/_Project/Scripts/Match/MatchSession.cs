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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Selected = null;
        }
    }
}
