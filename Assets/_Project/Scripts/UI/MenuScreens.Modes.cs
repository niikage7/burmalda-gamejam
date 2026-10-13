using System.Collections.Generic;
using TankBrawl.AI;
using TankBrawl.Audio;
using TankBrawl.Match;
using TankBrawl.Progress;
using UnityEngine;
using UnityEngine.UI;

namespace TankBrawl.UI
{
    public partial class MenuScreens
    {
        // One of the three mode cards: its difficulty picker and its "selected" marks.
        sealed class ModeCardUi
        {
            public MatchModeConfig Config;
            public int Mode;
            public RectTransform Slot;
            public RectTransform CardRect;
            public GameObject Ring;
            public GameObject SelectedTag;
            public Text DifficultyLabel;
            public Text LockHint;
            public Image LockIcon;
            public UiButton Previous;
            public UiButton Next;
        }

        sealed class MapItemUi
        {
            public int Index;
            public GameObject Ring;
            public Text Label;
            public UiPlate LabelPill;
        }

        readonly List<ModeCardUi> modeCards = new List<ModeCardUi>();
        readonly List<MapItemUi> mapItems = new List<MapItemUi>();
        readonly List<SceneEntry> availableMaps = new List<SceneEntry>();
        int mapIndex;
        UiButton doneButton;

        const float ModeCardWidth = 570f;
        const float ModeCardHeight = 520f;
        const float ModeCardGap = 32f;
        const float MapItemWidth = 200f;
        const float MapItemGap = 28f;

        // ---- Selection state ----

        MatchModeConfig SelectedMode
        {
            get
            {
                if (MatchSession.Selected != null)
                    return MatchSession.Selected;

                if (modes != null)
                {
                    foreach (MatchModeConfig mode in modes)
                    {
                        if (mode != null)
                            return mode;
                    }
                }

                return null;
            }
        }

        SceneEntry SelectedMap => availableMaps[Mathf.Clamp(mapIndex, 0, availableMaps.Count - 1)];

        static UiStyle.Tone ToneOf(MatchModeKind kind)
        {
            switch (kind)
            {
                case MatchModeKind.TeamElimination: return UiStyle.Blue;
                case MatchModeKind.CapturePoint: return UiStyle.Orange;
                default: return UiStyle.Sand;
            }
        }

        static string FormatOf(MatchModeKind kind)
        {
            return kind == MatchModeKind.Deathmatch ? "4 игрока" : "3 × 3";
        }

        // The difficulty picked for a mode: the last one looked at, else the best one that is open.
        static int DifficultyOf(int mode)
        {
            int count = DifficultyConfig.Count;
            int highest = Mathf.Min(PlayerProgress.HighestUnlockedDifficulty(mode), count - 1);
            int last = mode >= 0 && mode < MatchSession.LastDifficulty.Length ? MatchSession.LastDifficulty[mode] : -1;
            return last < 0 ? highest : Mathf.Min(last, count - 1);
        }

        // The maps whose scenes are in the build, starting on the one picked last time.
        void CollectMaps()
        {
            availableMaps.Clear();
            if (maps != null)
            {
                foreach (SceneEntry entry in maps)
                {
                    if (!string.IsNullOrEmpty(entry.sceneName) && Application.CanStreamedLevelBeLoaded(entry.sceneName))
                        availableMaps.Add(entry);
                }
            }

            if (availableMaps.Count == 0)
                availableMaps.Add(new SceneEntry { label = arenaScene, sceneName = arenaScene });

            mapIndex = 0;
            for (int i = 0; i < availableMaps.Count; i++)
            {
                if (availableMaps[i].sceneName == MatchSession.MapScene)
                    mapIndex = i;
            }
        }

        // ---- Building ----

        void BuildModes()
        {
            RectTransform root = NewPage(Page.Modes);
            Text header = UiStyle.Label(root, "Header", "РЕЖИМ И КАРТА", 80, Color.white, TextAnchor.MiddleLeft);
            UiLayout.LT(header.rectTransform, 176f, 28f, 1000f, 110f);

            int count = modes != null ? modes.Length : 0;
            for (int i = 0; i < count; i++)
            {
                if (modes[i] != null)
                    modeCards.Add(BuildModeCard(root, modes[i], i, (i - (count - 1) * 0.5f) * (ModeCardWidth + ModeCardGap)));
            }

            Text mapTitle = UiStyle.Label(root, "MapTitle", "КАРТА", 34, Color.white);
            UiLayout.Mid(mapTitle.rectTransform, 0f, -183f, 400f, 44f);

            float rowX = -(availableMaps.Count - 1) * 0.5f * (MapItemWidth + MapItemGap);
            for (int i = 0; i < availableMaps.Count; i++)
                mapItems.Add(BuildMapItem(root, i, rowX + i * (MapItemWidth + MapItemGap)));

            doneButton = UiStyle.Button(root, "Done", "ГОТОВО", UiStyle.Green, new Vector2(440f, 96f), FinishModes, 56);
            UiLayout.BottomMid(doneButton.Plate.rectTransform, 0f, 50f, 440f, 96f);
            doneButton.Plate.Radius = 22f;
            doneButton.Plate.Depth = 10f;
        }

