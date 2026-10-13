using System;
using TankBrawl.Audio;
using TankBrawl.Tank;
using TankBrawl.Weapons;
using UnityEngine;

namespace TankBrawl.PowerUps
{
    /// <summary>
    /// The effects that are on a tank right now. Pick-ups call <see cref="Apply"/>; a repeated pick-up of the same kind
    /// refills its timer instead of adding to it. Effects of different kinds work together.
    /// The effects themselves live in the tank's own components: <see cref="TankWeapon"/> (fire rate, projectile type,
    /// muting the gun), <see cref="TankLaser"/>, <see cref="TankShield"/> and <see cref="TankController.SpeedMultiplier"/>.
    /// </summary>
    [RequireComponent(typeof(TankWeapon), typeof(TankController), typeof(TankShield))]
    public class TankPowerUps : MonoBehaviour
    {
        static readonly int KindCount = Enum.GetValues(typeof(PowerUpKind)).Length;

        TankWeapon weapon;
        TankController tank;
        TankShield shield;
        TankLaser laser; // optional: a tank without the laser component just ignores the laser power-up

        // Indexed by PowerUpKind. The shield keeps its own timer (it can also end by absorbing a hit).
        PowerUpConfig[] configs;
        float[] endTimes;
        float[] appliedAt;

        /// <summary>Raised when an effect starts or ends.</summary>
        public event Action Changed;

        public bool IsActive(PowerUpKind kind)
        {
            int i = (int)kind;
            if (configs == null || configs[i] == null)
                return false;

            return kind == PowerUpKind.Shield ? shield.IsActive : Time.time < endTimes[i];
        }

        /// <summary>
        /// For the HUD: the effect picked up last among those still on. Returns false when there is none.
        /// </summary>
        public bool TryGetLatest(out string effectName, out float secondsLeft)
        {
            effectName = null;
            secondsLeft = 0f;
            if (configs == null)
                return false;

            int latest = -1;
            for (int i = 0; i < KindCount; i++)
            {
                if (IsActive((PowerUpKind)i) && (latest < 0 || appliedAt[i] > appliedAt[latest]))
                    latest = i;
            }

            if (latest < 0)
                return false;

            effectName = configs[latest].displayName;
            secondsLeft = latest == (int)PowerUpKind.Shield ? shield.TimeLeft : endTimes[latest] - Time.time;
            return true;
        }

        public void Apply(PowerUpConfig config)
        {
            if (config == null)
                return;

            int i = (int)config.kind;
            configs[i] = config;
            endTimes[i] = Time.time + config.duration;
            appliedAt[i] = Time.time;

            if (config.kind == PowerUpKind.Shield)
            {
                shield.Activate(config.duration, config.shieldBlinkTime);
                AudioManager.PlayAt(Sfx.ShieldUp, transform.position);
            }
            else if (config.kind == PowerUpKind.Laser)
            {
                AudioManager.PlayAt(Sfx.LaserStart, transform.position);
            }

#if UNITY_EDITOR
            Debug.Log($"{name}: power-up {config.displayName} for {config.duration} s", this);
#endif

            Refresh();
        }

        void Awake()
        {
            weapon = GetComponent<TankWeapon>();
            tank = GetComponent<TankController>();
            shield = GetComponent<TankShield>();
            TryGetComponent(out laser);

            configs = new PowerUpConfig[KindCount];
            endTimes = new float[KindCount];
            appliedAt = new float[KindCount];
        }

        void OnDisable()
        {
            // The components the effects act on reset themselves when the tank is deactivated.
            Array.Clear(configs, 0, configs.Length);
        }

        void Update()
        {
            bool ended = false;
            for (int i = 0; i < KindCount; i++)
            {
                if (configs[i] == null || IsActive((PowerUpKind)i))
                    continue;

                configs[i] = null;
                ended = true;
            }

            if (ended)
                Refresh();
        }

        void Refresh()
        {
            PowerUpConfig rapid = IsActive(PowerUpKind.RapidFire) ? configs[(int)PowerUpKind.RapidFire] : null;
            PowerUpConfig explosive = IsActive(PowerUpKind.ExplosiveShells) ? configs[(int)PowerUpKind.ExplosiveShells] : null;
            PowerUpConfig beam = IsActive(PowerUpKind.Laser) ? configs[(int)PowerUpKind.Laser] : null;

            weapon.FireIntervalScale = rapid != null ? rapid.fireIntervalScale : 1f;
            weapon.SetOverride(explosive != null ? explosive.explosiveWeapon : null);
            weapon.Suppressed = beam != null;
            tank.SpeedMultiplier = beam != null ? beam.laserSpeedMultiplier : 1f;

            if (laser != null)
            {
                if (beam != null)
                    laser.Begin(beam.laserDamage, beam.laserInterval, beam.laserRange, beam.laserBeamRadius);
                else if (laser.IsOn)
                    laser.End();
            }

            Changed?.Invoke();
        }
    }
}
