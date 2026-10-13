using System;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Rule Tile, который считает «своими» соседями не только себя, но и тайлы из списка siblings.
/// Нужен для кирпича: целый и повреждённый кирпич стыкуются без контура между ними,
/// поэтому повреждённый можно ставить в любую клетку стены (Tilemap.SetTile).
/// Требует пакет 2D Tilemap Extras (com.unity.2d.tilemap.extras).
/// </summary>
[CreateAssetMenu(fileName = "SiblingRuleTile", menuName = "2D/Tiles/Sibling Rule Tile")]
public class SiblingRuleTile : RuleTile
{
    [Tooltip("Тайлы, которые для правил соседства считаются тем же тайлом (This).")]
    public TileBase[] siblings = Array.Empty<TileBase>();

    public override bool RuleMatch(int neighbor, TileBase other)
    {
        bool same = other == this || (other != null && siblings != null && Array.IndexOf(siblings, other) >= 0);
        switch (neighbor)
        {
            case TilingRuleOutput.Neighbor.This: return same;
            case TilingRuleOutput.Neighbor.NotThis: return !same;
        }
        return base.RuleMatch(neighbor, other);
    }
}
