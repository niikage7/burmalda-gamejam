using TankBrawl.Audio;
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
        TankStats stats;
        ITankInput input;
        float stock;

        public NitroConfig Config => config;

        /// <summary>Nitro boosts the tank right now.</summary>
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

        void Awake()
        {
            tank = GetComponent<TankController>();
            input = GetComponent<ITankInput>();
            TryGetComponent(out stats);
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

            if (IsActive && tank.IsDriving)
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
