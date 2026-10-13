using UnityEngine;

namespace TankBrawl.Tank
{
    /// <summary>Look of the small health bar under a tank. One asset is shared by all tanks. Units: 1 unit = 1D (hull diameter).</summary>
    [CreateAssetMenu(menuName = "TankBrawl/Health Bar Style", fileName = "HealthBarStyle")]
    public class HealthBarStyle : ScriptableObject
    {
        [Header("Size and place")]
        [Tooltip("Bar length, units.")]
        [Min(0.05f)] public float width = 0.7f;

        [Tooltip("Bar thickness, units.")]
        [Min(0.01f)] public float height = 0.08f;

        [Tooltip("Centre of the bar relative to the tank centre, in world axes (the bar never turns with the hull), units.")]
        public Vector2 offset = new Vector2(0f, -0.72f);

        [Tooltip("Sorting order of the bar (the tank's own sprites use 9-14).")]
        public int sortingOrder = 15;

        [Header("Colours")]
        [Tooltip("Fill at full health.")]
        public Color fullColor = new Color(0.35f, 0.9f, 0.3f, 1f);

        [Tooltip("Fill at the lowest health (blended in linearly).")]
        public Color lowColor = new Color(0.95f, 0.2f, 0.15f, 1f);

        [Tooltip("Dark strip behind the fill.")]
        public Color backColor = new Color(0f, 0f, 0f, 0.55f);

        [Header("Visibility")]
        [Tooltip("Do not draw the bar while the tank is undamaged: it appears with the first hit and stays until death. Keeps the field clean.")]
        public bool hideWhenFull = true;

        [Tooltip("How fast the bar fades in and out, 1/s (alpha per second).")]
        [Min(0.1f)] public float fadeSpeed = 6f;
    }
}
