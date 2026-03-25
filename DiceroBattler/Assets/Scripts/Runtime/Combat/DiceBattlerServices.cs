using System;
using System.Collections.Generic;
using DiceBattler.Configs;
using UnityEngine;

namespace DiceBattler.Runtime
{
    public interface IRandomService
    {
        int RangeInclusive(int minInclusive, int maxInclusive);
        int RangeExclusive(int minInclusive, int maxExclusive);
    }

    public sealed class UnityRandomService : IRandomService
    {
        public int RangeInclusive(int minInclusive, int maxInclusive)
        {
            return UnityEngine.Random.Range(minInclusive, maxInclusive + 1);
        }

        public int RangeExclusive(int minInclusive, int maxExclusive)
        {
            return UnityEngine.Random.Range(minInclusive, maxExclusive);
        }
    }

    public sealed class DiceController
    {
        private readonly List<DieRuntimeModel> dice;
        private readonly IRandomService randomService;

        public DiceController(int totalSlots, IRandomService randomService)
        {
            this.randomService = randomService;
            dice = new List<DieRuntimeModel>(totalSlots);

            for (int index = 0; index < totalSlots; index++)
            {
                dice.Add(new DieRuntimeModel
                {
                    Index = index,
                    IsUnlocked = false,
                    State = DieVisualState.Locked,
                    ResolvedValue = 0,
                });
            }
        }

        public IReadOnlyList<DieRuntimeModel> Dice => dice;

        public void BeginTurn(int unlockedDiceCount, int rerollsPerTurn)
        {
            int boundedUnlockedCount = Mathf.Clamp(unlockedDiceCount, 0, dice.Count);

            for (int index = 0; index < dice.Count; index++)
            {
                DieRuntimeModel die = dice[index];
                die.IsUnlocked = index < boundedUnlockedCount;
                die.ResolvedValue = die.IsUnlocked ? die.ResolvedValue : 0;
                die.State = die.IsUnlocked ? DieVisualState.Rolling : DieVisualState.Locked;
            }
        }

        public void RollAllUnlocked()
        {
            for (int index = 0; index < dice.Count; index++)
            {
                if (dice[index].IsUnlocked)
                {
                    RollDie(index);
                }
            }
        }

        public bool TryRerollPrefix(int tappedIndex)
        {
            if (tappedIndex < 0 || tappedIndex >= dice.Count || !dice[tappedIndex].IsUnlocked)
            {
                return false;
            }

            for (int index = 0; index <= tappedIndex; index++)
            {
                if (dice[index].IsUnlocked)
                {
                    RollDie(index);
                }
            }

            return true;
        }

        public void SetShowingResultForRollingDice()
        {
            for (int index = 0; index < dice.Count; index++)
            {
                if (dice[index].State == DieVisualState.Rolling)
                {
                    dice[index].State = DieVisualState.ShowingResult;
                }
            }
        }

        public void SetReadyForRollingDice()
        {
            for (int index = 0; index < dice.Count; index++)
            {
                if (dice[index].State == DieVisualState.ShowingResult || dice[index].State == DieVisualState.Rolling)
                {
                    dice[index].State = DieVisualState.Ready;
                }
            }
        }

        public void CloseTurn()
        {
            for (int index = 0; index < dice.Count; index++)
            {
                if (dice[index].IsUnlocked)
                {
                    dice[index].State = DieVisualState.TurnClosed;
                }
            }
        }

        public List<int> GetUnlockedValues()
        {
            List<int> values = new List<int>();

            for (int index = 0; index < dice.Count; index++)
            {
                if (dice[index].IsUnlocked)
                {
                    values.Add(dice[index].ResolvedValue);
                }
            }

            return values;
        }

        private void RollDie(int index)
        {
            dice[index].State = DieVisualState.Rolling;
            dice[index].ResolvedValue = randomService.RangeInclusive(1, 6);
        }
    }

    public sealed class RerollBudgetController
    {
        public int Remaining { get; private set; }
        public int Maximum { get; private set; }

        public void BeginTurn(int rerollsPerTurn)
        {
            Maximum = Mathf.Max(0, rerollsPerTurn);
            Remaining = Maximum;
        }

        public bool TryConsume()
        {
            if (Remaining <= 0)
            {
                return false;
            }

            Remaining--;
            return true;
        }

        public void Refund()
        {
            Remaining = Mathf.Min(Remaining + 1, Maximum);
        }
    }

