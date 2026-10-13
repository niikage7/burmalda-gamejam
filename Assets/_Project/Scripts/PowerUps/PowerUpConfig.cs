using System.Globalization;
using TankBrawl.Weapons;
using UnityEngine;

namespace TankBrawl.PowerUps
{
    /// <summary>Kinds of power-ups. Stored as numbers in assets: add new kinds at the end only.</summary>
    public enum PowerUpKind
    {
        RapidFire,
        ExplosiveShells,
        Laser,
        Shield,

        /// <summary>Instant: restores health (balance v0.2 §7).</summary>
        Repair,

        /// <summary>Instant: fills the nitro stock (balance v0.2 §7).</summary>
        NitroRefill
    }

    /// <summary>
    /// Tunable numbers of one power-up. One asset per pick-up on the map; only the section of its <see cref="kind"/> is used.
    /// Units: 1 unit = 1D (hull diameter), see tank_balance_v0_2.md.
    /// </summary>
    [CreateAssetMenu(menuName = "TankBrawl/Power-Up Config", fileName = "PowerUpConfig")]
    public class PowerUpConfig : ScriptableObject
    {
        public PowerUpKind kind;

        [Tooltip("Name shown next to the timer.")]
        public string displayName = "УСИЛЕНИЕ";

        [Tooltip("Short line above the pick-up on the map. Empty = made from the numbers below, e.g. \"Стрельба ×2 · 10 с\".")]
        public string mapLabel;

        [Tooltip("How long the effect lasts, s. A second pick-up of the same kind refills it. Balance v0.2: explosive 15, laser 4. Rapid fire and shield are our own proposals. Ignored by the instant kinds (repair, nitro refill).")]
        [Min(0.1f)] public float duration = 10f;

        [Header("Pick-up look")]
        [Tooltip("Picture of the pick-up on the map (64 px, 64 PPU). Empty = a plain disc tinted with the colour below.")]
        public Sprite pickupSprite;

        [Tooltip("Colour of the burst when the pick-up is taken or fades; tints the disc when there is no sprite.")]
        public Color pickupColor = Color.white;

        [Header("Rapid fire")]
        [Tooltip("Multiplier of the time between shots. 0.5 = twice as often. Not in the balance doc yet.")]
        [Range(0.1f, 1f)] public float fireIntervalScale = 0.5f;

        [Header("Explosive shells")]
        [Tooltip("Replaces the tank's projectile while the effect lasts. Balance v0.2: 50 HP direct, 30 HP blast.")]
        public WeaponConfig explosiveWeapon;

        [Header("Laser")]
        [Tooltip("Damage of one tick to one unit in the beam, HP. Balance v0.2: 20")]
        [Min(0f)] public float laserDamage = 20f;

        [Tooltip("Pause between two ticks on the same unit, s. Balance v0.2: 0.5")]
        [Min(0.05f)] public float laserInterval = 0.5f;

        [Tooltip("Longest the beam can be, units. Arenas are closed, so this only guards against an open edge.")]
        [Min(1f)] public float laserRange = 40f;

        [Tooltip("Half-width of the beam, units (for hits and for walls).")]
        [Min(0.01f)] public float laserBeamRadius = 0.15f;

        [Tooltip("Tank speed multiplier while the laser works. Concept doc: it slows the tank; the number is our proposal.")]
        [Range(0.1f, 1f)] public float laserSpeedMultiplier = 0.6f;

        [Header("Shield")]
        [Tooltip("The ring blinks for this long before the shield expires, s.")]
        [Min(0f)] public float shieldBlinkTime = 2f;

        [Header("Repair")]
        [Tooltip("Health restored at once, never above the maximum, HP. Balance v0.2 §7: 30")]
        [Min(0f)] public float repairAmount = 30f;

        /// <summary>The line above the pick-up on the map: <see cref="mapLabel"/>, or what the effect does and for how long.</summary>
        public string MapLabel
        {
            get
            {
                if (!string.IsNullOrEmpty(mapLabel))
                    return mapLabel;

                string effect;
                switch (kind)
                {
                    case PowerUpKind.RapidFire:
                        effect = "Стрельба ×" + Number(1f / Mathf.Max(fireIntervalScale, 0.01f));
                        break;
                    case PowerUpKind.ExplosiveShells:
                        effect = "Взрывные снаряды";
                        break;
                    case PowerUpKind.Laser:
                        effect = "Лазер";
                        break;
                    case PowerUpKind.Shield:
                        effect = "Щит";
                        break;
                    case PowerUpKind.Repair:
                        return "+" + Mathf.RoundToInt(repairAmount) + " HP";
                    default:
                        return "Полное нитро";
                }

                return effect + " · " + Number(duration) + " с";
            }
        }

        // Russian style: a comma, at most one decimal ("1,5", "10").
        static string Number(float value) => value.ToString("0.#", CultureInfo.InvariantCulture).Replace('.', ',');

        /// <summary>The effect happens at once on pick-up and has no timer (repair, nitro refill).</summary>
        public bool IsInstant => kind == PowerUpKind.Repair || kind == PowerUpKind.NitroRefill;

        /// <summary>
        /// A config with the code defaults for an instant kind, for scenes whose spawner has no asset of that kind yet
        /// (see <see cref="PowerUpSpawner"/>). Not saved anywhere.
        /// </summary>
        public static PowerUpConfig CreateDefault(PowerUpKind kind)
        {
            var config = CreateInstance<PowerUpConfig>();
            config.kind = kind;
            config.pickupSprite = Resources.Load<Sprite>("PowerUpIcons/" + kind);
            if (kind == PowerUpKind.Repair)
            {
                config.name = "Repair (default)";
                config.displayName = "РЕМОНТ";
                config.pickupColor = new Color(0.3f, 0.95f, 0.4f, 1f);
            }
            else
            {
                config.name = "NitroRefill (default)";
                config.displayName = "НИТРО";
                config.pickupColor = new Color(0.9f, 0.45f, 1f, 1f);
            }

            return config;
        }
    }
}
