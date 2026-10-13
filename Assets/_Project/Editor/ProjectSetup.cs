using TankBrawl.AI;
using TankBrawl.Audio;
using TankBrawl.Match;
using TankBrawl.Progress;
using TankBrawl.Tank;
using TankBrawl.UI;
using TankBrawl.Weapons;
using UnityEditor;
using UnityEngine;

namespace TankBrawl.EditorTools
{
    /// <summary>
    /// Creates and fills the data assets of the progression, match modes, UI theme and weapons (balance v0.2 numbers).
    /// Safe to run again: existing assets are updated in place, never duplicated. Menu: TankBrawl / Setup Data Assets.
    /// </summary>
    public static class ProjectSetup
    {
        const string Root = "Assets/_Project/ScriptableObjects";
        const string ResourcesFolder = Root + "/Resources";
        const string MatchFolder = Root + "/Match";

        [MenuItem("TankBrawl/Setup Data Assets")]
        public static void Run()
        {
            Ensure(Root, "Resources");
            Ensure(Root, "Match");

            SetupTankConfigs();
            SetupWeapons();
            SetupProgression();
            SetupModes();
            SetupDifficulty();
            GetOrCreate<UiTheme>(ResourcesFolder + "/UiTheme.asset");

            AssetDatabase.SaveAssets();
            Debug.Log("TankBrawl data assets are set up.");
        }

