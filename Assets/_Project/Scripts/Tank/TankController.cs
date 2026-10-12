using TankBrawl.Core;
using UnityEngine;

namespace TankBrawl.Tank
{
    /// <summary>
    /// Drives the hull from an <see cref="ITankInput"/>: turns towards the stick direction
    /// and moves only along the hull (the gun always faces where the hull faces).
    /// Sprite convention: the tank faces local +Y.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class TankController : MonoBehaviour
    {
        [SerializeField] TankConfig config;

        Rigidbody2D body;
        ITankInput input;
        float currentSpeed;

        public TankConfig Config => config;
        public float CurrentSpeed => currentSpeed;
        public Vector2 Forward => AngleToDirection(body.rotation);

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            input = GetComponent<ITankInput>();
            if (input == null)
                Debug.LogError($"{name}: no ITankInput component, the tank will not move.", this);
            if (config == null)
                Debug.LogError($"{name}: TankConfig is not assigned.", this);
        }

        void FixedUpdate()
        {
            if (config == null)
                return;

            float dt = Time.fixedDeltaTime;
            Vector2 move = input != null ? input.Move : Vector2.zero;
            float angle = body.rotation;
            float targetSpeed = 0f;

            // The dead zone comes from Input System settings (Default Deadzone Min), so any non-zero value is intentional.
            if (move.sqrMagnitude > 0f)
            {
                float targetAngle = Mathf.Atan2(move.y, move.x) * Mathf.Rad2Deg - 90f;
                angle = Mathf.MoveTowardsAngle(angle, targetAngle, config.rotationSpeed * dt);

                bool aligned = Mathf.Abs(Mathf.DeltaAngle(angle, targetAngle)) <= config.maxDriveAngle;
                if (aligned && move.magnitude >= config.driveThreshold)
                    targetSpeed = config.moveSpeed;
            }

            float rate = targetSpeed > currentSpeed ? config.acceleration : config.deceleration;
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, rate * dt);

            body.MoveRotation(angle);
            body.angularVelocity = 0f;
            body.linearVelocity = AngleToDirection(angle) * currentSpeed;
        }

        static Vector2 AngleToDirection(float angleDeg)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            return new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad));
        }
    }
}
