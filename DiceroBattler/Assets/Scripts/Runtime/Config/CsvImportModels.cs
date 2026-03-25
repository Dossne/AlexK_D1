using System;
using System.Collections.Generic;
using DiceBattler.Configs;
using DiceBattler.Runtime;
using UnityEngine;

namespace DiceBattler.Importing
{
    [Serializable]
    public sealed class HeroRow
    {
        public string id;
        public string displayName;
        public int maxHp;
        public int startingUnlockedDice = 1;
        public int startingRerollsPerTurn = 3;
        public int startingFlatDamageBonus;
        public string baseRunId = "run_01";
    }

    [Serializable]
    public sealed class MobRow
    {
        public string id;
        public string displayName;
        public int hp;
        public int damageMin;
        public int damageMax;
        public int expReward;
        public string prefabKey;
    }

    [Serializable]
    public sealed class WaveRow
    {
        public int waveNumber;
        public List<string> mobList = new List<string>();
        public int expReward;
    }

    [Serializable]
    public sealed class CombinationRow
    {
        public CombinationFamily combination;
        public float one;
        public float two;
        public float three;
        public float four;
        public float five;
    }

    [Serializable]
    public sealed class UpgradeRow
    {
        public string id;
        public UpgradeType type;
        public string title;
        public string description;
        public float value;
        public int eligibleFromLevel = 1;
        public bool enabled = true;
        public int targetDiceCount;
        public string uiIconKey;
    }

    [Serializable]
    public sealed class ProgressionRow
    {
        public int level;
        public int expToNext;
    }

    [Serializable]
    public sealed class RunConfigRow
    {
        public string runId = "run_01";
        public int totalWaves = 3;
        public int diceSlotsTotal = 5;
        public int rerollsPerTurnDefault = 3;
        public float diceHighlightDuration = 0.35f;
        public float postWaveRunTransitionDuration = 0.65f;
        public float damagePanelDisplayDuration = 1f;
        public bool showBonusWhenZero = true;
        public bool lockedDiceVisible = true;
    }

    [Serializable]
    public sealed class ImportedGameData
    {
        public HeroRow hero;
        public List<MobRow> mobs = new List<MobRow>();
        public List<WaveRow> waves = new List<WaveRow>();
        public List<CombinationRow> combinations = new List<CombinationRow>();
        public List<UpgradeRow> upgrades = new List<UpgradeRow>();
        public List<ProgressionRow> progression = new List<ProgressionRow>();
        public RunConfigRow runConfig = new RunConfigRow();
    }

