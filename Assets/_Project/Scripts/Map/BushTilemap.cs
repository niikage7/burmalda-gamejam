using UnityEngine;
using UnityEngine.Tilemaps;

namespace TankBrawl.Map
{
    /// <summary>Tilemap of bushes: no collider, tanks standing on it are concealed (see TankConcealment).</summary>
    [RequireComponent(typeof(Tilemap))]
    public class BushTilemap : MonoBehaviour
    {
        Tilemap tilemap;

        void Awake()
        {
            tilemap = GetComponent<Tilemap>();
        }

        /// <summary>True when there is a bush tile in the cell under <paramref name="worldPosition"/>.</summary>
        public bool Covers(Vector2 worldPosition)
        {
            return tilemap.HasTile(tilemap.WorldToCell(worldPosition));
        }
    }
}
