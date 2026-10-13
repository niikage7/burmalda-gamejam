using UnityEngine;

namespace TankBrawl.Core
{
    /// <summary>
    /// Camera shake as "trauma": events add to it, it fades by itself, the shake strength grows with the square of it.
    /// <see cref="CameraFollow"/> calls <see cref="Step"/> every frame. Static, because several unrelated things shake the camera.
    /// </summary>
    public static class ScreenShake
    {
        // Largest offset of the camera at full trauma, units.
        const float MaxOffset = 0.45f;
        const float FadePerSecond = 1.8f;
        const float NoiseSpeed = 28f;

        static float trauma;
        static float seed;

        /// <summary>Master switch for code (tests, cut-scenes). The player's setting is <see cref="Progress.PlayerProgress.ScreenShakeOn"/>; both must be on.</summary>
        public static bool Enabled { get; set; } = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            trauma = 0f;
            Enabled = true;
        }

        /// <summary>Adds <paramref name="amount"/> (0..1) of trauma. A hit on the player ~0.25, a tank explosion ~0.5.</summary>
        public static void Add(float amount)
        {
            if (!Enabled || !Progress.PlayerProgress.ScreenShakeOn)
                return;

            trauma = Mathf.Clamp01(trauma + amount);
            seed = Random.value * 100f;
        }

        /// <summary>Like <see cref="Add"/>, but weaker the farther the event is from the camera (nothing beyond ~20 units).</summary>
        public static void AddAt(Vector2 position, float amount)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                Add(amount);
                return;
            }

            float distance = Vector2.Distance(camera.transform.position, position);
            Add(amount * Mathf.Clamp01(1f - (distance - 6f) / 14f));
        }

        /// <summary>Advances the fade and returns this frame's camera offset. Call once per frame.</summary>
        public static Vector2 Step(float deltaTime)
        {
            if (trauma <= 0f)
                return Vector2.zero;

            trauma = Mathf.Max(0f, trauma - FadePerSecond * deltaTime);
            float strength = trauma * trauma * MaxOffset;
            float t = Time.unscaledTime * NoiseSpeed;
            float x = (Mathf.PerlinNoise(seed, t) - 0.5f) * 2f;
            float y = (Mathf.PerlinNoise(seed + 31.7f, t) - 0.5f) * 2f;
            return new Vector2(x, y) * strength;
        }
    }
}
