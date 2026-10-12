using System.Collections.Generic;
using TankBrawl.Core;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TankBrawl.Map
{
    /// <summary>
    /// Destructible bricks. Sits on the same object as the tilemap's collider, so projectiles find it through the
    /// collider they hit. A destroyed brick comes back after <see cref="restoreDelay"/>, blinking for the last
    /// <see cref="warningDuration"/> seconds; while a tank stands on the cell it waits.
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

        [SerializeField] TileBase brickTile;

        [Tooltip("Durability of a brick. Balance v0.2: 2")]
        [SerializeField, Min(1)] int brickDurability = 2;

        [Tooltip("Tint of a brick that is one hit away from breaking.")]
        [SerializeField] Color damagedColor = new Color(0.6f, 0.5f, 0.5f, 1f);

        [Header("Restoring")]
        [Tooltip("Seconds until a destroyed brick comes back. Balance v0.2: 5")]
        [SerializeField, Min(0f)] float restoreDelay = 5f;

        [Tooltip("Blinking before the brick comes back, s. Balance v0.2: 0.6")]
        [SerializeField, Min(0f)] float warningDuration = 0.6f;

        [SerializeField, Min(0.02f)] float warningBlinkInterval = 0.1f;

        [Tooltip("How often a brick blocked by a tank retries, s.")]
        [SerializeField, Min(0.02f)] float retryInterval = 0.25f;

        [Tooltip("Render-only tilemap (child) that shows the blinking brick. Optional.")]
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
            if (tilemap.GetTile(cell) != brickTile)
                return;

            damageTaken.TryGetValue(cell, out int taken);
            taken += info.BlockDamage;

            if (taken >= brickDurability)
            {
                damageTaken.Remove(cell);
                tilemap.SetTile(cell, null);
                RebuildCollider();
                pending.Add(new PendingBrick { Cell = cell, RestoreAt = Time.time + restoreDelay });
                return;
            }

            damageTaken[cell] = taken;
            tilemap.SetTileFlags(cell, TileFlags.None);
            tilemap.SetColor(cell, Color.Lerp(Color.white, damagedColor, (float)taken / (brickDurability - 1)));
        }

        bool IsBlocked(Vector3Int cell)
        {
            Vector3 size = tilemap.cellSize;
            return Physics2D.OverlapBox(tilemap.GetCellCenterWorld(cell), size, 0f, blockers) != null;
        }

        void ShowWarning(ref PendingBrick brick)
        {
            if (warningTilemap == null)
                return;

            bool on = (int)(Time.time / warningBlinkInterval) % 2 == 0;
            if (brick.WarningShown && brick.BlinkOn == on)
                return;

            if (!brick.WarningShown)
                warningTilemap.SetTile(brick.Cell, brickTile);

            brick.WarningShown = true;
            brick.BlinkOn = on;
            warningTilemap.SetTileFlags(brick.Cell, TileFlags.None);
            warningTilemap.SetColor(brick.Cell, on ? Color.white : Color.clear);
        }

        void Restore(Vector3Int cell)
        {
            if (warningTilemap != null)
                warningTilemap.SetTile(cell, null);

            tilemap.SetTile(cell, brickTile);
            tilemap.SetTileFlags(cell, TileFlags.None);
            tilemap.SetColor(cell, Color.white);
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
