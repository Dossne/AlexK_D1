using System.Collections;
using System.Collections.Generic;
using DiceBattler.Configs;
using DiceBattler.Presentation;
using UnityEngine;

namespace DiceBattler.Runtime
{
    public sealed class CombatFlowController : MonoBehaviour
    {
        [SerializeField] private PrototypeContentSet contentSet;
        [SerializeField] private CombatHudPresenter hudPresenter;
        [SerializeField] private HeroPresenter heroPresenter;
        [SerializeField] private EnemyPresenter[] enemyPresenters;
        [SerializeField] private DiePresenter[] diePresenters;
        [SerializeField] private OverlayController overlayController;

        private readonly List<EnemyRuntimeUnit> activeEnemies = new List<EnemyRuntimeUnit>();
        private RunSession runSession;
        private CombatFlowPhase currentPhase;
        private IRandomService randomService;
        private DiceController diceController;
        private RerollBudgetController rerollBudget;
        private DamageCalculationService damageCalculationService;
        private TargetingService targetingService;
        private EnemyIntentService enemyIntentService;
        private ExpController expController;
        private UpgradeSelectionService upgradeSelectionService;
        private UpgradeApplyService upgradeApplyService;
        private Coroutine flowRoutine;
        private DamageCalculationResult latestDamage;

        public void Initialize(
            PrototypeContentSet prototypeContentSet,
            CombatHudPresenter combatHudPresenter,
            HeroPresenter combatHeroPresenter,
            EnemyPresenter[] combatEnemyPresenters,
            DiePresenter[] combatDiePresenters,
            OverlayController combatOverlayController)
        {
            contentSet = prototypeContentSet;
            hudPresenter = combatHudPresenter;
            heroPresenter = combatHeroPresenter;
            enemyPresenters = combatEnemyPresenters;
            diePresenters = combatDiePresenters;
            overlayController = combatOverlayController;
        }

        private void Start()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            randomService = new UnityRandomService();
            diceController = new DiceController(contentSet.runConfig.diceSlotsTotal, randomService);
            rerollBudget = new RerollBudgetController();
            damageCalculationService = new DamageCalculationService(contentSet.combinationDatabase, new CombinationEvaluationService());
            targetingService = new TargetingService();
            enemyIntentService = new EnemyIntentService(randomService);
            expController = new ExpController(contentSet.progressionDatabase);
            upgradeSelectionService = new UpgradeSelectionService(contentSet.runConfig, randomService);
            upgradeApplyService = new UpgradeApplyService(contentSet.runConfig);

            for (int index = 0; index < diePresenters.Length; index++)
            {
                int capturedIndex = index;
                diePresenters[index].Bind(capturedIndex, HandleDiePressed);
            }

            hudPresenter.BindAttack(HandleAttackPressed);
            overlayController.HideAll();
            StartRun();
        }

        public void StartRun()
        {
            if (flowRoutine != null)
            {
                StopCoroutine(flowRoutine);
            }

            runSession = new RunSession(contentSet.heroConfig, contentSet.runConfig);
            heroPresenter.SetHero(runSession.Hero);
            hudPresenter.HideDamageDealt();
            hudPresenter.SetWave(runSession.CurrentWaveNumber, contentSet.runConfig.totalWaves);
            hudPresenter.SetLevel(runSession.CurrentLevel);
            RefreshExpHud();
            flowRoutine = StartCoroutine(RunFlow());
        }

        private IEnumerator RunFlow()
        {
            while (runSession.Hero.IsAlive)
            {
                yield return RunEncounterTransition();
                SpawnWave();

                if (activeEnemies.Count == 0)
                {
                    EnterDefeat("Wave configuration is invalid.");
                    yield break;
                }

                yield return PlayerTurnLoop();
                if (currentPhase == CombatFlowPhase.VictoryOverlay || currentPhase == CombatFlowPhase.DefeatOverlay)
                {
                    yield break;
                }
            }
        }

        private IEnumerator RunEncounterTransition()
        {
            currentPhase = CombatFlowPhase.EncounterTransition;
            heroPresenter.PlayRun();
            yield return new WaitForSeconds(contentSet.runConfig.postWaveRunTransitionDuration);
            heroPresenter.PlayIdle();
        }

