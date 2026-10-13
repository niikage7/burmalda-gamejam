using TankBrawl.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TankBrawl.Input
{
    /// <summary>
    /// Feeds the tank from the TankControls actions (keyboard, gamepad, on-screen controls). A mouse click on the
    /// interface (an on-screen button shown in the editor, a menu) does not fire the gun.
    /// </summary>
    public class PlayerTankInput : MonoBehaviour, ITankInput
    {
        [SerializeField] InputActionReference moveAction;
        [SerializeField] InputActionReference fireAction;
        [SerializeField] InputActionReference nitroAction;
        [SerializeField] InputActionReference cycleAmmoAction;

        public Vector2 Move => moveAction.action.ReadValue<Vector2>();
        public bool FireHeld => fireAction.action.IsPressed() && !OnlyMouseOverUi();
        public bool FirePressed => fireAction.action.WasPressedThisFrame() && !OnlyMouseOverUi();
        public bool NitroHeld => nitroAction.action.IsPressed();
        public bool CycleAmmoPressed => cycleAmmoAction.action.WasPressedThisFrame();

        // True when the mouse is the only control holding Fire and the pointer is over the interface: the click was meant
        // for a button. The on-screen fire button itself presses the virtual gamepad, so it still fires.
        bool OnlyMouseOverUi()
        {
            EventSystem events = EventSystem.current;
            if (events == null || !events.IsPointerOverGameObject())
                return false;

            var controls = fireAction.action.controls;
            for (int i = 0; i < controls.Count; i++)
            {
                if (!(controls[i].device is Mouse) && controls[i].IsPressed())
                    return false;
            }

            return true;
        }

        void OnEnable()
        {
            if (TryGetComponent(out TeamMember member))
                TeamMember.LocalTeam = member.Team;

            moveAction.action.Enable();
            fireAction.action.Enable();
            nitroAction.action.Enable();
            cycleAmmoAction.action.Enable();
        }

        void OnDisable()
        {
            moveAction.action.Disable();
            fireAction.action.Disable();
            nitroAction.action.Disable();
            cycleAmmoAction.action.Disable();
        }
    }
}
