using UnityEngine;

namespace TankBrawl.Weapons
{
    /// <summary>
    /// Placeholder blast visual: a sprite that grows to the blast diameter and fades out, then destroys itself.
    /// The prefab's sprite is expected to be a circle that fills a 1x1 unit square.
    /// </summary>
    public class ExplosionVisual : MonoBehaviour
    {
        [SerializeField] SpriteRenderer sprite;
        [SerializeField, Min(0.05f)] float duration = 0.25f;

        [Tooltip("Share of the final size the blast starts with.")]
        [SerializeField, Range(0f, 1f)] float startScale = 0.3f;

        float diameter;
        float age;
        Color baseColor;

        /// <summary>Starts the blast of the given radius at the current position.</summary>
        public void Play(float radius)
        {
            diameter = radius * 2f;
            age = 0f;
            baseColor = sprite != null ? sprite.color : Color.white;
            Apply(0f);
        }

        void Update()
        {
            age += Time.deltaTime;
            if (age >= duration)
            {
                Destroy(gameObject);
                return;
            }

            Apply(age / duration);
        }

        void Apply(float t)
        {
            float scale = diameter * Mathf.Lerp(startScale, 1f, 1f - (1f - t) * (1f - t));
            transform.localScale = new Vector3(scale, scale, 1f);
            if (sprite != null)
            {
                Color c = baseColor;
                c.a *= 1f - t;
                sprite.color = c;
            }
        }
    }
}
