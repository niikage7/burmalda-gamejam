using UnityEngine;

namespace TankBrawl.Core
{
    /// <summary>
    /// Side identifiers. Player and Enemy keep their numbers (they are stored in scenes and prefabs); Third and Fourth
    /// are for modes with more sides (deathmatch: one team per tank). The names carry no meaning beyond that:
    /// which of them is "ours" is <see cref="TeamMember.LocalTeam"/>.
    /// </summary>
    public enum Team
    {
        Player = 0,
        Enemy = 1,
        Third = 2,
        Fourth = 3
    }

    /// <summary>Which side a tank is on. Projectiles pass through their own team and only collide with the other ones.</summary>
    public class TeamMember : MonoBehaviour
    {
        [SerializeField] Team team = Team.Enemy;

        public Team Team => team;

        /// <summary>Moves the tank to another team (a match mode sets the sides when it starts).</summary>
        public void SetTeam(Team newTeam)
        {
            team = newTeam;
        }

        /// <summary>The team of the human player (bushes hide the others from it). Set by the player's input component; Player until then.</summary>
        public static Team LocalTeam { get; set; } = Team.Player;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            LocalTeam = Team.Player;
        }
    }
}
