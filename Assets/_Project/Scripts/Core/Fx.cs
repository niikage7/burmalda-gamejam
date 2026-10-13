using UnityEngine;

namespace TankBrawl.Core
{
    /// <summary>
    /// Cheap particle effects with no assets: one shared particle system that scenes emit single particles into
    /// (sparks, debris, smoke puffs, muzzle flashes). Placeholders until real effects are drawn; the calls stay.
    /// The system is created on the first use and re-created if a scene load destroyed it.
    /// </summary>
    public static class Fx
    {
        const int MaxParticles = 600;
        const int SortingOrder = 20;

        static ParticleSystem system;
        static ParticleSystem.EmitParams emit;
        static Material material;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            system = null;
            material = null;
        }

        /// <summary>
        /// A burst of <paramref name="count"/> particles flying out from <paramref name="position"/>.
        /// </summary>
        /// <param name="speed">Mean speed, units/s (each particle gets 40-100% of it).</param>
        /// <param name="lifetime">Mean lifetime, s.</param>
        /// <param name="size">Particle size, units.</param>
        /// <param name="direction">Mean direction in degrees; with <paramref name="spread"/> 360 the burst goes everywhere.</param>
        public static void Burst(Vector2 position, Color color, int count, float speed, float lifetime, float size, float direction = 0f, float spread = 360f)
        {
            if (!EnsureSystem())
                return;

            emit.position = position;
            emit.startColor = color;
            for (int i = 0; i < count; i++)
            {
                float angle = (direction + Random.Range(-spread * 0.5f, spread * 0.5f)) * Mathf.Deg2Rad;
                float velocity = speed * Random.Range(0.4f, 1f);
                emit.velocity = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * velocity;
                emit.startLifetime = lifetime * Random.Range(0.6f, 1.2f);
                emit.startSize = size * Random.Range(0.6f, 1.2f);
                system.Emit(emit, 1);
            }
        }

        /// <summary>Sparks off a wall or a tank hit by a shell: <paramref name="normal"/> points away from the surface.</summary>
        public static void Sparks(Vector2 position, Vector2 normal, Color color)
        {
            float angle = Mathf.Atan2(normal.y, normal.x) * Mathf.Rad2Deg;
            Burst(position, color, 7, 5f, 0.3f, 0.09f, angle, 100f);
        }

        /// <summary>Muzzle flash: a few bright particles forward.</summary>
        public static void MuzzleFlash(Vector2 position, Vector2 direction)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Burst(position, new Color(1f, 0.9f, 0.5f, 1f), 5, 4f, 0.15f, 0.14f, angle, 50f);
        }

        /// <summary>Debris of a broken block.</summary>
        public static void Debris(Vector2 position, Color color)
        {
            Burst(position, color, 10, 4f, 0.5f, 0.14f);
        }

        /// <summary>A tank blowing up: flash, fire and smoke.</summary>
        public static void TankExplosion(Vector2 position, Color tankColor)
        {
            Burst(position, new Color(1f, 0.75f, 0.2f, 1f), 24, 6.5f, 0.55f, 0.22f);
            Burst(position, new Color(1f, 0.35f, 0.1f, 1f), 14, 4.5f, 0.7f, 0.3f);
            Burst(position, tankColor, 14, 5.5f, 0.8f, 0.16f);
            Burst(position, new Color(0.25f, 0.25f, 0.25f, 0.8f), 10, 2f, 1.1f, 0.4f);
        }

        /// <summary>Haptic tick on a phone when the player is hurt. Does nothing elsewhere or when vibration is switched off.</summary>
        public static void Haptic()
        {
#if UNITY_ANDROID || UNITY_IOS
            if (Progress.PlayerProgress.Vibration)
                Handheld.Vibrate();
#endif
        }

        static bool EnsureSystem()
        {
            if (system != null)
                return true;

            var host = new GameObject("Fx");
            system = host.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.maxParticles = MaxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;
            main.startSpeed = 0f;

            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;

            // Fade out and shrink over life.
            var colorOverLife = system.colorOverLifetime;
            colorOverLife.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) });
            colorOverLife.color = gradient;

            var sizeOverLife = system.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.2f)));

            var drag = system.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.dampen = 0.08f;
            drag.limit = 100f;

            var renderer = host.GetComponent<ParticleSystemRenderer>();
            if (material == null)
                material = new Material(Shader.Find("Sprites/Default"));
            renderer.sharedMaterial = material;
            renderer.sortingOrder = SortingOrder;

            system.Play();
            return true;
        }
    }
}
