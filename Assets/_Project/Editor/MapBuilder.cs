using System.Collections.Generic;
using System.IO;
using System.Text;
using TankBrawl.Core;
using TankBrawl.Input;
using TankBrawl.Map;
using TankBrawl.Match;
using TankBrawl.PowerUps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace TankBrawl.EditorTools
{
    /// <summary>
    /// Builds arena scenes from the text layouts in <c>Assets/_Project/Editor/MapLayouts</c> (legend inside each file).
    /// Every map is a copy of <c>Scenes/Arena.unity</c> (same manager, tanks, spawner, camera, lighting) with its tilemaps
    /// repainted and the spawn points, power-up points, flag zone and base pads moved to the layout's markers;
    /// nitro charging points are created from the 'n' markers.
    /// The scenes land in <c>Scenes/Maps</c> and are added to the Build Settings; the menu lists them in
    /// <c>MenuScreens.maps</c>. Menu: TankBrawl / Maps.
    /// </summary>
    public static class MapBuilder
    {
        const string LayoutFolder = "Assets/_Project/Editor/MapLayouts";
        const string TemplateScene = "Assets/_Project/Scenes/Arena.unity";
        const string OutputParent = "Assets/_Project/Scenes";
        const string OutputFolderName = "Maps";
        const string OutputFolder = OutputParent + "/" + OutputFolderName;

        // The border is this many cells thick around the layout (as on the template arena).
        const int BorderThickness = 2;

        // Spawn points face the zone, snapped to this step, deg.
        const float FacingStep = 45f;

        // Fallbacks when a tilemap of the template has no tile to copy.
        const string GroundTilePath = "Assets/_Project/Art/Tiles/GroundCheckerTile.asset";
        const string BorderTilePath = "Assets/_Project/Art/Placeholders/Tiles/BorderTile.asset";
        const string SteelTilePath = "Assets/_Project/Art/Tiles/SteelBlobTile.asset";
        const string BrickTilePath = "Assets/_Project/Art/Tiles/BrickBlobTile.asset";
        const string WaterTilePath = "Assets/_Project/Art/Tiles/WaterBlobTile.asset";
        const string BushTilePath = "Assets/_Project/Art/Tiles/BushBlobTile.asset";

        sealed class MapLayout
        {
            public string File;
            public string Id;
            public string Name;
            public float ZoneRadius = 3f;
            public int Width;
            public int Height;
            public string[] Rows;

            public string ScenePath => OutputFolder + "/" + Id + ".unity";

            public char At(int col, int row) => Rows[row][col];

            // Column 0 is the west edge, row 0 the north edge; the map is centred on the world origin like the template.
            public Vector3Int Cell(int col, int row) => new Vector3Int(col - Width / 2, Height - 1 - row - Height / 2, 0);

            public List<Vector3Int> CellsOf(char marker)
            {
                var cells = new List<Vector3Int>();
                for (int row = 0; row < Height; row++)
                {
                    for (int col = 0; col < Width; col++)
                    {
                        if (At(col, row) == marker)
                            cells.Add(Cell(col, row));
                    }
                }

                return cells;
            }
        }

        [MenuItem("TankBrawl/Maps/Build New Maps")]
        public static void BuildNew()
        {
            Run(false);
        }

        [MenuItem("TankBrawl/Maps/Rebuild All Maps (overwrite)")]
        public static void RebuildAll()
        {
            if (!EditorUtility.DisplayDialog("Rebuild all maps",
                    "Every scene in " + OutputFolder + " that has a layout will be recreated from it. Changes made to those scenes by hand will be lost.",
                    "Rebuild", "Cancel"))
                return;

            Run(true);
        }

        static void Run(bool overwrite)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            if (!File.Exists(TemplateScene))
            {
                Debug.LogError($"MapBuilder: template scene {TemplateScene} not found.");
                return;
            }

            string previousScene = SceneManager.GetActiveScene().path;

            // An empty scene first: a map that is about to be overwritten must not be the open one.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!AssetDatabase.IsValidFolder(OutputFolder))
                AssetDatabase.CreateFolder(OutputParent, OutputFolderName);

            var built = new List<string>();
            try
            {
                string[] files = Directory.GetFiles(LayoutFolder, "*.txt");
                for (int i = 0; i < files.Length; i++)
                {
                    EditorUtility.DisplayProgressBar("Building maps", files[i], (float)i / files.Length);
                    MapLayout layout = Parse(files[i]);
                    if (layout == null)
                        continue;

                    if (!overwrite && File.Exists(layout.ScenePath))
                    {
                        Debug.Log($"MapBuilder: {layout.ScenePath} exists, skipped (use Rebuild All Maps to overwrite).");
                        AddToBuildSettings(layout.ScenePath);
                        continue;
                    }

                    if (Build(layout))
                        built.Add(layout.ScenePath);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            if (!string.IsNullOrEmpty(previousScene) && File.Exists(previousScene))
                EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);

            Debug.Log(built.Count > 0
                ? "MapBuilder: built " + string.Join(", ", built) + ". They are in the Build Settings and in the menu's map list."
                : "MapBuilder: nothing new to build.");
        }

        // ---- Layout ----

        static MapLayout Parse(string path)
        {
            var layout = new MapLayout { File = path, Id = Path.GetFileNameWithoutExtension(path) };
            var rows = new List<string>();
            foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
            {
                string line = raw.TrimEnd();
                if (line.Length == 0)
                    continue;

                // "# " (with a space) is a comment or a header key; a map row may start with '#' (steel) but has no spaces.
                if (line == "#" || line.StartsWith("# "))
                {
                    int colon = line.IndexOf(':');
                    if (colon < 0)
                        continue;

                    string key = line.Substring(1, colon - 1).Trim().ToLowerInvariant();
                    string value = line.Substring(colon + 1).Trim();
                    if (key == "name")
                        layout.Name = value;
                    else if (key == "id")
                        layout.Id = value;
                    else if (key == "zone" && float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float radius))
                        layout.ZoneRadius = radius;
                    continue;
                }

                rows.Add(line);
            }

            layout.Rows = rows.ToArray();
            layout.Height = rows.Count;
            layout.Width = rows.Count > 0 ? rows[0].Length : 0;
            if (string.IsNullOrEmpty(layout.Name))
                layout.Name = layout.Id;

            string problem = Validate(layout);
            if (problem != null)
            {
                Debug.LogError($"MapBuilder: {path}: {problem}. The map is skipped.");
                return null;
            }

            return layout;
        }

        static string Validate(MapLayout layout)
        {
            if (layout.Height == 0)
                return "no rows";

            for (int row = 0; row < layout.Height; row++)
            {
                if (layout.Rows[row].Length != layout.Width)
                    return $"row {row + 1} is {layout.Rows[row].Length} wide, the first row is {layout.Width}";

                foreach (char c in layout.Rows[row])
                {
                    if (".#x~%12fpZn".IndexOf(c) < 0)
                        return $"unknown character '{c}' in row {row + 1}";
                }
            }

            if (layout.CellsOf('1').Count == 0 || layout.CellsOf('2').Count == 0)
                return "needs spawn points of both bases ('1' and '2')";
            if (layout.CellsOf('f').Count == 0)
                return "needs free-for-all spawn points ('f')";
            if (layout.CellsOf('Z').Count == 0)
                return "needs a capture zone marker ('Z')";
            return null;
        }

        // ---- Scene ----

        static bool Build(MapLayout layout)
        {
            if (File.Exists(layout.ScenePath))
                AssetDatabase.DeleteAsset(layout.ScenePath);

            if (!AssetDatabase.CopyAsset(TemplateScene, layout.ScenePath))
            {
                Debug.LogError($"MapBuilder: could not copy {TemplateScene} to {layout.ScenePath}.");
                return false;
            }

            Scene scene = EditorSceneManager.OpenScene(layout.ScenePath, OpenSceneMode.Single);
            if (!PaintTiles(scene, layout))
                return false;

            Vector2 zone = PlaceZone(scene, layout);
            PlaceSpawnPoints(scene, layout, zone);
            PlacePowerUpPoints(scene, layout);
            PlaceNitroStations(scene, layout);
            PlaceBasePads(scene, layout);
            PlaceTanksAndCamera(scene, zone);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AddToBuildSettings(layout.ScenePath);
            Debug.Log($"MapBuilder: {layout.Name} ({layout.Width}x{layout.Height}) -> {layout.ScenePath}");
            return true;
        }

        static bool PaintTiles(Scene scene, MapLayout layout)
        {
            Transform grid = FindRoot(scene, "Grid");
            Tilemap ground = FindTilemap(grid, "Ground");
            Tilemap border = FindTilemap(grid, "Border");
            Tilemap steel = FindTilemap(grid, "Steel");
            Tilemap bricks = FindTilemap(grid, "Bricks");
            Tilemap warning = FindTilemap(grid, "BricksWarning");
            Tilemap water = FindTilemap(grid, "Water");
            Tilemap bushes = FindTilemap(grid, "Bushes");
            if (ground == null || border == null || steel == null || bricks == null || water == null || bushes == null)
            {
                Debug.LogError($"MapBuilder: the template has no Grid with Ground, Border, Steel, Bricks, Water and Bushes tilemaps; {layout.Id} is not built.");
                return false;
            }

            TileBase groundTile = TemplateTile(ground, GroundTilePath);
            TileBase borderTile = TemplateTile(border, BorderTilePath);
            TileBase steelTile = TemplateTile(steel, SteelTilePath);
            TileBase brickTile = BrickTile(bricks);
            TileBase waterTile = TemplateTile(water, WaterTilePath);
            TileBase bushTile = TemplateTile(bushes, BushTilePath);

            foreach (Tilemap map in new[] { ground, border, steel, bricks, warning, water, bushes })
            {
                if (map != null)
                    map.ClearAllTiles();
            }

            var groundCells = new List<Vector3Int>();
            var borderCells = new List<Vector3Int>();
            Vector3Int min = layout.Cell(0, layout.Height - 1);
            Vector3Int max = layout.Cell(layout.Width - 1, 0);
            for (int y = min.y - BorderThickness; y <= max.y + BorderThickness; y++)
            {
                for (int x = min.x - BorderThickness; x <= max.x + BorderThickness; x++)
                {
                    var cell = new Vector3Int(x, y, 0);
                    groundCells.Add(cell);
                    if (x < min.x || x > max.x || y < min.y || y > max.y)
                        borderCells.Add(cell);
                }
            }

            Paint(ground, groundCells, groundTile);
            Paint(border, borderCells, borderTile);
            Paint(steel, layout.CellsOf('#'), steelTile);
            Paint(bricks, layout.CellsOf('x'), brickTile);
            Paint(water, layout.CellsOf('~'), waterTile);
            Paint(bushes, layout.CellsOf('%'), bushTile);

            // Tight bounds matter: the bots' grid, the wall shadows and the power-up interval read cellBounds.
            foreach (Tilemap map in new[] { ground, border, steel, bricks, warning, water, bushes })
            {
                if (map != null)
                    map.CompressBounds();
            }

            return true;
        }

        static void Paint(Tilemap map, List<Vector3Int> cells, TileBase tile)
        {
            if (cells.Count == 0)
                return;

            if (tile == null)
            {
                Debug.LogWarning($"MapBuilder: no tile for {map.name}, {cells.Count} cells left empty.");
                return;
            }

            var tiles = new TileBase[cells.Count];
            for (int i = 0; i < tiles.Length; i++)
                tiles[i] = tile;
            map.SetTiles(cells.ToArray(), tiles);
        }

        // The tile the template already uses on this tilemap, so a map built from it looks the same.
        static TileBase TemplateTile(Tilemap map, string fallbackPath)
        {
            foreach (Vector3Int cell in map.cellBounds.allPositionsWithin)
            {
                TileBase tile = map.GetTile(cell);
                if (tile != null)
                    return tile;
            }

            return AssetDatabase.LoadAssetAtPath<TileBase>(fallbackPath);
        }

        // An intact brick: the one the destructible tilemap restores, not a damaged look that may lie in the template.
        static TileBase BrickTile(Tilemap bricks)
        {
            if (bricks.TryGetComponent(out DestructibleTilemap destructible))
            {
                var tile = new SerializedObject(destructible).FindProperty("brickTile").objectReferenceValue as TileBase;
                if (tile != null)
                    return tile;
            }

            return TemplateTile(bricks, BrickTilePath);
        }

        static Vector2 PlaceZone(Scene scene, MapLayout layout)
        {
            Tilemap ground = FindTilemap(FindRoot(scene, "Grid"), "Ground");
            List<Vector3Int> cells = layout.CellsOf('Z');
            Vector2 centre = Vector2.zero;
            foreach (Vector3Int cell in cells)
                centre += (Vector2)ground.GetCellCenterWorld(cell);
            centre /= cells.Count;

            CapturePointZone zone = FindInScene<CapturePointZone>(scene);
            if (zone == null)
            {
                Debug.LogWarning("MapBuilder: no CapturePointZone in the template, the point mode will have no zone.");
                return centre;
            }

            zone.transform.position = new Vector3(centre.x, centre.y, zone.transform.position.z);
            var serialized = new SerializedObject(zone);
            serialized.FindProperty("radius").floatValue = layout.ZoneRadius;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return centre;
        }

        static void PlaceSpawnPoints(Scene scene, MapLayout layout, Vector2 zone)
        {
            Transform parent = FindRoot(scene, "SpawnPoints");
            if (parent == null)
            {
                Debug.LogError("MapBuilder: no SpawnPoints object in the template, tanks will spawn where they stand.");
                return;
            }

            var blue = new List<SpawnPoint>();
            var red = new List<SpawnPoint>();
            var free = new List<SpawnPoint>();
            foreach (SpawnPoint point in parent.GetComponentsInChildren<SpawnPoint>(true))
            {
                if (point.FreeForAll)
                    free.Add(point);
                else if (point.Team == Team.Player)
                    blue.Add(point);
                else if (point.Team == Team.Enemy)
                    red.Add(point);
            }

            Ground ground = new Ground(scene);
            PlaceGroup(blue, layout.CellsOf('1'), parent, "Base_Blue_", Team.Player, false, ground, zone);
            PlaceGroup(red, layout.CellsOf('2'), parent, "Base_Red_", Team.Enemy, false, ground, zone);
            PlaceGroup(free, layout.CellsOf('f'), parent, "Free_", Team.Player, true, ground, zone);
        }

        // Reuses the template's points, adds or removes some so the count matches the layout.
        static void PlaceGroup(List<SpawnPoint> points, List<Vector3Int> cells, Transform parent, string prefix, Team team, bool freeForAll, Ground ground, Vector2 zone)
        {
            points.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            while (points.Count > cells.Count)
            {
                Object.DestroyImmediate(points[points.Count - 1].gameObject);
                points.RemoveAt(points.Count - 1);
            }

            while (points.Count < cells.Count)
            {
                var host = new GameObject(prefix + (points.Count + 1));
                host.transform.SetParent(parent, false);
                SpawnPoint point = host.AddComponent<SpawnPoint>();
                var serialized = new SerializedObject(point);
                serialized.FindProperty("team").intValue = (int)team;
                serialized.FindProperty("freeForAll").boolValue = freeForAll;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                points.Add(point);
            }

            for (int i = 0; i < cells.Count; i++)
            {
                Vector2 position = ground.Centre(cells[i]);
                points[i].name = prefix + (i + 1);
                points[i].transform.SetPositionAndRotation(position, FacingTowards(position, zone));
            }
        }

        static void PlacePowerUpPoints(Scene scene, MapLayout layout)
        {
            Transform parent = FindRoot(scene, "PowerUpPoints");
            PowerUpSpawner spawner = FindInScene<PowerUpSpawner>(scene);
            if (parent == null || spawner == null)
            {
                Debug.LogWarning("MapBuilder: no PowerUpPoints / PowerUpSpawner in the template, power-ups are not placed.");
                return;
            }

            var points = new List<Transform>();
            foreach (Transform child in parent)
                points.Add(child);
            points.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            List<Vector3Int> cells = layout.CellsOf('p');
            while (points.Count > cells.Count)
            {
                Object.DestroyImmediate(points[points.Count - 1].gameObject);
                points.RemoveAt(points.Count - 1);
            }

            while (points.Count < cells.Count)
            {
                var host = new GameObject("P" + (points.Count + 1));
                host.transform.SetParent(parent, false);
                points.Add(host.transform);
            }

            Ground ground = new Ground(scene);
            for (int i = 0; i < cells.Count; i++)
            {
                points[i].name = "P" + (i + 1);
                points[i].position = ground.Centre(cells[i]);
            }

            var serialized = new SerializedObject(spawner);
            SerializedProperty array = serialized.FindProperty("spawnPoints");
            array.arraySize = points.Count;
            for (int i = 0; i < points.Count; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = points[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // Charging points are made fresh every build (the template has none): one object per 'n' marker under NitroStations.
        static void PlaceNitroStations(Scene scene, MapLayout layout)
        {
            Transform parent = FindRoot(scene, "NitroStations");
            if (parent != null)
                Object.DestroyImmediate(parent.gameObject);

            List<Vector3Int> cells = layout.CellsOf('n');
            if (cells.Count == 0)
                return;

            var root = new GameObject("NitroStations");
            SceneManager.MoveGameObjectToScene(root, scene);
            Ground ground = new Ground(scene);
            for (int i = 0; i < cells.Count; i++)
            {
                var host = new GameObject("N" + (i + 1));
                host.transform.SetParent(root.transform, false);
                host.transform.position = ground.Centre(cells[i]);
                host.AddComponent<NitroStation>();
            }
        }

        // A pad sits on the cell nearest to the middle of its base's spawn points.
        static void PlaceBasePads(Scene scene, MapLayout layout)
        {
            Transform pads = FindRoot(scene, "BasePads");
            if (pads == null)
                return;

            Ground ground = new Ground(scene);
            foreach (Transform pad in pads)
            {
                char marker = pad.name.Contains("Blue") ? '1' : pad.name.Contains("Red") ? '2' : '\0';
                List<Vector3Int> cells = marker != '\0' ? layout.CellsOf(marker) : null;
                if (cells == null || cells.Count == 0)
                    continue;

                Vector2 middle = Vector2.zero;
                foreach (Vector3Int cell in cells)
                    middle += ground.Centre(cell);
                middle /= cells.Count;
                Vector2 snapped = ground.Centre(ground.CellAt(middle));
                pad.position = new Vector3(snapped.x, snapped.y, pad.position.z);
            }
        }

        // MatchManager respawns everyone on the points anyway; this keeps the scene tidy and the camera on the player at load.
        static void PlaceTanksAndCamera(Scene scene, Vector2 zone)
        {
            var points = new List<SpawnPoint>();
            foreach (GameObject root in scene.GetRootGameObjects())
                points.AddRange(root.GetComponentsInChildren<SpawnPoint>(true));

            var tanks = new List<TeamMember>();
            foreach (GameObject root in scene.GetRootGameObjects())
                tanks.AddRange(root.GetComponentsInChildren<TeamMember>(true));

            // The human first, so it takes the first point of its base.
            tanks.Sort((a, b) =>
            {
                bool aHuman = a.GetComponent<PlayerTankInput>() != null;
                bool bHuman = b.GetComponent<PlayerTankInput>() != null;
                return aHuman == bHuman ? string.CompareOrdinal(a.name, b.name) : (aHuman ? -1 : 1);
            });

            var used = new Dictionary<Team, int>();
            Transform human = null;
            foreach (TeamMember tank in tanks)
            {
                used.TryGetValue(tank.Team, out int index);
                used[tank.Team] = index + 1;

                List<SpawnPoint> basePoints = points.FindAll(p => !p.FreeForAll && p.Team == tank.Team);
                basePoints.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                if (basePoints.Count == 0)
                    continue;

                Transform point = basePoints[index % basePoints.Count].transform;
                tank.transform.SetPositionAndRotation(point.position, point.rotation);
                if (human == null && tank.GetComponent<PlayerTankInput>() != null)
                    human = tank.transform;
            }

            Camera camera = FindInScene<Camera>(scene);
            if (camera != null)
            {
                Vector2 target = human != null ? (Vector2)human.position : zone;
                camera.transform.position = new Vector3(target.x, target.y, camera.transform.position.z);
            }
        }

        static void AddToBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (EditorBuildSettingsScene entry in scenes)
            {
                if (entry.path == scenePath)
                {
                    entry.enabled = true;
                    EditorBuildSettings.scenes = scenes.ToArray();
                    return;
                }
            }

            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ---- Helpers ----

        static Quaternion FacingTowards(Vector2 from, Vector2 to)
        {
            Vector2 direction = to - from;
            if (direction.sqrMagnitude < 0.01f)
                return Quaternion.identity;

            // Tanks face local +Y.
            float angle = Vector2.SignedAngle(Vector2.up, direction);
            return Quaternion.Euler(0f, 0f, Mathf.Round(angle / FacingStep) * FacingStep);
        }

        static Transform FindRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                    return root.transform;
            }

            return null;
        }

        static Tilemap FindTilemap(Transform grid, string name)
        {
            if (grid == null)
                return null;

            Transform child = grid.Find(name);
            return child != null ? child.GetComponent<Tilemap>() : null;
        }

        static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null)
                    return found;
            }

            return null;
        }

        // World positions of cells, through the arena's own grid (so a grid offset or cell size change is respected).
        sealed class Ground
        {
            readonly Tilemap map;

            public Ground(Scene scene)
            {
                map = FindTilemap(FindRoot(scene, "Grid"), "Ground");
            }

            public Vector2 Centre(Vector3Int cell) => map != null ? (Vector2)map.GetCellCenterWorld(cell) : new Vector2(cell.x + 0.5f, cell.y + 0.5f);

            public Vector3Int CellAt(Vector2 world) => map != null ? map.WorldToCell(world) : new Vector3Int(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y), 0);
        }
    }
}
