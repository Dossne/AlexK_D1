using DiceBattler.Configs;
using DiceBattler.Runtime;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DiceBattler.Presentation
{
    public sealed class CombatSceneInstaller : MonoBehaviour
    {
        [SerializeField] private PrototypeContentSet contentSet;
        [SerializeField] private CombatHudPresenter hudPresenter;
        [SerializeField] private HeroPresenter heroPresenter;
        [SerializeField] private EnemyPresenter[] enemyPresenters;
        [SerializeField] private DiePresenter[] diePresenters;
        [SerializeField] private OverlayController overlayController;
        [SerializeField] private CombatFlowController flowController;

        public void Initialize(PrototypeContentSet injectedContentSet)
        {
            contentSet = injectedContentSet;
            EnsureScenePresentation();

            if (flowController == null)
            {
                flowController = GetComponent<CombatFlowController>();
                if (flowController == null)
                {
                    flowController = gameObject.AddComponent<CombatFlowController>();
                }
            }

            flowController.Initialize(contentSet, hudPresenter, heroPresenter, enemyPresenters, diePresenters, overlayController);
        }

        private void EnsureScenePresentation()
        {
            if (hudPresenter != null
                && heroPresenter != null
                && overlayController != null
                && enemyPresenters != null
                && enemyPresenters.Length == 3
                && diePresenters != null
                && diePresenters.Length == 5)
            {
                return;
            }

            PlaceholderSceneGraph graph = PlaceholderSceneGraph.Create(transform);
            hudPresenter = graph.HudPresenter;
            heroPresenter = graph.HeroPresenter;
            enemyPresenters = graph.EnemyPresenters;
            diePresenters = graph.DiePresenters;
            overlayController = graph.OverlayController;
        }
    }

    internal sealed class PlaceholderSceneGraph
    {
        private const string DamageDealtLabelPrefabPath = "DiceBattler/UI/DamageDealtLabel";
        private const string DamageTakenLabelPrefabPath = "DiceBattler/UI/DamageTakenLabel";

        public CombatHudPresenter HudPresenter { get; private set; }
        public HeroPresenter HeroPresenter { get; private set; }
        public EnemyPresenter[] EnemyPresenters { get; private set; }
        public DiePresenter[] DiePresenters { get; private set; }
        public OverlayController OverlayController { get; private set; }

        public static PlaceholderSceneGraph Create(Transform parent)
        {
            EnsureEventSystem();

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            GameObject canvasGo = new GameObject("PrototypeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(parent, false);
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            CreatePanelImage(canvasGo.transform, "Background", new Color(0.09f, 0.11f, 0.16f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            CombatHudPresenter hud = CreateHud(canvasGo.transform, font, out Button attackButton);
            HeroPresenter hero = CreateHero(canvasGo.transform, font);
            EnemyPresenter[] enemies = CreateEnemies(canvasGo.transform, font);
            DiePresenter[] dice = CreateDice(canvasGo.transform, font);
            OverlayController overlays = CreateOverlays(canvasGo.transform, font);

            hud.BindAttack(null);

            return new PlaceholderSceneGraph
            {
                HudPresenter = hud,
                HeroPresenter = hero,
                EnemyPresenters = enemies,
                DiePresenters = dice,
                OverlayController = overlays,
            };
        }

        private static CombatHudPresenter CreateHud(Transform canvasRoot, Font font, out Button attackButton)
        {
            GameObject hudRoot = CreateRectObject(canvasRoot, "HUD", new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            CombatHudPresenter presenter = hudRoot.AddComponent<CombatHudPresenter>();

            Slider expBar = CreateSlider(hudRoot.transform, "ExpBar", new Vector2(0.07f, 0.94f), new Vector2(0.68f, 0.97f), new Color(0.35f, 0.8f, 0.4f));
            Text levelText = CreateText(hudRoot.transform, "LevelText", font, "Level 1", 30, TextAnchor.MiddleLeft, new Vector2(0.07f, 0.9f), new Vector2(0.35f, 0.935f));
            Text waveText = CreateText(hudRoot.transform, "WaveText", font, "Waves 1/3", 30, TextAnchor.MiddleRight, new Vector2(0.65f, 0.9f), new Vector2(0.93f, 0.935f));
            Text damagePreview = CreateText(hudRoot.transform, "DamagePreview", font, "Damage value: 0 No Combination x1 + Bonus 0", 28, TextAnchor.MiddleCenter, new Vector2(0.12f, 0.33f), new Vector2(0.88f, 0.39f));
            Text rerollText = CreateText(hudRoot.transform, "RerollText", font, "3/3", 28, TextAnchor.MiddleCenter, new Vector2(0.18f, 0.16f), new Vector2(0.36f, 0.21f));
            Text damageDealt = CreateDamageLabel(
                hudRoot.transform,
                font,
                DamageDealtLabelPrefabPath,
                "DamageDealt",
                "Damage dealt: 0",
                new Vector2(0.04f, 0.5f),
                new Vector2(0.24f, 0.72f));
            Text damageTaken = CreateDamageLabel(
                hudRoot.transform,
                font,
                DamageTakenLabelPrefabPath,
                "DamageTaken",
                "Damage taken: 0",
                new Vector2(0.76f, 0.5f),
                new Vector2(0.96f, 0.72f));

            attackButton = CreateButton(hudRoot.transform, font, "AttackButton", "ATTACK", new Vector2(0.62f, 0.12f), new Vector2(0.9f, 0.2f), new Color(0.88f, 0.34f, 0.16f));
            presenter.Configure(expBar, levelText, waveText, rerollText, damagePreview, damageDealt, damageTaken, attackButton);
            return presenter;
        }

        private static Text CreateDamageLabel(
            Transform parent,
            Font font,
            string resourcePath,
            string fallbackName,
            string initialText,
            Vector2 fallbackAnchorMin,
            Vector2 fallbackAnchorMax)
        {
            GameObject prefab = Resources.Load<GameObject>(resourcePath);
            GameObject labelObject = prefab != null
                ? Object.Instantiate(prefab, parent, false)
                : CreateRectObject(parent, fallbackName, fallbackAnchorMin, fallbackAnchorMax, Vector2.zero, Vector2.zero);

            labelObject.name = fallbackName;

            if (labelObject.GetComponent<CanvasRenderer>() == null)
            {
                labelObject.AddComponent<CanvasRenderer>();
            }

            Text label = labelObject.GetComponent<Text>();
            if (label == null)
            {
                label = labelObject.AddComponent<Text>();
            }

            label.font = font;
            label.text = initialText;
            label.fontSize = 28;
            label.alignment = TextAnchor.UpperCenter;
            label.color = Color.white;
            label.raycastTarget = false;

            return label;
        }

        private static HeroPresenter CreateHero(Transform canvasRoot, Font font)
        {
            GameObject root = CreatePanelImage(canvasRoot, "Hero", new Color(0.2f, 0.35f, 0.55f), new Vector2(0.33f, 0.42f), new Vector2(0.67f, 0.62f), Vector2.zero, Vector2.zero);
            HeroPresenter presenter = root.AddComponent<HeroPresenter>();
            Text label = CreateText(root.transform, "HeroLabel", font, "HERO", 36, TextAnchor.MiddleCenter, new Vector2(0.15f, 0.25f), new Vector2(0.85f, 0.8f));
            Slider hpBar = CreateSlider(root.transform, "HeroHpBar", new Vector2(0.15f, 0.08f), new Vector2(0.85f, 0.2f), new Color(0.91f, 0.24f, 0.24f));
            GameObject damageAnchor = CreateRectObject(root.transform, "DamageAnchor", new Vector2(0.5f, 0.85f), new Vector2(0.5f, 0.85f), new Vector2(0f, 0f), new Vector2(0f, 0f));
            GameObject hitFxAnchor = CreateRectObject(root.transform, "HitFxAnchor", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(0f, 0f));
            presenter.Configure(null, hpBar, hpBar.transform, damageAnchor.transform, hitFxAnchor.transform);
            return presenter;
        }

        private static EnemyPresenter[] CreateEnemies(Transform canvasRoot, Font font)
        {
            EnemyPresenter[] presenters = new EnemyPresenter[3];
            Vector2[] mins = { new Vector2(0.08f, 0.66f), new Vector2(0.38f, 0.73f), new Vector2(0.68f, 0.66f) };
            Vector2[] maxs = { new Vector2(0.28f, 0.82f), new Vector2(0.62f, 0.89f), new Vector2(0.88f, 0.82f) };

            for (int index = 0; index < presenters.Length; index++)
            {
                GameObject root = CreatePanelImage(canvasRoot, $"Enemy_{index + 1}", new Color(0.42f, 0.22f, 0.26f), mins[index], maxs[index], Vector2.zero, Vector2.zero);
                EnemyPresenter presenter = root.AddComponent<EnemyPresenter>();
                Text nameText = CreateText(root.transform, "Name", font, $"Enemy {index + 1}", 24, TextAnchor.MiddleCenter, new Vector2(0.1f, 0.58f), new Vector2(0.9f, 0.9f));
                Text intentText = CreateText(root.transform, "Intent", font, "0", 22, TextAnchor.MiddleCenter, new Vector2(0.32f, 0.18f), new Vector2(0.68f, 0.42f));
                Slider hpBar = CreateSlider(root.transform, "HpBar", new Vector2(0.12f, 0.04f), new Vector2(0.88f, 0.14f), new Color(0.83f, 0.18f, 0.2f));
                GameObject intentAnchor = CreateRectObject(root.transform, "IntentAnchor", new Vector2(0.5f, 0.3f), new Vector2(0.5f, 0.3f), Vector2.zero, Vector2.zero);
                GameObject damageAnchor = CreateRectObject(root.transform, "DamageAnchor", new Vector2(0.5f, 0.95f), new Vector2(0.5f, 0.95f), Vector2.zero, Vector2.zero);
                GameObject hitFxAnchor = CreateRectObject(root.transform, "HitFxAnchor", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                presenter.Configure(null, hpBar, intentText, nameText, hpBar.transform, intentAnchor.transform, damageAnchor.transform, hitFxAnchor.transform);
                presenters[index] = presenter;
            }

            return presenters;
        }

        private static DiePresenter[] CreateDice(Transform canvasRoot, Font font)
        {
            DiePresenter[] presenters = new DiePresenter[5];
            for (int index = 0; index < presenters.Length; index++)
            {
                float xMin = 0.09f + index * 0.17f;
                float xMax = xMin + 0.13f;
                GameObject root = CreatePanelImage(canvasRoot, $"Die_{index + 1}", new Color(0.86f, 0.82f, 0.7f), new Vector2(xMin, 0.22f), new Vector2(xMax, 0.3f), Vector2.zero, Vector2.zero);
                CanvasGroup canvasGroup = root.AddComponent<CanvasGroup>();
                Button button = root.AddComponent<Button>();
                ColorBlock colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1f, 0.95f, 0.7f);
                colors.pressedColor = new Color(0.92f, 0.82f, 0.52f);
                button.colors = colors;
                DiePresenter presenter = root.AddComponent<DiePresenter>();
                Text valueText = CreateText(root.transform, "Value", font, "X", 32, TextAnchor.MiddleCenter, new Vector2(0f, 0f), new Vector2(1f, 1f));
                GameObject highlight = CreatePanelImage(root.transform, "Highlight", new Color(1f, 0.94f, 0.45f, 0.45f), new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
                highlight.transform.SetAsFirstSibling();
                GameObject locked = CreatePanelImage(root.transform, "Lock", new Color(0.1f, 0.1f, 0.1f, 0.7f), new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
                Text lockText = CreateText(locked.transform, "LockText", font, "LOCK", 18, TextAnchor.MiddleCenter, new Vector2(0f, 0f), new Vector2(1f, 1f));
                presenter.Configure(valueText, button.targetGraphic != null ? button.targetGraphic : root.GetComponent<Image>(), highlight, locked, canvasGroup);
                presenters[index] = presenter;
            }

            return presenters;
        }

        private static OverlayController CreateOverlays(Transform canvasRoot, Font font)
        {
            GameObject overlayRoot = CreateRectObject(canvasRoot, "Overlays", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            OverlayController controller = overlayRoot.AddComponent<OverlayController>();

            GameObject levelUpRoot = CreatePanelImage(overlayRoot.transform, "LevelUpOverlay", new Color(0f, 0f, 0f, 0.75f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            CreateText(levelUpRoot.transform, "Title", font, "Choose Upgrade", 42, TextAnchor.MiddleCenter, new Vector2(0.2f, 0.7f), new Vector2(0.8f, 0.82f));
            UpgradeChoiceView[] choices = new UpgradeChoiceView[3];
            for (int index = 0; index < choices.Length; index++)
            {
                float yMax = 0.62f - index * 0.17f;
                float yMin = yMax - 0.13f;
                Button choiceButton = CreateButton(levelUpRoot.transform, font, $"Choice_{index + 1}", "Upgrade", new Vector2(0.14f, yMin), new Vector2(0.86f, yMax), new Color(0.18f, 0.28f, 0.46f));
                Text titleText = choiceButton.GetComponentInChildren<Text>();
                Text descriptionText = CreateText(choiceButton.transform, "Description", font, "", 18, TextAnchor.LowerCenter, new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.45f));
                choices[index] = new UpgradeChoiceView
                {
                    button = choiceButton,
                    titleText = titleText,
                    descriptionText = descriptionText,
                };
            }

            GameObject defeatRoot = CreatePanelImage(overlayRoot.transform, "DefeatOverlay", new Color(0f, 0f, 0f, 0.75f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            CreateText(defeatRoot.transform, "Title", font, "Defeat", 56, TextAnchor.MiddleCenter, new Vector2(0.25f, 0.56f), new Vector2(0.75f, 0.68f));
            Button retryButton = CreateButton(defeatRoot.transform, font, "RetryButton", "Retry", new Vector2(0.34f, 0.36f), new Vector2(0.66f, 0.44f), new Color(0.7f, 0.2f, 0.2f));

            GameObject victoryRoot = CreatePanelImage(overlayRoot.transform, "VictoryOverlay", new Color(0f, 0f, 0f, 0.75f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            CreateText(victoryRoot.transform, "Title", font, "Victory", 56, TextAnchor.MiddleCenter, new Vector2(0.25f, 0.56f), new Vector2(0.75f, 0.68f));
            Button continueButton = CreateButton(victoryRoot.transform, font, "ContinueButton", "Continue", new Vector2(0.3f, 0.36f), new Vector2(0.7f, 0.44f), new Color(0.2f, 0.55f, 0.25f));

            controller.Configure(levelUpRoot, choices, defeatRoot, retryButton, victoryRoot, continueButton);
            controller.HideAll();
            return controller;
        }

        private static void EnsureEventSystem()
        {
            EventSystem existingEventSystem = Object.FindFirstObjectByType<EventSystem>();
            if (existingEventSystem != null)
            {
                StandaloneInputModule legacyModule = existingEventSystem.GetComponent<StandaloneInputModule>();
                if (legacyModule != null)
                {
                    Object.Destroy(legacyModule);
                }

                if (existingEventSystem.GetComponent<InputSystemUIInputModule>() == null)
                {
                    existingEventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
                }

                return;
            }

            GameObject eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Object.DontDestroyOnLoad(eventSystemGo);
        }

        private static GameObject CreateRectObject(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return gameObject;
        }

        private static GameObject CreatePanelImage(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject gameObject = CreateRectObject(parent, name, anchorMin, anchorMax, offsetMin, offsetMax);
            Image image = gameObject.AddComponent<Image>();
            image.color = color;
            return gameObject;
        }

        private static Text CreateText(Transform parent, string name, Font font, string textValue, int fontSize, TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject gameObject = CreateRectObject(parent, name, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            Text text = gameObject.AddComponent<Text>();
            text.font = font;
            text.text = textValue;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static Button CreateButton(Transform parent, Font font, string name, string label, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            GameObject gameObject = CreatePanelImage(parent, name, color, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            Button button = gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = color * 1.1f;
            colors.pressedColor = color * 0.9f;
            button.colors = colors;
            CreateText(gameObject.transform, "Label", font, label, 28, TextAnchor.MiddleCenter, new Vector2(0f, 0f), new Vector2(1f, 1f));
            return button;
        }

        private static Slider CreateSlider(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color fillColor)
        {
            GameObject root = CreateRectObject(parent, name, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            Image background = root.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.4f);

            GameObject fillArea = CreateRectObject(root.transform, "Fill Area", new Vector2(0.02f, 0.2f), new Vector2(0.98f, 0.8f), Vector2.zero, Vector2.zero);
            GameObject fill = CreatePanelImage(fillArea.transform, "Fill", fillColor, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            Slider slider = root.AddComponent<Slider>();
            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.targetGraphic = fill.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            return slider;
        }
    }
}
