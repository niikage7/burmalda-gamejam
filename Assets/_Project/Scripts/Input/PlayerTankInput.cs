using TankBrawl.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TankBrawl.Input
{
    /// <summary>Feeds the tank from the TankControls actions (keyboard, gamepad, on-screen controls).</summary>
    public class PlayerTankInput : MonoBehaviour, ITankInput
    {
        [SerializeField] InputActionReference moveAction;
        [SerializeField] InputActionReference fireAction;
        [SerializeField] InputActionReference nitroAction;
        [SerializeField] InputActionReference cycleAmmoAction;

        /// <inheritdoc/>
        public Vector2 Move => moveAction.action.ReadValue<Vector2>();
        /// <inheritdoc/>
        public bool FireHeld => fireAction.action.IsPressed();
        /// <inheritdoc/>
        public bool NitroHeld => nitroAction.action.IsPressed();
        /// <inheritdoc/>
        public bool CycleAmmoPressed => cycleAmmoAction.action.WasPressedThisFrame();

        void OnEnable()
        {
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
