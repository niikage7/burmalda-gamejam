using UnityEngine;

namespace TankBrawl.Core
{
    /// <summary>Side in a match. Tanks of one team do not hurt each other.</summary>
    public enum Team
    {
        Player,
        Enemy
    }

    /// <summary>Which side a tank is on. Projectiles pass through their own team and only collide with the other one.</summary>
    public class TeamMember : MonoBehaviour
    {
        [SerializeField] Team team = Team.Enemy;

        /// <summary>Side of this tank, set in the inspector.</summary>
        public Team Team => team;
    }
}
