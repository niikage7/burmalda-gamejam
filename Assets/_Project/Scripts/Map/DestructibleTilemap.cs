using System.Collections.Generic;
using TankBrawl.Audio;
using TankBrawl.Core;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TankBrawl.Map
{
    /// <summary>
    /// Destructible bricks, one whole block per cell. Sits on the same object as the tilemap's collider, so projectiles
    /// find it through the collider they hit. A damaged block swaps to the matching tile of <see cref="damagedTiles"/>.
    /// A destroyed block leaves rubble and comes back after <see cref="restoreDelay"/>, blinking between the block and
    /// the rubble for the last <see cref="warningDuration"/> seconds; while a tank stands on the cell it waits.
    /// Looks are never tinted from code: every state is a tile, so a restored block is exactly the default one.
    /// </summary>
    [RequireComponent(typeof(Tilemap))]
    public class DestructibleTilemap : MonoBehaviour, IDamageable
    {
        struct PendingBrick
        {
            public Vector3Int Cell;
            public float RestoreAt;
            public bool WarningShown;
            public bool BlinkOn;
        }

        [Tooltip("Intact block.")]
        [SerializeField] TileBase brickTile;

        [Tooltip("Damaged looks: element i is shown after i + 1 hits. The last one is used for any further hits before the block breaks.")]
        [SerializeField] TileBase[] damagedTiles;

        [Tooltip("Durability of a brick. Balance v0.2: 2")]
        [SerializeField, Min(1)] int brickDurability = 2;

        [Header("Restoring")]
        [Tooltip("Seconds until a destroyed brick comes back. Balance v0.2: 5")]
        [SerializeField, Min(0f)] float restoreDelay = 5f;

        [Tooltip("Blinking before the brick comes back, s. Balance v0.2: 0.6")]
        [SerializeField, Min(0f)] float warningDuration = 0.6f;

        [SerializeField, Min(0.02f)] float warningBlinkInterval = 0.1f;

        [Tooltip("How often a brick blocked by a tank retries, s.")]
        [SerializeField, Min(0.02f)] float retryInterval = 0.25f;

        [Tooltip("Rubble shown in the empty cell while the block is gone. Optional.")]
        [SerializeField] TileBase rubbleTile;

        [Tooltip("Render-only tilemap (child, no collider) that shows the rubble and the blinking brick. Optional.")]
        [SerializeField] Tilemap warningTilemap;

        [Tooltip("Layers whose colliders keep a brick from coming back (tanks).")]
        [SerializeField] LayerMask blockers;

        [Tooltip("Colour of the debris particles when a brick breaks.")]
        [SerializeField] Color brickDebrisColor = new Color(0.72f, 0.42f, 0.28f, 1f);

        // A blast ray that hits a wall is pushed this far past the surface to find the struck cell.
        const float SurfacePenetration = 0.02f;

        // The ray to the nearest point of a cell aims slightly inside it, not at its very edge.
        const float CellInset = 0.9f;

        readonly Dictionary<Vector3Int, int> damageTaken = new Dictionary<Vector3Int, int>();
        readonly List<PendingBrick> pending = new List<PendingBrick>();
        Tilemap tilemap;
        TilemapCollider2D tilemapCollider;
        int wallMask;

        public DamageableKind Kind => DamageableKind.Terrain;

        void Awake()
        {
            tilemap = GetComponent<Tilemap>();
            TryGetComponent(out tilemapCollider);
            wallMask = LayerMask.GetMask("Wall");
            if (brickTile == null)
                Debug.LogError($"{name}: brick tile is not assigned, nothing will break.", this);
        }

        void Update()
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                PendingBrick brick = pending[i];
                float left = brick.RestoreAt - Time.time;
                if (left > warningDuration)
                    continue;

                if (left > 0f || IsBlocked(brick.Cell))
                {
                    if (left <= 0f)
                        brick.RestoreAt = Time.time + retryInterval;

                    ShowWarning(ref brick);
                    pending[i] = brick;
                    continue;
                }

                Restore(brick.Cell);
                pending.RemoveAt(i);
            }
        }

        public void TakeDamage(in DamageInfo info)
        {
            if (info.BlockDamage <= 0 || brickTile == null)
                return;

            if (DamageCell(tilemap.WorldToCell(info.Point), info.BlockDamage))
                RebuildCollider();
        }

        /// <summary>
        /// Damages every brick whose cell touches the circle (blast) and is the first wall on the way from the centre:
        /// steel and the front bricks shield the ones behind them. Water does not shield.
        /// </summary>
        public void DamageArea(Vector2 center, float radius, int blockDamage)
        {
            if (blockDamage <= 0 || brickTile == null)
                return;

            Vector3Int min = tilemap.WorldToCell(center - new Vector2(radius, radius));
            Vector3Int max = tilemap.WorldToCell(center + new Vector2(radius, radius));
            float radiusSqr = radius * radius;
            Vector3 half = tilemap.cellSize * 0.5f;
            bool broke = false;

            for (int x = min.x; x <= max.x; x++)
            {
                for (int y = min.y; y <= max.y; y++)
                {
                    var cell = new Vector3Int(x, y, 0);
                    if (!tilemap.HasTile(cell))
                        continue;

                    // Distance from the blast centre to the nearest point of the cell.
                    Vector3 cellCenter = tilemap.GetCellCenterWorld(cell);
                    float dx = Mathf.Max(Mathf.Abs(center.x - cellCenter.x) - half.x, 0f);
                    float dy = Mathf.Max(Mathf.Abs(center.y - cellCenter.y) - half.y, 0f);
                    if (dx * dx + dy * dy <= radiusSqr && IsExposed(center, cell, cellCenter, half))
                        broke |= DamageCell(cell, blockDamage);
                }
            }

            // One collider rebuild for the whole blast, not one per brick.
            if (broke)
                RebuildCollider();
        }

        // The cell is exposed when the first wall a ray from the centre meets is the cell itself: towards its nearest
        // point or its centre. Bricks broken earlier in the same blast still shield (the collider is rebuilt after the loop).
        bool IsExposed(Vector2 center, Vector3Int cell, Vector2 cellCenter, Vector2 half)
        {
            var nearest = new Vector2(
                Mathf.Clamp(center.x, cellCenter.x - half.x * CellInset, cellCenter.x + half.x * CellInset),
                Mathf.Clamp(center.y, cellCenter.y - half.y * CellInset, cellCenter.y + half.y * CellInset));
            return FirstWallIs(center, nearest, cell) || FirstWallIs(center, cellCenter, cell);
        }

        bool FirstWallIs(Vector2 from, Vector2 to, Vector3Int cell)
        {
            Vector2 offset = to - from;
            float distance = offset.magnitude;
            if (distance < SurfacePenetration)
                return tilemap.WorldToCell(from) == cell;

            Vector2 direction = offset / distance;
            RaycastHit2D hit = Physics2D.Raycast(from, direction, distance + SurfacePenetration, wallMask);
            return hit.collider == null || tilemap.WorldToCell(hit.point + direction * SurfacePenetration) == cell;
        }

        // Returns true when the brick broke, i.e. the collider needs a rebuild.
        bool DamageCell(Vector3Int cell, int blockDamage)
        {
            if (!tilemap.HasTile(cell))
                return false;

            damageTaken.TryGetValue(cell, out int taken);
            taken += blockDamage;

            if (taken >= brickDurability)
            {
                damageTaken.Remove(cell);
                tilemap.SetTile(cell, null);
                if (warningTilemap != null)
                    warningTilemap.SetTile(cell, rubbleTile);
                pending.Add(new PendingBrick { Cell = cell, RestoreAt = Time.time + restoreDelay });

                Vector3 centre = tilemap.GetCellCenterWorld(cell);
                AudioManager.PlayAt(Sfx.BrickBreak, centre);
                Fx.Debris(centre, brickDebrisColor);
                return true;
            }

            damageTaken[cell] = taken;
            if (damagedTiles != null && damagedTiles.Length > 0)
            {
                TileBase look = damagedTiles[Mathf.Min(taken, damagedTiles.Length) - 1];
                if (look != null)
                    tilemap.SetTile(cell, look);
            }

            return false;
        }

        bool IsBlocked(Vector3Int cell)
        {
            Vector3 size = tilemap.cellSize;
            return Physics2D.OverlapBox(tilemap.GetCellCenterWorld(cell), size, 0f, blockers) != null;
        }

        // Blinks by swapping the tile itself (block / rubble), so the blink shows the block's real colours.
        void ShowWarning(ref PendingBrick brick)
        {
            if (warningTilemap == null)
                return;

            bool on = (int)(Time.time / warningBlinkInterval) % 2 == 0;
            if (brick.WarningShown && brick.BlinkOn == on)
                return;

            brick.WarningShown = true;
            brick.BlinkOn = on;
            warningTilemap.SetTile(brick.Cell, on ? brickTile : rubbleTile);
        }

        void Restore(Vector3Int cell)
        {
            if (warningTilemap != null)
                warningTilemap.SetTile(cell, null);

            tilemap.SetTile(cell, brickTile);
            RebuildCollider();
            AudioManager.PlayAt(Sfx.BrickRestore, tilemap.GetCellCenterWorld(cell));
        }

        /// <summary>Puts every brick back at full durability at once (a new round starts). Call before the tanks are placed.</summary>
        public void ResetAll()
        {
            foreach (Vector3Int cell in damageTaken.Keys)
                tilemap.SetTile(cell, brickTile);
            damageTaken.Clear();

            for (int i = 0; i < pending.Count; i++)
            {
                if (warningTilemap != null)
                    warningTilemap.SetTile(pending[i].Cell, null);
                tilemap.SetTile(pending[i].Cell, brickTile);
            }

            pending.Clear();
            RebuildCollider();
        }

        // Rebuild the collider now, not at the end of the frame: the next projectile must not hit a removed brick.
        void RebuildCollider()
        {
            if (tilemapCollider != null)
                tilemapCollider.ProcessTilemapChanges();
        }
    }
}
