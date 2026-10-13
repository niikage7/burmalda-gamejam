using TankBrawl.Input;
using TankBrawl.PowerUps;
using TankBrawl.Tank;
using TankBrawl.Weapons;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TankBrawl.UI
{
    /// <summary>
    /// Root of the on-screen controls (stick, fire / nitro / ammo buttons, back button, power-up timer).
    /// The stick and buttons are On-Screen Stick / Button components that emulate a gamepad,
    /// so PlayerTankInput reads them through the normal actions.
    /// Shown on mobile platforms; in the editor only when <see cref="showTouchInEditor"/> is on.
    /// The stick's invisible touch zone is trimmed to the safe area: past its edge lives the system back gesture.
    /// </summary>
    public class TouchControls : MonoBehaviour
    {
        [Tooltip("Show the controls in the editor / on desktop (for layout work). Always shown on mobile platforms.")]
        [SerializeField] bool showTouchInEditor;

        [Tooltip("Everything that is visible: hidden as a whole when the controls are off.")]
        [SerializeField] GameObject content;

        [SerializeField] Button backButton;

        [Tooltip("Placeholder for the power-up timer (bottom center). Hidden until power-ups exist.")]
        [SerializeField] Text powerUpTimerText;

        [Tooltip("Label of the ammo button: shows the selected projectile type.")]
        [SerializeField] Text ammoLabel;

        [Header("Nitro ring around the nitro button")]
        [Tooltip("Radial-filled ring (Image, Filled / Radial 360): how much nitro is left. Empty = no ring.")]
        [SerializeField] Image nitroFill;

        [Tooltip("The same ring as a dark track behind the fill (optional).")]
        [SerializeField] Image nitroTrack;

        [Tooltip("Fill colour when the nitro is nearly empty.")]
        [SerializeField] Color nitroLowColor = new Color(0.106f, 0.435f, 0.722f, 1f);

        [Tooltip("Fill colour when the nitro is full.")]
        [SerializeField] Color nitroFullColor = new Color(0.455f, 0.843f, 0.984f, 1f);

        [Tooltip("Fill colour while the nitro is locked after running dry.")]
        [SerializeField] Color nitroLockedColor = new Color(0.51f, 0.59f, 0.69f, 1f);

        // Path of the stick's touch zone inside the content, and of the area it must stay in.
        const string StickZonePath = "SafeArea/Stick/Zone";
        const string SafeAreaPath = "SafeArea";

        TankPowerUps playerPowerUps;
        TankNitro playerNitro;
        TankWeapon playerWeapon;
        RectTransform stickZone;
        RectTransform safeArea;
        Vector2 zoneOffsetMin;
        Vector2 zoneOffsetMax;
        Rect fittedSafeArea;
        Vector2Int fittedScreen;

        void Awake()
        {
            bool visible = Application.isMobilePlatform || (Application.isEditor && showTouchInEditor);
            content.SetActive(visible);
            if (!visible)
                return;

            EnsureEventSystem();
            backButton.onClick.AddListener(SceneNavigator.GoBack);

            stickZone = content.transform.Find(StickZonePath) as RectTransform;
            safeArea = content.transform.Find(SafeAreaPath) as RectTransform;
            if (stickZone != null)
            {
                zoneOffsetMin = stickZone.offsetMin;
                zoneOffsetMax = stickZone.offsetMax;
            }
        }

        // Start, not Awake: the player tank of the scene must exist already.
        void Start()
        {
            if (!content.activeSelf)
                return;

            PlayerTankInput player = FindAnyObjectByType<PlayerTankInput>();
            if (player == null)
                return;

            player.TryGetComponent(out playerPowerUps);
            player.TryGetComponent(out playerNitro);
            if (player.TryGetComponent(out playerWeapon))
            {
                playerWeapon.AmmoChanged += ShowAmmo;
                ShowAmmo();
            }
        }

        void OnDestroy()
        {
            if (backButton != null)
                backButton.onClick.RemoveListener(SceneNavigator.GoBack);
            if (playerWeapon != null)
                playerWeapon.AmmoChanged -= ShowAmmo;
        }

        void Update()
        {
            UpdateNitroRing();
            if (playerPowerUps == null)
                return;

            if (playerPowerUps.TryGetLatest(out string effectName, out float secondsLeft))
                SetPowerUpTime(secondsLeft, effectName);
            else
                SetPowerUpTime(0f);
        }

        // The ring fills with the nitro stock: light at full, deep blue when low, grey while locked.
        void UpdateNitroRing()
        {
            if (nitroFill == null || playerNitro == null)
                return;

            float level = Mathf.Clamp01(playerNitro.Normalized);
            nitroFill.fillAmount = level;
            nitroFill.color = playerNitro.IsLocked ? nitroLockedColor : Color.Lerp(nitroLowColor, nitroFullColor, level);
        }

        // After SafeAreaPanel (it applies in Update), and again only when the screen or the safe area changes.
        void LateUpdate()
        {
            if (stickZone == null || safeArea == null)
                return;

            var screen = new Vector2Int(Screen.width, Screen.height);
            if (screen == fittedScreen && Screen.safeArea == fittedSafeArea)
                return;

            fittedScreen = screen;
            fittedSafeArea = Screen.safeArea;
            stickZone.offsetMin = zoneOffsetMin;
            stickZone.offsetMax = zoneOffsetMax;
            KeepInside(stickZone, safeArea);
        }

        // Moves the rectangle's edges in so that it does not stick out of the bounds (works in any canvas scale).
        static void KeepInside(RectTransform rect, RectTransform bounds)
        {
            var corners = new Vector3[4];
            var limits = new Vector3[4];
            rect.GetWorldCorners(corners);
            bounds.GetWorldCorners(limits);

            Vector3 scale = rect.parent != null ? rect.parent.lossyScale : Vector3.one;
            if (scale.x <= 0f || scale.y <= 0f)
                return;

            // Corner 0 is bottom-left, corner 2 is top-right.
            float left = Mathf.Max(0f, limits[0].x - corners[0].x) / scale.x;
            float bottom = Mathf.Max(0f, limits[0].y - corners[0].y) / scale.y;
            float right = Mathf.Max(0f, corners[2].x - limits[2].x) / scale.x;
            float top = Mathf.Max(0f, corners[2].y - limits[2].y) / scale.y;
            rect.offsetMin += new Vector2(left, bottom);
            rect.offsetMax -= new Vector2(right, top);
        }

        /// <summary>Shows the remaining power-up time, or hides the timer when <paramref name="secondsLeft"/> is not positive.</summary>
        public void SetPowerUpTime(float secondsLeft, string effectName = null)
        {
            bool active = secondsLeft > 0f;
            if (powerUpTimerText.gameObject.activeSelf != active)
                powerUpTimerText.gameObject.SetActive(active);
            if (active)
            {
                string time = secondsLeft.ToString("0.0");
                powerUpTimerText.text = string.IsNullOrEmpty(effectName) ? time : effectName + " " + time;
            }
        }

        void ShowAmmo()
        {
            if (ammoLabel != null && playerWeapon != null && playerWeapon.SelectedAmmo != null)
                ammoLabel.text = playerWeapon.SelectedAmmo.displayName;
        }

        // uGUI delivers touches to the on-screen controls only through an EventSystem, and the test scenes have none.
        static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null)
                return;

            var host = new GameObject(nameof(EventSystem));
            host.SetActive(false);
            host.AddComponent<EventSystem>();
            host.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            host.SetActive(true);
        }
    }
}
