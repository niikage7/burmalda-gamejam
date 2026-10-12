using TankBrawl.Core;
using TankBrawl.Tank;
using UnityEngine;

namespace TankBrawl.Weapons
{
    /// <summary>Fires the tank's projectile along the hull while <see cref="ITankInput.FireHeld"/> is true.</summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(TankController), typeof(TeamMember))]
    public class TankWeapon : MonoBehaviour
    {
        [SerializeField] WeaponConfig weapon;

        Rigidbody2D body;
        TankController tank;
        TeamMember member;
        ITankInput input;
        float nextFireTime;

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
        }

        // Update, not FixedUpdate: FireHeld is polled, so a short tap must not fall between two physics steps.
        void Update()
        {
            if (input == null || weapon == null || weapon.projectilePrefab == null)
                return;
            if (!input.FireHeld || Time.time < nextFireTime)
                return;

            nextFireTime = Time.time + weapon.fireInterval;
            Projectile projectile = Instantiate(weapon.projectilePrefab);
            projectile.Launch(weapon, gameObject, member.Team, body.position, tank.Forward);
        }
    }
}
