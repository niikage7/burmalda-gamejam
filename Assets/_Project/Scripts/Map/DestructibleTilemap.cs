using System.Collections.Generic;
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

        readonly Dictionary<Vector3Int, int> damageTaken = new Dictionary<Vector3Int, int>();
        readonly List<PendingBrick> pending = new List<PendingBrick>();
        Tilemap tilemap;
        TilemapCollider2D tilemapCollider;

        public DamageableKind Kind => DamageableKind.Terrain;

        void Awake()
        {
            tilemap = GetComponent<Tilemap>();
            TryGetComponent(out tilemapCollider);
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

            Vector3Int cell = tilemap.WorldToCell(info.Point);
            if (!tilemap.HasTile(cell))
                return;

            damageTaken.TryGetValue(cell, out int taken);
            taken += info.BlockDamage;

            if (taken >= brickDurability)
            {
                damageTaken.Remove(cell);
                tilemap.SetTile(cell, null);
                RebuildCollider();
                if (warningTilemap != null)
                    warningTilemap.SetTile(cell, rubbleTile);
                pending.Add(new PendingBrick { Cell = cell, RestoreAt = Time.time + restoreDelay });
                return;
            }

            damageTaken[cell] = taken;
            if (damagedTiles != null && damagedTiles.Length > 0)
            {
                TileBase look = damagedTiles[Mathf.Min(taken, damagedTiles.Length) - 1];
                if (look != null)
                    tilemap.SetTile(cell, look);
            }
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
        }

        // Rebuild the collider now, not at the end of the frame: the next projectile must not hit a removed brick.
        void RebuildCollider()
        {
            if (tilemapCollider != null)
                tilemapCollider.ProcessTilemapChanges();
        }
    }
}
