using TankBrawl.Core;
using TankBrawl.Map;
using UnityEngine;

namespace TankBrawl.Tank
{
    /// <summary>
    /// Bushes hide a tank by its centre. The player's own tank only turns translucent, enemy tanks vanish.
    /// Works through sprite alpha, so it does not clash with the hit blink in <see cref="TankHealth"/>.
    /// </summary>
    [RequireComponent(typeof(TeamMember))]
    public class TankConcealment : MonoBehaviour
    {
        [Tooltip("Opacity of the player's own tank while in a bush.")]
        [SerializeField, Range(0f, 1f)] float ownAlphaInBush = 0.5f;

        SpriteRenderer[] renderers;
        Color[] baseColors;
        TeamMember member;
        BushTilemap bushes;
        bool inBush;

        /// <summary>True when an enemy tank is hidden from the player. Bots use it too.</summary>
        public bool IsConcealed => IsHiddenFrom(Team.Player);

        /// <summary>True when this tank stands in a bush (for any observer).</summary>
        public bool InBush => inBush;

        /// <summary>Opacity the player sees this tank with: 1 outside bushes, translucent for the player's own tank, 0 for enemies.</summary>
        public float VisibilityAlpha => !inBush ? 1f : (member.Team == Team.Player ? ownAlphaInBush : 0f);

        /// <summary>True when this tank stands in a bush and <paramref name="observer"/> belongs to the other team.</summary>
        public bool IsHiddenFrom(Team observer) => inBush && member.Team != observer;

        void Awake()
        {
            member = GetComponent<TeamMember>();
            renderers = TankHealthBar.FindTankRenderers(this);
            baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
                baseColors[i] = renderers[i].color;
        }

        void OnEnable()
        {
            bushes = FindAnyObjectByType<BushTilemap>();
            inBush = false;
            ApplyAlpha(1f);
        }

        void Update()
        {
            bool now = bushes != null && bushes.Covers(transform.position);
            if (now == inBush)
                return;

            inBush = now;
            ApplyAlpha(VisibilityAlpha);
        }

        void ApplyAlpha(float alpha)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                Color c = baseColors[i];
                c.a *= alpha;
                renderers[i].color = c;
            }
        }
    }
}
