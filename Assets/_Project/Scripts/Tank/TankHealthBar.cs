using UnityEngine;

namespace TankBrawl.Tank
{
    /// <summary>
    /// A thin health bar under a tank, without numbers. Sits on a child object of the tank, keeps the world rotation
    /// (does not turn with the hull) and follows the tank at a fixed offset. An enemy tank hidden in a bush shows no bar
    /// (it would give its position away); the player's own tank in a bush fades together with the tank.
    /// The two sprites (<see cref="back"/>, <see cref="fill"/>) are children of this object; their sprite is set here at runtime.
    /// <see cref="TankHealth"/> and <see cref="TankConcealment"/> skip renderers under this component.
    /// </summary>
    public class TankHealthBar : MonoBehaviour
    {
        [SerializeField] HealthBarStyle style;
        [SerializeField] SpriteRenderer back;
        [SerializeField] SpriteRenderer fill;

        // One shared 1x1 white sprite with its pivot on the left edge, so scaling X shrinks the bar towards the left.
        static Sprite barSprite;

        TankHealth health;
        TankConcealment concealment;
        Transform owner;
        float alpha;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            barSprite = null;
        }

        /// <summary>The tank's sprite renderers without the bar's own (the bar handles its alpha and visibility itself).</summary>
        public static SpriteRenderer[] FindTankRenderers(Component tankRoot)
        {
            SpriteRenderer[] all = tankRoot.GetComponentsInChildren<SpriteRenderer>();
            int kept = 0;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].GetComponentInParent<TankHealthBar>() == null)
                    all[kept++] = all[i];
            }

            System.Array.Resize(ref all, kept);
            return all;
        }

        static Sprite BarSprite()
        {
            // A runtime texture can be unloaded together with unused assets, so check for a destroyed one.
            if (barSprite == null)
            {
                var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Point
                };
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                barSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0f, 0.5f), 1f);
                barSprite.hideFlags = HideFlags.HideAndDontSave;
            }

            return barSprite;
        }

        void Awake()
        {
            health = GetComponentInParent<TankHealth>();
            concealment = GetComponentInParent<TankConcealment>();
            owner = health != null ? health.transform : transform.parent;

            if (style == null || back == null || fill == null || health == null)
            {
                Debug.LogError($"{name}: health bar is not set up (style, back, fill or TankHealth is missing).", this);
                enabled = false;
                return;
            }

            Sprite sprite = BarSprite();
            back.sprite = sprite;
            fill.sprite = sprite;
            back.sortingOrder = style.sortingOrder;
            fill.sortingOrder = style.sortingOrder + 1;
        }

        void OnEnable()
        {
            alpha = style != null && style.hideWhenFull ? 0f : 1f;
            Apply();
        }

        void LateUpdate()
        {
            Apply();
        }

        void Apply()
        {
            if (style == null)
                return;

            transform.SetPositionAndRotation(owner.position + (Vector3)style.offset, Quaternion.identity);

            float ratio = health.Max > 0f ? Mathf.Clamp01(health.Current / health.Max) : 0f;

            float wanted = style.hideWhenFull && ratio >= 1f ? 0f : 1f;
            alpha = Mathf.MoveTowards(alpha, wanted, style.fadeSpeed * Time.deltaTime);

            float visibility = alpha * (concealment != null ? concealment.VisibilityAlpha : 1f);
            bool shown = visibility > 0.001f;
            if (back.enabled != shown)
            {
                back.enabled = shown;
                fill.enabled = shown;
            }

            if (!shown)
                return;

            float left = -style.width * 0.5f;
            back.transform.localPosition = new Vector3(left, 0f, 0f);
            back.transform.localScale = new Vector3(style.width, style.height, 1f);
            fill.transform.localPosition = new Vector3(left, 0f, 0f);
            fill.transform.localScale = new Vector3(style.width * ratio, style.height, 1f);

            Color backColor = style.backColor;
            backColor.a *= visibility;
            back.color = backColor;

            Color fillColor = Color.Lerp(style.lowColor, style.fullColor, ratio);
            fillColor.a *= visibility;
            fill.color = fillColor;
        }
    }
}
