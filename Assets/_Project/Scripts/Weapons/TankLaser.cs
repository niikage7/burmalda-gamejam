using System.Collections.Generic;
using TankBrawl.Core;
using TankBrawl.Tank;
using UnityEngine;

namespace TankBrawl.Weapons
{
    /// <summary>
    /// The laser power-up: while on, a beam goes out along the hull, stopped by any wall and never damaging walls.
    /// Every enemy unit in the beam takes damage no more often than once per interval (each unit has its own timer);
    /// the beam passes through units. Water does not stop it (like projectiles). Driven by <c>TankPowerUps</c>.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(TankController), typeof(TeamMember))]
    public class TankLaser : MonoBehaviour
    {
        const int MaxHits = 16; // beam hits kept per frame

        // Scratch buffer, overwritten before every use: nothing to reset between Play Mode runs.
        static readonly RaycastHit2D[] Hits = new RaycastHit2D[MaxHits];

        // Water is excluded, built lazily: layer names cannot be resolved while Unity loads a MonoBehaviour type.
        static ContactFilter2D filter;
        static bool filterReady;

        [Tooltip("Line renderer of the beam, a child object that is switched on while the laser works.")]
        [SerializeField] LineRenderer beam;

        [Tooltip("Where the beam starts, units from the hull centre.")]
        [SerializeField, Min(0f)] float muzzleOffset = 0.5f;

        // The bright core inside the beam: lighter in the middle than at the edges.
        const float CoreWidthShare = 0.38f;
        const float CoreWhiten = 0.85f;

        LineRenderer core;

        readonly Dictionary<IDamageable, float> nextHitAt = new Dictionary<IDamageable, float>();

        Rigidbody2D body;
        TankController tank;
        TeamMember member;
        float damage;
        float interval;
        float range;
        float radius;

        public bool IsOn { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            filterReady = false;
        }

        /// <summary>Turns the beam on (or updates its numbers if it is on already).</summary>
        public void Begin(float hitDamage, float hitInterval, float beamRange, float beamRadius)
        {
            damage = hitDamage;
            interval = hitInterval;
            range = beamRange;
            radius = beamRadius;
            IsOn = true;
            if (beam != null)
                beam.gameObject.SetActive(true);
        }

        public void End()
        {
            IsOn = false;
            nextHitAt.Clear();
            if (beam != null)
                beam.gameObject.SetActive(false);
        }

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            tank = GetComponent<TankController>();
            member = GetComponent<TeamMember>();
            if (beam == null)
                Debug.LogError($"{name}: the laser beam LineRenderer is not assigned.", this);
            else
                BuildCore();
        }

        // A second, thinner and nearly white line on top of the beam.
        void BuildCore()
        {
            var host = new GameObject("Core");
            host.transform.SetParent(beam.transform, false);
            core = host.AddComponent<LineRenderer>();
            core.sharedMaterial = beam.sharedMaterial;
            core.useWorldSpace = beam.useWorldSpace;
            core.positionCount = 2;
            core.numCapVertices = beam.numCapVertices;
            core.alignment = beam.alignment;
            core.sortingLayerID = beam.sortingLayerID;
            core.sortingOrder = beam.sortingOrder + 1;
            core.widthMultiplier = beam.widthMultiplier * CoreWidthShare;
            Color edge = beam.startColor;
            Color light = Color.Lerp(edge, Color.white, CoreWhiten);
            light.a = 1f;
            core.startColor = core.endColor = light;
        }

        void OnEnable()
        {
            if (!filterReady)
            {
                filter = new ContactFilter2D { useTriggers = false, useLayerMask = true, layerMask = ~LayerMask.GetMask("Water") };
                filterReady = true;
            }
        }

        void OnDisable()
        {
            End();
        }

        void Update()
        {
            if (!IsOn)
                return;

            Vector2 origin = body.position;
            Vector2 direction = tank.Forward;

            int count = Physics2D.CircleCast(origin, radius, direction, filter, Hits, range);

            // The nearest thing that is not a unit stops the beam.
            float length = range;
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = Hits[i];
                if (hit.distance < length && !IsOwn(hit.collider) && !TryGetUnit(hit.collider, out _))
                    length = hit.distance;
            }

            float now = Time.time;
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = Hits[i];
                if (hit.distance > length || IsOwn(hit.collider) || !TryGetUnit(hit.collider, out IDamageable unit))
                    continue;

                TeamMember other = hit.collider.GetComponentInParent<TeamMember>();
                if (other != null && other.Team == member.Team)
                    continue;

                if (nextHitAt.TryGetValue(unit, out float readyAt) && now < readyAt)
                    continue;

                // The timer runs even if the target ignores this hit (invulnerability window, shield).
                nextHitAt[unit] = now + interval;
                unit.TakeDamage(new DamageInfo(damage, 0, hit.point, direction, gameObject));
            }

            if (beam != null)
            {
                beam.SetPosition(0, origin + direction * muzzleOffset);
                beam.SetPosition(1, origin + direction * Mathf.Max(length, muzzleOffset));
                if (core != null)
                {
                    core.SetPosition(0, beam.GetPosition(0));
                    core.SetPosition(1, beam.GetPosition(1));
                }
            }
        }

        bool IsOwn(Collider2D hitCollider)
        {
            Rigidbody2D hitBody = hitCollider.attachedRigidbody;
            return hitBody != null && hitBody.gameObject == gameObject;
        }

        static bool TryGetUnit(Collider2D hitCollider, out IDamageable unit)
        {
            unit = hitCollider.GetComponentInParent<IDamageable>();
            return unit != null && unit.Kind == DamageableKind.Unit;
        }
    }
}