    public sealed class CsvImportValidator
    {
        public List<ValidationIssue> Validate(ImportedGameData data)
        {
            List<ValidationIssue> issues = new List<ValidationIssue>();

            if (data == null)
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Fatal, "No import data was provided."));
                return issues;
            }

            ValidateHero(data, issues);
            ValidateMobs(data, issues);
            ValidateRunConfig(data.runConfig, issues);
            ValidateWaves(data, issues);
            ValidateCombinations(data, issues);
            ValidateUpgrades(data, issues);
            ValidateProgression(data, issues);
            return issues;
        }

        private static void ValidateHero(ImportedGameData data, List<ValidationIssue> issues)
        {
            if (data.hero == null)
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Fatal, "Hero row is missing."));
                return;
            }

            if (string.IsNullOrWhiteSpace(data.hero.id))
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Fatal, "Hero id is required."));
            }

            if (data.hero.maxHp <= 0)
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Fatal, "Hero max HP must be positive."));
            }

            if (data.hero.startingUnlockedDice < 1 || data.hero.startingUnlockedDice > 5)
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Fatal, "Hero starting unlocked dice must be between 1 and 5."));
            }
        }

        private static void ValidateMobs(ImportedGameData data, List<ValidationIssue> issues)
        {
            HashSet<string> mobIds = new HashSet<string>();
            for (int index = 0; index < data.mobs.Count; index++)
            {
                MobRow mob = data.mobs[index];
                if (!mobIds.Add(mob.id))
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Fatal, $"Duplicate mob id: {mob.id}"));
                }

                if (mob.hp <= 0)
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Fatal, $"Mob {mob.id} must have positive HP."));
                }

                if (mob.damageMin < 0 || mob.damageMax < mob.damageMin)
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Fatal, $"Mob {mob.id} has invalid intent damage range."));
                }

                if (string.IsNullOrWhiteSpace(mob.prefabKey))
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Fatal, $"Mob {mob.id} is missing prefabKey."));
                }
            }
        }

        private static void ValidateRunConfig(RunConfigRow row, List<ValidationIssue> issues)
        {
            if (row == null)
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Fatal, "RunConfig row is missing."));
                return;
            }

            if (row.totalWaves <= 0)
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Fatal, "RunConfig totalWaves must be positive."));
            }

            if (row.diceSlotsTotal != 5)
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Fatal, "RunConfig diceSlotsTotal must be 5 for this prototype."));
            }
        }

        private static void ValidateWaves(ImportedGameData data, List<ValidationIssue> issues)
        {
            HashSet<int> waveNumbers = new HashSet<int>();
            HashSet<string> knownMobIds = new HashSet<string>();
            for (int index = 0; index < data.mobs.Count; index++)
            {
                knownMobIds.Add(data.mobs[index].id);
            }

            for (int index = 0; index < data.waves.Count; index++)
            {
                WaveRow wave = data.waves[index];
                if (!waveNumbers.Add(wave.waveNumber))
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Fatal, $"Duplicate wave number: {wave.waveNumber}"));
                }

                if (wave.mobList.Count == 0 || wave.mobList.Count > 3)
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Fatal, $"Wave {wave.waveNumber} must contain 1 to 3 enemies."));
                }

                for (int mobIndex = 0; mobIndex < wave.mobList.Count; mobIndex++)
                {
                    if (!knownMobIds.Contains(wave.mobList[mobIndex]))
                    {
                        issues.Add(new ValidationIssue(ValidationSeverity.Fatal, $"Wave {wave.waveNumber} references unknown mob {wave.mobList[mobIndex]}."));
                    }
                }
            }

            if (data.runConfig != null && data.waves.Count != data.runConfig.totalWaves)
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Fatal, "Wave row count does not match RunConfig totalWaves."));
            }
        }

        private static void ValidateCombinations(ImportedGameData data, List<ValidationIssue> issues)
        {
            HashSet<CombinationFamily> combinations = new HashSet<CombinationFamily>();
            for (int index = 0; index < data.combinations.Count; index++)
            {
                CombinationRow row = data.combinations[index];
                if (!combinations.Add(row.combination))
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Fatal, $"Duplicate combination entry: {row.combination}"));
                }

                if (row.one < 0f || row.two < 0f || row.three < 0f || row.four < 0f || row.five < 0f)
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Fatal, $"Combination {row.combination} has a non-numeric or negative multiplier."));
                }
            }

            foreach (CombinationFamily family in Enum.GetValues(typeof(CombinationFamily)))
            {
                if (family == CombinationFamily.None)
                {
                    continue;
                }

                if (!combinations.Contains(family))
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Fatal, $"Missing combination row: {family}"));
                }
            }
        }

        private static void ValidateUpgrades(ImportedGameData data, List<ValidationIssue> issues)
        {
            HashSet<string> ids = new HashSet<string>();
            for (int index = 0; index < data.upgrades.Count; index++)
            {
                UpgradeRow row = data.upgrades[index];
                if (!ids.Add(row.id))
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Fatal, $"Duplicate upgrade id: {row.id}"));
                }

                if (row.enabled && row.value <= 0f)
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Fatal, $"Upgrade {row.id} must have a positive value."));
                }

                if (row.type == UpgradeType.UnlockDie && (row.targetDiceCount < 2 || row.targetDiceCount > 5))
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Fatal, $"Upgrade {row.id} must target a dice count between 2 and 5."));
                }
            }
        }

        private static void ValidateProgression(ImportedGameData data, List<ValidationIssue> issues)
        {
            HashSet<int> levels = new HashSet<int>();
            int maxSingleReward = 0;
            for (int index = 0; index < data.mobs.Count; index++)
            {
                maxSingleReward = Mathf.Max(maxSingleReward, data.mobs[index].expReward);
            }

            for (int index = 0; index < data.waves.Count; index++)
            {
                maxSingleReward += data.waves[index].expReward;
            }

            for (int index = 0; index < data.progression.Count; index++)
            {
                ProgressionRow row = data.progression[index];
                if (!levels.Add(row.level))
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Fatal, $"Duplicate progression level: {row.level}"));
                }

                if (row.expToNext <= 0)
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Fatal, $"Progression level {row.level} must have positive expToNext."));
                }
                else if (row.expToNext <= maxSingleReward)
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Warning, $"Progression level {row.level} may allow a one-grant level-up edge case."));
                }
            }
        }
    }
}
