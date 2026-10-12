using TankBrawl.Core;
using UnityEngine;

namespace TankBrawl.Tank
{
    /// <summary>
    /// Nitro stock and state. Reads <see cref="ITankInput.NitroHeld"/>, so it does not care who drives the tank.
    /// The stock drains only while the button is held and the tank is actually driving; otherwise it refills.
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
        ITankInput input;
        float stock;

        public NitroConfig Config => config;

        /// <summary>Nitro boosts the tank right now.</summary>
        public bool IsActive { get; private set; }

        /// <summary>The stock was emptied and has not refilled to the minimum yet.</summary>
        public bool IsLocked { get; private set; }

        /// <summary>Stock in seconds of boost.</summary>
        public float Current => stock;

        public float Max => config != null ? config.capacity : 0f;

        /// <summary>Stock as 0..1, for a future bar.</summary>
        public float Normalized => Max > 0f ? stock / Max : 0f;

        /// <summary>Top speed while <see cref="IsActive"/>, units/s.</summary>
        public float Speed => config != null ? config.speed : 0f;

        void Awake()
        {
            tank = GetComponent<TankController>();
            input = GetComponent<ITankInput>();
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

            if (IsLocked && stock >= config.reactivateStock * config.capacity)
                IsLocked = false;

            IsActive = input != null && input.NitroHeld && !IsLocked && stock > 0f;

            if (IsActive && tank.IsDriving)
            {
                stock = Mathf.Max(0f, stock - dt);
                if (stock <= 0f)
                    IsLocked = true;
            }
            else
            {
                stock = Mathf.Min(config.capacity, stock + config.regenPerSecond * config.capacity * dt);
            }
        }
    }
}
