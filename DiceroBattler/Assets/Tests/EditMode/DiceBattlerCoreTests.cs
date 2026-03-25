using System.Collections.Generic;
using System.Reflection;
using DiceBattler.Configs;
using DiceBattler.Importing;
using DiceBattler.Presentation;
using DiceBattler.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

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
        public void RerollBudgetStopsAtZeroAfterThreeConsumes()
        {
            RerollBudgetController controller = new RerollBudgetController();
            controller.BeginTurn(3);

            Assert.That(controller.TryConsume(), Is.True);
            Assert.That(controller.TryConsume(), Is.True);
            Assert.That(controller.TryConsume(), Is.True);
            Assert.That(controller.TryConsume(), Is.False);
            Assert.That(controller.Remaining, Is.EqualTo(0));
        }

        [Test]
        public void HandleDiePressedConsumesOneRerollAndLocksFurtherClicksImmediately()
        {
            GameObject root = new GameObject("CombatFlowControllerTest");
            try
            {
                CombatFlowController flowController = root.AddComponent<CombatFlowController>();
                SetPrivateField(flowController, "currentPhase", CombatFlowPhase.PlayerDecision);
                SetPrivateField(flowController, "runSession", CreateRunSession());
                SetPrivateField(flowController, "diceController", CreateDiceControllerForReroll());

                RerollBudgetController budget = new RerollBudgetController();
                budget.BeginTurn(3);
                SetPrivateField(flowController, "rerollBudget", budget);
                SetPrivateField(flowController, "contentSet", CreateContentSet());
                SetPrivateField(flowController, "hudPresenter", CreateHudPresenter(root));
                SetPrivateField(flowController, "diePresenters", CreateDiePresenters(root, 5));
                SetPrivateField(flowController, "latestDamage", new DamageCalculationResult());

                InvokePrivateMethod(flowController, "HandleDiePressed", 1);

                RunSession session = GetPrivateField<RunSession>(flowController, "runSession");
                Assert.That(budget.Remaining, Is.EqualTo(2));
                Assert.That(session.RerollsRemaining, Is.EqualTo(2));
                Assert.That(GetPrivateField<CombatFlowPhase>(flowController, "currentPhase"), Is.EqualTo(CombatFlowPhase.PlayerRoll));

                InvokePrivateMethod(flowController, "HandleDiePressed", 1);

                Assert.That(budget.Remaining, Is.EqualTo(2));
                Assert.That(session.RerollsRemaining, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CombatHudPresenterHidesAndShowsDamageDealtText()
        {
            GameObject root = new GameObject("HudPresenterTest");
            try
            {
                CombatHudPresenter presenter = root.AddComponent<CombatHudPresenter>();
                Text damageDealtText = CreateText(root, "DamageDealtText");

                presenter.Configure(null, null, null, null, null, damageDealtText, null);

                Assert.That(damageDealtText.gameObject.activeSelf, Is.False);

                presenter.ShowDamageDealt(17);

                Assert.That(damageDealtText.gameObject.activeSelf, Is.True);
                Assert.That(damageDealtText.text, Is.EqualTo("Damage dealt: 17"));

                presenter.HideDamageDealt();

                Assert.That(damageDealtText.gameObject.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ApplyHeroDamageUsesFinalDamageForDamageDealtText()
        {
            GameObject root = new GameObject("CombatDamageFlowTest");
            try
            {
                CombatFlowController flowController = root.AddComponent<CombatFlowController>();
                CombatHudPresenter hudPresenter = CreateHudPresenter(root, out Text damageDealtText);
                EnemyRuntimeUnit enemy = new EnemyRuntimeUnit(CreateMob("slime"), FormationSlot.FrontCenter);

                SetPrivateField(flowController, "hudPresenter", hudPresenter);
                SetPrivateField(flowController, "latestDamage", new DamageCalculationResult
                {
                    DiceSum = 9,
                    FinalDamage = 17,
                    Multiplier = 1.8f,
                    FlatBonus = 1,
                    Combination = CombinationFamily.Straight,
                });

                InvokePrivateMethod(flowController, "ApplyHeroDamage", enemy);

                Assert.That(enemy.CurrentHp, Is.EqualTo(0));
                Assert.That(damageDealtText.gameObject.activeSelf, Is.True);
                Assert.That(damageDealtText.text, Is.EqualTo("Damage dealt: 17"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
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

        private static PrototypeContentSet CreateContentSet()
        {
            PrototypeContentSet contentSet = ScriptableObject.CreateInstance<PrototypeContentSet>();
            contentSet.runConfig = ScriptableObject.CreateInstance<RunConfig>();
            contentSet.runConfig.diceHighlightDuration = 0f;
            contentSet.runConfig.showBonusWhenZero = true;
            return contentSet;
        }

        private static RunSession CreateRunSession()
        {
            HeroConfig hero = ScriptableObject.CreateInstance<HeroConfig>();
            hero.maxHp = 20;
            hero.startingUnlockedDice = 5;

            RunConfig run = ScriptableObject.CreateInstance<RunConfig>();
            run.diceSlotsTotal = 5;

            return new RunSession(hero, run);
        }

        private static DiceController CreateDiceControllerForReroll()
        {
            DiceController controller = new DiceController(5, new StubRandomService(1, 2, 3, 4, 5, 6, 6));
            controller.BeginTurn(5, 3);
            controller.RollAllUnlocked();
            controller.SetReadyForUnlocked();
            return controller;
        }

        private static CombatHudPresenter CreateHudPresenter(GameObject root)
        {
            return CreateHudPresenter(root, out _);
        }

        private static CombatHudPresenter CreateHudPresenter(GameObject root, out Text damageDealtText)
        {
            GameObject hudObject = new GameObject("Hud");
            hudObject.transform.SetParent(root.transform);
            CombatHudPresenter presenter = hudObject.AddComponent<CombatHudPresenter>();

            GameObject rerollObject = new GameObject("RerollText");
            rerollObject.transform.SetParent(hudObject.transform);
            Text rerollText = rerollObject.AddComponent<Text>();
            damageDealtText = CreateText(hudObject, "DamageDealtText");
            presenter.Configure(null, null, null, rerollText, null, damageDealtText, null);
            return presenter;
        }

        private static Text CreateText(GameObject parent, string name)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(parent.transform);
            return textObject.AddComponent<Text>();
        }

        private static DiePresenter[] CreateDiePresenters(GameObject root, int count)
        {
            DiePresenter[] presenters = new DiePresenter[count];
            for (int index = 0; index < count; index++)
            {
                GameObject dieObject = new GameObject($"Die_{index}");
                dieObject.transform.SetParent(root.transform);
                presenters[index] = dieObject.AddComponent<DiePresenter>();
            }

            return presenters;
        }

        private static T GetPrivateField<T>(object instance, string name)
        {
            FieldInfo field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field {name}");
            return (T)field.GetValue(instance);
        }

        private static void SetPrivateField(object instance, string name, object value)
        {
            FieldInfo field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field {name}");
            field.SetValue(instance, value);
        }

        private static void InvokePrivateMethod(object instance, string name, params object[] args)
        {
            MethodInfo method = instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Missing method {name}");
            method.Invoke(instance, args);
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
