using UnityEngine;

namespace TankBrawl.Map
{
    /// <summary>
    /// Marker for indestructible terrain (steel) that bounces projectiles. Put it on the object with the collider.
    /// Map borders deliberately do not have it: projectiles die there.
    /// </summary>
    public class RicochetSurface : MonoBehaviour
    {
    }
}
