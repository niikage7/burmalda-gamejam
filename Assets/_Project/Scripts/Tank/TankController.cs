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
        TankStats stats;
        float currentSpeed;

        // A shove from outside (a ram). This script overwrites linearVelocity every step, so a shove
        // cannot be an impulse on the body: it is added on top of the drive velocity and fades out linearly.
        Vector2 knockbackVelocity;
        float knockbackDuration;
        float knockbackLeft;

        public TankConfig Config => config;

        /// <summary>Current driving speed along the hull, units/s. Does not include a knockback.</summary>
        public float CurrentSpeed => currentSpeed;

        /// <summary>Unit vector the hull (and so the gun) faces, in world space.</summary>
        public Vector2 Forward => AngleToDirection(body.rotation);

        /// <summary>Top speed without nitro and without the speed multiplier: the config speed plus the engine upgrades, units/s.</summary>
        public float BaseSpeed => config.moveSpeed + (stats != null ? config.speedPerLevel * stats.EngineLevel : 0f);

        /// <summary>Scales the drive speed, nitro included (the laser power-up slows the tank). 1 = normal.</summary>
        public float SpeedMultiplier { get; set; } = 1f;

        /// <summary>Scales the hull turn speed. 1 = the config's rotation speed; bots on easier difficulty levels turn slower.</summary>
        public float TurnRateScale { get; set; } = 1f;

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
            TryGetComponent(out stats);
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
                // -90: Atan2 measures from +X, the sprite faces +Y.
                float targetAngle = Mathf.Atan2(move.y, move.x) * Mathf.Rad2Deg - 90f;
                angle = Mathf.MoveTowardsAngle(angle, targetAngle, config.rotationSpeed * TurnRateScale * dt);

                bool aligned = Mathf.Abs(Mathf.DeltaAngle(angle, targetAngle)) <= config.maxDriveAngle;
                if (aligned && move.magnitude >= config.driveThreshold)
                    targetSpeed = (nitro != null && nitro.IsActive ? nitro.Speed : BaseSpeed) * SpeedMultiplier;
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
            currentSpeed = 0f;
            SpeedMultiplier = 1f;
        }

        // Rotation 0 faces +Y, positive angles turn counter-clockwise.
        static Vector2 AngleToDirection(float angleDeg)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            return new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad));
        }
    }
}
