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
    /// The whole menu, built in code in the style of tanks_art_style.html (see <see cref="UiStyle"/>): the lobby with the tank on a
    /// podium, its stats, the chosen mode and map and the big start button; the garage with the upgrades; the mode and map screen;
    /// settings and the reset question as pop-ups; the debug list of test scenes (reached from the settings). The back key walks
    /// one step back (SceneNavigator asks <see cref="HandleBack"/>). The code is split by screen: MenuScreens.Lobby.cs,
    /// .Garage.cs, .Modes.cs, .Settings.cs.
    /// </summary>
    public partial class MenuScreens : MonoBehaviour
    {
        [Serializable]
        public struct SceneEntry
        {
            public string label;
            public string sceneName;
        }

        enum Page
        {
            Lobby,
            Garage,
            Modes,
            Debug
        }

        /// <summary>Set by the result screen's "Upgrades" button: the menu opens straight on the garage.</summary>
        public static bool OpenUpgradesOnStart;

        const int Order = 10;
        const int DebugGift = 500;
        const float GrassPeriod = 384f;
        const float GrassSpeed = 14f;

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

        readonly Dictionary<Page, RectTransform> pages = new Dictionary<Page, RectTransform>();
        readonly Stack<Page> history = new Stack<Page>();
        readonly List<RectTransform> swaying = new List<RectTransform>();
        readonly List<RectTransform> spinning = new List<RectTransform>();
        RectTransform safe;
        Page current;
        RawImage grass;
        UiButton backButton;
        Text walletText;

        void Awake()
        {
            // Menu scene may keep an old canvas around; ours sits above it.
            UiKit.EnsureEventSystem();
        }

        void Start()
        {
            Canvas canvas = UiKit.CreateCanvas("MenuCanvas", Order, transform);
            BuildBackground(canvas.transform);

            safe = UiKit.CreateSafeArea(canvas.transform);
            CollectMaps();
            BuildLobby();
            BuildGarage();
            BuildModes();
            BuildDebug();
            BuildChrome();
            BuildSettings();
            BuildConfirm();

            PlayerProgress.Changed += OnProgressChanged;
            SceneNavigator.BackHandler = HandleBack;

            Show(Page.Lobby, false);
            if (OpenUpgradesOnStart)
            {
                OpenUpgradesOnStart = false;
                Show(Page.Garage, true);
            }

            RefreshAll();
        }

        void OnDestroy()
        {
            PlayerProgress.Changed -= OnProgressChanged;
            if (SceneNavigator.BackHandler == (Func<bool>)HandleBack)
                SceneNavigator.BackHandler = null;
        }

        void Update()
        {
            float time = Time.unscaledTime;
            if (grass != null)
            {
                Rect size = grass.rectTransform.rect;
                float shift = time * GrassSpeed / GrassPeriod;
                grass.uvRect = new Rect(-shift, shift, size.width / GrassPeriod, size.height / GrassPeriod);
            }

            // The tank on the podium sways like in the demo, the dashed ring turns slowly.
            float sway = Mathf.Lerp(-14f, 14f, Mathf.SmoothStep(0f, 1f, Mathf.PingPong(time / 3.2f, 1f)));
            for (int i = 0; i < swaying.Count; i++)
                swaying[i].localRotation = Quaternion.Euler(0f, 0f, sway);

            float spin = -time * 9f;
            for (int i = 0; i < spinning.Count; i++)
                spinning[i].localRotation = Quaternion.Euler(0f, 0f, spin);
        }

        // ---- Navigation ----

        void Show(Page page, bool remember)
        {
            if (remember)
                history.Push(current);
            else
                history.Clear();

            Apply(page);
        }

        void Back()
        {
            if (history.Count == 0)
                return;

            Apply(history.Pop());
        }

        void Apply(Page page)
        {
            current = page;
            foreach (KeyValuePair<Page, RectTransform> pair in pages)
                pair.Value.gameObject.SetActive(pair.Key == page);

            backButton.gameObject.SetActive(page != Page.Lobby);
            RefreshAll();
        }

        /// <summary>Back key: closes a pop-up, otherwise one screen back. Returns false on the lobby (then the app quits).</summary>
        bool HandleBack()
        {
            if (confirmModal != null && confirmModal.activeSelf)
            {
                confirmModal.SetActive(false);
                AudioManager.Play(Sfx.UiBack);
                return true;
            }

            if (settingsModal != null && settingsModal.activeSelf)
            {
                settingsModal.SetActive(false);
                AudioManager.Play(Sfx.UiBack);
                return true;
            }

            if (current == Page.Lobby)
                return false;

            AudioManager.Play(Sfx.UiBack);
            Back();
            return true;
        }

        RectTransform NewPage(Page page)
        {
            RectTransform rect = UiKit.CreateRect(page.ToString(), safe);
            UiKit.Stretch(rect);
            pages[page] = rect;
            return rect;
        }

        // ---- Background and chrome ----

        void BuildBackground(Transform canvas)
        {
            UiTheme theme = UiKit.Theme;
            Texture2D tile = theme != null ? theme.grassTile : null;
            RectTransform rect = UiKit.CreateRect("Background", canvas);
            UiKit.Stretch(rect);
            grass = rect.gameObject.AddComponent<RawImage>();
            grass.raycastTarget = true;
            grass.color = tile != null ? Color.white : UiStyle.Grass;
            grass.texture = tile;
        }

        // The back button, the gear wallet and the settings button float above every page.
        void BuildChrome()
        {
            Sprite backIcon = UiKit.Theme != null ? UiKit.Theme.backIcon : null;
            backButton = UiStyle.IconButton(safe, "Back", backIcon, 104f, () => { AudioManager.Play(Sfx.UiBack); Back(); });
            UiLayout.LT(backButton.Plate.rectTransform, 40f, 30f, 104f, 104f);

            RectTransform wallet = UiKit.CreateRect("Wallet", safe);
            UiLayout.RT(wallet, 164f, 36f, 330f, 88f);
            UiPlate pill = UiStyle.Pill(wallet, "Pill", UiStyle.Ink);
            UiLayout.Fill(pill.rectTransform);

            Image icon = UiStyle.Icon(wallet, "Gear", UiKit.Theme != null ? UiKit.Theme.gear : null, 68f);
            UiLayout.LT(icon.rectTransform, 12f, 10f, 68f, 68f);
            walletText = UiStyle.Label(wallet, "Amount", "0", 50, Color.white, TextAnchor.MiddleRight, false);
            UiLayout.Fill(walletText.rectTransform, 90f, 0f, 30f, 4f);

            Sprite settingsIcon = UiKit.Theme != null ? UiKit.Theme.settingsIcon : null;
            UiButton settings = UiStyle.IconButton(safe, "Settings", settingsIcon, 104f, OpenSettings);
            UiLayout.RT(settings.Plate.rectTransform, 40f, 30f, 104f, 104f);
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
            RefreshLobby();
            RefreshGarage();
            RefreshModes();
        }

        // ---- Small shared pieces ----

        static UiPlate Disc(Transform parent, string name, float diameter, Color color)
        {
            UiPlate disc = UiStyle.Pill(parent, name, color);
            UiLayout.Mid(disc.rectTransform, 0f, 0f, diameter, diameter);
            return disc;
        }

        // A dark capsule with a short text, e.g. "РЕЖИМ · 3 × 3".
        static Text Tag(Transform parent, string name, string text, int size, Color textColor, Color fill)
        {
            UiPlate pill = UiStyle.Pill(parent, name, fill);
            Text label = UiStyle.Plain(pill.transform, "Text", text, size, textColor, TextAnchor.MiddleCenter);
            UiLayout.Fill(label.rectTransform);
            return label;
        }

        static void SizeTag(Text label, float x, float y, float width, float height)
        {
            UiLayout.LT((RectTransform)label.transform.parent, x, y, width, height);
        }
    }
}
