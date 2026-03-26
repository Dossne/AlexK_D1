using System.Collections.Generic;
using DiceBattler.Configs;
using DiceBattler.Importing;
using DiceBattler.Presentation;
using UnityEngine;

namespace DiceBattler.Boot
{
    public static class PrototypeRuntimeFactory
    {
        public static PrototypeContentSet CreateImportedContentSet(ImportedGameData data, PrototypeContentSet registrySource = null)
        {
            if (data == null || data.hero == null || data.runConfig == null)
            {
                return null;
            }

            HeroConfig hero = ScriptableObject.CreateInstance<HeroConfig>();
            hero.heroId = data.hero.id;
            hero.displayName = data.hero.displayName;
            hero.maxHp = data.hero.maxHp;
            hero.startingUnlockedDice = data.hero.startingUnlockedDice;
            hero.startingRerollsPerTurn = data.hero.startingRerollsPerTurn;
            hero.startingFlatDamageBonus = data.hero.startingFlatDamageBonus;
            hero.baseRunId = data.hero.baseRunId;

            RunConfig runConfig = ScriptableObject.CreateInstance<RunConfig>();
            runConfig.runId = data.runConfig.runId;
            runConfig.totalWaves = data.runConfig.totalWaves;
            runConfig.diceSlotsTotal = data.runConfig.diceSlotsTotal;
            runConfig.rerollsPerTurnDefault = data.runConfig.rerollsPerTurnDefault;
            runConfig.diceHighlightDuration = data.runConfig.diceHighlightDuration;
            runConfig.postWaveRunTransitionDuration = data.runConfig.postWaveRunTransitionDuration;
            runConfig.damagePanelDisplayDuration = data.runConfig.damagePanelDisplayDuration;
            runConfig.showBonusWhenZero = data.runConfig.showBonusWhenZero;
            runConfig.lockedDiceVisible = data.runConfig.lockedDiceVisible;

            List<MobConfig> mobs = new List<MobConfig>(data.mobs.Count);
            for (int index = 0; index < data.mobs.Count; index++)
            {
                MobRow row = data.mobs[index];
                MobConfig mob = ScriptableObject.CreateInstance<MobConfig>();
                mob.mobId = row.id;
                mob.displayName = row.displayName;
                mob.hp = row.hp;
                mob.damageMin = row.damageMin;
                mob.damageMax = row.damageMax;
                mob.expReward = row.expReward;
                mob.prefabKey = row.prefabKey;
                mobs.Add(mob);
            }

            MobDatabase mobDatabase = ScriptableObject.CreateInstance<MobDatabase>();
            mobDatabase.mobs = mobs;

            List<WaveConfig> waves = new List<WaveConfig>(data.waves.Count);
            for (int index = 0; index < data.waves.Count; index++)
            {
                WaveRow row = data.waves[index];
                WaveConfig wave = ScriptableObject.CreateInstance<WaveConfig>();
                wave.waveNumber = row.waveNumber;
                wave.mobIds = new List<string>(row.mobList);
                wave.expReward = row.expReward;
                waves.Add(wave);
            }

            WaveDatabase waveDatabase = ScriptableObject.CreateInstance<WaveDatabase>();
            waveDatabase.totalWaves = data.runConfig.totalWaves;
            waveDatabase.waves = waves;

            CombinationDatabase combinationDatabase = ScriptableObject.CreateInstance<CombinationDatabase>();
            combinationDatabase.entries = new List<CombinationMultiplierEntry>(data.combinations.Count);
            for (int index = 0; index < data.combinations.Count; index++)
            {
                CombinationRow row = data.combinations[index];
                combinationDatabase.entries.Add(new CombinationMultiplierEntry
                {
                    combination = row.combination,
                    oneDie = row.one,
                    twoDice = row.two,
                    threeDice = row.three,
                    fourDice = row.four,
                    fiveDice = row.five,
                });
            }

            List<UpgradeConfig> upgrades = new List<UpgradeConfig>(data.upgrades.Count);
            for (int index = 0; index < data.upgrades.Count; index++)
            {
                UpgradeRow row = data.upgrades[index];
                UpgradeConfig upgrade = ScriptableObject.CreateInstance<UpgradeConfig>();
                upgrade.upgradeId = row.id;
                upgrade.type = row.type;
                upgrade.title = row.title;
                upgrade.description = row.description;
                upgrade.value = row.value;
                upgrade.eligibleFromLevel = row.eligibleFromLevel;
                upgrade.enabled = row.enabled;
                upgrade.targetDiceCount = row.targetDiceCount;
                upgrade.uiIconKey = row.uiIconKey;
                upgrades.Add(upgrade);
            }

            UpgradeDatabase upgradeDatabase = ScriptableObject.CreateInstance<UpgradeDatabase>();
            upgradeDatabase.upgrades = upgrades;

            ProgressionDatabase progressionDatabase = ScriptableObject.CreateInstance<ProgressionDatabase>();
            progressionDatabase.levels = new List<ProgressionLevelEntry>(data.progression.Count);
            for (int index = 0; index < data.progression.Count; index++)
            {
                ProgressionRow row = data.progression[index];
                progressionDatabase.levels.Add(new ProgressionLevelEntry
                {
                    level = row.level,
                    expToNext = row.expToNext,
                });
            }

            PrototypeContentSet contentSet = ScriptableObject.CreateInstance<PrototypeContentSet>();
            contentSet.name = "ImportedSnapshotContentSet";
            contentSet.heroConfig = hero;
            contentSet.mobDatabase = mobDatabase;
            contentSet.waveDatabase = waveDatabase;
            contentSet.combinationDatabase = combinationDatabase;
            contentSet.upgradeDatabase = upgradeDatabase;
            contentSet.progressionDatabase = progressionDatabase;
            contentSet.runConfig = runConfig;
            contentSet.heroPrefabRegistry = registrySource != null ? registrySource.heroPrefabRegistry : ScriptableObject.CreateInstance<HeroPrefabRegistry>();
            contentSet.mobPrefabRegistry = registrySource != null ? registrySource.mobPrefabRegistry : ScriptableObject.CreateInstance<MobPrefabRegistry>();
            contentSet.uiSkinRegistry = registrySource != null ? registrySource.uiSkinRegistry : ScriptableObject.CreateInstance<UISkinRegistry>();
            contentSet.vfxRegistry = registrySource != null ? registrySource.vfxRegistry : ScriptableObject.CreateInstance<VfxRegistry>();
            return contentSet;
        }

