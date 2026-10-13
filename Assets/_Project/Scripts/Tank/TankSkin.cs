using UnityEngine;

namespace TankBrawl.Tank
{
    /// <summary>
    /// The look of a tank in one colour: the sprite of every part plus the layout numbers that come with the art
    /// (layout.json). All sprites share one canvas and pivot, so every part sits at local (0, 0).
    /// </summary>
    [CreateAssetMenu(menuName = "Tank Brawl/Tank Skin", fileName = "TankSkin")]
    public class TankSkin : ScriptableObject
    {
        public Sprite shadow;
        public Sprite body;
        public Sprite turret;
        public Sprite barrel;

        [Tooltip("Track frames, played in a loop while the track moves.")]
        public Sprite[] trackLeft;

        public Sprite[] trackRight;

        [Tooltip("Distance from the hull centre to each track, units. A turning hull moves one track faster than the other.")]
        [Min(0f)] public float trackHalfSpacing = 0.3125f;

        [Tooltip("World-space shift of the shadow, units. Fixed in the world so the light does not turn with the hull.")]
        public Vector2 shadowOffset = new Vector2(0.05f, -0.07f);

        [Tooltip("Distance from the hull centre to the muzzle along the hull, units. Shots and the laser start here.")]
        [Min(0f)] public float muzzleDistance = 0.6875f;
    }
}
