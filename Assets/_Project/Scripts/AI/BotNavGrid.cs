using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TankBrawl.AI
{
    /// <summary>
    /// Navigation for bots: A* over the tile map plus path smoothing.
    /// A tank is 2×2 cells, so the graph nodes are cell corners (vertices): a node is free when all four cells
    /// around it are free, and a node is where the tank's centre can stand. This also lets a tank through
    /// corridors exactly two cells wide.
    /// Solid maps (border, steel, water) always block; brick maps block only where a brick stands now.
    /// <see cref="Version"/> grows whenever a brick appears or disappears, so bots know their paths are stale.
    /// Assumes all tilemaps share one Grid and have no offset.
    /// </summary>
    public class BotNavGrid : MonoBehaviour
    {
        struct HeapItem
        {
            public int Node;
            public float Cost;
        }

        const float DiagonalCost = 1.41421356f;

        static readonly int[] StepX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        static readonly int[] StepY = { 0, 0, 1, -1, 1, -1, 1, -1 };

        [Tooltip("Impassable terrain: border, steel, water.")]
        [SerializeField] Tilemap[] solidMaps;

        [Tooltip("Bricks: passable while the cell is empty. Paths are rebuilt when a brick breaks or comes back.")]
        [SerializeField] Tilemap[] brickMaps;

        [Tooltip("Tank collider radius, units. Used to check that a straight segment is free when smoothing a path.")]
        [SerializeField, Min(0.05f)] float agentRadius = 0.45f;

        [Tooltip("Extra path cost next to walls, in steps. Keeps paths off the walls where there is room.")]
        [SerializeField, Min(0f)] float wallPenalty = 0.5f;

        [Tooltip("Sampling step when checking a segment, units.")]
        [SerializeField, Min(0.05f)] float segmentStep = 0.2f;

        [Tooltip("How far (in nodes) to look for a free node when a start or goal point is inside an obstacle.")]
        [SerializeField, Min(0)] int snapRadius = 3;

        bool built;
        int originX, originY;
        int cellsW, cellsH;
        int nodesW, nodesH;
        Vector2 worldOrigin;
        Vector2 cellSize;
        bool[] solidCells;
        bool[] brickCells;

        float[] gScore;
        int[] parent;
        int[] openStamp;
        int[] closedStamp;
        int stamp;
        HeapItem[] heap = new HeapItem[256];
        int heapCount;

        /// <summary>Grows each time a brick appears or disappears.</summary>
        public int Version { get; private set; }

        void OnEnable()
        {
            Rebuild();
            Tilemap.tilemapTileChanged += OnTilemapTileChanged;
        }

        void OnDisable()
        {
            Tilemap.tilemapTileChanged -= OnTilemapTileChanged;
        }

        /// <summary>
        /// Finds a smoothed path for a tank-sized agent. <paramref name="result"/> gets the waypoints
        /// (without the start). Returns false when the goal cannot be reached.
        /// </summary>
        public bool TryFindPath(Vector2 from, Vector2 to, List<Vector2> result)
        {
            result.Clear();
            if (!EnsureBuilt()
                || !TryGetNode(from, out int startX, out int startY)
                || !TryGetNode(to, out int goalX, out int goalY))
                return false;

            int start = NodeId(startX, startY);
            int goal = NodeId(goalX, goalY);
            if (start == goal)
            {
                result.Add(NodeWorld(goalX, goalY));
                return true;
            }

            stamp++;
            heapCount = 0;
            gScore[start] = 0f;
            parent[start] = -1;
            openStamp[start] = stamp;
            Push(start, Heuristic(startX, startY, goalX, goalY));

            bool found = false;
            while (heapCount > 0)
            {
                int node = Pop();
                if (closedStamp[node] == stamp)
                    continue;
                if (node == goal)
                {
                    found = true;
                    break;
                }

                closedStamp[node] = stamp;
                int nx = node % nodesW;
                int ny = node / nodesW;

                for (int d = 0; d < StepX.Length; d++)
                {
                    int mx = nx + StepX[d];
                    int my = ny + StepY[d];
                    if (!IsNodeFree(mx, my))
                        continue;

                    int next = NodeId(mx, my);
                    if (closedStamp[next] == stamp)
                        continue;

                    // A diagonal move sweeps the tank over the corner cells too: both side nodes must be free.
                    bool diagonal = d >= 4;
                    if (diagonal && (!IsNodeFree(nx + StepX[d], ny) || !IsNodeFree(nx, ny + StepY[d])))
                        continue;

                    float step = diagonal ? DiagonalCost : 1f;
                    if (wallPenalty > 0f && IsNearWall(mx, my))
                        step += wallPenalty;

                    float g = gScore[node] + step;
                    if (openStamp[next] == stamp && g >= gScore[next])
                        continue;

                    openStamp[next] = stamp;
                    gScore[next] = g;
                    parent[next] = node;
                    Push(next, g + Heuristic(mx, my, goalX, goalY));
                }
            }

            if (!found)
                return false;

            for (int node = goal; node != start; node = parent[node])
                result.Add(NodeWorld(node % nodesW, node / nodesW));
            result.Reverse();

            Smooth(from, result);
            return true;
        }

        /// <summary>True when a tank can drive the straight segment from <paramref name="a"/> to <paramref name="b"/>.</summary>
        public bool IsSegmentWalkable(Vector2 a, Vector2 b)
        {
            if (!EnsureBuilt())
                return false;

            float length = Vector2.Distance(a, b);
            int steps = Mathf.Max(1, Mathf.CeilToInt(length / segmentStep));
            for (int i = 0; i <= steps; i++)
            {
                if (CircleHitsBlocked(Vector2.Lerp(a, b, (float)i / steps), agentRadius))
                    return false;
            }

            return true;
        }

        bool EnsureBuilt()
        {
            if (!built)
                Rebuild();
            return built;
        }

        void Rebuild()
        {
            built = false;

            Tilemap reference = null;
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            GrowBounds(solidMaps, ref reference, ref minX, ref minY, ref maxX, ref maxY);
            GrowBounds(brickMaps, ref reference, ref minX, ref minY, ref maxX, ref maxY);
            if (reference == null)
            {
                Debug.LogError($"{name}: no tiles in the assigned tilemaps, bots cannot navigate.", this);
                return;
            }

            Grid grid = reference.layoutGrid;
            cellSize = grid.cellSize;
            originX = minX;
            originY = minY;
            cellsW = maxX - minX;
            cellsH = maxY - minY;
            nodesW = cellsW + 1;
            nodesH = cellsH + 1;
            worldOrigin = grid.CellToWorld(new Vector3Int(originX, originY, 0));

            solidCells = new bool[cellsW * cellsH];
            brickCells = new bool[cellsW * cellsH];
            FillCells(solidMaps, solidCells);
            FillCells(brickMaps, brickCells);

            int nodeCount = nodesW * nodesH;
            gScore = new float[nodeCount];
            parent = new int[nodeCount];
            openStamp = new int[nodeCount];
            closedStamp = new int[nodeCount];
            stamp = 0;

            Version++;
            built = true;
        }

        static void GrowBounds(Tilemap[] maps, ref Tilemap reference, ref int minX, ref int minY, ref int maxX, ref int maxY)
        {
            if (maps == null)
                return;

            for (int i = 0; i < maps.Length; i++)
            {
                if (maps[i] == null)
                    continue;

                BoundsInt b = maps[i].cellBounds;
                if (b.size.x == 0 || b.size.y == 0)
                    continue;

                if (reference == null)
                    reference = maps[i];
                minX = Mathf.Min(minX, b.xMin);
                minY = Mathf.Min(minY, b.yMin);
                maxX = Mathf.Max(maxX, b.xMax);
                maxY = Mathf.Max(maxY, b.yMax);
            }
        }

        void FillCells(Tilemap[] maps, bool[] cells)
        {
            if (maps == null)
                return;

            for (int i = 0; i < maps.Length; i++)
            {
                if (maps[i] == null)
                    continue;

                BoundsInt b = maps[i].cellBounds;
                for (int y = b.yMin; y < b.yMax; y++)
                {
                    for (int x = b.xMin; x < b.xMax; x++)
                    {
                        if (maps[i].HasTile(new Vector3Int(x, y, 0)))
                            cells[(x - originX) + (y - originY) * cellsW] = true;
                    }
                }
            }
        }

        void OnTilemapTileChanged(Tilemap map, Tilemap.SyncTile[] tiles)
        {
            if (!built || brickMaps == null || Array.IndexOf(brickMaps, map) < 0)
                return;

            bool changed = false;
            for (int i = 0; i < tiles.Length; i++)
            {
                Vector3Int position = tiles[i].position;
                int cx = position.x - originX;
                int cy = position.y - originY;
                if (cx < 0 || cy < 0 || cx >= cellsW || cy >= cellsH)
                    continue;

                int index = cx + cy * cellsW;
                bool hasBrick = HasBrickAt(position);
                if (brickCells[index] == hasBrick)
                    continue;

                brickCells[index] = hasBrick;
                changed = true;
            }

            // Other changes (a damaged brick getting a tint) do not move any wall.
            if (changed)
                Version++;
        }

        bool HasBrickAt(Vector3Int cell)
        {
            for (int i = 0; i < brickMaps.Length; i++)
            {
                if (brickMaps[i] != null && brickMaps[i].HasTile(cell))
                    return true;
            }

            return false;
        }

        // Cell coordinates are relative to the grid origin. Everything outside the maps counts as a wall.
        bool IsCellBlocked(int cx, int cy)
        {
            if (cx < 0 || cy < 0 || cx >= cellsW || cy >= cellsH)
                return true;

            int index = cx + cy * cellsW;
            return solidCells[index] || brickCells[index];
        }

        // Node (vx, vy) is the lower-left corner of cell (vx, vy): the tank covers the four cells around it.
        bool IsNodeFree(int vx, int vy)
        {
            return !IsCellBlocked(vx - 1, vy - 1) && !IsCellBlocked(vx, vy - 1)
                   && !IsCellBlocked(vx - 1, vy) && !IsCellBlocked(vx, vy);
        }

        bool IsNearWall(int vx, int vy)
        {
            for (int y = vy - 2; y <= vy + 1; y++)
            {
                for (int x = vx - 2; x <= vx + 1; x++)
                {
                    if (IsCellBlocked(x, y))
                        return true;
                }
            }

            return false;
        }

        int NodeId(int vx, int vy)
        {
            return vx + vy * nodesW;
        }

        Vector2 NodeWorld(int vx, int vy)
        {
            return worldOrigin + new Vector2(vx * cellSize.x, vy * cellSize.y);
        }

        // Nearest free node to a world point, searching outwards ring by ring.
        bool TryGetNode(Vector2 point, out int vx, out int vy)
        {
            Vector2 local = point - worldOrigin;
            int centerX = Mathf.RoundToInt(local.x / cellSize.x);
            int centerY = Mathf.RoundToInt(local.y / cellSize.y);

            vx = vy = 0;
            for (int ring = 0; ring <= snapRadius; ring++)
            {
                float best = float.MaxValue;
                bool found = false;
                for (int dy = -ring; dy <= ring; dy++)
                {
                    for (int dx = -ring; dx <= ring; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != ring || !IsNodeFree(centerX + dx, centerY + dy))
                            continue;

                        float sqr = (NodeWorld(centerX + dx, centerY + dy) - point).sqrMagnitude;
                        if (sqr >= best)
                            continue;

                        best = sqr;
                        vx = centerX + dx;
                        vy = centerY + dy;
                        found = true;
                    }
                }

                if (found)
                    return true;
            }

            return false;
        }

        // Octile distance in node steps.
        static float Heuristic(int ax, int ay, int bx, int by)
        {
            int dx = Mathf.Abs(ax - bx);
            int dy = Mathf.Abs(ay - by);
            return dx + dy + (DiagonalCost - 2f) * Mathf.Min(dx, dy);
        }

        // Greedy string pulling: from each anchor jump to the farthest waypoint reachable in a straight tank-sized line.
        void Smooth(Vector2 from, List<Vector2> path)
        {
            Vector2 anchor = from;
            int write = 0;
            int i = 0;
            while (i < path.Count)
            {
                int best = i;
                for (int j = path.Count - 1; j > i; j--)
                {
                    if (IsSegmentWalkable(anchor, path[j]))
                    {
                        best = j;
                        break;
                    }
                }

                anchor = path[best];
                path[write++] = anchor;
                i = best + 1;
            }

            path.RemoveRange(write, path.Count - write);
        }

        // Does a circle of the given radius at `point` overlap any blocked cell?
        bool CircleHitsBlocked(Vector2 point, float radius)
        {
            Vector2 local = point - worldOrigin;
            float cx = local.x / cellSize.x;
            float cy = local.y / cellSize.y;
            float reachX = radius / cellSize.x;
            float reachY = radius / cellSize.y;

            int x0 = Mathf.FloorToInt(cx - reachX);
            int x1 = Mathf.FloorToInt(cx + reachX);
            int y0 = Mathf.FloorToInt(cy - reachY);
            int y1 = Mathf.FloorToInt(cy + reachY);

            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    if (!IsCellBlocked(x, y))
                        continue;

                    float dx = (cx - Mathf.Clamp(cx, x, x + 1)) * cellSize.x;
                    float dy = (cy - Mathf.Clamp(cy, y, y + 1)) * cellSize.y;
                    if (dx * dx + dy * dy < radius * radius)
                        return true;
                }
            }

            return false;
        }

        // Binary min-heap on cost; stale duplicates are skipped by the closed stamp when popped.
        void Push(int node, float cost)
        {
            if (heapCount == heap.Length)
                Array.Resize(ref heap, heap.Length * 2);

            int i = heapCount++;
            while (i > 0)
            {
                int up = (i - 1) / 2;
                if (heap[up].Cost <= cost)
                    break;

                heap[i] = heap[up];
                i = up;
            }

            heap[i] = new HeapItem { Node = node, Cost = cost };
        }

        int Pop()
        {
            int result = heap[0].Node;
            HeapItem last = heap[--heapCount];

            int i = 0;
            while (true)
            {
                int child = i * 2 + 1;
                if (child >= heapCount)
                    break;
                if (child + 1 < heapCount && heap[child + 1].Cost < heap[child].Cost)
                    child++;
                if (heap[child].Cost >= last.Cost)
                    break;

                heap[i] = heap[child];
                i = child;
            }

            if (heapCount > 0)
                heap[i] = last;
            return result;
        }
    }
}
