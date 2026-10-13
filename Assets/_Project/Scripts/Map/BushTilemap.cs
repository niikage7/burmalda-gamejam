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

        /// <summary>
        /// Centre of a bush cell within <paramref name="radius"/> of <paramref name="around"/> and at least
        /// <paramref name="minDistance"/> from <paramref name="from"/>; the nearest to <paramref name="around"/> wins, near ties at random.
        /// Bots use it to check the bushes where an enemy vanished.
        /// </summary>
        public bool TryFindNear(Vector2 around, float radius, Vector2 from, float minDistance, out Vector2 point)
        {
            point = default;
            Vector3Int centre = tilemap.WorldToCell(around);
            int reach = Mathf.CeilToInt(radius / Mathf.Max(tilemap.cellSize.x, 0.01f));
            float best = float.MaxValue;
            for (int dy = -reach; dy <= reach; dy++)
            {
                for (int dx = -reach; dx <= reach; dx++)
                {
                    var cell = new Vector3Int(centre.x + dx, centre.y + dy, 0);
                    if (!tilemap.HasTile(cell))
                        continue;

                    Vector2 candidate = tilemap.GetCellCenterWorld(cell);
                    float score = (candidate - around).sqrMagnitude;
                    if (score > radius * radius || (candidate - from).sqrMagnitude < minDistance * minDistance)
                        continue;

                    score += Random.value;
                    if (score < best)
                    {
                        best = score;
                        point = candidate;
                    }
                }
            }

            return best < float.MaxValue;
        }
    }
}