        static void Ensure(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + child))
                AssetDatabase.CreateFolder(parent, child);
        }

        static T GetOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }

            return asset;
        }

        static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                Debug.LogWarning($"Asset not found: {path}");
            return asset;
        }

        // Balance v0.2, section 13.1 and 14: the per-level numbers of the player and of the bots.
        static void SetupTankConfigs()
        {
            var player = Load<TankConfig>(Root + "/Tank/PlayerTankConfig.asset");
            if (player != null)
            {
                player.healthPerLevel = 10f;
                player.speedPerLevel = 0.15f;
                EditorUtility.SetDirty(player);
            }

            var bot = Load<TankConfig>(Root + "/Tank/BotTankConfig.asset");
            if (bot != null)
            {
                bot.healthPerLevel = 4f;
                bot.speedPerLevel = 0.01f;
                EditorUtility.SetDirty(bot);
            }

            var nitro = Load<NitroConfig>(Root + "/Tank/NitroConfig.asset");
            if (nitro != null)
            {
                nitro.capacityPerLevel = 0.25f;
                EditorUtility.SetDirty(nitro);
            }
        }

        static void SetupWeapons()
        {
            Weapon("ArmorPiercing", 4f, 0f, 0f, 0.05f, Sfx.ShotArmorPiercing);
            Weapon("Fragment", 2f, 1f, 0f, 0.05f, Sfx.ShotFragment);
            Weapon("ExplosiveShell", 4f, 1f, 0f, 0.05f, Sfx.ShotExplosive);
            Weapon("BotWeapon", 0.5f, 0f, 0.03f, 0.01f, Sfx.ShotBot);
        }

        static void Weapon(string assetName, float damagePerLevel, float blastPerLevel, float speedPerLevel, float ratePerLevel, Sfx sound)
        {
            var weapon = Load<WeaponConfig>(Root + "/Weapons/" + assetName + ".asset");
            if (weapon == null)
                return;

            weapon.damagePerLevel = damagePerLevel;
            weapon.explosionDamagePerLevel = blastPerLevel;
            weapon.speedPerLevel = speedPerLevel;
            weapon.fireRatePerLevel = ratePerLevel;
            weapon.fireSfx = sound;
            EditorUtility.SetDirty(weapon);
        }

        static void SetupProgression()
        {
            var config = GetOrCreate<ProgressionConfig>(ResourcesFolder + "/ProgressionConfig.asset");
            config.gearsPerKill = 12;
            config.gearsPerWin = 100;
            config.costGrowth = 1.35f;
            config.playerTank = Load<TankConfig>(Root + "/Tank/PlayerTankConfig.asset");
            config.nitro = Load<NitroConfig>(Root + "/Tank/NitroConfig.asset");
            config.primaryWeapon = Load<WeaponConfig>(Root + "/Weapons/ArmorPiercing.asset");

            if (config.branches == null || config.branches.Length != PlayerProgress.BranchCount)
                config.branches = new ProgressionConfig.Branch[PlayerProgress.BranchCount];

            Branch(config, UpgradeBranch.Hull, "Корпус", "Больше прочности: +10 HP за уровень.", 100);
            Branch(config, UpgradeBranch.Engine, "Двигатель", "Выше скорость танка: +0,15 D/с за уровень.", 90);
            Branch(config, UpgradeBranch.Weapon, "Орудие", "Сильнее снаряды: +4 урона бронебойному, +2/+1 осколочному.", 140);
            Branch(config, UpgradeBranch.FireRate, "Скорострельность", "Чаще выстрелы: +0,05 выстр/с за уровень.", 130);
            Branch(config, UpgradeBranch.Nitro, "Нитро", "Дольше ускорение и таран: +0,25 с за уровень.", 80);
            EditorUtility.SetDirty(config);
        }

        static void Branch(ProgressionConfig config, UpgradeBranch branch, string displayName, string description, int baseCost)
        {
            ProgressionConfig.Branch info = config.branches[(int)branch] ?? new ProgressionConfig.Branch();
            info.displayName = displayName;
            info.description = description;
            info.baseCost = baseCost;
            config.branches[(int)branch] = info;
        }

        // Bot difficulty levels. Medium is about the bots as they were before the levels existed, but more human (slower turning,
        // an unsteady aim); easy is clearly weaker, hard is more accurate, tougher and quicker to react. First guess, tune in playtests.
        static void SetupDifficulty()
        {
            var config = GetOrCreate<DifficultyConfig>(ResourcesFolder + "/DifficultyConfig.asset");
            config.defaultLevel = 1;
            config.levels = new[]
            {
                new DifficultyConfig.Level
                {
                    displayName = "Лёгкая",
                    description = "Боты медленно поворачиваются, часто промахиваются и плохо уворачиваются.",
                    turnRateScale = 0.45f, statScale = 0.5f, reactionScale = 2.5f, aimSpreadScale = 2.2f, aimWobbleDegrees = 6f,
                    fireAlignScale = 3f, leadScale = 0.3f, dodgeChanceScale = 0.3f, aggressionScale = 0.4f, decisionScale = 1.6f
                },
                new DifficultyConfig.Level
                {
                    displayName = "Средняя",
                    description = "Боты стреляют метко, но поворачиваются и целятся как люди.",
                    turnRateScale = 0.7f, statScale = 1f, reactionScale = 1.5f, aimSpreadScale = 1.3f, aimWobbleDegrees = 3f,
                    fireAlignScale = 1.5f, leadScale = 0.8f, dodgeChanceScale = 0.8f, aggressionScale = 0.8f, decisionScale = 1.2f
                },
                new DifficultyConfig.Level
                {
                    displayName = "Сложная",
                    description = "Боты быстрее реагируют, точнее целятся и крепче: на 30% сильнее по улучшениям.",
                    turnRateScale = 0.9f, statScale = 1.3f, reactionScale = 1f, aimSpreadScale = 0.8f, aimWobbleDegrees = 1.5f,
                    fireAlignScale = 1f, leadScale = 1f, dodgeChanceScale = 1f, aggressionScale = 1f, decisionScale = 1f
                }
            };
            EditorUtility.SetDirty(config);
        }

        // The three modes. Numbers are the first guess (the user delegated the first balance): to be tuned in playtests.
        static void SetupModes()
        {
            var team = GetOrCreate<MatchModeConfig>(MatchFolder + "/TeamElimination.asset");
            team.kind = MatchModeKind.TeamElimination;
            team.displayName = "Команда на команду";
            team.description = "3 на 3, без возрождений. Раунд идёт до полного уничтожения команды врага, матч — до 3 побед.";
            team.spawnProtection = 2f;
            team.respawnDelay = 0f;
            team.randomSpawn = false;
            team.timeLimit = 0f;
            team.scoreLimit = 0;
            team.roundsToWin = 3;
            team.roundIntroSeconds = 2f;
            team.roundEndSeconds = 3f;
            team.extraGoalText = "Без потерь: ни разу не погибнуть";
            team.extraGoalGears = 20;
            EditorUtility.SetDirty(team);

            var point = GetOrCreate<MatchModeConfig>(MatchFolder + "/CapturePoint.asset");
            point.kind = MatchModeKind.CapturePoint;
            point.displayName = "Удержание точки";
            point.description = "3 на 3. Очко в секунду команде, которая одна держит зону в центре. До 100 очков или 8 минут. Возрождение через 7 с.";
            point.spawnProtection = 3f;
            point.respawnDelay = 7f;
            point.randomSpawn = false;
            point.timeLimit = 480f;
            point.scoreLimit = 100;
            point.pointsPerSecond = 1f;
            point.extraGoalText = "Продержать точку 30 с";
            point.extraGoalHoldSeconds = 30f;
            point.extraGoalGears = 20;
            EditorUtility.SetDirty(point);

            var dm = GetOrCreate<MatchModeConfig>(MatchFolder + "/Deathmatch.asset");
            dm.kind = MatchModeKind.Deathmatch;
            dm.displayName = "Все против всех";
            dm.description = "Вы и три бота. 10 очков за убийство, плюс очки за выживание. До 100 очков или 7 минут. Возрождение через 7 с.";
            dm.spawnProtection = 3f;
            dm.respawnDelay = 7f;
            dm.randomSpawn = true;
            dm.safeSpawnDistance = 6f;
            dm.timeLimit = 420f;
            dm.scoreLimit = 100;
            dm.killPoints = 10;
            dm.survivalStartSeconds = 25f;
            dm.survivalIntervalSeconds = 5f;
            dm.survivalPoints = 1;
            dm.extraGoalText = "5 убийств за матч";
            dm.extraGoalKills = 5;
            dm.extraGoalGears = 20;
            EditorUtility.SetDirty(dm);
        }
    }
}
