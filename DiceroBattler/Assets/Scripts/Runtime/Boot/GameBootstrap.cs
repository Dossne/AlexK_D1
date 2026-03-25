using DiceBattler.Configs;
using DiceBattler.Presentation;
using UnityEngine;

namespace DiceBattler.Boot
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private PrototypeContentSet contentSet;
        [SerializeField] private CombatSceneInstaller combatSceneInstaller;

        private void Awake()
        {
            PrototypeContentSet importedContentSet = Resources.Load<PrototypeContentSet>("DiceBattler/PrototypeContentSet");
            if (importedContentSet != null)
            {
                contentSet = importedContentSet;
            }

            if (contentSet == null)
            {
                contentSet = PrototypeRuntimeFactory.CreateInMemoryContentSet();
                Debug.LogWarning("GameBootstrap is using an in-memory default content set because no PrototypeContentSet asset was assigned or found in Resources.");
            }

            if (combatSceneInstaller == null)
            {
                combatSceneInstaller = FindFirstObjectByType<CombatSceneInstaller>();
                if (combatSceneInstaller == null)
                {
                    GameObject installerRoot = new GameObject("Combat Scene Installer");
                    combatSceneInstaller = installerRoot.AddComponent<CombatSceneInstaller>();
                }
            }

            if (contentSet != null && contentSet.runConfig != null)
            {
                Debug.Log($"GameBootstrap using content set '{contentSet.name}' with totalWaves={contentSet.runConfig.totalWaves}.");
            }

            combatSceneInstaller.Initialize(contentSet);
        }
    }
}
