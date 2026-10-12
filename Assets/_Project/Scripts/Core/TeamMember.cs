using UnityEngine;

namespace TankBrawl.Core
{
    public enum Team
    {
        Player,
        Enemy
    }

    /// <summary>Which side a tank is on. Projectiles pass through their own team and only collide with the other one.</summary>
    public class TeamMember : MonoBehaviour
    {
        [SerializeField] Team team = Team.Enemy;

        public Team Team => team;
    }
}
