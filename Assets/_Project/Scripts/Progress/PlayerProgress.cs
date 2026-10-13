using System;
using UnityEngine;

namespace TankBrawl.Progress
{
    /// <summary>
    /// Everything the player keeps between matches: gears, upgrade levels, settings, a few statistics. Saved as JSON in
    /// PlayerPrefs after every change. Static on purpose (it outlives scenes); the cache is dropped on Play Mode start,
    /// because domain reload is off.
    /// </summary>
    public static class PlayerProgress
    {
        public const int BranchCount = 5;

        /// <summary>Game modes that have their own unlocked difficulty (the order of <c>MatchModeKind</c>).</summary>
        public const int ModeCount = 3;

        const string SaveKey = "tankbrawl.progress.v1";
        const string ConfigResource = "ProgressionConfig";

        [Serializable]
        class SaveData
        {
            public int gears;
            public int[] levels = new int[BranchCount];
            public int[] unlockedDifficulty = new int[ModeCount];
            public float sfxVolume = 1f;
            public float musicVolume = 0.6f;
            public bool vibration = true;
            public bool screenShake = true;
            public int matchesPlayed;
            public int matchesWon;
        }

        const float SaveDelay = 0.6f;

        static SaveData data;
        static ProgressionConfig config;
        static bool saveDue;
        static float saveAt;

        /// <summary>Raised after any change of gears, levels or settings.</summary>
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            data = null;
            config = null;
            saveDue = false;
            Changed = null;
        }

        public static ProgressionConfig Config
        {
            get
            {
                if (config == null)
                {
                    config = Resources.Load<ProgressionConfig>(ConfigResource);
                    if (config == null)
                        Debug.LogError($"Resources/{ConfigResource} not found: upgrades will not work.");
                }

                return config;
            }
        }

        public static int Gears => Data.gears;
        public static int MatchesPlayed => Data.matchesPlayed;
        public static int MatchesWon => Data.matchesWon;

        // Settings that change continuously (a slider drag) are saved a moment later, not on every tick.
        public static float SfxVolume
        {
            get => Data.sfxVolume;
            set { Data.sfxVolume = Mathf.Clamp01(value); Commit(false); }
        }

        public static float MusicVolume
        {
            get => Data.musicVolume;
            set { Data.musicVolume = Mathf.Clamp01(value); Commit(false); }
        }

        public static bool Vibration
        {
            get => Data.vibration;
            set { Data.vibration = value; Commit(); }
        }

        /// <summary>The camera shakes on hits and blasts (a setting; read by <see cref="Core.ScreenShake"/>).</summary>
        public static bool ScreenShakeOn
        {
            get => Data.screenShake;
            set { Data.screenShake = value; Commit(); }
        }

        /// <summary>Writes a postponed change to disk once it is due (or at once with <paramref name="force"/>). Called by the audio manager every frame.</summary>
        public static void FlushIfDue(bool force = false)
        {
            if (!saveDue || data == null)
                return;

            if (force || Time.unscaledTime >= saveAt)
                Save();
        }

        /// <summary>Sum of all bought levels: the threat rank R that scales the bots (balance v0.2, section 14).</summary>
        public static int ThreatRank
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < BranchCount; i++)
                    sum += Data.levels[i];
                return sum;
            }
        }

        public static int Level(UpgradeBranch branch) => Data.levels[(int)branch];

        public static int NextCost(UpgradeBranch branch)
        {
            ProgressionConfig cfg = Config;
            return cfg != null ? cfg.Cost(branch, Level(branch)) : int.MaxValue;
        }

        public static bool CanAfford(UpgradeBranch branch) => Data.gears >= NextCost(branch);

        /// <summary>Buys the next level of the branch if there are enough gears.</summary>
        public static bool TryBuy(UpgradeBranch branch)
        {
            int cost = NextCost(branch);
            if (Data.gears < cost)
                return false;

            Data.gears -= cost;
            Data.levels[(int)branch]++;
            Commit();
            return true;
        }

        public static void AddGears(int amount)
        {
            if (amount <= 0)
                return;

            Data.gears += amount;
            Commit();
        }

        public static void RegisterMatch(bool won)
        {
            Data.matchesPlayed++;
            if (won)
                Data.matchesWon++;
            Commit();
        }

        /// <summary>The hardest difficulty (index) the player may start in the mode. The easiest one is always open.</summary>
        public static int HighestUnlockedDifficulty(int mode)
        {
            return mode >= 0 && mode < ModeCount ? Data.unlockedDifficulty[mode] : 0;
        }

        public static bool IsDifficultyUnlocked(int mode, int difficulty)
        {
            return difficulty <= HighestUnlockedDifficulty(mode);
        }

        /// <summary>
        /// A win in the mode on the difficulty <paramref name="beaten"/> opens the next one. Only a difficulty that was open counts
        /// (a scene started directly from the editor cannot open anything). Returns the newly opened index or -1.
        /// </summary>
        public static int UnlockNextDifficulty(int mode, int beaten, int difficultyCount)
        {
            if (mode < 0 || mode >= ModeCount)
                return -1;

            int next = beaten + 1;
            if (beaten > Data.unlockedDifficulty[mode] || next >= difficultyCount || next <= Data.unlockedDifficulty[mode])
                return -1;

            Data.unlockedDifficulty[mode] = next;
            Commit();
            return next;
        }

        /// <summary>Debug: opens every difficulty in every mode.</summary>
        public static void UnlockAllDifficulties(int difficultyCount)
        {
            for (int i = 0; i < ModeCount; i++)
                Data.unlockedDifficulty[i] = Mathf.Max(0, difficultyCount - 1);
            Commit();
        }

        /// <summary>Forgets everything (gears, levels, unlocked difficulties, statistics) but keeps the settings.</summary>
        public static void ResetProgress()
        {
            SaveData fresh = new SaveData
            {
                sfxVolume = Data.sfxVolume,
                musicVolume = Data.musicVolume,
                vibration = Data.vibration,
                screenShake = Data.screenShake
            };
            data = fresh;
            Commit();
        }

        static SaveData Data
        {
            get
            {
                if (data == null)
                    Load();
                return data;
            }
        }

        static void Load()
        {
            data = null;
            try
            {
                string json = PlayerPrefs.GetString(SaveKey, string.Empty);
                if (!string.IsNullOrEmpty(json))
                    data = JsonUtility.FromJson<SaveData>(json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Progress could not be read, starting fresh: {exception.Message}");
            }

            if (data == null)
                data = new SaveData();
            if (data.unlockedDifficulty == null || data.unlockedDifficulty.Length != ModeCount)
            {
                var fixedUnlocked = new int[ModeCount];
                if (data.unlockedDifficulty != null)
                    Array.Copy(data.unlockedDifficulty, fixedUnlocked, Mathf.Min(data.unlockedDifficulty.Length, ModeCount));
                data.unlockedDifficulty = fixedUnlocked;
            }

            if (data.levels == null || data.levels.Length != BranchCount)
            {
                var fixedLevels = new int[BranchCount];
                if (data.levels != null)
                    Array.Copy(data.levels, fixedLevels, Mathf.Min(data.levels.Length, BranchCount));
                data.levels = fixedLevels;
            }
        }

        static void Commit(bool immediate = true)
        {
            if (immediate)
            {
                Save();
            }
            else
            {
                saveDue = true;
                saveAt = Time.unscaledTime + SaveDelay;
            }

            Changed?.Invoke();
        }

        static void Save()
        {
            saveDue = false;
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }
    }
}