        private void SpawnWave()
        {
            currentPhase = CombatFlowPhase.EncounterSpawn;
            activeEnemies.Clear();

            for (int index = 0; index < enemyPresenters.Length; index++)
            {
                enemyPresenters[index].SetVisible(false);
            }

            WaveConfig wave = contentSet.waveDatabase.GetWave(runSession.CurrentWaveNumber);
            if (wave == null)
            {
                return;
            }

            List<FormationSlot> slots = ResolveFormationSlots(wave.mobIds.Count);
            for (int index = 0; index < wave.mobIds.Count && index < enemyPresenters.Length; index++)
            {
                MobConfig mob = contentSet.mobDatabase.GetById(wave.mobIds[index]);
                if (mob == null)
                {
                    continue;
                }

                EnemyRuntimeUnit runtimeEnemy = new EnemyRuntimeUnit(mob, slots[index]);
                enemyIntentService.RollIntent(runtimeEnemy);
                activeEnemies.Add(runtimeEnemy);
                enemyPresenters[index].SetEnemy(runtimeEnemy);
                enemyPresenters[index].SetVisible(true);
            }

            SyncEnemyPresenters();
            hudPresenter.SetWave(runSession.CurrentWaveNumber, contentSet.runConfig.totalWaves);
        }

        private IEnumerator PlayerTurnLoop()
        {
            while (true)
            {
                yield return RunPlayerRoll();

                currentPhase = CombatFlowPhase.PlayerDecision;
                hudPresenter.SetAttackInteractable(true);
                UpdateDicePresentation();

                while (currentPhase == CombatFlowPhase.PlayerDecision || currentPhase == CombatFlowPhase.PlayerRoll)
                {
                    yield return null;
                }

                if (currentPhase == CombatFlowPhase.VictoryOverlay || currentPhase == CombatFlowPhase.DefeatOverlay)
                {
                    yield break;
                }

                if (AllEnemiesDefeated())
                {
                    yield return ResolveWaveEnd();
                    yield break;
                }
            }
        }

        private IEnumerator RunPlayerRoll()
        {
            currentPhase = CombatFlowPhase.PlayerRoll;
            hudPresenter.HideDamageDealt();
            rerollBudget.BeginTurn(contentSet.runConfig.rerollsPerTurnDefault);
            runSession.RerollsRemaining = rerollBudget.Remaining;
            diceController.BeginTurn(runSession.UnlockedDiceCount, rerollBudget.Remaining);
            diceController.RollAllUnlocked();
            diceController.SetShowingResultForUnlocked();
            UpdateDicePresentation();
            yield return new WaitForSeconds(contentSet.runConfig.diceHighlightDuration);
            diceController.SetReadyForUnlocked();
            latestDamage = damageCalculationService.Calculate(diceController.GetUnlockedValues(), runSession.FlatDamageBonus);
            UpdateHudValues();
            UpdateDicePresentation();
        }

        private void HandleDiePressed(int index)
        {
            if (!TryBeginReroll(index))
            {
                return;
            }
            StartCoroutine(ResolveReroll());
        }

        private bool TryBeginReroll(int index)
        {
            if (currentPhase != CombatFlowPhase.PlayerDecision || rerollBudget.Remaining <= 0)
            {
                return false;
            }

            if (!rerollBudget.TryConsume())
            {
                return false;
            }

            if (!diceController.TryRerollPrefix(index))
            {
                rerollBudget.Refund();
                runSession.RerollsRemaining = rerollBudget.Remaining;
                return false;
            }

            runSession.RerollsRemaining = rerollBudget.Remaining;
            currentPhase = CombatFlowPhase.PlayerRoll;
            UpdateHudValues();
            UpdateDicePresentation();
            return true;
        }

        private IEnumerator ResolveReroll()
        {
            diceController.SetShowingResultForUnlocked();
            UpdateDicePresentation();
            yield return new WaitForSeconds(contentSet.runConfig.diceHighlightDuration);
            diceController.SetReadyForUnlocked();
            latestDamage = damageCalculationService.Calculate(diceController.GetUnlockedValues(), runSession.FlatDamageBonus);
            UpdateHudValues();
            currentPhase = CombatFlowPhase.PlayerDecision;
            UpdateDicePresentation();
        }

