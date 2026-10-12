using System.Collections.Generic;
using UnityEngine;

namespace TankBrawl.Map
{
    /// <summary>
    /// The flag zone of the point-holding mode: a circle on the map. A tank counts as holding it when its centre is inside.
    /// Scoring belongs to the future match manager; for now bots read the position and radius to go there and stay.
    /// A scene without a zone is a scene without that mode, so bots then ignore the point.
    /// </summary>
    public class CapturePointZone : MonoBehaviour
    {
        static readonly List<CapturePointZone> Active = new List<CapturePointZone>();

        [Tooltip("Radius of the zone, units (1 unit = 1D).")]
        [SerializeField, Min(0.5f)] float radius = 3f;

        /// <summary>The first active zone in the scene, or null. The mode has one flag.</summary>
        public static CapturePointZone Primary => Active.Count > 0 ? Active[0] : null;

        public Vector2 Center => transform.position;

        public float Radius => radius;

        public bool Contains(Vector2 point) => (point - Center).sqrMagnitude <= radius * radius;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Active.Clear();
        }

        void OnEnable()
        {
            Active.Add(this);
        }

        void OnDisable()
        {
            Active.Remove(this);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
            Vector3 centre = transform.position;
            const int segments = 48;
            Vector3 previous = centre + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector3 next = centre + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }
}
