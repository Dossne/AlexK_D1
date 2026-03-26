using DiceBattler.Configs;
using DiceBattler.Importing;
using DiceBattler.Presentation;
using UnityEngine;

namespace DiceBattler.Boot
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        private const string ImportedResourcesRoot = "DiceBattler/Imported";
        private const string ImportedSnapshotResourcePath = "DiceBattler/ImportedGameData";

        [SerializeField] private PrototypeContentSet contentSet;
        [SerializeField] private CombatSceneInstaller combatSceneInstaller;

        private void Awake()
        {
            PrototypeContentSet resourcesContentSet = Resources.Load<PrototypeContentSet>("DiceBattler/PrototypeContentSet");
            PrototypeContentSet importedSnapshotContentSet = TryCreateImportedSnapshotContentSet(resourcesContentSet != null ? resourcesContentSet : contentSet);
            PrototypeContentSet importedResourcesContentSet = importedSnapshotContentSet != null
                ? importedSnapshotContentSet
                : TryCreateImportedResourcesContentSet(resourcesContentSet != null ? resourcesContentSet : contentSet);

            if (importedResourcesContentSet != null)
            {
                contentSet = importedResourcesContentSet;
            }
            else if (resourcesContentSet != null)
            {
                contentSet = resourcesContentSet;
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

        private static PrototypeContentSet TryCreateImportedSnapshotContentSet(PrototypeContentSet registrySource)
        {
            TextAsset snapshotAsset = Resources.Load<TextAsset>(ImportedSnapshotResourcePath);
            if (snapshotAsset == null || string.IsNullOrWhiteSpace(snapshotAsset.text))
            {
                return null;
            }

            ImportedGameData data = JsonUtility.FromJson<ImportedGameData>(snapshotAsset.text);
            return PrototypeRuntimeFactory.CreateImportedContentSet(data, registrySource);
        }

        private static PrototypeContentSet TryCreateImportedResourcesContentSet(PrototypeContentSet registrySource)
        {
            HeroConfig heroConfig = Resources.Load<HeroConfig>($"{ImportedResourcesRoot}/HeroConfig");
            MobDatabase mobDatabase = Resources.Load<MobDatabase>($"{ImportedResourcesRoot}/MobDatabase");
            WaveDatabase waveDatabase = Resources.Load<WaveDatabase>($"{ImportedResourcesRoot}/WaveDatabase");
            CombinationDatabase combinationDatabase = Resources.Load<CombinationDatabase>($"{ImportedResourcesRoot}/CombinationDatabase");
            UpgradeDatabase upgradeDatabase = Resources.Load<UpgradeDatabase>($"{ImportedResourcesRoot}/UpgradeDatabase");
            ProgressionDatabase progressionDatabase = Resources.Load<ProgressionDatabase>($"{ImportedResourcesRoot}/ProgressionDatabase");
            RunConfig runConfig = Resources.Load<RunConfig>($"{ImportedResourcesRoot}/RunConfig");

            if (heroConfig == null
                || mobDatabase == null
                || waveDatabase == null
                || combinationDatabase == null
                || upgradeDatabase == null
                || progressionDatabase == null
                || runConfig == null)
            {
                return null;
            }

            PrototypeContentSet importedContentSet = ScriptableObject.CreateInstance<PrototypeContentSet>();
            importedContentSet.name = "ImportedRuntimeContentSet";
            importedContentSet.heroConfig = heroConfig;
            importedContentSet.mobDatabase = mobDatabase;
            importedContentSet.waveDatabase = waveDatabase;
            importedContentSet.combinationDatabase = combinationDatabase;
            importedContentSet.upgradeDatabase = upgradeDatabase;
            importedContentSet.progressionDatabase = progressionDatabase;
            importedContentSet.runConfig = runConfig;
            importedContentSet.heroPrefabRegistry = registrySource != null ? registrySource.heroPrefabRegistry : ScriptableObject.CreateInstance<HeroPrefabRegistry>();
            importedContentSet.mobPrefabRegistry = registrySource != null ? registrySource.mobPrefabRegistry : ScriptableObject.CreateInstance<MobPrefabRegistry>();
            importedContentSet.uiSkinRegistry = registrySource != null ? registrySource.uiSkinRegistry : ScriptableObject.CreateInstance<UISkinRegistry>();
            importedContentSet.vfxRegistry = registrySource != null ? registrySource.vfxRegistry : ScriptableObject.CreateInstance<VfxRegistry>();
            return importedContentSet;
        }
    }
}
