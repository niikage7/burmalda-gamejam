using TankBrawl.Core;
using TankBrawl.Tank;
using TankBrawl.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TankBrawl.Match
{
    /// <summary>
    /// The in-match interface, built in code by <see cref="MatchManager"/>: the <see cref="ScoreBoard"/> at the top,
    /// big banner and hint in the middle, the human's health bar, gears earned, damage vignette, kill feed,
    /// pause panel and result screen. Sits below the touch controls (order 100) except the pause/result overlays.
    /// Every text comes from the mode as a cached string, so nothing is allocated per frame.
    /// </summary>
    public class MatchHud : MonoBehaviour
    {
        const int HudOrder = 90;
        const float VignetteFade = 1.6f;
        const int VignetteTextureSize = 128;

        // Edge-only red: generated once (placeholder until the art's ui_damage_vignette), dropped on Play Mode start.
        static Sprite vignetteSprite;
        const string SpectatingHint = "Вы наблюдаете за союзником";

        static readonly Color BarInk = new Color(0.07f, 0.08f, 0.13f, 1f);
        static readonly Color BarBack = new Color(0.1f, 0.12f, 0.18f, 0.9f);

        MatchManager match;
        IMatchMode mode;
        RectTransform safe;
        Text banner;
        Text hint;
        Text gearsLabel;
        Text hpText;
        UiShape hpBar;
        Image vignette;

        TankHealth humanHealth;
        int lastKillGears = -1;
        int lastHp = -1;
        float vignetteAlpha;
        PausePanel pausePanel;
        MatchEndScreen endScreen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            vignetteSprite = null;
        }

        public static MatchHud Create(MatchManager owner)
        {
            var root = new GameObject("MatchHud");
            root.transform.SetParent(owner.transform, false);

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = HudOrder;
            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(UiKit.ReferenceWidth, UiKit.ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            var hud = root.AddComponent<MatchHud>();
            hud.match = owner;
            hud.mode = owner.Mode;
            hud.Build(canvas);
            return hud;
        }

        void Build(Canvas canvas)
        {
            UiKit.EnsureEventSystem();
            safe = UiKit.CreateSafeArea(canvas.transform);

            vignette = UiKit.CreateImage(canvas.transform, "Vignette", new Color(0.85f, 0.05f, 0.05f, 0f), VignetteSprite());
            UiKit.Stretch(vignette.rectTransform);
            vignette.transform.SetAsFirstSibling();

            ScoreBoard.Create(safe, match);

            banner = UiStyle.Label(safe, "Banner", string.Empty, 104, Color.white, TextAnchor.MiddleCenter);
            UiKit.Place(banner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(1600f, 260f));

            hint = UiStyle.Label(safe, "Hint", string.Empty, 40, Color.white, TextAnchor.MiddleCenter);
            UiKit.Place(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(1200f, 60f));

            BuildPlayerBars();

            pausePanel = PausePanel.Create(match);
            endScreen = MatchEndScreen.Create(match);

            MatchParticipant human = match.Human;
            if (human != null)
            {
                humanHealth = human.Health;
                humanHealth.Damaged += OnHumanDamaged;
            }
        }

        void BuildPlayerBars()
        {
            // Same plates as the score board: dark outline, the fill inside.
            hpBar = UiShape.Create(safe, "Hp", BarBack, 9f, 4f);
            hpBar.OutlineColor = BarInk;
            hpBar.FillColor = UiKit.Good;
            UiKit.Place(hpBar.rectTransform, new Vector2(0f, 1f), new Vector2(16f, -124f), new Vector2(320f, 36f));
            hpText = UiKit.CreateText(hpBar.transform, "Text", string.Empty, 22, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Stretch(hpText.rectTransform);

            gearsLabel = UiStyle.Label(safe, "Gears", string.Empty, 30, UiStyle.Yellow, TextAnchor.UpperLeft, false);
            UiKit.Place(gearsLabel.rectTransform, new Vector2(0f, 1f), new Vector2(16f, -166f), new Vector2(520f, 40f));
        }

        void OnDestroy()
        {
            if (humanHealth != null)
                humanHealth.Damaged -= OnHumanDamaged;
        }

        void OnHumanDamaged(TankHealth health, float amount, GameObject source)
        {
            // The vignette is clear in the middle, so it can be strong at the edges without hiding the fight.
            vignetteAlpha = Mathf.Min(0.85f, 0.3f + amount / health.Max * 1.2f);
        }

        void LateUpdate()
        {
            if (mode == null)
                return;

            // The result screen has its own headline: the banner and the hint step aside.
            bool finished = match.IsFinished;
            SetText(banner, finished ? string.Empty : mode.BannerText);

            string hintText = finished ? string.Empty : mode.HintText;
            if (hintText.Length == 0 && match.IsSpectating && !match.IsOver)
                hintText = SpectatingHint;
            SetText(hint, hintText);

            UpdateBars();
            UpdateGears();
            UpdateVignette();
        }

        static void SetText(Text label, string value)
        {
            if (label.text != value)
                label.text = value;
        }

        void UpdateBars()
        {
            if (humanHealth == null)
                return;

            float ratio = humanHealth.Max > 0f ? humanHealth.Current / humanHealth.Max : 0f;
            hpBar.FillAmount = Mathf.Clamp01(ratio);
            hpBar.FillColor = Color.Lerp(UiKit.Bad, UiKit.Good, Mathf.Clamp01(ratio * 1.3f));

            int hp = Mathf.CeilToInt(humanHealth.Current);
            if (hp != lastHp)
            {
                lastHp = hp;
                hpText.text = hp + " / " + Mathf.RoundToInt(humanHealth.Max);
            }

        }

        void UpdateGears()
        {
            if (match.KillGears == lastKillGears)
                return;

            lastKillGears = match.KillGears;
            gearsLabel.text = lastKillGears > 0 ? "Шестерёнки: +" + lastKillGears : string.Empty;
        }

        void UpdateVignette()
        {
            if (vignetteAlpha <= 0f)
                return;

            vignetteAlpha = Mathf.Max(0f, vignetteAlpha - VignetteFade * Time.unscaledDeltaTime * 0.5f);
            Color color = vignette.color;
            color.a = vignetteAlpha;
            vignette.color = color;
        }

        // Transparent in the middle, red towards the edges and strongest in the corners; stretched over the whole screen.
        static Sprite VignetteSprite()
        {
            if (vignetteSprite != null)
                return vignetteSprite;

            const int size = VignetteTextureSize;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.25f, Mathf.Sqrt(u * u + v * v)));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            vignetteSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            vignetteSprite.hideFlags = HideFlags.HideAndDontSave;
            return vignetteSprite;
        }
    }
}
