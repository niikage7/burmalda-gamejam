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

        public bool Covers(Vector2 worldPosition)
        {
            return tilemap.HasTile(tilemap.WorldToCell(worldPosition));
        }
    }
}
