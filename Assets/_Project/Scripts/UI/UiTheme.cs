using UnityEngine;

namespace TankBrawl.UI
{
    /// <summary>
    /// Optional art for the code-built screens. Every sprite may stay empty: the screen then draws a plain coloured rectangle.
    /// Drop the real sprites in when they are drawn; no code changes. Loaded from Resources by <see cref="UiKit"/>.
    /// 9-sliced sprites (panel, button) need borders set in their import settings.
    /// </summary>
    [CreateAssetMenu(menuName = "TankBrawl/UI Theme", fileName = "UiTheme")]
    public class UiTheme : ScriptableObject
    {
        [Header("Frames (9-slice)")]
        public Sprite panel;
        public Sprite button;
        public Sprite bar;

        [Header("Icons")]
        public Sprite gear;
        public Sprite killIcon;
        public Sprite lockIcon;

        [Tooltip("Cards of the mode picker, in the order of MatchModeKind: Team, Point, Deathmatch.")]
        public Sprite[] modeIcons = new Sprite[3];

        [Header("Screens")]
        public Sprite menuBackground;
        public Sprite logo;
        public Sprite victoryBanner;
        public Sprite defeatBanner;

        [Header("Game name")]
        public string gameName = "TANK BRAWL";
    }
}