        public static PrototypeContentSet CreateInMemoryContentSet()
        {
            HeroConfig hero = ScriptableObject.CreateInstance<HeroConfig>();
            hero.heroId = "hero_main";
            hero.displayName = "Knight";
            hero.maxHp = 30;
            hero.startingUnlockedDice = 1;
            hero.startingRerollsPerTurn = 3;

            RunConfig runConfig = ScriptableObject.CreateInstance<RunConfig>();
            runConfig.runId = "run_01";
            runConfig.totalWaves = 3;
            runConfig.diceSlotsTotal = 5;
            runConfig.rerollsPerTurnDefault = 3;

            MobConfig slime = ScriptableObject.CreateInstance<MobConfig>();
            slime.mobId = "slime";
            slime.displayName = "Slime";
            slime.hp = 8;
            slime.damageMin = 1;
            slime.damageMax = 3;
            slime.expReward = 3;
            slime.prefabKey = "slime";

            MobConfig goblin = ScriptableObject.CreateInstance<MobConfig>();
            goblin.mobId = "goblin";
            goblin.displayName = "Goblin";
            goblin.hp = 12;
            goblin.damageMin = 2;
            goblin.damageMax = 4;
            goblin.expReward = 4;
            goblin.prefabKey = "goblin";

            MobConfig archer = ScriptableObject.CreateInstance<MobConfig>();
            archer.mobId = "archer";
            archer.displayName = "Archer";
            archer.hp = 10;
            archer.damageMin = 3;
            archer.damageMax = 5;
            archer.expReward = 5;
            archer.prefabKey = "archer";

            MobDatabase mobDatabase = ScriptableObject.CreateInstance<MobDatabase>();
            mobDatabase.mobs = new List<MobConfig> { slime, goblin, archer };

            WaveConfig wave01 = ScriptableObject.CreateInstance<WaveConfig>();
            wave01.waveNumber = 1;
            wave01.mobIds = new List<string> { "slime" };
            wave01.expReward = 4;

            WaveConfig wave02 = ScriptableObject.CreateInstance<WaveConfig>();
            wave02.waveNumber = 2;
            wave02.mobIds = new List<string> { "slime", "goblin" };
            wave02.expReward = 5;

            WaveConfig wave03 = ScriptableObject.CreateInstance<WaveConfig>();
            wave03.waveNumber = 3;
            wave03.mobIds = new List<string> { "goblin", "archer", "slime" };
            wave03.expReward = 6;

            WaveDatabase waveDatabase = ScriptableObject.CreateInstance<WaveDatabase>();
            waveDatabase.totalWaves = 3;
            waveDatabase.waves = new List<WaveConfig> { wave01, wave02, wave03 };

            CombinationDatabase combinationDatabase = ScriptableObject.CreateInstance<CombinationDatabase>();
            combinationDatabase.entries = new List<CombinationMultiplierEntry>
            {
                new CombinationMultiplierEntry { combination = CombinationFamily.OnePair, oneDie = 1f, twoDice = 1.25f, threeDice = 1.25f, fourDice = 1.25f, fiveDice = 1.25f },
                new CombinationMultiplierEntry { combination = CombinationFamily.TwoPair, oneDie = 1f, twoDice = 1f, threeDice = 1f, fourDice = 1.6f, fiveDice = 1.6f },
                new CombinationMultiplierEntry { combination = CombinationFamily.ThreeOfAKind, oneDie = 1f, twoDice = 1f, threeDice = 1.8f, fourDice = 1.8f, fiveDice = 1.8f },
                new CombinationMultiplierEntry { combination = CombinationFamily.FourOfAKind, oneDie = 1f, twoDice = 1f, threeDice = 1f, fourDice = 2.2f, fiveDice = 2.2f },
                new CombinationMultiplierEntry { combination = CombinationFamily.Straight, oneDie = 1f, twoDice = 1.1f, threeDice = 1.3f, fourDice = 1.7f, fiveDice = 2f },
            };

            UpgradeConfig unlockTwo = ScriptableObject.CreateInstance<UpgradeConfig>();
            unlockTwo.upgradeId = "unlock_2";
            unlockTwo.type = UpgradeType.UnlockDie;
            unlockTwo.title = "Add New Die";
            unlockTwo.description = "Unlock a second die immediately.";
            unlockTwo.value = 1f;
            unlockTwo.targetDiceCount = 2;

            UpgradeConfig heal = ScriptableObject.CreateInstance<UpgradeConfig>();
            heal.upgradeId = "heal_small";
            heal.type = UpgradeType.HealPercent;
            heal.title = "Restore Health";
            heal.description = "Restore 30% of max HP.";
            heal.value = 30f;

            UpgradeConfig damageBonus = ScriptableObject.CreateInstance<UpgradeConfig>();
            damageBonus.upgradeId = "flat_bonus_small";
            damageBonus.type = UpgradeType.FlatDamageBonus;
            damageBonus.title = "Sharpen Blade";
            damageBonus.description = "Increase final damage by 2.";
            damageBonus.value = 2f;

            UpgradeDatabase upgradeDatabase = ScriptableObject.CreateInstance<UpgradeDatabase>();
            upgradeDatabase.upgrades = new List<UpgradeConfig> { unlockTwo, heal, damageBonus };

            ProgressionDatabase progressionDatabase = ScriptableObject.CreateInstance<ProgressionDatabase>();
            progressionDatabase.levels = new List<ProgressionLevelEntry>
            {
                new ProgressionLevelEntry { level = 1, expToNext = 10 },
                new ProgressionLevelEntry { level = 2, expToNext = 16 },
                new ProgressionLevelEntry { level = 3, expToNext = 22 },
            };

            PrototypeContentSet contentSet = ScriptableObject.CreateInstance<PrototypeContentSet>();
            contentSet.heroConfig = hero;
            contentSet.mobDatabase = mobDatabase;
            contentSet.waveDatabase = waveDatabase;
            contentSet.combinationDatabase = combinationDatabase;
            contentSet.upgradeDatabase = upgradeDatabase;
            contentSet.progressionDatabase = progressionDatabase;
            contentSet.runConfig = runConfig;
            contentSet.heroPrefabRegistry = ScriptableObject.CreateInstance<HeroPrefabRegistry>();
            contentSet.mobPrefabRegistry = ScriptableObject.CreateInstance<MobPrefabRegistry>();
            contentSet.uiSkinRegistry = ScriptableObject.CreateInstance<UISkinRegistry>();
            contentSet.vfxRegistry = ScriptableObject.CreateInstance<VfxRegistry>();
            return contentSet;
        }
    }
}
