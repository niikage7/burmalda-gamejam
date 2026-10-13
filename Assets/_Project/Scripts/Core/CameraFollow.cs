using UnityEngine;

namespace TankBrawl.Core
{
    /// <summary>
    /// Smoothly follows the target, slightly ahead along its facing so the player sees where they shoot.
    /// Adds <see cref="ScreenShake"/> on top without feeding it back into the smoothing.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] Transform target;

        [Tooltip("Smoothing time, s. 0 = rigid follow.")]
        [SerializeField, Min(0f)] float smoothTime = 0.15f;

        [Tooltip("How far ahead of the target along its facing to look, units (1 unit = 1D).")]
        [SerializeField, Min(0f)] float lookAhead = 1f;

        Vector3 velocity;
        Vector3 basePosition;
        bool hasBase;

        public Transform Target
        {
            get => target;
            set => target = value;
        }

        void Start()
        {
            basePosition = transform.position;
            hasBase = true;
            if (target != null)
                basePosition = DesiredPosition();
            transform.position = basePosition;
        }

        /// <summary>Puts the camera on the target at once, without the smoothing (a new round starts).</summary>
        public void Snap()
        {
            if (target == null)
                return;

            basePosition = DesiredPosition();
            velocity = Vector3.zero;
            hasBase = true;
            transform.position = basePosition;
        }

        void LateUpdate()
        {
            if (!hasBase)
            {
                basePosition = transform.position;
                hasBase = true;
            }

            if (target != null)
                basePosition = Vector3.SmoothDamp(basePosition, DesiredPosition(), ref velocity, smoothTime);

            Vector2 shake = ScreenShake.Step(Time.unscaledDeltaTime);
            transform.position = basePosition + new Vector3(shake.x, shake.y, 0f);
        }

        Vector3 DesiredPosition()
        {
            Vector3 desired = target.position + target.up * lookAhead;
            desired.z = basePosition.z;
            return desired;
        }
    }
}
