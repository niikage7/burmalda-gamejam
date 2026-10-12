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
        TankNitro nitro;
        float currentSpeed;

        // A shove from outside (a ram). This script overwrites linearVelocity every step, so a shove
        // cannot be an impulse on the body: it is added on top of the drive velocity and fades out linearly.
        Vector2 knockbackVelocity;
        float knockbackDuration;
        float knockbackLeft;

        public TankConfig Config => config;
        public float CurrentSpeed => currentSpeed;
        public Vector2 Forward => AngleToDirection(body.rotation);

        /// <summary>The input asked for driving in the last physics step (not just turning in place).</summary>
        public bool IsDriving { get; private set; }

        /// <summary>Shoves the tank with <paramref name="velocity"/> (units/s), fading to zero over <paramref name="duration"/> s. Replaces a shove in progress.</summary>
        public void ApplyKnockback(Vector2 velocity, float duration)
        {
            knockbackVelocity = velocity;
            knockbackDuration = Mathf.Max(duration, Mathf.Epsilon);
            knockbackLeft = duration;
        }

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            input = GetComponent<ITankInput>();
            TryGetComponent(out nitro);
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
                    targetSpeed = nitro != null && nitro.IsActive ? nitro.Speed : config.moveSpeed;
            }

            IsDriving = targetSpeed > 0f;

            float rate = targetSpeed > currentSpeed ? config.acceleration : config.deceleration;
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, rate * dt);

            Vector2 velocity = AngleToDirection(angle) * currentSpeed;
            if (knockbackLeft > 0f)
            {
                velocity += knockbackVelocity * (knockbackLeft / knockbackDuration);
                knockbackLeft -= dt;
            }

            body.MoveRotation(angle);
            body.angularVelocity = 0f;
            body.linearVelocity = velocity;
        }

        void OnDisable()
        {
            knockbackLeft = 0f;
        }

        static Vector2 AngleToDirection(float angleDeg)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            return new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad));
        }
    }
}
