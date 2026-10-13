using TankBrawl.Core;
using UnityEngine;

namespace TankBrawl.Match
{
    /// <summary>
    /// A place where a tank appears, facing where the object's up axis (the green arrow in the editor) looks.
    /// A base point belongs to <see cref="team"/> (team modes); a free-for-all point belongs to nobody and is used
    /// by modes with <see cref="MatchModeConfig.randomSpawn"/>.
    /// </summary>
    public class SpawnPoint : MonoBehaviour
    {
        [SerializeField] Team team = Team.Player;

        [Tooltip("Not a team base: any tank may appear here when the mode respawns at random places.")]
        [SerializeField] bool freeForAll;

        public Team Team => team;

        public bool FreeForAll => freeForAll;

        void OnDrawGizmos()
        {
            Color color;
            if (freeForAll)
            {
                color = Color.white;
            }
            else
            {
                switch (team)
                {
                    case Team.Player: color = Color.cyan; break;
                    case Team.Enemy: color = Color.red; break;
                    case Team.Third: color = Color.green; break;
                    default: color = Color.yellow; break;
                }
            }

            Gizmos.color = color;
            Gizmos.DrawWireSphere(transform.position, 0.45f);
            Gizmos.DrawLine(transform.position, transform.position + transform.up * 0.7f);
        }
    }
}
