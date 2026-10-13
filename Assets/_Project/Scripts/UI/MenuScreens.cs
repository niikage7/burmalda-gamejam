using System;
using System.Collections.Generic;
using TankBrawl.Audio;
using TankBrawl.Match;
using TankBrawl.Progress;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TankBrawl.UI
{
    /// <summary>
    /// The whole menu, built in code: main screen, mode picker, upgrades, settings (with the "Debug" section that holds the
    /// old test scenes) and a reset confirmation. The back key walks one screen back (SceneNavigator asks <see cref="HandleBack"/>).
    /// Art comes from <see cref="UiTheme"/> when it has any; without it the screens are flat coloured rectangles.
    /// </summary>
    public class MenuScreens : MonoBehaviour
    {
        [Serializable]
        public struct SceneEntry
        {
            public string label;
            public string sceneName;
        }

        enum Screen
        {
            Main,
            Modes,
            Upgrades,
            Settings,
            Debug
        }

        /// <summary>Set by the result screen's "Upgrades" button: the menu opens straight on the upgrade screen.</summary>
        public static bool OpenUpgradesOnStart;

        const int Order = 10;
        const int DebugGift = 500;

        [Tooltip("The three modes in the order of MatchModeKind: Team, Point, Deathmatch.")]
        [SerializeField] MatchModeConfig[] modes = new MatchModeConfig[3];

        [Tooltip("The arena scene a match is played in when no map from the list below is available.")]
        [SerializeField] string arenaScene = "Arena";

        [Tooltip("Maps offered on the mode screen, in order. A map whose scene is not in the Build Settings is not shown. Scenes of the new maps are built by TankBrawl / Maps / Build New Maps.")]
        [SerializeField] SceneEntry[] maps =
        {
            new SceneEntry { label = "Арена", sceneName = "Arena" },
            new SceneEntry { label = "Перекрёсток", sceneName = "Crossroads" },
            new SceneEntry { label = "Река", sceneName = "River" },
            new SceneEntry { label = "Лабиринт", sceneName = "Maze" }
        };

        [Tooltip("Test scenes listed under Settings > Debug.")]
        [SerializeField] SceneEntry[] debugScenes;

        readonly Dictionary<Screen, RectTransform> screens = new Dictionary<Screen, RectTransform>();
        readonly Stack<Screen> history = new Stack<Screen>();
        RectTransform safe;
        Screen current;
        Text walletText;
        GameObject walletBox;
        Text statsText;
        GameObject confirmReset;
        Button backButton;
        readonly List<UpgradeCard> upgradeCards = new List<UpgradeCard>();
        Text threatText;
        readonly List<SceneEntry> availableMaps = new List<SceneEntry>();
        int mapIndex;
        Text mapLabel;

        sealed class UpgradeCard
        {
            public UpgradeBranch Branch;
            public Text Level;
            public Text Stat;
            public Text Next;
            public Button Buy;
            public Text BuyLabel;
            public Image Icon;
        }

        void Awake()
        {
            // Menu scene may keep an old canvas around; ours sits above it.
            UiKit.EnsureEventSystem();
        }

        void Start()
        {
            Canvas canvas = UiKit.CreateCanvas("MenuCanvas", Order, transform);
            UiTheme theme = UiKit.Theme;

            Image background = UiKit.CreateImage(canvas.transform, "Background", new Color(0.07f, 0.09f, 0.13f, 1f), theme != null ? theme.menuBackground : null, true);
            UiKit.Stretch(background.rectTransform);
            background.preserveAspect = false;

            safe = UiKit.CreateSafeArea(canvas.transform);
            BuildMain();
            BuildModes();
            BuildUpgrades();
            BuildSettings();
            BuildDebug();
            BuildChrome();
            BuildConfirm();

            PlayerProgress.Changed += OnProgressChanged;
            SceneNavigator.BackHandler = HandleBack;

            Show(Screen.Main, false);
            if (OpenUpgradesOnStart)
            {
                OpenUpgradesOnStart = false;
                Show(Screen.Upgrades, true);
            }

            RefreshAll();
        }

        void OnDestroy()
        {
            PlayerProgress.Changed -= OnProgressChanged;
            if (SceneNavigator.BackHandler == (Func<bool>)HandleBack)
                SceneNavigator.BackHandler = null;
        }

        // ---- Navigation ----

        void Show(Screen screen, bool remember)
        {
            if (remember)
                history.Push(current);
            else
                history.Clear();

            current = screen;
            foreach (KeyValuePair<Screen, RectTransform> pair in screens)
                pair.Value.gameObject.SetActive(pair.Key == screen);

            backButton.gameObject.SetActive(screen != Screen.Main);
            RefreshAll();
        }

        void Back()
        {
            if (confirmReset.activeSelf)
            {
                confirmReset.SetActive(false);
                return;
            }

            if (history.Count == 0)
                return;

            Screen previous = history.Pop();
            current = previous;
            foreach (KeyValuePair<Screen, RectTransform> pair in screens)
                pair.Value.gameObject.SetActive(pair.Key == previous);
            backButton.gameObject.SetActive(previous != Screen.Main);
            RefreshAll();
        }

        /// <summary>Back key: one screen back. Returns false on the main screen (then the app quits).</summary>
        bool HandleBack()
        {
            if (confirmReset != null && confirmReset.activeSelf)
            {
                confirmReset.SetActive(false);
                AudioManager.Play(Sfx.UiBack);
                return true;
            }

            if (current == Screen.Main)
                return false;

            AudioManager.Play(Sfx.UiBack);
            Back();
            return true;
        }

        RectTransform NewScreen(Screen screen)
        {
            RectTransform rect = UiKit.CreateRect(screen.ToString(), safe);
            UiKit.Stretch(rect);
            screens[screen] = rect;
            return rect;
        }

        // ---- Main ----

        void BuildMain()
        {
            RectTransform root = NewScreen(Screen.Main);
            UiTheme theme = UiKit.Theme;

            if (theme != null && theme.logo != null)
            {
                Image logo = UiKit.CreateImage(root, "Logo", Color.white, theme.logo);
                logo.preserveAspect = true;
                UiKit.Place(logo.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(900f, 300f));
                logo.rectTransform.pivot = new Vector2(0.5f, 1f);
            }
            else
            {
                string gameName = theme != null ? theme.gameName : "TANK BRAWL";
                Text title = UiKit.CreateText(root, "Title", gameName, 150, UiKit.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
                UiKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(1500f, 200f));
                title.rectTransform.pivot = new Vector2(0.5f, 1f);
            }

            Text subtitle = UiKit.CreateText(root, "Subtitle", "танковая арена", 40, UiKit.Muted, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -260f), new Vector2(900f, 56f));
            subtitle.rectTransform.pivot = new Vector2(0.5f, 1f);

            MenuButton(root, "Play", "ИГРАТЬ", UiKit.ButtonGreen, 150f, 560f, 130f, () => Show(Screen.Modes, true));
            MenuButton(root, "Upgrades", "УЛУЧШЕНИЯ", UiKit.ButtonBlue, 25f, 560f, 110f, () => Show(Screen.Upgrades, true));
            MenuButton(root, "Settings", "НАСТРОЙКИ", UiKit.ButtonGray, -90f, 560f, 110f, () => Show(Screen.Settings, true));

#if !UNITY_WEBGL
            MenuButton(root, "Quit", "ВЫХОД", UiKit.ButtonRed, -205f, 560f, 90f, () => Application.Quit());
#endif

            statsText = UiKit.CreateText(root, "Stats", string.Empty, 28, UiKit.Muted, TextAnchor.LowerCenter, FontStyle.Normal);
            UiKit.Place(statsText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(1400f, 44f));
            statsText.rectTransform.pivot = new Vector2(0.5f, 0f);
        }

        static void MenuButton(Transform parent, string name, string label, Color color, float yFromCentre, float width, float height, UnityEngine.Events.UnityAction onClick)
        {
            Button button = UiKit.CreateButton(parent, name, label, color, new Vector2(width, height), onClick, 52);
            UiKit.Place(button.image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, yFromCentre), new Vector2(width, height));
        }

        // ---- Modes ----

        void BuildModes()
        {
            RectTransform root = NewScreen(Screen.Modes);
            Header(root, "ВЫБЕРИТЕ РЕЖИМ");

            Color[] headerColors = { new Color(0.85f, 0.35f, 0.3f, 1f), new Color(0.95f, 0.75f, 0.2f, 1f), new Color(0.3f, 0.7f, 0.4f, 1f) };
            UiTheme theme = UiKit.Theme;
            float cardWidth = 540f;
            float gap = 40f;
            float x = -(cardWidth + gap);

            for (int i = 0; i < modes.Length; i++)
            {
                MatchModeConfig config = modes[i];
                Image card = UiKit.CreatePanel(root, "Mode" + i, UiKit.PanelColor);
                UiKit.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x, -40f), new Vector2(cardWidth, 640f));

                Image stripe = UiKit.CreateImage(card.transform, "Stripe", headerColors[i % headerColors.Length]);
                stripe.rectTransform.anchorMin = new Vector2(0f, 1f);
                stripe.rectTransform.anchorMax = new Vector2(1f, 1f);
                stripe.rectTransform.pivot = new Vector2(0.5f, 1f);
                stripe.rectTransform.sizeDelta = new Vector2(0f, 14f);
                stripe.rectTransform.anchoredPosition = Vector2.zero;

                Sprite icon = theme != null && theme.modeIcons != null && i < theme.modeIcons.Length ? theme.modeIcons[i] : null;
                if (icon != null)
                {
                    Image picture = UiKit.CreateImage(card.transform, "Icon", Color.white, icon);
                    picture.preserveAspect = true;
                    UiKit.Place(picture.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(300f, 200f));
                    picture.rectTransform.pivot = new Vector2(0.5f, 1f);
                }

                string modeName = config != null ? config.displayName : "—";
                string description = config != null ? config.description : string.Empty;
                Text name = UiKit.CreateText(card.transform, "Name", modeName, 48, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                UiKit.Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, icon != null ? -250f : -60f), new Vector2(cardWidth - 40f, 120f));
                name.rectTransform.pivot = new Vector2(0.5f, 1f);
                name.horizontalOverflow = HorizontalWrapMode.Wrap;

                Text text = UiKit.CreateText(card.transform, "Description", description, 30, UiKit.Muted, TextAnchor.UpperCenter, FontStyle.Normal, false);
                UiKit.Place(text.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, icon != null ? -390f : -200f), new Vector2(cardWidth - 70f, 250f));
                text.rectTransform.pivot = new Vector2(0.5f, 1f);
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Truncate;

                MatchModeConfig picked = config;
                Button play = UiKit.CreateButton(card.transform, "Play", "В БОЙ", UiKit.ButtonGreen, new Vector2(cardWidth - 80f, 100f), () => StartMatch(picked), 46);
                UiKit.Place(play.image.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(cardWidth - 80f, 100f));
                play.image.rectTransform.pivot = new Vector2(0.5f, 0f);
                play.interactable = config != null;

                x += cardWidth + gap;
            }

            BuildMapPicker(root);

            Text reward = UiKit.CreateText(root, "Reward", string.Empty, 30, UiKit.Muted, TextAnchor.MiddleCenter, FontStyle.Normal);
            UiKit.Place(reward.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(1500f, 44f));
            reward.rectTransform.pivot = new Vector2(0.5f, 0f);
            ProgressionConfig progression = PlayerProgress.Config;
            if (progression != null)
                reward.text = "За каждого уничтоженного врага +" + progression.gearsPerKill + ", за победу +" + progression.gearsPerWin + " шестерёнок";
        }

        // "< КАРТА: name >" under the mode cards; shown only when there is more than one map to choose from.
        void BuildMapPicker(Transform root)
        {
            CollectMaps();
            if (availableMaps.Count < 2)
                return;

            Button previous = UiKit.CreateButton(root, "MapPrevious", "<", UiKit.ButtonGray, new Vector2(110f, 80f), () => ShiftMap(-1), 48);
            UiKit.Place(previous.image.rectTransform, new Vector2(0.5f, 0f), new Vector2(-400f, 84f), new Vector2(110f, 80f));

            mapLabel = UiKit.CreateText(root, "Map", string.Empty, 40, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place(mapLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 84f), new Vector2(640f, 80f));

            Button next = UiKit.CreateButton(root, "MapNext", ">", UiKit.ButtonGray, new Vector2(110f, 80f), () => ShiftMap(1), 48);
            UiKit.Place(next.image.rectTransform, new Vector2(0.5f, 0f), new Vector2(400f, 84f), new Vector2(110f, 80f));

            ShowMap();
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

        void ShiftMap(int step)
        {
            mapIndex = (mapIndex + step + availableMaps.Count) % availableMaps.Count;
            MatchSession.MapScene = availableMaps[mapIndex].sceneName;
            ShowMap();
        }

        void ShowMap()
        {
            if (mapLabel != null)
                mapLabel.text = "КАРТА:  " + availableMaps[mapIndex].label;
        }

        void StartMatch(MatchModeConfig config)
        {
            if (config == null)
                return;

            string scene = availableMaps.Count > 0 ? availableMaps[mapIndex].sceneName : arenaScene;
            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                Debug.LogError($"Scene '{scene}' is not in Build Settings.");
                return;
            }

            MatchSession.Selected = config;
            MatchSession.MapScene = scene;
            SceneManager.LoadScene(scene);
        }

        // ---- Upgrades ----

        void BuildUpgrades()
        {
            RectTransform root = NewScreen(Screen.Upgrades);
            Header(root, "УЛУЧШЕНИЯ");

            ProgressionConfig progression = PlayerProgress.Config;
            float cardWidth = 340f;
            float gap = 24f;
            float x = -2f * (cardWidth + gap);

            for (int i = 0; i < PlayerProgress.BranchCount; i++)
            {
                var branch = (UpgradeBranch)i;
                var card = new UpgradeCard { Branch = branch };

                Image panel = UiKit.CreatePanel(root, "Card" + branch, UiKit.PanelColor);
                UiKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x, -30f), new Vector2(cardWidth, 650f));

                ProgressionConfig.Branch info = progression != null && progression.branches != null && i < progression.branches.Length ? progression.branches[i] : null;
                string branchName = info != null ? info.displayName : branch.ToString();

                Text title = UiKit.CreateText(panel.transform, "Title", branchName, 32, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                UiKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(cardWidth - 20f, 90f));
                title.rectTransform.pivot = new Vector2(0.5f, 1f);
                title.horizontalOverflow = HorizontalWrapMode.Wrap;

                card.Icon = UiKit.CreateImage(panel.transform, "Icon", info != null && info.icon != null ? Color.white : BranchColor(branch), info != null ? info.icon : null);
                card.Icon.preserveAspect = true;
                UiKit.Place(card.Icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(120f, 120f));
                card.Icon.rectTransform.pivot = new Vector2(0.5f, 1f);

                card.Level = UiKit.CreateText(panel.transform, "Level", string.Empty, 34, UiKit.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
                UiKit.Place(card.Level.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -260f), new Vector2(cardWidth - 20f, 46f));
                card.Level.rectTransform.pivot = new Vector2(0.5f, 1f);

                card.Stat = UiKit.CreateText(panel.transform, "Stat", string.Empty, 34, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                UiKit.Place(card.Stat.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -320f), new Vector2(cardWidth - 20f, 46f));
                card.Stat.rectTransform.pivot = new Vector2(0.5f, 1f);

                card.Next = UiKit.CreateText(panel.transform, "Next", string.Empty, 28, UiKit.Good, TextAnchor.MiddleCenter, FontStyle.Normal);
                UiKit.Place(card.Next.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -372f), new Vector2(cardWidth - 20f, 40f));
                card.Next.rectTransform.pivot = new Vector2(0.5f, 1f);

                Text description = UiKit.CreateText(panel.transform, "Description", info != null ? info.description : string.Empty, 24, UiKit.Muted, TextAnchor.UpperCenter, FontStyle.Normal, false);
                UiKit.Place(description.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -425f), new Vector2(cardWidth - 40f, 110f));
                description.rectTransform.pivot = new Vector2(0.5f, 1f);
                description.horizontalOverflow = HorizontalWrapMode.Wrap;
                description.verticalOverflow = VerticalWrapMode.Truncate;

                UpgradeBranch captured = branch;
                card.Buy = UiKit.CreateButton(panel.transform, "Buy", string.Empty, UiKit.ButtonGreen, new Vector2(cardWidth - 50f, 90f), () => Buy(captured), 40);
                UiKit.Place(card.Buy.image.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(cardWidth - 50f, 90f));
                card.Buy.image.rectTransform.pivot = new Vector2(0.5f, 0f);
                card.BuyLabel = card.Buy.GetComponentInChildren<Text>();

                upgradeCards.Add(card);
                x += cardWidth + gap;
            }

            threatText = UiKit.CreateText(root, "Threat", string.Empty, 28, UiKit.Muted, TextAnchor.MiddleCenter, FontStyle.Normal);
            UiKit.Place(threatText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(1700f, 70f));
            threatText.rectTransform.pivot = new Vector2(0.5f, 0f);
            threatText.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        static Color BranchColor(UpgradeBranch branch)
        {
            switch (branch)
            {
                case UpgradeBranch.Hull: return new Color(0.4f, 0.8f, 0.45f, 1f);
                case UpgradeBranch.Engine: return new Color(0.95f, 0.6f, 0.25f, 1f);
                case UpgradeBranch.Weapon: return new Color(0.9f, 0.35f, 0.3f, 1f);
                case UpgradeBranch.FireRate: return new Color(0.95f, 0.85f, 0.3f, 1f);
                default: return new Color(0.4f, 0.65f, 1f, 1f);
            }
        }

        void Buy(UpgradeBranch branch)
        {
            if (PlayerProgress.TryBuy(branch))
                AudioManager.Play(Sfx.UiBuy);
            else
                AudioManager.Play(Sfx.UiDenied);
        }

        void RefreshUpgrades()
        {
            ProgressionConfig progression = PlayerProgress.Config;
            if (progression == null)
                return;

            for (int i = 0; i < upgradeCards.Count; i++)
            {
                UpgradeCard card = upgradeCards[i];
                int level = PlayerProgress.Level(card.Branch);
                card.Level.text = "Уровень " + level;
                card.Stat.text = progression.StatText(card.Branch, level);
                card.Next.text = "→ " + progression.StatText(card.Branch, level + 1);

                int cost = PlayerProgress.NextCost(card.Branch);
                bool canAfford = PlayerProgress.CanAfford(card.Branch);
                card.BuyLabel.text = "КУПИТЬ  " + cost;
                card.Buy.image.color = canAfford ? UiKit.ButtonGreen : UiKit.ButtonGray;
                card.BuyLabel.color = canAfford ? Color.white : UiKit.Bad;
            }

            int rank = PlayerProgress.ThreatRank;
            threatText.text = "Ранг угрозы: " + rank + ". Каждое купленное улучшение делает и вас, и ботов немного сильнее: у ботов +" + (4 * rank) + " HP, +" + Mathf.RoundToInt(0.5f * rank) + " урона.";
        }

        // ---- Settings and debug ----

        void BuildSettings()
        {
            RectTransform root = NewScreen(Screen.Settings);
            Header(root, "НАСТРОЙКИ");

            Image panel = UiKit.CreatePanel(root, "Panel", UiKit.PanelColor);
            UiKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(1100f, 620f));

            SliderRow(panel.transform, "Звук", -30f, PlayerProgress.SfxVolume, value => PlayerProgress.SfxVolume = value);
            SliderRow(panel.transform, "Музыка", -140f, PlayerProgress.MusicVolume, value => PlayerProgress.MusicVolume = value);

            Text vibrationLabel = UiKit.CreateText(panel.transform, "VibrationLabel", "Вибрация", 40, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Place(vibrationLabel.rectTransform, new Vector2(0f, 1f), new Vector2(50f, -250f), new Vector2(300f, 70f));
            vibrationLabel.rectTransform.pivot = new Vector2(0f, 1f);

            Button vibration = null;
            vibration = UiKit.CreateButton(panel.transform, "Vibration", PlayerProgress.Vibration ? "ВКЛ" : "ВЫКЛ", PlayerProgress.Vibration ? UiKit.ButtonGreen : UiKit.ButtonGray, new Vector2(240f, 80f), () =>
            {
                PlayerProgress.Vibration = !PlayerProgress.Vibration;
                UiKit.SetLabel(vibration, PlayerProgress.Vibration ? "ВКЛ" : "ВЫКЛ");
                vibration.image.color = PlayerProgress.Vibration ? UiKit.ButtonGreen : UiKit.ButtonGray;
            }, 38);
            UiKit.Place(vibration.image.rectTransform, new Vector2(1f, 1f), new Vector2(-70f, -245f), new Vector2(240f, 80f));
            vibration.image.rectTransform.pivot = new Vector2(1f, 1f);

            Button reset = UiKit.CreateButton(panel.transform, "Reset", "СБРОСИТЬ ПРОГРЕСС", UiKit.ButtonRed, new Vector2(500f, 90f), () => confirmReset.SetActive(true), 36);
            UiKit.Place(reset.image.rectTransform, new Vector2(0f, 0f), new Vector2(50f, 40f), new Vector2(500f, 90f));
            reset.image.rectTransform.pivot = new Vector2(0f, 0f);

            Button debug = UiKit.CreateButton(panel.transform, "Debug", "ОТЛАДКА", UiKit.ButtonGray, new Vector2(400f, 90f), () => Show(Screen.Debug, true), 38);
            UiKit.Place(debug.image.rectTransform, new Vector2(1f, 0f), new Vector2(-50f, 40f), new Vector2(400f, 90f));
            debug.image.rectTransform.pivot = new Vector2(1f, 0f);
        }

        static void SliderRow(Transform parent, string label, float yFromTop, float value, UnityEngine.Events.UnityAction<float> onChanged)
        {
            Text text = UiKit.CreateText(parent, label + "Label", label, 40, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Place(text.rectTransform, new Vector2(0f, 1f), new Vector2(50f, yFromTop), new Vector2(300f, 70f));
            text.rectTransform.pivot = new Vector2(0f, 1f);

            Slider slider = UiKit.CreateSlider(parent, label + "Slider", new Vector2(640f, 36f), value, onChanged);
            UiKit.Place((RectTransform)slider.transform, new Vector2(1f, 1f), new Vector2(-70f, yFromTop - 17f), new Vector2(640f, 36f));
            ((RectTransform)slider.transform).pivot = new Vector2(1f, 1f);
        }

        void BuildDebug()
        {
            RectTransform root = NewScreen(Screen.Debug);
            Header(root, "ОТЛАДКА");

            int count = debugScenes != null ? debugScenes.Length : 0;
            int columns = 3;
            float width = 520f;
            float height = 100f;
            float gapX = 30f;
            float gapY = 24f;
            for (int i = 0; i < count; i++)
            {
                int column = i % columns;
                int row = i / columns;
                float x = (column - (columns - 1) * 0.5f) * (width + gapX);
                float y = 200f - row * (height + gapY);

                string sceneName = debugScenes[i].sceneName;
                bool available = Application.CanStreamedLevelBeLoaded(sceneName);
                string label = available ? debugScenes[i].label : debugScenes[i].label + "  (нет в Build Settings)";
                Button button = UiKit.CreateButton(root, "Scene_" + sceneName, label, available ? UiKit.ButtonBlue : UiKit.ButtonGray, new Vector2(width, height), () => SceneManager.LoadScene(sceneName), 32);
                UiKit.Place(button.image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(width, height));
                button.interactable = available;
            }

            Button gift = UiKit.CreateButton(root, "Gift", "+" + DebugGift + " шестерёнок", UiKit.ButtonGreen, new Vector2(width, height), () =>
            {
                PlayerProgress.AddGears(DebugGift);
                AudioManager.Play(Sfx.GearGain);
            }, 34);
            UiKit.Place(gift.image.rectTransform, new Vector2(0.5f, 0f), new Vector2(-(width + gapX) * 0.5f, 50f), new Vector2(width, height));
            gift.image.rectTransform.pivot = new Vector2(0.5f, 0f);

            Button reset = UiKit.CreateButton(root, "ResetFromDebug", "Сбросить прогресс", UiKit.ButtonRed, new Vector2(width, height), () => confirmReset.SetActive(true), 34);
            UiKit.Place(reset.image.rectTransform, new Vector2(0.5f, 0f), new Vector2((width + gapX) * 0.5f, 50f), new Vector2(width, height));
            reset.image.rectTransform.pivot = new Vector2(0.5f, 0f);
        }

        // ---- Common ----

        void Header(Transform parent, string text)
        {
            Text title = UiKit.CreateText(parent, "Header", text, 72, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(1200f, 100f));
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
        }

        // The back button and the wallet float above every screen.
        void BuildChrome()
        {
            backButton = UiKit.CreateButton(safe, "Back", "‹ НАЗАД", UiKit.ButtonGray, new Vector2(260f, 90f), () => { Back(); }, 38);
            UiKit.Place(backButton.image.rectTransform, new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(260f, 90f));
            backButton.image.rectTransform.pivot = new Vector2(0f, 1f);

            Image box = UiKit.CreatePanel(safe, "Wallet", UiKit.PanelColor, false);
            UiKit.Place(box.rectTransform, new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(330f, 90f));
            box.rectTransform.pivot = new Vector2(1f, 1f);
            walletBox = box.gameObject;

            Image icon = UiKit.CreateGearIcon(box.transform, 46f);
            UiKit.Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(44f, 0f), new Vector2(46f, 46f));

            walletText = UiKit.CreateText(box.transform, "Amount", "0", 52, UiKit.Accent, TextAnchor.MiddleRight, FontStyle.Bold);
            UiKit.Place(walletText.rectTransform, new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(230f, 80f));
            walletText.rectTransform.pivot = new Vector2(1f, 0.5f);
        }

        void BuildConfirm()
        {
            Canvas canvas = UiKit.CreateCanvas("ConfirmCanvas", Order + 5, transform);
            confirmReset = new GameObject("Confirm", typeof(RectTransform));
            confirmReset.transform.SetParent(canvas.transform, false);
            UiKit.Stretch((RectTransform)confirmReset.transform);

            Image dim = UiKit.CreateImage(confirmReset.transform, "Dim", UiKit.Dim, null, true);
            UiKit.Stretch(dim.rectTransform);

            Image panel = UiKit.CreatePanel(confirmReset.transform, "Panel", UiKit.PanelColor);
            UiKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 440f));

            Text text = UiKit.CreateText(panel.transform, "Text", "Сбросить шестерёнки, улучшения и статистику?\nЭто нельзя отменить.", 40, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place(text.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(820f, 200f));
            text.rectTransform.pivot = new Vector2(0.5f, 1f);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;

            Button yes = UiKit.CreateButton(panel.transform, "Yes", "СБРОСИТЬ", UiKit.ButtonRed, new Vector2(360f, 100f), () =>
            {
                PlayerProgress.ResetProgress();
                confirmReset.SetActive(false);
            }, 40);
            UiKit.Place(yes.image.rectTransform, new Vector2(0.5f, 0f), new Vector2(-200f, 40f), new Vector2(360f, 100f));
            yes.image.rectTransform.pivot = new Vector2(0.5f, 0f);

            Button no = UiKit.CreateButton(panel.transform, "No", "ОТМЕНА", UiKit.ButtonGray, new Vector2(360f, 100f), () => confirmReset.SetActive(false), 40);
            UiKit.Place(no.image.rectTransform, new Vector2(0.5f, 0f), new Vector2(200f, 40f), new Vector2(360f, 100f));
            no.image.rectTransform.pivot = new Vector2(0.5f, 0f);

            confirmReset.SetActive(false);
        }

        void OnProgressChanged()
        {
            RefreshAll();
        }

        void RefreshAll()
        {
            if (walletText == null)
                return;

            walletText.text = PlayerProgress.Gears.ToString();
            RefreshUpgrades();
            if (statsText != null)
                statsText.text = "Матчей: " + PlayerProgress.MatchesPlayed + "   ·   Побед: " + PlayerProgress.MatchesWon + "   ·   Ранг угрозы: " + PlayerProgress.ThreatRank;
        }
    }
}
