using System.Collections.Generic;
using TankBrawl.Core;
using TankBrawl.Tank;
using TankBrawl.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TankBrawl.Match
{
    /// <summary>
    /// The in-match interface, built in code by <see cref="MatchManager"/>: score strip with title and clock at the top,
    /// big banner and hint in the middle, the human's health and nitro bars, gears earned, damage vignette, kill feed,
    /// pause panel and result screen. Sits below the touch controls (order 100) except the pause/result overlays.
    /// Every text comes from the mode as a cached string, so nothing is allocated per frame.
    /// </summary>
    public class MatchHud : MonoBehaviour
    {
        const int HudOrder = 90;
        const float CellHeight = 92f;
        const float VignetteFade = 1.6f;
        const string SpectatingHint = "Вы наблюдаете за союзником";

        sealed class Cell
        {
            public Image Background;
            public Image Stripe;
            public Image Frame;
            public Text Label;
            public Text Value;
            public Text Detail;
        }

        MatchManager match;
        IMatchMode mode;
        RectTransform safe;
        Text title;
        Text timer;
        Text banner;
        Text hint;
        Text gearsLabel;
        Text hpText;
        Image hpFill;
        Image nitroFill;
        Image vignette;
        readonly List<Cell> cells = new List<Cell>();

        TankHealth humanHealth;
        TankNitro humanNitro;
        int lastScoreVersion = -1;
        int lastKillGears = -1;
        int lastHp = -1;
        float vignetteAlpha;
        PausePanel pausePanel;
        MatchEndScreen endScreen;

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

            vignette = UiKit.CreateImage(canvas.transform, "Vignette", new Color(0.85f, 0.05f, 0.05f, 0f));
            UiKit.Stretch(vignette.rectTransform);
            vignette.transform.SetAsFirstSibling();

            title = UiKit.CreateText(safe, "Title", string.Empty, 26, UiKit.Muted, TextAnchor.UpperCenter, FontStyle.Bold);
            UiKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(900f, 34f));

            timer = UiKit.CreateText(safe, "Timer", string.Empty, 38, UiKit.Accent, TextAnchor.UpperCenter, FontStyle.Bold);
            UiKit.Place(timer.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -146f), new Vector2(300f, 50f));

            banner = UiKit.CreateText(safe, "Banner", string.Empty, 104, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place(banner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(1600f, 260f));

            hint = UiKit.CreateText(safe, "Hint", string.Empty, 40, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(1200f, 60f));

            BuildPlayerBars();

            // Kill feed: top right.
            RectTransform feedRoot = UiKit.CreateRect("KillFeed", safe);
            UiKit.Place(feedRoot, new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(560f, 240f));
            feedRoot.gameObject.AddComponent<KillFeed>().Init(match);

            pausePanel = PausePanel.Create(match);
            endScreen = MatchEndScreen.Create(match);

            MatchParticipant human = match.Human;
            if (human != null)
            {
                humanHealth = human.Health;
                humanHealth.Damaged += OnHumanDamaged;
                human.Object.TryGetComponent(out humanNitro);
            }
        }

        void BuildPlayerBars()
        {
            Image hpBack = UiKit.CreateImage(safe, "HpBack", new Color(0f, 0f, 0f, 0.55f));
            UiKit.Place(hpBack.rectTransform, new Vector2(0f, 1f), new Vector2(16f, -128f), new Vector2(320f, 30f));
            hpFill = UiKit.CreateImage(hpBack.transform, "Fill", Color.green);
            SetupFill(hpFill);
            hpText = UiKit.CreateText(hpBack.transform, "Text", string.Empty, 22, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Stretch(hpText.rectTransform);

            Image nitroBack = UiKit.CreateImage(safe, "NitroBack", new Color(0f, 0f, 0f, 0.55f));
            UiKit.Place(nitroBack.rectTransform, new Vector2(0f, 1f), new Vector2(16f, -164f), new Vector2(320f, 16f));
            nitroFill = UiKit.CreateImage(nitroBack.transform, "Fill", new Color(0.35f, 0.7f, 1f, 1f));
            SetupFill(nitroFill);

            gearsLabel = UiKit.CreateText(safe, "Gears", string.Empty, 28, UiKit.Accent, TextAnchor.UpperLeft, FontStyle.Bold);
            UiKit.Place(gearsLabel.rectTransform, new Vector2(0f, 1f), new Vector2(16f, -190f), new Vector2(520f, 40f));
        }

        // A bar fill is a stretched rectangle whose right anchor is the value (no sprite needed).
        static void SetupFill(Image fill)
        {
            RectTransform rect = fill.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(2f, 2f);
            rect.offsetMax = new Vector2(-2f, -2f);
        }

        static void SetFill(Image fill, float value)
        {
            Vector2 max = fill.rectTransform.anchorMax;
            max.x = Mathf.Clamp01(value);
            fill.rectTransform.anchorMax = max;
        }

        void OnDestroy()
        {
            if (humanHealth != null)
                humanHealth.Damaged -= OnHumanDamaged;
        }

        void OnHumanDamaged(TankHealth health, float amount, GameObject source)
        {
            vignetteAlpha = Mathf.Min(0.55f, 0.18f + amount / health.Max * 0.7f);
        }

        void LateUpdate()
        {
            if (mode == null)
                return;

            if (mode.ScoreVersion != lastScoreVersion)
            {
                lastScoreVersion = mode.ScoreVersion;
                RefreshCells();
            }

            SetText(title, mode.TitleText);
            SetText(timer, mode.TimerText);
            SetText(banner, mode.BannerText);

            string hintText = mode.HintText;
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
            SetFill(hpFill, ratio);
            hpFill.color = Color.Lerp(UiKit.Bad, UiKit.Good, Mathf.Clamp01(ratio * 1.3f));

            int hp = Mathf.CeilToInt(humanHealth.Current);
            if (hp != lastHp)
            {
                lastHp = hp;
                hpText.text = hp + " / " + Mathf.RoundToInt(humanHealth.Max);
            }

            if (humanNitro != null)
                SetFill(nitroFill, humanNitro.Normalized);
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

        // ---- Score strip ----

        void RefreshCells()
        {
            IReadOnlyList<ScoreEntry> entries = mode.Scores;
            if (cells.Count != entries.Count)
                BuildCells(entries.Count);

            for (int i = 0; i < entries.Count; i++)
            {
                ScoreEntry entry = entries[i];
                Cell cell = cells[i];
                Color teamColor = TeamColors.Of(entry.Team);
                cell.Background.color = new Color(teamColor.r * 0.28f, teamColor.g * 0.28f, teamColor.b * 0.28f, 0.88f);
                cell.Stripe.color = teamColor;
                cell.Frame.color = entry.IsLocal ? UiKit.Accent : new Color(0f, 0f, 0f, 0f);
                SetText(cell.Label, entry.Label);
                SetText(cell.Value, entry.Value);
                SetText(cell.Detail, entry.Detail);
            }
        }

        void BuildCells(int count)
        {
            for (int i = 0; i < cells.Count; i++)
                Destroy(cells[i].Frame.gameObject);
            cells.Clear();

            float width = count <= 2 ? 330f : 250f;
            float gap = count <= 2 ? 150f : 12f;
            float total = count * width + (count - 1) * gap;
            float x = -total * 0.5f + width * 0.5f;
            for (int i = 0; i < count; i++)
            {
                cells.Add(BuildCell(x, width));
                x += width + gap;
            }
        }

        Cell BuildCell(float x, float width)
        {
            var cell = new Cell();
            cell.Frame = UiKit.CreateImage(safe, "Cell", Color.clear);
            UiKit.Place(cell.Frame.rectTransform, new Vector2(0.5f, 1f), new Vector2(x, -40f), new Vector2(width + 6f, CellHeight + 6f));

            cell.Background = UiKit.CreateImage(cell.Frame.transform, "Back", Color.black, UiKit.Theme != null ? UiKit.Theme.panel : null);
            UiKit.Stretch(cell.Background.rectTransform, 3f);

            cell.Stripe = UiKit.CreateImage(cell.Background.transform, "Stripe", Color.white);
            cell.Stripe.rectTransform.anchorMin = Vector2.zero;
            cell.Stripe.rectTransform.anchorMax = new Vector2(0f, 1f);
            cell.Stripe.rectTransform.offsetMin = Vector2.zero;
            cell.Stripe.rectTransform.offsetMax = new Vector2(10f, 0f);

            cell.Label = UiKit.CreateText(cell.Background.transform, "Label", string.Empty, 28, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Place(cell.Label.rectTransform, new Vector2(0f, 1f), new Vector2(22f, -10f), new Vector2(width * 0.55f, 36f));
            cell.Label.rectTransform.pivot = new Vector2(0f, 1f);

            cell.Value = UiKit.CreateText(cell.Background.transform, "Value", string.Empty, 52, Color.white, TextAnchor.MiddleRight, FontStyle.Bold);
            UiKit.Place(cell.Value.rectTransform, new Vector2(1f, 1f), new Vector2(-14f, -6f), new Vector2(width * 0.5f, 62f));
            cell.Value.rectTransform.pivot = new Vector2(1f, 1f);

            cell.Detail = UiKit.CreateText(cell.Background.transform, "Detail", string.Empty, 22, UiKit.Muted, TextAnchor.LowerLeft, FontStyle.Normal);
            UiKit.Place(cell.Detail.rectTransform, new Vector2(0f, 0f), new Vector2(22f, 6f), new Vector2(width - 40f, 28f));
            cell.Detail.rectTransform.pivot = new Vector2(0f, 0f);
            return cell;
        }
    }
}
