using System;
using UnityEngine;

namespace TankBrawl.AI
{
    /// <summary>
    /// The bot difficulty levels (easy, medium, hard...) and the one picked for the match being played. A level does not
    /// replace a <see cref="BotProfile"/>: it scales the profile's numbers, so an aggressor stays an aggressor on every level.
    /// The menu sets <see cref="Selected"/> before the arena scene loads; the bots read <see cref="Current"/> when they wake up.
    /// All numbers are 1 = "as in the profile" (the old behaviour of the bots).
    /// </summary>
    [CreateAssetMenu(menuName = "TankBrawl/Difficulty Config", fileName = "DifficultyConfig")]
    public class DifficultyConfig : ScriptableObject
    {
        [Serializable]
        public class Level
        {
            [Header("Menu")]
            public string displayName = "Средняя";

            [TextArea(1, 3)] public string description = string.Empty;

            [Header("Body")]
            [Tooltip("Turn speed of the bot's hull relative to the config (1 = the same as the player's tank). Slower turning makes the bot late to aim and to dodge.")]
            [Range(0.1f, 1.5f)] public float turnRateScale = 1f;

            [Tooltip("Scales the threat rank the bot is built from (hull, engine, weapon, fire rate). 1 = the full rank, 0 = the base stats.")]
            [Range(0f, 2f)] public float statScale = 1f;

            [Header("Reaction and aim")]
            [Tooltip("Multiplies the delay before the first shot and before a dodge.")]
            [Min(0.1f)] public float reactionScale = 1f;

            [Tooltip("Multiplies the random aiming error rolled for every shot.")]
            [Min(0f)] public float aimSpreadScale = 1f;

            [Tooltip("Slow drift of the aim around the target, ± deg: a human hand does not hold a stick perfectly still.")]
            [Min(0f)] public float aimWobbleDegrees;

            [Tooltip("Multiplies the angle within which the bot already fires (bigger = shoots before it is aligned).")]
            [Min(0.1f)] public float fireAlignScale = 1f;

            [Tooltip("Multiplies how well the bot leads a moving target.")]
            [Range(0f, 1.5f)] public float leadScale = 1f;

            [Header("Tactics")]
            [Tooltip("Multiplies the chance to dodge a shot that would hit.")]
            [Range(0f, 1.5f)] public float dodgeChanceScale = 1f;

            [Tooltip("Multiplies the chance to start a ram.")]
            [Range(0f, 1.5f)] public float aggressionScale = 1f;

            [Tooltip("Multiplies the time between the bot's decisions (state, circling side, targets). Bigger = slower thinking.")]
            [Min(0.1f)] public float decisionScale = 1f;

            /// <summary>A copy of <paramref name="source"/> with this level's scaling applied. The caller owns (and destroys) the copy.</summary>
            public BotProfile CreateProfile(BotProfile source)
            {
                BotProfile profile = Instantiate(source);
                profile.name = source.name + " (" + displayName + ")";

                profile.reactionDelayMin *= reactionScale;
                profile.reactionDelayMax *= reactionScale;
                profile.dodgeReactionMin *= reactionScale;
                profile.dodgeReactionMax *= reactionScale;

                profile.aimSpreadDegrees *= aimSpreadScale;
                profile.aimWobbleDegrees += aimWobbleDegrees;
                profile.fireAlignTolerance = Mathf.Min(45f, profile.fireAlignTolerance * fireAlignScale);
                profile.leadFactor = Mathf.Clamp01(profile.leadFactor * leadScale);

                profile.dodgeChance = Mathf.Clamp01(profile.dodgeChance * dodgeChanceScale);
                profile.aggression = Mathf.Clamp01(profile.aggression * aggressionScale);
                profile.decisionInterval *= decisionScale;
                return profile;
            }
        }

        public const string ResourceName = "DifficultyConfig";

        [Tooltip("From the easiest to the hardest. The next level opens after a win on the previous one (per game mode).")]
        public Level[] levels = new Level[0];

        [Tooltip("The level used when nothing was picked in the menu (a test scene opened directly).")]
        [Min(0)] public int defaultLevel = 1;

        static DifficultyConfig instance;
        static int selected = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
            selected = -1;
        }

        /// <summary>The asset from Resources, or null (then every difficulty number is "as in the profile").</summary>
        public static DifficultyConfig Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = Resources.Load<DifficultyConfig>(ResourceName);
                    if (instance == null)
                        Debug.LogWarning($"Resources/{ResourceName} not found: bots use their profiles as they are. Run TankBrawl / Setup Data Assets.");
                }

                return instance;
            }
        }

        /// <summary>How many levels there are (at least 1).</summary>
        public static int Count
        {
            get
            {
                DifficultyConfig config = Instance;
                return config != null && config.levels != null && config.levels.Length > 0 ? config.levels.Length : 1;
            }
        }

        /// <summary>The level of the match being played (index). Set by the menu; -1 = not picked, the default level is used.</summary>
        public static int Selected
        {
            get => selected;
            set => selected = value;
        }

        public static int CurrentIndex
        {
            get
            {
                DifficultyConfig config = Instance;
                int index = selected >= 0 ? selected : (config != null ? config.defaultLevel : 0);
                return Mathf.Clamp(index, 0, Count - 1);
            }
        }

        /// <summary>The level of the match being played, or null without a config.</summary>
        public static Level Current => Get(CurrentIndex);

        public static Level Get(int index)
        {
            DifficultyConfig config = Instance;
            if (config == null || config.levels == null || config.levels.Length == 0)
                return null;

            return config.levels[Mathf.Clamp(index, 0, config.levels.Length - 1)];
        }

        public static string NameOf(int index)
        {
            Level level = Get(index);
            return level != null && !string.IsNullOrEmpty(level.displayName) ? level.displayName : "Обычная";
        }
    }
}
