using System.Collections.Generic;
using DiceBattler.Configs;
using DiceBattler.Importing;
using DiceBattler.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DiceBattler.Tests
{
    public class DiceBattlerCoreTests
    {
        [Test]
        public void PrefixRerollOnlyChangesTappedDieAndToTheLeft()
        {
            StubRandomService random = new StubRandomService(2, 3, 4, 5, 6, 1, 1, 1);
            DiceController controller = new DiceController(5, random);
            controller.BeginTurn(3, 3);
            controller.RollAllUnlocked();

            List<int> before = controller.GetUnlockedValues();
            Assert.That(before, Is.EqualTo(new[] { 2, 3, 4 }));

            controller.TryRerollPrefix(1);
            List<int> after = controller.GetUnlockedValues();

            Assert.That(after[0], Is.EqualTo(5));
            Assert.That(after[1], Is.EqualTo(6));
            Assert.That(after[2], Is.EqualTo(4));
        }

        [Test]
        public void CombinationResolutionPicksBestMultiplier()
        {
            CombinationDatabase database = ScriptableObject.CreateInstance<CombinationDatabase>();
            database.entries = new List<CombinationMultiplierEntry>
            {
                new CombinationMultiplierEntry { combination = CombinationFamily.OnePair, fiveDice = 1.2f },
                new CombinationMultiplierEntry { combination = CombinationFamily.TwoPair, fiveDice = 1.5f },
                new CombinationMultiplierEntry { combination = CombinationFamily.ThreeOfAKind, fiveDice = 2f },
                new CombinationMultiplierEntry { combination = CombinationFamily.FourOfAKind, fiveDice = 2.5f },
                new CombinationMultiplierEntry { combination = CombinationFamily.Straight, fiveDice = 1.8f },
            };

            DamageCalculationService service = new DamageCalculationService(database, new CombinationEvaluationService());
            DamageCalculationResult result = service.Calculate(new List<int> { 3, 3, 3, 4, 4 }, 2);

            Assert.That(result.Combination, Is.EqualTo(CombinationFamily.ThreeOfAKind));
            Assert.That(result.Multiplier, Is.EqualTo(2f));
            Assert.That(result.FinalDamage, Is.EqualTo(34));
        }

        [Test]
        public void TargetingUsesFrontLineAndRightPriority()
        {
            TargetingService service = new TargetingService();
            List<EnemyRuntimeUnit> enemies = new List<EnemyRuntimeUnit>
            {
                new EnemyRuntimeUnit(CreateMob("front_left"), FormationSlot.FrontLeft),
                new EnemyRuntimeUnit(CreateMob("front_right"), FormationSlot.FrontRight),
                new EnemyRuntimeUnit(CreateMob("back_left"), FormationSlot.BackLeft),
            };

            EnemyRuntimeUnit target = service.ResolveHeroTarget(enemies);
            Assert.That(target.Config.mobId, Is.EqualTo("front_right"));
        }

        [Test]
        public void ExpGrantDoesNotOverflowAndOnlyFlagsOneLevelUp()
        {
            ProgressionDatabase progression = ScriptableObject.CreateInstance<ProgressionDatabase>();
            progression.levels = new List<ProgressionLevelEntry>
            {
                new ProgressionLevelEntry { level = 1, expToNext = 10 },
                new ProgressionLevelEntry { level = 2, expToNext = 20 },
            };

            HeroConfig hero = ScriptableObject.CreateInstance<HeroConfig>();
            RunConfig run = ScriptableObject.CreateInstance<RunConfig>();
            RunSession session = new RunSession(hero, run);
            ExpController controller = new ExpController(progression);

            ExpGrantResult result = controller.Grant(session, 50, true);

            Assert.That(result.AppliedAmount, Is.EqualTo(10));
            Assert.That(session.PendingLevelUp, Is.True);
            Assert.That(session.CurrentLevelExp, Is.EqualTo(10));
        }

        [Test]
        public void UnlockDieUpgradesBecomeIneligibleAtCurrentCount()
        {
            RunConfig run = ScriptableObject.CreateInstance<RunConfig>();
            run.diceSlotsTotal = 5;

            HeroConfig hero = ScriptableObject.CreateInstance<HeroConfig>();
            hero.startingUnlockedDice = 4;
            RunSession session = new RunSession(hero, run);
            UpgradeDatabase database = ScriptableObject.CreateInstance<UpgradeDatabase>();
            database.upgrades = new List<UpgradeConfig>
            {
                CreateUnlockUpgrade("unlock_4", 4),
                CreateUnlockUpgrade("unlock_5", 5),
            };

            UpgradeSelectionService service = new UpgradeSelectionService(run, new StubRandomService(0, 0, 0));
            UpgradeOfferSet offers = service.GenerateOffers(session, database);

            Assert.That(offers.Options.TrueForAll(option => option.targetDiceCount == 5));
        }

        [Test]
        public void ImportValidationCatchesDuplicateIdsAndBrokenWaveReferences()
        {
            ImportedGameData data = CreateValidImportedData();
            data.mobs.Add(new MobRow
            {
                id = "slime",
                displayName = "Other Slime",
                hp = 5,
                damageMin = 1,
                damageMax = 2,
                expReward = 1,
                prefabKey = "slime_prefab",
            });
            data.waves[0].mobList[0] = "missing";

            CsvImportValidator validator = new CsvImportValidator();
            List<ValidationIssue> issues = validator.Validate(data);

            Assert.That(issues.Exists(issue => issue.Message.Contains("Duplicate mob id")));
            Assert.That(issues.Exists(issue => issue.Message.Contains("unknown mob")));
        }

        [Test]
        public void ImportValidationRequiresFiveDiceSlots()
        {
            ImportedGameData data = CreateValidImportedData();
            data.runConfig.diceSlotsTotal = 4;

            CsvImportValidator validator = new CsvImportValidator();
            List<ValidationIssue> issues = validator.Validate(data);

            Assert.That(issues.Exists(issue => issue.Message.Contains("diceSlotsTotal must be 5")));
        }

        private static ImportedGameData CreateValidImportedData()
        {
            return new ImportedGameData
            {
                hero = new HeroRow
                {
                    id = "hero_main",
                    displayName = "Hero",
                    maxHp = 20,
                    startingUnlockedDice = 1,
                    startingRerollsPerTurn = 3,
                },
                mobs = new List<MobRow>
                {
                    new MobRow
                    {
                        id = "slime",
                        displayName = "Slime",
                        hp = 5,
                        damageMin = 1,
                        damageMax = 2,
                        expReward = 1,
                        prefabKey = "slime_prefab",
                    },
                },
                waves = new List<WaveRow>
                {
                    new WaveRow
                    {
                        waveNumber = 1,
                        mobList = new List<string> { "slime" },
                        expReward = 1,
                    },
                },
                combinations = new List<CombinationRow>
                {
                    new CombinationRow { combination = CombinationFamily.OnePair, one = 1f, two = 1.2f, three = 1.2f, four = 1.2f, five = 1.2f },
                    new CombinationRow { combination = CombinationFamily.TwoPair, one = 1f, two = 1f, three = 1f, four = 1.5f, five = 1.5f },
                    new CombinationRow { combination = CombinationFamily.ThreeOfAKind, one = 1f, two = 1f, three = 1.8f, four = 1.8f, five = 1.8f },
                    new CombinationRow { combination = CombinationFamily.FourOfAKind, one = 1f, two = 1f, three = 1f, four = 2f, five = 2f },
                    new CombinationRow { combination = CombinationFamily.Straight, one = 1f, two = 1.1f, three = 1.3f, four = 1.6f, five = 1.9f },
                },
                upgrades = new List<UpgradeRow>
                {
                    new UpgradeRow { id = "heal", type = UpgradeType.HealPercent, title = "Heal", description = "Recover HP", value = 20f, enabled = true, eligibleFromLevel = 1 },
                },
                progression = new List<ProgressionRow>
                {
                    new ProgressionRow { level = 1, expToNext = 10 },
                },
                runConfig = new RunConfigRow
                {
                    totalWaves = 1,
                    diceSlotsTotal = 5,
                    rerollsPerTurnDefault = 3,
                },
            };
        }

        private static UpgradeConfig CreateUnlockUpgrade(string id, int targetDiceCount)
        {
            UpgradeConfig upgrade = ScriptableObject.CreateInstance<UpgradeConfig>();
            upgrade.upgradeId = id;
            upgrade.type = UpgradeType.UnlockDie;
            upgrade.enabled = true;
            upgrade.value = 1f;
            upgrade.targetDiceCount = targetDiceCount;
            upgrade.eligibleFromLevel = 1;
            return upgrade;
        }

        private static MobConfig CreateMob(string id)
        {
            MobConfig mob = ScriptableObject.CreateInstance<MobConfig>();
            mob.mobId = id;
            mob.displayName = id;
            mob.hp = 5;
            mob.damageMin = 1;
            mob.damageMax = 2;
            mob.expReward = 1;
            mob.prefabKey = id;
            return mob;
        }

        private sealed class StubRandomService : IRandomService
        {
            private readonly Queue<int> values;

            public StubRandomService(params int[] values)
            {
                this.values = new Queue<int>(values);
            }

            public int RangeInclusive(int minInclusive, int maxInclusive)
            {
                return values.Dequeue();
            }

            public int RangeExclusive(int minInclusive, int maxExclusive)
            {
                return values.Count > 0 ? values.Dequeue() : minInclusive;
            }
        }
    }
}
