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

        const string SaveKey = "tankbrawl.progress.v1";
        const string ConfigResource = "ProgressionConfig";

        [Serializable]
        class SaveData
        {
            public int gears;
            public int[] levels = new int[BranchCount];
            public float sfxVolume = 1f;
            public float musicVolume = 0.6f;
            public bool vibration = true;
            public int matchesPlayed;
            public int matchesWon;
        }

        static SaveData data;
        static ProgressionConfig config;

        /// <summary>Raised after any change of gears, levels or settings.</summary>
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            data = null;
            config = null;
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

        public static float SfxVolume
        {
            get => Data.sfxVolume;
            set { Data.sfxVolume = Mathf.Clamp01(value); Commit(); }
        }

        public static float MusicVolume
        {
            get => Data.musicVolume;
            set { Data.musicVolume = Mathf.Clamp01(value); Commit(); }
        }

        public static bool Vibration
        {
            get => Data.vibration;
            set { Data.vibration = value; Commit(); }
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

        /// <summary>Forgets everything (gears, levels, statistics) but keeps the settings.</summary>
        public static void ResetProgress()
        {
            SaveData fresh = new SaveData
            {
                sfxVolume = Data.sfxVolume,
                musicVolume = Data.musicVolume,
                vibration = Data.vibration
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
            if (data.levels == null || data.levels.Length != BranchCount)
            {
                var fixedLevels = new int[BranchCount];
                if (data.levels != null)
                    Array.Copy(data.levels, fixedLevels, Mathf.Min(data.levels.Length, BranchCount));
                data.levels = fixedLevels;
            }
        }

        static void Commit()
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
