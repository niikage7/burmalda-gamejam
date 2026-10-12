using UnityEngine;

namespace TankBrawl.Core
{
    /// <summary>
    /// Source of tank commands. Implemented by the player (Input System) and by bots (FSM),
    /// so the tank itself never knows who drives it.
    /// </summary>
    public interface ITankInput
    {
        /// <summary>Desired hull direction. Magnitude 0..1, dead zone already applied.</summary>
        Vector2 Move { get; }

        /// <summary>The fire button is held. The weapon keeps firing at its own fire rate while it is true.</summary>
        bool FireHeld { get; }

        /// <summary>The nitro button is held. See <c>TankNitro</c> for when it actually boosts.</summary>
        bool NitroHeld { get; }

        /// <summary>True only on the frame the ammo switch was requested. Read it in Update.</summary>
        bool CycleAmmoPressed { get; }
    }
}