        ModeCardUi BuildModeCard(Transform parent, MatchModeConfig config, int index, float x)
        {
            var ui = new ModeCardUi { Config = config, Mode = (int)config.kind };
            UiStyle.Tone tone = ToneOf(config.kind);

            // The slot lifts the whole card (and its yellow ring) when it is chosen.
            RectTransform slot = UiKit.CreateRect("Slot" + index, parent);
            UiLayout.Mid(slot, x, 110f, ModeCardWidth, ModeCardHeight);
            ui.Slot = slot;

            UiPlate ring = UiStyle.Plate(slot, "Ring", UiStyle.Gold, 34f, 4f, 10f, 0f, 0f);
            UiLayout.Mid(ring.rectTransform, 0f, -2f, ModeCardWidth + 24f, ModeCardHeight + 24f);
            ui.Ring = ring.gameObject;

            UiButton card = UiStyle.Button(slot, "Card", string.Empty, tone, new Vector2(ModeCardWidth, ModeCardHeight), () => PickMode(config), 40);
            UiLayout.Mid(card.Plate.rectTransform, 0f, 0f, ModeCardWidth, ModeCardHeight);
            card.Plate.Radius = 26f;
            card.Plate.OutlineWidth = 5f;
            card.Plate.Depth = 10f;
            card.Plate.FaceHeight = 16f;
            card.Plate.HiHeight = 26f;
            card.Label.gameObject.SetActive(false);
            ui.CardRect = card.Plate.rectTransform;
            Transform content = card.Content;

            Text format = Tag(content, "Format", FormatOf(config.kind), 22, Color.white, UiStyle.Ink);
            SizeTag(format, 22f, 22f, 150f, 38f);

            Text name = UiStyle.Label(content, "Name", config.displayName, 44, Color.white, TextAnchor.UpperLeft);
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiLayout.LT(name.rectTransform, 22f, 70f, ModeCardWidth - 44f, 100f);

            UiPlate rule = UiStyle.Cream(content, "Rule", 14f, 3f);
            UiLayout.LT(rule.rectTransform, 22f, 174f, ModeCardWidth - 44f, 128f);
            Text ruleText = UiStyle.Plain(rule.transform, "Text", config.description, 22, UiStyle.Ink, TextAnchor.UpperLeft);
            ruleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            ruleText.verticalOverflow = VerticalWrapMode.Truncate;
            UiLayout.Fill(ruleText.rectTransform, 14f, 10f, 14f, 8f);

            Text difficultyTitle = UiStyle.Label(content, "DifficultyTitle", "СЛОЖНОСТЬ", 26, Color.white, TextAnchor.MiddleLeft, false);
            UiLayout.LT(difficultyTitle.rectTransform, 22f, 316f, 300f, 34f);

            Sprite backIcon = UiKit.Theme != null ? UiKit.Theme.backIcon : null;
            ui.Previous = UiStyle.IconButton(content, "Previous", backIcon, 64f, () => ShiftDifficulty(ui, -1));
            UiLayout.LT(ui.Previous.Plate.rectTransform, 22f, 356f, 64f, 64f);
            ui.Next = UiStyle.IconButton(content, "Next", backIcon, 64f, () => ShiftDifficulty(ui, 1));
            UiLayout.LT(ui.Next.Plate.rectTransform, ModeCardWidth - 22f - 64f, 356f, 64f, 64f);
            Transform flip = ui.Next.Content.Find("Icon");
            if (flip != null)
                flip.localScale = new Vector3(-1f, 1f, 1f);

            ui.DifficultyLabel = UiStyle.Label(content, "Difficulty", string.Empty, 36, Color.white);
            UiLayout.LT(ui.DifficultyLabel.rectTransform, 96f, 356f, ModeCardWidth - 192f, 64f);

            ui.LockIcon = UiStyle.Icon(content, "Lock", UiKit.Theme != null ? UiKit.Theme.lockIcon : null, 46f);
            UiLayout.LT(ui.LockIcon.rectTransform, 108f, 365f, 46f, 46f);

            ui.LockHint = UiStyle.Label(content, "LockHint", string.Empty, 22, Color.white, TextAnchor.UpperLeft, false);
            ui.LockHint.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiLayout.LT(ui.LockHint.rectTransform, 22f, 432f, ModeCardWidth - 44f, 60f);

            // "ВЫБРАН" tab on the top edge.
            UiPlate chosen = UiStyle.Plate(slot, "Chosen", UiStyle.Gold, 999f, 4f, 4f, 0f, 0f);
            UiLayout.TopMid(chosen.rectTransform, ModeCardWidth * 0.5f - 130f, -30f, 190f, 50f);
            Text chosenText = UiStyle.Label(chosen.transform, "Text", "ВЫБРАН", 28, UiStyle.Ink, TextAnchor.MiddleCenter, false);
            chosenText.GetComponent<UiTextOutline>().Width = 0f;
            UiLayout.Fill(chosenText.rectTransform, 0f, 0f, 0f, 4f);
            ui.SelectedTag = chosen.gameObject;
            return ui;
        }

