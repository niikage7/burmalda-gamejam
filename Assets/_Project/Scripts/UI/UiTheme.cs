using System;
using UnityEngine;

namespace TankBrawl.UI
{
    /// <summary>
    /// Art of the code-built screens. Every sprite may stay empty: the screen then draws a flat shape or leaves the spot blank.
    /// Drop the real sprites in when they are drawn; no code changes. Loaded from Resources by <see cref="UiKit"/>.
    /// 9-sliced sprites (panel, button) need borders set in their import settings.
    /// </summary>
    [CreateAssetMenu(menuName = "TankBrawl/UI Theme", fileName = "UiTheme")]
    public class UiTheme : ScriptableObject
    {
        [Serializable]
        public struct MapPreview
        {
            [Tooltip("Scene name of the map, as in the Build Settings.")]
            public string sceneName;
            public Sprite sprite;
        }

        [Header("Frames (9-slice, old style, not used by the new screens)")]
        public Sprite panel;
        public Sprite button;
        public Sprite bar;

        [Header("Fonts of the menu style")]
        [Tooltip("Titles, buttons, numbers: the rounded bold one (Lilita One, Nunito Black for the glyphs it lacks).")]
        public Font displayFont;

        [Tooltip("Small running text on cream plates.")]
        public Font bodyFont;

        [Header("Icons")]
        public Sprite gear;
        public Sprite gearGray;
        public Sprite killIcon;
        public Sprite lockIcon;
        public Sprite backIcon;
        public Sprite closeIcon;
        public Sprite settingsIcon;
        public Sprite pauseIcon;

        [Tooltip("Cards of the mode picker, in the order of MatchModeKind: Team, Point, Deathmatch.")]
        public Sprite[] modeIcons = new Sprite[3];

        [Header("Screens")]
        public Sprite menuBackground;
        public Sprite logo;
        public Sprite victoryBanner;
        public Sprite defeatBanner;

        [Tooltip("Seamless grass for the menu background (repeat wrap).")]
        public Texture2D grassTile;

        [Tooltip("The tank on the podium and in the lists, in the order of Team: blue, red, green, yellow.")]
        public Sprite[] tankPreviews = new Sprite[4];

        [Tooltip("Pictures of the maps for the mode screen and the lobby.")]
        public MapPreview[] mapPreviews;

        [Header("Game name")]
        public string gameName = "TANK BRAWL";

        public Sprite PreviewOf(string sceneName)
        {
            if (mapPreviews == null)
                return null;

            for (int i = 0; i < mapPreviews.Length; i++)
            {
                if (mapPreviews[i].sceneName == sceneName)
                    return mapPreviews[i].sprite;
            }

            return null;
        }
    }
}
