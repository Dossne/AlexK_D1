using System;
using System.Collections.Generic;
using DiceBattler.Presentation;
using UnityEngine;

namespace DiceBattler.Configs
{
    public enum UpgradeType
    {
        UnlockDie = 0,
        HealPercent = 1,
        FlatDamageBonus = 2,
    }

    public enum CombinationFamily
    {
        None = 0,
        OnePair = 1,
        TwoPair = 2,
        ThreeOfAKind = 3,
        FourOfAKind = 4,
        Straight = 5,
    }

    [CreateAssetMenu(menuName = "Dice Battler/Configs/Hero Config", fileName = "HeroConfig")]
    public sealed class HeroConfig : ScriptableObject
    {
        public string heroId = "hero_main";
        public string displayName = "Hero";
        public int maxHp = 30;
        [Range(1, 5)] public int startingUnlockedDice = 1;
        [Range(0, 10)] public int startingRerollsPerTurn = 3;
        public int startingFlatDamageBonus;
        public string baseRunId = "run_01";
    }

    [CreateAssetMenu(menuName = "Dice Battler/Configs/Mob Config", fileName = "MobConfig")]
    public sealed class MobConfig : ScriptableObject
    {
        public string mobId;
        public string displayName;
        public int hp = 5;
        public int damageMin = 1;
        public int damageMax = 2;
        public int expReward = 1;
        public string prefabKey;
    }

    [CreateAssetMenu(menuName = "Dice Battler/Configs/Mob Database", fileName = "MobDatabase")]
    public sealed class MobDatabase : ScriptableObject
    {
        public List<MobConfig> mobs = new List<MobConfig>();

        public MobConfig GetById(string mobId)
        {
            return mobs.Find(mob => mob != null && mob.mobId == mobId);
        }
    }

    [CreateAssetMenu(menuName = "Dice Battler/Configs/Wave Config", fileName = "WaveConfig")]
    public sealed class WaveConfig : ScriptableObject
    {
        [Min(1)] public int waveNumber = 1;
        public List<string> mobIds = new List<string>();
        [Min(0)] public int expReward;
    }

    [CreateAssetMenu(menuName = "Dice Battler/Configs/Wave Database", fileName = "WaveDatabase")]
    public sealed class WaveDatabase : ScriptableObject
    {
        [Min(1)] public int totalWaves = 3;
        public List<WaveConfig> waves = new List<WaveConfig>();

        public WaveConfig GetWave(int oneBasedWaveNumber)
        {
            return waves.Find(wave => wave != null && wave.waveNumber == oneBasedWaveNumber);
        }
    }

    [Serializable]
    public struct CombinationMultiplierEntry
    {
        public CombinationFamily combination;
        public float oneDie;
        public float twoDice;
        public float threeDice;
        public float fourDice;
        public float fiveDice;

        public float GetMultiplier(int unlockedDiceCount)
        {
            switch (unlockedDiceCount)
            {
                case 1:
                    return oneDie;
                case 2:
                    return twoDice;
                case 3:
                    return threeDice;
                case 4:
                    return fourDice;
                case 5:
                    return fiveDice;
                default:
                    return 1f;
            }
        }
    }

    [CreateAssetMenu(menuName = "Dice Battler/Configs/Combination Database", fileName = "CombinationDatabase")]
    public sealed class CombinationDatabase : ScriptableObject
    {
        public List<CombinationMultiplierEntry> entries = new List<CombinationMultiplierEntry>();

        public float GetMultiplier(CombinationFamily family, int unlockedDiceCount)
        {
            for (int index = 0; index < entries.Count; index++)
            {
                if (entries[index].combination == family)
                {
                    return Mathf.Max(1f, entries[index].GetMultiplier(unlockedDiceCount));
                }
            }

            return 1f;
        }
    }

    [CreateAssetMenu(menuName = "Dice Battler/Configs/Upgrade Config", fileName = "UpgradeConfig")]
    public sealed class UpgradeConfig : ScriptableObject
    {
        public string upgradeId;
        public UpgradeType type;
        public string title;
        [TextArea] public string description;
        public float value = 1f;
        [Min(1)] public int eligibleFromLevel = 1;
        public bool enabled = true;
        [Range(0, 5)] public int targetDiceCount;
        public string uiIconKey;
    }

