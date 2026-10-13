using UnityEngine;

namespace TankBrawl.Core
{
    /// <summary>The colour of each team, used by the score, the kill feed and effects. Matches the tank skins (Blue, Red, Green, Yellow).</summary>
    public static class TeamColors
    {
        public static readonly Color Blue = new Color(0.35f, 0.65f, 1f, 1f);
        public static readonly Color Red = new Color(1f, 0.4f, 0.35f, 1f);
        public static readonly Color Green = new Color(0.45f, 0.88f, 0.45f, 1f);
        public static readonly Color Yellow = new Color(1f, 0.86f, 0.3f, 1f);

        public static Color Of(Team team)
        {
            switch (team)
            {
                case Team.Player: return Blue;
                case Team.Enemy: return Red;
                case Team.Third: return Green;
                default: return Yellow;
            }
        }

        /// <summary>Russian name of the team's colour, for "your team" labels.</summary>
        public static string Name(Team team)
        {
            switch (team)
            {
                case Team.Player: return "Синие";
                case Team.Enemy: return "Красные";
                case Team.Third: return "Зелёные";
                default: return "Жёлтые";
            }
        }
    }
}
