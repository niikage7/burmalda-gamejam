using UnityEngine;
using UnityEngine.Tilemaps;

namespace TankBrawl.Map
{
    /// <summary>
    /// A tile with several looks. The look is picked from the cell coordinates, so the floor varies evenly,
    /// stays the same on every run, and any cell painted with this tile varies without extra work.
    /// With two looks they are balanced exactly (see <see cref="BalancedPatterns"/>); with more, a plain hash is used.
    /// </summary>
    [CreateAssetMenu(menuName = "Tank Brawl/Variant Tile", fileName = "VariantTile")]
    public class VariantTile : TileBase
    {
        [Tooltip("Looks to choose from. Picked per cell by a hash of its position.")]
        [SerializeField] Sprite[] sprites;

        [SerializeField] Color color = Color.white;

        [Tooltip("Change it to get a different arrangement of the same looks.")]
        [SerializeField] int seed;

        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            tileData.sprite = Pick(position);
            tileData.color = color;
            tileData.colliderType = Tile.ColliderType.None;
            tileData.flags = TileFlags.LockAll;
        }

        // Two looks: bit i of a pattern says which look cell i of a 2×2 block gets. Every pattern has two of each,
        // so the floor is exactly balanced and long same-look runs are rare (a plain hash gave runs of 14).
        static readonly int[] BalancedPatterns = { 0b0011, 0b0101, 0b0110, 0b1001, 0b1010, 0b1100 };

        Sprite Pick(Vector3Int position)
        {
            if (sprites == null || sprites.Length == 0)
                return null;

            if (sprites.Length == 2)
            {
                // >> floors for negative coordinates too.
                uint block = Hash(position.x >> 1, position.y >> 1);
                int pattern = BalancedPatterns[(int)(block % (uint)BalancedPatterns.Length)];
                int local = (position.x & 1) + 2 * (position.y & 1);
                return sprites[(pattern >> local) & 1];
            }

            return sprites[(int)(Hash(position.x, position.y) % (uint)sprites.Length)];
        }

        // Integer hash (xorshift-multiply): neighbouring cells get unrelated values.
        uint Hash(int x, int y)
        {
            uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(seed * 83492791);
            h ^= h >> 16;
            h *= 0x7feb352dU;
            h ^= h >> 15;
            h *= 0x846ca68bU;
            h ^= h >> 16;
            return h;
        }
    }
}
