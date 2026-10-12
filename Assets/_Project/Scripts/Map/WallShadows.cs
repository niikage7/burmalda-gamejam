using System;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TankBrawl.Map
{
    /// <summary>
    /// Soft drop shadow under walls. One low-resolution texture covers the whole map: the occupied cells of the
    /// caster tilemaps are blurred and shifted a little, then drawn below the walls. Redrawn when a brick breaks or comes back.
    /// One texture (not a sprite per block) so neighbouring blocks do not stack their shadows into stripes.
    /// Assumes all caster tilemaps share one Grid.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class WallShadows : MonoBehaviour
    {
        [Tooltip("Tilemaps whose tiles cast a shadow (bricks, steel). Not the border.")]
        [SerializeField] Tilemap[] casters;

        [Tooltip("Texture resolution, pixels per unit. Low is fine: the shadow is soft and gets bilinear filtering.")]
        [SerializeField, Range(4, 32)] int pixelsPerUnit = 12;

        [Tooltip("How far the shadow spreads, units.")]
        [SerializeField, Min(0f)] float blurRadius = 0.3f;

        [Tooltip("Shift of the shadow, units. Down and to the right looks like light from the upper left.")]
        [SerializeField] Vector2 offset = new Vector2(0.08f, -0.1f);

        [SerializeField] Color shadowColor = Color.black;

        [Tooltip("Darkest alpha of the shadow.")]
        [SerializeField, Range(0f, 1f)] float opacity = 0.4f;

        [Tooltip("Free cells around the walls, so the blur is not cut off at the edge.")]
        [SerializeField, Min(1)] int marginCells = 2;

        SpriteRenderer spriteRenderer;
        Texture2D texture;
        Sprite sprite;
        Color32[] pixels;
        float[] mask;
        float[] blurBuffer;
        float[] prefix;
        int[] columnCell;
        int[] rowCell;
        bool[] occupied;
        bool[] lastOccupied;
        int originX, originY;
        int cellsW, cellsH;
        int texW, texH;
        Vector2 cellSize;
        bool dirty;

        void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        void OnEnable()
        {
            if (Build())
                Refresh(true);
            Tilemap.tilemapTileChanged += OnTilemapTileChanged;
        }

        void OnDisable()
        {
            Tilemap.tilemapTileChanged -= OnTilemapTileChanged;
        }

        void OnDestroy()
        {
            if (sprite != null)
                Destroy(sprite);
            if (texture != null)
                Destroy(texture);
        }

        void LateUpdate()
        {
            if (!dirty)
                return;

            dirty = false;
            Refresh(false);
        }

        void OnTilemapTileChanged(Tilemap map, Tilemap.SyncTile[] tiles)
        {
            if (casters != null && Array.IndexOf(casters, map) >= 0)
                dirty = true;
        }

        bool Build()
        {
            if (casters == null)
                return false;

            Tilemap reference = null;
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            for (int i = 0; i < casters.Length; i++)
            {
                if (casters[i] == null)
                    continue;

                BoundsInt b = casters[i].cellBounds;
                if (b.size.x == 0 || b.size.y == 0)
                    continue;

                if (reference == null)
                    reference = casters[i];
                minX = Mathf.Min(minX, b.xMin);
                minY = Mathf.Min(minY, b.yMin);
                maxX = Mathf.Max(maxX, b.xMax);
                maxY = Mathf.Max(maxY, b.yMax);
            }

            if (reference == null)
                return false;

            Grid grid = reference.layoutGrid;
            cellSize = grid.cellSize;
            originX = minX - marginCells;
            originY = minY - marginCells;
            cellsW = maxX - minX + 2 * marginCells;
            cellsH = maxY - minY + 2 * marginCells;
            texW = Mathf.CeilToInt(cellsW * cellSize.x * pixelsPerUnit);
            texH = Mathf.CeilToInt(cellsH * cellSize.y * pixelsPerUnit);

            columnCell = new int[texW];
            for (int x = 0; x < texW; x++)
                columnCell[x] = Mathf.Min(cellsW - 1, (int)((x + 0.5f) / pixelsPerUnit / cellSize.x));
            rowCell = new int[texH];
            for (int y = 0; y < texH; y++)
                rowCell[y] = Mathf.Min(cellsH - 1, (int)((y + 0.5f) / pixelsPerUnit / cellSize.y));

            occupied = new bool[cellsW * cellsH];
            lastOccupied = new bool[cellsW * cellsH];
            mask = new float[texW * texH];
            blurBuffer = new float[texW * texH];
            prefix = new float[Mathf.Max(texW, texH) + 1];
            pixels = new Color32[texW * texH];

            if (sprite != null)
                Destroy(sprite);
            if (texture != null)
                Destroy(texture);
            texture = new Texture2D(texW, texH, TextureFormat.RGBA32, false)
            {
                name = "WallShadows",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            sprite = Sprite.Create(texture, new Rect(0, 0, texW, texH), Vector2.zero, pixelsPerUnit);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            spriteRenderer.sprite = sprite;
            transform.position = grid.CellToWorld(new Vector3Int(originX, originY, 0));
            return true;
        }

        void Refresh(bool force)
        {
            if (texture == null)
                return;

            for (int y = 0; y < cellsH; y++)
            {
                for (int x = 0; x < cellsW; x++)
                    occupied[x + y * cellsW] = IsCasting(new Vector3Int(originX + x, originY + y, 0));
            }

            if (!force && SameCells())
                return;

            Array.Copy(occupied, lastOccupied, occupied.Length);

            for (int y = 0; y < texH; y++)
            {
                int row = rowCell[y] * cellsW;
                for (int x = 0; x < texW; x++)
                    mask[x + y * texW] = occupied[columnCell[x] + row] ? 1f : 0f;
            }

            int radius = Mathf.RoundToInt(blurRadius * pixelsPerUnit);
            if (radius > 0)
            {
                // Two box passes approximate a gaussian.
                for (int pass = 0; pass < 2; pass++)
                {
                    BlurHorizontal(mask, blurBuffer, radius);
                    BlurVertical(blurBuffer, mask, radius);
                }
            }

            int shiftX = Mathf.RoundToInt(offset.x * pixelsPerUnit);
            int shiftY = Mathf.RoundToInt(offset.y * pixelsPerUnit);
            Color baseColor = shadowColor;
            for (int y = 0; y < texH; y++)
            {
                int sy = y - shiftY;
                for (int x = 0; x < texW; x++)
                {
                    int sx = x - shiftX;
                    float a = sx >= 0 && sy >= 0 && sx < texW && sy < texH ? mask[sx + sy * texW] : 0f;
                    pixels[x + y * texW] = new Color(baseColor.r, baseColor.g, baseColor.b, a * opacity * baseColor.a);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false);
        }

        // True when the cells did not change since the last redraw.
        bool SameCells()
        {
            for (int i = 0; i < occupied.Length; i++)
            {
                if (occupied[i] != lastOccupied[i])
                    return false;
            }

            return true;
        }

        bool IsCasting(Vector3Int cell)
        {
            for (int i = 0; i < casters.Length; i++)
            {
                if (casters[i] != null && casters[i].HasTile(cell))
                    return true;
            }

            return false;
        }

        void BlurHorizontal(float[] src, float[] dst, int radius)
        {
            float norm = 1f / (2 * radius + 1);
            for (int y = 0; y < texH; y++)
            {
                int row = y * texW;
                prefix[0] = 0f;
                for (int x = 0; x < texW; x++)
                    prefix[x + 1] = prefix[x] + src[row + x];
                for (int x = 0; x < texW; x++)
                    dst[row + x] = (prefix[Mathf.Min(x + radius + 1, texW)] - prefix[Mathf.Max(x - radius, 0)]) * norm;
            }
        }

        void BlurVertical(float[] src, float[] dst, int radius)
        {
            float norm = 1f / (2 * radius + 1);
            for (int x = 0; x < texW; x++)
            {
                prefix[0] = 0f;
                for (int y = 0; y < texH; y++)
                    prefix[y + 1] = prefix[y] + src[x + y * texW];
                for (int y = 0; y < texH; y++)
                    dst[x + y * texW] = (prefix[Mathf.Min(y + radius + 1, texH)] - prefix[Mathf.Max(y - radius, 0)]) * norm;
            }
        }
    }
}