    [CreateAssetMenu(menuName = "Dice Battler/Configs/Upgrade Database", fileName = "UpgradeDatabase")]
    public sealed class UpgradeDatabase : ScriptableObject
    {
        public List<UpgradeConfig> upgrades = new List<UpgradeConfig>();
    }

    [Serializable]
    public struct ProgressionLevelEntry
    {
        [Min(1)] public int level;
        [Min(1)] public int expToNext;
    }

    [CreateAssetMenu(menuName = "Dice Battler/Configs/Progression Database", fileName = "ProgressionDatabase")]
    public sealed class ProgressionDatabase : ScriptableObject
    {
        public List<ProgressionLevelEntry> levels = new List<ProgressionLevelEntry>();

        public int GetThresholdForLevel(int level)
        {
            for (int index = 0; index < levels.Count; index++)
            {
                if (levels[index].level == level)
                {
                    return levels[index].expToNext;
                }
            }

            return 0;
        }

        public bool HasNextLevel(int level)
        {
            return GetThresholdForLevel(level) > 0;
        }
    }

    [CreateAssetMenu(menuName = "Dice Battler/Configs/Run Config", fileName = "RunConfig")]
    public sealed class RunConfig : ScriptableObject
    {
        public string runId = "run_01";
        [Range(1, 99)] public int totalWaves = 3;
        [Range(1, 5)] public int diceSlotsTotal = 5;
        [Range(0, 10)] public int rerollsPerTurnDefault = 3;
        [Min(0f)] public float diceHighlightDuration = 0.35f;
        [Min(0f)] public float postWaveRunTransitionDuration = 0.65f;
        [Min(0f)] public float damagePanelDisplayDuration = 1f;
        public bool showBonusWhenZero = true;
        public bool lockedDiceVisible = true;
    }

    [CreateAssetMenu(menuName = "Dice Battler/Configs/Import Settings", fileName = "SheetsImportSettings")]
    public sealed class SheetsImportSettings : ScriptableObject
    {
        public string csvFolderPath = "Assets/Data/ImportedCsv";
        public string outputFolderPath = "Assets/Configs/Imported";
        public string spreadsheetId = "1ocPHKALVIMOhgBFsvG_mnZk3aFEVDNsVOsuQ9Y98wEQ";
        public List<GoogleSheetTabReference> sheetTabs = new List<GoogleSheetTabReference>
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

    [Serializable]
    public sealed class GoogleSheetTabReference
    {
        public GoogleSheetTabReference(string fileName, string gid)
        {
            this.fileName = fileName;
            this.gid = gid;
        }

        public string fileName;
        public string gid;
    }

    [Serializable]
    public struct ImportStatusEntry
    {
        public string severity;
        public string message;
    }

    [CreateAssetMenu(menuName = "Dice Battler/Configs/Import Status Report", fileName = "ImportStatusReport")]
    public sealed class ImportStatusReport : ScriptableObject
    {
        public string sourceIdentifier;
        public string importedAtUtc;
        public bool importSucceeded;
        public List<ImportStatusEntry> entries = new List<ImportStatusEntry>();
    }

    [CreateAssetMenu(menuName = "Dice Battler/Configs/Prototype Content Set", fileName = "PrototypeContentSet")]
    public sealed class PrototypeContentSet : ScriptableObject
    {
        public HeroConfig heroConfig;
        public MobDatabase mobDatabase;
        public WaveDatabase waveDatabase;
        public CombinationDatabase combinationDatabase;
        public UpgradeDatabase upgradeDatabase;
        public ProgressionDatabase progressionDatabase;
        public RunConfig runConfig;
        public HeroPrefabRegistry heroPrefabRegistry;
        public MobPrefabRegistry mobPrefabRegistry;
        public UISkinRegistry uiSkinRegistry;
        public VfxRegistry vfxRegistry;
    }
}
