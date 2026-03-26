#if UNITY_EDITOR
using System.Collections.Generic;
using DiceBattler.Boot;
using DiceBattler.Configs;
using DiceBattler.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DiceBattler.EditorTools
{
    public static class PrototypeSetupUtility
    {
        private const string DefaultsFolder = "Assets/Configs/Defaults";
        private const string RegistriesFolder = "Assets/Configs/Registries";
        private const string ResourcesFolder = "Assets/Resources/DiceBattler";

        [MenuItem("Tools/Dice Battler/Setup/Create Default Assets")]
        public static void CreateDefaultAssets()
        {
            EnsureFolder("Assets/Configs");
            EnsureFolder(DefaultsFolder);
            EnsureFolder(RegistriesFolder);
            EnsureFolder("Assets/Resources");
            EnsureFolder(ResourcesFolder);

            HeroConfig heroConfig = CreateOrLoadAsset<HeroConfig>(DefaultsFolder, "HeroConfig.asset");
            heroConfig.heroId = "hero_main";
            heroConfig.displayName = "Knight";
            heroConfig.maxHp = 30;
            heroConfig.startingUnlockedDice = 1;
            heroConfig.startingRerollsPerTurn = 3;
            heroConfig.startingFlatDamageBonus = 0;
            heroConfig.baseRunId = "run_01";
            EditorUtility.SetDirty(heroConfig);

            RunConfig runConfig = CreateOrLoadAsset<RunConfig>(DefaultsFolder, "RunConfig.asset");
            runConfig.runId = "run_01";
            runConfig.totalWaves = 3;
            runConfig.diceSlotsTotal = 5;
            runConfig.rerollsPerTurnDefault = 3;
            runConfig.diceHighlightDuration = 0.35f;
            runConfig.postWaveRunTransitionDuration = 0.65f;
            runConfig.damagePanelDisplayDuration = 1f;
            runConfig.showBonusWhenZero = true;
            runConfig.lockedDiceVisible = true;
            EditorUtility.SetDirty(runConfig);

            MobConfig slime = CreateOrLoadAsset<MobConfig>(DefaultsFolder, "Mob_slime.asset");
            slime.mobId = "slime";
            slime.displayName = "Slime";
            slime.hp = 8;
            slime.damageMin = 1;
            slime.damageMax = 3;
            slime.expReward = 3;
            slime.prefabKey = "slime";
            EditorUtility.SetDirty(slime);

            MobConfig goblin = CreateOrLoadAsset<MobConfig>(DefaultsFolder, "Mob_goblin.asset");
            goblin.mobId = "goblin";
            goblin.displayName = "Goblin";
            goblin.hp = 12;
            goblin.damageMin = 2;
            goblin.damageMax = 4;
            goblin.expReward = 4;
            goblin.prefabKey = "goblin";
            EditorUtility.SetDirty(goblin);

            MobConfig archer = CreateOrLoadAsset<MobConfig>(DefaultsFolder, "Mob_archer.asset");
            archer.mobId = "archer";
            archer.displayName = "Archer";
            archer.hp = 10;
            archer.damageMin = 3;
            archer.damageMax = 5;
            archer.expReward = 5;
            archer.prefabKey = "archer";
            EditorUtility.SetDirty(archer);

            MobDatabase mobDatabase = CreateOrLoadAsset<MobDatabase>(DefaultsFolder, "MobDatabase.asset");
            mobDatabase.mobs = new List<MobConfig> { slime, goblin, archer };
            EditorUtility.SetDirty(mobDatabase);

            WaveConfig wave01 = CreateOrLoadAsset<WaveConfig>(DefaultsFolder, "Wave_01.asset");
            wave01.waveNumber = 1;
            wave01.mobIds = new List<string> { "slime" };
            wave01.expReward = 4;
            EditorUtility.SetDirty(wave01);

            WaveConfig wave02 = CreateOrLoadAsset<WaveConfig>(DefaultsFolder, "Wave_02.asset");
            wave02.waveNumber = 2;
            wave02.mobIds = new List<string> { "slime", "goblin" };
            wave02.expReward = 5;
            EditorUtility.SetDirty(wave02);

            WaveConfig wave03 = CreateOrLoadAsset<WaveConfig>(DefaultsFolder, "Wave_03.asset");
            wave03.waveNumber = 3;
            wave03.mobIds = new List<string> { "goblin", "archer", "slime" };
            wave03.expReward = 6;
            EditorUtility.SetDirty(wave03);

            WaveDatabase waveDatabase = CreateOrLoadAsset<WaveDatabase>(DefaultsFolder, "WaveDatabase.asset");
            waveDatabase.totalWaves = 3;
            waveDatabase.waves = new List<WaveConfig> { wave01, wave02, wave03 };
            EditorUtility.SetDirty(waveDatabase);

            CombinationDatabase combinationDatabase = CreateOrLoadAsset<CombinationDatabase>(DefaultsFolder, "CombinationDatabase.asset");
            combinationDatabase.entries = new List<CombinationMultiplierEntry>
            {
                new CombinationMultiplierEntry { combination = CombinationFamily.OnePair, oneDie = 1f, twoDice = 1.25f, threeDice = 1.25f, fourDice = 1.25f, fiveDice = 1.25f },
                new CombinationMultiplierEntry { combination = CombinationFamily.TwoPair, oneDie = 1f, twoDice = 1f, threeDice = 1f, fourDice = 1.6f, fiveDice = 1.6f },
                new CombinationMultiplierEntry { combination = CombinationFamily.ThreeOfAKind, oneDie = 1f, twoDice = 1f, threeDice = 1.8f, fourDice = 1.8f, fiveDice = 1.8f },
                new CombinationMultiplierEntry { combination = CombinationFamily.FourOfAKind, oneDie = 1f, twoDice = 1f, threeDice = 1f, fourDice = 2.2f, fiveDice = 2.2f },
                new CombinationMultiplierEntry { combination = CombinationFamily.Straight, oneDie = 1f, twoDice = 1.1f, threeDice = 1.3f, fourDice = 1.7f, fiveDice = 2f },
            };
            EditorUtility.SetDirty(combinationDatabase);

            UpgradeConfig unlockTwo = CreateOrLoadAsset<UpgradeConfig>(DefaultsFolder, "Upgrade_unlock_2.asset");
            unlockTwo.upgradeId = "unlock_2";
            unlockTwo.type = UpgradeType.UnlockDie;
            unlockTwo.title = "Add New Die";
            unlockTwo.description = "Unlock a second die immediately.";
            unlockTwo.value = 1f;
            unlockTwo.eligibleFromLevel = 1;
            unlockTwo.enabled = true;
            unlockTwo.targetDiceCount = 2;
            EditorUtility.SetDirty(unlockTwo);

            UpgradeConfig heal = CreateOrLoadAsset<UpgradeConfig>(DefaultsFolder, "Upgrade_heal.asset");
            heal.upgradeId = "heal_small";
            heal.type = UpgradeType.HealPercent;
            heal.title = "Restore Health";
            heal.description = "Restore 30% of max HP.";
            heal.value = 30f;
            heal.eligibleFromLevel = 1;
            heal.enabled = true;
            heal.targetDiceCount = 0;
            EditorUtility.SetDirty(heal);

            UpgradeConfig damageBonus = CreateOrLoadAsset<UpgradeConfig>(DefaultsFolder, "Upgrade_bonus.asset");
            damageBonus.upgradeId = "flat_bonus_small";
            damageBonus.type = UpgradeType.FlatDamageBonus;
            damageBonus.title = "Sharpen Blade";
            damageBonus.description = "Increase final damage by 2.";
            damageBonus.value = 2f;
            damageBonus.eligibleFromLevel = 1;
            damageBonus.enabled = true;
            damageBonus.targetDiceCount = 0;
            EditorUtility.SetDirty(damageBonus);

            UpgradeDatabase upgradeDatabase = CreateOrLoadAsset<UpgradeDatabase>(DefaultsFolder, "UpgradeDatabase.asset");
            upgradeDatabase.upgrades = new List<UpgradeConfig> { unlockTwo, heal, damageBonus };
            EditorUtility.SetDirty(upgradeDatabase);

            ProgressionDatabase progressionDatabase = CreateOrLoadAsset<ProgressionDatabase>(DefaultsFolder, "ProgressionDatabase.asset");
            progressionDatabase.levels = new List<ProgressionLevelEntry>
            {
                new ProgressionLevelEntry { level = 1, expToNext = 10 },
                new ProgressionLevelEntry { level = 2, expToNext = 16 },
                new ProgressionLevelEntry { level = 3, expToNext = 22 },
            };
            EditorUtility.SetDirty(progressionDatabase);

            HeroPrefabRegistry heroRegistry = CreateOrLoadAsset<HeroPrefabRegistry>(RegistriesFolder, "HeroPrefabRegistry.asset");
            MobPrefabRegistry mobRegistry = CreateOrLoadAsset<MobPrefabRegistry>(RegistriesFolder, "MobPrefabRegistry.asset");
            UISkinRegistry uiSkinRegistry = CreateOrLoadAsset<UISkinRegistry>(RegistriesFolder, "UISkinRegistry.asset");
            VfxRegistry vfxRegistry = CreateOrLoadAsset<VfxRegistry>(RegistriesFolder, "VfxRegistry.asset");
            EditorUtility.SetDirty(heroRegistry);
            EditorUtility.SetDirty(mobRegistry);
            EditorUtility.SetDirty(uiSkinRegistry);
            EditorUtility.SetDirty(vfxRegistry);

            SheetsImportSettings importSettings = CreateOrLoadAsset<SheetsImportSettings>(DefaultsFolder, "SheetsImportSettings.asset");
            importSettings.googleSheetUrl = "https://docs.google.com/spreadsheets/d/1ocPHKALVIMOhgBFsvG_mnZk3aFEVDNsVOsuQ9Y98wEQ/edit?usp=sharing";
            importSettings.csvFolderPath = "Assets/Data/ImportedCsv";
            importSettings.outputFolderPath = "Assets/Resources/DiceBattler/Imported";
            if (importSettings.sheetTabs == null || importSettings.sheetTabs.Count == 0)
            {
                importSettings.sheetTabs = new List<GoogleSheetTabReference>
                {
                    new GoogleSheetTabReference("Hero", string.Empty),
                    new GoogleSheetTabReference("Mobs", string.Empty),
                    new GoogleSheetTabReference("Waves", string.Empty),
                    new GoogleSheetTabReference("Combinations", string.Empty),
                    new GoogleSheetTabReference("Upgrades", string.Empty),
                    new GoogleSheetTabReference("Progression", string.Empty),
                    new GoogleSheetTabReference("RunConfig", string.Empty),
                };
            }
            EditorUtility.SetDirty(importSettings);

            ImportStatusReport statusReport = CreateOrLoadAsset<ImportStatusReport>(DefaultsFolder, "ImportStatusReport.asset");
            EditorUtility.SetDirty(statusReport);

            PrototypeContentSet contentSet = CreateOrLoadAsset<PrototypeContentSet>(ResourcesFolder, "PrototypeContentSet.asset");
            contentSet.heroConfig = heroConfig;
            contentSet.mobDatabase = mobDatabase;
            contentSet.waveDatabase = waveDatabase;
            contentSet.combinationDatabase = combinationDatabase;
            contentSet.upgradeDatabase = upgradeDatabase;
            contentSet.progressionDatabase = progressionDatabase;
            contentSet.runConfig = runConfig;
            contentSet.heroPrefabRegistry = heroRegistry;
            contentSet.mobPrefabRegistry = mobRegistry;
            contentSet.uiSkinRegistry = uiSkinRegistry;
            contentSet.vfxRegistry = vfxRegistry;
            EditorUtility.SetDirty(contentSet);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(contentSet);
            Debug.Log("Dice Battler default assets created under Assets/Configs.");
        }

        [MenuItem("Tools/Dice Battler/Setup/Create Import Sheet Assets")]
        public static void CreateImportSheetAssets()
        {
            EnsureFolder("Assets/Configs");
            EnsureFolder(DefaultsFolder);
            EnsureFolder("Assets/Resources");
            EnsureFolder(ResourcesFolder);

            SheetsImportSettings importSettings = CreateOrLoadAsset<SheetsImportSettings>(DefaultsFolder, "SheetsImportSettings.asset");
            importSettings.googleSheetUrl = "https://docs.google.com/spreadsheets/d/1ocPHKALVIMOhgBFsvG_mnZk3aFEVDNsVOsuQ9Y98wEQ/edit?usp=sharing";
            importSettings.csvFolderPath = "Assets/Data/ImportedCsv";
            importSettings.outputFolderPath = "Assets/Resources/DiceBattler/Imported";
            if (importSettings.sheetTabs == null || importSettings.sheetTabs.Count == 0)
            {
                importSettings.sheetTabs = new List<GoogleSheetTabReference>
                {
                    new GoogleSheetTabReference("Hero", string.Empty),
                    new GoogleSheetTabReference("Mobs", string.Empty),
                    new GoogleSheetTabReference("Waves", string.Empty),
                    new GoogleSheetTabReference("Combinations", string.Empty),
                    new GoogleSheetTabReference("Upgrades", string.Empty),
                    new GoogleSheetTabReference("Progression", string.Empty),
                    new GoogleSheetTabReference("RunConfig", string.Empty),
                };
            }

            ImportStatusReport statusReport = CreateOrLoadAsset<ImportStatusReport>(DefaultsFolder, "ImportStatusReport.asset");
            EditorUtility.SetDirty(importSettings);
            EditorUtility.SetDirty(statusReport);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.objects = new Object[] { importSettings, statusReport };
            Debug.Log("Created SheetsImportSettings.asset and ImportStatusReport.asset under Assets/Configs/Defaults.");
        }

        [MenuItem("Tools/Dice Battler/Setup/Prepare Current Scene For Play")]
        public static void PrepareCurrentSceneForPlay()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                Debug.LogError("No active scene found.");
                return;
            }

            GameBootstrap bootstrap = Object.FindFirstObjectByType<GameBootstrap>();
            if (bootstrap == null)
            {
                GameObject bootstrapGo = new GameObject("Game Bootstrap");
                bootstrap = bootstrapGo.AddComponent<GameBootstrap>();
            }

            EditorSceneManager.MarkSceneDirty(activeScene);
            Selection.activeObject = bootstrap.gameObject;
            Debug.Log("Current scene prepared. Press Play and the prototype will auto-build placeholder UI at runtime.");
        }

        private static T CreateOrLoadAsset<T>(string folderPath, string fileName) where T : ScriptableObject
        {
            string assetPath = $"{folderPath}/{fileName}";
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, assetPath);
            return asset;
        }

        private static void EnsureFolder(string folderPath)
        {
            string[] pieces = folderPath.Split('/');
            string current = pieces[0];
            for (int index = 1; index < pieces.Length; index++)
            {
                string next = $"{current}/{pieces[index]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, pieces[index]);
                }

                current = next;
            }
        }
    }
}
#endif
