using UnityEngine;

namespace TankBrawl.Core
{
    /// <summary>Smoothly follows the target, slightly ahead along its facing so the player sees where they shoot.</summary>
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] Transform target;

        [Tooltip("Smoothing time, s. 0 = rigid follow.")]
        [SerializeField, Min(0f)] float smoothTime = 0.15f;

        [Tooltip("How far ahead of the target along its facing to look, units (1 unit = 1D).")]
        [SerializeField, Min(0f)] float lookAhead = 1f;

        Vector3 velocity;

        /// <summary>What the camera follows. Can be swapped at runtime (e.g. after respawn); null stops the camera.</summary>
        public Transform Target
        {
            get => target;
            set => target = value;
        }

        void Start()
        {
            if (target != null)
                transform.position = DesiredPosition();
        }

        void LateUpdate()
        {
            if (target == null)
                return;

            transform.position = Vector3.SmoothDamp(transform.position, DesiredPosition(), ref velocity, smoothTime);
        }

        Vector3 DesiredPosition()
        {
            Vector3 desired = target.position + target.up * lookAhead;
            desired.z = transform.position.z;
            return desired;
        }
    }
}
