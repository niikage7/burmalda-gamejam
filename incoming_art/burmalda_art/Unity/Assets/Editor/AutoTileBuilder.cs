// Собирает Rule Tile из 47-тайловых blob-наборов и Tile-ассеты травы.
// Меню: Tools → Tiles → Build Rule Tiles. Можно запускать повторно — ассеты обновляются на месте.
//
// Наборы ищутся по именам файлов в любой папке проекта:
//   brick_blob_NNN, brick_damaged_blob_NNN, steel_NNN, water_NNN, bush_NNN (NNN — маска соседей),
//   grass_light_N, grass_dark_N, grass_background_128.
// Rule Tile создаётся рядом с картинками: <папка>/<Имя>_RuleTile.asset.
// Целый и повреждённый кирпич создаются как SiblingRuleTile и связываются друг с другом.
// Нужен пакет 2D Tilemap Extras (com.unity.2d.tilemap.extras).
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class AutoTileBuilder
{
    // 64 px на клетку = 1 юнит (так же у танков: pixelsPerUnit 64 в layout.json).
    const float PixelsPerUnit = 64f;

    // Значения RuleTile.TilingRuleOutput.Neighbor: This = 1, NotThis = 2.
    const int This = 1, NotThis = 2;
    // Биты соседей в имени файла.
    const int N = 1, NE = 2, E = 4, SE = 8, S = 16, SW = 32, W = 64, NW = 128;

    class SetDef
    {
        public string Prefix, AssetName;
        public Tile.ColliderType Collider;
        public bool BrickGroup;
        public SetDef(string prefix, string assetName, Tile.ColliderType collider, bool brickGroup)
        { Prefix = prefix; AssetName = assetName; Collider = collider; BrickGroup = brickGroup; }
    }

    // Grid — квадратный коллайдер на клетку: предсказуемый рикошет, ровное слияние через CompositeCollider2D.
    static readonly SetDef[] Sets =
    {
        new SetDef("brick_blob_",         "Brick",        Tile.ColliderType.Grid, true),
        new SetDef("brick_damaged_blob_", "BrickDamaged", Tile.ColliderType.Grid, true),
        new SetDef("steel_",              "Steel",        Tile.ColliderType.Grid, false),
        new SetDef("water_",              "Water",        Tile.ColliderType.Grid, false),
        new SetDef("bush_",               "Bush",         Tile.ColliderType.None, false),
    };

    static readonly Regex GrassTile = new Regex(@"^grass_(light|dark)_\d$");
    const string GrassBackground = "grass_background_128";

    [MenuItem("Tools/Tiles/Build Rule Tiles")]
    public static void Build()
    {
        // (папка, набор) -> (маска -> путь к картинке)
        var groups = new Dictionary<(string folder, int set), SortedDictionary<int, string>>();
        var grass = new List<string>();

        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path);
            string folder = Path.GetDirectoryName(path).Replace('\\', '/');

            if (GrassTile.IsMatch(name) || name == GrassBackground)
            {
                grass.Add(path);
                continue;
            }
            for (int i = 0; i < Sets.Length; i++)
            {
                var m = Regex.Match(name, "^" + Regex.Escape(Sets[i].Prefix) + @"(\d{3})$");
                if (!m.Success) continue;
                var key = (folder, i);
                if (!groups.TryGetValue(key, out var dict)) groups[key] = dict = new SortedDictionary<int, string>();
                dict[int.Parse(m.Groups[1].Value)] = path;
                break;
            }
        }

        var brickTiles = new List<SiblingRuleTile>();
        foreach (var kv in groups)
        {
            var def = Sets[kv.Key.set];
            var tile = BuildRuleTile(kv.Key.folder, def, kv.Value);
            if (tile is SiblingRuleTile sibling) brickTiles.Add(sibling);
        }

        // Целый и повреждённый кирпич — «свои» друг для друга.
        foreach (var t in brickTiles)
        {
            t.siblings = brickTiles.Where(o => o != t).Cast<TileBase>().ToArray();
            EditorUtility.SetDirty(t);
        }

        foreach (string path in grass) BuildGrass(path);

        AssetDatabase.SaveAssets();
        Debug.Log($"[AutoTileBuilder] Готово: Rule Tile — {groups.Count}, кирпичных связано — {brickTiles.Count}, трава — {grass.Count}.");
    }

    static RuleTile BuildRuleTile(string folder, SetDef def, SortedDictionary<int, string> files)
    {
        var sprites = new SortedDictionary<int, Sprite>();
        foreach (var f in files)
        {
            ConfigureImporter(f.Value, TextureWrapMode.Clamp, SpriteMeshType.Tight);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(f.Value);
            if (sprite != null) sprites[f.Key] = sprite;
        }
        if (sprites.Count != 47)
            Debug.LogWarning($"[AutoTileBuilder] {folder}: {def.AssetName} — найдено {sprites.Count} спрайтов из 47.");

        string assetPath = $"{folder}/{def.AssetName}_RuleTile.asset";
        var tile = AssetDatabase.LoadAssetAtPath<RuleTile>(assetPath);
        bool isNew = tile == null;
        if (isNew)
        {
            tile = def.BrickGroup ? ScriptableObject.CreateInstance<SiblingRuleTile>()
                                  : ScriptableObject.CreateInstance<RuleTile>();
        }
        else if (def.BrickGroup && !(tile is SiblingRuleTile))
        {
            Debug.LogWarning($"[AutoTileBuilder] {assetPath} — обычный Rule Tile. Удалите его и запустите сборку снова, " +
                             "чтобы целый и повреждённый кирпич стыковались без контура.");
        }

        sprites.TryGetValue(0, out var single);
        tile.m_DefaultSprite = single;
        tile.m_DefaultColliderType = def.Collider;
        tile.m_TilingRules.Clear();
        int id = 0;
        foreach (var s in sprites)
        {
            var rule = MakeRule(s.Key, s.Value, def.Collider);
            rule.m_Id = id++;
            tile.m_TilingRules.Add(rule);
        }

        if (isNew) AssetDatabase.CreateAsset(tile, assetPath);
        else EditorUtility.SetDirty(tile);
        Debug.Log($"[AutoTileBuilder] {assetPath}: {tile.m_TilingRules.Count} правил.");
        return tile;
    }

    static RuleTile.TilingRule MakeRule(int mask, Sprite sprite, Tile.ColliderType collider)
    {
        var rule = new RuleTile.TilingRule
        {
            m_Sprites = new[] { sprite },
            m_ColliderType = collider,
            m_NeighborPositions = new List<Vector3Int>(),
            m_Neighbors = new List<int>(),
        };

        bool n = (mask & N) != 0, e = (mask & E) != 0, s = (mask & S) != 0, w = (mask & W) != 0;
        AddNeighbor(rule, 0, 1, n); AddNeighbor(rule, 1, 0, e); AddNeighbor(rule, 0, -1, s); AddNeighbor(rule, -1, 0, w);
        // Диагональ проверяется только когда оба соседних кардинальных соседа есть.
        if (n && e) AddNeighbor(rule, 1, 1, (mask & NE) != 0);
        if (s && e) AddNeighbor(rule, 1, -1, (mask & SE) != 0);
        if (s && w) AddNeighbor(rule, -1, -1, (mask & SW) != 0);
        if (n && w) AddNeighbor(rule, -1, 1, (mask & NW) != 0);
        return rule;
    }

    static void AddNeighbor(RuleTile.TilingRule rule, int x, int y, bool same)
    {
        rule.m_NeighborPositions.Add(new Vector3Int(x, y, 0));
        rule.m_Neighbors.Add(same ? This : NotThis);
    }

    static void BuildGrass(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        bool background = name == GrassBackground;
        // Фон одним спрайтом: Draw Mode = Tiled требует Repeat и Full Rect.
        ConfigureImporter(path, background ? TextureWrapMode.Repeat : TextureWrapMode.Clamp,
                          background ? SpriteMeshType.FullRect : SpriteMeshType.Tight);
        if (background) return;

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) return;
        string folder = Path.GetDirectoryName(path).Replace('\\', '/');
        string assetPath = $"{folder}/{name}_Tile.asset";
        var tile = AssetDatabase.LoadAssetAtPath<Tile>(assetPath);
        bool isNew = tile == null;
        if (isNew) tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sprite;
        tile.colliderType = Tile.ColliderType.None;
        if (isNew) AssetDatabase.CreateAsset(tile, assetPath);
        else EditorUtility.SetDirty(tile);
    }

    static void ConfigureImporter(string path, TextureWrapMode wrap, SpriteMeshType mesh)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return;

        var settings = new TextureImporterSettings();
        imp.ReadTextureSettings(settings);
        bool ok = imp.textureType == TextureImporterType.Sprite
                  && imp.spriteImportMode == SpriteImportMode.Single
                  && Mathf.Approximately(imp.spritePixelsPerUnit, PixelsPerUnit)
                  && imp.textureCompression == TextureImporterCompression.Uncompressed
                  && !imp.mipmapEnabled
                  && imp.wrapMode == wrap
                  && imp.alphaIsTransparency
                  && settings.spriteMeshType == mesh;
        if (ok) return;

        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = PixelsPerUnit;
        imp.mipmapEnabled = false;
        imp.wrapMode = wrap;
        imp.filterMode = FilterMode.Bilinear;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.alphaIsTransparency = true;
        imp.ReadTextureSettings(settings);
        settings.spriteMeshType = mesh;
        imp.SetTextureSettings(settings);
        imp.SaveAndReimport();
    }
}
#endif