        private void HandleAttackPressed()
        {
            if (currentPhase != CombatFlowPhase.PlayerDecision)
            {
                return;
            }

            if (flowRoutine != null)
            {
                StartCoroutine(ResolveHeroAttackThenEnemyTurn());
            }
        }

        private IEnumerator ResolveHeroAttackThenEnemyTurn()
        {
            currentPhase = CombatFlowPhase.HeroAttack;
            hudPresenter.SetAttackInteractable(false);
            diceController.CloseTurn();
            UpdateDicePresentation();

            EnemyRuntimeUnit target = targetingService.ResolveHeroTarget(activeEnemies);
            if (target == null)
            {
                hudPresenter.HideDamageDealt();
                yield return ResolveWaveEnd();
                yield break;
            }

            yield return heroPresenter.PlayAttack();
            ApplyHeroDamage(target);
            EnemyPresenter targetPresenter = FindPresenter(target);
            if (targetPresenter != null)
            {
                targetPresenter.PlayHitOrDeath(target.IsAlive, latestDamage.FinalDamage);
            }

            yield return ShowDamageDealtForConfiguredDuration();

            if (!target.IsAlive)
            {
                expController.Grant(runSession, target.Config.expReward, !IsFinalWave());
                RefreshExpHud();
            }

            SyncEnemyPresenters();
            if (AllEnemiesDefeated())
            {
                yield return ResolveWaveEnd();
                yield break;
            }

            currentPhase = CombatFlowPhase.EnemyAttack;
            for (int index = 0; index < activeEnemies.Count; index++)
            {
                EnemyRuntimeUnit enemy = activeEnemies[index];
                if (!enemy.IsAlive)
                {
                    continue;
                }

                EnemyPresenter presenter = FindPresenter(enemy);
                if (presenter != null)
                {
                    yield return presenter.PlayAttack();
                }

                runSession.Hero.ApplyDamage(enemy.CurrentIntentDamage);
                heroPresenter.PlayHitOrDeath(runSession.Hero.IsAlive, enemy.CurrentIntentDamage);
                heroPresenter.SetHero(runSession.Hero);

                if (!runSession.Hero.IsAlive)
                {
                    hudPresenter.HideDamageDealt();
                    yield return heroPresenter.PlayDeath();
                    EnterDefeat(null);
                    yield break;
                }
            }

            for (int index = 0; index < activeEnemies.Count; index++)
            {
                if (activeEnemies[index].IsAlive)
                {
                    enemyIntentService.RollIntent(activeEnemies[index]);
                }
            }

            SyncEnemyPresenters();
        }

        private IEnumerator ResolveWaveEnd()
        {
            currentPhase = CombatFlowPhase.PostWaveResolve;
            hudPresenter.HideDamageDealt();
            WaveConfig wave = contentSet.waveDatabase.GetWave(runSession.CurrentWaveNumber);
            if (wave != null)
            {
                expController.Grant(runSession, wave.expReward, !IsFinalWave());
                RefreshExpHud();
            }

            if (IsFinalWave())
            {
                EnterVictory();
                yield break;
            }

            if (runSession.PendingLevelUp)
            {
                currentPhase = CombatFlowPhase.LevelUpOverlay;
                UpgradeOfferSet offers = upgradeSelectionService.GenerateOffers(runSession, contentSet.upgradeDatabase);
                bool waiting = true;
                overlayController.ShowLevelUp(offers.Options, selectedUpgrade =>
                {
                    upgradeApplyService.Apply(runSession, selectedUpgrade);
                    expController.ConsumePendingLevelUp(runSession);
                    hudPresenter.SetLevel(runSession.CurrentLevel);
                    heroPresenter.SetHero(runSession.Hero);
                    waiting = false;
                });

                while (waiting)
                {
                    yield return null;
                }

                overlayController.HideLevelUp();
                RefreshExpHud();
            }

            runSession.CurrentWaveNumber += 1;
        }

        private void EnterVictory()
        {
            currentPhase = CombatFlowPhase.VictoryOverlay;
            hudPresenter.HideDamageDealt();
            overlayController.ShowVictory(StartRun);
        }

