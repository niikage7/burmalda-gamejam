using TankBrawl.AI;
using TankBrawl.Input;
using TankBrawl.Progress;
using UnityEngine;

namespace TankBrawl.Tank
{
    /// <summary>
    /// Upgrade levels of one tank. The components that use a level (health, drive, nitro, weapon) read it from here and
    /// multiply it by the per-level numbers in their own configs, so level 0 is exactly the base config.
    /// The human's levels are what the player bought; a bot gets the threat rank R (scaled by the difficulty level) in hull, engine, weapon and fire rate
    /// (balance v0.2, section 14) and nothing in nitro. A tank with neither input (a dummy) stays at level 0.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class TankStats : MonoBehaviour
    {
        public int HullLevel { get; private set; }
        public int EngineLevel { get; private set; }
        public int WeaponLevel { get; private set; }
        public int FireLevel { get; private set; }
        public int NitroLevel { get; private set; }

        void OnEnable()
        {
            Refresh();
        }

        /// <summary>Reads the levels again (they only change in the menu, so this is needed once per match).</summary>
        public void Refresh()
        {
            if (TryGetComponent(out PlayerTankInput _))
            {
                HullLevel = PlayerProgress.Level(UpgradeBranch.Hull);
                EngineLevel = PlayerProgress.Level(UpgradeBranch.Engine);
                WeaponLevel = PlayerProgress.Level(UpgradeBranch.Weapon);
                FireLevel = PlayerProgress.Level(UpgradeBranch.FireRate);
                NitroLevel = PlayerProgress.Level(UpgradeBranch.Nitro);
            }
            else if (TryGetComponent(out BotTankInput _))
            {
                DifficultyConfig.Level difficulty = DifficultyConfig.Current;
                int rank = Mathf.RoundToInt(PlayerProgress.ThreatRank * (difficulty != null ? difficulty.statScale : 1f));
                HullLevel = EngineLevel = WeaponLevel = FireLevel = rank;
                NitroLevel = 0;
            }
            else
            {
                HullLevel = EngineLevel = WeaponLevel = FireLevel = NitroLevel = 0;
            }
        }
    }
}