    public sealed class CombinationEvaluationService
    {
        public List<CombinationFamily> Evaluate(List<int> values)
        {
            List<CombinationFamily> combinations = new List<CombinationFamily>();
            if (values == null || values.Count == 0)
            {
                return combinations;
            }

            Dictionary<int, int> counts = new Dictionary<int, int>();
            for (int index = 0; index < values.Count; index++)
            {
                int value = values[index];
                counts[value] = counts.ContainsKey(value) ? counts[value] + 1 : 1;
            }

            int pairCount = 0;
            bool hasThree = false;
            bool hasFour = false;

            foreach (KeyValuePair<int, int> entry in counts)
            {
                if (entry.Value >= 2)
                {
                    pairCount++;
                }

                if (entry.Value >= 3)
                {
                    hasThree = true;
                }

                if (entry.Value >= 4)
                {
                    hasFour = true;
                }
            }

            if (pairCount >= 1 && values.Count >= 2)
            {
                combinations.Add(CombinationFamily.OnePair);
            }

            if (pairCount >= 2 && values.Count >= 4)
            {
                combinations.Add(CombinationFamily.TwoPair);
            }

            if (hasThree)
            {
                combinations.Add(CombinationFamily.ThreeOfAKind);
            }

            if (hasFour)
            {
                combinations.Add(CombinationFamily.FourOfAKind);
            }

            if (IsStraight(values))
            {
                combinations.Add(CombinationFamily.Straight);
            }

            return combinations;
        }

        private static bool IsStraight(List<int> values)
        {
            if (values.Count < 2)
            {
                return false;
            }

            List<int> sorted = new List<int>(values);
            sorted.Sort();

            for (int index = 1; index < sorted.Count; index++)
            {
                if (sorted[index] == sorted[index - 1] || sorted[index] != sorted[index - 1] + 1)
                {
                    return false;
                }
            }

            return true;
        }
    }

    public sealed class DamageCalculationService
    {
        private readonly CombinationDatabase combinationDatabase;
        private readonly CombinationEvaluationService evaluationService;

        public DamageCalculationService(CombinationDatabase combinationDatabase, CombinationEvaluationService evaluationService)
        {
            this.combinationDatabase = combinationDatabase;
            this.evaluationService = evaluationService;
        }

        public DamageCalculationResult Calculate(List<int> unlockedValues, int flatBonus)
        {
            int diceSum = 0;
            for (int index = 0; index < unlockedValues.Count; index++)
            {
                diceSum += unlockedValues[index];
            }

            List<CombinationFamily> validCombinations = evaluationService.Evaluate(unlockedValues);
            float bestMultiplier = 1f;
            CombinationFamily bestCombination = CombinationFamily.None;
            int unlockedCount = unlockedValues.Count;

            for (int index = 0; index < validCombinations.Count; index++)
            {
                float multiplier = combinationDatabase != null
                    ? combinationDatabase.GetMultiplier(validCombinations[index], unlockedCount)
                    : 1f;

                if (multiplier > bestMultiplier)
                {
                    bestMultiplier = multiplier;
                    bestCombination = validCombinations[index];
                }
            }

            int finalDamage = Mathf.Max(0, Mathf.RoundToInt(diceSum * bestMultiplier) + Mathf.Max(0, flatBonus));
            return new DamageCalculationResult
            {
                DiceSum = diceSum,
                FinalDamage = finalDamage,
                Multiplier = bestMultiplier,
                FlatBonus = Mathf.Max(0, flatBonus),
                Combination = bestCombination,
            };
        }
    }

    public sealed class TargetingService
    {
        public EnemyRuntimeUnit ResolveHeroTarget(List<EnemyRuntimeUnit> enemies)
        {
            EnemyRuntimeUnit frontRight = FindAlive(enemies, FormationSlot.FrontRight);
            if (frontRight != null)
            {
                return frontRight;
            }

            EnemyRuntimeUnit frontCenter = FindAlive(enemies, FormationSlot.FrontCenter);
            if (frontCenter != null)
            {
                return frontCenter;
            }

            EnemyRuntimeUnit frontLeft = FindAlive(enemies, FormationSlot.FrontLeft);
            if (frontLeft != null)
            {
                return frontLeft;
            }

            EnemyRuntimeUnit backRight = FindAlive(enemies, FormationSlot.BackRight);
            if (backRight != null)
            {
                return backRight;
            }

            return FindAlive(enemies, FormationSlot.BackLeft);
        }

        private static EnemyRuntimeUnit FindAlive(List<EnemyRuntimeUnit> enemies, FormationSlot slot)
        {
            for (int index = 0; index < enemies.Count; index++)
            {
                if (enemies[index].FormationSlot == slot && enemies[index].IsAlive)
                {
                    return enemies[index];
                }
            }

            return null;
        }
    }

    public sealed class EnemyIntentService
    {
        private readonly IRandomService randomService;

        public EnemyIntentService(IRandomService randomService)
        {
            this.randomService = randomService;
        }