        private void EnterDefeat(string errorMessage)
        {
            currentPhase = CombatFlowPhase.DefeatOverlay;
            hudPresenter.HideDamageDealt();
            if (!string.IsNullOrEmpty(errorMessage))
            {
                Debug.LogError(errorMessage);
            }

            overlayController.ShowDefeat(StartRun);
        }

        private void UpdateHudValues()
        {
            hudPresenter.SetRerolls(rerollBudget.Remaining, rerollBudget.Maximum);
            hudPresenter.SetDamagePreview(latestDamage.ToDisplayString(contentSet.runConfig.showBonusWhenZero));
        }

        private void UpdateDicePresentation()
        {
            IReadOnlyList<DieRuntimeModel> dice = diceController.Dice;
            for (int index = 0; index < diePresenters.Length && index < dice.Count; index++)
            {
                diePresenters[index].SetState(dice[index], currentPhase == CombatFlowPhase.PlayerDecision && rerollBudget.Remaining > 0);
            }
        }

        private void SyncEnemyPresenters()
        {
            for (int index = 0; index < enemyPresenters.Length; index++)
            {
                if (index < activeEnemies.Count)
                {
                    enemyPresenters[index].SetEnemy(activeEnemies[index]);
                    enemyPresenters[index].SetVisible(activeEnemies[index].IsAlive);
                }
                else
                {
                    enemyPresenters[index].SetVisible(false);
                }
            }
        }

        private void RefreshExpHud()
        {
            int threshold = contentSet.progressionDatabase.GetThresholdForLevel(runSession.CurrentLevel);
            hudPresenter.SetExp(runSession.CurrentLevelExp, threshold);
            hudPresenter.SetLevel(runSession.CurrentLevel);
        }

        private bool AllEnemiesDefeated()
        {
            for (int index = 0; index < activeEnemies.Count; index++)
            {
                if (activeEnemies[index].IsAlive)
                {
                    return false;
                }
            }

            return activeEnemies.Count > 0;
        }

        private bool IsFinalWave()
        {
            return runSession.CurrentWaveNumber >= contentSet.runConfig.totalWaves;
        }

        private EnemyPresenter FindPresenter(EnemyRuntimeUnit enemy)
        {
            for (int index = 0; index < enemyPresenters.Length; index++)
            {
                if (enemyPresenters[index].CurrentEnemy == enemy)
                {
                    return enemyPresenters[index];
                }
            }

            return null;
        }

        private void ApplyHeroDamage(EnemyRuntimeUnit target)
        {
            target.ApplyDamage(latestDamage.FinalDamage);
            hudPresenter.ShowDamageDealt(latestDamage.FinalDamage);
        }

        private IEnumerator ShowDamageDealtForConfiguredDuration()
        {
            float duration = Mathf.Max(0f, contentSet.runConfig.damagePanelDisplayDuration);
            if (duration > 0f)
            {
                yield return new WaitForSeconds(duration);
            }

            hudPresenter.HideDamageDealt();
        }

        private static List<FormationSlot> ResolveFormationSlots(int enemyCount)
        {
            List<FormationSlot> result = new List<FormationSlot>(3);
            switch (enemyCount)
            {
                case 1:
                    result.Add(FormationSlot.FrontCenter);
                    break;
                case 2:
                    result.Add(FormationSlot.FrontLeft);
                    result.Add(FormationSlot.FrontRight);
                    break;
                default:
                    result.Add(FormationSlot.FrontCenter);
                    result.Add(FormationSlot.BackLeft);
                    result.Add(FormationSlot.BackRight);
                    break;
            }

            return result;
        }

        private bool ValidateReferences()
        {
            return contentSet != null
                   && contentSet.heroConfig != null
                   && contentSet.mobDatabase != null
                   && contentSet.waveDatabase != null
                   && contentSet.combinationDatabase != null
                   && contentSet.upgradeDatabase != null
                   && contentSet.progressionDatabase != null
                   && contentSet.runConfig != null
                   && hudPresenter != null
                   && heroPresenter != null
                   && overlayController != null
                   && enemyPresenters != null
                   && enemyPresenters.Length > 0
                   && diePresenters != null
                   && diePresenters.Length == contentSet.runConfig.diceSlotsTotal;
        }
    }
}
