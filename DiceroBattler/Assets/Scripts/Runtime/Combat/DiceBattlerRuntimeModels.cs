using System;
using System.Collections.Generic;
using DiceBattler.Configs;
using UnityEngine;

namespace DiceBattler.Runtime
{
    public enum DieVisualState
    {
        Locked = 0,
        Rolling = 1,
        ShowingResult = 2,
        Ready = 3,
        TurnClosed = 4,
    }

    public enum CombatFlowPhase
    {
        Idle = 0,
        EncounterTransition = 1,
        EncounterSpawn = 2,
        PlayerRoll = 3,
        PlayerDecision = 4,
        HeroAttack = 5,
        EnemyAttack = 6,
        PostWaveResolve = 7,
        LevelUpOverlay = 8,
        VictoryOverlay = 9,
        DefeatOverlay = 10,
    }

    public enum FormationSlot
    {
        FrontLeft = 0,
        FrontCenter = 1,
        FrontRight = 2,
        BackLeft = 3,
        BackRight = 4,
    }

    [Serializable]
    public sealed class DieRuntimeModel
    {
        public int Index;
        public bool IsUnlocked;
        public DieVisualState State;
        public int ResolvedValue;
    }

    public sealed class HeroRuntimeUnit
    {
        public HeroRuntimeUnit(int maxHp)
        {
            MaxHp = Mathf.Max(1, maxHp);
            CurrentHp = MaxHp;
        }

        public int CurrentHp { get; private set; }
        public int MaxHp { get; private set; }
        public bool IsAlive => CurrentHp > 0;

        public void ApplyDamage(int amount)
        {
            CurrentHp = Mathf.Max(0, CurrentHp - Mathf.Max(0, amount));
        }

        public void Heal(int amount)
        {
            CurrentHp = Mathf.Clamp(CurrentHp + Mathf.Max(0, amount), 0, MaxHp);
        }
    }

    public sealed class EnemyRuntimeUnit
    {
        public EnemyRuntimeUnit(MobConfig config, FormationSlot formationSlot)
        {
            Config = config;
            FormationSlot = formationSlot;
            CurrentHp = Mathf.Max(1, config != null ? config.hp : 1);
        }

        public MobConfig Config { get; }
        public FormationSlot FormationSlot { get; }
        public int CurrentHp { get; private set; }
        public int CurrentIntentDamage { get; set; }
        public bool IsAlive => CurrentHp > 0;

        public void ApplyDamage(int amount)
        {
            CurrentHp = Mathf.Max(0, CurrentHp - Mathf.Max(0, amount));
        }
    }

    public sealed class RunSession
    {
        public RunSession(HeroConfig heroConfig, RunConfig runConfig)
        {
            Hero = new HeroRuntimeUnit(heroConfig.maxHp);
            CurrentWaveNumber = 1;
            CurrentLevel = 1;
            CurrentLevelExp = 0;
            UnlockedDiceCount = Mathf.Clamp(heroConfig.startingUnlockedDice, 1, runConfig.diceSlotsTotal);
            FlatDamageBonus = Mathf.Max(0, heroConfig.startingFlatDamageBonus);
            RerollsRemaining = Mathf.Max(0, heroConfig.startingRerollsPerTurn);
        }

        public HeroRuntimeUnit Hero { get; }
        public int CurrentWaveNumber { get; set; }
        public int CurrentLevel { get; set; }
        public int CurrentLevelExp { get; set; }
        public int UnlockedDiceCount { get; set; }
        public int FlatDamageBonus { get; set; }
        public int RerollsRemaining { get; set; }
        public bool PendingLevelUp { get; set; }
    }

    public struct DamageCalculationResult
    {
        public int DiceSum;
        public int FinalDamage;
        public float Multiplier;
        public int FlatBonus;
        public CombinationFamily Combination;

        public string ToDisplayString(bool showZeroBonus)
        {
            string combinationName = Combination == CombinationFamily.None ? "No Combination" : Combination.ToString();
            string bonusSection = showZeroBonus || FlatBonus != 0 ? $" + Bonus {FlatBonus}" : string.Empty;
            return $"Damage value: {FinalDamage} {combinationName} x{Multiplier:0.##}{bonusSection}";
        }
    }

    public struct ExpGrantResult
    {
        public int AppliedAmount;
        public bool TriggeredLevelUp;
        public int Threshold;
    }

    public struct UpgradeOfferSet
    {
        public UpgradeOfferSet(List<UpgradeConfig> options)
        {
            Options = options;
        }

        public List<UpgradeConfig> Options { get; }
    }

    public struct ValidationIssue
    {
        public ValidationIssue(ValidationSeverity severity, string message)
        {
            Severity = severity;
            Message = message;
        }

        public ValidationSeverity Severity { get; }
        public string Message { get; }
    }

    public enum ValidationSeverity
    {
        Info = 0,
        Warning = 1,
        Fatal = 2,
    }
}
