using System;
using TankBrawl.Audio;
using TankBrawl.Core;
using TankBrawl.Tank;
using UnityEngine;

namespace TankBrawl.Weapons
{
    /// <summary>
    /// Fires the tank's projectile along the hull while <see cref="ITankInput.FireHeld"/> is true or a tap was buffered.
    /// The main projectile is <see cref="weapon"/>; <see cref="extraAmmo"/> are further types that
    /// <see cref="ITankInput.CycleAmmoPressed"/> switches through in a loop. All types share one reload.
    /// Power-ups act through <see cref="SetOverride"/>, <see cref="FireIntervalScale"/> and <see cref="Suppressed"/>.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(TankController), typeof(TeamMember))]
    public class TankWeapon : MonoBehaviour
    {
        [Tooltip("The main projectile type (index 0 of the ammo loop).")]
        [SerializeField] WeaponConfig weapon;

        [Tooltip("Further types the ammo button switches to, in order. Empty = the tank has only the main one.")]
        [SerializeField] WeaponConfig[] extraAmmo;

        [Tooltip("How long a fire tap made during reload is remembered, seconds.")]
        [SerializeField, Min(0f)] float fireBufferTime = 0.2f;

        Rigidbody2D body;
        TankController tank;
        TeamMember member;
        TankStats stats;
        ITankInput input;
        WeaponConfig overrideWeapon;

        // The selected ammo type survives a respawn (OnEnable does not reset it): the button label only updates on AmmoChanged,
        // so a reset here would leave the label showing a type the gun no longer uses. A new match reloads the scene.
        int ammoIndex;
        float nextFireTime;
        float bufferedFireUntil;

        /// <summary>The projectile type that the next shot uses: a power-up override, otherwise the selected ammo.</summary>
        public WeaponConfig Config => overrideWeapon != null ? overrideWeapon : SelectedAmmo;

        /// <summary>The ammo type picked with the ammo button (ignores a power-up override).</summary>
        public WeaponConfig SelectedAmmo => ammoIndex == 0 || extraAmmo == null || ammoIndex > extraAmmo.Length ? weapon : extraAmmo[ammoIndex - 1];

        public int AmmoCount => 1 + (extraAmmo != null ? extraAmmo.Length : 0);

        /// <summary>Weapon upgrade level of this tank (damage, projectile speed); 0 without <see cref="TankStats"/>.</summary>
        public int WeaponLevel => stats != null ? stats.WeaponLevel : 0;

        /// <summary>Fire-rate upgrade level of this tank.</summary>
        public int FireLevel => stats != null ? stats.FireLevel : 0;

        /// <summary>Speed of the next shot, units/s (the config speed plus the weapon upgrades).</summary>
        public float ProjectileSpeed => Config.SpeedAt(WeaponLevel);

        /// <summary>Seconds until the gun can shoot again (0 = ready).</summary>
        public float CooldownLeft => Mathf.Max(0f, nextFireTime - Time.time);

        /// <summary>Multiplier of the time between shots (a power-up sets it below 1).</summary>
        public float FireIntervalScale { get; set; } = 1f;

        /// <summary>While true the tank does not shoot its gun (the laser power-up replaces it).</summary>
        public bool Suppressed { get; set; }

        /// <summary>Raised right after a shot is fired.</summary>
        public event Action Fired;

        /// <summary>Raised when the ammo button switched the projectile type.</summary>
        public event Action AmmoChanged;

        /// <summary>Makes every shot use <paramref name="config"/> (null = back to the selected ammo). Does not touch the reload.</summary>
        public void SetOverride(WeaponConfig config)
        {
            overrideWeapon = config;
        }

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            tank = GetComponent<TankController>();
            member = GetComponent<TeamMember>();
            TryGetComponent(out stats);
            input = GetComponent<ITankInput>();
            if (input == null)
                Debug.LogError($"{name}: no ITankInput component, the tank will not shoot.", this);
            if (weapon == null || weapon.projectilePrefab == null)
                Debug.LogError($"{name}: WeaponConfig or its projectile prefab is not assigned.", this);
        }

        void OnEnable()
        {
            nextFireTime = 0f;
            bufferedFireUntil = 0f;
            overrideWeapon = null;
            FireIntervalScale = 1f;
            Suppressed = false;
        }

        // Update, not FixedUpdate: FireHeld is polled, so a short tap must not fall between two physics steps.
        void Update()
        {
            if (input == null || weapon == null || weapon.projectilePrefab == null)
                return;

            if (input.CycleAmmoPressed && AmmoCount > 1)
            {
                ammoIndex = (ammoIndex + 1) % AmmoCount;
                AudioManager.PlayAt(Sfx.AmmoSwitch, body.position);
                AmmoChanged?.Invoke();
            }

            if (Suppressed)
            {
                bufferedFireUntil = 0f;
                return;
            }

            // A tap shorter than a frame is seen only by FirePressed; a tap during reload is remembered for a moment.
            if (input.FirePressed)
                bufferedFireUntil = Time.time + fireBufferTime;
            bool wantsFire = input.FireHeld || Time.time <= bufferedFireUntil;
            if (!wantsFire || Time.time < nextFireTime)
                return;

            WeaponConfig shot = Config;
            if (shot.projectilePrefab == null)
                return;

            bufferedFireUntil = 0f;
            nextFireTime = Time.time + shot.IntervalAt(FireLevel) * FireIntervalScale;
            Projectile projectile = Instantiate(shot.projectilePrefab);
            projectile.Launch(shot, gameObject, member.Team, body.position, tank.Forward, WeaponLevel);

            Vector2 muzzle = body.position + tank.Forward * shot.muzzleOffset;
            AudioManager.PlayAt(shot.fireSfx, muzzle);
            Fx.MuzzleFlash(muzzle, tank.Forward);
            Fired?.Invoke();
        }
    }
}
