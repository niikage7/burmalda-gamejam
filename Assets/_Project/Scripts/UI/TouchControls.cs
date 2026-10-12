using TankBrawl.Input;
using TankBrawl.PowerUps;
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

        TankPowerUps playerPowerUps;
        TankWeapon playerWeapon;

        void Awake()
        {
            bool visible = Application.isMobilePlatform || (Application.isEditor && showTouchInEditor);
            content.SetActive(visible);
            if (!visible)
                return;

            EnsureEventSystem();
            backButton.onClick.AddListener(SceneNavigator.GoBack);
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
            if (playerPowerUps == null)
                return;

            if (playerPowerUps.TryGetLatest(out string effectName, out float secondsLeft))
                SetPowerUpTime(secondsLeft, effectName);
            else
                SetPowerUpTime(0f);
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
