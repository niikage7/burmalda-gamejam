using System.Collections.Generic;
using TankBrawl.Audio;
using TankBrawl.Core;
using UnityEngine;

namespace TankBrawl.Tank
{
    /// <summary>
    /// The ram: while nitro is on and the tank is fast enough, hitting something with the front of the hull
    /// breaks bricks and hurts and shoves an enemy tank. Steel and water are not <see cref="IDamageable"/>, so they are left alone.
    /// Damage goes through <see cref="TankHealth.TakeDamage"/> (respects its invulnerability window); the shove goes
    /// through <see cref="TankController.ApplyKnockback"/>. Never rams the own team.
    /// A tank takes one ram per contact: pinning an enemy against a wall hurts it once, not every cooldown.
    /// The tanks have to come apart (the shove usually does it) before the next ram can land.
    /// </summary>
    [RequireComponent(typeof(TankController), typeof(TankNitro), typeof(TeamMember))]
    public class TankRam : MonoBehaviour
    {
        // How far past the contact point a brick hit is reported, so it lands inside the brick (same as the projectile's).
        const float SurfacePenetration = 0.02f;

        // Contact callbacks come every physics step while two tanks touch; a longer gap means they came apart.
        const float ContactGap = 0.1f;

        struct Contact
        {
            public float LastSeen;
            public bool Struck;
        }

        readonly Dictionary<TankHealth, float> nextHitAt = new Dictionary<TankHealth, float>();
        readonly Dictionary<TankHealth, Contact> contacts = new Dictionary<TankHealth, Contact>();

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
            contacts.Clear();
        }

        void OnCollisionEnter2D(Collision2D collision)
        {
            TryRam(collision);
        }

        // A contact that began without the ram conditions (nitro off, too slow) can still turn into a ram while it lasts.
        void OnCollisionStay2D(Collision2D collision)
        {
            TryRam(collision);
        }

        void TryRam(Collision2D collision)
        {
            // The contact is tracked before the ram conditions, so a long push is recognised as one contact.
            TankHealth victim = collision.collider.GetComponentInParent<TankHealth>();
            bool alreadyStruck = victim != null && TrackContact(victim);

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

            if (victim != null)
            {
                if (!alreadyStruck)
                    RamTank(config, victim, contact.point, inward);
            }
            else
            {
                BreakTerrain(config, collision.collider, contact.point, inward);
            }
        }

        // Returns true when this contact with the tank has already taken a ram.
        bool TrackContact(TankHealth victim)
        {
            float now = Time.time;
            contacts.TryGetValue(victim, out Contact contact);
            if (now - contact.LastSeen > ContactGap)
                contact.Struck = false;

            contact.LastSeen = now;
            contacts[victim] = contact;
            return contact.Struck;
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

            Contact contact = contacts[victim];
            contact.Struck = true;
            contacts[victim] = contact;

            victim.TakeDamage(new DamageInfo(config.ramDamage, 0, point, direction, gameObject));
            AudioManager.PlayAt(Sfx.RamHit, point);
            ScreenShake.AddAt(point, 0.35f);

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
