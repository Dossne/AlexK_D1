using System;
using System.Collections;
using System.Collections.Generic;
using DiceBattler.Configs;
using DiceBattler.Runtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DiceBattler.Presentation
{
    public sealed class CombatHudPresenter : MonoBehaviour
    {
        [SerializeField] private Slider expBar;
        [SerializeField] private Text levelText;
        [SerializeField] private Text waveText;
        [SerializeField] private Text rerollText;
        [SerializeField] private Text damagePreviewText;
        [SerializeField] private Text damageDealtText;
        [SerializeField] private Text damageTakenText;
        [SerializeField] private Button attackButton;

        public void Configure(
            Slider expBarSlider,
            Text levelLabel,
            Text waveLabel,
            Text rerollLabel,
            Text damagePreviewLabel,
            Text damageDealtLabel,
            Text damageTakenLabel,
            Button attackCta)
        {
            expBar = expBarSlider;
            levelText = levelLabel;
            waveText = waveLabel;
            rerollText = rerollLabel;
            damagePreviewText = damagePreviewLabel;
            damageDealtText = damageDealtLabel;
            damageTakenText = damageTakenLabel;
            attackButton = attackCta;
            HideDamageDealt();
            HideDamageTaken();
        }

        public void BindAttack(Action onAttackPressed)
        {
            if (attackButton == null)
            {
                return;
            }

            attackButton.onClick.RemoveAllListeners();
            attackButton.onClick.AddListener(() => onAttackPressed?.Invoke());
        }

        public void SetExp(int current, int threshold)
        {
            if (expBar == null)
            {
                return;
            }

            expBar.maxValue = Mathf.Max(1, threshold);
            expBar.value = Mathf.Clamp(current, 0, expBar.maxValue);
        }

        public void SetLevel(int level)
        {
            if (levelText != null)
            {
                levelText.text = $"Level {level}";
            }
        }

        public void SetWave(int currentWave, int totalWaves)
        {
            if (waveText != null)
            {
                waveText.text = $"Waves {currentWave}/{totalWaves}";
            }
        }

        public void SetRerolls(int remaining, int maximum)
        {
            if (rerollText != null)
            {
                rerollText.text = $"{remaining}/{maximum}";
            }
        }

        public void SetDamagePreview(string preview)
        {
            if (damagePreviewText != null)
            {
                damagePreviewText.text = preview;
            }
        }

        public void ShowDamageDealt(int amount)
        {
            if (damageDealtText != null)
            {
                damageDealtText.text = $"Damage dealt: {amount}";
                damageDealtText.gameObject.SetActive(true);
            }
        }

        public void HideDamageDealt()
        {
            if (damageDealtText != null)
            {
                damageDealtText.gameObject.SetActive(false);
            }
        }

        public void ShowDamageTaken(int amount)
        {
            if (damageTakenText != null)
            {
                damageTakenText.text = $"Damage dealt: {amount}";
                damageTakenText.gameObject.SetActive(true);
            }
        }

        public void HideDamageTaken()
        {
            if (damageTakenText != null)
            {
                damageTakenText.gameObject.SetActive(false);
            }
        }

        public void SetAttackInteractable(bool isInteractable)
        {
            if (attackButton != null)
            {
                attackButton.interactable = isInteractable;
            }
        }
    }

    public sealed class HeroPresenter : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private Slider hpBar;
        [SerializeField] private Transform hpBarAnchor;
        [SerializeField] private Transform damageAnchor;
        [SerializeField] private Transform hitFxAnchor;
        [SerializeField] private float attackDuration = 0.45f;
        [SerializeField] private float hitDuration = 0.25f;
        [SerializeField] private float deathDuration = 0.8f;

        public void Configure(Animator heroAnimator, Slider heroHpBar, Transform hpAnchor, Transform damagePoint, Transform hitFxPoint)
        {
            animator = heroAnimator;
            hpBar = heroHpBar;
            hpBarAnchor = hpAnchor;
            damageAnchor = damagePoint;
            hitFxAnchor = hitFxPoint;
        }

        public Transform HpBarAnchor => hpBarAnchor;
        public Transform DamageAnchor => damageAnchor;
        public Transform HitFxAnchor => hitFxAnchor;

        public void SetHero(HeroRuntimeUnit hero)
        {
            if (hpBar != null && hero != null)
            {
                hpBar.maxValue = hero.MaxHp;
                hpBar.value = hero.CurrentHp;
            }
        }

        public void PlayRun()
        {
            CrossFade("Run");
        }

        public void PlayIdle()
        {
            CrossFade("Idle");
        }

        public IEnumerator PlayAttack()
        {
            CrossFade("Attack");
            yield return new WaitForSeconds(attackDuration);
            CrossFade("Idle");
        }

        public void PlayHitOrDeath(bool survived, int damageAmount)
        {
            if (survived)
            {
                CrossFade("Hit");
                StartCoroutine(ReturnToIdle(hitDuration));
                return;
            }

            CrossFade("Death");
        }

        public IEnumerator PlayDeath()
        {
            CrossFade("Death");
            yield return new WaitForSeconds(deathDuration);
        }

        private IEnumerator ReturnToIdle(float duration)
        {
            yield return new WaitForSeconds(duration);
            CrossFade("Idle");
        }

        private void CrossFade(string stateName)
        {
            if (animator != null)
            {
                animator.CrossFadeInFixedTime(stateName, 0.05f);
            }
        }
    }

    public sealed class EnemyPresenter : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private Slider hpBar;
        [SerializeField] private Text intentText;
        [SerializeField] private Text nameText;
        [SerializeField] private Transform hpBarAnchor;
        [SerializeField] private Transform intentAnchor;
        [SerializeField] private Transform damageAnchor;
        [SerializeField] private Transform hitFxAnchor;
        [SerializeField] private float attackDuration = 0.45f;
        [SerializeField] private float hitDuration = 0.2f;
        [SerializeField] private float deathDuration = 0.65f;

        public void Configure(
            Animator enemyAnimator,
            Slider enemyHpBar,
            Text enemyIntentText,
            Text enemyNameText,
            Transform hpAnchor,
            Transform intentPoint,
            Transform damagePoint,
            Transform hitFxPoint)
        {
            animator = enemyAnimator;
            hpBar = enemyHpBar;
            intentText = enemyIntentText;
            nameText = enemyNameText;
            hpBarAnchor = hpAnchor;
            intentAnchor = intentPoint;
            damageAnchor = damagePoint;
            hitFxAnchor = hitFxPoint;
        }

        public EnemyRuntimeUnit CurrentEnemy { get; private set; }
        public Transform HpBarAnchor => hpBarAnchor;
        public Transform IntentAnchor => intentAnchor;
        public Transform DamageAnchor => damageAnchor;
        public Transform HitFxAnchor => hitFxAnchor;

        public void SetEnemy(EnemyRuntimeUnit enemy)
        {
            CurrentEnemy = enemy;
            if (enemy == null)
            {
                return;
            }

            if (nameText != null)
            {
                nameText.text = enemy.Config != null ? enemy.Config.displayName : "Enemy";
            }

            if (hpBar != null)
            {
                hpBar.maxValue = enemy.Config != null ? enemy.Config.hp : 1;
                hpBar.value = enemy.CurrentHp;
            }

            if (intentText != null)
            {
                intentText.text = enemy.CurrentIntentDamage.ToString();
            }
        }

        public void SetVisible(bool isVisible)
        {
            gameObject.SetActive(isVisible);
        }

        public IEnumerator PlayAttack()
        {
            CrossFade("Attack");
            yield return new WaitForSeconds(attackDuration);
            CrossFade("Idle");
        }

        public void PlayHitOrDeath(bool survived, int damageAmount)
        {
            if (CurrentEnemy != null && hpBar != null)
            {
                hpBar.value = CurrentEnemy.CurrentHp;
            }

            if (survived)
            {
                CrossFade("Hit");
                StartCoroutine(ReturnToIdle(hitDuration));
                return;
            }

            StartCoroutine(PlayDeathRoutine());
        }

        private IEnumerator PlayDeathRoutine()
        {
            CrossFade("Death");
            yield return new WaitForSeconds(deathDuration);
            SetVisible(false);
        }

        private IEnumerator ReturnToIdle(float duration)
        {
            yield return new WaitForSeconds(duration);
            CrossFade("Idle");
        }

        private void CrossFade(string stateName)
        {
            if (animator != null)
            {
                animator.CrossFadeInFixedTime(stateName, 0.05f);
            }
        }
    }

    public sealed class DiePresenter : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Text valueText;
        [SerializeField] private Graphic tapTarget;
        [SerializeField] private GameObject highlightVisual;
        [SerializeField] private GameObject lockVisual;
        [SerializeField] private CanvasGroup canvasGroup;

        private Action<int> onPressed;
        private int dieIndex;

        public void Configure(Text dieValueText, Graphic dieTapTarget, GameObject dieHighlightVisual, GameObject dieLockVisual, CanvasGroup dieCanvasGroup)
        {
            valueText = dieValueText;
            tapTarget = dieTapTarget;
            highlightVisual = dieHighlightVisual;
            lockVisual = dieLockVisual;
            canvasGroup = dieCanvasGroup;
        }

        public void Bind(int index, Action<int> onPressedCallback)
        {
            dieIndex = index;
            onPressed = onPressedCallback;
        }

        public void SetState(DieRuntimeModel model, bool canInteract)
        {
            dieIndex = model.Index;

            if (valueText != null)
            {
                valueText.text = model.IsUnlocked ? model.ResolvedValue.ToString() : "X";
            }

            if (lockVisual != null)
            {
                lockVisual.SetActive(model.State == DieVisualState.Locked);
            }

            if (highlightVisual != null)
            {
                highlightVisual.SetActive(model.State == DieVisualState.ShowingResult);
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = model.State == DieVisualState.TurnClosed ? 0.65f : 1f;
                canvasGroup.blocksRaycasts = canInteract && model.State == DieVisualState.Ready;
            }

            if (tapTarget != null)
            {
                tapTarget.raycastTarget = canInteract && model.State == DieVisualState.Ready;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            onPressed?.Invoke(dieIndex);
        }
    }

    [Serializable]
    public sealed class UpgradeChoiceView
    {
        public Button button;
        public Text titleText;
        public Text descriptionText;
    }

    public sealed class OverlayController : MonoBehaviour
    {
        [SerializeField] private GameObject levelUpRoot;
        [SerializeField] private UpgradeChoiceView[] levelUpChoices;
        [SerializeField] private GameObject defeatRoot;
        [SerializeField] private Button retryButton;
        [SerializeField] private GameObject victoryRoot;
        [SerializeField] private Button continueButton;

        public void Configure(
            GameObject levelUpOverlayRoot,
            UpgradeChoiceView[] choices,
            GameObject defeatOverlayRoot,
            Button defeatRetryButton,
            GameObject victoryOverlayRoot,
            Button victoryContinueButton)
        {
            levelUpRoot = levelUpOverlayRoot;
            levelUpChoices = choices;
            defeatRoot = defeatOverlayRoot;
            retryButton = defeatRetryButton;
            victoryRoot = victoryOverlayRoot;
            continueButton = victoryContinueButton;
        }

        public void HideAll()
        {
            if (levelUpRoot != null)
            {
                levelUpRoot.SetActive(false);
            }

            if (defeatRoot != null)
            {
                defeatRoot.SetActive(false);
            }

            if (victoryRoot != null)
            {
                victoryRoot.SetActive(false);
            }
        }

        public void ShowLevelUp(List<UpgradeConfig> options, Action<UpgradeConfig> onSelected)
        {
            HideAll();
            if (levelUpRoot == null)
            {
                return;
            }

            levelUpRoot.SetActive(true);
            for (int index = 0; index < levelUpChoices.Length; index++)
            {
                UpgradeChoiceView choice = levelUpChoices[index];
                bool hasOption = options != null && index < options.Count && options[index] != null;
                if (choice.button != null)
                {
                    choice.button.gameObject.SetActive(hasOption);
                }

                if (!hasOption)
                {
                    continue;
                }

                UpgradeConfig option = options[index];
                if (choice.titleText != null)
                {
                    choice.titleText.text = option.title;
                }

                if (choice.descriptionText != null)
                {
                    choice.descriptionText.text = option.description;
                }

                choice.button.onClick.RemoveAllListeners();
                choice.button.onClick.AddListener(() => onSelected?.Invoke(option));
            }
        }

        public void HideLevelUp()
        {
            if (levelUpRoot != null)
            {
                levelUpRoot.SetActive(false);
            }
        }

        public void ShowDefeat(Action onRetry)
        {
            HideAll();
            if (defeatRoot != null)
            {
                defeatRoot.SetActive(true);
            }

            if (retryButton != null)
            {
                retryButton.onClick.RemoveAllListeners();
                retryButton.onClick.AddListener(() => onRetry?.Invoke());
            }
        }

        public void ShowVictory(Action onContinue)
        {
            HideAll();
            if (victoryRoot != null)
            {
                victoryRoot.SetActive(true);
            }

            if (continueButton != null)
            {
                continueButton.onClick.RemoveAllListeners();
                continueButton.onClick.AddListener(() => onContinue?.Invoke());
            }
        }
    }

    [Serializable]
    public struct HeroPrefabEntry
    {
        public string heroId;
        public HeroPresenter presenterPrefab;
    }

    [CreateAssetMenu(menuName = "Dice Battler/Registries/Hero Prefab Registry", fileName = "HeroPrefabRegistry")]
    public sealed class HeroPrefabRegistry : ScriptableObject
    {
        public List<HeroPrefabEntry> entries = new List<HeroPrefabEntry>();
    }

    [Serializable]
    public struct MobPrefabEntry
    {
        public string prefabKey;
        public EnemyPresenter presenterPrefab;
    }

    [CreateAssetMenu(menuName = "Dice Battler/Registries/Mob Prefab Registry", fileName = "MobPrefabRegistry")]
    public sealed class MobPrefabRegistry : ScriptableObject
    {
        public List<MobPrefabEntry> entries = new List<MobPrefabEntry>();
    }

    [CreateAssetMenu(menuName = "Dice Battler/Registries/UI Skin Registry", fileName = "UISkinRegistry")]
    public sealed class UISkinRegistry : ScriptableObject
    {
        public CombatHudPresenter hudPrefab;
        public OverlayController overlayPrefab;
    }

    [CreateAssetMenu(menuName = "Dice Battler/Registries/Vfx Registry", fileName = "VfxRegistry")]
    public sealed class VfxRegistry : ScriptableObject
    {
        public GameObject diceToDamageSwarm;
        public GameObject expSwarm;
        public GameObject hitVfx;
        public GameObject buttonTapVfx;
    }
}
