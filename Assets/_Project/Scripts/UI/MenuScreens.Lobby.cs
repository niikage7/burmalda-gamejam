using TankBrawl.AI;
using TankBrawl.Audio;
using TankBrawl.Match;
using TankBrawl.Progress;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TankBrawl.UI
{
    public partial class MenuScreens
    {
        sealed class StatRow
        {
            public UpgradeBranch Branch;
            public Text Level;
            public Text Value;
        }

        readonly System.Collections.Generic.List<StatRow> statRows = new System.Collections.Generic.List<StatRow>();
        Text lobbyRank;
        GameObject garageBadge;
        Text garageBadgeText;
        UiButton modeCard;
        Text modeTag;
        Text modeName;
        Text modeMap;
        Image modeThumb;
        UiButton playButton;

        void BuildLobby()
        {
            RectTransform root = NewPage(Page.Lobby);
            UiTheme theme = UiKit.Theme;

            BuildTitle(root, theme != null ? theme.gameName : "TANK BRAWL");
            BuildPodium(root, 560f, 0f, 50f, 330f, "Podium");

            // Under the podium: the tank's name tag and the threat rank.
            RectTransform tagRow = UiKit.CreateRect("TagRow", root);
            UiLayout.Mid(tagRow, 0f, -300f, 720f, 90f);
            UiPlate tag = UiStyle.Plate(tagRow, "Name", UiStyle.Blue, 18f, 4f, 8f, 9f, 8f);
            UiLayout.Mid(tag.rectTransform, -190f, 0f, 320f, 76f);
            Text tagText = UiStyle.Label(tag.transform, "Text", "МОЙ ТАНК", 40, Color.white);
            UiLayout.Fill(tagText.rectTransform, 0f, 0f, 0f, 6f);

            UiPlate rank = UiStyle.Pill(tagRow, "Rank", UiStyle.Ink);
            UiLayout.Mid(rank.rectTransform, 150f, 0f, 250f, 66f);
            Text rankCaption = UiStyle.Label(rank.transform, "Caption", "РАНГ", 30, UiStyle.Yellow, TextAnchor.MiddleLeft, false);
            UiLayout.Fill(rankCaption.rectTransform, 28f, 0f, 0f, 4f);
            lobbyRank = UiStyle.Label(rank.transform, "Value", "0", 36, Color.white, TextAnchor.MiddleRight, false);
            UiLayout.Fill(lobbyRank.rectTransform, 0f, 0f, 32f, 4f);

            BuildStatsPanel(root);
            BuildModeCard(root);

            playButton = UiStyle.Button(root, "Play", "В БОЙ!", UiStyle.Green, new Vector2(560f, 168f), StartFromLobby, 88);
            UiLayout.RB(playButton.Plate.rectTransform, 40f, 52f, 560f, 168f);
            playButton.Plate.Radius = 26f;
            playButton.Plate.OutlineWidth = 5f;
            playButton.Plate.Depth = 11f;
            playButton.Plate.FaceHeight = 14f;
            playButton.Plate.HiHeight = 40f;

#if !UNITY_WEBGL
            if (!Application.isMobilePlatform)
            {
                UiButton quit = UiStyle.Button(root, "Quit", "ВЫХОД", UiStyle.Orange, new Vector2(170f, 60f), Application.Quit, 28);
                UiLayout.LT(quit.Plate.rectTransform, 56f, 282f, 170f, 60f);
            }
#endif
        }

        // "TANK" in white, "BRAWL" in yellow shifted right, the whole thing tilted.
        static void BuildTitle(Transform parent, string gameName)
        {
            string first = gameName;
            string second = string.Empty;
            int space = gameName.IndexOf(' ');
            if (space > 0)
            {
                first = gameName.Substring(0, space);
                second = gameName.Substring(space + 1);
            }

            RectTransform title = UiKit.CreateRect("Title", parent);
            UiLayout.LT(title, 48f, 26f, 700f, 240f);
            title.localRotation = Quaternion.Euler(0f, 0f, 3f);

            Text a = UiStyle.Label(title, "Line1", first, 96, Color.white, TextAnchor.UpperLeft);
            UiLayout.LT(a.rectTransform, 0f, 0f, 700f, 110f);
            Text b = UiStyle.Label(title, "Line2", second, 96, UiStyle.Yellow, TextAnchor.UpperLeft);
            UiLayout.LT(b.rectTransform, 64f, 92f, 700f, 110f);
        }

        // The steel disc with the tank on it (lobby and garage). Returns nothing: the tank sways by itself (see Update).
        void BuildPodium(Transform parent, float diameter, float x, float y, float tankSize, string name)
        {
            RectTransform podium = UiKit.CreateRect(name, parent);
            UiLayout.Mid(podium, x, y, diameter * 1.55f, diameter * 1.55f);

            Disc(podium, "Halo", diameter * 1.53f, new Color(0.706f, 0.886f, 0.478f, 0.6f));

            UiPlate plate = UiStyle.Plate(podium, "Plate", UiStyle.Steel, 999f, 5f, 16f, diameter * 0.032f, 10f);
            UiLayout.Mid(plate.rectTransform, 0f, 0f, diameter, diameter);

            UiPlate inner = UiStyle.Plate(podium, "Inner", UiStyle.Gray, 999f, 4f, 0f, 0f, 8f);
            UiLayout.Mid(inner.rectTransform, 0f, 0f, diameter - 124f, diameter - 124f);

            // Dashed ring.
            RectTransform ring = UiKit.CreateRect("Ring", podium);
            UiLayout.Mid(ring, 0f, 0f, diameter, diameter);
            float radius = diameter * 0.5f - 26f;
            const int dashes = 32;
            for (int i = 0; i < dashes; i++)
            {
                float angle = 360f * i / dashes;
                UiPlate dash = UiStyle.Pill(ring, "Dash", UiStyle.SteelText);
                float rad = angle * Mathf.Deg2Rad;
                UiLayout.Mid(dash.rectTransform, Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius, 38f, 8f);
                dash.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle + 90f);
            }

            spinning.Add(ring);

            Sprite preview = UiKit.Theme != null && UiKit.Theme.tankPreviews != null && UiKit.Theme.tankPreviews.Length > 0 ? UiKit.Theme.tankPreviews[0] : null;
            RectTransform sway = UiKit.CreateRect("Sway", podium);
            UiLayout.Mid(sway, 0f, 0f, tankSize, tankSize);
            Image tank = UiStyle.Icon(sway, "Tank", preview, tankSize);
            UiLayout.Fill(tank.rectTransform);
            var shadow = tank.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.078f, 0.063f, 0.059f, 0.3f);
            shadow.effectDistance = new Vector2(0f, -12f);
            swaying.Add(sway);
        }

        void BuildStatsPanel(Transform parent)
        {
            UiPlate panel = UiStyle.Panel(parent, "Stats", 22f);
            UiLayout.LB(panel.rectTransform, 40f, 50f, 560f, 486f);

            Text title = UiStyle.Label(panel.transform, "Title", "ХАРАКТЕРИСТИКИ", 32, Color.white, TextAnchor.MiddleLeft);
            UiLayout.LT(title.rectTransform, 22f, 14f, 500f, 46f);

            UiPlate box = UiStyle.Cream(panel.transform, "Rows", 14f, 3f);
            UiLayout.LT(box.rectTransform, 22f, 68f, 516f, 286f);

            ProgressionConfig progression = PlayerProgress.Config;
            for (int i = 0; i < PlayerProgress.BranchCount; i++)
            {
                var row = new StatRow { Branch = (UpgradeBranch)i };
                float y = 14f + i * 53f;

                ProgressionConfig.Branch info = progression != null && progression.branches != null && i < progression.branches.Length ? progression.branches[i] : null;
                Text name = UiStyle.Plain(box.transform, "Name", info != null ? info.displayName : row.Branch.ToString(), 24, UiStyle.Ink, TextAnchor.MiddleLeft);
                UiLayout.LT(name.rectTransform, 14f, y, 206f, 44f);
                name.fontSize = 21;

                UiPlate chip = UiStyle.Pill(box.transform, "Level", UiStyle.Ink);
                UiLayout.LT(chip.rectTransform, 222f, y + 8f, 76f, 30f);
                row.Level = UiStyle.Label(chip.transform, "Text", string.Empty, 20, UiStyle.Yellow, TextAnchor.MiddleCenter, false);
                UiLayout.Fill(row.Level.rectTransform, 0f, 0f, 0f, 2f);

                row.Value = UiStyle.Label(box.transform, "Value", string.Empty, 27, UiStyle.Ink, TextAnchor.MiddleRight, false);
                UiLayout.LT(row.Value.rectTransform, 300f, y, 204f, 44f);
                row.Value.GetComponent<UiTextOutline>().Width = 0f;
                statRows.Add(row);
            }

            UiButton garage = UiStyle.Button(panel.transform, "Garage", "ГАРАЖ", UiStyle.Blue, new Vector2(516f, 92f), () => Show(Page.Garage, true), 42);
            UiLayout.LT(garage.Plate.rectTransform, 22f, 370f, 516f, 92f);

            // Red badge: how many upgrades can be bought right now.
            RectTransform badge = UiKit.CreateRect("Badge", garage.Plate.transform);
            UiLayout.RT(badge, -14f, -18f, 56f, 56f);
            UiPlate badgeBack = UiStyle.Plate(badge, "Back", UiStyle.Red, 999f, 4f, 4f, 0f, 0f);
            UiLayout.Fill(badgeBack.rectTransform);
            garageBadgeText = UiStyle.Label(badge, "Count", "0", 32, Color.white, TextAnchor.MiddleCenter, false);
            UiLayout.Fill(garageBadgeText.rectTransform, 0f, 0f, 0f, 3f);
            garageBadge = badge.gameObject;
        }

        void BuildModeCard(Transform parent)
        {
            modeCard = UiStyle.Button(parent, "Mode", string.Empty, UiStyle.Blue, new Vector2(560f, 168f), () => Show(Page.Modes, true));
            UiLayout.RB(modeCard.Plate.rectTransform, 40f, 252f, 560f, 168f);
            modeCard.Plate.Radius = 22f;
            modeCard.Plate.OutlineWidth = 4f;
            modeCard.Plate.Depth = 9f;
            modeCard.Plate.FaceHeight = 12f;
            modeCard.Plate.HiHeight = 24f;
            modeCard.Label.gameObject.SetActive(false);
            Transform content = modeCard.Content;

            modeTag = Tag(content, "Tag", string.Empty, 20, Color.white, UiStyle.Ink);
            SizeTag(modeTag, 26f, 18f, 250f, 30f);

            modeName = UiStyle.Label(content, "Name", string.Empty, 31, Color.white, TextAnchor.MiddleLeft);
                        UiLayout.LT(modeName.rectTransform, 26f, 54f, 360f, 50f);

            modeMap = UiStyle.Label(content, "Map", string.Empty, 24, Color.white, TextAnchor.MiddleLeft, false);
            UiLayout.LT(modeMap.rectTransform, 26f, 112f, 340f, 34f);

            UiPlate frame = UiStyle.Plate(content, "Frame", UiStyle.Dark, 14f, 4f, 5f, 0f, 0f);
            frame.color = UiStyle.Ink;
            UiLayout.RT(frame.rectTransform, 18f, 22f, 150f, 112f);
            modeThumb = UiKit.CreateImage(frame.transform, "Thumb", Color.white);
            modeThumb.preserveAspect = true;
            UiLayout.Fill(modeThumb.rectTransform, 4f, 4f, 4f, 4f);
        }

        void RefreshLobby()
        {
            if (lobbyRank == null)
                return;

            lobbyRank.text = PlayerProgress.ThreatRank.ToString();

            ProgressionConfig progression = PlayerProgress.Config;
            foreach (StatRow row in statRows)
            {
                int level = PlayerProgress.Level(row.Branch);
                row.Level.text = "Ур. " + level;
                row.Value.text = progression != null ? progression.StatText(row.Branch, level) : "-";
            }

            int affordable = 0;
            for (int i = 0; i < PlayerProgress.BranchCount; i++)
            {
                if (PlayerProgress.CanAfford((UpgradeBranch)i))
                    affordable++;
            }

            garageBadge.SetActive(affordable > 0);
            garageBadgeText.text = affordable.ToString();

            MatchModeConfig mode = SelectedMode;
            if (mode != null)
            {
                modeCard.SetTone(ToneOf(mode.kind));
                modeTag.text = "РЕЖИМ · " + FormatOf(mode.kind);
                modeName.text = mode.displayName;
            }

            SceneEntry map = SelectedMap;
            modeMap.text = map.label;
            UiTheme theme = UiKit.Theme;
            Sprite preview = theme != null ? theme.PreviewOf(map.sceneName) : null;
            modeThumb.sprite = preview;
            modeThumb.enabled = preview != null;
            playButton.interactable = mode != null;
        }

        // The lobby starts the chosen mode on the chosen map at the chosen difficulty (one that is not open yet falls back to the best open one).
        void StartFromLobby()
        {
            MatchModeConfig mode = SelectedMode;
            if (mode == null)
                return;

            int index = (int)mode.kind;
            int difficulty = DifficultyOf(index);
            if (!PlayerProgress.IsDifficultyUnlocked(index, difficulty))
                difficulty = Mathf.Min(PlayerProgress.HighestUnlockedDifficulty(index), DifficultyConfig.Count - 1);

            Launch(mode, difficulty);
        }

        void Launch(MatchModeConfig config, int difficulty)
        {
            string scene = SelectedMap.sceneName;
            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                Debug.LogError($"Scene '{scene}' is not in Build Settings.");
                return;
            }

            MatchSession.Selected = config;
            MatchSession.MapScene = scene;
            DifficultyConfig.Selected = difficulty;
            SceneManager.LoadScene(scene);
        }
    }
}
