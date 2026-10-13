using TankBrawl.Audio;
using TankBrawl.Core;
using UnityEngine;

namespace TankBrawl.Tank
{
    /// <summary>
    /// Nitro stock and state. Reads <see cref="ITankInput.NitroHeld"/>, so it does not care who drives the tank.
    /// The stock drains only while the button is held and the tank is actually moving (not pressed against a wall);
    /// otherwise it refills.
    /// Once emptied, nitro stays locked until a small minimum has refilled. Gives no invulnerability.
    /// <see cref="TankController"/> reads <see cref="IsActive"/> and <see cref="Speed"/>.
    /// </summary>
    // Runs before TankController, so the controller sees this step's state.
    [DefaultExecutionOrder(-10)]
    [RequireComponent(typeof(TankController))]
    public class TankNitro : MonoBehaviour
    {
        [SerializeField] NitroConfig config;

        TankController tank;
        Rigidbody2D body;
        TankStats stats;
        ITankInput input;
        float stock;

        public NitroConfig Config => config;

        /// <summary>Nitro is switched on (the button is held and there is stock); it boosts the tank whenever it drives.</summary>
        public bool IsActive { get; private set; }

        /// <summary>The stock was emptied and has not refilled to the minimum yet.</summary>
        public bool IsLocked { get; private set; }

        /// <summary>Stock in seconds of boost.</summary>
        public float Current => stock;

        /// <summary>Capacity in seconds of boost: the config value plus the nitro upgrades.</summary>
        public float Max => config != null ? config.capacity + (stats != null ? config.capacityPerLevel * stats.NitroLevel : 0f) : 0f;

        /// <summary>Stock as 0..1, for a future bar.</summary>
        public float Normalized => Max > 0f ? stock / Max : 0f;

        /// <summary>Top speed while <see cref="IsActive"/>, units/s.</summary>
        public float Speed => config != null ? config.speed : 0f;

        /// <summary>Fills the stock to the maximum and lifts the lock (the nitro refill power-up and charging points).</summary>
        public void Refill()
        {
            stock = Max;
            IsLocked = false;
        }

        void Awake()
        {
            tank = GetComponent<TankController>();
            body = GetComponent<Rigidbody2D>();
            input = GetComponent<ITankInput>();
            TryGetComponent(out stats);
            if (!TryGetComponent<NitroTrail>(out _))
                gameObject.AddComponent<NitroTrail>();
            if (input == null)
                Debug.LogError($"{name}: no ITankInput component, nitro will not work.", this);
            if (config == null)
                Debug.LogError($"{name}: NitroConfig is not assigned.", this);
        }

        void OnEnable()
        {
            stock = Max;
            IsActive = false;
            IsLocked = false;
        }

        void OnDisable()
        {
            IsActive = false;
        }

        void FixedUpdate()
        {
            if (config == null)
                return;

            float dt = Time.fixedDeltaTime;
            float capacity = Max;

            if (IsLocked && stock >= config.reactivateStock * capacity)
                IsLocked = false;

            bool wasActive = IsActive;
            IsActive = input != null && input.NitroHeld && !IsLocked && stock > 0f;
            if (IsActive && !wasActive && tank.IsDriving)
                AudioManager.PlayAt(Sfx.NitroStart, transform.position);

            // The body's velocity is what the last physics step left after collisions: near zero against a wall.
            bool moving = body == null || body.linearVelocity.sqrMagnitude >= config.minDrainSpeed * config.minDrainSpeed;
            if (IsActive && tank.IsDriving && moving)
            {
                stock = Mathf.Max(0f, stock - dt);
                if (stock <= 0f)
                    IsLocked = true;
            }
            else
            {
                stock = Mathf.Min(capacity, stock + config.regenPerSecond * capacity * dt);
            }
        }
    }
}
