#if UNITY_EDITOR
using System.Collections.Generic;
using DiceBattler.Configs;
using DiceBattler.Presentation;
using UnityEditor;
using UnityEngine;

namespace DiceBattler.EditorTools
{
    public static class PrefabContractValidator
    {
        [MenuItem("Tools/Dice Battler/Validate Selected Prefab Contracts")]
        public static void ValidateSelection()
        {
            Object[] selection = Selection.objects;
            if (selection == null || selection.Length == 0)
            {
                Debug.LogWarning("Select one or more prefabs or content assets first.");
                return;
            }

            List<string> messages = new List<string>();
            for (int index = 0; index < selection.Length; index++)
            {
                ValidateObject(selection[index], messages);
            }

            if (messages.Count == 0)
            {
                Debug.Log("Prefab contract validation passed.");
                return;
            }

            for (int index = 0; index < messages.Count; index++)
            {
                Debug.LogError(messages[index], selection.Length > index ? selection[index] : null);
            }
        }

        private static void ValidateObject(Object selectedObject, List<string> messages)
        {
            if (selectedObject is GameObject selectedGameObject)
            {
                HeroPresenter hero = selectedGameObject.GetComponent<HeroPresenter>();
                if (hero != null)
                {
                    ValidateHero(hero, messages);
                }

                EnemyPresenter enemy = selectedGameObject.GetComponent<EnemyPresenter>();
                if (enemy != null)
                {
                    ValidateEnemy(enemy, messages);
                }

                DiePresenter die = selectedGameObject.GetComponent<DiePresenter>();
                if (die != null)
                {
                    ValidateDie(die, messages);
                }

                CombatHudPresenter hud = selectedGameObject.GetComponent<CombatHudPresenter>();
                if (hud != null)
                {
                    ValidateHud(hud, messages);
                }

                OverlayController overlay = selectedGameObject.GetComponent<OverlayController>();
                if (overlay != null)
                {
                    ValidateOverlay(overlay, messages);
                }
            }

            if (selectedObject is PrototypeContentSet contentSet)
            {
                ValidateRegistries(contentSet, messages);
            }
        }

        private static void ValidateHero(HeroPresenter hero, List<string> messages)
        {
            if (hero.GetComponent<Animator>() == null)
            {
                messages.Add($"{hero.name}: Hero prefab is missing Animator.");
            }
        }

        private static void ValidateEnemy(EnemyPresenter enemy, List<string> messages)
        {
            if (enemy.GetComponent<Animator>() == null)
            {
                messages.Add($"{enemy.name}: Enemy prefab is missing Animator.");
            }
        }

        private static void ValidateDie(DiePresenter die, List<string> messages)
        {
            if (die.GetComponent<CanvasGroup>() == null)
            {
                messages.Add($"{die.name}: Die prefab should include CanvasGroup for state gating.");
            }
        }

        private static void ValidateHud(CombatHudPresenter hud, List<string> messages)
        {
            if (hud.GetComponentInChildren<UnityEngine.UI.Button>() == null)
            {
                messages.Add($"{hud.name}: HUD prefab is missing an attack button binding.");
            }
        }

        private static void ValidateOverlay(OverlayController overlay, List<string> messages)
        {
            if (overlay.GetComponentsInChildren<UnityEngine.UI.Button>(true).Length < 2)
            {
                messages.Add($"{overlay.name}: Overlay prefab is missing required CTA buttons.");
            }
        }

        private static void ValidateRegistries(PrototypeContentSet contentSet, List<string> messages)
        {
            if (contentSet.heroPrefabRegistry == null)
            {
                messages.Add($"{contentSet.name}: HeroPrefabRegistry is missing.");
            }

            if (contentSet.mobPrefabRegistry == null)
            {
                messages.Add($"{contentSet.name}: MobPrefabRegistry is missing.");
            }

            if (contentSet.uiSkinRegistry == null)
            {
                messages.Add($"{contentSet.name}: UISkinRegistry is missing.");
            }
        }
    }
}
#endif
