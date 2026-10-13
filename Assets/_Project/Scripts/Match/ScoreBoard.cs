using System.Collections.Generic;
using TankBrawl.Core;
using TankBrawl.Tank;
using TankBrawl.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TankBrawl.Match
{
    /// <summary>
    /// The score board at the top centre of the match HUD, built in code from <see cref="UiShape"/> plates (slanted, thick
    /// dark outline, like the tanks and tiles). A dark core in the middle holds the clock (or the round) and what wins the
    /// match. Team modes put a wing on each side: the score next to the core, a bar that grows from the core outwards
    /// (the team's total health in the elimination mode, progress to the score limit otherwise), the team's tanks as small
    /// icons that go dark while a tank is dead, and round-win pips in the elimination mode. The free-for-all puts one
    /// plate per tank around the core; the plate itself fills up as the tank nears the score limit.
    /// Everything is read from the mode and the participants; texts change only when their value does.
    /// </summary>
    public class ScoreBoard : MonoBehaviour
    {
        static readonly Vector2 Mid = new Vector2(0.5f, 0.5f);
        static readonly Color Ink = new Color(0.07f, 0.08f, 0.13f, 1f);
        static readonly Color PlateColor = new Color(0.15f, 0.18f, 0.27f, 1f);
        static readonly Color EmptyPip = new Color(0.1f, 0.12f, 0.18f, 1f);
        static readonly Color DeadTint = new Color(0.17f, 0.19f, 0.25f, 0.85f);

        const float TopMargin = 8f;
        const float Slope = 0.2f;
        const float Outline = 5f;

        // Core.
        const float CoreHeight = 88f;
        const float RoundsCoreWidth = 300f;
        const float PointsCoreWidth = 380f;
        const float SoloCoreWidth = 190f;
        const float ScoreInset = 54f;
        const float PointsScoreInset = 64f;
        const int RoundsScoreSize = 58;
        const int PointsScoreSize = 50;
        const int ClockSize = 46;
        const int TitleSize = 30;
        const int ClockMaxLength = 5;

        // Status tab under the core.
        const float StatusWidth = 250f;
        const float StatusHeight = 34f;
        const float StatusOverlap = 6f;
        const float StatusDarken = 0.55f;

        // Team wings.
        const float WingGap = -4f;
        const float BarWidth = 330f;
        const float BarHeight = 36f;
        const float BarY = -30f;
        const float BarTextInset = 20f;
        const float BarTextWidth = 120f;
        const float BackDarken = 0.3f;
        const float TankRowY = -70f;
        const float TankSize = 44f;
        const float TankStep = 50f;
        const float FirstTankOffset = 24f;
        const float PipWidth = 26f;
        const float PipHeight = 16f;
        const float PipStep = 30f;

        // Free-for-all plates.
        const float PlateWidth = 216f;
        const float PlateHeight = 64f;
        const float PlateGap = -8f;
        const float PlateFillDarken = 0.62f;
        const float SoloTankSize = 40f;
        const float LeaderWidth = 96f;
        const float LeaderHeight = 24f;

        // A tank sprite's canvas is 1.5 times the hull (96 px for a 64 px tank).
        const float SpriteCanvasScale = 1.5f;

        const float FillSpeed = 1.5f;
        const float PopTime = 0.3f;
        const float PopScale = 0.4f;
        const float PulseRate = 6f;
        const float PulseDepth = 0.3f;

        sealed class TankIcon
        {
            public MatchParticipant Participant;
            public readonly List<Graphic> Parts = new List<Graphic>(5);
            public Color AliveColor = Color.white;
            public bool ShownAlive = true;
        }

        // A team wing, or one tank's plate in the free-for-all.
        sealed class Side
        {
            public ScoreEntry Entry;
            public UiShape Bar;
            public Text Score;
            public Text BarValue;
            public UiShape[] Pips;
            public GameObject LeaderTab;
            public readonly List<TankIcon> Tanks = new List<TankIcon>(3);
            public Color TeamColor;
            public Color FillColor;
            public int ShownPoints = int.MinValue;
            public int ShownHealth = -1;
            public float Fill;
            public float PopLeft;
        }

        MatchManager match;
        IMatchMode mode;
        readonly List<Side> sides = new List<Side>(4);
        bool built;
        bool freeForAll;
        bool teamHealthBars;
        int lastScoreVersion = -1;

        Text goal;
        Text clock;
        UiShape statusTab;
        Text statusText;
        string shownMain = string.Empty;
        Color shownStatusColor = Color.clear;

        public static ScoreBoard Create(RectTransform parent, MatchManager owner)
        {
            RectTransform rect = UiKit.CreateRect("ScoreBoard", parent);
            UiKit.Place(rect, new Vector2(0.5f, 1f), new Vector2(0f, -TopMargin), Vector2.zero);
            var board = rect.gameObject.AddComponent<ScoreBoard>();
            board.match = owner;
            board.mode = owner.Mode;
            return board;
        }

        // ---- Building ----

        // The mode fills its score entries in Begin, after the HUD exists: the board is built on the first frame they are there.
        void Build()
        {
            IReadOnlyList<ScoreEntry> entries = mode.Scores;
            freeForAll = match.Config.kind == MatchModeKind.Deathmatch;
            teamHealthBars = match.Config.kind == MatchModeKind.TeamElimination;
            // Round wins are one digit; points go up to three and need a wider core.
            float coreWidth = freeForAll ? SoloCoreWidth : teamHealthBars ? RoundsCoreWidth : PointsCoreWidth;

            BuildStatusTab();
            if (freeForAll)
            {
                for (int i = 0; i < entries.Count; i++)
                    sides.Add(BuildPlate(entries[i], i, entries.Count, coreWidth));
            }

            RectTransform core = BuildCore(coreWidth);
            if (!freeForAll)
            {
                sides.Add(BuildWing(entries[0], -1, coreWidth, core));
                if (entries.Count > 1)
                    sides.Add(BuildWing(entries[1], 1, coreWidth, core));
            }

            built = true;
        }

        RectTransform BuildCore(float width)
        {
            UiShape core = UiShape.Create(transform, "Core", PlateColor, 14f, Outline);
            core.OutlineColor = Ink;
            core.SetSlope(-Slope, Slope);
            UiKit.Place(core.rectTransform, Mid, new Vector2(0f, -CoreHeight * 0.5f), new Vector2(width, CoreHeight));

            goal = UiKit.CreateText(core.transform, "Goal", string.Empty, 20, UiKit.Muted, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place(goal.rectTransform, Mid, new Vector2(0f, 25f), new Vector2(140f, 24f));

            clock = UiKit.CreateText(core.transform, "Clock", string.Empty, ClockSize, UiKit.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place(clock.rectTransform, Mid, new Vector2(0f, -10f), new Vector2(140f, 52f));
            return core.rectTransform;
        }

        void BuildStatusTab()
        {
            statusTab = UiShape.Create(transform, "Status", PlateColor, 10f, Outline);
            statusTab.OutlineColor = Ink;
            statusTab.SetSlope(-Slope, Slope);
            float y = -(CoreHeight + StatusHeight * 0.5f - StatusOverlap);
            UiKit.Place(statusTab.rectTransform, Mid, new Vector2(0f, y), new Vector2(StatusWidth, StatusHeight));

            statusText = UiKit.CreateText(statusTab.transform, "Text", string.Empty, 19, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Stretch(statusText.rectTransform);
            statusText.rectTransform.offsetMax = new Vector2(0f, -StatusOverlap * 0.5f);
            statusTab.gameObject.SetActive(false);
        }

        // side: -1 = left of the core, 1 = right. Everything is mirrored around the core.
        Side BuildWing(ScoreEntry entry, int side, float coreWidth, RectTransform core)
        {
            var wing = new Side { Entry = entry, TeamColor = TeamColors.Of(entry.Team) };
            wing.FillColor = wing.TeamColor;
            float edge = coreWidth * 0.5f;

            wing.Bar = UiShape.Create(transform, "Bar", Darken(wing.TeamColor, BackDarken), 9f, Outline);
            wing.Bar.OutlineColor = Ink;
            wing.Bar.SetSlope(side * Slope);
            wing.Bar.FillColor = wing.FillColor;
            wing.Bar.FillFromRight = side < 0;
            wing.Bar.FillAmount = 0f;
            UiKit.Place(wing.Bar.rectTransform, Mid, new Vector2(side * (edge + WingGap + BarWidth * 0.5f), BarY), new Vector2(BarWidth, BarHeight));

            Text label = BarText(wing.Bar.transform, "Label", side, true);
            label.text = entry.Label;
            if (teamHealthBars)
                wing.BarValue = BarText(wing.Bar.transform, "Value", side, false);

            int scoreSize = teamHealthBars ? RoundsScoreSize : PointsScoreSize;
            float scoreInset = teamHealthBars ? ScoreInset : PointsScoreInset;
            wing.Score = UiKit.CreateText(core, "Score", string.Empty, scoreSize, Color.Lerp(wing.TeamColor, Color.white, 0.25f), TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place(wing.Score.rectTransform, Mid, new Vector2(side * (edge - scoreInset), -2f), new Vector2(100f, 70f));

            // The team's tanks look at the core, the nearest to it first.
            float x = edge + FirstTankOffset;
            for (int i = 0; i < match.Participants.Count; i++)
            {
                MatchParticipant p = match.Participants[i];
                if (p.Team != entry.Team)
                    continue;

                wing.Tanks.Add(BuildIcon(p, side * x, TankRowY, TankSize, side * 90f));
                x += TankStep;
            }

            if (teamHealthBars)
            {
                int count = Mathf.Max(1, match.Config.roundsToWin);
                wing.Pips = new UiShape[count];
                x += PipWidth * 0.5f;
                for (int i = 0; i < count; i++)
                {
                    UiShape pip = UiShape.Create(transform, "Pip", EmptyPip, 4f, 4f);
                    pip.OutlineColor = Ink;
                    pip.SetSlope(side * Slope);
                    UiKit.Place(pip.rectTransform, Mid, new Vector2(side * x, TankRowY), new Vector2(PipWidth, PipHeight));
                    wing.Pips[i] = pip;
                    x += PipStep;
                }
            }

            return wing;
        }

        // A text at the end of a wing's bar: the end next to the core, or the far one.
        static Text BarText(Transform bar, string name, int side, bool nearCore)
        {
            bool atRight = nearCore ? side < 0 : side > 0;
            Text text = UiKit.CreateText(bar, name, string.Empty, 22, Color.white, atRight ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, FontStyle.Bold);
            float x = (atRight ? 1f : -1f) * (BarWidth * 0.5f - BarTextInset - BarTextWidth * 0.5f);
            UiKit.Place(text.rectTransform, Mid, new Vector2(x, 1f), new Vector2(BarTextWidth, BarHeight));
            return text;
        }

        // Plates stand in the order of the entries, left to right, half of them on each side of the core.
        Side BuildPlate(ScoreEntry entry, int index, int count, float coreWidth)
        {
            var plate = new Side { Entry = entry, TeamColor = TeamColors.Of(entry.Team) };
            plate.FillColor = Darken(plate.TeamColor, PlateFillDarken);

            int leftCount = (count + 1) / 2;
            int side = index < leftCount ? -1 : 1;
            int slot = side < 0 ? leftCount - 1 - index : index - leftCount;
            float x = side * (coreWidth * 0.5f + PlateGap + PlateWidth * 0.5f + slot * (PlateWidth + PlateGap));
            float y = -CoreHeight * 0.5f;

            // The leader's tab hangs from under the plate, so it is created first.
            UiShape leader = UiShape.Create(transform, "Leader", UiKit.Accent, 8f, 4f);
            leader.OutlineColor = Ink;
            leader.SetSlope(side * Slope);
            UiKit.Place(leader.rectTransform, Mid, new Vector2(x, y - PlateHeight * 0.5f - LeaderHeight * 0.5f + 5f), new Vector2(LeaderWidth, LeaderHeight));
            Text leaderText = UiKit.CreateText(leader.transform, "Text", "ЛИДЕР", 15, Ink, TextAnchor.MiddleCenter, FontStyle.Bold, false);
            UiKit.Stretch(leaderText.rectTransform);
            leaderText.rectTransform.offsetMax = new Vector2(0f, -3f);
            plate.LeaderTab = leader.gameObject;
            plate.LeaderTab.SetActive(false);

            plate.Bar = UiShape.Create(transform, "Plate", Darken(plate.TeamColor, BackDarken), 11f, Outline);
            plate.Bar.OutlineColor = entry.IsLocal ? UiKit.Accent : Ink;
            plate.Bar.SetSlope(side * Slope);
            plate.Bar.FillColor = plate.FillColor;
            plate.Bar.FillAmount = 0f;
            UiKit.Place(plate.Bar.rectTransform, Mid, new Vector2(x, y), new Vector2(PlateWidth, PlateHeight));

            if (entry.Participant != null)
                plate.Tanks.Add(BuildIcon(entry.Participant, x - PlateWidth * 0.5f + 38f, y, SoloTankSize, 0f));

            Text label = UiKit.CreateText(plate.Bar.transform, "Label", entry.Label, 21, entry.IsLocal ? UiKit.Accent : Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Place(label.rectTransform, Mid, new Vector2(-PlateWidth * 0.5f + 66f + 45f, 15f), new Vector2(90f, 26f));

            plate.Score = UiKit.CreateText(plate.Bar.transform, "Score", string.Empty, 40, Color.white, TextAnchor.MiddleRight, FontStyle.Bold);
            UiKit.Place(plate.Score.rectTransform, new Vector2(1f, 0.5f), new Vector2(-22f, -5f), new Vector2(90f, 46f));
            return plate;
        }

        // The tank itself, small: the parts of its skin stacked like on the field (all of them share one canvas and pivot).
        TankIcon BuildIcon(MatchParticipant p, float x, float y, float size, float angle)
        {
            var icon = new TankIcon { Participant = p };
            RectTransform root = UiKit.CreateRect("Tank", transform);
            UiKit.Place(root, Mid, new Vector2(x, y), new Vector2(size, size) * SpriteCanvasScale);
            root.localRotation = Quaternion.Euler(0f, 0f, angle);

            TankSkin skin = p.Object.TryGetComponent(out TankVisuals visuals) ? visuals.Skin : null;
            if (skin != null && skin.body != null)
            {
                AddPart(icon, root, First(skin.trackLeft));
                AddPart(icon, root, First(skin.trackRight));
                AddPart(icon, root, skin.body);
                AddPart(icon, root, skin.barrel);
                AddPart(icon, root, skin.turret);
            }
            else
            {
                // No skin: a block in the team's colour.
                icon.AliveColor = TeamColors.Of(p.Team);
                UiShape block = UiShape.Create(root, "Block", icon.AliveColor, 6f, 4f);
                block.OutlineColor = Ink;
                UiKit.Stretch(block.rectTransform, size * (SpriteCanvasScale - 1f) * 0.5f);
                icon.Parts.Add(block);
            }

            if (p.IsHuman && !freeForAll)
            {
                // Which of the team's tanks is mine.
                UiShape mark = UiShape.Create(transform, "Mine", UiKit.Accent, 3f, 2f);
                mark.OutlineColor = Ink;
                mark.Gloss = 0f;
                UiKit.Place(mark.rectTransform, Mid, new Vector2(x, y - size * 0.5f - 5f), new Vector2(24f, 8f));
            }

            return icon;
        }

        static void AddPart(TankIcon icon, RectTransform root, Sprite sprite)
        {
            if (sprite == null)
                return;

            Image image = UiKit.CreateImage(root, sprite.name, Color.white, sprite);
            UiKit.Stretch(image.rectTransform);
            icon.Parts.Add(image);
        }

        static Sprite First(Sprite[] frames)
        {
            return frames != null && frames.Length > 0 ? frames[0] : null;
        }

        static Color Darken(Color color, float factor)
        {
            return new Color(color.r * factor, color.g * factor, color.b * factor, 1f);
        }

        // ---- Updating ----

        void LateUpdate()
        {
            if (mode == null)
                return;

            if (!built)
            {
                if (mode.Scores.Count == 0)
                    return;

                Build();
            }

            UpdateCore();

            bool scoresChanged = mode.ScoreVersion != lastScoreVersion;
            lastScoreVersion = mode.ScoreVersion;

            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < sides.Count; i++)
            {
                Side side = sides[i];
                if (scoresChanged)
                    RefreshScore(side);

                UpdateTanks(side);
                UpdateBar(side, dt);
                UpdatePop(side, dt);
            }
        }

        void UpdateCore()
        {
            if (goal.text != mode.GoalText)
                goal.text = mode.GoalText;

            // The clock when the mode has one, otherwise what is going on ("РАУНД 2"), smaller.
            string main = mode.TimerText.Length > 0 ? mode.TimerText : mode.TitleText;
            if (main != shownMain)
            {
                shownMain = main;
                clock.fontSize = main.Length <= ClockMaxLength ? ClockSize : TitleSize;
                clock.text = main;
            }

            string status = mode.StatusText;
            if (statusText.text != status)
            {
                statusText.text = status;
                statusTab.gameObject.SetActive(status.Length > 0);
            }

            Color statusColor = mode.StatusColor;
            if (statusColor != shownStatusColor)
            {
                shownStatusColor = statusColor;
                statusTab.color = Darken(statusColor, StatusDarken);
            }
        }

        void RefreshScore(Side side)
        {
            ScoreEntry entry = side.Entry;
            if (side.Score.text != entry.Value)
                side.Score.text = entry.Value;

            if (entry.Points != side.ShownPoints)
            {
                // The number jumps when it grows (not on the first fill).
                if (side.ShownPoints != int.MinValue && entry.Points > side.ShownPoints)
                    side.PopLeft = PopTime;
                side.ShownPoints = entry.Points;

                if (side.Pips != null)
                {
                    for (int i = 0; i < side.Pips.Length; i++)
                        side.Pips[i].color = i < entry.Points ? side.TeamColor : EmptyPip;
                }
            }

            if (side.LeaderTab != null && side.LeaderTab.activeSelf != entry.Highlight)
                side.LeaderTab.SetActive(entry.Highlight);
        }

        void UpdateTanks(Side side)
        {
            for (int i = 0; i < side.Tanks.Count; i++)
            {
                TankIcon icon = side.Tanks[i];
                bool alive = icon.Participant.IsAlive;
                if (alive == icon.ShownAlive)
                    continue;

                icon.ShownAlive = alive;
                Color tint = alive ? icon.AliveColor : DeadTint;
                for (int k = 0; k < icon.Parts.Count; k++)
                    icon.Parts[k].color = tint;
            }
        }

        void UpdateBar(Side side, float dt)
        {
            float target = teamHealthBars ? TeamHealth(side) : side.Entry.Progress;
            side.Fill = Mathf.MoveTowards(side.Fill, target, FillSpeed * dt);
            side.Bar.FillAmount = side.Fill;

            // A team that is scoring right now pulses.
            Color fill = side.FillColor;
            if (side.Entry.Highlight && !freeForAll)
                fill = Color.Lerp(fill, Color.white, PulseDepth * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * PulseRate)));
            side.Bar.FillColor = fill;
        }

        // The living tanks' health as a share of the whole team's; also keeps the number on the bar up to date.
        float TeamHealth(Side side)
        {
            float current = 0f;
            float max = 0f;
            for (int i = 0; i < side.Tanks.Count; i++)
            {
                MatchParticipant p = side.Tanks[i].Participant;
                max += p.Health.Max;
                if (p.IsAlive)
                    current += p.Health.Current;
            }

            int shown = Mathf.CeilToInt(current);
            if (side.BarValue != null && shown != side.ShownHealth)
            {
                side.ShownHealth = shown;
                side.BarValue.text = shown.ToString();
            }

            return max > 0f ? Mathf.Clamp01(current / max) : 0f;
        }

        void UpdatePop(Side side, float dt)
        {
            if (side.PopLeft <= 0f)
                return;

            side.PopLeft = Mathf.Max(0f, side.PopLeft - dt);
            float t = side.PopLeft / PopTime;
            float scale = 1f + PopScale * t * t;
            side.Score.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