        MapItemUi BuildMapItem(Transform parent, int index, float x)
        {
            var ui = new MapItemUi { Index = index };
            SceneEntry entry = availableMaps[index];
            UiTheme theme = UiKit.Theme;

            RectTransform slot = UiKit.CreateRect("Map" + index, parent);
            UiLayout.Mid(slot, x, -293f, MapItemWidth, 170f);

            UiPlate ring = UiStyle.Plate(slot, "Ring", UiStyle.Gold, 22f, 4f, 7f, 0f, 0f);
            UiLayout.LT(ring.rectTransform, -9f, -9f, MapItemWidth + 18f, 148f);
            ui.Ring = ring.gameObject;

            int captured = index;
            UiButton frame = UiStyle.Button(slot, "Frame", string.Empty, UiStyle.Dark, new Vector2(MapItemWidth, 130f), () => PickMap(captured), 20);
            UiLayout.LT(frame.Plate.rectTransform, 0f, 0f, MapItemWidth, 130f);
            frame.Plate.SetTone(new UiStyle.Tone(UiStyle.Ink, UiStyle.Ink, UiStyle.Ink));
            frame.Plate.Radius = 14f;
            frame.Plate.Depth = 6f;
            frame.Plate.FaceHeight = 0f;
            frame.Plate.HiHeight = 0f;
            frame.Label.gameObject.SetActive(false);

            Image thumb = UiKit.CreateImage(frame.Content, "Thumb", Color.white, theme != null ? theme.PreviewOf(entry.sceneName) : null);
            thumb.preserveAspect = true;
            UiLayout.Fill(thumb.rectTransform, 5f, 5f, 5f, 5f);

            ui.LabelPill = UiStyle.Pill(slot, "Label", UiStyle.Ink);
            UiLayout.LT(ui.LabelPill.rectTransform, 0f, 142f, MapItemWidth, 30f);
            ui.Label = UiStyle.Plain(ui.LabelPill.transform, "Text", entry.label, 19, Color.white, TextAnchor.MiddleCenter);
            UiLayout.Fill(ui.Label.rectTransform);
            return ui;
        }

        // ---- Actions ----

        void PickMode(MatchModeConfig config)
        {
            MatchSession.Selected = config;
            RefreshModes();
            RefreshLobby();
        }

        void PickMap(int index)
        {
            mapIndex = index;
            MatchSession.MapScene = availableMaps[index].sceneName;
            RefreshModes();
            RefreshLobby();
        }

        void ShiftDifficulty(ModeCardUi ui, int step)
        {
            int next = Mathf.Clamp(DifficultyOf(ui.Mode) + step, 0, DifficultyConfig.Count - 1);
            if (ui.Mode >= 0 && ui.Mode < MatchSession.LastDifficulty.Length)
                MatchSession.LastDifficulty[ui.Mode] = next;

            // Touching a card's picker also picks that card.
            MatchSession.Selected = ui.Config;
            RefreshModes();
            RefreshLobby();
        }

        void FinishModes()
        {
            if (history.Count > 0)
                Back();
            else
                Show(Page.Lobby, false);
        }

        // ---- Refresh ----

        void RefreshModes()
        {
            if (doneButton == null)
                return;

            MatchModeConfig chosen = SelectedMode;
            int levels = DifficultyConfig.Count;
            bool chosenOpen = true;
            foreach (ModeCardUi ui in modeCards)
            {
                bool selected = ui.Config == chosen;
                ui.Ring.SetActive(selected);
                ui.SelectedTag.SetActive(selected);
                ui.Slot.anchoredPosition = new Vector2(ui.Slot.anchoredPosition.x, selected ? 124f : 110f);

                int difficulty = DifficultyOf(ui.Mode);
                bool unlocked = PlayerProgress.IsDifficultyUnlocked(ui.Mode, difficulty);
                ui.DifficultyLabel.text = DifficultyConfig.NameOf(difficulty).ToUpperInvariant();
                ui.LockIcon.gameObject.SetActive(!unlocked && ui.LockIcon.sprite != null);
                ui.LockHint.text = unlocked || difficulty < 1 ? string.Empty : "Закрыта: победите на «" + DifficultyConfig.NameOf(difficulty - 1) + "»";
                ui.Previous.interactable = difficulty > 0;
                ui.Next.interactable = difficulty < levels - 1;
                if (selected)
                    chosenOpen = unlocked;
            }

            foreach (MapItemUi item in mapItems)
            {
                bool selected = item.Index == mapIndex;
                item.Ring.SetActive(selected);
                item.LabelPill.color = selected ? UiStyle.Yellow : UiStyle.Ink;
                item.Label.color = selected ? UiStyle.Ink : Color.white;
            }

            doneButton.interactable = chosenOpen;
        }
    }
}
