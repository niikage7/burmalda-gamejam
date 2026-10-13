using UnityEngine;
using UnityEngine.Tilemaps;

namespace TankBrawl.Map
{
    /// <summary>
    /// A checkerboard floor: light and dark cells alternate, and each cell picks one of several looks
    /// (plain, tufts of grass) by a hash of its coordinates, so the floor stays the same on every run
    /// and any cell painted with this tile follows the pattern.
    /// </summary>
    [CreateAssetMenu(menuName = "Tank Brawl/Checker Tile", fileName = "CheckerTile")]
    public class CheckerTile : TileBase
    {
        [Tooltip("Looks of the light cells.")]
        [SerializeField] Sprite[] lightSprites;

        [Tooltip("Looks of the dark cells. Same order as the light ones, so a look has the same weight on both.")]
        [SerializeField] Sprite[] darkSprites;

        [Tooltip("How often each look is picked, relative to the others. Missing entries count as 1.")]
        [SerializeField, Min(0)] int[] weights;

        [Tooltip("Change it to get a different arrangement of the same looks.")]
        [SerializeField] int seed;

        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            // & keeps working for negative coordinates: (-1) & 1 == 1.
            bool light = ((position.x + position.y) & 1) == 0;
            Sprite[] looks = light ? lightSprites : darkSprites;
            tileData.sprite = Pick(looks, position);
            tileData.color = Color.white;
            tileData.colliderType = Tile.ColliderType.None;
            tileData.flags = TileFlags.LockAll;
        }

        Sprite Pick(Sprite[] looks, Vector3Int position)
        {
            if (looks == null || looks.Length == 0)
                return null;

            int total = 0;
            for (int i = 0; i < looks.Length; i++)
                total += WeightOf(i);
            if (total <= 0)
                return looks[0];

            int roll = (int)(Hash(position.x, position.y) % (uint)total);
            for (int i = 0; i < looks.Length; i++)
            {
                roll -= WeightOf(i);
                if (roll < 0)
                    return looks[i];
            }

            return looks[looks.Length - 1];
        }

        int WeightOf(int index)
        {
            return weights != null && index < weights.Length ? weights[index] : 1;
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
