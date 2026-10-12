using System;
using TankBrawl.Core;
using TankBrawl.Tank;
using UnityEngine;

namespace TankBrawl.Weapons
{
    /// <summary>Fires the tank's projectile along the hull while <see cref="ITankInput.FireHeld"/> is true or a tap was buffered.</summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(TankController), typeof(TeamMember))]
    public class TankWeapon : MonoBehaviour
    {
        [SerializeField] WeaponConfig weapon;
        [Tooltip("How long a fire tap made during reload is remembered, seconds.")]
        [SerializeField, Min(0f)] float fireBufferTime = 0.2f;

        Rigidbody2D body;
        TankController tank;
        TeamMember member;
        ITankInput input;
        float nextFireTime;
        float bufferedFireUntil;

        public WeaponConfig Config => weapon;

        /// <summary>Raised right after a shot is fired.</summary>
        public event Action Fired;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            tank = GetComponent<TankController>();
            member = GetComponent<TeamMember>();
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
        }

        // Update, not FixedUpdate: FireHeld is polled, so a short tap must not fall between two physics steps.
        void Update()
        {
            if (input == null || weapon == null || weapon.projectilePrefab == null)
                return;

            // A tap shorter than a frame is seen only by FirePressed; a tap during reload is remembered for a moment.
            if (input.FirePressed)
                bufferedFireUntil = Time.time + fireBufferTime;
            bool wantsFire = input.FireHeld || Time.time <= bufferedFireUntil;
            if (!wantsFire || Time.time < nextFireTime)
                return;

            bufferedFireUntil = 0f;
            nextFireTime = Time.time + weapon.fireInterval;
            Projectile projectile = Instantiate(weapon.projectilePrefab);
            projectile.Launch(weapon, gameObject, member.Team, body.position, tank.Forward);
            Fired?.Invoke();
        }
    }
}
