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

        bool FireHeld { get; }

        /// <summary>True only on the frame the fire button went down, even for a tap shorter than a frame. Read it in Update.</summary>
        bool FirePressed { get; }

        bool NitroHeld { get; }

        /// <summary>True only on the frame the ammo switch was requested. Read it in Update.</summary>
        bool CycleAmmoPressed { get; }
    }
}