        public void RollIntent(EnemyRuntimeUnit enemy)
        {
            if (enemy == null || enemy.Config == null)
            {
                return;
            }

            enemy.CurrentIntentDamage = randomService.RangeInclusive(enemy.Config.damageMin, enemy.Config.damageMax);
        }
    }

    public sealed class ExpController
    {
        private readonly ProgressionDatabase progressionDatabase;

        public ExpController(ProgressionDatabase progressionDatabase)
        {
            this.progressionDatabase = progressionDatabase;
        }

        public ExpGrantResult Grant(RunSession session, int amount, bool allowLevelUp)
        {
            int threshold = progressionDatabase != null ? progressionDatabase.GetThresholdForLevel(session.CurrentLevel) : 0;
            if (threshold <= 0)
            {
                return new ExpGrantResult { AppliedAmount = 0, TriggeredLevelUp = false, Threshold = 0 };
            }

            int remaining = Mathf.Max(0, threshold - session.CurrentLevelExp);
            int applied = Mathf.Clamp(amount, 0, remaining);
            session.CurrentLevelExp += applied;

            bool leveledUp = allowLevelUp && !session.PendingLevelUp && session.CurrentLevelExp >= threshold;
            if (leveledUp)
            {
                session.PendingLevelUp = true;
                session.CurrentLevelExp = threshold;
            }

            return new ExpGrantResult
            {
                AppliedAmount = applied,
                TriggeredLevelUp = leveledUp,
                Threshold = threshold,
            };
        }

        public void ConsumePendingLevelUp(RunSession session)
        {
            if (!session.PendingLevelUp)
            {
                return;
            }

            session.PendingLevelUp = false;
            session.CurrentLevel += 1;
            session.CurrentLevelExp = 0;
        }
    }

    public sealed class UpgradeSelectionService
    {
        private readonly RunConfig runConfig;
        private readonly IRandomService randomService;

        public UpgradeSelectionService(RunConfig runConfig, IRandomService randomService)
        {
            this.runConfig = runConfig;
            this.randomService = randomService;
        }

        public UpgradeOfferSet GenerateOffers(RunSession session, UpgradeDatabase upgradeDatabase)
        {
            List<UpgradeConfig> eligible = new List<UpgradeConfig>();
            for (int index = 0; index < upgradeDatabase.upgrades.Count; index++)
            {
                UpgradeConfig upgrade = upgradeDatabase.upgrades[index];
                if (upgrade != null && IsEligible(session, upgrade))
                {
                    eligible.Add(upgrade);
                }
            }

            List<UpgradeConfig> picks = new List<UpgradeConfig>(3);
            if (eligible.Count == 0)
            {
                return new UpgradeOfferSet(picks);
            }

            List<UpgradeConfig> bag = new List<UpgradeConfig>(eligible);
            while (picks.Count < 3)
            {
                if (bag.Count == 0)
                {
                    bag.AddRange(eligible);
                }

                int pickIndex = randomService.RangeExclusive(0, bag.Count);
                picks.Add(bag[pickIndex]);
                bag.RemoveAt(pickIndex);
            }

            return new UpgradeOfferSet(picks);
        }

        private bool IsEligible(RunSession session, UpgradeConfig upgrade)
        {
            if (!upgrade.enabled || session.CurrentLevel < upgrade.eligibleFromLevel)
            {
                return false;
            }

            if (upgrade.type == UpgradeType.UnlockDie)
            {
                int nextUnlockedCount = session.UnlockedDiceCount + 1;
                return upgrade.targetDiceCount == nextUnlockedCount
                       && upgrade.targetDiceCount <= runConfig.diceSlotsTotal;
            }

            return upgrade.value > 0f;
        }
    }

    public sealed class UpgradeApplyService
    {
        private readonly RunConfig runConfig;

        public UpgradeApplyService(RunConfig runConfig)
        {
            this.runConfig = runConfig;
        }

        public void Apply(RunSession session, UpgradeConfig upgrade)
        {
            if (upgrade == null)
            {
                return;
            }

            switch (upgrade.type)
            {
                case UpgradeType.UnlockDie:
                    session.UnlockedDiceCount = Mathf.Clamp(upgrade.targetDiceCount, session.UnlockedDiceCount, runConfig.diceSlotsTotal);
                    break;
                case UpgradeType.HealPercent:
                    int healAmount = Mathf.RoundToInt(session.Hero.MaxHp * (upgrade.value / 100f));
                    session.Hero.Heal(healAmount);
                    break;
                case UpgradeType.FlatDamageBonus:
                    session.FlatDamageBonus += Mathf.RoundToInt(upgrade.value);
                    break;
            }
        }
    }
}
