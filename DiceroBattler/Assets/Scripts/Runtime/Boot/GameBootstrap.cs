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
            if (contentSet == null || combatSceneInstaller == null)
            {
                Debug.LogError("GameBootstrap is missing required references.");
                return;
            }

            combatSceneInstaller.Initialize(contentSet);
        }
    }
}
