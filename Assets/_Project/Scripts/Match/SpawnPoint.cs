using TankBrawl.Core;
using UnityEngine;

namespace TankBrawl.Match
{
    /// <summary>
    /// A place where a tank of <see cref="team"/> appears. The tank takes the point by its number inside the team,
    /// facing where the object's up axis (the green arrow in the editor) looks.
    /// </summary>
    public class SpawnPoint : MonoBehaviour
    {
        [SerializeField] Team team = Team.Player;

        public Team Team => team;

        void OnDrawGizmos()
        {
            Gizmos.color = team switch
            {
                Team.Player => Color.cyan,
                Team.Enemy => Color.red,
                Team.Third => Color.green,
                _ => Color.yellow
            };
            Gizmos.DrawWireSphere(transform.position, 0.45f);
            Gizmos.DrawLine(transform.position, transform.position + transform.up * 0.7f);
        }
    }
}
