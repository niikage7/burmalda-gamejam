using System;
using TankBrawl.Tank;
using TankBrawl.Weapons;
using UnityEngine;

namespace TankBrawl.Progress
{
    /// <summary>The five permanent upgrade branches (balance v0.2, section 13).</summary>
    public enum UpgradeBranch
    {
        Hull = 0,
        Engine = 1,
        Weapon = 2,
        FireRate = 3,
        Nitro = 4
    }

    /// <summary>
    /// Costs and rewards of the economy (balance v0.2, sections 12-15). What a level does lives next to the base number it
    /// changes: <see cref="TankConfig.healthPerLevel"/>, <see cref="TankConfig.speedPerLevel"/>, <see cref="NitroConfig.capacityPerLevel"/>
    /// and the per-level fields of <see cref="WeaponConfig"/>. This asset only prices them and describes them to the player.
    /// Loaded from Resources by <see cref="PlayerProgress"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "TankBrawl/Progression Config", fileName = "ProgressionConfig")]
    public class ProgressionConfig : ScriptableObject
    {
        [Serializable]
        public class Branch
        {
            public string displayName;
            [TextArea(1, 3)] public string description;

            [Tooltip("Cost of the first level, gears. cost(n) = round(baseCost * costGrowth ^ n).")]
            [Min(1)] public int baseCost = 100;

            [Tooltip("Optional icon for the upgrade card. Empty = a plain coloured square.")]
            public Sprite icon;
        }

        [Header("Rewards (balance v0.2, section 12)")]
        [Tooltip("Gears for every enemy tank the human destroys. Balance: 12")]
        [Min(0)] public int gearsPerKill = 12;

        [Tooltip("Gears for achieving the main goal of a match. Balance: 100")]
        [Min(0)] public int gearsPerWin = 100;

        [Header("Prices (balance v0.2, section 13.2)")]
        [Tooltip("Price growth per level. Balance: 1.35")]
        [Min(1f)] public float costGrowth = 1.35f;

        [Tooltip("In the order of UpgradeBranch: Hull, Engine, Weapon, FireRate, Nitro.")]
        public Branch[] branches = new Branch[5];

        [Header("Configs the descriptions read the base numbers from")]
        public TankConfig playerTank;
        public NitroConfig nitro;
        public WeaponConfig primaryWeapon;

        public int Cost(UpgradeBranch branch, int level)
        {
            return Mathf.RoundToInt(branches[(int)branch].baseCost * Mathf.Pow(costGrowth, Mathf.Max(0, level)));
        }

        public Branch Info(UpgradeBranch branch) => branches[(int)branch];

        /// <summary>The branch's stat at the given level as text, e.g. "120 HP".</summary>
        public string StatText(UpgradeBranch branch, int level)
        {
            switch (branch)
            {
                case UpgradeBranch.Hull:
                    return playerTank != null ? Format(playerTank.maxHealth + playerTank.healthPerLevel * level) + " HP" : "-";
                case UpgradeBranch.Engine:
                    return playerTank != null ? (playerTank.moveSpeed + playerTank.speedPerLevel * level).ToString("0.00") + " D/с" : "-";
                case UpgradeBranch.Weapon:
                    return primaryWeapon != null ? Format(primaryWeapon.DamageAt(level)) + " урона" : "-";
                case UpgradeBranch.FireRate:
                    return primaryWeapon != null ? (1f / primaryWeapon.IntervalAt(level)).ToString("0.00") + " выстр/с" : "-";
                default:
                    return nitro != null ? (nitro.capacity + nitro.capacityPerLevel * level).ToString("0.00") + " с" : "-";
            }
        }

        static string Format(float value) => Mathf.RoundToInt(value).ToString();
    }
}
