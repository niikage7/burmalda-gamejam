using System;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TankBrawl.Map
{
    /// <summary>
    /// Autotile for walls: the sprite follows which of the 8 neighbouring cells hold a tile of the same
    /// <see cref="connectGroup"/>, so a wall gets rough outer edges and seamless inner joins.
    /// The 47-sprite "blob" set is addressed by a neighbour bit mask:
    /// N = 1, NE = 2, E = 4, SE = 8, S = 16, SW = 32, W = 64, NW = 128 (a bit is set when that neighbour is solid).
    /// A corner counts only when both edges next to it are set, which is why the set has 47 sprites, not 256.
    /// </summary>
    [CreateAssetMenu(menuName = "Tank Brawl/Blob Tile", fileName = "BlobTile")]
    public class BlobTile : TileBase
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("Neighbour bit mask (N=1, NE=2, E=4, SE=8, S=16, SW=32, W=64, NW=128).")]
            public int mask;
            public Sprite sprite;
        }

        static readonly Vector3Int[] Offsets =
        {
            new Vector3Int(0, 1, 0), new Vector3Int(1, 1, 0), new Vector3Int(1, 0, 0), new Vector3Int(1, -1, 0),
            new Vector3Int(0, -1, 0), new Vector3Int(-1, -1, 0), new Vector3Int(-1, 0, 0), new Vector3Int(-1, 1, 0)
        };

        const int North = 1, NorthEast = 2, East = 4, SouthEast = 8, South = 16, SouthWest = 32, West = 64, NorthWest = 128;

        [Tooltip("Tiles with the same group join up. Damaged and intact versions of one wall share a group.")]
        [SerializeField] string connectGroup = "wall";

        [SerializeField] Tile.ColliderType colliderType = Tile.ColliderType.Grid;

        [SerializeField] Color color = Color.white;

        [SerializeField] Entry[] entries;

        Sprite[] lookup;

        void OnEnable()
        {
            lookup = null;
        }

        void OnValidate()
        {
            lookup = null;
        }

        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            int mask = 0;
            for (int i = 0; i < Offsets.Length; i++)
            {
                if (ConnectsTo(tilemap.GetTile(position + Offsets[i])))
                    mask |= 1 << i;
            }

            tileData.sprite = Resolve(mask);
            tileData.color = color;
            tileData.colliderType = colliderType;
            tileData.flags = TileFlags.LockAll;
        }

        public override void RefreshTile(Vector3Int position, ITilemap tilemap)
        {
            base.RefreshTile(position, tilemap);
            for (int i = 0; i < Offsets.Length; i++)
                tilemap.RefreshTile(position + Offsets[i]);
        }

        bool ConnectsTo(TileBase other)
        {
            return other is BlobTile blob && blob.connectGroup == connectGroup;
        }

        Sprite Resolve(int mask)
        {
            if (entries == null || entries.Length == 0)
                return null;

            if (lookup == null)
                BuildLookup();

            Sprite sprite = lookup[Canonical(mask)];
            return sprite != null ? sprite : entries[0].sprite;
        }

        void BuildLookup()
        {
            lookup = new Sprite[256];
            for (int i = 0; i < entries.Length; i++)
                lookup[entries[i].mask & 255] = entries[i].sprite;
        }

        // Drop corners whose two neighbouring edges are not both set: they do not change the look.
        static int Canonical(int mask)
        {
            if ((mask & North) == 0 || (mask & East) == 0)
                mask &= ~NorthEast;
            if ((mask & East) == 0 || (mask & South) == 0)
                mask &= ~SouthEast;
            if ((mask & South) == 0 || (mask & West) == 0)
                mask &= ~SouthWest;
            if ((mask & West) == 0 || (mask & North) == 0)
                mask &= ~NorthWest;
            return mask;
        }
    }
}
