using System.Collections.Generic;
using TankBrawl.Core;
using UnityEngine;

namespace TankBrawl.Tank
{
    /// <summary>
    /// The ram: while nitro is on and the tank is fast enough, hitting something with the front of the hull
    /// breaks bricks and hurts and shoves an enemy tank. Steel and water are not <see cref="IDamageable"/>, so they are left alone.
    /// Damage goes through <see cref="TankHealth.TakeDamage"/> (respects its invulnerability window); the shove goes
    /// through <see cref="TankController.ApplyKnockback"/>. Never rams the own team.
    /// </summary>
    [RequireComponent(typeof(TankController), typeof(TankNitro), typeof(TeamMember))]
    public class TankRam : MonoBehaviour
    {
        // How far past the contact point a brick hit is reported, so it lands inside the brick (same as the projectile's).
        const float SurfacePenetration = 0.02f;

        readonly Dictionary<TankHealth, float> nextHitAt = new Dictionary<TankHealth, float>();

        TankController tank;
        TankNitro nitro;
        TeamMember member;

        void Awake()
        {
            tank = GetComponent<TankController>();
            nitro = GetComponent<TankNitro>();
            member = GetComponent<TeamMember>();
        }

        void OnDisable()
        {
            nextHitAt.Clear();
        }

        void OnCollisionEnter2D(Collision2D collision)
        {
            TryRam(collision);
        }

        // Pushing against something on nitro keeps hitting it, limited by the cooldown (tanks) or by the brick breaking at once.
        void OnCollisionStay2D(Collision2D collision)
        {
            TryRam(collision);
        }

        void TryRam(Collision2D collision)
        {
            NitroConfig config = nitro.Config;
            if (config == null || !nitro.IsActive || tank.CurrentSpeed < config.ramMinSpeed || collision.contactCount == 0)
                return;

            ContactPoint2D contact = collision.GetContact(0);
            Vector2 toContact = contact.point - (Vector2)transform.position;
            if (toContact.sqrMagnitude < Mathf.Epsilon)
                return;

            // For a round hull the direction to the contact point is the contact normal pointing into the obstacle.
            Vector2 inward = toContact.normalized;
            if (Vector2.Angle(tank.Forward, inward) > config.ramMaxAngle)
                return;

            Collider2D other = collision.collider;
            TankHealth victim = other.GetComponentInParent<TankHealth>();
            if (victim != null)
                RamTank(config, victim, contact.point, inward);
            else
                BreakTerrain(config, other, contact.point, inward);
        }

        void RamTank(NitroConfig config, TankHealth victim, Vector2 point, Vector2 direction)
        {
            TeamMember victimTeam = victim.GetComponent<TeamMember>();
            if (victimTeam != null && victimTeam.Team == member.Team)
                return;

            // An ignored hit must not spend the cooldown or give a free shove.
            if (victim.IsDead || victim.IsInvulnerable)
                return;

            float now = Time.time;
            if (nextHitAt.TryGetValue(victim, out float readyAt) && now < readyAt)
                return;
            nextHitAt[victim] = now + config.ramCooldown;

            victim.TakeDamage(new DamageInfo(config.ramDamage, 0, point, direction, gameObject));

            if (victim.gameObject.activeInHierarchy && victim.TryGetComponent(out TankController victimTank))
                victimTank.ApplyKnockback(direction * config.knockbackSpeed, config.knockbackDuration);
        }

        // Terrain takes no HP from a ram (Amount 0), only durability, so a future base is not one-shot by it.
        void BreakTerrain(NitroConfig config, Collider2D other, Vector2 point, Vector2 direction)
        {
            IDamageable terrain = other.GetComponentInParent<IDamageable>();
            if (terrain == null || terrain.Kind != DamageableKind.Terrain)
                return;

            Vector2 centre = point + direction * SurfacePenetration;
            Vector2 side = new Vector2(-direction.y, direction.x) * config.brickBreakHalfWidth;
            terrain.TakeDamage(new DamageInfo(0f, config.ramBlockDamage, centre, direction, gameObject));
            terrain.TakeDamage(new DamageInfo(0f, config.ramBlockDamage, centre + side, direction, gameObject));
            terrain.TakeDamage(new DamageInfo(0f, config.ramBlockDamage, centre - side, direction, gameObject));
        }
    }
}
